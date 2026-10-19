"""Builds the UPM tarball com.nanora.nanora-unity-mcp-<version>.tgz (Package Manager > Add package from tarball).

    python scripts/release/build_tarball.py <output dir>

npm layout (package/...); paths in Assets/NanoraUnityMCP/.npmignore are left out. Prints the tarball path.
"""
import fnmatch
import json
import os
import sys
import tarfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PACKAGE = os.path.join(ROOT, "Assets", "NanoraUnityMCP")


def package_version():
    with open(os.path.join(PACKAGE, "package.json"), encoding="utf-8") as f:
        return json.load(f)["version"]


def build_tarball(out_dir):
    with open(os.path.join(PACKAGE, ".npmignore"), encoding="utf-8") as f:
        ignored = [line.strip() for line in f if line.strip() and not line.startswith("#")]

    def skip(rel):
        return any(fnmatch.fnmatch(rel, p) or fnmatch.fnmatch(os.path.basename(rel), p) or rel.startswith(p) for p in ignored)

    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, f"com.nanora.nanora-unity-mcp-{package_version()}.tgz")
    with tarfile.open(path, "w:gz") as tar:
        for folder, _, files in os.walk(PACKAGE):
            for file in sorted(files):
                full = os.path.join(folder, file)
                rel = os.path.relpath(full, PACKAGE).replace(os.sep, "/")
                if not skip(rel):
                    tar.add(full, "package/" + rel)
    return path


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    print(build_tarball(sys.argv[1]))
