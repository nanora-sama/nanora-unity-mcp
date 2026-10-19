# Visual Regression Testing レシピ

## 概要

`capture_screenshot` で取得したスクリーンショットをベースラインと比較し、
意図しない視覚的変更を検出する。

> **注意1 (Play Mode)**: `play` の前に必ず `action: status` を呼び、`Is Playing: False` のときだけ play を発射すること。既にPlay中に `wait_for_ready: true` を打つとサーバ側がポーリング待ちで応答を返さず、クライアントが詰まる。
>
> **注意2 (UI Toolkit)**: `capture_screenshot` は UI Toolkit の runtime panel (UIDocument) を含まない。UI Toolkit を含む画面を撮るときは `execute_csharp` 経由で `UnityEngine.ScreenCapture.CaptureScreenshot("Temp/shot.png")` を呼び、保存されたファイルを読むこと。

## 基本ワークフロー

### 1. ベースライン撮影

```json
// status先読み: 既にPlay中なら play をスキップ
{"name": "manage_playmode", "arguments": {"action": "status"}}
// Is Playing が False のときだけ
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}
```

```json
{"name": "wait_for_selector", "arguments": {"selector": "#MainMenu", "state": "visible"}}
```

```json
{"name": "capture_screenshot", "arguments": {
  "view": "game",
  "width": 1920,
  "height": 1080,
  "save_to_file": "Tests/Screenshots/baseline_main_menu.png"
}}
```

```json
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

### 2. 変更後の比較撮影

コードやアセットを変更した後、同じ手順でスクリーンショットを撮影:

```json
{"name": "capture_screenshot", "arguments": {
  "view": "game",
  "width": 1920,
  "height": 1080,
  "save_to_file": "Tests/Screenshots/current_main_menu.png"
}}
```

### 3. 比較（C#コードで実行）

```json
{"name": "execute_csharp", "arguments": {
  "code": "var baseline = System.IO.File.ReadAllBytes(\"Tests/Screenshots/baseline_main_menu.png\"); var current = System.IO.File.ReadAllBytes(\"Tests/Screenshots/current_main_menu.png\"); return $\"Baseline: {baseline.Length} bytes, Current: {current.Length} bytes, Match: {baseline.Length == current.Length && baseline.SequenceEqual(current)}\";"
}}
```

## Scene View からの撮影

特定のカメラアングルからの撮影:

```json
{"name": "capture_screenshot", "arguments": {
  "view": "scene",
  "width": 1920,
  "height": 1080,
  "camera_position": {"x": 0, "y": 10, "z": -15},
  "camera_rotation": {"x": 30, "y": 0, "z": 0},
  "camera_size": 5,
  "save_to_file": "Tests/Screenshots/scene_overview.png"
}}
```

## 複数画面の一括撮影

### メニュー画面
```json
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}
{"name": "wait_for_selector", "arguments": {"selector": "#MainMenu", "state": "visible"}}
{"name": "capture_screenshot", "arguments": {"view": "game", "save_to_file": "Tests/Screenshots/01_main_menu.png"}}
```

### ゲーム画面
```json
{"name": "simulate_input", "arguments": {"action": "ui_click", "selector": "#StartButton"}}
{"name": "wait_for_selector", "arguments": {"selector": "#GameUI", "state": "visible", "timeout": 10}}
{"name": "capture_screenshot", "arguments": {"view": "game", "save_to_file": "Tests/Screenshots/02_gameplay.png"}}
```

### ポーズ画面
```json
{"name": "simulate_input", "arguments": {"action": "key_tap", "key": "Escape"}}
{"name": "wait_for_selector", "arguments": {"selector": "#PauseMenu", "state": "visible"}}
{"name": "capture_screenshot", "arguments": {"view": "game", "save_to_file": "Tests/Screenshots/03_pause.png"}}
```

```json
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

## 高品質撮影

### スーパーサンプリング付き（アンチエイリアス効果）
```json
{"name": "capture_screenshot", "arguments": {
  "view": "game",
  "width": 1920,
  "height": 1080,
  "supersampling": 2,
  "save_to_file": "Tests/Screenshots/hq_screenshot.png"
}}
```

## ヒント

- ベースライン画像はバージョン管理に含める
- 解像度は固定値を指定する（0だと環境依存になる）
- スーパーサンプリングはベースラインと同じ値を使う
- Scene Viewのカメラ位置を固定値で指定すると再現性が高い
- Play Mode中はGame View、エディタ時はScene Viewを使い分ける
