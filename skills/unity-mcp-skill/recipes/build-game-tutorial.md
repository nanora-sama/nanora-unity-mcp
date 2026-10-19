# Recipe: Coin Collector ミニゲーム構築

AIとの対話だけでUnityゲームを構築するステップバイステップレシピ。
コードもオブジェクト配置も全てMCPツール経由で実行する。

## 前提条件
- Unity EditorでMCPサーバーが起動済み
- `configure_editor` の `optimize_for_mcp` 実行済み

## Step 1: 地面を作る

```json
{"name": "create_game_object", "arguments": {
  "name": "Ground", "primitive_type": "Plane",
  "position": {"x": 0, "y": 0, "z": 0},
  "scale": {"x": 3, "y": 1, "z": 3}
}}
```

マテリアルを緑に変更:
```json
{"name": "execute_csharp", "arguments": {
  "code": "var ground = GameObject.Find(\"Ground\"); var mat = new Material(Shader.Find(\"Standard\")); mat.color = new Color(0.2f, 0.7f, 0.3f); ground.GetComponent<Renderer>().material = mat; return \"done\";"
}}
```

## Step 2: プレイヤーを配置

```json
{"name": "create_game_object", "arguments": {
  "name": "Player", "primitive_type": "Capsule",
  "position": {"x": 0, "y": 1, "z": 0}
}}
```

青マテリアルを適用:
```json
{"name": "execute_csharp", "arguments": {
  "code": "var player = GameObject.Find(\"Player\"); var mat = new Material(Shader.Find(\"Standard\")); mat.color = new Color(0.2f, 0.4f, 0.9f); player.GetComponent<Renderer>().material = mat; return \"done\";"
}}
```

## Step 3: コインを配置

Sphere x5を異なる座標に配置し、金色マテリアルを一括適用:
```json
{"name": "execute_csharp", "arguments": {
  "code": "var gold = new Material(Shader.Find(\"Standard\")); gold.color = new Color(1f, 0.84f, 0f); gold.SetFloat(\"_Metallic\", 0.8f); for(int i=0;i<5;i++) { var c = GameObject.Find(\"Coin_\"+i); if(c!=null) c.GetComponent<Renderer>().material = gold; } return \"done\";"
}}
```

## Step 4: 壁を batch_execute で一括作成

```json
{"name": "batch_execute", "arguments": {
  "commands": [
    {"tool": "create_game_object", "arguments": {"name": "Wall_North", "primitive_type": "Cube", "position": {"x": 0, "y": 1, "z": 15}, "scale": {"x": 30, "y": 2, "z": 0.5}}},
    {"tool": "create_game_object", "arguments": {"name": "Wall_South", "primitive_type": "Cube", "position": {"x": 0, "y": 1, "z": -15}, "scale": {"x": 30, "y": 2, "z": 0.5}}},
    {"tool": "create_game_object", "arguments": {"name": "Wall_East", "primitive_type": "Cube", "position": {"x": 15, "y": 1, "z": 0}, "scale": {"x": 0.5, "y": 2, "z": 30}}},
    {"tool": "create_game_object", "arguments": {"name": "Wall_West", "primitive_type": "Cube", "position": {"x": -15, "y": 1, "z": 0}, "scale": {"x": 0.5, "y": 2, "z": 30}}}
  ],
  "atomic": false
}}
```

> **Point**: `batch_execute` で4つのオブジェクトを1回のAPI呼び出しで作成。`atomic: false` は個々のUndoを保持する場合に使用。

## Step 5: ゲームロジックを追加

コインをトリガーに設定:
```json
{"name": "execute_csharp", "arguments": {
  "code": "for(int i=0;i<5;i++) { var coin = GameObject.Find(\"Coin_\"+i); if(coin!=null) { coin.GetComponent<Collider>().isTrigger = true; coin.AddComponent<Rigidbody>().isKinematic = true; } } return \"Coins configured\";"
}}
```

## Step 6: Play Modeでテスト

> **先に status 確認を挟むこと**（既にPlay中だと `wait_for_ready: true` で応答が詰まる）

```json
{"name": "manage_playmode", "arguments": {"action": "status"}}
// "Is Playing" が False のときだけ次を実行
{"name": "manage_playmode", "arguments": {
  "action": "play", "auto_compile": true, "wait_for_ready": true
}}
```

スクリーンショットで確認:
```json
{"name": "capture_screenshot", "arguments": {"view": "game"}}
```

テスト後に停止:
```json
{"name": "manage_playmode", "arguments": {"action": "stop"}}
```

## Step 7: パフォーマンス分析

```json
{"name": "analyze_memory", "arguments": {}}
```

## Step 8: シーン保存

```json
{"name": "manage_scene", "arguments": {
  "action": "save_as",
  "path": "Assets/Scenes/CoinCollector.unity"
}}
```

## 使用ツール一覧

| ツール | 用途 |
|--------|------|
| `create_game_object` | プリミティブ作成 |
| `execute_csharp` | マテリアル適用、スクリプト操作 |
| `batch_execute` | 壁・障害物の一括作成 |
| `capture_screenshot` | 各ステップの記録 |
| `manage_playmode` | Play Mode開始/停止 |
| `get_scene_hierarchy` | シーン確認 |
| `analyze_memory` | パフォーマンス分析 |
| `manage_scene` | シーン保存 |
