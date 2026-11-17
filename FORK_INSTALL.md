# Crucible SUX Leveler v0.3.26 — direct source overlay

This ZIP contains **complete replacement `.cs` files**, not patch fragments. Extract it over the folder containing `CrucibleSux.sln`; existing source that is not included is unchanged.

## Changes

- Combat handoff: `CrucibleSux/Integrations/RotationSolverIpc.cs` now chooses an eligible adjacent mob before switching RSR into Manual mode when the journey has not yet selected a valid target. It preserves explicitly selected nearby targets, including capture targets and training dummies.
- Bestiary scan: `BestiaryWatcher` scans all **owned** beasts when the Bestiary opens outside a board and AutoDuty is idle. The scan can also be requested from the Beasts tab. The bestiary must be open for the plugin to see the rank fields; unknown/unowned beasts are never given invented levels.
- Dungeon roster: a new tab shows the selected beasts from `XBMPetParty`, with rank, last observed HP, and horn assignment, and keeps the latest snapshot after the registration window closes.
- Version 0.3.26.0 / Git tag `v0.3.26`; the old published plugin manifest remains at 0.3.25 until the new GitHub release is successfully published.

## Install

```bash
cd ~/Documents/git/CrucibleSux
unzip -o ~/Downloads/CrucibleSux_Leveler_v0.3.26_DropIn.zip
dotnet build CrucibleSux/CrucibleSux.csproj -c Release
dotnet test --project CrucibleSux.Tests -c Release
```

Run `/suxl`, open **Beasts**, then open the Master's Bestiary before entering a board. The scanner reads the rank of each owned beast. The **Dungeon roster** tab displays the registered team once a Crucible party screen is observed.

Commit the files and push. After CI passes, create `v0.3.26` and push that tag to publish the release and update `pluginmaster.json`.

**Important:** The old `pluginmaster.json` is deliberately not overwritten. It should continue serving the last valid release until the release workflow publishes v0.3.26. The plugin still needs live validation. A successful build alone does not publish or update Dalamud's repository listing.
