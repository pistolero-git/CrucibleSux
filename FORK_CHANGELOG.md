# Crucible SUX Leveler — fork changelog

The upstream project's earlier release history remains in `CHANGELOG.md`. The fork tracks its own version numbers here.

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
