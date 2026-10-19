# nanora-unity-mcp

**An MCP server that lives inside the Unity Editor.** Let Claude Code, Codex, Cursor or any MCP client build, play, test and profile your Unity project — with nothing else to install.

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)](https://unity.com/)
[![Unity 6](https://img.shields.io/badge/Unity%206-Supported-black?logo=unity)](https://unity.com/releases/unity-6)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

日本語のクイックスタート: [Documentation~/QuickStart_JP.md](Documentation~/QuickStart_JP.md)

## Why this one

- **Unity is all you need.** The server is C# running in the Editor process. No Node.js, no Python, no separate server binary, no account or subscription.
- **Codex and Claude on the same server.** Streamable HTTP at `/mcp` for Codex and other HTTP clients; SSE at `/sse` for Claude Code, Claude Desktop and Cursor.
- **Checks its own work.** Playwright-style selectors, `wait_for_selector`, `assert_ui_state` and `simulate_input` let the AI play the game and verify the result, and the bundled skills make "did it actually apply?" part of the workflow.
- **Many projects, no mix-ups.** Each project gets a fixed port from its path, `.mcp.json` is written for Claude Code, and `/health` names the project so a client can confirm it reached the right Editor.
- **Local only.** Binds to 127.0.0.1, rejects foreign browser origins, keeps every reply with the client that asked.

## Install

**Package Manager (Git URL)** — Window > Package Manager > **+** > *Add package from git URL…*

```
https://github.com/nanora-sama/nanora-unity-mcp.git?path=Assets/NanoraUnityMCP
```

**Tarball** — download `com.nanora.nanora-unity-mcp-<version>.tgz` from [Releases](https://github.com/nanora-sama/nanora-unity-mcp/releases) or BOOTH, then *Package Manager > **+** > Add package from tarball…* Dependencies are installed for you.

**.unitypackage** — install `com.unity.nuget.newtonsoft-json` first (*Add package by name…*), then *Assets > Import Package > Custom Package…* A .unitypackage cannot install dependencies or carry the sample.

Requires Unity 2021.3 LTS or later. The server starts with the Editor; the Setup Wizard (**Window > Unity MCP > Setup Wizard**) opens on first launch.

## Connect a client

The port is chosen from the project path (51726–51825) and shown in **Window > Unity MCP Server**.

| Client | How |
|---|---|
| Claude Code | Start `claude` in the project folder. `.mcp.json` is created automatically. |
| Codex | `codex mcp add nanora-unity-mcp --url http://127.0.0.1:<PORT>/mcp` |
| Claude Desktop | `"nanora-unity-mcp": { "url": "http://localhost:<PORT>/sse" }` in `claude_desktop_config.json` |
| Cursor | the same entry in `.cursor/mcp.json` |

The Setup Wizard can configure detected clients for you. Enable **Background Execution** in the wizard (or call `configure_editor` with `optimize_for_mcp`) so Unity answers while it is not focused.

## Try it

Import **Samples > Shooting Game Sample** from the package page, then ask your AI:

```
Build the shooting game sample scene, play it, tap Space five times,
check that ScoreText is greater than 0, and show me a screenshot.
```

It will call `execute_menu_item`, `manage_playmode`, `simulate_input`, `assert_ui_state` and `capture_screenshot`. See the [sample README](Samples~/ShootingGame/README.md).

## Tools

57 tools in 18 categories (plus `list_categories` / `list_tools` / `execute` for SuperSave mode, below).

| Area | Tools |
|---|---|
| Scene | `get_scene_hierarchy`, `get_object_details`, `create_game_object`, `modify_game_object`, `delete_game_object`, `manage_scene` |
| Query | `query_objects` (U-Selector), `search_assets`, `wait_for_selector` |
| Batch | `batch_modify`, `batch_execute`, `destroy_objects`, `set_active`, `instantiate_asset` |
| Code | `execute_csharp` (Roslyn), `execute_menu_item`, `compile_project`, `get_compilation_errors`, `unity_reflect` |
| Play & test | `manage_playmode`, `simulate_input`, `assert_ui_state`, `run_tests`, `get_test_results`, `run_e2e_test`, `visual_regression_test`, `performance_budget_test`, `generate_test_report` |
| Performance | `analyze_frame`, `analyze_memory`, `profile_cpu`, `get_profiler_stats` |
| Diagnostics | `capture_screenshot`, `get_console_logs`, `mcp_status`, `diagnose_materials`, `repair_materials`, `configure_editor`, `setup_status`, `reconfigure_client`, `install_skills` |
| Assets & packages | `manage_assets`, `manage_prefabs`, `manage_packages`, `install_package`, `uninstall_package` |
| Scene building | lights, cameras, Cinemachine, UI canvases and elements, animation, audio |

Full reference: [Documentation~/ToolReference.md](Documentation~/ToolReference.md).

**SuperSave mode**: set `NANORA_MCP_SUPERSAVE=1` to expose only the three meta tools. The AI lists categories, then the tools of one category, then calls through `execute`, which keeps the tool list out of its context.

### U-Selector

```
#Player                 by name
.Camera                 by component type
/Canvas/Menu/PlayButton by hierarchy path
.Enemy[tag=Boss]        with an attribute filter
ui:#StartButton         UI Toolkit element by name
```

## Claude Code skills

Installed into `<Project>/.claude/skills/` when Claude Code is configured; your edits are kept on update (`install_skills` with `force: true` overwrites).

| Skill | Does |
|---|---|
| `/unity-verify-applied` | Confirms saved files, running state and the visible result before a change is called done |
| `/unity-pre-pr-check` | Revision, tests, saved state and visible behavior before a PR |
| `/unity-e2e-test` | Play Mode smoke test: wait, input, assert, screenshot |
| `/unity-material-repair` | Finds and fixes pink materials with before/after screenshots |
| `/unity-performance-budget` | FPS, draw calls and memory against a budget |
| `/unity-scene-setup` | A new scene with lighting, camera and ground |

## Endpoints

| Path | Use |
|---|---|
| `POST /mcp` | Streamable HTTP (stateless, JSON replies). Notifications get `202`. |
| `GET /sse` + `POST /message?sessionId=…` | Legacy SSE; each connection gets its own session. |
| `POST /message` | Direct JSON-RPC request/reply for scripts. |
| `GET /health` | Project name and path, process id, version, protocol version, transports. Answers during compiles. |
| `unity://project/info`, `unity://editor/state` | Read-only resources: identity, compile failure, play/pause, loaded and unsaved scenes. |

Protocol versions 2024-11-05 through 2025-11-25 are negotiated.

### Domain reloads

Compiling and entering Play Mode reload scripts and restart the server for a few seconds. A request cut off by the reload gets a JSON-RPC error saying so. The tool may already have done its work: wait until `/health` answers, read the state (`mcp_status`, `unity://editor/state`, `manage_playmode` status), and only then decide whether to repeat a change. Never replay a mutation blindly. Turning off *Reload Domain* in **Project Settings > Editor > Enter Play Mode Settings** avoids the reload on Play.

## Verify an installation

From a clone of the repository, with Node.js 20+ (only for these checks):

```
node scripts/mcp/verify-transport.mjs   # handshake, notifications, errors, Origin, SSE isolation, a tool call
node scripts/mcp/verify-codex.mjs       # an installed Codex client lists and calls the tools
```

## Security

`execute_csharp` and `execute_menu_item` run code in your Editor with your permissions. The server listens on loopback only; keep it that way, and review what your AI client is allowed to call. `execute_csharp` blocks some APIs (process launch, web clients) and stops runaway loops, but it is not a sandbox.

## Help

- Quick start and troubleshooting: [日本語](Documentation~/QuickStart_JP.md) / [English](Documentation~/QuickStart.md)
- Questions, bugs, ideas: https://github.com/nanora-sama/nanora-unity-mcp/issues/new/choose

## License

[MIT](LICENSE). Bundled Roslyn and System.* assemblies: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Issues and pull requests: [GitHub](https://github.com/nanora-sama/nanora-unity-mcp/issues).
