# Crucible SUX Leveler — fork changelog

The upstream project's earlier release history remains in `CHANGELOG.md`. The fork tracks its own version numbers here.

## [0.3.26] - 2026-11-16

### Fixed

- Combat handoff for open-world leveling: when vnavmesh reaches melee range with no valid current target, select an adjacent hostile before asking Rotation Solver Reborn to enter Manual mode. Preserve explicitly chosen quest and capture targets.
- Report failures to select a target in the plugin log instead of assuming that an RSR mode change began combat.

### Added

- A full pre-entry read of **all owned** beast ranks and EXP: when the Master's Bestiary opens outside a Crucible board, the plugin can visit each owned entry, including entries on other pages. It reads the game's real rank and EXP values instead of estimating them. The Beasts tab shows scan progress, lets you refresh or cancel, and Settings allows disabling automatic scanning.
- A new **Dungeon roster** tab showing the registered beasts, their observed ranks, saved EXP, last-seen HP, and Battlehorn assignment. The roster is captured from the game's registration window and retained after entry, then ranks are updated from results.

### Limitations

- Unowned beasts do not have a rank to read. The Bestiary must be opened before entry; a run will not open it automatically on behalf of the player. Scanning pauses while AutoDuty controls the same window.
- The registered roster is a snapshot; HP shown after the registration window closes is the last observed value, not continuous live dungeon HP.
- Changes require in-game testing against the current game's Bestiary layout and RSR behavior.

## [0.3.25] - 2026-11-16

### Added

- Separate Dalamud plugin identity (`CrucibleSuxLeveler`) and commands (`/suxl`, `/cruciblesuxleveler`) so it can be distinguished from upstream Crucible SUX.
- Carry-leveling roster selection through AutoDuty's `Leveling` / `Carry_Minus_2` mode, with tests.
- Direct beast-rank and EXP observations from Bestiary, registered teams, and board results, with rank-merging tests and an EXP column.

### Changed

- Fork repository URL, name, icon URL, and published package metadata now point to `pistolero-git/CrucibleSux`.
- Versioned releases use the project assembly version and matching `vX.Y.Z` Git tag, with validation before publication.
- Release packaging and checksum generation use `CrucibleSuxLeveler.dll` and its matching JSON manifest.

### Known limitations

- In-game UI offsets, rank/EXP observations, and AutoDuty leveling behavior require live validation.
- Release builds require the Dalamud development files and .NET 10 SDK.
