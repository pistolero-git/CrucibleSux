using System;
using System.Linq;
using CrucibleSux.Automation;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Integrations;

/// <summary>
/// Rotation Solver Reborn's IPC: turns its rotation on around fights the journey starts.
/// Manual mode requires a target BEFORE the mode change; a mob approached by vnavmesh
/// otherwise leaves RSR on with nothing to attack.
/// </summary>
public sealed class RotationSolverIpc(IDalamudPluginInterface pi, IPluginLog log, Action<string, bool> note, Func<bool>? boardOwnsRotation = null)
{
    private static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResendNoteEvery = TimeSpan.FromSeconds(30);

    public enum Mode : byte
    {
        Off = 0,
        Auto = 1,
        Manual = 3,
    }

    private readonly ICallGateSubscriber<Mode, object> changeMode = pi.GetIpcSubscriber<Mode, object>("RotationSolverReborn.ChangeOperatingMode");
    private readonly ICallGateSubscriber<object> targetFreelyOn = pi.GetIpcSubscriber<object>("RotationSolverReborn.EnableTargetFreelyOverride");
    private readonly ICallGateSubscriber<object> targetFreelyOff = pi.GetIpcSubscriber<object>("RotationSolverReborn.DisableTargetFreelyOverride");
    private readonly ICallGateSubscriber<bool> active = pi.GetIpcSubscriber<bool>("RotationSolverReborn.AutorotationActive");
    private DateTime sentAt;
    private DateTime nextCheck;
    private DateTime failedUntil;
    private string? lastFailure;
    private DateTime resendNotedAt;
    private int resendsUnnoted;
    private Mode? current;
    private bool? freely;
    private bool targetWarned;
    private ulong manualSelectedTarget;
    private DateTime manualSelectedSince;
    private bool manualStallWarned;
    private GameActions? manualActions;

    public Mode? Requested { get; private set; }

    public bool Set(Mode mode, bool targetFreely = false)
    {
        // A managed board owns RSR, including during journey stop/cleanup.
        // Never overwrite AutoDuty mode with Off, Manual, or journey Auto.
        if (!CrucibleSux.Core.BoardRotationRecovery.JourneyMayChangeRotation(boardOwnsRotation?.Invoke() == true))
            return false;

        var now = DateTime.UtcNow;
        this.Requested = mode;
        if (now < this.failedUntil)
            return false;

        // The existing leveling path reaches the mob before assigning a target. In Manual mode,
        // RSR will not choose a target itself. Choose a hostile ALREADY in melee range before IPC.
        // Capture, quest and defensive fighting paths already target explicitly, so preserve those.
        if (mode == Mode.Manual)
        {
            this.EnsureManualTarget();
            this.WatchManualEngagement(now);
        }
        else
        {
            this.manualSelectedTarget = 0;
            this.manualStallWarned = false;
        }

        bool failed = false;
        try
        {
            if (this.current == mode && mode != Mode.Off && now - this.sentAt > ResendAfter && now >= this.nextCheck)
            {
                this.nextCheck = now + CheckEvery;
                if (this.Active() == false)
                {
                    this.NoteResend(now, mode);
                    this.current = null;
                    this.freely = null;
                }
            }
            if (this.current != mode)
            {
                this.changeMode.InvokeAction(mode);
                this.current = mode;
                this.sentAt = now;
                note(mode == Mode.Off ? "Rotation Solver Reborn: Off" : $"Rotation Solver Reborn: {mode}{(targetFreely ? ", picking its own targets" : string.Empty)}", false);
            }
        }
        catch (Exception ex)
        {
            this.Failed(now, $"the {mode} call", ex);
            this.failedUntil = now + RetryAfter;
            this.current = null;
            return false;
        }
        try
        {
            if (this.freely != targetFreely)
            {
                (targetFreely ? this.targetFreelyOn : this.targetFreelyOff).InvokeAction();
                this.freely = targetFreely;
            }
        }
        catch (Exception ex)
        {
            this.Failed(now, "the target override", ex);
            this.freely = targetFreely;
            failed = true;
        }
        if (!failed)
            this.lastFailure = null;
        return true;
    }

    private void EnsureManualTarget()
    {
        try
        {
            if (Plugin.ObjectTable.LocalPlayer is not { } me)
                return;
            // An existing valid target (the capture prey, an attacker, or a training dummy) wins.
            if (me.TargetObject is IBattleNpc { IsTargetable: true } current && !current.IsDead
                && (current.Position - me.Position).Length() - current.HitboxRadius <= 4f)
                return;

            var mob = Plugin.ObjectTable.OfType<IBattleNpc>()
                .Where(x => GameActions.IsHostile(x) && !x.IsDead && x.CurrentHp > 0 && x.IsTargetable
                    && !GameActions.IsTrainingDummy(x)
                    && (x.Position - me.Position).Length() - x.HitboxRadius <= 3.5f)
                .OrderBy(x => (x.Position - me.Position).Length() - x.HitboxRadius)
                .FirstOrDefault();
            if (mob is null)
                return;
            this.manualActions ??= new GameActions(Plugin.ObjectTable, Plugin.Condition, Plugin.GameGui, Plugin.PlayerState);
            this.manualActions.Target(mob);
            if (this.manualSelectedTarget != mob.GameObjectId)
            {
                this.manualSelectedTarget = mob.GameObjectId;
                this.manualSelectedSince = DateTime.UtcNow;
                this.manualStallWarned = false;
                note($"Combat handoff: targeted {mob.Name.TextValue} before asking RSR for Manual", false);
            }
        }
        catch (Exception ex)
        {
            // Target acquisition should never disable the rotation; report the failure once in the plugin log.
            if (!this.targetWarned)
            {
                this.targetWarned = true;
                log.Warning(ex, "Could not preselect an adjacent hostile for RSR Manual");
            }
        }
    }

    private void WatchManualEngagement(DateTime now)
    {
        if (this.manualSelectedTarget == 0 || this.manualStallWarned || now - this.manualSelectedSince < TimeSpan.FromSeconds(10))
            return;
        this.manualStallWarned = true;
        if (Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
            return;
        if (Plugin.ObjectTable.LocalPlayer?.TargetObject?.GameObjectId != this.manualSelectedTarget
            || Plugin.ObjectTable.SearchById(this.manualSelectedTarget) is not IBattleNpc mob || mob.IsDead || !mob.IsTargetable)
            return;
        note($"Combat handoff stalled for {mob.Name.TextValue} after 10 s. Retrying auto-attack and checking RSR.", true);
        this.manualActions?.EnsureAutoAttack(mob);
        this.current = null; // Reassert mode on this same tick.
    }

    public void ForceOff()
    {
        if (!CrucibleSux.Core.BoardRotationRecovery.JourneyMayChangeRotation(boardOwnsRotation?.Invoke() == true))
            return;

        this.current = null;
        this.freely = null;
        this.failedUntil = default;
        this.Set(Mode.Off);
    }

    public bool? Active()
    {
        try { return this.active.InvokeFunc(); }
        catch (Exception) { return null; }
    }

    public void Reassert()
    {
        this.current = null;
        this.freely = null;
    }

    public void Release()
    {
        if (this.current == Mode.Off && this.freely == false)
            return;
        this.Set(Mode.Off);
    }

    private void NoteResend(DateTime now, Mode mode)
    {
        if (now - this.resendNotedAt < ResendNoteEvery)
        {
            this.resendsUnnoted++;
            return;
        }
        string more = this.resendsUnnoted > 0 ? $" ({this.resendsUnnoted} more times since the last note)" : string.Empty;
        note($"Rotation Solver Reborn is off though it was asked for {mode}: asking again{more}", false);
        this.resendNotedAt = now;
        this.resendsUnnoted = 0;
    }

    private void Failed(DateTime now, string what, Exception ex)
    {
        string text = $"Rotation Solver Reborn did not take {what}: {ex.Message}";
        if (text == this.lastFailure)
            return;
        this.lastFailure = text;
        log.Warning(ex, "Rotation Solver Reborn IPC call failed");
        note(text, true);
    }
}
