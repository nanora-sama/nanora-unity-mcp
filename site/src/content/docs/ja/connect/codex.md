---
title: Codex を Unity につなぐ
description: nanora-unity-mcp で OpenAI の Codex から Unity Editor を操作する。Streamable HTTP の http://127.0.0.1:<PORT>/mcp を codex mcp add で登録する。
sidebar:
  order: 2
---

Codex は **Streamable HTTP** でつなぎます。nanora-unity-mcp は SSE と同じポートの `/mcp` で受けます。

```sh
codex mcp add nanora-unity-mcp --url http://127.0.0.1:<PORT>/mcp
```

または `~/.codex/config.toml` に:

```toml
[mcp_servers.nanora-unity-mcp]
url = "http://127.0.0.1:<PORT>/mcp"
```

設定のあとは新しいセッションを始めてください（設定はセッションの開始時に読まれます）。*Window > Unity MCP Server* にポート番号と **Copy Codex setup command** ボタンがあり、今の番号で上のコマンドをコピーできます。

## 接続を確かめる

Node.js 20 以降で（この確認のときだけ必要です）:

```sh
node scripts/mcp/verify-codex.mjs
```

入っている Codex クライアントが、ツールを一覧して呼べるかを確かめます。

## 出てこないとき

- URL が `/mcp` で終わっているか、番号が今の番号か
- [`/mcp` エンドポイント](/ja/reference/endpoints/)は状態を持たず、JSON で応答します。通知には `202` を返します
