---
title: Several Unity projects at once
description: nanora-unity-mcp derives its port from the project path (51726–51825), writes the client config and reports the project in /health, so several Unity Editors never get mixed up.
sidebar:
  order: 4
---

You can open several Unity projects at the same time, each with its own server.

- **The port comes from the project path**, in the range 51726–51825. The same project always gets the same port, so client configs stay valid across restarts. If the port is taken, the server moves to the next free one; use the port shown in *Window > Unity MCP Server*.
- **The client config is written for you** — `.mcp.json` for Claude Code, and Cursor or Claude Desktop through the Setup Wizard.
- **`/health` names the project.** Ask it before you trust a connection:

```sh
curl http://127.0.0.1:<PORT>/health
```

The answer includes the project name and path, process ID, package version, protocol version and transports. If `projectPath` is not the project you meant, update the client's URL.

The [`unity://project/info`](/reference/endpoints/#resources) resource gives the AI itself the same answer.
