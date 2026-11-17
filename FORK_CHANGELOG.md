# Crucible SUX Leveler — fork changelog

The upstream project's earlier release history remains in `CHANGELOG.md`. The fork tracks its own version numbers here.

## [0.3.30] - 2026-11-16

### Fixed

- Detect and recover Rotation Solver Reborn not activating when AutoDuty begins a Crucible board. After board entry and a six-second startup grace, assert RSR's **AutoDuty** mode with the same targeting and IPC overrides as AutoDuty.
- Monitor RSR's activation with bounded retries and report a clear failure after three unsuccessful requests instead of silently leaving the character unable to attack.
- Prevent journey-side rotation cleanup and leveling mode changes from switching off RSR while AutoDuty owns an active board.

### Added

- Dungeon rotation handoff state in the Run page, diagnostic events, and isolated regression tests.

### Release note

- The `v0.3.29` tag was accidentally pushed at the v0.3.28 manifest commit before the v0.3.29 changes reached GitHub. It failed version validation before compilation. The same correction is released as **v0.3.30**, leaving the old tag untouched.

## [0.3.28] - 2026-11-16

### Fixed

- Fixed v0.3.27 GitHub Actions compile failure CS0103 in `CrucibleSux/Ui/MainWindow.cs` lines 86 and 94: replaced unsupported `ImGuiChildFlags.Borders` argument with the `bool border` argument supported by the currently bundled `Dalamud.Bindings.ImGui` API.
- Preserved the original v0.3.27 two-pane sidebar and dashboard redesign without changing any combat automation.

### Note

- v0.3.27 was not published because its build failed; v0.3.28 is the corrective release. Does not address the Rotation Solver Reborn start-of-board handoff issue.

## [0.3.27] - 2026-11-16

### Changed

- Replaced the overcrowded horizontal tab strip with an original two-pane dashboard: grouped sidebar navigation, a persistent context header, and a scrollable workspace.
- Added a dashboard with live Beastmaster level, collection progress, verified ranks, planned boards, and shortcuts to the board runner, journey and roster.
- Kept every existing tab and its underlying behavior, including Bestiary rank scans, Dungeon roster, Team planner, diagnostics, configuration and safety messaging.
- The UI is original ImGui code and does not copy Keybound Brawler Autopilot/Glasswork source (AGPL-3.0).

### Note

- This is a presentation release only. It does **not** resolve the AutoDuty boss-engagement stall described before this UI request.

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
