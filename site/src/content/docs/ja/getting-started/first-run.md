---
title: サンプルのゲームで試す
description: 同梱のシューティングゲームのサンプルを取り込み、AI に Play・キー入力・スコアの判定・スクリーンショットを nanora-unity-mcp 経由で頼んでみる。
sidebar:
  order: 2
---

パッケージには小さなシューティングゲームが入っています。AI がシーンを遊んでテストする様子を数分で見られます。

1. *Package Manager > nanora-unity-mcp > **Samples** > Shooting Game Sample > **Import***（.unitypackage で入れた人は、配布物の `Samples/ShootingGame` を Assets にコピー）
2. *Tools > Nanora Unity MCP > Samples > **Build Shooting Game Scene***
3. AI にこう頼みます:

```text
シューティングゲームのシーンを Play して、Space を 5 回押して撃って、
ScoreText が 0 より大きくなったか確かめて、スクリーンショットを見せて。
```

AI は [`manage_playmode`](/ja/tools/manage-playmode/)・[`simulate_input`](/ja/tools/simulate-input/)・[`assert_ui_state`](/ja/tools/assert-ui-state/)・[`capture_screenshot`](/ja/tools/capture-screenshot/) を順に使います。

「同じことを `run_e2e_test` の 1 回でやって」と頼むと、手順ごとの合否が一覧で返ります。詳しくは [AI にゲームをテストさせる](/ja/guides/testing/)。
