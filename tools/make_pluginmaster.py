"""Publish the custom Dalamud repository entry from a BUILT fork package.

The package's `CrucibleSuxLeveler.json`, not the old upstream `CrucibleSux.json`, is authoritative.
Examples:
  python tools/make_pluginmaster.py --tag v0.3.25
  python tools/make_pluginmaster.py --tag v0.3.25 --output /tmp/pluginmaster.json
"""
import argparse
import json
from pathlib import Path
import re
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
INTERNAL_NAME = "CrucibleSuxLeveler"
DEFAULT_REPO = "pistolero-git/CrucibleSux"


def release_notes(version: str) -> str:
    """Return the fork changelog section for the semantic version."""
    path = ROOT / "FORK_CHANGELOG.md"
    if not path.is_file():
        raise ValueError(f"Missing {path}")
    captured = []
    capture = False
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("## ["):
            if capture:
                break
            capture = line.startswith(f"## [{version}]")
        elif capture:
            captured.append(line)
    notes = "\n".join(captured).strip()
    if not notes or not any(line.lstrip().startswith("- ") for line in notes.splitlines()):
        raise ValueError(f"Missing release notes in FORK_CHANGELOG.md for [{version}]")
    return notes


def find_manifest() -> Path:
    # SDK output can use either bin/Release or bin/x64/Release depending on the build.
    candidates = list((ROOT / "CrucibleSux" / "bin").glob(
        f"**/{INTERNAL_NAME}/{INTERNAL_NAME}.json"))
    if not candidates:
        raise FileNotFoundError(f"Could not find {INTERNAL_NAME}.json; build CrucibleSux.csproj first")
    return max(candidates, key=lambda p: p.stat().st_mtime)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=DEFAULT_REPO)
    parser.add_argument("--tag", required=True, help="vX.Y.Z or vX.Y.Z-rcN")
    parser.add_argument("--branch", default="main")
    parser.add_argument("--manifest", type=Path, default=None, help="Override packaged manifest location")
    parser.add_argument("--output", type=Path, default=ROOT / "pluginmaster.json")
    args = parser.parse_args()

    match = re.fullmatch(r"v(\d+\.\d+\.\d+)(?:-rc\d+)?", args.tag)
    if not match:
        parser.error("tag must be vX.Y.Z or vX.Y.Z-rcN")
    version = match.group(1)
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.repo):
        parser.error("--repo must be GitHub owner/repository")
    if not re.fullmatch(r"[A-Za-z0-9._/-]+", args.branch):
        parser.error("--branch must be a branch name")

    manifest_path = args.manifest or find_manifest()
    entry = json.loads(manifest_path.read_text(encoding="utf-8"))
    if entry.get("InternalName") != INTERNAL_NAME:
        raise ValueError(f"Package InternalName is {entry.get('InternalName')!r}, not {INTERNAL_NAME}")
    expected_version = version + ".0"
    if entry.get("AssemblyVersion") != expected_version:
        raise ValueError(f"Package version {entry.get('AssemblyVersion')!r} does not match {args.tag} ({expected_version})")
    if entry.get("Name") != "Crucible SUX Leveler":
        raise ValueError("Package Name must be Crucible SUX Leveler")
    notes = release_notes(version)
    base = f"https://github.com/{args.repo}"
    raw = f"https://raw.githubusercontent.com/{args.repo}/{args.branch}"
    download = f"{base}/releases/download/{args.tag}/latest.zip"
    entry.update({
        "RepoUrl": base,
        "IconUrl": f"{raw}/assets/icon.png",
        "DownloadLinkInstall": download,
        "DownloadLinkTesting": download,
        "DownloadLinkUpdate": download,
        "Description": entry.get("Description", "").replace("\r\n", "\n"),
        "Changelog": notes,
        "LastUpdate": int(time.time()),
        "DownloadCount": 0,
        "IsHide": False,
        "IsTestingExclusive": False,
    })
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps([entry], indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Wrote {args.output} with {INTERNAL_NAME} {expected_version} -> {download}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, FileNotFoundError, json.JSONDecodeError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        sys.exit(1)
