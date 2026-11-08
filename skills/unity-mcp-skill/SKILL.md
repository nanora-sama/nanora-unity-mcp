---
name: unity-mcp-skill
description: "Unity Editor を MCP 経由で AI から操作するスキル。シーン操作・コード実行・E2Eテスト自動化・パフォーマンス分析・Visual Regression Testing・Prefab/アセット管理・リフレクション・バッチ実行の詳細リファレンスとレシピを提供。Unity開発でAIアシスタントを使いたいときに参照。"
---

# Unity MCP スキル

## 概要

nanora-unity-mcp (MCPサーバー) を通じてUnity Editorと連携するためのスキル。

このスキルの役割:
- Unity MCP の全ツールリファレンスを提供（50+ツール、完全なJSON Schema付き）
- E2Eテスト自動化のレシピを提供（Playwright風のセレクタ・待機・アサーション）
- U-Selector構文の完全ガイドを提供
- パフォーマンス分析・Visual Regression Testing のワークフローを提供

## セットアップ

### 1. パッケージインストール

`Assets/NanoraUnityMCP` フォルダをプロジェクトにインポート。

### 2. Setup Wizard

`Window > Unity MCP > Setup Wizard` を開き、クライアント設定を行う。

### 3. Claude Codeで接続

```bash
# 通常は不要: Unity が起動するとプロジェクト直下に .mcp.json が作られ、そのフォルダの claude が自動でつながる
claude mcp add --transport sse nanora-unity-mcp http://localhost:<PORT>/sse
```

> **ポート番号はプロジェクトごとに変わる可能性がある**。プロジェクトルートの `.mcp.json` を正とし、そこに記載されたURLで接続すること。ポートが噛み合わないときは `~/.unity-mcp-registry.json` や `/health` スキャンで特定できる。

### 4. 初回最適化（推奨）

接続後、以下を実行してバックグラウンド実行を有効化:

```json
{"name": "configure_editor", "arguments": {"action": "optimize_for_mcp"}}
```

## MCP ツール一覧

### Scene (シーン操作)
| ツール | 説明 |
|--------|------|
| `get_scene_hierarchy` | シーン内の全GameObjectのヒエラルキーを取得 |
| `get_object_details` | GameObjectの詳細情報（全コンポーネント・プロパティ）を取得 |
| `create_game_object` | 新しいGameObjectを作成（プリミティブ型・位置・親指定） |
| `modify_game_object` | GameObjectのTransform・名前・Active状態などを変更 |
| `delete_game_object` | GameObjectを削除（Undo対応） |
| `manage_scene` | シーンの新規作成・保存・読み込み・情報取得 |

### Query (検索・セレクタ)
| ツール | 説明 |
|--------|------|
| `query_objects` | U-Selector構文でGameObjectを検索 |
| `search_assets` | プロジェクト内アセットをファジー検索 |
| `instantiate_asset` | プレハブ・アセットをシーンにインスタンス化 |
| `wait_for_selector` | セレクタにマッチするオブジェクトが条件を満たすまで待機 |

### Code (コード実行・コンパイル)
| ツール | 説明 |
|--------|------|
| `execute_csharp` | C#コードをUnity Editor内で動的実行（Roslyn/リフレクション）。**複数statement・`var`宣言・`using`・class/method定義を含む場合は `force_roslyn: true` を付けること**（付けないとreflectionに落ちて `Expression not directly evaluable` で失敗） |
| `execute_menu_item` | Unityメニューアイテムをパスで実行 |
| `compile_project` | プロジェクトのスクリプトコンパイルをトリガーし完了を待機 |
| `get_compilation_errors` | 現在のコンパイルエラー・警告を取得（再コンパイルなし） |
| `get_console_logs` | Unityコンソールのログを取得 |

### Testing (テスト)
| ツール | 説明 |
|--------|------|
| `run_tests` | Unity Test Frameworkのテストを実行 |
| `get_test_results` | テスト結果を取得 |
| `simulate_input` | キーボード・マウス入力をシミュレート |
| `capture_screenshot` | Game View/Scene Viewのスクリーンショットを撮影（Base64）。**UI Toolkit の runtime panel（UIDocument）は映らない** — その場合は `execute_csharp` で `UnityEngine.ScreenCapture.CaptureScreenshot("Temp/foo.png")` を呼んでファイル経由で取得する |
| `assert_ui_state` | UI状態のアサーション（Playwright風expect） |
| `manage_playmode` | Play Modeの開始・停止・一時停止・ステップ実行。**`play` を呼ぶ前に必ず `action: status` で `isPlaying` を確認**し、true なら play を発射しない（既にPlay中に `wait_for_ready: true` 付きで呼ぶとサーバ側ポーリング待ちでクライアントが詰まる） |

### Performance (パフォーマンス)
| ツール | 説明 |
|--------|------|
| `analyze_frame` | フレームレンダリング分析（ドローコール・シェーダー使用量） |
| `analyze_memory` | メモリ使用量分析（カテゴリ別・トップ消費者） |
| `profile_cpu` | CPU プロファイリング（ProfilerRecorder API） |
| `get_profiler_stats` | プロファイラ統計・パフォーマンスメトリクス |

### Asset (アセット・Prefab管理)
| ツール | 説明 |
|--------|------|
| `manage_prefabs` | Prefab管理（create/apply/unpack/save/create_variant/revert/get_info） |
| `manage_assets` | アセットCRUD（get_info/create_folder/delete/move/rename/duplicate/import） |

### Batch (バッチ操作)
| ツール | 説明 |
|--------|------|
| `batch_modify` | セレクタにマッチする複数オブジェクトを一括変更 |
| `set_active` | セレクタにマッチするオブジェクトのActive状態を一括設定 |
| `destroy_objects` | セレクタにマッチする複数オブジェクトを一括削除 |
| `batch_execute` | 複数ツールを順次実行（atomic/rollback対応） |

### Package (パッケージ管理)
| ツール | 説明 |
|--------|------|
| `manage_packages` | パッケージの一覧・検索・情報取得 |
| `install_package` | パッケージのインストール（Registry/Git/ローカル/.unitypackage） |
| `uninstall_package` | パッケージのアンインストール |

### UI (UIオーサリング)
| ツール | 説明 |
|--------|------|
| `create_ui_element` | UIエレメントの作成（Button, Text, Image, Panel等） |
| `manage_canvas` | Canvasの作成・設定管理 |

### Camera / Light / Audio / Animation
| ツール | 説明 |
|--------|------|
| `control_camera` | カメラの制御・設定 |
| `setup_cinemachine` | Cinemachineのセットアップ |
| `manage_light` | ライトの管理 |
| `configure_lighting` | ライティング設定 |
| `manage_audio_source` | AudioSourceの管理 |
| `play_audio` | オーディオ再生 |
| `control_animator` | Animatorの制御 |
| `manage_animation_clip` | AnimationClipの管理 |

### Editor (エディタ操作・リフレクション)
| ツール | 説明 |
|--------|------|
| `editor_operations` | Undo/Redo/Selection管理（undo/redo/get_selection/set_selection/get_undo_history） |
| `unity_reflect` | リフレクションAPI（get_type/search_type/get_member/invoke_method） |

### Diagnostics (診断)
| ツール | 説明 |
|--------|------|
| `diagnose_materials` | マテリアル問題の診断 |
| `repair_materials` | マテリアル問題の修復 |
| `mcp_status` | MCPサーバーのステータス・ツール一覧 |
| `configure_editor` | エディタ設定の最適化 |
| `setup_status` | セットアップ状態の確認 |
| `reconfigure_client` | クライアント設定の再構成 |

### Meta (メタツール)
| ツール | 説明 |
|--------|------|
| `list_tools` | 利用可能なツール一覧を取得 |
| `list_categories` | ツールカテゴリ一覧を取得 |
| `execute` | ツール名を指定して動的実行 |

## 基本ワークフロー

### 1. 接続確認
```json
{"name": "mcp_status", "arguments": {"include_tool_list": false}}
```

### 2. シーン確認
```json
{"name": "get_scene_hierarchy", "arguments": {"include_inactive": true}}
```

### 3. オブジェクト検索・操作
```json
{"name": "query_objects", "arguments": {"selector": "#Player", "include_details": true}}
{"name": "modify_game_object", "arguments": {"object_path": "Player", "position": {"x": 0, "y": 1, "z": 0}}}
```

### 4. テスト実行
**Play Modeは必ず status → 分岐 → play の順で呼ぶこと**（既にPlay中なら何もしない）:

```json
// Step 1: 現在状態を取得
{"name": "manage_playmode", "arguments": {"action": "status"}}
// 結果の "Is Playing" が False のときだけ次を呼ぶ
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true, "auto_compile": true}}
{"name": "capture_screenshot", "arguments": {"view": "game"}}
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

**UIキャプチャの注意**: UI Toolkit の runtime panel は `capture_screenshot` には映らない。その場合は:

```json
{"name": "execute_csharp", "arguments": {
  "code": "UnityEngine.ScreenCapture.CaptureScreenshot(\"Temp/shot.png\"); return \"ok\";",
  "force_roslyn": true
}}
// → Temp/shot.png をホスト側で読む
```

## ドメインリロードへの対処

Unityはスクリプトコンパイル時にドメインリロードを行い、MCPサーバーが3-10秒間停止する。

**対処法:**
1. `/health` が応答するまで待ち、`mcp_status` か `unity://editor/state` で状態を読んでから続ける（応答が途切れた変更操作は反映済みのことがあるので、無条件に再送しない）
2. `mcp_status` で接続を確認してから次の操作
3. `configure_editor` で `optimize_for_mcp` を実行しておく

## Unity のコードを書く・直すとき

`execute_csharp` のコードや、利用者のプロジェクトに書くスクリプトは、対象の Unity 版の公式 API に合わせる。
古い手本をそのまま写すと、非推奨 API や毎フレームの無駄が利用者のプロジェクトに入る。

- **検索**: `FindObjectOfType` / `FindObjectsOfType` は Unity 6 で非推奨。一つなら `FindAnyObjectByType<T>()`、
  複数なら `FindObjectsByType<T>(FindObjectsSortMode.None)`。`FindFirstObjectByType` と並び順の指定は
  6000.5〜6000.6 で非推奨になるので、新しいコードで順序に依存しない。
  （[FindObjectOfType](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindObjectOfType.html)、
  [FindObjectsByType](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindObjectsByType.html)）
- **毎フレームの処理**: `Update` で検索・`GetComponent`・`Camera.main`・LINQ・文字列連結をしない。参照は `Awake` / `OnEnable`
  で取ってキャッシュし、毎フレームのマネージド割り当てを 0 に近づける。高頻度のコールバックに渡すデリゲートも使い回す。
  （[Garbage collector overview](https://docs.unity3d.com/6000.3/Documentation/Manual/performance-garbage-collector.html)）
- **static 状態**: Domain Reload を無効にした Play では static フィールドとイベント購読が残る。
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` でリセットする。
  （[Enter Play mode with domain reload disabled](https://docs.unity3d.com/6000.3/Documentation/Manual/domain-reloading.html)）
- **非同期**: `Awaitable` は同じインスタンスを複数回 `await` しない。破棄で止める処理は `destroyCancellationToken` を使う。
  （[Awaitable](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.html)）
- **書いた後**: `compile_project` を `include_warnings: true` で呼び、変更したファイルに `CS0618`（非推奨）が増えていないか確かめる。

## レシピ

- [シーン操作](recipes/scene-operations.md) -- シーンの構築・オブジェクト操作の定型パターン
- [E2Eテスト自動化](recipes/e2e-testing.md) -- Playwright風のE2Eテスト手順
- [Visual Regression Testing](recipes/visual-regression.md) -- スクリーンショット比較による回帰テスト
- [パフォーマンステスト](recipes/performance-testing.md) -- Performance Budgetの設定と検証
- [コード実行](recipes/code-execution.md) -- C#コード動的実行のパターン集
- [入力シミュレーション](recipes/input-simulation.md) -- キーボード・マウス・UI操作の自動化
- [トラブルシューティング](recipes/troubleshooting.md) -- よくある問題と解決法
- [AI UIデザインワークフロー](recipes/ai-ui-design-workflow.md) -- Gemini + Nano Banana で素材生成→Unity UI構築
- [ゲーム構築チュートリアル](recipes/build-game-tutorial.md) -- Coin CollectorミニゲームをAIだけで構築

## リファレンス

- [Scene Tools](references/scene-tools.md) -- シーン操作ツールの完全リファレンス
- [Query Tools](references/query-tools.md) -- 検索・セレクタツールの完全リファレンス
- [Code Tools](references/code-tools.md) -- コード実行・コンパイルツールの完全リファレンス
- [Testing Tools](references/testing-tools.md) -- テスト実行・アサーションツールの完全リファレンス
- [Performance Tools](references/performance-tools.md) -- パフォーマンス分析ツールの完全リファレンス
- [Batch Tools](references/batch-tools.md) -- バッチ操作ツールの完全リファレンス
- [Package Tools](references/package-tools.md) -- パッケージ管理ツールの完全リファレンス
- [Diagnostics Tools](references/diagnostics-tools.md) -- 診断・設定ツールの完全リファレンス
- [U-Selector構文](references/u-selector-syntax.md) -- U-Selectorの完全構文リファレンス
- [Asset Management Tools](references/asset-management-tools.md) -- manage_prefabs + manage_assets の完全リファレンス
- [Editor Tools](references/editor-tools.md) -- unity_reflect + editor_operations + batch_execute の完全リファレンス

## エラー対応

### MCPサーバーに接続できない
1. Unity Editorが起動しているか確認
2. `Window > Unity MCP > Server` でサーバーが起動しているか確認
3. `Window > Unity MCP Server` の番号と `.mcp.json` の番号が一致するか確認（ポートはプロジェクトごとに 51726〜51825 から決まる）

### ドメインリロードで接続が切れる
1. `/health` が応答するまで待ち、`mcp_status` か `unity://editor/state` で状態を読んでから続ける（応答が途切れた変更操作は反映済みのことがあるので、無条件に再送しない）
2. `mcp_status` で接続確認
3. `configure_editor` の `optimize_for_mcp` を実行

### Play Mode遷移後に接続が切れる
1. **先に `action: status` で `Is Playing` を確認**し、既にPlay中なら何もしない
2. `Is Playing: False` のときだけ `manage_playmode` の `wait_for_ready` オプションを使用
3. Play Mode開始後は5-10秒待機
4. `mcp_status` で接続確認してから操作

### Play Modeの呼び出しで応答が返ってこない（クライアント側でstackする）
原因: 既にPlay Mode中に `wait_for_ready: true` 付きで再度 `play` を呼んだ。サーバが両フラグ成立待ちでポーリングに入り、HTTP層のタイムアウトを踏む。

対処:
1. `action: status` を必ず先に呼ぶ（上記）
2. 手動でUnityがPlay起動済みの可能性を常に考慮する

### バックグラウンドで応答しない
`configure_editor` の `enable_background` で解決:
```json
{"name": "configure_editor", "arguments": {"action": "enable_background"}}
```
