---
title: First run with the sample game
description: Import the Shooting Game sample and ask your AI to play it, press keys, assert the score and take a screenshot through nanora-unity-mcp.
sidebar:
  order: 2
---

The package ships a small shooting game so you can watch an AI play and test a scene in a few minutes.

1. *Package Manager > nanora-unity-mcp > **Samples** > Shooting Game Sample > **Import*** (.unitypackage users: copy `Samples/ShootingGame` from the download into Assets).
2. *Tools > Nanora Unity MCP > Samples > **Build Shooting Game Scene***.
3. Ask your AI:

```text
Play the shooting game, press Space five times, check that ScoreText is greater than 0,
and show me a screenshot.
```

The AI calls [`manage_playmode`](/tools/manage-playmode/), [`simulate_input`](/tools/simulate-input/), [`assert_ui_state`](/tools/assert-ui-state/) and [`capture_screenshot`](/tools/capture-screenshot/) in turn.

Ask it to "do the same with one `run_e2e_test`" and it returns a pass/fail table per step. More on that in [Testing with an AI](/guides/testing/).
