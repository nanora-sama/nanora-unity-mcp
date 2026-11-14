---
title: Unity を複数開く
description: nanora-unity-mcp はプロジェクトのパスからポート（51726〜51825）を決め、接続設定を書き、/health でプロジェクト名を返します。Unity を何個開いても混線しません。
sidebar:
  order: 4
---

Unity のプロジェクトを同時にいくつ開いても、それぞれに別のサーバーが立ちます。

- **ポートはプロジェクトのパスから決まります**（51726〜51825）。同じプロジェクトはいつも同じ番号なので、再起動してもクライアントの設定はそのまま使えます。番号が使用中なら次の空き番号に移ります。*Window > Unity MCP Server* に出た番号を使ってください。
- **接続設定を書きます** — Claude Code の `.mcp.json`、Cursor と Claude Desktop は Setup Wizard から。
- **`/health` がプロジェクト名を返します。** つながった先を信じる前に聞いてください。

```sh
curl http://127.0.0.1:<PORT>/health
```

プロジェクト名とパス・プロセス ID・パッケージの版・プロトコルの版・トランスポートが返ります。`projectPath` が意図したプロジェクトでなければ、クライアントの URL を直してください。

AI 自身は、リソース [`unity://project/info`](/ja/reference/endpoints/#リソース) で同じことを確かめられます。
