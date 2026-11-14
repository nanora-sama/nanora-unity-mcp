---
title: Unity MCP servers compared
description: What nanora-unity-mcp, CoplayDev unity-mcp, CoderGamester mcp-unity, IvanMurzak Unity-MCP and Unity's own MCP need besides Unity, checked against each project's documentation.
---

There are several good ways to connect an AI to Unity. The biggest practical difference is **what you have to install and run besides Unity**. Checked against each project's own documentation on 2026-10-19; things change, so check the projects themselves before deciding.

| | Needs besides Unity | Unity versions |
|---|---|---|
| **nanora-unity-mcp** (MIT) | Nothing — C# inside the Editor process; Roslyn is bundled | 2021.3 – 6 |
| [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (MIT) | Python 3.10+ (uv) | 2021.3 LTS – 6.x |
| [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) (MIT) | Node.js 18+; a WebSocket on the Unity side | 6 or later |
| [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) (Apache-2.0) | A separate server executable | — |
| Unity's own MCP (AI Assistant) | A relay program, a project linked to Unity Cloud, and an AI trial or subscription | 6 or later |

## Where nanora-unity-mcp fits

- **Only Unity.** No Python, Node.js, extra executable or relay.
- **Streamable HTTP and SSE on one server**, so Codex and Claude Code or Cursor can use the same Editor.
- **A fixed port per project, a written client config and a `/health` that names the project**, so several Editors do not get mixed up without any setup.
- **Checking, not only editing.** U-Selector with `wait_for_selector`, `assert_ui_state` and `simulate_input`, and [skills](/guides/skills/) that make the AI verify its work.

## Where others are ahead

- Other servers have more tools (IvanMurzak's has 70+ with extensions) and larger communities.
- Remote use (OAuth, cloud relays) is not a goal here: nanora-unity-mcp is local only.
- Running C# and handling several Editors are available elsewhere too.
