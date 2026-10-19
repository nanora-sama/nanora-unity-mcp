"""Prints the CHANGELOG section of one version as GitHub release notes, followed by install lines.

    python scripts/release/changelog_section.py <version> [<owner/repo> <tag>]

Exits with an error when the version has no section, so a release is never published without notes.
"""
import os
import re
import sys

CHANGELOG = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "NanoraUnityMCP", "CHANGELOG.md")


def section(version):
    with open(CHANGELOG, encoding="utf-8") as f:
        text = f.read().replace("\r\n", "\n")
    match = re.search(r"^## \[" + re.escape(version) + r"\][^\n]*\n(.*?)(?=^## \[|\Z)", text, re.S | re.M)
    return match.group(1).strip() if match else ""


if __name__ == "__main__":
    if len(sys.argv) not in (2, 4):
        sys.exit(__doc__)
    body = section(sys.argv[1])
    if not body:
        sys.exit(f"no CHANGELOG section for {sys.argv[1]}")
    print(body)
    if len(sys.argv) == 4:
        repo, tag = sys.argv[2], sys.argv[3]
        print("\n---\n")
        print("Install: Package Manager > + > *Add package from tarball...* with the .tgz below, or *Add package from git URL...*:")
        print(f"`https://github.com/{repo}.git?path=Assets/NanoraUnityMCP#{tag}`")
