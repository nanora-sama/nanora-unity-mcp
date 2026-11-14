---
title: ドメインリロードと「state unknown」のエラー
description: Unity はコンパイルのあとと Play モードに入るときにスクリプトを読み直し、MCP サーバーは数秒止まります。nanora-unity-mcp の返し方と、AI の立て直し方。
sidebar:
  order: 5
---

Unity はコンパイルのあとや Play モードに入るとき、スクリプトを全部読み直します（ドメインリロード）。サーバーも Editor の一部なので、数秒止まります。

リロードで打ち切られた要求には、その旨の JSON-RPC エラーを返します。**ツールの処理はもう終わっているかもしれません。** なので:

1. `/health` が応答するまで待つ（コンパイル中も応答します）
2. 状態を読む — [`mcp_status`](/ja/tools/mcp-status/)、リソース [`unity://editor/state`](/ja/reference/endpoints/#リソース)、[`manage_playmode`](/ja/tools/manage-playmode/) の `status`
3. そのあとで、変更をやり直すか決める。**同じ変更をそのまま繰り返さない**（もう反映されていることがあります）

## Play のときのリロードをなくす

*Project Settings > Editor > Enter Play Mode Settings* で *Reload Domain* を外すと、Play に入ってもサーバーが止まらなくなります。その場合、自分のスクリプトの static フィールドが初期化されないことに注意してください。
