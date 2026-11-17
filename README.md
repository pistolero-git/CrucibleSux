# Crucible SUX Leveler

A maintained fork of [Crucible SUX](https://github.com/xenofei/CrucibleSux) by Michiru Tsukikage, with beast carry-leveling and improved rank/EXP tracking. This repository and its releases are hosted at **https://github.com/pistolero-git/CrucibleSux**.

**Plugin:** Crucible SUX Leveler  
**Internal name / DLL:** `CrucibleSuxLeveler` / `CrucibleSuxLeveler.dll`  
**Command:** `/suxl` (alias `/cruciblesuxleveler`)  
**Version source of truth:** `CrucibleSux/CrucibleSux.csproj` (`0.3.25.0` for the initial fork release)

The plugin automates boards in FFXIV's Crucible of the Unbroken through AutoDuty and tracks Beastmaster progress. **Unattended automation violates the game's terms of service. Use it at your own risk.** The session queue and journey are experimental; do not assume they are safe or fully tested.

## Fork changes

- **Carry 3 + level others:** optionally ask AutoDuty to select its three strong carrier beasts and lower-ranked beasts for EXP (`TeamMode=Leveling`, `LevelingMode=Carry_Minus_2`). The default scoring mode continues to register the best three beasts.
- **Direct rank and EXP capture:** read observed beast progress from the Master's Bestiary, registered party screen and board results. Display the saved rank and EXP in the Beasts tab. Unknown/unseen beasts remain unknown.
- **Separate plugin identity:** install/load independently of upstream Crucible SUX using a different assembly, display name and `/suxl` command. The two plugins still control the same game/AutoDuty state, so do not run their automation simultaneously.

The new rank readers have unit tests for merging progress; correctness of live FFXIV UI offsets and leveling choices still needs in-game verification.

## Core features inherited from upstream

- **Run:** queue Crucible boards; checks character readiness, rankings opt-in, dependencies, and retries. Uses AutoDuty, an appropriate rotation plugin and BossMod AI while recording progress.
- **History:** records runs and results per character.
- **Journey:** optional Beastmaster quests, acquiring beasts, and character leveling; each part remains opt-in.
- **Overview:** clears, degrees, Apex and Legendary results and achievements.
- **One sitting:** checks which remaining boards can be attempted sequentially.
- **Beasts:** befriended beasts, ranks, EXP, and locations.
- **Teams:** experimental board-specific suggestions based on game data.
- **Plugins:** checks required companion tools and warns about conflicts.
- **Log:** combat and automation diagnostics for troubleshooting.

## Commands

| Command | Action |
|---|---|
| `/suxl` | Open Crucible SUX Leveler |
| `/suxl check` | Report which boards can be cleared in one sitting |
| `/suxl run` | Start the queued session |
| `/suxl journey` | Start the opt-in Beastmaster journey |
| `/suxl stop` | Stop the session or journey |
| `/suxl report` | Copy a recent log summary |
| `/suxl report full` | Copy the full log |

## Install

Add this URL in `/xlsettings` > Experimental > Custom Plugin Repositories after the fork publishes its first release:

```
https://raw.githubusercontent.com/pistolero-git/CrucibleSux/main/pluginmaster.json
```

In `/xlplugins`, look for **Crucible SUX Leveler**. **The initial `pluginmaster.json` is intentionally hidden until `v0.3.25` is released**, preventing users from following an uncreated download link. Publishing the stable release unhides it automatically.

For a development build, open `/xlsettings` > Experimental > Dev Plugin Locations and point to the compiled `CrucibleSuxLeveler.dll`, not the upstream `CrucibleSux.dll`. See [FORK_INSTALL.md](FORK_INSTALL.md) for details.

## Build

Requires .NET 10 SDK and Dalamud development libraries. XIVLauncher.Core typically puts the latter under `~/.xlcore/dalamud/Hooks/dev` on Linux; you can explicitly set `DALAMUD_HOME`.

```bash
export DALAMUD_HOME="$HOME/.xlcore/dalamud/Hooks/dev"
dotnet build CrucibleSux/CrucibleSux.csproj -c Release
dotnet test --project CrucibleSux.Tests -c Release
```

The DLL is under `CrucibleSux/bin/x64/Release/` on many Linux builds. Find `CrucibleSuxLeveler.dll` if your SDK chooses a different output folder. The release package is `CrucibleSuxLeveler/latest.zip` under the configured Release output.

If you build the *full solution* on Linux, `tools/CrucibleSux.DataDump` also needs `Lumina.dll` and `Lumina.Excel.dll` in `DALAMUD_HOME`; its project file includes Linux and Windows path handling. The DataDump tool is not required to run the plugin.

## Versioning and releases

- Plugin assembly version: `CrucibleSux/CrucibleSux.csproj` → `<Version>0.3.25.0</Version>`.
- Release tag: `v0.3.25` (three components, matching the first three assembly version components).
- Fork release notes: [`FORK_CHANGELOG.md`](FORK_CHANGELOG.md). Earlier upstream history is retained in [`CHANGELOG.md`](CHANGELOG.md).
- CI validates fork identity, repository URLs and version policy.
- Stable tag builds and tests the fork, checks the packaged `CrucibleSuxLeveler.dll` / `CrucibleSuxLeveler.json`, publishes `latest.zip`, and updates `pluginmaster.json` on `main` with the fork's own download URLs and `LastUpdate`.
- Release-candidate tags `vX.Y.Z-rcN` publish a prerelease without updating the stable repository manifest.

For step-by-step publishing, read [`FORK_INSTALL.md`](FORK_INSTALL.md).

## Credit and license

Original plugin: [xenofei/CrucibleSux](https://github.com/xenofei/CrucibleSux). Fork and distribution: [pistolero-git/CrucibleSux](https://github.com/pistolero-git/CrucibleSux). Original source and fork changes remain MIT-licensed; see [`LICENSE`](LICENSE). This project is not the official upstream repository.
