# Testing Tools Reference

## run_tests

Unity Test Frameworkのテストを実行する。EditMode/PlayMode両対応。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| test_mode | string | No | "Both" | `EditMode`, `PlayMode`, `Both` |
| filter | string | No | "" | テスト名フィルタ（ワイルドカード対応） |
| category | string | No | "" | カテゴリフィルタ |
| assembly_filter | string | No | "" | アセンブリ名フィルタ |
| run_synchronously | boolean | No | true | テスト完了を待機するか |
| timeout_seconds | integer | No | 300 | 最大待機時間（秒） |

### Example

```json
{"name": "run_tests", "arguments": {
  "test_mode": "EditMode",
  "filter": "PlayerMovement*",
  "run_synchronously": true,
  "timeout_seconds": 120
}}
```

### Response

```
# Test Run Report

## Summary
- Overall Result: **Passed**
- Total Tests: 5
- Passed: 4
- Failed: 1
- Skipped: 0
- Inconclusive: 0
- Duration: 2.34s

## Failed Tests
### TestPlayerJump
- Full Name: `Tests.PlayerMovementTests.TestPlayerJump`
- Duration: 0.123s
- Message: Expected height > 2.0 but was 1.5
- Stack Trace:
...
```

---

## get_test_results

テスト定義一覧を取得する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| include_all_tests | boolean | No | false | 全テスト定義を含めるか |

### Example

```json
{"name": "get_test_results", "arguments": {"include_all_tests": true}}
```

---

## simulate_input

キーボード・マウス・UI入力をシミュレートする。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | "key_tap" | `key_press`, `key_release`, `key_tap`, `mouse_click`, `mouse_move`, `mouse_drag`, `ui_click` |
| key | string | No | - | キー名（key_*アクション用） |
| position | object | No | - | スクリーン座標 `{x, y}`（mouse_*アクション用） |
| button | integer | No | 0 | マウスボタン（0=左, 1=右, 2=中） |
| selector | string | No | - | U-Selector（ui_clickアクション用） |
| duration | number | No | 0.1 | ドラッグ操作の持続時間（秒） |

### キー名一覧

| キー | 名前 |
|------|------|
| Space | Space |
| Enter/Return | Enter, Return |
| Escape | Escape, Esc |
| Tab | Tab |
| Backspace | Backspace |
| Delete | Delete |
| 矢印 | Up, Down, Left, Right |
| 修飾 | Shift, Ctrl, Control, Alt |
| 文字 | A-Z |
| 数字 | 0-9 |

### Example: キータップ

```json
{"name": "simulate_input", "arguments": {"action": "key_tap", "key": "Space"}}
```

### Example: UIクリック

```json
{"name": "simulate_input", "arguments": {"action": "ui_click", "selector": "#StartButton"}}
```

### Example: マウスクリック

```json
{"name": "simulate_input", "arguments": {
  "action": "mouse_click",
  "position": {"x": 960, "y": 540},
  "button": 0
}}
```

### ui_click の自動検出

`ui_click` は対象オブジェクトのコンポーネントを自動検出:
- **Button**: `onClick.Invoke()` を呼び出し
- **Toggle**: `isOn` をトグル
- **InputField**: `Select()` でフォーカス
- **その他**: EventSystem経由でpointerClickを送信

---

## capture_screenshot

Game ViewまたはScene Viewのスクリーンショットを撮影する。Base64エンコードされたPNG画像を返す。

> **重要な制限**: このツールはカメラRenderTexture経由でキャプチャするため、**以下は映らない**:
> - **UI Toolkit の runtime panel（UIDocument）** — PanelSettings経由で描画される
> - **Screen Space Overlay の UGUI Canvas** — カメラに依存せず画面に直書きされる
>
> これらを含む画面を撮るには、`execute_csharp` (`force_roslyn: true`) で
> `UnityEngine.ScreenCapture.CaptureScreenshot("Temp/shot.png")` を呼び、**保存されたファイルをホスト側から読む**こと。
>
> 例:
> ```json
> {"name": "execute_csharp", "arguments": {
>   "code": "UnityEngine.ScreenCapture.CaptureScreenshot(\"Temp/shot.png\"); return \"ok\";",
>   "force_roslyn": true
> }}
> ```
>
> UGUIのみなら Canvas の `renderMode` を `ScreenSpaceCamera` にしてもよい（UI Toolkit には効かない）。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| view | string | No | "game" | `game` または `scene` |
| width | integer | No | 0 | 幅（0 = 現在のサイズ） |
| height | integer | No | 0 | 高さ（0 = 現在のサイズ） |
| supersampling | integer | No | 1 | スーパーサンプリング係数（1-4） |
| save_to_file | string | No | - | ファイル保存パス |
| camera_position | object | No | - | Scene Viewカメラ位置 `{x, y, z}` |
| camera_rotation | object | No | - | Scene Viewカメラ回転（オイラー角） `{x, y, z}` |
| camera_size | number | No | - | Scene View正投影サイズ/パースペクティブ距離 |

### Example

```json
{"name": "capture_screenshot", "arguments": {
  "view": "game",
  "width": 1920,
  "height": 1080,
  "save_to_file": "Screenshots/test.png"
}}
```

### Response

MCP Image Content (Base64 PNG) + テキスト情報:
```
Screenshot captured from game view.
Size: 1920x1080 (or current if 0)
Data size: 245KB
Saved to: Screenshots/test.png
```

---

## assert_ui_state

UI状態のアサーション。Playwright風のexpect。条件が満たされるかタイムアウトするまでポーリング。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| selector | string | Yes | - | U-Selector |
| property | string | Yes | - | `text`, `active`, `enabled`, `visible`, `color`, `alpha`, `value`, `interactable` |
| expected | any | Yes | - | 期待値 |
| comparison | string | No | "equals" | `equals`, `contains`, `starts_with`, `ends_with`, `greater_than`, `less_than`, `not_equals` |
| timeout | number | No | 5 | タイムアウト（秒） |
| poll_interval | number | No | 0.1 | ポーリング間隔（秒） |

### Property の取得元

| property | 取得元コンポーネント |
|----------|---------------------|
| text | TextMeshProUGUI > Text > TextMeshPro |
| active | GameObject.activeInHierarchy |
| enabled | Behaviour.enabled |
| visible | CanvasGroup.alpha > 0, Renderer.enabled |
| color | Image.color / RawImage.color / Text.color |
| alpha | CanvasGroup.alpha / Image.color.a |
| value | Slider.value / Scrollbar.value / InputField.text |
| interactable | Button.interactable / Selectable.interactable / CanvasGroup.interactable |

### Example

```json
{"name": "assert_ui_state", "arguments": {
  "selector": "#ScoreText",
  "property": "text",
  "expected": "Score: 100",
  "comparison": "equals",
  "timeout": 10
}}
```

### Response (成功)

```
Assertion PASSED
- Selector: #ScoreText
- Property: text
- Expected: Score: 100 (equals)
- Actual: Score: 100
- Elapsed: 234ms
```

### Response (失敗)

```
Assertion FAILED (timeout after 5000ms)
- Selector: #ScoreText
- Property: text
- Expected: Score: 100 (equals)
- Actual: Score: 50
```

---

## manage_playmode

Play Modeの管理（開始・停止・一時停止・ステップ・ステータス）。

> **運用規約（必読）**: `action: play` を発射する前に**必ず** `action: status` を呼び、`Is Playing: True` のときは play を**スキップする**。既にPlay中に `wait_for_ready: true` 付きで play を呼ぶとサーバ側が内部ポーリングに入り、HTTP/SSE層のタイムアウトを踏んでクライアント側が応答待ちで詰まる（実運用で確認済み）。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | - | `play`, `stop`, `pause`, `resume`, `step`, `status` |
| auto_compile | boolean | No | false | play前にコンパイル（playアクションのみ） |
| wait_for_ready | boolean | No | false | Play Mode完全起動を待機（playアクションのみ）。**既にPlay中なら再発射しないこと** |
| timeout_seconds | integer | No | 30 | wait_for_ready時の最大待機時間（1-120秒） |

### Example: Play Mode開始（必ず status → 分岐 → play）

```json
// Step 1: 現在状態を取得
{"name": "manage_playmode", "arguments": {"action": "status"}}
// 返り値の "Is Playing" が False のときだけ次を実行
{"name": "manage_playmode", "arguments": {
  "action": "play",
  "auto_compile": true,
  "wait_for_ready": true,
  "timeout_seconds": 30
}}
```

### Response

```
Play Mode is ready.
- Is Playing: True
- Application.isPlaying: True
- Frame Count: 1
- Time to ready: 3.2s
```

### Example: ステータス確認

```json
{"name": "manage_playmode", "arguments": {"action": "status"}}
```

### Response

```
=== Play Mode Status ===
Is Playing: True
Is Paused: False
Is Compiling: False
Is Updating: False
Current State: Play Mode (Running)

=== Time Info ===
Time Since Startup: 12.34s
Time Scale: 1
Frame Count: 742
```
