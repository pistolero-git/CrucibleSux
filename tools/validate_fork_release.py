"""Check fork identity, version policy and URL consistency before publishing."""
import argparse
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
REPO = "pistolero-git/CrucibleSux"
INTERNAL = "CrucibleSuxLeveler"


def validate(tag: str | None) -> None:
    p = ROOT / "CrucibleSux" / "CrucibleSux.csproj"
    props = ET.parse(p).getroot().find("PropertyGroup")
    if props is None:
        raise ValueError("No csproj PropertyGroup")
    prop = {item.tag: (item.text or "").strip() for item in props}
    version = prop.get("Version", "")
    if not re.fullmatch(r"\d+\.\d+\.\d+\.0", version):
        raise ValueError("csproj Version must have format X.Y.Z.0")
    base = ".".join(version.split(".")[:3])
    if tag and not re.fullmatch(rf"v{re.escape(base)}(?:-rc\d+)?", tag):
        raise ValueError(f"Tag {tag!r} does not match csproj version {version}")
    if prop.get("AssemblyName") != INTERNAL:
        raise ValueError("csproj AssemblyName must be CrucibleSuxLeveler")
    if prop.get("Name") != "Crucible SUX Leveler":
        raise ValueError("csproj display Name must be Crucible SUX Leveler")
    if prop.get("RepoUrl") != f"https://github.com/{REPO}":
        raise ValueError("csproj RepoUrl still references another repository")
    if prop.get("IconUrl") != f"https://raw.githubusercontent.com/{REPO}/main/assets/icon.png":
        raise ValueError("csproj IconUrl still references another repository")
    if prop.get("FileVersion") != version:
        raise ValueError("FileVersion must equal Version")
    notes = ROOT / "FORK_CHANGELOG.md"
    source = notes.read_text(encoding="utf-8")
    if not re.search(rf"^## \[{re.escape(base)}\]", source, re.M):
        raise ValueError(f"FORK_CHANGELOG.md lacks version {base}")
    repo_manifest = json.loads((ROOT / "pluginmaster.json").read_text(encoding="utf-8"))
    if not isinstance(repo_manifest, list) or len(repo_manifest) != 1:
        raise ValueError("pluginmaster.json must have exactly one entry")
    m = repo_manifest[0]
    if m.get("InternalName") != INTERNAL or m.get("Name") != "Crucible SUX Leveler":
        raise ValueError("pluginmaster.json has the wrong plugin identity")
    if m.get("RepoUrl") != f"https://github.com/{REPO}":
        raise ValueError("pluginmaster.json RepoUrl points to the wrong repository")
    for fld in ("DownloadLinkInstall", "DownloadLinkTesting", "DownloadLinkUpdate"):
        url = m.get(fld, "")
        if not url.startswith(f"https://github.com/{REPO}/releases/download/"):
            raise ValueError(f"pluginmaster.json {fld} points to another repository")
    if m.get("IconUrl") != prop["IconUrl"]:
        raise ValueError("pluginmaster.json IconUrl differs from csproj")
    if m.get("AssemblyVersion") != version:
        # Pre-release development may carry an older published version; never a different plugin identity.
        if m.get("IsHide") is not True:
            print("NOTE: published manifest version differs from development version; verify deliberate bump", file=sys.stderr)
    print(f"Fork validation succeeded: {INTERNAL} {version}, repository {REPO}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--tag", default=None)
    args = parser.parse_args()
    try:
        validate(args.tag)
    except (ValueError, OSError, ET.ParseError, json.JSONDecodeError) as err:
        print(f"VALIDATION FAILED: {err}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
