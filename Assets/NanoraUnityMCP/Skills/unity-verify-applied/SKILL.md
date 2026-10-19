---
name: unity-verify-applied
description: "Verify that a Unity or Unity-connected application change reached the intended saved files, running instance, and user-visible result. Use after applying scene, asset, UI, package, or integration changes and before reporting them applied."
---

# Verify Applied Changes

Prove the requested effect in the application the user actually uses. A successful edit,
tool response, HTTP request, or restart command is evidence of that step only.

## Define the target and expected result

- Identify the project path, scene or asset, application instance, and intended version.
  Read the endpoint from the project's configuration; use `mcp_status`, project resources,
  or `/health` where available to verify identity before inspecting or changing Unity.
- State the expected observable effect before testing. Record relevant file hashes or
  revision, settings, process identity, and the start time of this verification run.
- Select checks appropriate to the request. Documentation-only edits need document and
  reference validation; they do not require a running Editor or unrelated render tests.
- Keep concurrent work separate. Do not deploy unrelated newer files to make a check pass.

## Verify each applicable stage

| Stage | Required observation |
| --- | --- |
| Saved / installed | Read back the intended scene or asset; compare deployed files to an independent distribution source. A source edit alone is insufficient. |
| Loaded | Wait for import / compilation / reload to finish. Confirm the intended running instance loaded the change using fresh application logs or runtime state. A restart CLI exit code is insufficient. |
| Visible | Inspect the actual UI or open a fresh Game View image. Check the requested elements, not only a manifest or Inspector values. |
| Behaving | Perform the requested operation and independently read back the resulting state or output. Receipt / queue acceptance is not completion. |
| Persistent | When persistence is requested, verify saved state after the relevant reload or Play Mode transition. Preserve unrelated user edits. |

For Unity, use live tool schemas rather than guessing parameter names. After compilation,
reconnect to the same project and confirm compilation / updating have ended. After an
ambiguous timeout, inspect the state before retrying a mutation; repeating a toggle or
"next" command may undo the intended effect.

For Play Mode checks, read `manage_playmode` with `action: "status"` first. Use
`action: "play"` only if needed, and confirm the resulting state. In cleanup, restore
test-owned state; stop Play Mode only if this run started it, then confirm it stopped.

For visual changes, read the renderer / effective material or Volume state as well as
the image. A setting on an inactive object, unused quality profile, or overwritten shader
property does not prove a rendered effect. For runtime UI Toolkit panels, use a full
`ScreenCapture.CaptureScreenshot` capture rather than a camera-only capture, wait for the
new file to exist, and open it. Capture creation alone is not visual inspection.

For external integrations, verify the destination's installed content and actual UI.
An API test does not prove the physical button was pressed. Use the project's deployment
verification script when available; a manifest listing expected actions is not UI evidence.
Do not compare an installation to itself or derive expected files from that installation.

## Evidence and verdict

Keep a per-run record: timestamp, target identity, intended revision / hashes, expected
result, observed result, checks performed, and paths to logs / images / reports. Use only
evidence from after the final relevant change; invalidate affected checks if files, settings,
or the process change during verification.

- **APPLIED**: all applicable checks passed, with evidence of the requested effect.
- **NOT APPLIED**: an observed mismatch; correct it within the authorized scope and recheck.
- **UNVERIFIED**: required evidence is missing, stale, inaccessible, still running, or timed out.
  Continue independent work and report the exact gap; never convert it to success.

State the verified scope explicitly. UI registration, simulated input, HTTP behavior,
physical input, saved persistence, and merge status are distinct claims. This skill does
not authorize installing unrelated updates, overwriting user work, or publishing changes.
