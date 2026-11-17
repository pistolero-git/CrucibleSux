# Crucible SUX Leveler v0.3.27 — original dashboard UI

This ZIP contains **complete replacement source files**, not patches. It is designed to overlay the **v0.3.26** source in `pistolero-git/CrucibleSux` and preserves existing plugin behavior.

## Changes

- A polished two-column interface with sidebar navigation instead of 12 scrolling horizontal tabs.
- A dashboard with character level, collection, verified beast ranks, planned boards and quick navigation.
- Separate Operations, Beasts, Activity and System navigation categories.
- Retains all existing pages, settings, scanning controls, run controls, journey, Dungeon roster, Team planning and logs.
- Displays the installed assembly version automatically in the sidebar.
- Pure ImGui/Dalamud UI; no Glasswork dependency and no copied AGPL sources or assets.

## Installation

```bash
cd ~/Documents/git/CrucibleSux
git pull --rebase origin main
unzip -o ~/Downloads/CrucibleSux_Leveler_v0.3.27_UI_DropIn.zip
dotnet build CrucibleSux/CrucibleSux.csproj -c Release
dotnet test --project CrucibleSux.Tests -c Release
git diff --check
git status
```

Commit and push when the build and tests pass. Create and push the tag `v0.3.27` only after verifying the UI in Dalamud. Release Actions will update `pluginmaster.json` after publishing a package.

**Not included**: a fix for the AutoDuty boss-encounter standstill. The earlier boss issue remains separate from this UI redesign.
