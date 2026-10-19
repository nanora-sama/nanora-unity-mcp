# Scene Tools Reference

## get_scene_hierarchy

シーン内の全GameObjectのヒエラルキーを取得する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| include_inactive | boolean | No | true | 非アクティブなGameObjectを含めるか |
| max_depth | integer | No | 0 | 走査する最大深度（0 = 無制限） |

### Example

```json
{"name": "get_scene_hierarchy", "arguments": {"include_inactive": true, "max_depth": 3}}
```

### Response

```
Scene: SampleScene
Path: Assets/Scenes/SampleScene.unity
Root GameObjects: 4

- Main Camera (Transform, Camera, AudioListener)
- Directional Light (Transform, Light)
- Player [prefab] (Transform, MeshRenderer, MeshFilter, BoxCollider)
  - Weapon (Transform, MeshRenderer)
- Canvas (Transform, Canvas, CanvasScaler, GraphicRaycaster)
  - ScoreText (Transform, Text)
```

---

## get_object_details

GameObjectの詳細情報（全コンポーネント・SerializedPropertyを含む）を取得する。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| object_name | string | No | GameObjectの名前（object_pathがなければこちらを使用） |
| object_path | string | No | GameObjectのフルパス（例: `Parent/Child/Target`）。object_nameより優先 |

### Example

```json
{"name": "get_object_details", "arguments": {"object_path": "Main Camera"}}
```

### Response

```
GameObject: Main Camera
Active: True (in hierarchy: True)
Layer: Default (0)
Tag: MainCamera
Static: False

Transform:
  Position: (0.0, 1.0, -10.0)
  Local Position: (0.0, 1.0, -10.0)
  Rotation: (0.0, 0.0, 0.0)
  Local Rotation: (0.0, 0.0, 0.0)
  Scale: (1.0, 1.0, 1.0)
  Parent: none
  Child Count: 0

Components:
  - Transform
      Position: (0.00, 1.00, -10.00)
  - Camera
      Clear Flags: Skybox
      Background: RGBA(0.192, 0.302, 0.475, 0.000)
      Field of View: 60
      Near Clip Plane: 0.3
      Far Clip Plane: 1000
```

---

## create_game_object

新しいGameObjectをシーンに作成する。プリミティブ型・位置・親の指定が可能。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| name | string | Yes | "New GameObject" | GameObjectの名前 |
| primitive_type | string | No | "Empty" | プリミティブ型。`Cube`, `Sphere`, `Capsule`, `Cylinder`, `Plane`, `Quad`, `Empty` |
| parent_path | string | No | - | 親GameObjectのパス |
| position | object | No | {x:0, y:0, z:0} | 初期位置 `{x, y, z}` |
| rotation | object | No | {x:0, y:0, z:0} | 初期回転（オイラー角） `{x, y, z}` |
| scale | object | No | {x:1, y:1, z:1} | 初期スケール `{x, y, z}` |

### Example

```json
{"name": "create_game_object", "arguments": {
  "name": "Wall",
  "primitive_type": "Cube",
  "position": {"x": 0, "y": 1, "z": 5},
  "scale": {"x": 10, "y": 2, "z": 0.5}
}}
```

### Response

```
Created GameObject 'Wall' at position (0.0, 1.0, 5.0)
```

---

## modify_game_object

既存のGameObjectのTransform・名前・Active状態・レイヤー・タグを変更する。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| object_path | string | Yes | 変更対象のGameObjectパス |
| new_name | string | No | 新しい名前 |
| active | boolean | No | Active状態 |
| position | object | No | 新しいローカル位置 `{x, y, z}` |
| rotation | object | No | 新しいローカル回転（オイラー角） `{x, y, z}` |
| scale | object | No | 新しいローカルスケール `{x, y, z}` |
| new_parent_path | string | No | 新しい親のパス（空文字でルートに移動） |
| layer | string | No | レイヤー名 |
| tag | string | No | タグ |

### Example

```json
{"name": "modify_game_object", "arguments": {
  "object_path": "Player",
  "position": {"x": 10, "y": 0, "z": 5},
  "tag": "Player"
}}
```

### Response

```
Modified GameObject: Player
  Position -> (10.0, 0.0, 5.0)
  Tag -> Player
```

---

## delete_game_object

GameObjectをシーンから削除する。Undo対応。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| object_path | string | Yes | 削除対象のGameObjectパス |

### Example

```json
{"name": "delete_game_object", "arguments": {"object_path": "OldObject"}}
```

### Response

```
Deleted GameObject 'OldObject' (including 3 children)
```

---

## manage_scene

シーンの新規作成・保存・読み込み・情報取得を行う。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | - | `new`, `save`, `save_as`, `load`, `info` |
| path | string | No | - | シーンパス（`save_as`, `load` で必要） |
| save_current | boolean | No | true | 読み込み前に現在のシーンを保存するか |

### Example

```json
{"name": "manage_scene", "arguments": {"action": "load", "path": "Assets/Scenes/Level2.unity"}}
```

### Response

```
Loaded scene: Level2
Path: Assets/Scenes/Level2.unity
Root objects: 8
```
