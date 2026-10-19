# Unity CI

GitHub Actions runs Unity EditMode tests and the CI preparation and failure-gate tests.

| Workflow | Scope | Versions |
|---|---|---|
| `unity-tests.yml` | Repository project | 6000.3.4f1 on every PR; 2021.3.45f2 and 2022.3.62f3 also on push/manual runs |
| `clean-install-test.yml` | Base package installed through UPM into a generated empty project | Same version policy |
| `ci-configuration.yml` | Project preparation and failure-gate regression tests | Python, no Unity license |

`python3 scripts/ci/unity_ci.py prepare TestProject <version>` creates the clean fixture before caching or launching Unity. Its manifest references `file:../../Assets/NanoraUnityMCP`, lists the package in `testables`, and supplies Unity's built-in modules, input/UI packages and test framework. Newtonsoft must resolve from the package's dependency declaration. No existing Library or developer ProjectSettings are required. Root-project preparation retains its dependencies and applies the legacy compatibility versions.

The GameCI action is pinned to `70a43fd07570eba926a875a9708c5be06adb092d`, the Node 24 revision immediately before the CLI migration. That implementation converts the existing ULF's DeveloperData to a serial and activates on the runner. The later v4.4 CLI path tried to import the machine-bound ULF directly and failed with `Machine bindings don't match` in this repository. Upgrade the pin only after running both workflows manually against all three Unity versions. See [GameCI's activation documentation](https://game.ci/docs/github/activation/) and the [pinned activation input implementation](https://github.com/game-ci/unity-test-runner/blob/70a43fd07570eba926a875a9708c5be06adb092d/src/model/input.ts).

## Secrets

| Secret | Contents |
|---|---|
| `UNITY_LICENSE_6000` | Unity Hub-generated ULF for the Unity account used by CI |
| `UNITY_LICENSE_2021` | ULF used by the 2021 job |
| `UNITY_LICENSE_2022` | ULF used by the 2022 job |
| `UNITY_EMAIL` | Unity account email |
| `UNITY_PASSWORD` | Unity account password |

Store the original ULF XML in GitHub Secrets; never print credentials or license contents in logs. The older `UNITY_LICENSE` secret is not read by these matrix jobs. The existing `activation.yml` is a legacy manual-activation-file helper; Personal CI setup follows Unity Hub activation as documented by GameCI, not manual ALF upload.

## Required evidence

Each Unity job must produce NUnit XML containing passing tests and no failures. Missing artifacts, zero executed tests and fully skipped runs fail verification. Summaries fail if the latest job fails, is cancelled or is skipped, or if an expected legacy job fails. The legacy skip is allowed only on pull requests. Artifacts are retained for 14 days.

Before merging a CI change, run:

```sh
python3 -m unittest discover -s scripts/ci -v
# From the PR branch, exercise the full version matrix:
gh workflow run unity-tests.yml --ref <branch>
gh workflow run clean-install-test.yml --ref <branch>
```

Read job logs and NUnit artifacts as well as the workflow conclusion. A successful local test or Summary alone does not establish that the hosted Unity tests ran. Fork PRs without access to the Unity secrets cannot complete license activation; they must not be reported as tested.

Unity jobs share the `unity-ci-license` concurrency group across both workflows. `queue: max` retains pending matrix jobs and prevents overlapping activation/return for the same account. GitHub supports this property; actionlint 1.7.12 predates it, so its specific unknown-`queue` diagnostic is excluded during local linting. GitHub's workflow execution remains the acceptance check for this property. [GitHub concurrency documentation](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency).

The restored clean-install test also caught a real UPM install bug: bundled skills were searched under the old, incorrect package name. `SkillInstaller` now uses `PackageInfo.resolvedPath`, and tests copy the actual bundled files and compare their contents after installation.

The full repository run exposed testing-tool tests (formerly Test Pro) blocking the editor thread with `.Result`. These tests now use `await` without weakening their assertions. The previously hidden failures also required validating empty batch commands before registry lookup and moving Prefab selector resolution inside its existing MCP error boundary.
