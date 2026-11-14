---
title: Unity MCP の比較
description: nanora-unity-mcp・CoplayDev unity-mcp・CoderGamester mcp-unity・IvanMurzak Unity-MCP・Unity 公式 MCP が、Unity のほかに何を必要とするか。各プロジェクトの資料で確かめた比較。
---

AI を Unity につなぐ良いツールはいくつもあります。実際に使ううえでいちばん違うのは、**Unity のほかに何を入れて動かすか**です。2026-10-19 に各プロジェクトの資料で確かめました。変わることがあるので、決める前に各プロジェクトを確かめてください。

| | Unity のほかに要るもの | Unity の版 |
|---|---|---|
| **nanora-unity-mcp**（MIT） | なし（Editor プロセス内の C#。Roslyn を同梱） | 2021.3〜6 |
| [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)（MIT） | Python 3.10 以降（uv） | 2021.3 LTS〜6.x |
| [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity)（MIT） | Node.js 18 以降、Unity 側の WebSocket | 6 以降 |
| [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)（Apache-2.0） | 別プロセスのサーバー実行ファイル | — |
| Unity 公式 MCP（AI Assistant） | 中継プログラム、Unity Cloud につないだプロジェクト、AI ツールのトライアルかサブスク | 6 以降 |

## nanora-unity-mcp の位置づけ

- **Unity だけで完結。** Python・Node.js・別の実行ファイル・中継プログラムが要りません。
- **Streamable HTTP と SSE を同じサーバーで。** Codex と Claude Code・Cursor が同じ Editor を使えます。
- **プロジェクトごとに決まるポート・接続設定の自動生成・プロジェクト名を返す `/health`。** 設定なしで、Unity を複数開いても混線しません。
- **作るだけでなく確かめる。** U-Selector と `wait_for_selector`・`assert_ui_state`・`simulate_input`、AI に作業を確かめさせる [Skill](/ja/guides/skills/)。

## ほかのツールが上のところ

- ツールの数はほかの方が多いです（IvanMurzak は拡張込みで 70 以上）。コミュニティもほかの方が大きいです。
- リモートからの利用（OAuth・クラウドの中継）は目指していません。nanora-unity-mcp はローカル専用です。
- C# の実行や複数の Editor への対応は、ほかのツールにもあります。
