# Performance Tools Reference

## analyze_frame

フレームレンダリングの分析。ドローコール数・シェーダー使用量・最適化提案を提供。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| enable_frame_debugger | boolean | No | true | Frame Debuggerを有効にしてデータを取得 |
| include_shader_details | boolean | No | false | 詳細なシェーダー情報を含める |
| include_recommendations | boolean | No | true | 最適化提案を含める |

### Example

```json
{"name": "analyze_frame", "arguments": {
  "enable_frame_debugger": true,
  "include_recommendations": true
}}
```

### Response

```
# Frame Analysis Report

## Rendering Statistics
- Total Draw Calls: 234
- Batched Draw Calls: 0
- Set Pass Calls: 45
- Total Triangles: 125,432
- Total Vertices: 67,891

## Render Pipeline
- Current Pipeline: Universal Render Pipeline (URP)
- Color Space: Linear

## Active Cameras
- Main Camera: Skybox, Depth=0, Culling=-1

## Shader Usage
- Universal Render Pipeline/Lit: 120 draw calls
- Universal Render Pipeline/Unlit: 45 draw calls
- UI/Default: 30 draw calls

## Optimization Recommendations
- [MEDIUM] Moderate draw call count (234). Consider enabling batching.
- [MEDIUM] No batched draw calls detected. Static/Dynamic Batching may not be configured.
```

---

## analyze_memory

メモリ使用量の詳細分析。カテゴリ別内訳・トップ消費者・最適化提案。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| include_asset_breakdown | boolean | No | true | アセット種別ごとの内訳 |
| include_top_consumers | boolean | No | true | メモリ消費トップN |
| top_count | integer | No | 10 | トップNの件数 |
| take_snapshot | boolean | No | false | Memory Profilerスナップショット取得 |

### Example

```json
{"name": "analyze_memory", "arguments": {
  "include_asset_breakdown": true,
  "include_top_consumers": true,
  "top_count": 5
}}
```

### Response

```
# Memory Analysis Report

## Memory Overview
- Total Reserved: 512.34 MB
- Total Allocated: 389.12 MB
- Mono Heap: 45.67 MB
- Mono Used: 32.12 MB
- GFX Memory: 128.45 MB

## Asset Memory Breakdown
- Textures: 156.78 MB (234 assets)
- Meshes: 45.23 MB (89 assets)
- Materials: 67 assets
- Shaders: 23 assets
- Audio Clips: 12.34 MB (15 assets)
- Animation Clips: 34 assets
- Render Textures: 32.00 MB (4 assets)

## Top 5 Memory Consumers
1. [Texture2D] MainCharacterDiffuse: 16.00 MB
2. [Texture2D] TerrainSplat: 8.00 MB
3. [RenderTexture] _CameraColorTexture: 7.50 MB
4. [Mesh] TerrainMesh: 4.23 MB
5. [AudioClip] BackgroundMusic: 3.45 MB

## Optimization Recommendations
- Consider enabling texture compression (ASTC for mobile, BC7 for desktop).
- Use texture mipmaps to reduce memory for distant objects.
```

---

## profile_cpu

CPU パフォーマンスプロファイリング。ProfilerRecorder APIを使用。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| markers | array[string] | No | (デフォルト5個) | プロファイラマーカー名。`"common"` で共通マーカー全て |
| sample_count | integer | No | 60 | サンプルフレーム数 |
| include_gc | boolean | No | true | GCアロケーション追跡 |
| show_system_stats | boolean | No | true | システム統計を表示 |

### 共通マーカー一覧（`"common"` で展開）

- `PlayerLoop`
- `Update.ScriptRunBehaviourUpdate`
- `PreLateUpdate.ScriptRunBehaviourLateUpdate`
- `FixedUpdate.ScriptRunBehaviourFixedUpdate`
- `Update.DirectorUpdate`
- `PreLateUpdate.DirectorUpdateAnimationEnd`
- `Render.TransparentGeometry`
- `Render.OpaqueGeometry`
- `Camera.Render`
- `Gfx.WaitForPresentOnGfxThread`
- `Physics.Processing`
- `Physics.Simulate`
- `BehaviourUpdate`
- `CoroutinesDelayedCalls`
- `GUI.Repaint`

### Example

```json
{"name": "profile_cpu", "arguments": {
  "markers": ["common"],
  "include_gc": true,
  "show_system_stats": true
}}
```

### 注意事項

ProfilerRecorderは複数フレームのデータ蓄積が必要。単一リクエストではゼロ値が返る場合がある。
より信頼性の高いデータには `get_profiler_stats` を使用。

---

## get_profiler_stats

プロファイラ統計とパフォーマンスメトリクスを取得する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| include_frame_timing | boolean | No | true | FrameTimingManager統計を含める |

### Example

```json
{"name": "get_profiler_stats", "arguments": {"include_frame_timing": true}}
```

### Response

```
# Profiler Statistics

## Memory Usage
- Total Reserved: 512.34 MB
- Total Allocated: 389.12 MB
- Mono Heap Size: 45.67 MB
- Mono Used Size: 32.12 MB
- Temp Allocator Size: 4.00 MB
- GFX Driver Memory: 128.45 MB

## Rendering Stats
- Max Texture Size: 16384
- Max Cubemap Size: 16384

## Frame Timing
- CPU Frame Time: 8.34 ms
- CPU Main Thread Frame Time: 6.23 ms
- CPU Render Thread Frame Time: 4.12 ms
- GPU Frame Time: 5.67 ms

## Profiler State
- Profiler Enabled: True
- Profiler Supported: True
```
