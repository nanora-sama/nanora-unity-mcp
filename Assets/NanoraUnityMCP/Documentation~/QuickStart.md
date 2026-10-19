# nanora-unity-mcp Quick Start

An MCP server that runs inside the Unity Editor, so Claude Code, Codex, Cursor and other MCP clients can operate Unity.
No Node.js, Python or separate server. About five minutes from install to the first command.

## 1. Requirements

- Unity 2021.3 LTS or later (used daily on Unity 6)
- An MCP client: Claude Code, Codex, Claude Desktop, Cursor, …
- For simulated key input: the Input System package (1.4 or later)

## 2. Install (pick one)

| Method | Steps | For |
|---|---|---|
| **A. Tarball (.tgz)** | Window > Package Manager > **+** > *Add package from tarball…* and pick `com.nanora.nanora-unity-mcp-<version>.tgz` | Downloads from BOOTH or GitHub Releases (recommended) |
| **B. Git URL** | *Add package from git URL…* `https://github.com/nanora-sama/nanora-unity-mcp.git?path=Assets/NanoraUnityMCP` | Always the latest |
| **C. .unitypackage** | First *Add package by name…* `com.unity.nuget.newtonsoft-json`, then *Assets > Import Package > Custom Package…* | Keeping it under Assets |

A and B install the dependencies (Newtonsoft JSON, Test Framework, uGUI). C cannot, so add Newtonsoft JSON first.
Keep the .tgz of method A outside the project in a place you will not delete; Unity keeps referring to it.

## 3. Check that it runs

The server starts with the Editor; the Setup Wizard (**Window > Unity MCP > Setup Wizard**) opens on first launch.
**Window > Unity MCP Server** shows `Running` and the port.

The port comes from the project path (51726–51825), so a project always gets the same one and several Editors do not collide.

```
curl http://127.0.0.1:<PORT>/health
```

If `projectPath` is your project, clients will reach the right Editor.

## 4. Connect a client

| Client | Setup |
|---|---|
| **Claude Code** | Run `claude` in the project folder. The server writes `.mcp.json`. `/mcp` should list `nanora-unity-mcp` as connected |
| **Codex** | `codex mcp add nanora-unity-mcp --url http://127.0.0.1:<PORT>/mcp` (or `url = "http://127.0.0.1:<PORT>/mcp"` in `~/.codex/config.toml`), then start a new session |
| **Claude Desktop** | Add `"nanora-unity-mcp": { "url": "http://localhost:<PORT>/sse" }` under `mcpServers` in `claude_desktop_config.json` and restart |
| **Cursor** | The same entry in the project's `.cursor/mcp.json` |

**Configure All Detected Clients** in the Setup Wizard does this for every client it finds.
`.mcp.json` holds a machine-specific address; keep it out of Git (*Add to .gitignore* in the wizard).

## 5. Recommended settings

- **Optimize for MCP (Recommended)** in the Setup Wizard: Unity keeps answering while it is in the background. AI clients usually work from another window, so turn this on.
- Optional: untick *Reload Domain* in *Project Settings > Editor > Enter Play Mode Settings* so entering Play Mode does not restart the server.

## 6. Try the sample

1. Package Manager > nanora-unity-mcp > **Samples** > *Shooting Game Sample* > **Import** (method C: copy `Samples/ShootingGame` from the download into Assets)
2. **Tools > Nanora Unity MCP > Samples > Build Shooting Game Scene**
3. Ask your AI:

```
Play the shooting game, press Space five times, check that ScoreText is greater than 0,
and show me a screenshot.
```

## 7. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Compile errors right after install (`Newtonsoft`, `JObject`, `TestTools` not found) | Installed from the .unitypackage without its dependencies. Add `com.unity.nuget.newtonsoft-json` (and `com.unity.test-framework` if missing) by name, or reinstall with A or B |
| Never shows `Running` | Start it in Window > Unity MCP Server and check the Console for `[UnityMCP]` errors. A taken port moves to the next free one; use the port shown |
| Claude Code does not list it | Start `claude` in the project folder after Unity, and check that `.mcp.json` exists there |
| Codex does not list it | The URL must end in `/mcp` with the current port; new sessions pick up the change |
| It talks to another project | Compare `/health` `projectPath`; update client configs if the port changed |
| Slow or no answers while Unity is in the background | Turn on Optimize for MCP (section 5) |
| An error mentions "domain reload" or "state unknown" | Unity was compiling or entering Play Mode and the server paused for a few seconds. Wait for `/health`, read the state, and **do not repeat a change blindly**; it may already have happened |
| `simulate_input` keys do nothing | Be in Play Mode and have the Input System package (the legacy Input Manager does not receive simulated keys). With Input System 1.4+ keys arrive even while Unity is in the background |
| Screenshots miss the UI | The game-view capture renders the camera, so *Screen Space - Overlay* canvases are not in it. Use *Screen Space - Camera* |
| `execute_csharp` refuses code | Process launch, networking and a few other APIs are blocked; the error says which |
| Too many tools for the AI | Start Unity with `NANORA_MCP_SUPERSAVE=1` to expose only categories and a dispatcher |

## 8. Reporting a problem

Open an issue: https://github.com/nanora-sama/nanora-unity-mcp/issues — include your Unity version and OS, the package version, the client and what you asked, and the output of `mcp_status` plus any `[UnityMCP]` Console errors.

## 9. Update and remove

- Update: A — pick the new .tgz the same way; B — *Update* in Package Manager; C — import the new .unitypackage over the old one.
- Remove: *Remove* in Package Manager (C: delete `Assets/NanoraUnityMCP`). Delete the generated `.mcp.json` and `.claude/skills/unity-*` if you no longer want them.

Tool reference: [ToolReference.md](ToolReference.md).
