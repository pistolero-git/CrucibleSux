# Fix the failed v0.3.27 release

The GitHub Actions job `44001937702` failed in **Build and test the fork** with two `CS0103` errors (`ImGuiChildFlags` missing). This package changes both `ImGui.BeginChild(...)` calls to use `true` as the third argument and bumps the fork version to `0.3.28.0`.

## Apply, validate and release v0.3.28

```bash
cd ~/Documents/git/CrucibleSux
git status --short
git pull --rebase --autostash origin main
unzip -o ~/Downloads/CrucibleSux_Leveler_v0.3.28_UI_Fix_DropIn.zip
python3 tools/validate_fork_release.py --tag v0.3.28
dotnet build CrucibleSux/CrucibleSux.csproj -c Release
dotnet test --project CrucibleSux.Tests -c Release
git diff --check
git add CrucibleSux/Ui/MainWindow.cs CrucibleSux/CrucibleSux.csproj FORK_CHANGELOG.md FORK_INSTALL.md UI_DROPIN_README.md FIX_AND_RELEASE.md
git commit -m "fix: restore Dalamud UI build for v0.3.28"
git push origin main
git fetch origin
git tag -a v0.3.28 origin/main -m "Crucible SUX Leveler v0.3.28"
git push origin v0.3.28
```

If `git status --short` showed existing uncommitted edits before the pull, review them before tagging. The ZIP deliberately contains no `pluginmaster.json`: the release workflow updates it after successful publication.

This UI fix does not repair the separately identified Rotation Solver Reborn activation issue.
