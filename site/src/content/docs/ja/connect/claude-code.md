---
title: Claude Code を Unity につなぐ
description: nanora-unity-mcp で Claude Code から Unity Editor を操作する。.mcp.json はサーバーが自動で作るので、プロジェクトのフォルダで claude を起動して /mcp で確かめるだけ。
sidebar:
  order: 1
---

Claude Code の設定は nanora-unity-mcp が書きます。

1. Unity でプロジェクトを開き、*Window > Unity MCP Server* が `Running` になるのを待つ
2. **プロジェクトのフォルダで** `claude` を起動する。`.mcp.json` はもうサーバーが作っています
3. `/mcp` で `nanora-unity-mcp` が connected になっていれば OK

```json title=".mcp.json（サーバーが書く）"
{
  "mcpServers": {
    "nanora-unity-mcp": { "type": "sse", "url": "http://localhost:<PORT>/sse" }
  }
}
```

`.mcp.json` にはこの PC 用のアドレスが入るので、Git には入れないでください（Setup Wizard の *Add to .gitignore*）。

## Skill

Claude Code の接続を設定すると、同梱の Skill が `<Project>/.claude/skills/` に入ります。たとえば `/unity-verify-applied` は、「できた」と言う前に保存されたファイル・動いている状態・見た目の結果を AI に確かめさせます。[同梱の Skill](/ja/guides/skills/) を参照。

## 出てこないとき

- Unity を先に起動し、`.mcp.json` のあるフォルダで `claude` を起動し直す
- `curl http://127.0.0.1:<PORT>/health` が今のプロジェクトの `projectPath` を返すか
- ほかは[困ったとき](/ja/troubleshooting/)
