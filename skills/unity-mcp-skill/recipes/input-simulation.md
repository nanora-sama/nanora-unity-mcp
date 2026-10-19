# 入力シミュレーション レシピ

## 概要

`simulate_input` を使ってキーボード・マウス・UI操作を自動化する。
Legacy Input ManagerとNew Input Systemの両方に対応。

## キーボード入力

### キーを押す（押しっぱなし）
```json
{"name": "simulate_input", "arguments": {"action": "key_press", "key": "W"}}
```

### キーを離す
```json
{"name": "simulate_input", "arguments": {"action": "key_release", "key": "W"}}
```

### キーをタップ（押して離す）
```json
{"name": "simulate_input", "arguments": {"action": "key_tap", "key": "Space"}}
```

### 対応キー名
- 文字: `A`-`Z`
- 数字: `0`-`9`
- 特殊: `Space`, `Enter`, `Return`, `Escape`, `Tab`, `Backspace`, `Delete`
- 矢印: `Up`, `Down`, `Left`, `Right`
- 修飾: `Shift`, `Ctrl`, `Control`, `Alt`

## マウス入力

### マウスクリック
```json
{"name": "simulate_input", "arguments": {
  "action": "mouse_click",
  "position": {"x": 960, "y": 540},
  "button": 0
}}
```

### 右クリック
```json
{"name": "simulate_input", "arguments": {
  "action": "mouse_click",
  "position": {"x": 960, "y": 540},
  "button": 1
}}
```

### マウス移動
```json
{"name": "simulate_input", "arguments": {
  "action": "mouse_move",
  "position": {"x": 500, "y": 300}
}}
```

### マウスドラッグ
```json
{"name": "simulate_input", "arguments": {
  "action": "mouse_drag",
  "position": {"x": 800, "y": 600},
  "duration": 0.5
}}
```

## UI操作

### ボタンクリック（U-Selector指定）
```json
{"name": "simulate_input", "arguments": {
  "action": "ui_click",
  "selector": "#StartButton"
}}
```

### トグル操作
```json
{"name": "simulate_input", "arguments": {
  "action": "ui_click",
  "selector": "#SoundToggle"
}}
```

### 入力フィールドの選択
```json
{"name": "simulate_input", "arguments": {
  "action": "ui_click",
  "selector": "#PlayerNameInput"
}}
```

## ゲームプレイ自動化の例

> **Play Modeは status 先読みを挟むこと**（既にPlay中に `wait_for_ready: true` を再発射するとクライアント側で詰まる）

### キャラクター移動（WASD）
```
0. Play Mode状態を確認
{"name": "manage_playmode", "arguments": {"action": "status"}}

1. Play Mode開始（"Is Playing": False のときだけ）
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}

2. 前方に移動（Wキー押しっぱなし）
{"name": "simulate_input", "arguments": {"action": "key_press", "key": "W"}}

3. 数フレーム進める
{"name": "manage_playmode", "arguments": {"action": "pause"}}
{"name": "manage_playmode", "arguments": {"action": "step"}}
{"name": "manage_playmode", "arguments": {"action": "step"}}
{"name": "manage_playmode", "arguments": {"action": "step"}}

4. キーを離す
{"name": "simulate_input", "arguments": {"action": "key_release", "key": "W"}}

5. 位置を確認
{"name": "query_objects", "arguments": {"selector": "#Player", "include_details": true}}

6. Play Mode終了
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

### メニューナビゲーション
```
1. 設定ボタンをクリック
{"name": "simulate_input", "arguments": {"action": "ui_click", "selector": "#SettingsButton"}}

2. 設定画面の表示を待機
{"name": "wait_for_selector", "arguments": {"selector": "#SettingsPanel", "state": "visible"}}

3. 音量スライダーのあるオブジェクトをクリック
{"name": "simulate_input", "arguments": {"action": "ui_click", "selector": "#VolumeSlider"}}

4. 閉じるボタンをクリック
{"name": "simulate_input", "arguments": {"action": "ui_click", "selector": "#CloseButton"}}
```

## 注意事項

- New Input Systemが有効な場合、`key_press`/`key_release` は `InputState.Change` を使用
- Legacy Input Managerの場合、`Event.KeyboardEvent` にフォールバック
- `ui_click` はButton, Toggle, InputFieldのコンポーネントを自動検出して適切に操作
- マウス座標は画面左下が原点（0, 0）
- Play Mode中のみ入力が有効（Edit Modeではログのみ）
