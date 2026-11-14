---
title: Let the AI test your game
description: Have Claude Code, Codex or Cursor play a Unity scene and verify it — wait for objects, press keys, assert UI text and take screenshots, or run a whole E2E scenario in one call.
sidebar:
  order: 1
---

An AI that edits a scene should also check the result. nanora-unity-mcp gives it Playwright-style tools for that, all driven by [U-Selector](/nanora-unity-mcp/reference/u-selector/) queries such as `#ScoreText` or `.Enemy[tag=Boss]`.

## Step by step

| Step | Tool |
|---|---|
| Enter or leave Play Mode | [`manage_playmode`](/nanora-unity-mcp/tools/manage-playmode/) |
| Wait until an object exists, is visible or interactable | [`wait_for_selector`](/nanora-unity-mcp/tools/wait-for-selector/) |
| Press keys, click, drag, click a UI element | [`simulate_input`](/nanora-unity-mcp/tools/simulate-input/) |
| Check text, active state, color, value… | [`assert_ui_state`](/nanora-unity-mcp/tools/assert-ui-state/) |
| See the result | [`capture_screenshot`](/nanora-unity-mcp/tools/capture-screenshot/) |

Simulated keys need the **Input System** package; the legacy Input Manager does not receive them. With Input System 1.4 or later keys arrive even while Unity is in the background.

## One scenario, one call

[`run_e2e_test`](/nanora-unity-mcp/tools/run-e2e-test/) runs a list of steps in Play Mode and returns pass/fail per step. Step actions are `wait_for_selector`, `simulate_input`, `assert`, `screenshot`, `wait` and `execute_csharp`. It enters and leaves Play Mode for you and takes a screenshot when a step fails.

```json
{
  "name": "run_e2e_test",
  "arguments": {
    "name": "start and score",
    "steps": [
      { "action": "wait_for_selector", "selector": "#StartButton" },
      { "action": "simulate_input", "input_action": "ui_click", "selector": "#StartButton" },
      { "action": "simulate_input", "input_action": "key_tap", "key": "Space" },
      { "action": "wait", "ms": 500 },
      { "action": "assert", "selector": "#ScoreText", "property": "text", "comparison": "not_equals", "expected": "0" },
      { "action": "screenshot", "label": "after-shot" }
    ]
  }
}
```

The object names here are an example; use the ones in your scene.

## Beyond a single run

- [`visual_regression_test`](/nanora-unity-mcp/tools/visual-regression-test/) compares a screenshot with a saved baseline.
- [`performance_budget_test`](/nanora-unity-mcp/tools/performance-budget-test/) checks FPS, draw calls and memory against a budget.
- [`run_tests`](/nanora-unity-mcp/tools/run-tests/) and [`get_test_results`](/nanora-unity-mcp/tools/get-test-results/) run your Unity Test Framework tests.
- [`generate_test_report`](/nanora-unity-mcp/tools/generate-test-report/) writes the results up as a report.

## Screenshots without the UI?

The game-view capture renders the camera, so *Screen Space - Overlay* canvases are not in it. Use *Screen Space - Camera* for UI you want the AI to see.
