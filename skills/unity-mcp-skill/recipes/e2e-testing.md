# E2Eテスト自動化レシピ

## 概要

Unity MCPのテストツールはPlaywrightのAPIデザインに着想を得ている。
U-Selectorによるオブジェクト検索、auto-waiting、UIアサーションを組み合わせることで、
コードを書かずにE2Eテストを自動化できる。

> **重要 — Play Modeは必ず status → 分岐 → play の順で呼ぶこと。**
> 既にPlay Mode中（ユーザーが手動起動した等）に `{"action":"play","wait_for_ready":true}` を発射すると、サーバがポーリング待ちに入りクライアントが **応答なしで詰まる**。
> 本レシピ中の「Play Mode開始」はすべて `status` 先読みを省略表記しているものとし、実装時は必ず挟むこと。

## 基本フロー

### 0. Play Mode状態の確認（全レシピ共通・省略禁止）
```json
{"name": "manage_playmode", "arguments": {"action": "status"}}
// 返り値の "Is Playing" が True なら手順1はスキップ
```

### 1. Play Mode開始（isPlaying=False のときだけ）
```json
{"name": "manage_playmode", "arguments": {
  "action": "play",
  "auto_compile": true,
  "wait_for_ready": true,
  "timeout_seconds": 30
}}
```

### 2. 要素の出現を待機
```json
{"name": "wait_for_selector", "arguments": {
  "selector": "#StartButton",
  "state": "interactable",
  "timeout": 10
}}
```

### 3. UI操作
```json
{"name": "simulate_input", "arguments": {
  "action": "ui_click",
  "selector": "#StartButton"
}}
```

### 4. 状態アサーション
```json
{"name": "assert_ui_state", "arguments": {
  "selector": "#ScoreText",
  "property": "text",
  "expected": "Score: 0",
  "comparison": "equals"
}}
```

### 5. スクリーンショット撮影
```json
{"name": "capture_screenshot", "arguments": {"view": "game"}}
```

### 6. Play Mode終了
```json
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

## メインメニューのテスト

```
Step 1: Play Mode開始
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}

Step 2: メニュー表示を待機
{"name": "wait_for_selector", "arguments": {"selector": "#MainMenu", "state": "visible"}}

Step 3: スタートボタンが操作可能か確認
{"name": "assert_ui_state", "arguments": {
  "selector": "#StartButton",
  "property": "interactable",
  "expected": true
}}

Step 4: スタートボタンをクリック
{"name": "simulate_input", "arguments": {"action": "ui_click", "selector": "#StartButton"}}

Step 5: メニューが非表示になるのを待機
{"name": "wait_for_selector", "arguments": {"selector": "#MainMenu", "state": "hidden", "timeout": 5}}

Step 6: ゲーム画面が表示されるのを待機
{"name": "wait_for_selector", "arguments": {"selector": "#GameUI", "state": "visible"}}

Step 7: スクリーンショットで確認
{"name": "capture_screenshot", "arguments": {"view": "game"}}

Step 8: Play Mode終了
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

## ゲームプレイのテスト

```
Step 1: Play Mode開始
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}

Step 2: プレイヤーの存在確認
{"name": "wait_for_selector", "arguments": {"selector": "#Player", "state": "visible"}}

Step 3: 移動入力をシミュレート
{"name": "simulate_input", "arguments": {"action": "key_press", "key": "W"}}

Step 4: 数フレーム待機（ポーズ→ステップで制御）
{"name": "manage_playmode", "arguments": {"action": "pause"}}
{"name": "manage_playmode", "arguments": {"action": "step"}}
{"name": "manage_playmode", "arguments": {"action": "step"}}

Step 5: 移動キーを離す
{"name": "simulate_input", "arguments": {"action": "key_release", "key": "W"}}

Step 6: プレイヤーの位置を確認
{"name": "query_objects", "arguments": {"selector": "#Player", "include_details": true}}

Step 7: Play Mode再開→終了
{"name": "manage_playmode", "arguments": {"action": "resume"}}
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

## UIテキスト検証

### テキストの完全一致
```json
{"name": "assert_ui_state", "arguments": {
  "selector": "#ScoreText",
  "property": "text",
  "expected": "Score: 100",
  "comparison": "equals"
}}
```

### テキストの部分一致
```json
{"name": "assert_ui_state", "arguments": {
  "selector": "#StatusMessage",
  "property": "text",
  "expected": "Level",
  "comparison": "contains"
}}
```

### 数値比較
```json
{"name": "assert_ui_state", "arguments": {
  "selector": "#HealthSlider",
  "property": "value",
  "expected": 0.5,
  "comparison": "greater_than"
}}
```

### 可視性の確認
```json
{"name": "assert_ui_state", "arguments": {
  "selector": "#GameOverPanel",
  "property": "visible",
  "expected": false
}}
```

## テストフレームワークとの連携

### EditMode/PlayModeテスト実行
```json
{"name": "run_tests", "arguments": {
  "test_mode": "EditMode",
  "run_synchronously": true,
  "timeout_seconds": 120
}}
```

### 特定のテストをフィルタ実行
```json
{"name": "run_tests", "arguments": {
  "test_mode": "Both",
  "filter": "PlayerMovement*",
  "run_synchronously": true
}}
```

### テスト結果を確認
```json
{"name": "get_test_results", "arguments": {"include_all_tests": true}}
```

## ドメインリロード対策

Play Mode遷移やコンパイル時にMCPサーバーが再起動する。

**対策パターン:**
1. `manage_playmode` で `wait_for_ready: true` を使う
2. 接続エラー時は `/health` が応答するまで待ち、`mcp_status` か `unity://editor/state` で状態を読んでから続ける（応答が途切れた変更操作は反映済みのことがあるので、無条件に再送しない）
3. `compile_project` の後は `mcp_status` で接続確認

```
Step 1: コンパイル
{"name": "compile_project", "arguments": {"timeout_seconds": 60}}

Step 2: (5秒待機)

Step 3: 接続確認
{"name": "mcp_status", "arguments": {"include_tool_list": false}}

Step 4: Play Mode開始
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}
```
