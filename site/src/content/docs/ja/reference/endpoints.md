---
title: エンドポイントとリソース
description: nanora-unity-mcp の HTTP エンドポイント（Streamable HTTP の /mcp・SSE の /sse・JSON-RPC の /message・/health）と、MCP リソース unity://project/info・unity://editor/state。
sidebar:
  order: 2
---

どれも `http://127.0.0.1:<PORT>` で受けます。ポート番号は *Window > Unity MCP Server* に出ます。

| パス | 用途 |
|---|---|
| `POST /mcp` | Streamable HTTP。状態を持たず JSON で応答。通知には `202`。Codex など Streamable HTTP のクライアント用 |
| `GET /sse` + `POST /message?sessionId=…` | 従来の SSE。接続ごとに別のセッション |
| `POST /message` | JSON-RPC の要求を送ると、そのまま応答が返る。スクリプト用 |
| `GET /health` | プロジェクト名とパス・プロセス ID・パッケージの版・プロトコルの版・トランスポート。コンパイル中も応答 |

MCP のプロトコルは **2024-11-05〜2025-11-25** の版で合わせます。

## リソース

AI がいつでも読める、読み取り専用の MCP リソース:

| URI | 中身 |
|---|---|
| `unity://project/info` | このサーバーがどのプロジェクトのものか |
| `unity://editor/state` | コンパイルの失敗・Play／一時停止・読み込み中と未保存のシーン |

[ドメインリロード](/ja/guides/domain-reload/)のあと、変更をやり直す前に `unity://editor/state` を読んでください。

## スクリプトからツールを呼ぶ

```sh
curl -s http://127.0.0.1:<PORT>/message \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"mcp_status","arguments":{}}}'
```
