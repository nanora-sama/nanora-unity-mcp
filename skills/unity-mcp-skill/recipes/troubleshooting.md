# トラブルシューティング

## 接続問題

### MCPサーバーに接続できない

**症状:** `Connection refused` または `timeout`

**チェックリスト:**
1. Unity Editorが起動しているか
2. MCP Serverが有効か: `Window > Unity MCP > Server`
3. ポート確認: `Window > Unity MCP Server` の番号と `.mcp.json` の番号が一致するか（プロジェクトごとに 51726〜51825 から決まる）

**確認コマンド:**
```json
{"name": "mcp_status", "arguments": {"include_tool_list": false}}
```

### ドメインリロード後に接続が切れる

**症状:** コンパイルやPlay Mode遷移後にエラー

**原因:** Unityのドメインリロードにより、MCPサーバーが再起動される（3-10秒）

**対処:**
1. `/health` が応答するまで待ち、`mcp_status` か `unity://editor/state` で状態を読んでから続ける（応答が途切れた変更操作は反映済みのことがあるので、無条件に再送しない）
2. `mcp_status` で接続確認

### バックグラウンドで応答しない

**症状:** Unity Editorからフォーカスが外れると応答しなくなる

**対処:**
```json
{"name": "configure_editor", "arguments": {"action": "enable_background"}}
```

## Play Mode の問題

### Play Mode開始後に接続が切れる

**原因:** Play Mode遷移でドメインリロードが発生

**対処:** **必ず status で現在状態を確認してから** play を発射する:
```json
{"name": "manage_playmode", "arguments": {"action": "status"}}
// "Is Playing": False のときだけ play
{"name": "manage_playmode", "arguments": {
  "action": "play",
  "wait_for_ready": true,
  "timeout_seconds": 30
}}
```

### Play Modeコール後、応答が返らずクライアント側でstackする

**症状:** `{"action":"play","wait_for_ready":true}` を呼んだあと、ツールが無応答になりClaude Code側がタイムアウト/詰まり扱いになる。

**原因:** 既にPlay Mode中のプロジェクトに対して play を再発射した。サーバ側は内部で両フラグ(`EditorApplication.isPlaying && Application.isPlaying`)成立待ちポーリングに入り、HTTP/SSE層のタイムアウトが先に切れる。

**対処:**
1. `action: status` を事前に必ず挟む運用に統一
2. ユーザーが手動でPlay起動している可能性を常に考慮
3. すでにPlay中なら何もしない（stop→play の再起動は明示指示されたときのみ）

### Play Mode中にコンパイルが必要

**手順:**
1. Play Mode終了
2. コンパイル実行
3. 接続確認
4. Play Mode再開（status先読み付き）

```json
{"name": "manage_playmode", "arguments": {"action": "stop"}}
{"name": "compile_project", "arguments": {"timeout_seconds": 60}}
{"name": "mcp_status", "arguments": {"include_tool_list": false}}
{"name": "manage_playmode", "arguments": {"action": "status"}}
// "Is Playing": False のときだけ
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}
```

## コンパイルの問題

### コンパイルエラーの確認

```json
{"name": "get_compilation_errors", "arguments": {
  "include_warnings": true,
  "include_source_context": true
}}
```

### 特定ファイルのエラーだけ確認

```json
{"name": "get_compilation_errors", "arguments": {
  "file_filter": "PlayerController",
  "include_source_context": true
}}
```

### コンパイルを強制実行

```json
{"name": "compile_project", "arguments": {"timeout_seconds": 60}}
```

## コード実行の問題

### Roslynコンパイルエラー

**症状:** `Roslyn compilation failed`

**対処:** `force_roslyn: true` でエラーメッセージを確認:
```json
{"name": "execute_csharp", "arguments": {
  "code": "...",
  "force_roslyn": true
}}
```

### セキュリティ違反

**症状:** `Security violation: Forbidden API call`

**原因:** 禁止APIの使用（ファイル書き込み、プロセス起動等）

**対処:** 別の方法で実現するか、制限を理解して回避

### タイムアウト

**症状:** `Execution failed: timeout`

**対処:** `timeout_ms` を延長:
```json
{"name": "execute_csharp", "arguments": {
  "code": "...",
  "timeout_ms": 30000
}}
```

## セレクタの問題

### オブジェクトが見つからない

**症状:** `No object found for selector`

**チェックリスト:**
1. オブジェクト名のスペル
2. 非アクティブオブジェクトには `include_inactive: true`
3. パスセレクタの場合、完全なパスを指定

```json
{"name": "query_objects", "arguments": {
  "selector": "#Player",
  "include_inactive": true,
  "include_details": true
}}
```

### 複数オブジェクトがマッチ

**症状:** `Selector matched N objects, expected exactly one`

**対処:** より具体的なセレクタを使用:
- パスセレクタ: `/Canvas/Menu/Button`
- 属性フィルタ: `#Button[tag=MainButton]`
- ワイルドカード: `#Enemy_01`（`#Enemy*` ではなく）

## パフォーマンスの問題

### ProfilerRecorderの値が全て0

**原因:** ProfilerRecorderは複数フレームのデータ蓄積が必要

**対処:**
1. Play Mode中に実行する
2. `get_profiler_stats` でFrameTimingManagerデータを使う
3. `analyze_frame` でドローコール分析を使う

### メモリ分析が不正確

**対処:** Play Mode中に `analyze_memory` を実行すると、ランタイムアセットも含まれる

## コンソールログの問題

### ログが取得できない

**確認:**
```json
{"name": "get_console_logs", "arguments": {"count": 10, "log_type": "All"}}
```

### エラーログのみ取得
```json
{"name": "get_console_logs", "arguments": {"count": 50, "log_type": "Error"}}
```

### コンソールをクリア
```json
{"name": "get_console_logs", "arguments": {"clear": true}}
```

## テストの問題

### テストがタイムアウト

```json
{"name": "run_tests", "arguments": {
  "test_mode": "EditMode",
  "timeout_seconds": 600,
  "run_synchronously": true
}}
```

### テスト一覧の取得

```json
{"name": "get_test_results", "arguments": {"include_all_tests": true}}
```

## 一般的なデバッグ手順

1. **接続確認**: `mcp_status`
2. **コンソールログ確認**: `get_console_logs` (Error)
3. **コンパイル状態確認**: `get_compilation_errors`
4. **シーン状態確認**: `get_scene_hierarchy`
5. **Play Mode状態確認**: `manage_playmode` (status)
