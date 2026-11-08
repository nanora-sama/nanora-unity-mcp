import json
from pathlib import Path
import tempfile
import unittest

from unity_ci import LICENSE_VARIABLES, VERSIONS, check_jobs, check_license, check_results, prepare


class CiVerificationTests(unittest.TestCase):
    def test_summary_rejects_failures_cancellations_and_unexpected_skips(self):
        for event in ("push", "workflow_dispatch", "pull_request"):
            expected = "skipped" if event == "pull_request" else "success"
            check_jobs("success", expected, event)
            for latest in ("failure", "cancelled", "skipped"):
                with self.subTest(event=event, latest=latest), self.assertRaises(ValueError):
                    check_jobs(latest, expected, event)
            for legacy in ("failure", "cancelled", "success" if event == "pull_request" else "skipped"):
                with self.subTest(event=event, legacy=legacy), self.assertRaises(ValueError):
                    check_jobs("success", legacy, event)

    def test_license_preflight_requires_every_secret_and_never_prints_values(self):
        full = {name: "value-" + name for name in LICENSE_VARIABLES}
        check_license(full)
        for name in LICENSE_VARIABLES:
            for empty in ("", "  "):
                env = dict(full, **{name: empty}, GITHUB_ACTOR="dependabot[bot]", GITHUB_EVENT_NAME="pull_request")
                with self.subTest(name=name, empty=repr(empty)), self.assertRaises(ValueError) as caught:
                    check_license(env)
                message = str(caught.exception)
                self.assertIn(name, message)
                self.assertIn("Dependabot", message)
                for value in full.values():
                    self.assertNotIn(value, message)
        with self.assertRaises(ValueError):
            check_license({})

    def test_license_preflight_names_the_missing_secret_and_the_likely_cause(self):
        empty = {name: "" for name in LICENSE_VARIABLES}
        cases = (
            ("dependabot[bot]", "pull_request", "Dependabot secrets"),
            ("contributor", "pull_request", "Fork pull requests"),
            ("nanora-sama", "push", "Settings > Secrets and variables > Actions"),
            ("nanora-sama", "workflow_dispatch", "Settings > Secrets and variables > Actions"),
        )
        for actor, event, hint in cases:
            env = dict(empty, UNITY_LICENSE_SECRET="UNITY_LICENSE_2022", GITHUB_ACTOR=actor,
                       GITHUB_EVENT_NAME=event, GITHUB_REPOSITORY="owner/repo")
            with self.subTest(actor=actor, event=event), self.assertRaises(ValueError) as caught:
                check_license(env)
            message = str(caught.exception)
            self.assertIn(hint, message)
            self.assertIn("UNITY_LICENSE_2022", message)
            self.assertIn("UNITY_EMAIL", message)
        with self.assertRaises(ValueError) as caught:
            check_license(dict(empty, GITHUB_ACTOR="nanora-sama", GITHUB_EVENT_NAME="push"))
        self.assertNotIn("Dependabot", str(caught.exception))

    def test_nunit_evidence_cannot_pass_without_completed_passing_tests(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(ValueError):
                check_results(folder)
            path = Path(folder) / "editmode-results.xml"
            for xml in ('<test-run failed="1"><test-case result="Failed"/></test-run>', '<test-run failed="0"/>', '<test-run failed="0"><test-case result="Skipped"/></test-run>', '<test-run failed="0"><test-case result="Failed"/></test-run>'):
                path.write_text(xml)
                with self.subTest(xml=xml), self.assertRaises(ValueError):
                    check_results(folder)
            path.write_text('<test-run failed="0"><test-case result="Passed"/><test-case result="Skipped"/></test-run>')
            check_results(folder)

    def test_prepares_all_versions_from_fresh_checkout_and_enables_package_tests(self):
        source = Path(__file__).resolve().parents[2]
        for version in VERSIONS:
            for project in (".", "TestProject"):
                with self.subTest(version=version, project=project), tempfile.TemporaryDirectory() as folder:
                    root = Path(folder)
                    for name in ("Packages/manifest.json", "Assets/NanoraUnityMCP/package.json"):
                        dest = root / name
                        dest.parent.mkdir(parents=True, exist_ok=True)
                        dest.write_bytes((source / name).read_bytes())
                    prepare(root, project, version)
                    target = root / project
                    self.assertTrue((target / "Assets").is_dir())
                    self.assertEqual(f"m_EditorVersion: {version}\n", (target / "ProjectSettings/ProjectVersion.txt").read_text())
                    manifest = json.loads((target / "Packages/manifest.json").read_text())
                    deps = manifest["dependencies"]
                    if project == "TestProject":
                        self.assertEqual(["com.nanora.nanora-unity-mcp"], manifest["testables"])
                        package_path = (target / "Packages" / deps[manifest["testables"][0]].removeprefix("file:")).resolve()
                        self.assertEqual(root / "Assets/NanoraUnityMCP", package_path)
                        self.assertNotIn("com.unity.nuget.newtonsoft-json", deps)
                        self.assertNotIn("com.unity.render-pipelines.universal", deps)
                    if version.startswith(("2021", "2022")):
                        self.assertIn("com.unity.textmeshpro", deps)
                        self.assertNotIn("com.unity.modules.vectorgraphics", deps)


if __name__ == "__main__":
    unittest.main()
