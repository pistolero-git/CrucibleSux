using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace CrucibleSux.Game;

/// <summary>
/// Keeps the per-character beast rank cache current from the game's numeric rank/EXP fields.
/// The Bestiary, party registration window and board result all expose real ranks; stat-based
/// inference is deliberately not used. Unvisited and unregistered beasts remain unknown.
/// </summary>
public sealed partial class BestiaryWatcher : IDisposable
{
    public const string AddonName = "XBMMonsterBookDetail";
    private const string NotebookName = "XBMMonsterNotebook";
    private const string PartyName = "XBMPetParty";
    private const string ResultName = "XBMResult";

    private readonly IAddonLifecycle lifecycle;
    private readonly IPlayerState player;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly Action save;
    private readonly Dictionary<string, BeastInfo> byName;
    private readonly Dictionary<uint, BeastInfo> byId;
    private bool warned;

    public string? LastRead { get; private set; }

    public BestiaryWatcher(IAddonLifecycle lifecycle, IPlayerState player, IPluginLog log,
        Configuration config, Action save, IEnumerable<BeastInfo> beasts)
    {
        this.lifecycle = lifecycle;
        this.player = player;
        this.log = log;
        this.config = config;
        this.save = save;
        var known = beasts.ToList();
        this.byName = known.GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        this.byId = known.ToDictionary(b => b.Id);
        lifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, this.OnDetail);
        lifecycle.RegisterListener(AddonEvent.PostUpdate, NotebookName, this.OnNotebook);
        lifecycle.RegisterListener(AddonEvent.PostUpdate, PartyName, this.OnParty);
        lifecycle.RegisterListener(AddonEvent.PostUpdate, ResultName, this.OnResult);
    }

    public void Dispose()
    {
        this.lifecycle.UnregisterListener(AddonEvent.PostUpdate, AddonName, this.OnDetail);
        this.lifecycle.UnregisterListener(AddonEvent.PostUpdate, NotebookName, this.OnNotebook);
        this.lifecycle.UnregisterListener(AddonEvent.PostUpdate, PartyName, this.OnParty);
        this.lifecycle.UnregisterListener(AddonEvent.PostUpdate, ResultName, this.OnResult);
    }

    private unsafe void OnNotebook(AddonEvent _, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (!Ready(addon))
                return;
            // Passive observation also picks up every beast AutoDuty selects when it scans.
            if (Bool(addon, 255))
            {
                uint id = UInt(addon, 5) ?? 0;
                int? rank = Number(ValueText(addon, 258));
                int? exp = AsInt(UInt(addon, 261));
                if (this.Record(id, rank, exp, "Bestiary"))
                    this.save();
            }
            this.TickBestiaryScan(addon);
        }
        catch (Exception ex) { this.Warn(ex); }
    }

    private unsafe void OnDetail(AddonEvent _, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (!Ready(addon))
                return;
            var uld = &addon->UldManager;
            // The detail page has actual rank at text node 40 and EXP at 42.
            // Node 12 is the beast number; the name is a fallback when it is absent.
            uint id = (uint)(Number(NodeText(uld, 12)) ?? 0);
            if (id == 0 && this.byName.TryGetValue(NodeText(uld, 13).Trim(), out var beast))
                id = beast.Id;
            int? rank = Number(NodeText(uld, 40));
            int? exp = Number(NodeText(uld, 42));
            if (this.Record(id, rank, exp, "Bestiary detail"))
                this.save();
        }
        catch (Exception ex) { this.Warn(ex); }
    }

    private unsafe void OnParty(AddonEvent _, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (!Ready(addon) || UInt(addon, 5) is not uint count || count > 15)
                return;
            bool changed = false;
            var team = new List<DungeonBeast>();
            for (int i = 0; i < (int)count; i++)
            {
                int entry = 6 + i * 77;
                uint id = UInt(addon, entry + 76) ?? 0;
                int? rank = Number(ValueText(addon, entry));
                if (this.byId.ContainsKey(id))
                {
                    uint horn = UInt(addon, entry + 74) ?? 3;
                    team.Add(new DungeonBeast(id, rank,
                        UInt(addon, entry + 5) ?? 0, UInt(addon, entry + 6) ?? 0,
                        horn <= 2 ? (int)horn + 1 : null, Bool(addon, entry + 2)));
                }
                // The party row does not give EXP. Retain the last observed value.
                changed |= this.Record(id, rank, null, "Registered team", authoritative: false);
            }
            this.RememberRoster(team);
            if (changed)
                this.save();
        }
        catch (Exception ex) { this.Warn(ex); }
    }

    private unsafe void OnResult(AddonEvent _, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon.Address;
            if (!Ready(addon) || UInt(addon, 72) is not uint count || count > 15)
                return;
            bool changed = false;
            for (int i = 0; i < (int)count; i++)
            {
                // Result entries are staggered: beast icon at 73+i, rank at +4*16, EXP at +2*16.
                int entry = 73 + i;
                uint icon = UInt(addon, entry) ?? 0;
                if (icon < 242001 || icon > 242000 + CrucibleIds.TotalBeasts)
                    continue;
                uint id = icon - 242000;
                int? rank = AsInt(UInt(addon, entry + 64));
                int? exp = AsInt(UInt(addon, entry + 32));
                changed |= this.Record(id, rank, exp, "Board result");
                this.RememberResultRank(id, rank);
            }
            if (changed)
                this.save();
        }
        catch (Exception ex) { this.Warn(ex); }
    }

    /// <summary>Save only real, valid rank values. Never replace a newer rank with a synced/older one.</summary>
    private bool Record(uint id, int? rank, int? exp, string source, bool authoritative = true)
    {
        if (this.player.ContentId == 0 || !this.byId.TryGetValue(id, out var beast))
            return false;
        if (!this.config.Characters.TryGetValue(this.player.ContentId, out var saved))
            this.config.Characters[this.player.ContentId] = saved = [];
        var previous = saved.GetValueOrDefault(id);
        BeastProgress? old = previous is null ? null : new BeastProgress(id, previous.Rank, previous.Exp,
            previous.RankIsEstimated, previous.SeenUtc);
        var updated = BeastRankUpdates.Merge(id, rank, exp, old, DateTime.UtcNow, authoritative);
        if (updated is null)
            return false;
        saved[id] = new SavedBeast
        {
            Rank = updated.Rank,
            Exp = updated.Exp,
            RankIsEstimated = false,
            SeenUtc = updated.SeenUtc,
        };
        this.LastRead = $"{beast.Name}: rank {updated.Rank}, EXP {(updated.Exp?.ToString() ?? "?")} ({source})";
        return true;
    }

    private static unsafe bool Ready(AtkUnitBase* addon) =>
        addon != null && addon->IsVisible && addon->AtkValues != null;

    private static unsafe uint? UInt(AtkUnitBase* addon, int index)
    {
        if (!Ready(addon) || index < 0 || index >= addon->AtkValuesCount)
            return null;
        var value = addon->AtkValues[index];
        return (value.Type & AtkValueType.TypeMask) switch
        {
            AtkValueType.UInt => value.UInt,
            AtkValueType.Int when value.Int >= 0 => (uint)value.Int,
            _ => null,
        };
    }

    private static unsafe bool Bool(AtkUnitBase* addon, int index) =>
        Ready(addon) && index >= 0 && index < addon->AtkValuesCount
        && (addon->AtkValues[index].Type & AtkValueType.TypeMask) == AtkValueType.Bool
        && addon->AtkValues[index].Bool;

    private static unsafe string ValueText(AtkUnitBase* addon, int index)
    {
        if (!Ready(addon) || index < 0 || index >= addon->AtkValuesCount)
            return string.Empty;
        var value = addon->AtkValues[index];
        var type = value.Type & AtkValueType.TypeMask;
        return type is AtkValueType.String or AtkValueType.ConstString ? value.GetValueAsString() : string.Empty;
    }

    private static unsafe string NodeText(AtkUldManager* uld, uint id)
    {
        var node = uld->SearchNodeById(id);
        var textNode = node == null ? null : node->GetAsAtkTextNode();
        return textNode == null ? string.Empty : new ReadOnlySeStringSpan(textNode->NodeText.AsSpan()).ExtractText();
    }

    private static int? AsInt(uint? value) => value <= int.MaxValue ? (int?)value : null;

    private static int? Number(string text)
    {
        int value = 0;
        bool started = false;
        foreach (char c in text)
        {
            if (char.IsAsciiDigit(c))
            {
                if (value > (int.MaxValue - (c - '0')) / 10)
                    return null;
                value = value * 10 + (c - '0');
                started = true;
            }
            else if (started && c != ',')
                break;
        }
        return started ? value : null;
    }

    private void Warn(Exception ex)
    {
        if (this.warned)
            return;
        this.warned = true;
        this.log.Warning(ex, "Could not read current beast ranks from the Crucible UI; cached ranks are retained");
    }
}
