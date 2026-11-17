# Crucible SUX Leveler — installation and release guide

This repository is the fork at **https://github.com/pistolero-git/CrucibleSux**. The original upstream repository is https://github.com/xenofei/CrucibleSux.

## Apply this drop-in ZIP

Extract the ZIP **over the current repository root**, the directory containing `CrucibleSux.sln`, and overwrite files. The ZIP contains complete source/configuration files in their relative directories: **no patch or assembly script is needed**. It also includes the existing two feature implementations, so you do not need earlier ZIPs.

## Plugin identity

| | Original Crucible SUX | This fork |
|---|---|---|
| Display name | Crucible SUX | Crucible SUX Leveler |
| Internal name | CrucibleSux | CrucibleSuxLeveler |
| DLL | CrucibleSux.dll | CrucibleSuxLeveler.dll |
| Command | /sux | /suxl |
| Repository | xenofei/CrucibleSux | pistolero-git/CrucibleSux |

The solution filename and C# namespaces remain unchanged; they do not determine the plugin's internal identity. The fork's separate configuration location means existing upstream beast ranks/settings are not automatically migrated.

## Build and test on Bazzite / Linux

Install the .NET 10 SDK and make Dalamud development files discoverable:

```bash
export DALAMUD_HOME="$HOME/.xlcore/dalamud/Hooks/dev"
dotnet build CrucibleSux/CrucibleSux.csproj -c Release
dotnet test --project CrucibleSux.Tests -c Release
python3 tools/validate_fork_release.py
```

For a local development plugin, add the built `CrucibleSuxLeveler.dll` under `/xlsettings` > Experimental > Dev Plugin Locations. Remove obsolete CrucibleSux dev locations that point to the same path. Restart FFXIV if an old plugin is already loaded. Do not simultaneously run automated sessions in upstream and fork.

## Release v0.3.25

1. Commit the replaced files to `main` and allow CI to finish successfully. If GitHub Actions is unavailable or disabled, enable it for your repository.
2. Confirm `CrucibleSux/CrucibleSux.csproj` has `<Version>0.3.25.0</Version>` and `FORK_CHANGELOG.md` has a `[0.3.25]` section.
3. Push a matching tag:

   ```bash
   git tag v0.3.25
   git push origin main
   git push origin v0.3.25
   ```

4. The release workflow checks the version and plugin identity, builds and tests, ensures `CrucibleSuxLeveler.dll` and `CrucibleSuxLeveler.json` are in `latest.zip`, then publishes the GitHub release.
5. After a **stable** release succeeds, the workflow updates `pluginmaster.json` on `main`, unhiding the fork entry and linking to `https://github.com/pistolero-git/CrucibleSux/releases/download/v0.3.25/latest.zip`.
6. Configure the following URL under Dalamud custom repositories, then install **Crucible SUX Leveler**:

   ```text
   https://raw.githubusercontent.com/pistolero-git/CrucibleSux/main/pluginmaster.json
   ```

**Before step 4**, the manifest deliberately sets `IsHide: true` because no v0.3.25 downloadable release exists yet. This is intentional. It becomes installable after the stable release updates it.

## Every later release

Increment the `<Version>` and `<FileVersion>` fields in `CrucibleSux.csproj` to the next `X.Y.Z.0`, add a matching `## [X.Y.Z]` section in `FORK_CHANGELOG.md`, push the code, and tag `vX.Y.Z`. The workflow rejects a version/tag mismatch or upstream plugin identity. Each stable release creates updated metadata and `LastUpdate`, enabling Dalamud upgrades.

For a prerelease, use `vX.Y.Z-rcN`. This creates a prerelease artifact without updating the stable `pluginmaster.json`.

## Current limitations

Live UI field offsets for beast ranks/EXP and AutoDuty leveling behavior have not been validated against FFXIV in this environment. Run tests and check the plugin in game before general distribution. Publishing is not performed by this ZIP; you must commit and tag it in your repository.
