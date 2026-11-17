using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Automation;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CrucibleSux.Game;

/// <summary>A snapshot of a beast registered on the Crucible party screen.</summary>
public sealed record DungeonBeast(uint Number, int? Rank, uint Hp, uint MaxHp, int? Battlehorn, bool Disabled);

/// <summary>Pre-entry notebook scan and current registered-roster capture.</summary>
public sealed partial class BestiaryWatcher
{
    private readonly List<uint> scanQueue = [];
    private readonly HashSet<ulong> scannedCharacters = [];
    private DateTime nextScanAction;
    private DateTime nextAutoDutyCheck;
    private bool autoDutyIdle = true;
    private DateTime scanWaitingSince;
    private uint scanWaitingFor;
    private int scanDone;
    private int scanSkipped;
    private int pageSwitches;
    private bool scanRequested;
    private ulong scanOwner;
    private ulong rosterOwner;
    private IReadOnlyList<DungeonBeast> roster = [];

    public IReadOnlyList<DungeonBeast> CurrentRoster =>
        this.rosterOwner != 0 && this.rosterOwner == this.player.ContentId ? this.roster : [];

    public DateTime? RosterUpdatedUtc { get; private set; }

    public bool IsScanning => this.scanQueue.Count > 0;
    public int ScanCompleted => this.scanDone;
    public int ScanSkipped => this.scanSkipped;
    public int ScanTotal => this.scanDone + this.scanSkipped + this.scanQueue.Count;
    public string ScanStatus { get; private set; } = "Open the Master's Bestiary before entering a board to read all owned beast ranks.";

    /// <summary>Requests a fresh scan. It begins when the player opens the Master's Bestiary.</summary>
    public void RequestFullScan()
    {
        this.scanRequested = true;
        this.scanQueue.Clear();
        this.scanWaitingFor = 0;
        this.scannedCharacters.Remove(this.player.ContentId);
        this.ScanStatus = "Open the Master's Bestiary to read every owned beast's rank and EXP.";
    }

    public void CancelScan()
    {
        this.scanRequested = false;
        this.scanQueue.Clear();
        this.scanWaitingFor = 0;
        this.scannedCharacters.Add(this.player.ContentId); // Do not restart automatically on the next UI frame.
        this.ScanStatus = "Scan cancelled. Existing rank data has been retained.";
    }

    private void RememberRoster(List<DungeonBeast> team)
    {
        if (this.player.ContentId == 0 || team.Count == 0)
            return;
        // A registration screen may refresh HP/ranks on every frame; retain the most recent roster
        // when the UI disappears on dungeon entry. A different character never sees this snapshot.
        if (this.rosterOwner != this.player.ContentId || !this.roster.SequenceEqual(team))
        {
            this.roster = team.ToArray();
            this.rosterOwner = this.player.ContentId;
            this.RosterUpdatedUtc = DateTime.UtcNow;
        }
    }

    private void RememberResultRank(uint number, int? rank)
    {
        if (this.rosterOwner != this.player.ContentId || rank is null || this.roster.Count == 0)
            return;
        var entries = this.roster.ToArray();
        int index = Array.FindIndex(entries, x => x.Number == number);
        if (index < 0)
            return;
        entries[index] = entries[index] with { Rank = rank };
        this.roster = entries;
        this.RosterUpdatedUtc = DateTime.UtcNow;
    }

    private unsafe void TickBestiaryScan(AtkUnitBase* notebook)
    {
        if (this.player.ContentId == 0 || Plugin.Condition[ConditionFlag.BoundByDuty])
            return;

        // AutoDuty also scans the Bestiary before selecting a board team. Never click the same
        // UI while it is running; passively collect its observed ranks instead. Probe at most
        // once a second so an unavailable IPC does not generate an exception on every frame.
        var now = DateTime.UtcNow;
        if (now >= this.nextAutoDutyCheck)
        {
            this.nextAutoDutyCheck = now.AddSeconds(1);
            try
            {
                this.autoDutyIdle = Plugin.PluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped").InvokeFunc();
            }
            catch (Exception)
            {
                this.autoDutyIdle = true;
            }
        }
        if (!this.autoDutyIdle)
        {
            if (this.IsScanning)
                this.ScanStatus = "AutoDuty is controlling the Bestiary; collecting its rank reads without clicking.";
            return;
        }

        ulong owner = this.player.ContentId;
        if (this.scanOwner != owner)
        {
            this.scanQueue.Clear();
            this.scanOwner = owner;
            this.scanWaitingFor = 0;
        }
        if (this.scanQueue.Count == 0 && (this.scanRequested || (this.config.AutoScanBeasts && !this.scannedCharacters.Contains(owner))))
            this.StartScan();
        if (this.scanQueue.Count == 0)
            return;

        if (Bool(notebook, 8))
        {
            this.ScanStatus = "Clear the Master's Bestiary filters to read all owned beasts.";
            return;
        }

        uint current = this.scanQueue[0];
        bool selected = Bool(notebook, 255);
        uint selectedNumber = UInt(notebook, 5) ?? 0;
        if (selected && selectedNumber == current && Number(ValueText(notebook, 258)) is >= 1 and <= 25)
        {
            this.scanQueue.RemoveAt(0);
            this.scanDone++;
            this.scanWaitingFor = 0;
            this.pageSwitches = 0;
            if (this.scanQueue.Count == 0)
            {
                this.ScanStatus = $"Read {this.scanDone} owned beast ranks; {this.scanSkipped} unavailable. Ready for team selection.";
                this.scannedCharacters.Add(owner);
                return;
            }
            current = this.scanQueue[0];
        }

        if (now < this.nextScanAction)
            return;
        if (this.scanWaitingFor == current)
        {
            if (now - this.scanWaitingSince <= TimeSpan.FromSeconds(3))
                return;
            // A row not selectable for 3 s must not wedge the entire queue.
            this.scanSkipped++;
            this.scanQueue.RemoveAt(0);
            this.scanWaitingFor = 0;
            this.pageSwitches = 0;
            this.ScanStatus = $"Could not read beast #{current}; continuing ({this.scanDone}/{this.ScanTotal}).";
            return;
        }

        int slot = -1;
        for (int i = 0; i < 25; i++)
        {
            int entry = 24 + i * 8;
            if (UInt(notebook, entry) == current)
            {
                slot = i;
                break;
            }
        }
        if (slot < 0)
        {
            uint pages = UInt(notebook, 9) ?? 0;
            uint page = UInt(notebook, 10) ?? 0;
            if (pages is < 1 or > 10 || this.pageSwitches >= pages + 2)
            {
                this.scanSkipped++;
                this.scanQueue.RemoveAt(0);
                this.pageSwitches = 0;
                this.ScanStatus = $"Beast #{current} is not on the visible Bestiary pages; continuing.";
                return;
            }
            GameActions.Callback(notebook, 3, (int)((page + 1) % pages));
            this.pageSwitches++;
            this.nextScanAction = now.AddMilliseconds(600);
            return;
        }

        // Selecting a notebook row requires both the addon callback and its mouse-down event
        // in the current game layout (the same sequence used by AutoDuty's notebook reader).
        GameActions.Callback(notebook, 5, slot);
        ClickNotebookRow(notebook, (uint)(4 + slot));
        this.scanWaitingFor = current;
        this.scanWaitingSince = now;
        this.nextScanAction = now.AddMilliseconds(300);
        this.ScanStatus = $"Reading owned beast #{current}: {this.scanDone + this.scanSkipped + 1}/{this.ScanTotal}.";
    }

    private unsafe void StartScan()
    {
        var manager = XBMManager.Instance();
        if (manager == null || manager->NumUnlockedPets <= 0)
        {
            this.ScanStatus = "The game has not provided the owned beast list yet; reopen the Master's Bestiary.";
            return;
        }
        this.scanQueue.Clear();
        this.scanQueue.AddRange(this.byId.Keys.Where(id => manager->IsPetUnlocked(id)).OrderBy(id => id));
        this.scanDone = 0;
        this.scanSkipped = 0;
        this.scanWaitingFor = 0;
        this.pageSwitches = 0;
        this.nextScanAction = DateTime.MinValue;
        this.scanRequested = false;
        if (this.scanQueue.Count == 0)
        {
            this.ScanStatus = "There are no owned beasts to scan.";
            this.scannedCharacters.Add(this.player.ContentId);
            return;
        }
        this.ScanStatus = $"Scanning all {this.scanQueue.Count} owned beast ranks before entry.";
    }

    private static unsafe void ClickNotebookRow(AtkUnitBase* addon, uint eventParam)
    {
        AtkEvent* evt = FindNotebookEvent(&addon->UldManager, AtkEventType.MouseDown, eventParam, 0);
        if (evt == null)
            return;
        var data = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.MouseDown, (int)eventParam, evt, &data);
    }

    private static unsafe AtkEvent* FindNotebookEvent(AtkUldManager* uld, AtkEventType type, uint param, int depth)
    {
        if (depth > 6)
            return null;
        for (int i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null || !node->IsVisible())
                continue;
            for (var evt = node->AtkEventManager.Event; evt != null; evt = evt->NextEvent)
                if (evt->State.EventType == type && evt->Param == param)
                    return evt;
            var component = node->GetAsAtkComponentNode();
            if (component == null || component->Component == null)
                continue;
            var found = FindNotebookEvent(&component->Component->UldManager, type, param, depth + 1);
            if (found != null)
                return found;
        }
        return null;
    }
}
