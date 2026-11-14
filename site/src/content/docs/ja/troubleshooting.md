---
title: 困ったとき
description: nanora-unity-mcp でよくある問題の直し方。導入直後のコンパイルエラー、Running にならない、Claude Code や Codex に出てこない、違うプロジェクトにつながる、返事が遅い、ドメインリロードのエラー、キー入力とスクリーンショット。
---

## 入れた直後にコンパイルエラーが出る

`Newtonsoft`・`JObject`・`TestTools` が見つからないなら、.unitypackage で入れて依存パッケージが無い状態です。*Add package by name…* で `com.unity.nuget.newtonsoft-json`（無ければ `com.unity.test-framework` も）を入れてください。Git URL か tarball で入れ直すと、依存パッケージも一緒に入ります。

## `Running` にならない

*Window > Unity MCP Server* で Start し、Console に `[UnityMCP]` のエラーが出ていないか見てください。ポートが使用中なら次の空き番号に移ります。ウィンドウに出た番号を使ってください。

## Claude Code に出てこない

Unity を先に起動し、**プロジェクトのフォルダで** `claude` を起動し直してください。そこに `.mcp.json` があるかも確かめます。[Claude Code をつなぐ](/nanora-unity-mcp/ja/connect/claude-code/)を参照。

## Codex に出てこない

URL が `/mcp` で終わっているか、番号が今の番号かを確かめてください。設定はセッションの開始時に読まれるので、新しいセッションを始めます。[Codex をつなぐ](/nanora-unity-mcp/ja/connect/codex/)を参照。

## 違うプロジェクトを触っている

`curl http://127.0.0.1:<PORT>/health` の `projectPath` を、意図したプロジェクトと比べてください。番号が変わっていたらクライアントの設定を直します。[Unity を複数開く](/nanora-unity-mcp/ja/guides/multiple-projects/)を参照。

## Unity が裏にあると返事が遅い・来ない

Setup Wizard の **Optimize for MCP**（Background Execution）を有効にしてください。

## 「domain reload」「state unknown」というエラーが返る

コンパイル中か Play に入るところで、サーバーが数秒止まっただけです。`/health` が返るのを待ち、状態を読んでください。**同じ変更をすぐ繰り返さない**こと（もう反映されていることがあります）。[ドメインリロード](/nanora-unity-mcp/ja/guides/domain-reload/)を参照。

## `simulate_input` のキーが効かない

Play 中か、**Input System** パッケージが入っているかを確かめてください（旧 Input Manager だけだとキーは届きません）。Input System 1.4 以降なら、Unity が裏にあってもキーが届きます。

## スクリーンショットに UI が写らない

game ビューの撮影はカメラの描画だけを撮るので、*Screen Space - Overlay* の Canvas は写りません。*Screen Space - Camera* にしてください。

## `execute_csharp` が拒否される

プロセスの起動やネットワークなど、一部の API は使えません。理由はエラー文に出ます。[セキュリティ](/nanora-unity-mcp/ja/guides/security/)を参照。

## ツールが多すぎて AI が迷う

Unity を `NANORA_MCP_SUPERSAVE=1` で起動してください。[SuperSave モード](/nanora-unity-mcp/ja/guides/supersave/)を参照。

## それでも直らないとき

[Issue](https://github.com/nanora-sama/nanora-unity-mcp/issues/new/choose) へどうぞ（日本語で大丈夫です）。次の 4 つがあると早く直せます。

1. Unity のバージョンと OS
2. パッケージの版（Package Manager か、`/health` の `version`）
3. 使っている AI クライアントと、頼んだ文
4. [`mcp_status`](/nanora-unity-mcp/ja/tools/mcp-status/) の結果と、Console の `[UnityMCP]` のエラー
