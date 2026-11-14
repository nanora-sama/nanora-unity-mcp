---
title: よくある質問
description: nanora-unity-mcp のよくある質問。料金とライセンス、対応する Unity の版と AI クライアント、ほかに入れるもの、ほかの Unity MCP との違い。
---

## 無料ですか？

はい。nanora-unity-mcp は [MIT ライセンス](https://github.com/nanora-sama/nanora-unity-mcp/blob/main/LICENSE)のオープンソースです。BOOTH でも無料で配っています。BOOTH の有料の「応援版」も中身は同じです。

## 対応する Unity の版は？

Unity 2021.3 LTS 以降。Unity 6 にも対応しています。

## どの AI クライアントで使えますか？

Streamable HTTP か SSE に対応した MCP クライアントなら使えます。動作を確かめているのは Claude Code・Codex・Cursor・Claude Desktop です。[そのほかの MCP クライアント](/nanora-unity-mcp/ja/connect/other-clients/)を参照。

## Node.js や Python は要りますか？

要りません。サーバーは Unity Editor の中で動く C# です。Node.js を使うのは、任意の[トランスポートの確認](/nanora-unity-mcp/ja/connect/other-clients/#トランスポートを確かめる)のときだけです。

## プロジェクトがどこかに送られますか？

サーバーは 127.0.0.1 だけで待ち受けます。AI クライアントが自分の提供元に何を送るかは、そのクライアントしだいです。[セキュリティ](/nanora-unity-mcp/ja/guides/security/)を参照。

## ツールはいくつありますか？

18 分類・57 ツールと、[SuperSave モード](/nanora-unity-mcp/ja/guides/supersave/)用のメタツール 3 本です。全部[ツールリファレンス](/nanora-unity-mcp/ja/tools/)にあります。

## AI は作ったものを自分でテストできますか？

できます。そのためのテスト用ツールがあります。[AI にゲームをテストさせる](/nanora-unity-mcp/ja/guides/testing/)を参照。

## ビルドしたゲームの中でも動きますか？

いいえ。Editor 用のツールで、サーバーは Unity Editor の中で動きます。

## ほかの Unity MCP と何が違いますか？

いちばんの違いは、Unity のほかに何が要るかです。[比較](/nanora-unity-mcp/ja/comparison/)を参照。

## 質問はどこに？

[GitHub の Issue](https://github.com/nanora-sama/nanora-unity-mcp/issues/new/choose) へ。日本語で大丈夫です。個人で開発しているので、対応はできる範囲になります。
