---
title: そのほかの MCP クライアントをつなぐ
description: MCP に対応したクライアントなら nanora-unity-mcp を使えます。http://127.0.0.1:<PORT>/mcp（Streamable HTTP）か /sse（SSE）を登録。プロトコルは 2024-11-05〜2025-11-25。
sidebar:
  order: 5
---

nanora-unity-mcp は同じポートで 2 つのトランスポートを受けます。クライアントが対応している方を登録してください。

| トランスポート | URL |
|---|---|
| Streamable HTTP | `http://127.0.0.1:<PORT>/mcp` |
| SSE | `http://127.0.0.1:<PORT>/sse` |

MCP のプロトコルは **2024-11-05〜2025-11-25** の版で合わせます。ポート（51726〜51825）はプロジェクトのパスから決まり、*Window > Unity MCP Server* に出ます。[Unity を複数開く](/nanora-unity-mcp/ja/guides/multiple-projects/)を参照。

スクリプトからは、`POST /message` に JSON-RPC の要求を送るとそのまま応答が返ります。エンドポイントの一覧は[エンドポイント](/nanora-unity-mcp/ja/reference/endpoints/)に。

## トランスポートを確かめる

```sh
node scripts/mcp/verify-transport.mjs
```

ハンドシェイク・通知・エラー・Origin の確認・SSE の分離・ツール呼び出しを確かめます（Node.js 20 以降。この確認のときだけ必要です）。
