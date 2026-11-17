# Crucible SUX Leveler v0.3.30 — RSR dungeon handoff recovery

This release restores and verifies the Rotation Solver Reborn (RSR) handoff during AutoDuty-controlled Crucible boards. It preserves the v0.3.28 dashboard, Bestiary rank scan, dungeon roster, and carry leveling.

**The failed v0.3.29 GitHub Actions run did not compile:** its tag pointed to the v0.3.28 commit. Do not retag v0.3.29. Use the new v0.3.30 tag instead.

## Install, verify, and publish

Extract `CrucibleSux_Leveler_v0.3.30_RSR_Recovery_FixedRelease.zip` into `~/Downloads`, then:

```bash
cd ~/Documents/git/CrucibleSux
# Only when local and remote have diverged, reconcile first:
git pull --rebase --autostash origin main
python3 ~/Downloads/CrucibleSux_v0.3.30_RSR_Recovery/apply_v0_3_30.py --check
python3 ~/Downloads/CrucibleSux_v0.3.30_RSR_Recovery/apply_v0_3_30.py --apply
python3 ~/Downloads/CrucibleSux_v0.3.30_RSR_Recovery/apply_v0_3_30.py --verify
```

Then **use the included** `publish_v0_3_30.sh` to validate/build/test, stage all ten exact source/document paths, verify the staging area, commit, push to `main`, confirm remote SHA, and only then push tag `v0.3.30`. Alternatively follow the explicit steps in the ZIP README.

## Files changed

- `CrucibleSux/Automation/SessionRunner.cs`
- `CrucibleSux/Integrations/RotationSolverIpc.cs`
- `CrucibleSux/Plugin.cs`
- `CrucibleSux/Ui/SessionTabs.cs`
- `CrucibleSux/CrucibleSux.csproj`
- `FORK_CHANGELOG.md`
- `FORK_INSTALL.md`
- `CrucibleSux.Core/BoardRotationRecovery.cs` (new)
- `CrucibleSux/Integrations/BoardRotationIpc.cs` (new)
- `CrucibleSux.Tests/BoardRotationRecoveryTests.cs` (new)

## Limitations

Testing is still required against the current Dalamud SDK and in-game. The check and installer verify structure, not compiled execution. Do not modify `pluginmaster.json` by hand; GitHub Actions updates it after a successful tagged release.
