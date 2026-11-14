---
title: セキュリティ
description: nanora-unity-mcp は 127.0.0.1 だけで待ち受け、外部のブラウザーからの要求を断り、応答は要求したクライアントにだけ返します。execute_csharp はあなたの権限でコードを実行します。
sidebar:
  order: 6
---

- **ローカルだけ。** サーバーはループバック（127.0.0.1）だけで待ち受けます。この設定は変えないでください。
- **ほかの Web ページからの要求は断ります。** よその `Origin` からの要求は受け付けません。
- **応答は要求したクライアントにだけ。** SSE は接続ごとに別のセッションです。

## コードの実行

[`execute_csharp`](/nanora-unity-mcp/ja/tools/execute-csharp/) と [`execute_menu_item`](/nanora-unity-mcp/ja/tools/execute-menu-item/) は、あなたの権限で Editor の中のコードを実行します。`execute_csharp` は一部の API（プロセスの起動・Web クライアント）を止め、終わらないループを打ち切りますが、**サンドボックスではありません**。AI クライアントに何を呼ばせるかを確かめてください。

脆弱性は [SECURITY.md](https://github.com/nanora-sama/nanora-unity-mcp/blob/main/SECURITY.md) の方法で、非公開で知らせてください。
