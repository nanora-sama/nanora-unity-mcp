---
title: nanora-unity-mcp の導入
description: nanora-unity-mcp を Unity 2021.3 以降に入れる方法。Git URL・tarball・.unitypackage のどれかで。.unitypackage 以外は必要なパッケージも自動で入ります。
sidebar:
  order: 1
---

nanora-unity-mcp は Unity のパッケージです。必要なのは **Unity 2021.3 LTS 以降**（Unity 6 で日常使用）だけ。キー入力のシミュレートを使うなら Input System パッケージ（1.4 以降）も入れてください。

## 入れ方を選ぶ

| 方法 | 手順 | 向いている人 |
|---|---|---|
| **Git URL** | *Window > Package Manager > **+** > Add package from git URL…* に下の URL を貼る | いつも最新版を使いたい人 |
| **tarball（.tgz）** | [Releases](https://github.com/nanora-sama/nanora-unity-mcp/releases) か BOOTH から `com.nanora.nanora-unity-mcp-<版>.tgz` を落とし、*Add package from tarball…* で選ぶ | 版を固定したい人（おすすめ） |
| **.unitypackage** | 先に *Add package by name…* で `com.unity.nuget.newtonsoft-json` を入れ、*Assets > Import Package > Custom Package…* | Assets に直接置きたい人 |

```text
https://github.com/nanora-sama/nanora-unity-mcp.git?path=Assets/NanoraUnityMCP
```

- Git URL と tarball は、必要なパッケージ（Newtonsoft JSON・Test Framework・uGUI）も自動で入ります。.unitypackage は入らないので、先に Newtonsoft JSON を入れてください。サンプルも含みません。
- .tgz はプロジェクトの外の、消さない場所に置いてください（Unity はその場所を参照し続けます）。

## 起動を確かめる

サーバーは Editor と一緒に起動します。初回は **Setup Wizard**（*Window > Unity MCP > Setup Wizard*）が開きます。*Window > Unity MCP Server* に `Running` とポート番号が出ていれば準備完了です。

```sh
curl http://127.0.0.1:<PORT>/health
```

返ってきた `projectPath` が今のプロジェクトなら、[AI をつなぎましょう](/ja/connect/claude-code/)。

## おすすめ設定

- Setup Wizard の **Optimize for MCP (Recommended)**: Unity が裏にあっても応答するようにします。AI はたいてい別のウィンドウから操作するので、ほぼ必須です。[`configure_editor`](/ja/tools/configure-editor/) の action `optimize_for_mcp` でも同じです。
- 任意: *Project Settings > Editor > Enter Play Mode Settings* で *Reload Domain* を外すと、Play に入るときにサーバーが止まらなくなります。[ドメインリロード](/ja/guides/domain-reload/)を参照。

## 更新・削除

- **更新** — Git URL: Package Manager の *Update*。tarball: 新しい .tgz を同じ手順で選び直す。.unitypackage: 新しいものを上書きインポート。
- **削除** — Package Manager の *Remove*（.unitypackage は `Assets/NanoraUnityMCP` を削除）。自動で作られた `.mcp.json` と `.claude/skills/unity-*` は、要らなければ手で消してください。
