---
title: Troubleshooting
description: Fixes for common nanora-unity-mcp problems — compile errors after install, the server not running, Claude Code or Codex not listing it, the wrong project, slow answers, domain reload errors, keys and screenshots.
---

## Compile errors right after installing

`Newtonsoft`, `JObject` or `TestTools` not found: the package was installed from the .unitypackage without its dependencies. Add `com.unity.nuget.newtonsoft-json` (and `com.unity.test-framework` if missing) with *Add package by name…*, or reinstall from the Git URL or the tarball, which bring them along.

## The server never shows `Running`

Start it in *Window > Unity MCP Server* and look for `[UnityMCP]` errors in the Console. If the port is taken, the server moves to the next free one — use the port the window shows.

## Claude Code does not list the server

Start Unity first, then start `claude` again **in the project folder**, and check that `.mcp.json` is there. See [Connect Claude Code](/nanora-unity-mcp/connect/claude-code/).

## Codex does not list the server

The URL must end in `/mcp` and use the current port. Codex reads the setting when a session starts, so start a new one. See [Connect Codex](/nanora-unity-mcp/connect/codex/).

## The AI is working on another project

Compare `projectPath` from `curl http://127.0.0.1:<PORT>/health` with the project you meant. If the port changed, update the client config. See [Several projects at once](/nanora-unity-mcp/guides/multiple-projects/).

## Slow or no answers while Unity is in the background

Turn on **Optimize for MCP** in the Setup Wizard (Background Execution).

## An error mentions "domain reload" or "state unknown"

Unity was compiling or entering Play Mode and the server paused for a few seconds. Wait for `/health`, read the state, and **do not repeat a change blindly** — it may already have happened. See [Domain reload](/nanora-unity-mcp/guides/domain-reload/).

## `simulate_input` keys do nothing

Be in Play Mode and have the **Input System** package; the legacy Input Manager does not receive simulated keys. With Input System 1.4 or later keys arrive even while Unity is in the background.

## Screenshots do not show the UI

The game-view capture renders the camera, so *Screen Space - Overlay* canvases are not in it. Use *Screen Space - Camera*.

## `execute_csharp` refuses the code

Starting processes, networking and a few other APIs are blocked; the error says which. See [Security](/nanora-unity-mcp/guides/security/).

## The AI gets lost among too many tools

Start Unity with `NANORA_MCP_SUPERSAVE=1`. See [SuperSave mode](/nanora-unity-mcp/guides/supersave/).

## Still stuck?

Open an [issue](https://github.com/nanora-sama/nanora-unity-mcp/issues/new/choose) (Japanese is fine) with:

1. Unity version and OS
2. Package version (Package Manager, or `version` in `/health`)
3. The AI client and what you asked
4. The output of [`mcp_status`](/nanora-unity-mcp/tools/mcp-status/) and any `[UnityMCP]` errors in the Console
