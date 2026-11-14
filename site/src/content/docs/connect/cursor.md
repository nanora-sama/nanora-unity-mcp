---
title: Connect Cursor to Unity
description: Use Cursor with the Unity Editor through nanora-unity-mcp. Add the server to .cursor/mcp.json in the project.
sidebar:
  order: 3
---

Add the server to the project's `.cursor/mcp.json`:

```json title=".cursor/mcp.json"
{
  "mcpServers": {
    "nanora-unity-mcp": { "type": "sse", "url": "http://localhost:<PORT>/sse" }
  }
}
```

The port is shown in *Window > Unity MCP Server*. The Setup Wizard writes this file for you when it detects Cursor (*Configure All Detected Clients*).

Turn on **Optimize for MCP** in the Setup Wizard so Unity keeps answering while Cursor is in front.
