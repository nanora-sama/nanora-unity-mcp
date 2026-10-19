"""Prepare disposable Unity CI projects and reject missing/failed test evidence."""
import argparse
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


VERSIONS = {
    "2021.3.45f2": {"com.unity.inputsystem": "1.7.0", "com.unity.test-framework": "1.3.0", "com.unity.ugui": "1.0.0", "com.unity.textmeshpro": "3.0.6"},
    "2022.3.62f3": {"com.unity.inputsystem": "1.10.0", "com.unity.test-framework": "1.4.5", "com.unity.ugui": "1.0.0", "com.unity.textmeshpro": "3.0.6"},
    "6000.3.4f1": {"com.unity.inputsystem": "1.18.0", "com.unity.test-framework": "1.6.0", "com.unity.ugui": "2.0.0"},
}
UNITY6_MODULES = {"com.unity.modules.accessibility", "com.unity.modules.adaptiveperformance", "com.unity.modules.vectorgraphics"}


def prepare(root, project, version):
    if project not in (".", "TestProject") or version not in VERSIONS:
        raise ValueError("Unsupported CI project or Unity version")
    root = Path(root)
    target = root / project
    manifest_path = target / "Packages/manifest.json"
    root_manifest = json.loads((root / "Packages/manifest.json").read_text(encoding="utf-8-sig"))
    if project == "TestProject":
        # Exercise the UPM install and its Newtonsoft dependency, with the input/UI
        # packages used by the supported editor features and the test framework.
        deps = {k: v for k, v in root_manifest["dependencies"].items() if k.startswith("com.unity.modules.") and k not in UNITY6_MODULES}
        deps.update(VERSIONS[version])
        package = json.loads((root / "Assets/NanoraUnityMCP/package.json").read_text(encoding="utf-8-sig"))
        deps[package["name"]] = "file:../../Assets/NanoraUnityMCP"
        manifest = {"dependencies": deps, "testables": [package["name"]]}
    else:
        manifest = root_manifest
        deps = manifest["dependencies"]
        if not version.startswith("6000."):
            for name in UNITY6_MODULES | {"com.unity.render-pipelines.universal", "com.unity.ai.navigation", "com.unity.multiplayer.center"}:
                deps.pop(name, None)
            if version.startswith("2021."):
                for name in ("com.unity.cinemachine", "com.unity.visualscripting"):
                    deps.pop(name, None)
                overrides = {"com.unity.timeline": "1.6.5", "com.unity.ide.rider": "3.0.28", "com.unity.ide.visualstudio": "2.0.22", "com.unity.collab-proxy": "2.0.7", "com.unity.nuget.newtonsoft-json": "3.0.2"}
            else:
                overrides = {"com.unity.cinemachine": "2.10.1", "com.unity.timeline": "1.7.6", "com.unity.ide.rider": "3.0.31", "com.unity.collab-proxy": "2.4.4", "com.unity.visualscripting": "1.9.2", "com.unity.nuget.newtonsoft-json": "3.2.1"}
            deps.update({k: v for k, v in overrides.items() if k in deps})
        deps.update(VERSIONS[version])
    for folder in ("Assets", "Packages", "ProjectSettings"):
        (target / folder).mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    (target / "Packages/packages-lock.json").unlink(missing_ok=True)
    (target / "ProjectSettings/ProjectVersion.txt").write_text(f"m_EditorVersion: {version}\n", encoding="utf-8")
    print(f"Prepared {project} for Unity {version}")


def check_results(folder):
    files = list(Path(folder).rglob("*-results.xml"))
    if not files:
        raise ValueError("No NUnit results were produced; Unity tests did not complete")
    total = passed = 0
    for path in files:
        result = ET.parse(path).getroot()
        if result.tag != "test-run" or int(result.get("failed", "0")) != 0:
            raise ValueError(f"Invalid or failed NUnit run: {path}")
        cases = result.findall(".//test-case")
        if any(case.get("result") == "Failed" for case in cases):
            raise ValueError(f"Failed test case: {path}")
        total += len(cases)
        passed += sum(case.get("result") == "Passed" for case in cases)
    if passed == 0:
        raise ValueError("No passing tests were recorded (empty or fully skipped run)")
    print(f"NUnit evidence: {passed} passed / {total} discovered")


LICENSE_VARIABLES = ("UNITY_LICENSE", "UNITY_EMAIL", "UNITY_PASSWORD")


def check_license(env):
    """Fail before Unity starts when the license secrets did not reach this run (values are never printed).

    GitHub does not give Actions secrets to Dependabot or fork pull requests; Unity then fails with
    "Licensing method: <none>" and only "No NUnit results" is reported, which reads like a test failure.
    On push or workflow_dispatch the secrets are simply not configured for the repository.
    """
    missing = [name for name in LICENSE_VARIABLES if not env.get(name, "").strip()]
    if missing:
        actor = env.get("GITHUB_ACTOR", "?")
        event = env.get("GITHUB_EVENT_NAME", "?")
        secrets = [env.get("UNITY_LICENSE_SECRET", "UNITY_LICENSE") if name == "UNITY_LICENSE" else name for name in missing]
        problem = f"Unity license secrets are empty for this run ({', '.join(secrets)}; actor={actor}, event={event}). "
        repository = env.get("GITHUB_REPOSITORY", "this repository")
        configure = (f"add them to {repository} under Settings > Secrets and variables > Actions "
                     "(see .github/workflows/README.md#secrets)")
        if actor == "dependabot[bot]":
            hint = "Dependabot pull requests do not receive Actions secrets: add them as Dependabot secrets, or run the workflow with workflow_dispatch."
        elif event in ("pull_request", "pull_request_target"):
            hint = f"Fork pull requests do not receive Actions secrets (run the workflow with workflow_dispatch); for a same-repository pull request, {configure}."
        else:
            hint = f"The secrets are not configured for this repository: {configure}."
        raise ValueError(problem + hint)
    print("Unity license secrets are present")


def check_jobs(latest, legacy, event):
    expected_legacy = "skipped" if event == "pull_request" else "success"
    if latest != "success" or legacy != expected_legacy:
        raise ValueError(f"Unity jobs did not pass: latest={latest}, legacy={legacy} (expected {expected_legacy})")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    prep = commands.add_parser("prepare")
    prep.add_argument("project", choices=[".", "TestProject"])
    prep.add_argument("version", choices=VERSIONS)
    results = commands.add_parser("results")
    results.add_argument("folder")
    jobs = commands.add_parser("jobs")
    jobs.add_argument("latest")
    jobs.add_argument("legacy")
    jobs.add_argument("event", choices=["push", "pull_request", "workflow_dispatch"])
    commands.add_parser("license")
    args = parser.parse_args()
    try:
        if args.command == "prepare":
            prepare(Path.cwd(), args.project, args.version)
        elif args.command == "results":
            check_results(args.folder)
        elif args.command == "license":
            check_license(os.environ)
        else:
            check_jobs(args.latest, args.legacy, args.event)
    except (ValueError, OSError, ET.ParseError) as error:
        print(f"CI verification failed: {error}", file=sys.stderr)
        print(f"::error::{error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
