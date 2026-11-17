using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace CrucibleSux.Integrations;

/// <summary>
/// Recovers the rotation that AutoDuty owns in a Crucible board. Enum values mirror RSR's
/// public IPC arguments (not a reference to either AutoDuty or RSR assemblies).
/// AutoDuty's current RSR targeting profile is read immediately before each recovery.
/// </summary>
public sealed class BoardRotationIpc
{
    // RSR Basic/Data/RSCommandType.cs; AutoDuty = 4, not Auto = 1 or Manual = 3.
    private enum StateCommandType : byte { AutoDuty = 4 }

    // RSR Basic/Data/TargetType.cs (int); AutoDuty chooses the non-tank profile for BST.
    private enum TargetingType
    {
        Big, Small, HighHP, LowHP, HighHPPercent, LowHPPercent, HighMaxHP,
        LowMaxHP, Nearest, Farthest, PvPHealers, PvPTanks, PvPDPS,
    }

    // RSR Basic/Data/TargetHostileType.cs.
    private enum TargetHostileType : byte
    {
        AllTargetsCanAttack, TargetsHaveTarget, AllTargetsWhenSoloInDuty,
        AllTargetsWhenSolo, SoloDeepDungeonSmart,
    }

    // RSR Basic/Data/SettingOverride.cs.
    private enum SettingOverride : byte { UseSetting, ForceOff, ForceOn }

    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<bool> active;
    private readonly ICallGateSubscriber<string, string> getAutoDutyConfig;
    private readonly ICallGateSubscriber<StateCommandType, TargetingType, TargetHostileType,
        SettingOverride, SettingOverride, SettingOverride, object> activate;

    public BoardRotationIpc(IDalamudPluginInterface pi, IPluginLog log)
    {
        this.log = log;
        this.active = pi.GetIpcSubscriber<bool>("RotationSolverReborn.AutorotationActive");
        this.getAutoDutyConfig = pi.GetIpcSubscriber<string, string>("AutoDuty.GetConfig");
        this.activate = pi.GetIpcSubscriber<StateCommandType, TargetingType, TargetHostileType,
            SettingOverride, SettingOverride, SettingOverride, object>(
                "RotationSolverReborn.AutodutyChangeOperatingModeWithOverrides");
    }

    /// <summary>Null means the IPC did not answer; it is NOT evidence that RSR is off.</summary>
    public bool? Active()
    {
        try
        {
            return this.active.HasFunction ? this.active.InvokeFunc() : null;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "Could not query RSR activation during the board");
            return null;
        }
    }

    /// <summary>Ask RSR for the same AutoDuty operating mode and overrides that AutoDuty uses.</summary>
    public bool ActivateAutoDutyMode(out string detail)
    {
        if (!this.activate.HasAction)
        {
            detail = "RSR's AutoDuty-mode IPC is unavailable";
            return false;
        }

        var targeting = this.ReadEnum("DutyConfig.RSR.TargetingTypeNonTank", TargetingType.LowHP);
        var hostile = this.ReadEnum("DutyConfig.RSR.TargetHostileType", TargetHostileType.AllTargetsCanAttack);
        try
        {
            this.activate.InvokeAction(StateCommandType.AutoDuty, targeting, hostile,
                SettingOverride.ForceOn, SettingOverride.ForceOff, SettingOverride.ForceOn);
            detail = $"RSR AutoDuty mode requested (target {targeting}, hostile {hostile}, " +
                     "free targeting on, auto-off after combat off, friendly NPC support on)";
            this.log.Information(detail);
            return true;
        }
        catch (Exception ex)
        {
            detail = $"RSR rejected AutoDuty mode: {ex.Message}";
            this.log.Warning(ex, "Failed to activate RSR AutoDuty mode");
            return false;
        }
    }

    private T ReadEnum<T>(string name, T fallback) where T : struct, Enum
    {
        try
        {
            if (this.getAutoDutyConfig.HasFunction)
            {
                string value = this.getAutoDutyConfig.InvokeFunc(name);
                if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
                    return parsed;
                this.log.Warning($"AutoDuty returned an unknown value '{value}' for {name}; using {fallback}");
            }
            else
            {
                this.log.Warning($"AutoDuty.GetConfig is unavailable; using {fallback} for {name}");
            }
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, $"Could not read {name}; using {fallback}");
        }

        return fallback;
    }
}
