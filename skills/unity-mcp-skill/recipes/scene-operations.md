# シーン操作レシピ

## シーンの確認

### ヒエラルキーを取得
```json
{"name": "get_scene_hierarchy", "arguments": {}}
```

### 非アクティブオブジェクトも含めて取得
```json
{"name": "get_scene_hierarchy", "arguments": {"include_inactive": true, "max_depth": 3}}
```

### 現在のシーン情報を取得
```json
{"name": "manage_scene", "arguments": {"action": "info"}}
```

## オブジェクト検索

### 名前で検索
```json
{"name": "query_objects", "arguments": {"selector": "#Player"}}
```

### コンポーネントで検索
```json
{"name": "query_objects", "arguments": {"selector": ".Camera"}}
```

### パスで検索
```json
{"name": "query_objects", "arguments": {"selector": "/Canvas/MainMenu/StartButton"}}
```

### 属性で検索（HPが50以上のEnemy）
```json
{"name": "query_objects", "arguments": {"selector": ".Enemy[Health>50]"}}
```

### ワイルドカードで検索
```json
{"name": "query_objects", "arguments": {"selector": "#Enemy*"}}
```

### 詳細情報付きで検索
```json
{"name": "query_objects", "arguments": {"selector": "#Player", "include_details": true}}
```

## オブジェクト作成

### 空のGameObject
```json
{"name": "create_game_object", "arguments": {"name": "GameManager"}}
```

### プリミティブ型（Cube）
```json
{"name": "create_game_object", "arguments": {
  "name": "Wall",
  "primitive_type": "Cube",
  "position": {"x": 0, "y": 1, "z": 5},
  "scale": {"x": 10, "y": 2, "z": 0.5}
}}
```

### 親オブジェクトの下に作成
```json
{"name": "create_game_object", "arguments": {
  "name": "ChildObject",
  "parent_path": "ParentObject",
  "position": {"x": 0, "y": 0, "z": 0}
}}
```

## オブジェクト変更

### 位置を変更
```json
{"name": "modify_game_object", "arguments": {
  "object_path": "Player",
  "position": {"x": 10, "y": 0, "z": 5}
}}
```

### 名前とタグを変更
```json
{"name": "modify_game_object", "arguments": {
  "object_path": "Cube",
  "new_name": "Floor",
  "tag": "Ground",
  "layer": "Default"
}}
```

### 非アクティブにする
```json
{"name": "modify_game_object", "arguments": {
  "object_path": "TutorialPanel",
  "active": false
}}
```

### 親を変更
```json
{"name": "modify_game_object", "arguments": {
  "object_path": "Weapon",
  "new_parent_path": "Player/RightHand"
}}
```

## オブジェクト詳細

### コンポーネントとプロパティを全て取得
```json
{"name": "get_object_details", "arguments": {"object_path": "Main Camera"}}
```

## オブジェクト削除

### 単体削除
```json
{"name": "delete_game_object", "arguments": {"object_path": "OldObject"}}
```

## シーン管理

### 新しいシーンを作成
```json
{"name": "manage_scene", "arguments": {"action": "new"}}
```

### シーンを保存
```json
{"name": "manage_scene", "arguments": {"action": "save"}}
```

### シーンを別名で保存
```json
{"name": "manage_scene", "arguments": {"action": "save_as", "path": "Assets/Scenes/Level2.unity"}}
```

### シーンを読み込み
```json
{"name": "manage_scene", "arguments": {"action": "load", "path": "Assets/Scenes/MainMenu.unity"}}
```

## プレハブのインスタンス化

### 基本的なインスタンス化
```json
{"name": "instantiate_asset", "arguments": {
  "asset_path": "Assets/Prefabs/Enemy.prefab",
  "position": {"x": 5, "y": 0, "z": 10}
}}
```

### 床に配置
```json
{"name": "instantiate_asset", "arguments": {
  "asset_path": "Assets/Prefabs/Tree.prefab",
  "position": {"x": 10, "y": 100, "z": 10},
  "place_on_floor": true
}}
```

### 親オブジェクトの下にインスタンス化
```json
{"name": "instantiate_asset", "arguments": {
  "asset_path": "Assets/Prefabs/Bullet.prefab",
  "parent_selector": "#Player",
  "instance_name": "ActiveBullet"
}}
```

## よくあるシーン構築パターン

### ステージの基本セットアップ
```
1. manage_scene → action: "new"
2. create_game_object → "Floor" (Plane, scale 10x1x10)
3. create_game_object → "Player" (Capsule, position 0,1,0)
4. modify_game_object → Main Camera, position 0,5,-10
5. manage_scene → action: "save_as", path: "Assets/Scenes/Level1.unity"
```

### UIの基本セットアップ
```
1. manage_canvas → action: "create"
2. create_ui_element → "ScoreText" (text)
3. create_ui_element → "HealthBar" (slider)
4. create_ui_element → "StartButton" (button)
```
