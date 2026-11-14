---
title: Claude Desktop を Unity につなぐ
description: nanora-unity-mcp で Claude Desktop から Unity Editor を操作する。claude_desktop_config.json に SSE の URL を足す。
sidebar:
  order: 4
---

`claude_desktop_config.json` の `mcpServers` にサーバーを足し、Claude Desktop を再起動します:

```json title="claude_desktop_config.json"
{
  "mcpServers": {
    "nanora-unity-mcp": { "type": "sse", "url": "http://localhost:<PORT>/sse" }
  }
}
```

ポート番号は *Window > Unity MCP Server* に出ます。Setup Wizard でも書けます（*Configure All Detected Clients*）。
