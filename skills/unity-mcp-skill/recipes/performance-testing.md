# パフォーマンステスト レシピ

## 概要

Unity MCPのパフォーマンスツールを使って、Performance Budgetの設定と検証を行う。

## Performance Budget チェック

### 1. メモリ使用量チェック

```json
{"name": "analyze_memory", "arguments": {
  "include_asset_breakdown": true,
  "include_top_consumers": true,
  "top_count": 10
}}
```

**判定基準例:**
- テクスチャメモリ < 200MB
- メッシュメモリ < 100MB
- Monoヒープ < 50MB
- 総メモリ < 1GB

### 2. フレーム分析チェック

```json
{"name": "analyze_frame", "arguments": {
  "enable_frame_debugger": true,
  "include_recommendations": true
}}
```

**判定基準例:**
- ドローコール < 500（モバイル: < 200）
- SetPassコール < 100
- 三角形数 < 500,000（モバイル: < 100,000）

### 3. CPU プロファイリング

```json
{"name": "profile_cpu", "arguments": {
  "markers": ["common"],
  "include_gc": true,
  "show_system_stats": true
}}
```

### 4. フレームタイミング

```json
{"name": "get_profiler_stats", "arguments": {"include_frame_timing": true}}
```

**判定基準例:**
- CPU Frame Time < 16.67ms (60 FPS)
- GPU Frame Time < 16.67ms (60 FPS)
- GC Alloc < 1KB/frame

## Play Mode でのパフォーマンス検証

> **Play Modeは status 先読みを必ず挟むこと**（既にPlay中に `wait_for_ready: true` を打つとクライアント側が詰まる）

### フルフロー

```
Step 0: 現在状態を取得
{"name": "manage_playmode", "arguments": {"action": "status"}}

Step 1: Play Mode開始（"Is Playing": False のときだけ）
{"name": "manage_playmode", "arguments": {"action": "play", "wait_for_ready": true}}

Step 2: ゲームプレイを待機（10フレーム分ステップ実行）
{"name": "manage_playmode", "arguments": {"action": "pause"}}

Step 3: メモリ分析
{"name": "analyze_memory", "arguments": {"include_top_consumers": true, "top_count": 5}}

Step 4: フレーム分析
{"name": "analyze_frame", "arguments": {"include_recommendations": true}}

Step 5: CPU プロファイリング
{"name": "profile_cpu", "arguments": {"markers": ["common"], "include_gc": true}}

Step 6: Play Mode終了
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

## シーン比較

### 最適化前の計測
```json
{"name": "analyze_memory", "arguments": {"include_asset_breakdown": true}}
{"name": "analyze_frame", "arguments": {"include_recommendations": true}}
```

### 最適化の実行（例: 不要オブジェクトの削除）
```json
{"name": "destroy_objects", "arguments": {
  "selector": "[tag=Debug]",
  "confirm": true
}}
```

### 最適化後の計測
```json
{"name": "analyze_memory", "arguments": {"include_asset_breakdown": true}}
{"name": "analyze_frame", "arguments": {"include_recommendations": true}}
```

## マテリアル最適化

### マテリアル問題の診断
```json
{"name": "diagnose_materials", "arguments": {}}
```

### 問題のあるマテリアルを修復
```json
{"name": "repair_materials", "arguments": {}}
```

## アセットサイズチェック

### 大きなテクスチャを検索
```json
{"name": "search_assets", "arguments": {
  "query": "*",
  "type_filter": "texture",
  "max_results": 20
}}
```

### 大きなメッシュを検索
```json
{"name": "search_assets", "arguments": {
  "query": "*",
  "type_filter": "model",
  "max_results": 20
}}
```

## 自動パフォーマンスレポート（C#コード実行）

```json
{"name": "execute_csharp", "arguments": {
  "code": "var totalMem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(); var monoUsed = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(); var gfxMem = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver(); return $\"Memory Report:\\n- Total: {totalMem / 1048576} MB\\n- Mono: {monoUsed / 1048576} MB\\n- GFX: {gfxMem / 1048576} MB\";"
}}
```
