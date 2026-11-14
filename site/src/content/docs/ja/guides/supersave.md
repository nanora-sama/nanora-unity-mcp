---
title: SuperSave モード（ツール一覧で文脈を埋めない）
description: Unity を NANORA_MCP_SUPERSAVE=1 で起動すると、nanora-unity-mcp はメタツール 3 本だけを見せます。ツールの一覧が AI の文脈を埋めません。
sidebar:
  order: 3
---

ツールの名前とスキーマは、どれも AI の文脈に入ります。57 ツールあると、それだけでかなりの量です。**SuperSave モード**では、クライアントに見せるのはメタツール 3 本だけになります。

1. [`list_categories`](/nanora-unity-mcp/ja/tools/list-categories/) — 分類の一覧
2. [`list_tools`](/nanora-unity-mcp/ja/tools/list-tools/) — 1 分類のツールを 1 行ずつ
3. [`execute`](/nanora-unity-mcp/ja/tools/execute/) — 名前でツールを呼ぶ: `{"tool": "capture_screenshot", "args": { … }}`

## 有効にする

Unity を起動する前に、環境変数を `1` か `true` にします。

```powershell
# PowerShell: このあと同じウィンドウから Unity を起動する
$env:NANORA_MCP_SUPERSAVE = "1"
```

```sh
# macOS / Linux
NANORA_MCP_SUPERSAVE=1 /path/to/Unity -projectPath .
```

Console に `[UnityMCP] SuperSave mode enabled - only meta-tools will be exposed` と出ます。

`list_tools` が返すのは短い説明だけで、パラメータのスキーマは含みません。各ツールのパラメータは[ツールリファレンス](/nanora-unity-mcp/ja/tools/)のページにあります。
