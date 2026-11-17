using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Integrations;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Automation;

/// <summary>
/// Runs the session queue: observes the game each tick, feeds the <see cref="Orchestrator"/>, carries out its commands
/// through AutoDuty, stores every run in the history and reports when the session ends (docs/PLAN.md F5, F9-F12).
/// </summary>
public sealed class SessionRunner : IDisposable
{
    private const string ResultAddon = "XBMResult";
    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ProbeEvery = TimeSpan.FromSeconds(2);

    /// <summary>Ticks in a row AutoDuty may fail to answer (5 s) before it counts as stopped.</summary>
    private const int UnansweredLimit = 10;

    /// <summary>The Crucible windows whose opening and closing count as progress for the watchdog.</summary>
    private static readonly string[] BoardAddons =
        ["XBMPetParty", "XBMStageDetailList", "XBMContentsBooty", "XBMContentsTreasure", ResultAddon, "XBMContentsItemShop", "SelectYesno"];

    private readonly Session session;
    private readonly AutoDutyIpc autoDuty;
    private readonly BossModIpc bossMod;
    private readonly BoardRotationIpc boardRotationIpc;
    private readonly BoardRotationRecovery boardRotationRecovery = new();
    private readonly Configuration config;
    private readonly Action save;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IGameGui gameGui;
    private readonly IPlayerState player;
    private readonly IChatGui chat;
    private readonly INotificationManager notifications;

    private DateTime nextTick;
    private DateTime nextProbe;
    private string? executorProblem;
    private string rotationPlugin = string.Empty;
    private ulong character;
    private int saved;
    private int unanswered;
    private bool boardRotationNoted;
    private bool? lastBoardRotationActive;

    public SessionRunner(
        Session session,
        AutoDutyIpc autoDuty,
        BossModIpc bossMod,
        Configuration config,
        Action save,
        IClientState clientState,
        ICondition condition,
        IObjectTable objects,
        IGameGui gameGui,
        IPlayerState player,
        IChatGui chat,
        INotificationManager notifications)
    {
        this.session = session;
        this.autoDuty = autoDuty;
        this.bossMod = bossMod;
        this.boardRotationIpc = new BoardRotationIpc(Plugin.PluginInterface, Plugin.Log);
        this.config = config;
        this.save = save;
        this.clientState = clientState;
        this.condition = condition;
        this.objects = objects;
        this.gameGui = gameGui;
        this.player = player;
        this.chat = chat;
        this.notifications = notifications;
        this.config.Queue.RemoveAll(e => !IsBoard(e.Board) || e.Runs < 1);
        foreach (var history in this.config.Runs.Values)
            history.RemoveAll(r => !IsBoard(r.Board));
        this.clientState.Logout += this.OnLogout;
    }

    /// <summary>The session in progress, or null.</summary>
    public Orchestrator? Current { get; private set; }

    public SessionReport? LastReport { get; private set; }

    /// <summary>Live handoff status shown in the board runner.</summary>
    public string RotationStatus { get; private set; } = "Waiting for dungeon entry";

    public void Dispose()
    {
        this.clientState.Logout -= this.OnLogout;
        this.Stop("Crucible SUX was unloaded.");
    }

    /// <summary>
    /// The guard rails for <paramref name="queue"/> (the saved queue when null). AutoDuty is asked at most every 2 s unless
    /// <paramref name="fresh"/>. <paramref name="beforeSwitch"/> judges the plugins as they will be once the BossMod the
    /// player does not use is turned off (<see cref="BossModGate"/>); <see cref="Start"/> always judges them as they are.
    /// </summary>
    public IReadOnlyList<ReadinessCheck> Checks(bool fresh = false, IReadOnlyList<QueueEntry>? queue = null, bool beforeSwitch = false)
    {
        queue ??= this.config.Queue;
        var now = DateTime.UtcNow;
        if (fresh || now >= this.nextProbe)
        {
            this.nextProbe = now + ProbeEvery;
            this.executorProblem = this.autoDuty.Problem(queue.Select(e => Boards.ByNumber(e.Board)).Distinct());
        }
        bool onBst = this.player.IsLoaded && this.player.ClassJob.RowId == CrucibleIds.BeastmasterJob;
        bool inDuty = this.condition[ConditionFlag.BoundByDuty] || Boards.ByTerritory(this.clientState.TerritoryType) is not null;
        bool switching = beforeSwitch && this.session.Conflicts.Count > 0;
        return Preflight.Check(new PreflightInput(
            this.session.Snapshot, onBst, inDuty, switching ? this.session.DependenciesWithConflictsOff : this.session.Dependencies,
            this.executorProblem, queue, this.config.RankingsOptIn, switching ? this.session.Conflicts : null));
    }

    /// <summary>
    /// Starts <paramref name="queue"/> (the saved queue when null; the journey passes a single board) when every guard rail
    /// allows it. Returns the checks either way.
    /// </summary>
    public IReadOnlyList<ReadinessCheck> Start(IReadOnlyList<QueueEntry>? queue = null)
    {
        if (this.Current is not null)
            return [];
        queue ??= this.config.Queue;
        this.session.Update(force: true);
        var checks = this.Checks(fresh: true, queue);
        if (!Preflight.CanStart(checks) || AutoDutyIpc.RotationPluginName(DependencyCatalog.RotationEngine(this.session.Dependencies)) is not { } rotation)
            return checks;

        this.rotationPlugin = rotation;
        var settings = OrchestratorSettings.Default with
        {
            MaxAttempts = Math.Clamp(this.config.MaxAttempts, 1, 5),
            StuckAfter = TimeSpan.FromMinutes(Math.Clamp(this.config.StuckMinutes, 2, 15)),
        };
        this.Current = new Orchestrator(queue, settings, DateTime.UtcNow);
        this.character = this.player.ContentId;
        this.saved = 0;
        this.LastReport = null;
        this.boardRotationRecovery.Reset();
        this.boardRotationNoted = false;
        this.lastBoardRotationActive = null;
        this.RotationStatus = "Waiting for dungeon entry";
        int runs = queue.Sum(e => e.Runs);
        Plugin.Log.Information($"Session started: {runs} run(s), rotation {rotation}");
        this.chat.Print($"[Crucible SUX] Session started: {runs} run(s). \"/sux stop\" stops it.");
        return checks;
    }

    public void Stop(string reason)
    {
        if (this.Current is not { } o)
            return;
        this.Execute(o, o.Cancel(DateTime.UtcNow, reason), DateTime.UtcNow);
        this.boardRotationRecovery.Reset();
        this.RotationStatus = "Stopped";
        this.Settle(o);
    }

    /// <summary>Called every framework tick; does its work twice a second.</summary>
    public void Update()
    {
        if (this.Current is not { } o)
            return;
        var now = DateTime.UtcNow;
        if (now < this.nextTick)
            return;
        this.nextTick = now + TickEvery;

        if (this.Observe(now) is not { } observation)
            return;
        this.Execute(o, o.Tick(observation), now);
        if (this.MonitorBoardRotation(o, observation, now))
            return;
        this.Settle(o);
    }

    /// <summary>The game as the orchestrator sees it, or null to skip this tick while the character or AutoDuty cannot be read.</summary>
    private Observation? Observe(DateTime now)
    {
        if (this.session.Snapshot is not { } snapshot)
            return null;
        bool running;
        if (this.autoDuty.IsRunning() is { } answer)
        {
            running = answer;
            this.unanswered = 0;
        }
        else if (++this.unanswered < UnansweredLimit)
        {
            return null;
        }
        else
        {
            running = false;
        }

        var me = this.objects.LocalPlayer;
        var pos = me?.Position ?? Vector3.Zero;
        int open = 0;
        for (int i = 0; i < BoardAddons.Length; i++)
        {
            if (this.gameGui.GetAddonByName(BoardAddons[i]).IsVisible)
                open |= 1 << i;
        }
        long activity = HashCode.Combine((int)MathF.Round(pos.X), (int)MathF.Round(pos.Y), (int)MathF.Round(pos.Z),
            this.condition[ConditionFlag.InCombat], open, me?.CurrentHp ?? 0);

        return new Observation(
            now,
            this.clientState.TerritoryType,
            running,
            this.gameGui.GetAddonByName(ResultAddon).IsVisible,
            activity,
            snapshot.ClearedBoards);
    }

    /// <summary>
    /// Verify the dungeon rotation independently of AutoDuty's accepted Run IPC. The initial
    /// handshake is delayed until the character has entered a live board, and the identical
    /// AutoDuty RSR mode is then asserted once; later dropouts get bounded retries.
    /// </summary>
    private bool MonitorBoardRotation(Orchestrator o, Observation observation, DateTime now)
    {
        bool owned = o.Phase == SessionPhase.InBoard && observation.ExecutorRunning
            && !observation.ResultShown && o.Board?.Territory == observation.Territory
            && string.Equals(this.rotationPlugin, "RotationSolverReborn", StringComparison.Ordinal);
        bool ready = this.player.IsLoaded && !this.condition[ConditionFlag.OccupiedInCutSceneEvent];
        bool? active = owned ? this.boardRotationIpc.Active() : null;
        var action = this.boardRotationRecovery.Tick(now, owned, ready, active);

        if (!owned)
        {
            this.boardRotationNoted = false;
            this.lastBoardRotationActive = null;
            this.RotationStatus = this.rotationPlugin == "RotationSolverReborn"
                ? "Waiting for dungeon entry" : "Managed by AutoDuty";
            return false;
        }

        if (!this.boardRotationNoted)
        {
            this.boardRotationNoted = true;
            Plugin.Trace("Board RSR handoff: inside Crucible, AutoDuty running; " +
                         "verifying and asserting AutoDuty mode after the startup grace.");
        }

        if (active != this.lastBoardRotationActive)
        {
            Plugin.Trace(active == true ? "Board RSR: autorotation active."
                : active == false ? "Board RSR: autorotation inactive."
                : "Board RSR: activation state unavailable.", important: active != true);
            this.lastBoardRotationActive = active;
        }
        this.RotationStatus = active switch
        {
            true => "RSR active (AutoDuty mode asserted at entry)",
            false => "RSR inactive; recovery monitor running",
            _ => "RSR did not answer; recovery monitor running",
        };

        if (action == BoardRotationAction.Exhausted)
        {
            const string reason = "RSR did not activate after 3 AutoDuty-mode requests; " +
                                  "stopping the board rather than proceeding without combat rotation.";
            Plugin.Trace($"Board RSR handoff failed: {reason}", important: true);
            this.Stop(reason);
            return true;
        }

        if (action is BoardRotationAction.EnsureAutoDutyMode or BoardRotationAction.RetryAutoDutyMode)
        {
            bool sent = this.boardRotationIpc.ActivateAutoDutyMode(out string detail);
            this.RotationStatus = sent ? "AutoDuty mode requested; checking activation"
                : "AutoDuty mode request failed; retry scheduled";
            Plugin.Trace($"Board RSR handoff ({action}, attempt " +
                         $"{this.boardRotationRecovery.Attempts}/3): {detail}", important: !sent);
        }
        return false;
    }

    private static bool IsBoard(int number) => Boards.All.Any(b => b.Number == number);

    private void Execute(Orchestrator o, IReadOnlyList<Command> commands, DateTime now)
    {
        foreach (var c in commands)
        {
            switch (c.Kind)
            {
                case CommandKind.StartBoard:
                    if (this.autoDuty.Start(c.Board!, this.rotationPlugin, this.config.BoardRunOptions()) is { } error)
                        o.StartFailed(now, error);
                    else
                        this.bossMod.AllowMovingWhileCasting();
                    break;
                case CommandKind.StopExecutor:
                    this.autoDuty.Stop();
                    break;
            }
        }
    }

    /// <summary>Stores new run records and, once the session has ended, reports it.</summary>
    private void Settle(Orchestrator o)
    {
        if (o.Runs.Count > this.saved)
        {
            if (!this.config.Runs.TryGetValue(this.character, out var history))
                this.config.Runs[this.character] = history = [];
            foreach (var run in o.Runs.Skip(this.saved))
            {
                history.Add(run);
                Plugin.Log.Information($"Run: {Boards.ByNumber(run.Board).Name}, attempt {run.Attempt}: {run.Outcome}. {run.Detail}");
            }
            // A run AutoDuty never began leaves the pushed settings in place; AutoDuty restores them only on a stop.
            // The adapter pops only in that case.
            this.autoDuty.PopOverrides();
            if (history.Count > Configuration.HistoryCap)
                history.RemoveRange(0, history.Count - Configuration.HistoryCap);
            this.saved = o.Runs.Count;
            this.save();
        }

        if (o.Report is not { } report)
            return;
        this.Current = null;
        this.LastReport = report;

        var lines = report.Lines().ToList();
        this.session.Update(force: true);
        if (this.session.Plan is { } plan)
        {
            lines.Add(plan.Boards.Count == 0
                ? "Every board is cleared."
                : $"Next: the {plan.Boards[0].Board.Name} ({plan.Boards[0].Verdict}).");
        }
        foreach (var line in lines)
            this.chat.Print($"[Crucible SUX] {line}");
        this.notifications.AddNotification(new Notification
        {
            Title = report.GoalReached ? "Crucible SUX: session done" : "Crucible SUX: session stopped",
            Content = string.Join("\n", lines),
            Type = report.GoalReached ? NotificationType.Success : NotificationType.Warning,
            InitialDuration = TimeSpan.FromHours(12),
        });
    }

    private void OnLogout(int type, int code) => this.Stop("Logged out.");
}
