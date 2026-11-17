# Crucible SUX Leveler — fork overlay

This bundle contains both previously requested changes and gives the fork a **different Dalamud internal identity**.

- Display name: **Crucible SUX Leveler**
- Assembly/InternalName: **CrucibleSuxLeveler**
- Output DLL: **CrucibleSuxLeveler.dll**
- Commands: **/suxl** and **/cruciblesuxleveler** (instead of /sux and /cruciblesux)
- Original solution: **CrucibleSux.sln** (unchanged)

## Apply to your existing checkout

1. Commit or back up local modifications. This ZIP overwrites 5 existing files and adds 3 C# files plus this patch and note.
2. Extract the ZIP **at the repository root** (the folder containing `CrucibleSux.sln`).
3. Apply the one remaining command-rename patch (because the original `Plugin.cs` is not bundled in this overlay):

   ```bash
   git apply --check CrucibleSux_Plugin_Command_Rename.patch
   git apply CrucibleSux_Plugin_Command_Rename.patch
   ```

4. Build with `dotnet build CrucibleSux.sln -c Release`.
5. In `/xlsettings` > Experimental > Dev Plugin Locations, remove the old CrucibleSux development DLL path and add the **new** `CrucibleSuxLeveler.dll` path from your build output.
6. Fully restart the game; invoke `/suxl` for the fork, `/sux` for the original.

**Do not aim both plugins at the same DLL path.** Also do not run automation in both plugins at once because both control AutoDuty and the game.

## Important — repository installations

This overlay changes local build identity and in-game command. The existing `pluginmaster.json` and `.github/workflows/release.yml` in your checkout still target the ORIGINAL CrucibleSux build and/or the upstream release. They are **not** valid for distributing the renamed fork as a separate custom repository plugin without additional changes and a published fork release. Do not try to install the renamed fork from the old `pluginmaster.json`.

After changing InternalName, Dalamud saves new settings under a different plugin identity; previous Crucible SUX character rank cache and settings are **not** migrated automatically. The original plugin remains independent. The file namespace/class names and core library do not need renaming. The solution does not require an update.

No .NET SDK and Dalamud compilation environment were available to validate a complete build. Source overlay and patch contents are provided for local build/testing.
