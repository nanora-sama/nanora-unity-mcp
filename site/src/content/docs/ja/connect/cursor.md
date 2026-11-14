---
title: Cursor を Unity につなぐ
description: nanora-unity-mcp で Cursor から Unity Editor を操作する。プロジェクトの .cursor/mcp.json にサーバーを登録する。
sidebar:
  order: 3
---

プロジェクトの `.cursor/mcp.json` にサーバーを足します:

```json title=".cursor/mcp.json"
{
  "mcpServers": {
    "nanora-unity-mcp": { "type": "sse", "url": "http://localhost:<PORT>/sse" }
  }
}
```

ポート番号は *Window > Unity MCP Server* に出ます。Setup Wizard が Cursor を見つけると、このファイルを書きます（*Configure All Detected Clients*）。

Cursor が前面にあっても Unity が応答するよう、Setup Wizard で **Optimize for MCP** を有効にしてください。
