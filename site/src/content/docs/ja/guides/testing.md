---
title: AI にゲームをテストさせる
description: Claude Code・Codex・Cursor に Unity のシーンを遊ばせて確かめる。オブジェクトを待つ・キーを押す・UI の文字を判定する・スクリーンショットを撮る。E2E のシナリオを 1 回の呼び出しで。
sidebar:
  order: 1
---

シーンを変えた AI には、結果も確かめてほしい。nanora-unity-mcp には、そのための Playwright 風のツールがあります。どれも `#ScoreText` や `.Enemy[tag=Boss]` のような [U-Selector](/ja/reference/u-selector/) で対象を指します。

## 1 手ずつ

| 手順 | ツール |
|---|---|
| Play モードに入る・出る | [`manage_playmode`](/ja/tools/manage-playmode/) |
| オブジェクトが現れる・見える・押せるまで待つ | [`wait_for_selector`](/ja/tools/wait-for-selector/) |
| キーを押す・クリック・ドラッグ・UI の要素を押す | [`simulate_input`](/ja/tools/simulate-input/) |
| 文字・表示・色・値などを判定する | [`assert_ui_state`](/ja/tools/assert-ui-state/) |
| 結果を見る | [`capture_screenshot`](/ja/tools/capture-screenshot/) |

キー入力のシミュレートには **Input System** パッケージが要ります（旧 Input Manager だけだとキーは届きません）。Input System 1.4 以降なら、Unity が裏にあってもキーが届きます。

## シナリオを 1 回で

[`run_e2e_test`](/ja/tools/run-e2e-test/) は手順の並びを Play モードで実行し、手順ごとの合否を返します。手順の種類は `wait_for_selector`・`simulate_input`・`assert`・`screenshot`・`wait`・`execute_csharp`。Play モードへの出入りは自動で、失敗した手順ではスクリーンショットを撮ります。

```json
{
  "name": "run_e2e_test",
  "arguments": {
    "name": "start and score",
    "steps": [
      { "action": "wait_for_selector", "selector": "#StartButton" },
      { "action": "simulate_input", "input_action": "ui_click", "selector": "#StartButton" },
      { "action": "simulate_input", "input_action": "key_tap", "key": "Space" },
      { "action": "wait", "ms": 500 },
      { "action": "assert", "selector": "#ScoreText", "property": "text", "comparison": "not_equals", "expected": "0" },
      { "action": "screenshot", "label": "after-shot" }
    ]
  }
}
```

オブジェクトの名前は例です。自分のシーンの名前に置き換えてください。

## 1 回の確認のその先

- [`visual_regression_test`](/ja/tools/visual-regression-test/) — スクリーンショットを保存した基準と比べる
- [`performance_budget_test`](/ja/tools/performance-budget-test/) — FPS・描画数・メモリを予算と比べる
- [`run_tests`](/ja/tools/run-tests/) と [`get_test_results`](/ja/tools/get-test-results/) — Unity Test Framework のテストを実行する
- [`generate_test_report`](/ja/tools/generate-test-report/) — 結果をレポートにまとめる

## スクリーンショットに UI が写らない

game ビューの撮影はカメラの描画だけを撮るので、*Screen Space - Overlay* の Canvas は写りません。AI に見せたい UI は *Screen Space - Camera* にしてください。
