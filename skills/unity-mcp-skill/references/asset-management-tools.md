# Asset Management Tools Reference

## manage_prefabs

Prefabの作成・適用・アンパック・保存・バリアント作成・リバート・情報取得を行う。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | - | `create`, `apply`, `unpack`, `save`, `create_variant`, `revert`, `get_info` |
| selector | string | Yes | - | U-Selector（対象GameObjectを指定） |
| path | string | No | - | Prefab保存パス（`create`/`create_variant`で必須。例: `Assets/Prefabs/MyPrefab.prefab`） |
| unpack_mode | string | No | `root` | `unpack`時のモード: `root`（最外層のみ）または `completely`（ネスト含め全て） |

### Actions

#### create -- GameObjectからPrefabを作成

`path`が必須。指定パスにPrefabアセットを保存する。ディレクトリが存在しない場合は自動作成。

```json
{"name": "manage_prefabs", "arguments": {
  "action": "create",
  "selector": "#Player",
  "path": "Assets/Prefabs/Player.prefab"
}}
```

**Response:**
```
Created prefab from 'Player'
- Path: Assets/Prefabs/Player.prefab
- GUID: a1b2c3d4e5f6...
```

#### apply -- Prefabインスタンスのオーバーライドを適用

シーン上のPrefabインスタンスの変更をソースPrefabアセットに反映する。

```json
{"name": "manage_prefabs", "arguments": {
  "action": "apply",
  "selector": "#Player"
}}
```

#### unpack -- Prefabインスタンスを通常GameObjectに変換

```json
{"name": "manage_prefabs", "arguments": {
  "action": "unpack",
  "selector": "#Player",
  "unpack_mode": "completely"
}}
```

#### save -- Prefabインスタンスの変更を保存

`apply`と同等。インスタンスのオーバーライドをソースPrefabに保存する。

```json
{"name": "manage_prefabs", "arguments": {
  "action": "save",
  "selector": "#Player"
}}
```

#### create_variant -- Prefabバリアントを作成

既存のPrefabインスタンスからバリアントを作成。`path`が必須。

```json
{"name": "manage_prefabs", "arguments": {
  "action": "create_variant",
  "selector": "#Player",
  "path": "Assets/Prefabs/Player_Red.prefab"
}}
```

#### revert -- Prefabインスタンスのオーバーライドを全て元に戻す

```json
{"name": "manage_prefabs", "arguments": {
  "action": "revert",
  "selector": "#Player"
}}
```

#### get_info -- Prefab情報を取得

Prefab種別、インスタンス状態、ソースパス、オーバーライド一覧を返す。

```json
{"name": "manage_prefabs", "arguments": {
  "action": "get_info",
  "selector": "#Player"
}}
```

**Response:**
```
=== Prefab Info: Player ===
Is Prefab Instance: True
Is Prefab Asset: False
Prefab Type: Regular
Instance Status: Connected
Source Prefab: Assets/Prefabs/Player.prefab
Outermost Root: Player

--- Overrides ---
Property overrides: 2
Added components: 1
Removed components: 0
Added GameObjects: 0
```

---

## manage_assets

プロジェクト内アセットのCRUD操作を行う。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | - | `get_info`, `create_folder`, `delete`, `move`, `rename`, `duplicate`, `import` |
| path | string | No | - | アセットパス（例: `Assets/Materials/MyMat.mat`） |
| new_path | string | No | - | `move`の移動先パス、`rename`の新しい名前 |
| parent_folder | string | No | - | `create_folder`時の親フォルダ（例: `Assets/Textures`） |
| folder_name | string | No | - | `create_folder`時の新フォルダ名 |
| source_path | string | No | - | `import`時の外部ファイルパス |

### Actions

#### get_info -- アセット詳細情報を取得

パス、GUID、型、ラベル、ファイルサイズ、依存関係、サブアセット、Importer情報を返す。
フォルダの場合は直下の子要素数と再帰的な総アセット数を返す。

```json
{"name": "manage_assets", "arguments": {
  "action": "get_info",
  "path": "Assets/Materials/PlayerMat.mat"
}}
```

**Response:**
```
=== Asset Info ===
Path: Assets/Materials/PlayerMat.mat
GUID: abc123def456
Type: Material
Name: PlayerMat
Hide Flags: None
Is Folder: False
File Size: 1.23 KB
Last Modified: 2026-03-28 10:30:00
Importer: NativeFormatImporter
```

#### create_folder -- フォルダを作成

`parent_folder`と`folder_name`が必須。

```json
{"name": "manage_assets", "arguments": {
  "action": "create_folder",
  "parent_folder": "Assets",
  "folder_name": "Prefabs"
}}
```

#### delete -- アセットを削除

`Assets`や`Packages`ルートフォルダの削除は安全のため拒否される。

```json
{"name": "manage_assets", "arguments": {
  "action": "delete",
  "path": "Assets/OldTexture.png"
}}
```

#### move -- アセットを移動

```json
{"name": "manage_assets", "arguments": {
  "action": "move",
  "path": "Assets/Textures/Icon.png",
  "new_path": "Assets/UI/Textures/Icon.png"
}}
```

#### rename -- アセットをリネーム

`new_path`に新しい名前（拡張子なし）を指定する。

```json
{"name": "manage_assets", "arguments": {
  "action": "rename",
  "path": "Assets/Materials/Mat_01.mat",
  "new_path": "PlayerMaterial"
}}
```

#### duplicate -- アセットを複製

`new_path`を省略すると`_copy`サフィックスで自動生成。

```json
{"name": "manage_assets", "arguments": {
  "action": "duplicate",
  "path": "Assets/Prefabs/Enemy.prefab",
  "new_path": "Assets/Prefabs/Enemy_Boss.prefab"
}}
```

#### import -- 外部ファイルをプロジェクトにインポート

`source_path`（外部ファイル）と`path`（インポート先）が必須。

```json
{"name": "manage_assets", "arguments": {
  "action": "import",
  "source_path": "/Users/dev/textures/icon.png",
  "path": "Assets/Textures/icon.png"
}}
```
