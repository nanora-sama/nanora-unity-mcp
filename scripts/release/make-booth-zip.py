"""Builds the BOOTH download: a UPM tarball, the .unitypackage, the Shooting Game sample, a Japanese README and the licenses.

The tarball is the recommended install (Package Manager > Add package from tarball): Unity resolves the
dependencies (Newtonsoft JSON, Test Framework, uGUI) and the sample is importable from the package page.
A .unitypackage installs none of the dependencies and cannot carry Samples~.

    python scripts/release/make-booth-zip.py <nanora-unity-mcp-<version>.unitypackage> <output dir>

Make the .unitypackage first (Unity batch mode, from the repository root):
    Unity -batchmode -quit -projectPath . -executeMethod NanoraUnityMCP.UI.PackageExporter.ExportFromCLI -outputPath <file>
A .unitypackage cannot carry Samples~, so the sample is added as a plain folder.
"""
import os
import sys
import zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_tarball import PACKAGE, ROOT, build_tarball, package_version  # noqa: E402


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    unitypackage, out_dir = sys.argv[1], sys.argv[2]
    version = package_version()
    name = f"nanora-unity-mcp-{version}"
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, f"{name}-booth.zip")
    with open(os.path.join(os.path.dirname(__file__), "booth", "README_JP.txt"), encoding="utf-8") as f:
        readme = f.read().replace("{VERSION}", version).replace("\n", "\r\n")

    tarball = build_tarball(out_dir)

    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(tarball, f"{name}/{os.path.basename(tarball)}")
        z.write(unitypackage, f"{name}/{name}.unitypackage")
        z.writestr(f"{name}/README_JP.txt", readme.encode("utf-8-sig"))
        z.write(os.path.join(ROOT, "LICENSE"), f"{name}/LICENSE")
        z.write(os.path.join(ROOT, "THIRD-PARTY-NOTICES.md"), f"{name}/THIRD-PARTY-NOTICES.md")
        sample = os.path.join(PACKAGE, "Samples~", "ShootingGame")
        for folder, _, files in os.walk(sample):
            for file in files:
                path = os.path.join(folder, file)
                z.write(path, f"{name}/Samples/ShootingGame/" + os.path.relpath(path, sample).replace(os.sep, "/"))
    print(f"{out} ({os.path.getsize(out) // 1024} KB)")


if __name__ == "__main__":
    main()
