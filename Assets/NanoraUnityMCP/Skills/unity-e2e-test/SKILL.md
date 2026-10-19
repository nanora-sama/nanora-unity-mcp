---
name: unity-e2e-test
description: Run an end-to-end smoke test in Unity Play Mode — enter play mode, wait for UI, simulate input, assert state, capture screenshot, exit. Use when the user wants a "playtest", "smoke test", or "automated run".
---

# Unity E2E Smoke Test

Run a scripted playthrough and report what happened.

## Before Starting

Confirm with the user (or infer):
- Scene to test (default: currently open scene)
- Starting selector (e.g. `#StartButton`, `Canvas/MainMenu/Play`)
- Assertion target after input (e.g. `#GameplayCanvas` visible)
- Timeout per step (default: 10s)

## Steps

1. **Ensure clean state** — no lingering errors:
   - Tool: `mcp__nanora-unity-mcp__get_console_logs` with `log_type: "Error"`, `count: 10`
   - If errors exist, ask before continuing.

2. **Enter Play Mode**:
   - Tool: `mcp__nanora-unity-mcp__manage_playmode` with `action: "status"` first; if not already playing, call it with `action: "play"`.
   - Wait for the editor to settle.

3. **Wait for entry UI**:
   - Tool: `mcp__nanora-unity-mcp__wait_for_selector` with the starting selector and timeout.
   - On timeout: capture screenshot, exit play mode, fail the run.

4. **Simulate input**:
   - Tool: `mcp__nanora-unity-mcp__simulate_input` with `action: "ui_click"` and the target selector.
   - For multi-step flows, use `mcp__nanora-unity-mcp__batch_execute` to chain clicks + waits.

5. **Assert post-state**:
   - Tool: `mcp__nanora-unity-mcp__assert_ui_state` with the expected visible/hidden element list.
   - Or: `mcp__nanora-unity-mcp__query_objects` to check spawned gameplay objects.

6. **Capture screenshot**:
   - Tool: `mcp__nanora-unity-mcp__capture_screenshot` → `./Build/Reports/e2e-<timestamp>.png`

7. **Exit Play Mode**:
   - Tool: `mcp__nanora-unity-mcp__manage_playmode` with `action: "stop"` — run this even on failure, if this run started Play Mode.

8. **Collect logs** during play:
   - Tool: `mcp__nanora-unity-mcp__get_console_logs` with `count: 100`
   - Surface any Error/Exception entries.

9. **Generate report**:
   - Tool: `mcp__nanora-unity-mcp__generate_test_report` if the project supports it.
   - Otherwise inline-report: PASS/FAIL, steps completed, screenshot path, error logs.

## Failure Handling

- **Always exit play mode** in the finally-equivalent phase, even on error.
- If Unity hangs: advise the user to manually exit play mode in the Editor; do not try to `execute_csharp` a forced exit.
- A failure is: selector timeout, assertion false, or any Error-level console log during the run.
