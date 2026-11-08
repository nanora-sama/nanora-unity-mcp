---
name: unity-pre-pr-check
description: "Verify a Unity change before a PR or merge: check the intended revision, completed tests, saved and running state, and visible behavior where affected. Report READY only with fresh evidence for all applicable checks."
---

# Unity Pre-PR Check

Assess the actual change that would merge. Use the task's existing authorization for
commits, pushes, and merges; this skill does not grant new external-action permission.

## Scope and identity

Read the diff against the intended base and identify the affected behavior, project,
scene, and revision. Keep unrelated working changes out of the PR. Define expected
results before checking. An Editor running another checkout cannot validate this revision.

Documentation / skill-only changes need frontmatter, references, distribution copies,
and workflow validation. Record compile, runtime, and visual checks as not applicable
with the reason; do not require an unrelated Editor session.

## Unity code and behavior

1. **Compile the intended code.** Use `compile_project`; after any domain reload,
   reconnect to the same project and confirm compilation / importing have finished.
   Read the actual compilation result and `get_compilation_errors` as needed.
   A transport success flag is not a successful compilation. Fix relevant failures
   within scope and recheck before relying on runtime tests. Compile with
   `include_warnings: true` and compare the changed files with the base: new `CS0618` /
   `CS0672` (obsolete API) warnings are findings, e.g. `FindObjectOfType` in Unity 6 —
   use `FindAnyObjectByType` / `FindObjectsByType(FindObjectsSortMode.None)` instead.
2. **Run relevant tests.** Use `run_tests` with `test_mode: "EditMode"` for Editor code
   and the needed PlayMode / behavior tests. Wait for completion and inspect actual
   results; "Tests started", zero tests when tests were expected, stale results,
   skipped required tests, or timeouts cannot pass. Use `get_test_results` only when
   its result can be tied to this run.
3. **Check application of the change.** Use the bundled
   [unity-verify-applied](../unity-verify-applied/SKILL.md) workflow: saved / installed
   content, loaded instance, visible result, behavior after input, and persistence
   where required. Read back the resulting state independently of the mutation.
4. **Inspect affected visuals.** Capture the actual scene / Game View and open the
   image. For runtime UI Toolkit, use a full `ScreenCapture.CaptureScreenshot`
   capture and wait for the new file, rather than a camera-only screenshot.
   Skip this only for changes with no visual effect and record why.
5. **Inspect errors and restore test state.** Check relevant Error / Exception logs,
   filtered before limiting count so ordinary logs cannot hide errors. Separate
   pre-existing errors from new ones. Confirm cleanup and requested saved state.
   Restore only state owned by the test; do not stop a Play Mode session the user started.

## Verdict and PR / merge checks

Record the run time, target and revision, expected / observed results, completed test
counts, and evidence paths. Recheck affected stages after the final edit or any target
change. Report **READY** only when all applicable checks passed; missing evidence is
**NOT READY**, with the exact gap. Mention any limits such as untested physical inputs.

For an authorized PR, describe the problem, resulting behavior, and evidence for this
revision. For an authorized merge, verify the PR head still matches the checked revision,
required checks are successful, and no unresolved blocking review remains. After merging,
read the hosting service's merged state and merge commit. A successful command or queued
auto-merge request alone does not prove that the PR merged.
