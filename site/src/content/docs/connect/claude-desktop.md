---
title: Connect Claude Desktop to Unity
description: Use Claude Desktop with the Unity Editor through nanora-unity-mcp by adding the SSE URL to claude_desktop_config.json.
sidebar:
  order: 4
---

Add the server under `mcpServers` in `claude_desktop_config.json` and restart Claude Desktop:

```json title="claude_desktop_config.json"
{
  "mcpServers": {
    "nanora-unity-mcp": { "type": "sse", "url": "http://localhost:<PORT>/sse" }
  }
}
```

The port is shown in *Window > Unity MCP Server*. The Setup Wizard can write this for you (*Configure All Detected Clients*).
