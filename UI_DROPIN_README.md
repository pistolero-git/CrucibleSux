# Crucible SUX Leveler v0.3.28 — Modern Menu Drop-In

Corrective release on top of `pistolero-git/CrucibleSux` **v0.3.27**. This overlay contains **full replacement files**, not patches. The new menu is an original Dalamud/ImGui implementation inspired by the *information hierarchy* of the Keybound Brawler Autopilot interface. It does not include or copy AGPL/Glasswork source or assets.

## Install

Unzip over your local repository root after pulling the current main branch:

```bash
cd ~/Documents/git/CrucibleSux
git pull --rebase origin main
unzip -o ~/Downloads/CrucibleSux_Leveler_v0.3.28_UI_DropIn.zip
dotnet build CrucibleSux/CrucibleSux.csproj -c Release
dotnet test --project CrucibleSux.Tests -c Release
git diff --check
git status
```

The layout keeps the original page functions and opens **Settings** through Dalamud's config shortcut. Its left rail groups **Command** (Dashboard, Board runner, Journey, Readiness), **Beasts** (Bestiary, Dungeon roster, Team planner), **Activity** (History, Diagnostics), and **System** (Dependencies, Settings, About). The dashboard exposes collection and verified-rank metrics with quick page links.

## Release steps

After build, commit and push. When the release is ready, tag the version on the updated main branch (`v0.3.28`) and push the tag. The release workflow owns updates to `pluginmaster.json`; this overlay intentionally does not include it.

## Important

This is a **UI-compile-fix-only** update. The previously reported boss standstill in AutoDuty is not fixed by changing this menu.

The original v0.3.27 GitHub Action failed with CS0103 at MainWindow.cs lines 86/94; these two calls now use the `bool border` overload. The plugin could not be compiled in this packaging environment because the .NET/Dalamud SDK is unavailable: build and test locally before tagging.
