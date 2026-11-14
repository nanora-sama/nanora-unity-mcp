---
title: Connect Claude Code to Unity
description: Use Claude Code with the Unity Editor through nanora-unity-mcp. The server writes .mcp.json for you; start claude in the project folder and check /mcp.
sidebar:
  order: 1
---

nanora-unity-mcp writes the Claude Code configuration for you.

1. Open the project in Unity and wait until *Window > Unity MCP Server* shows `Running`.
2. Start `claude` **in the project folder**. The server has already written `.mcp.json` there.
3. Run `/mcp`. `nanora-unity-mcp` should be listed as connected.

```json title=".mcp.json (written by the server)"
{
  "mcpServers": {
    "nanora-unity-mcp": { "type": "sse", "url": "http://localhost:<PORT>/sse" }
  }
}
```

`.mcp.json` holds an address for this machine, so keep it out of Git (*Add to .gitignore* in the Setup Wizard).

## Skills

When the Claude Code connection is configured, the bundled skills are copied to `<Project>/.claude/skills/` — for example `/unity-verify-applied`, which makes the AI check saved files, the running state and the visible result before it says "done". See [Bundled skills](/guides/skills/).

## If it does not appear

- Start Unity first, then restart `claude` in the folder that contains `.mcp.json`.
- `curl http://127.0.0.1:<PORT>/health` should answer with your `projectPath`.
- More in [Troubleshooting](/troubleshooting/).
