using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CrucibleSux.Ui;

/// <summary>The dashboard: progress, the one-sitting check, beasts, team suggestions, companion plugins and settings.</summary>
public sealed class MainWindow : Window, IDisposable
{
    /// <summary>The <see cref="BossModChoice"/> names, by value.</summary>
    private static readonly string[] BossModChoices = ["Not chosen yet (asked at the next start)", "BossMod Reborn", "The original BossMod"];

    private readonly Session session;
    private readonly CrucibleData data;
    private readonly Configuration config;
    private readonly BestiaryWatcher bestiary;
    private readonly Action save;
    private readonly SessionTabs sessionTabs;
    private readonly JourneyTab journeyTab;
    private readonly LogTab logTab;
    // Built-in FFXIV fonts: no bundled font files or third-party UI dependency.
    private readonly IFontHandle bodyFont;
    private readonly IFontHandle brandFont;
    private readonly IFontHandle pageFont;
    private readonly IFontHandle smallCapsFont;
    private bool compact;
    private bool expandOnNextDraw;
    private Vector2 expandedSize = new(1100, 760);
    private MenuPage currentPage = MenuPage.Dashboard;
    private bool missingOnly;
    private int teamBoard = 1;
    private (int Board, int Generation)? teamKey;
    private TeamPlan? team;

    public MainWindow(Session session, CrucibleData data, Configuration config, BestiaryWatcher bestiary, SessionTabs sessionTabs, JourneyTab journeyTab, LogTab logTab, Action save)
        : base("Crucible SUX Leveler###CrucibleSuxLevelerMain", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse)
    {
        this.session = session;
        this.data = data;
        this.config = config;
        this.bestiary = bestiary;
        this.sessionTabs = sessionTabs;
        this.journeyTab = journeyTab;
        this.logTab = logTab;
        this.save = save;
        this.bodyFont = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis14));
        this.brandFont = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.MiedingerMid18));
        this.pageFont = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Jupiter23));
        this.smallCapsFont = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.MiedingerMid12));
        this.AllowPinning = false;
        this.AllowClickthrough = false;
        this.ShowCloseButton = false;
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(860, 530) };
        this.Size = this.expandedSize;
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    // This is an original Dalamud UI. It uses the game's fonts, with real clickable ImGui
    // buttons in the nav rail. Draw-list accents are purely visual and do not capture input.
    private enum MenuPage
    {
        Dashboard,
        Run,
        Journey,
        Readiness,
        Bestiary,
        Roster,
        Teams,
        History,
        Log,
        Plugins,
        Settings,
        About,
    }

    private static readonly Vector4 WindowBg = new(0.042f, 0.055f, 0.075f, 1f);
    private static readonly Vector4 HeaderBg = new(0.066f, 0.086f, 0.116f, 1f);
    private static readonly Vector4 RailBg = new(0.056f, 0.071f, 0.098f, 1f);
    private static readonly Vector4 WorkspaceBg = new(0.075f, 0.093f, 0.122f, 1f);
    private static readonly Vector4 CardBg = new(0.10f, 0.126f, 0.16f, 1f);
    private static readonly Vector4 Accent = new(0.34f, 0.83f, 0.82f, 1f);
    private static readonly Vector4 AccentMuted = new(0.15f, 0.28f, 0.32f, 1f);
    private static readonly Vector4 TextBright = new(0.96f, 0.975f, 0.985f, 1f);
    private static readonly Vector4 Muted = new(0.62f, 0.69f, 0.76f, 1f);

    public void Dispose()
    {
        this.bodyFont.Dispose();
        this.brandFont.Dispose();
        this.pageFont.Dispose();
        this.smallCapsFont.Dispose();
    }

    public override void PreDraw()
    {
        this.SizeConstraints = this.compact
            ? new WindowSizeConstraints { MinimumSize = new Vector2(600, 78), MaximumSize = new Vector2(float.MaxValue, 78) }
            : new WindowSizeConstraints { MinimumSize = new Vector2(860, 530) };
        if (this.compact)
        {
            this.Size = new Vector2(this.expandedSize.X, 78);
            this.SizeCondition = ImGuiCond.Always;
        }
        else if (this.expandOnNextDraw)
        {
            this.Size = this.expandedSize;
            this.SizeCondition = ImGuiCond.Always;
            this.expandOnNextDraw = false;
        }
        else
        {
            this.SizeCondition = ImGuiCond.FirstUseEver;
        }
    }

    public void OpenSettings()
    {
        this.currentPage = MenuPage.Settings;
        this.IsOpen = true;
        if (this.compact)
            this.ToggleCompact();
    }

    public override void Draw()
    {
        // All pages, including existing forms/tables, use the game's native Axis body typography.
        using var bodyScope = this.bodyFont.Push();
        ImGui.PushStyleColor(ImGuiCol.WindowBg, WindowBg);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 11f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 7f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(9f, 8f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, CardBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, AccentMuted);
        ImGui.PushStyleColor(ImGuiCol.Header, AccentMuted);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.20f, 0.35f, 0.39f, 1f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Accent);

        var rootPosition = ImGui.GetWindowPos();
        if (!this.compact)
            this.expandedSize = ImGui.GetWindowSize();
        this.DrawHeader(rootPosition);

        if (!this.compact)
        {
            ImGui.Spacing();
            float room = ImGui.GetContentRegionAvail().X;
            float railWidth = room < 1010f ? 207f : 230f;
            ImGui.PushStyleColor(ImGuiCol.ChildBg, RailBg);
            if (ImGui.BeginChild("##CrucibleNav", new Vector2(railWidth, 0), false))
                this.DrawNavigation();
            ImGui.EndChild();
            ImGui.PopStyleColor();

            ImGui.SameLine(0, 9f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, WorkspaceBg);
            if (ImGui.BeginChild("##CrucibleWorkspace", new Vector2(0, 0), false))
                this.DrawContent();
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar(4);
    }

    private void ToggleCompact()
    {
        this.compact = !this.compact;
        this.expandOnNextDraw = !this.compact;
    }

    private void DrawHeader(Vector2 rootPosition)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, HeaderBg);
        if (ImGui.BeginChild("##CrucibleHeader", new Vector2(0, 64), false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var origin = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            var dl = ImGui.GetWindowDrawList();
            // Only the left part of the header is a drag target. The right-hand buttons
            // are separate ImGui items and cannot be covered by it.
            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.InvisibleButton("##CrucibleDrag", new Vector2(MathF.Max(180f, size.X - 190f), 64f));
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                ImGui.SetWindowPos("Crucible SUX Leveler###CrucibleSuxLevelerMain",
                    rootPosition + ImGui.GetIO().MouseDelta);

            dl.AddRectFilled(origin + new Vector2(16, 11), origin + new Vector2(59, 53),
                ImGui.ColorConvertFloat4ToU32(AccentMuted), 11f);
            dl.AddText(UiBuilder.IconFont, 21f, origin + new Vector2(28, 21),
                ImGui.ColorConvertFloat4ToU32(Accent), FontAwesomeIcon.Paw.ToIconString());
            ImGui.SetCursorPos(new Vector2(71, 7));
            using (this.brandFont.Push())
                ImGui.TextColored(TextBright, "CRUCIBLE SUX");
            ImGui.SetCursorPos(new Vector2(72, 35));
            using (this.smallCapsFont.Push())
                ImGui.TextColored(Muted, "LEVELER    /    CONTROL CENTER");

            if (!this.compact && this.session.Snapshot is not null)
            {
                ImGui.SetCursorPos(new Vector2(size.X - 291f, 24f));
                ImGui.TextColored(Accent, "● ONLINE");
            }
            ImGui.SetCursorPos(new Vector2(size.X - 162f, 14f));
            if (ImGui.Button(this.compact ? "Expand##crucibleCompact" : "Compact##crucibleCompact", new Vector2(94f, 35f)))
                this.ToggleCompact();
            ImGui.SameLine(0, 7f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.28f, 0.115f, 0.14f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.50f, 0.17f, 0.21f, 1f));
            if (ImGui.Button("X##CrucibleClose", new Vector2(43f, 35f)))
                this.IsOpen = false;
            ImGui.PopStyleColor(2);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawNavigation()
    {
        ImGui.Dummy(new Vector2(0, 11f));
        ImGui.SetCursorPosX(21f);
        using (this.smallCapsFont.Push())
            ImGui.TextColored(Accent, "WORKSPACE");
        ImGui.SetCursorPosX(21f);
        ImGui.TextColored(Muted, "Select a page");
        ImGui.Spacing();
        this.DrawNavGroup("OPERATIONS", [
            (MenuPage.Dashboard, "Dashboard", FontAwesomeIcon.ThLarge),
            (MenuPage.Run, "Board runner", FontAwesomeIcon.PlayCircle),
            (MenuPage.Journey, "Journey", FontAwesomeIcon.Route),
            (MenuPage.Readiness, "Readiness", FontAwesomeIcon.ClipboardCheck)]);
        this.DrawNavGroup("BEASTS", [
            (MenuPage.Bestiary, "Bestiary", FontAwesomeIcon.Paw),
            (MenuPage.Roster, "Dungeon roster", FontAwesomeIcon.Users),
            (MenuPage.Teams, "Team planner", FontAwesomeIcon.Sitemap)]);
        this.DrawNavGroup("ACTIVITY", [
            (MenuPage.History, "Run history", FontAwesomeIcon.History),
            (MenuPage.Log, "Diagnostics", FontAwesomeIcon.Terminal)]);
        this.DrawNavGroup("SYSTEM", [
            (MenuPage.Plugins, "Dependencies", FontAwesomeIcon.Plug),
            (MenuPage.Settings, "Settings", FontAwesomeIcon.Cog),
            (MenuPage.About, "About", FontAwesomeIcon.InfoCircle)]);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.SetCursorPosX(20);
        ImGui.TextColored(Muted, $"v{typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "dev"}");
        ImGui.SetCursorPosX(20);
        ImGui.TextDisabled("/suxl  ·  /suxl stop");
    }

    private void DrawNavGroup(string label, (MenuPage Page, string Label, FontAwesomeIcon Icon)[] entries)
    {
        ImGui.Dummy(new Vector2(0, 7f));
        ImGui.SetCursorPosX(21f);
        using (this.smallCapsFont.Push())
            ImGui.TextColored(Muted, label);
        foreach (var entry in entries)
        {
            bool selected = this.currentPage == entry.Page;
            float buttonWidth = MathF.Max(96f, ImGui.GetContentRegionAvail().X - 27f);
            ImGui.SetCursorPosX(13f);
            ImGui.PushStyleColor(ImGuiCol.Button, selected ? AccentMuted : new Vector4(0, 0, 0, 0));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.16f, 0.28f, 0.34f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.19f, 0.37f, 0.40f, 1f));
            // Real ImGui button: no negative-width Selectable, no invisible overlay.
            if (ImGui.Button($"##nav{entry.Page}", new Vector2(buttonWidth, 37f)))
                this.currentPage = entry.Page;
            var min = ImGui.GetItemRectMin();
            var dl = ImGui.GetWindowDrawList();
            var color = ImGui.ColorConvertFloat4ToU32(selected ? TextBright : Muted);
            if (selected)
                dl.AddRectFilled(min + new Vector2(1, 7), min + new Vector2(4, 30),
                    ImGui.ColorConvertFloat4ToU32(Accent), 2f);
            dl.AddText(UiBuilder.IconFont, 16f, min + new Vector2(16, 10),
                ImGui.ColorConvertFloat4ToU32(selected ? Accent : Muted), entry.Icon.ToIconString());
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), min + new Vector2(46, 10), color, entry.Label);
            ImGui.PopStyleColor(3);
        }
    }

    private void DrawContent()
    {
        ImGui.Dummy(new Vector2(0, 16f));
        ImGui.Indent(24f);
        string title = this.currentPage switch
        {
            MenuPage.Dashboard => "Dashboard",
            MenuPage.Run => "Board runner",
            MenuPage.Journey => "Beastmaster journey",
            MenuPage.Readiness => "Readiness",
            MenuPage.Bestiary => "Best collection",
            MenuPage.Roster => "Dungeon roster",
            MenuPage.Teams => "Team planner",
            MenuPage.History => "Run history",
            MenuPage.Log => "Diagnostics",
            MenuPage.Plugins => "Dependencies",
            MenuPage.Settings => "Settings",
            _ => "About",
        };
        string detail = this.currentPage switch
        {
            MenuPage.Dashboard => "Your progress, next boards and important actions",
            MenuPage.Run => "Configure, launch and monitor Crucible runs",
            MenuPage.Journey => "Automated progression and current objectives",
            MenuPage.Readiness => "What you can accomplish in one sitting",
            MenuPage.Bestiary => "Owned beasts, levels and rank verification",
            MenuPage.Roster => "The beasts registered for this dungeon",
            MenuPage.Teams => "Plan and compare your team composition",
            MenuPage.History => "Previous runs and their outcomes",
            MenuPage.Log => "Detailed events and integration diagnostics",
            MenuPage.Plugins => "Companion plugin connectivity and settings",
            MenuPage.Settings => "Session, board, targeting and ranking preferences",
            _ => "Project information and commands",
        };
        using (this.smallCapsFont.Push())
            ImGui.TextColored(Accent, "CRUCIBLE / OPERATIONS");
        using (this.pageFont.Push())
            ImGui.TextColored(TextBright, title);
        ImGui.TextColored(Muted, detail);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        switch (this.currentPage)
        {
            case MenuPage.Dashboard: this.DrawDashboard(); break;
            case MenuPage.Run: this.sessionTabs.DrawRun(); break;
            case MenuPage.Journey: this.journeyTab.Draw(); break;
            case MenuPage.Readiness: this.DrawSitting(); break;
            case MenuPage.Bestiary: this.DrawBeasts(); break;
            case MenuPage.Roster: this.DrawDungeonRoster(); break;
            case MenuPage.Teams: this.DrawTeams(); break;
            case MenuPage.History: this.sessionTabs.DrawHistory(); break;
            case MenuPage.Log: this.logTab.Draw(); break;
            case MenuPage.Plugins: this.DrawPlugins(); break;
            case MenuPage.Settings: this.DrawSettings(); break;
            case MenuPage.About: DrawAbout(); break;
        }
        ImGui.Dummy(new Vector2(0, 20f));
        ImGui.Unindent(24f);
    }

    private void DrawDashboard()
    {
        var snapshot = this.session.Snapshot;
        var plan = this.session.Plan;
        int verified = snapshot?.UnlockedBeasts?.Count(id =>
            snapshot.Beasts.GetValueOrDefault(id) is { Rank: > 0, RankIsEstimated: false }) ?? 0;

        // Distinct status card above the statistics. Uses actual game state, never dummy values.
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        if (ImGui.BeginChild("##StatusHero", new Vector2(0, 96), false))
        {
            ImGui.Indent(14f);
            ImGui.Spacing();
            using (this.smallCapsFont.Push())
                ImGui.TextColored(snapshot is null ? Muted : Accent,
                    snapshot is null ? "NO CHARACTER" : "CHARACTER READY");
            using (this.brandFont.Push())
                ImGui.TextColored(TextBright, snapshot is null ? "Log in to continue" : "Ready to plan your next run");
            ImGui.TextColored(Muted, snapshot is null
                ? "Progress and team data appear once a character is loaded."
                : $"{snapshot.UnlockedBeasts?.Count ?? 0} beasts collected · {plan?.Boards.Count ?? 0} boards in the current plan");
            ImGui.Unindent(14f);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Spacing();

        if (ImGui.BeginTable("##DashboardMetrics", 4, ImGuiTableFlags.SizingStretchSame))
        {
            MetricCard("BEASTMASTER", snapshot?.JobLevel > 0 ? $"Lv. {snapshot.JobLevel}" : "--", "Current level");
            MetricCard("COLLECTION", snapshot?.UnlockedBeasts is { } owned
                ? $"{owned.Count} / {CrucibleIds.TotalBeasts}" : "--", "Beasts owned");
            MetricCard("VERIFIED", snapshot?.UnlockedBeasts is { } known
                ? $"{verified} / {known.Count}" : "--", "Live beast ranks");
            MetricCard("PLANNED", plan is null ? "--" : plan.Boards.Count.ToString(), "Boards this sitting");
            ImGui.EndTable();
        }
        ImGui.Spacing();

        DrawSectionTitle("QUICK ACTIONS", "Jump directly to the most useful parts of your run");
        ImGui.PushStyleColor(ImGuiCol.Button, AccentMuted);
        if (ImGui.Button("Board runner", new Vector2(152f, 42f))) this.currentPage = MenuPage.Run;
        ImGui.SameLine();
        if (ImGui.Button("Journey", new Vector2(126f, 42f))) this.currentPage = MenuPage.Journey;
        ImGui.SameLine();
        if (ImGui.Button("Dungeon roster", new Vector2(152f, 42f))) this.currentPage = MenuPage.Roster;
        ImGui.PopStyleColor();

        if (this.bestiary.IsScanning)
            ImGui.TextColored(ImGuiColors.DalamudYellow, this.bestiary.ScanStatus);
        else if (snapshot?.UnlockedBeasts is { } beasts && verified < beasts.Count)
            ImGui.TextColored(ImGuiColors.DalamudYellow,
                $"{beasts.Count - verified} beast rank(s) need live verification. Open the Bestiary to scan.");
        ImGui.Spacing();
        DrawSectionTitle("BOARD PROGRESS", "Clears, rankings and achievements");
        this.DrawOverview();
    }

    private void MetricCard(string label, string value, string detail)
    {
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.ChildBg, CardBg);
        if (ImGui.BeginChild($"##Metric{label}", new Vector2(0, 110), false))
        {
            ImGui.Indent(12f);
            ImGui.Spacing();
            using (this.smallCapsFont.Push()) ImGui.TextColored(Muted, label);
            using (this.brandFont.Push()) ImGui.TextColored(TextBright, value);
            ImGui.TextColored(Muted, detail);
            ImGui.Unindent(12f);
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void DrawSectionTitle(string title, string? detail = null)
    {
        ImGui.Spacing();
        using (this.brandFont.Push()) ImGui.TextColored(TextBright, title);
        if (detail is not null)
            ImGui.TextColored(Muted, detail);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawOverview()
    {
        if (this.session.Snapshot is not { } c || this.session.Progress is not { } p)
        {
            ImGui.TextDisabled("Log in to a character to read its Crucible progress.");
            return;
        }

        ImGui.TextColored(Muted, $"Item level {c.ItemLevel?.ToString() ?? "unknown"}  ·  " +
                              $"Remnants {c.FadedRemnants} faded / {c.BrightRemnants} bright");
        if (c.CompletedAchievements is null)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Achievements not loaded: open the Achievements window once so the game sends them.");
        if (c.UnlockedBeasts is null)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Beast list not loaded: open the Master's Bestiary once so the game sends it.");

        ImGui.Spacing();
        using (var table = ImRaii.Table("##boards", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH))
        {
            if (table)
            {
                ImGui.TableSetupColumn("Board", ImGuiTableColumnFlags.WidthStretch, 3f);
                foreach (var h in new[] { "Cleared", "Third degree", "Apex", "Legendary" })
                    ImGui.TableSetupColumn(h, ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableHeadersRow();
                foreach (var b in p.Boards)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{b.Board.Number}. {b.Board.Name}");
                    ImGui.TableNextColumn();
                    Mark(b.Cleared);
                    ImGui.TableNextColumn();
                    Mark(b.ThirdDegree);
                    ImGui.TableNextColumn();
                    Mark(b.Apex);
                    ImGui.TableNextColumn();
                    Mark(b.Legendary);
                }
            }
        }
        if (!p.DegreesUnlocked)
            ImGui.TextDisabled("Degrees and the rankings open with the quest \"Blazing Trails\" after the Second Master's Board.");

        ImGui.Spacing();
        this.DrawSectionTitle("ACHIEVEMENTS");
        foreach (var goal in new[] { p.JobLevel, p.Befriended, p.Rank25 })
        {
            string next = goal.NextThreshold is int n ? $"next tier at {n}" : "all tiers done";
            string tiers = goal.TiersDone is int t ? $"{t}/{goal.Thresholds.Count} tiers" : "tiers unknown";
            ImGui.TextUnformatted($"{goal.Label}: {goal.Current?.ToString() ?? "?"} ({tiers}, {next})");
        }
        Line("Freeing the Beast", p.FreeingTheBeast);
        Line("Crucible Master", p.CrucibleMaster);
        Line("Unbroken Endgame", p.UnbrokenEndgame);
        int mates = p.KinMates.Count(k => k.Done == true);
        ImGui.TextUnformatted($"Kin mates: {(c.CompletedAchievements is null ? "?" : mates.ToString())}/{p.KinMates.Count}");
        if (c.CompletedAchievements is not null && mates < p.KinMates.Count)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"(missing: {string.Join(", ", p.KinMates.Where(k => k.Done == false).Select(k => k.Kin))})");
        }
    }

    private void DrawSitting()
    {
        if (this.session.Plan is not { } plan)
        {
            ImGui.TextDisabled("Log in to a character to check its readiness.");
            return;
        }

        if (plan.Boards.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, "Every board is cleared. The Dashboard lists remaining scores and achievements.");
            return;
        }

        if (plan.CoversEverything)
            ImGui.TextColored(ImGuiColors.HealerGreen, $"You can attempt every remaining board ({plan.Boards.Count}) in one sitting.");
        else
            ImGui.TextColored(ImGuiColors.DalamudYellow, $"This sitting can cover {plan.Boards.Count - 1} board(s); it stops at the {plan.StopsAt!.Board.Name}.");
        ImGui.TextDisabled("Each board is judged as if the boards before it in this sitting were cleared.");

        foreach (var r in plan.Boards)
        {
            ImGui.Spacing();
            var (color, verdict) = r.Verdict switch
            {
                BoardVerdict.Ready => (ImGuiColors.HealerGreen, "Ready"),
                BoardVerdict.Risky => (ImGuiColors.DalamudYellow, "Risky"),
                _ => (ImGuiColors.DalamudRed, "Blocked"),
            };
            bool open;
            using (ImRaii.PushColor(ImGuiCol.Text, color))
                open = ImGui.CollapsingHeader($"{r.Board.Number}. {r.Board.Name}: {verdict}###sit{r.Board.Number}", r.Verdict == BoardVerdict.Ready ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.DefaultOpen);
            if (!open)
                continue;
            foreach (var check in r.Checks)
                DrawCheck(check);
        }
    }

    /// <summary>One check as a coloured status word, its label and detail, and the hint when it is not a pass.</summary>
    internal static void DrawCheck(ReadinessCheck check)
    {
        ImGui.TextColored(StatusColor(check.Status), $"  {StatusWord(check.Status),-7}");
        ImGui.SameLine();
        ImGui.TextUnformatted($"{check.Label}: {check.Detail}");
        if (check.Hint is not null && check.Status != CheckStatus.Pass)
        {
            ImGui.Indent();
            ImGui.TextDisabled(check.Hint);
            ImGui.Unindent();
        }
    }

    private void DrawBeasts()
    {
        var c = this.session.Snapshot;
        ImGui.Checkbox("Only beasts not befriended", ref this.missingOnly);
        ImGui.SameLine();
        ImGui.TextDisabled("Ranks/EXP update when you select a beast in the Bestiary or after a board result. Registered teams also provide ranks.");
        if (this.bestiary.LastRead is { } last)
            ImGui.TextDisabled($"Last read: {last}");
        if (c?.UnlockedBeasts is { } unlocked)
        {
            int verified = unlocked.Count(id => c.Beasts.GetValueOrDefault(id) is { Rank: > 0, RankIsEstimated: false });
            ImGui.TextUnformatted($"Verified ranks: {verified}/{unlocked.Count} owned beasts.");
        }
        if (ImGui.Button("Refresh all owned beast ranks before board"))
            this.bestiary.RequestFullScan();
        if (this.bestiary.IsScanning)
        {
            ImGui.SameLine();
            if (ImGui.Button("Cancel scan"))
                this.bestiary.CancelScan();
        }
        ImGui.TextWrapped(this.bestiary.ScanStatus);
        ImGui.TextDisabled("Open the Master's Bestiary outside a board to scan every owned beast. Rank is not available for unowned beasts.");

        using var table = ImRaii.Table("##beasts", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
        if (!table)
            return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
        ImGui.TableSetupColumn("Beast", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Kin", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Befriended", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Rank", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("EXP", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Where", ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableHeadersRow();

        foreach (var beast in this.data.Beasts)
        {
            bool? owned = c?.UnlockedBeasts?.Contains(beast.Id);
            if (this.missingOnly && owned != false)
                continue;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Id.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Kin.ToString());
            ImGui.TableNextColumn();
            Mark(owned);
            ImGui.TableNextColumn();
            if (c?.Beasts.GetValueOrDefault(beast.Id) is { Rank: int rank } seen)
                ImGui.TextUnformatted(seen.RankIsEstimated ? $"{rank} (est.)" : rank.ToString());
            else
                ImGui.TextDisabled(owned == true ? "not read" : "-");
            ImGui.TableNextColumn();
            if (c?.Beasts.GetValueOrDefault(beast.Id) is { Rank: not null, Exp: int exp })
                ImGui.TextUnformatted($"{exp}/100");
            else
                ImGui.TextDisabled("-");
            ImGui.TableNextColumn();
            if (this.data.Locations.TryGetValue(beast.Id, out var where))
            {
                ImGui.TextUnformatted(where);
            }
            else
            {
                ImGui.TextDisabled("special");
                if (ImGui.IsItemHovered() && this.data.Descriptions.TryGetValue(beast.Id, out var text))
                    ImGui.SetTooltip(text);
            }
        }
    }

    private void DrawDungeonRoster()
    {
        var team = this.bestiary.CurrentRoster;
        bool inBoard = Boards.ByTerritory(Plugin.ClientState.TerritoryType) is not null;
        ImGui.TextUnformatted(inBoard ? "Registered beasts for this dungeon" : "Last observed Crucible registration");
        ImGui.TextDisabled("Captured from the game's XBMPetParty screen. Values update while that screen is visible; the roster is retained after entry.");
        if (this.bestiary.RosterUpdatedUtc is { } when)
            ImGui.TextDisabled($"Last roster observation: {when.ToLocalTime():g}");
        if (team.Count == 0)
        {
            ImGui.TextDisabled("No registered roster recorded. Open the Crucible team registration screen before entering.");
            return;
        }
        ImGui.TextUnformatted($"{team.Count} beast(s) registered; Battlehorn assignment comes from the game.");
        using var table = ImRaii.Table("##dungeonroster", 7,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH);
        if (!table)
            return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 30f);
        ImGui.TableSetupColumn("Beast", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Rank", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("EXP", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("HP last seen", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Battlehorn", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableHeadersRow();
        foreach (var beast in team)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Number.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(this.data.Beasts.FirstOrDefault(b => b.Id == beast.Number)?.Name ?? $"Beast {beast.Number}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Rank?.ToString() ?? "?");
            ImGui.TableNextColumn();
            if (this.session.Snapshot?.Beasts.GetValueOrDefault(beast.Number)?.Exp is int exp)
                ImGui.TextUnformatted($"{exp}/100");
            else
                ImGui.TextDisabled("-");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.MaxHp > 0 ? $"{beast.Hp}/{beast.MaxHp}" : "unknown");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Battlehorn is int horn ? $"Horn {horn}" : "Unassigned");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(beast.Disabled ? "Unavailable" : beast.MaxHp > 0 && beast.Hp == 0 ? "KO (last seen)" : "Registered");
        }
    }

    private void DrawTeams()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, "Experimental: picks are scored from the game's sheets alone and are suggestions, not tested strategies.");
        var c = this.session.Snapshot;
        if (c?.UnlockedBeasts is not { Count: > 0 } owned)
        {
            ImGui.TextDisabled("Needs the beast list: log in and open the Master's Bestiary once.");
            return;
        }

        ImGui.SetNextItemWidth(260f);
        using (var combo = ImRaii.Combo("Board", Boards.ByNumber(this.teamBoard).Name))
        {
            if (combo)
            {
                foreach (var b in Boards.All)
                {
                    if (ImGui.Selectable(b.Name, b.Number == this.teamBoard))
                        this.teamBoard = b.Number;
                }
            }
        }

        var board = Boards.ByNumber(this.teamBoard);
        if (this.teamKey != (board.Number, this.session.Generation))
        {
            this.teamKey = (board.Number, this.session.Generation);
            var battles = this.data.Battles.GetValueOrDefault(board.Number) ?? [];
            this.team = TeamRecommender.Recommend(board, battles, this.data.Beasts.Where(b => owned.Contains(b.Id)).ToList(), c.Beasts);
        }
        if (this.team is not { } plan)
            return;

        ImGui.TextUnformatted($"Bring {plan.Roster.Count} of {board.TeamSize} (ranks sync to {board.SyncRank}; unseen beasts count as rank {TeamRecommender.UnseenRank}):");
        ImGui.TextWrapped(string.Join(", ", plan.Roster.Select(r => $"{r.Beast.Name} ({r.Rank})")));
        foreach (var pick in plan.Battles)
        {
            ImGui.Spacing();
            string enemies = string.Join(", ", pick.Battle.Enemies.Select(e => e.Name).Distinct());
            if (!ImGui.CollapsingHeader($"{(pick.Battle.IsBoss ? "Boss" : $"Battle {pick.Battle.Index}")}: {enemies}###battle{pick.Battle.Index}"))
                continue;
            foreach (var s in pick.Beasts)
            {
                ImGui.BulletText($"{s.Beast.Name} (rank {s.Rank}, score {s.Score:0.#})");
                if (s.Reasons.Count > 0)
                {
                    ImGui.Indent();
                    ImGui.TextDisabled(string.Join("; ", s.Reasons));
                    ImGui.Unindent();
                }
            }
        }
    }

    private void DrawPlugins()
    {
        ImGui.TextWrapped("A session drives AutoDuty, which walks to Lauda, sets up the team, moves across the board and switches the rotation plugin and BossMod's AI on for each board. " +
                           "Both BossMods answer AutoDuty under the same names, so only the one you use may be loaded during a run: Crucible SUX offers to turn the other off when a session or the journey starts and back on when it ends.");
        ImGui.TextUnformatted($"BossMod you use: {BossModChoices[(int)this.config.MechanicsPlugin]} (Settings)");
        foreach (var role in this.session.Dependencies)
        {
            ImGui.Spacing();
            ImGui.TextColored(StatusColor(role.Status), StatusWord(role.Status));
            ImGui.SameLine();
            ImGui.TextUnformatted($"{role.Role}{(role.Required ? string.Empty : " (optional)")}: {role.Detail}");
            ImGui.Indent();
            foreach (var cand in role.Candidates)
            {
                ImGui.TextColored(StatusColor(cand.Status), "•");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{cand.Info.DisplayName}: {cand.Detail}");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{cand.Info.Purpose}\n{cand.Info.RepoUrl}");
            }
            ImGui.Unindent();
        }
    }

    private void DrawSettings()
    {
        this.DrawSectionTitle("SESSION CONTROL", "Retries, stuck detection and mechanics engine");
        int attempts = this.config.MaxAttempts;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderInt("Tries per run", ref attempts, 1, 5))
        {
            this.config.MaxAttempts = attempts;
            this.save();
        }
        ImGui.TextDisabled("A run that fails outside the board is retried; inside a board, the session always stops.");
        int stuck = this.config.StuckMinutes;
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderInt("Stuck after (minutes)", ref stuck, 2, 15))
        {
            this.config.StuckMinutes = stuck;
            this.save();
        }
        ImGui.TextDisabled("Inside a board, a run with no movement, combat, HP or window change for this long is stopped.");
        ImGui.SetNextItemWidth(260f);
        using (var combo = ImRaii.Combo("BossMod you use", BossModChoices[(int)this.config.MechanicsPlugin]))
        {
            if (combo)
            {
                foreach (var choice in new[] { BossModChoice.Reborn, BossModChoice.Original })
                {
                    if (ImGui.Selectable(BossModChoices[(int)choice], choice == this.config.MechanicsPlugin))
                    {
                        this.config.MechanicsPlugin = choice;
                        this.save();
                    }
                }
            }
        }
        bool turnOff = this.config.TurnOffBossMod;
        if (ImGui.Checkbox("Turn the other BossMod off for runs without asking", ref turnOff))
        {
            this.config.TurnOffBossMod = turnOff;
            this.save();
        }
        ImGui.TextDisabled("A session or the journey turns it off when it starts and back on when it ends, with Dalamud's temporary switch. Off: you are asked each time.");

        ImGui.Spacing();
        this.DrawSectionTitle("BOARD AUTOMATION", "Team selection, scoring and combat settings");
        string[] teamChoices = ["Best 3 (score)", "Carry 3 + level others", "Use AutoDuty team settings"];
        int teamSelection = this.config.RunCarryLeveling ? 1 : this.config.RunTeamOfThree ? 0 : 2;
        ImGui.SetNextItemWidth(260f);
        using (var combo = ImRaii.Combo("Team selection", teamChoices[teamSelection]))
        {
            if (combo)
            {
                for (int i = 0; i < teamChoices.Length; i++)
                {
                    if (ImGui.Selectable(teamChoices[i], i == teamSelection))
                    {
                        this.config.RunTeamOfThree = i == 0;
                        this.config.RunCarryLeveling = i == 1;
                        this.save();
                    }
                }
            }
        }
        if (teamSelection == 0)
            ImGui.TextDisabled("Three high-ranked beasts for the Beast Squad III scoring bonus (4,250 points).");
        else if (teamSelection == 1)
            ImGui.TextDisabled("AutoDuty picks 3 high-ranked carriers and low-ranked beasts for EXP, up to the board's roster limit minus 2. Trades away Beast Squad III.");
        else
            ImGui.TextDisabled("Crucible SUX leaves team selection unchanged, using your existing AutoDuty settings.");
        bool shop = this.config.RunShop;
        if (ImGui.Checkbox("Let AutoDuty buy from the board shop", ref shop))
        {
            this.config.RunShop = shop;
            this.save();
        }
        ImGui.TextDisabled("Any purchase forfeits Coin Clutcher (500).");
        bool score = this.config.RunGoal == RunGoal.Score;
        if (ImGui.Checkbox("Score mode: no items, treasure or campsite rest", ref score))
        {
            this.config.RunGoal = score ? RunGoal.Score : RunGoal.Clear;
            this.save();
        }
        if (score)
            ImGui.TextColored(ImGuiColors.DalamudYellow, "Riskier: AutoDuty then has no way to heal you or your beasts, and the shop stays closed.");
        else
            ImGui.TextDisabled("Keeps Economic Ethos, Nameday Attire and Sleepless Survival, at the cost of the ways AutoDuty recovers.");
        bool pick = this.config.PickTargets;
        if (ImGui.Checkbox("Kill dangerous adds first", ref pick))
        {
            this.config.PickTargets = pick;
            this.save();
        }
        ImGui.TextDisabled("Targets enemies that buff others (the Pas de Seul's mage) and adds with arena-wide attacks (the Ogre's wisps) first, from the game's enemy intel.");
        bool share = this.config.ShareAggro;
        if (ImGui.Checkbox("Share aggro with the beast", ref share))
        {
            this.config.ShareAggro = share;
            this.save();
        }
        ImGui.TextDisabled("Hands the hits between you and your familiar with Snarl and Challenge, by HP and by the score bonuses still reachable.");
        if (share)
        {
            int playerFloor = this.config.PlayerFloor;
            ImGui.SetNextItemWidth(160f);
            if (ImGui.SliderInt("Your HP floor (%)", ref playerFloor, 10, 90))
            {
                this.config.PlayerFloor = playerFloor;
                this.save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You stop tanking before the next 15 s could take you below this. Dying ends the run.");
            int beastFloor = this.config.BeastFloor;
            ImGui.SetNextItemWidth(160f);
            if (ImGui.SliderInt("Familiar HP floor (%)", ref beastFloor, 0, 90))
            {
                this.config.BeastFloor = beastFloor;
                this.save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The familiar stops tanking before the next 15 s could take it below this. A KO costs No Beast Left Behind.");
        }
        bool items = this.config.UseItems;
        if (ImGui.Checkbox("Heal with Beast Potions", ref items))
        {
            this.config.UseItems = items;
            this.save();
        }
        ImGui.TextDisabled("Heals you or the familiar before a KO, and at the end of the final battle when Selfless or Self-preservation is worth the Economic Ethos a potion costs.");

        ImGui.Spacing();
        this.DrawSectionTitle("RANKING PARTICIPATION", "Explicit per-board opt-in");
        ImGui.TextDisabled("Automated runs should stay out of the rankings unless you opt a board in here. Crucible SUX cannot untick the option yet, so the Run tab warns when it matters.");
        foreach (var b in Boards.All)
        {
            bool on = this.config.RankingsOptIn.Contains(b.Number);
            if (ImGui.Checkbox($"{b.Name}##rank{b.Number}", ref on))
            {
                this.config.RankingsOptIn.Remove(b.Number);
                if (on)
                    this.config.RankingsOptIn.Add(b.Number);
                this.save();
            }
        }

        ImGui.Spacing();
        this.DrawSectionTitle("BESTIARY DATA", "Rank scanning and saved character data");
        bool autoScan = this.config.AutoScanBeasts;
        if (ImGui.Checkbox("Automatically scan ranks when the Bestiary opens outside a board", ref autoScan))
        {
            this.config.AutoScanBeasts = autoScan;
            this.save();
        }
        ImGui.TextDisabled("Pauses when AutoDuty is controlling the Bestiary; never clicks while AutoDuty is running.");
        int characters = this.config.Characters.Count;
        ImGui.TextDisabled($"Ranks read from the Bestiary are kept for {characters} character(s).");
        if (ImGui.Button("Forget all saved ranks") && ImGui.GetIO().KeyCtrl)
        {
            this.config.Characters.Clear();
            this.save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Hold Ctrl and click. Ranks come back as you open beasts' pages again.");
    }

    private static void DrawAbout()
    {
        ImGui.TextUnformatted("Crucible SUX: Set-and-forget Unbroken eXecutor");
        ImGui.Spacing();
        ImGui.TextWrapped("Reads your progress, plans your sitting, and runs a queue of boards through AutoDuty, vnavmesh, a rotation plugin and BossMod. " +
                          "It watches every run, stops one that is stuck and reports what happened (docs/PLAN.md).");
        ImGui.Spacing();
        ImGui.TextWrapped("Unattended automation is against the game's terms of service. Use it at your own risk.");
        ImGui.Spacing();
        ImGui.TextDisabled("Commands: /suxl opens this window; /suxl check prints what this sitting can cover; /suxl run starts the queue; " +
                           "/suxl journey starts the Beastmaster journey; /suxl stop stops either.");
    }

    private static void Mark(bool? done)
    {
        if (done is null)
            ImGui.TextDisabled("?");
        else if (done.Value)
            ImGui.TextColored(ImGuiColors.HealerGreen, "Yes");
        else
            ImGui.TextDisabled("No");
    }

    private static void Line(string label, bool? done)
    {
        ImGui.TextUnformatted($"{label}:");
        ImGui.SameLine();
        Mark(done);
    }

    private static Vector4 StatusColor(CheckStatus s) => s switch
    {
        CheckStatus.Pass => ImGuiColors.HealerGreen,
        CheckStatus.Warn => ImGuiColors.DalamudYellow,
        CheckStatus.Unknown => ImGuiColors.DalamudGrey,
        _ => ImGuiColors.DalamudRed,
    };

    private static string StatusWord(CheckStatus s) => s switch
    {
        CheckStatus.Pass => "OK",
        CheckStatus.Warn => "Warn",
        CheckStatus.Unknown => "Unknown",
        _ => "Missing",
    };
}
