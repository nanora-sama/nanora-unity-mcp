---
title: 同梱の Skill（Claude Code 用）
description: nanora-unity-mcp は、AI に Unity の作業を確かめさせる Claude Code の Skill を入れます。反映確認・PR 前チェック・E2E スモークテスト・マテリアル修復・性能の予算・シーンの準備。
sidebar:
  order: 2
---

パッケージには Claude Code の Skill が 6 つ入っています。Claude Code の接続を設定すると `<Project>/.claude/skills/` に入ります。更新しても自分で直した内容は残ります（[`install_skills`](/nanora-unity-mcp/ja/tools/install-skills/) の `force: true` で上書き）。

| Skill | すること |
|---|---|
| `/unity-verify-applied` | 「できた」と言う前に、保存されたファイル・動いている状態・見た目の結果を確かめる |
| `/unity-pre-pr-check` | PR の前に、リビジョン・テスト・保存状態・見た目の動きを確かめる |
| `/unity-e2e-test` | Play モードのスモークテスト（待つ・入力・判定・スクリーンショット） |
| `/unity-material-repair` | ピンクのマテリアルを見つけて直し、前後のスクリーンショットを撮る |
| `/unity-performance-budget` | FPS・描画数・メモリを予算と比べる |
| `/unity-scene-setup` | ライト・カメラ・地面のある新しいシーンを作る |

Skill は Claude Code の機能です。ほかのクライアントでも同じツールはそのまま使えます。Skill の中身はただの Markdown なので、確認の手順書として読むこともできます。
