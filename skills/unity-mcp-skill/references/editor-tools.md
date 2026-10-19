# Editor Tools Reference

## unity_reflect

.NET/Unityの型情報をリフレクションで検査する。型の詳細取得、型名検索、メンバー詳細取得、静的メソッド呼び出しが可能。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | - | `get_type`, `search_type`, `get_member`, `invoke_method` |
| type_name | string | No | - | 完全修飾型名（例: `UnityEngine.GameObject`） |
| search_query | string | No | - | 型名の部分一致検索クエリ（`search_type`用） |
| member_name | string | No | - | メンバー名（`get_member`/`invoke_method`用） |
| method_args | array | No | - | 静的メソッド呼び出しの引数配列（`invoke_method`用） |

### Actions

#### get_type -- 型情報を取得

`type_name`が必須。名前空間、アセンブリ、基底型、インターフェース、属性、publicメンバー一覧を返す。
Enum型の場合は全値を表示。メンバーは最大200件まで表示。

```json
{"name": "unity_reflect", "arguments": {
  "action": "get_type",
  "type_name": "UnityEngine.GameObject"
}}
```

**Response:**
```
Type: UnityEngine.GameObject
Namespace: UnityEngine
Assembly: UnityEngine.CoreModule
IsClass: True
IsInterface: False
BaseType: UnityEngine.Object
Interfaces: ...

Public members (declared, 42 total):
  [Method] static GameObject Find(String name)
  [Method] T GetComponent()
  [Property] String name { get; set }
  ...
```

#### search_type -- 型名を検索

`search_query`が必須。部分一致で全ロード済みアセンブリを検索。最大50件。

```json
{"name": "unity_reflect", "arguments": {
  "action": "search_type",
  "search_query": "Cinemachine"
}}
```

#### get_member -- メンバー詳細を取得

`type_name`と`member_name`が必須。Public/NonPublic/Static/Instanceの全メンバーを検索。
メソッドの場合はパラメータ一覧（デフォルト値含む）を返す。プロパティの場合はget/set可否を返す。

```json
{"name": "unity_reflect", "arguments": {
  "action": "get_member",
  "type_name": "UnityEngine.Transform",
  "member_name": "SetParent"
}}
```

**Response:**
```
Member 'SetParent' on UnityEngine.Transform:

  Kind: Method
  DeclaringType: UnityEngine.Transform
  ReturnType: System.Void
  IsStatic: False
  IsVirtual: False
  Parameters (2):
    Transform parent
    Boolean worldPositionStays = True
```

#### invoke_method -- 静的メソッドを呼び出す

`type_name`と`member_name`が必須。**publicな静的メソッドのみ**呼び出し可能。
引数はJSON配列で渡し、パラメータ型に自動変換される。オプション引数のあるオーバーロードに対応。

```json
{"name": "unity_reflect", "arguments": {
  "action": "invoke_method",
  "type_name": "UnityEngine.Application",
  "member_name": "OpenURL",
  "method_args": ["https://unity.com"]
}}
```

### 注意事項

- `invoke_method`はpublic staticメソッドのみ対象。インスタンスメソッドを呼ぶ場合は`execute_csharp`を使用
- 型検索は全ロード済みアセンブリをスキャンするため、結果が多い場合は検索クエリを絞る

---

## editor_operations

Unity Editorの基本操作（Undo/Redo、Selection管理、Undo履歴）をプログラムから実行する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | - | `undo`, `redo`, `get_selection`, `set_selection`, `get_undo_history` |
| selector | string | No | - | U-Selector（`set_selection`で必須。例: `#MyObject`, `.Camera`, `/Canvas/Button`） |

### Actions

#### undo -- Undo実行

直前の操作を元に戻す。

```json
{"name": "editor_operations", "arguments": {
  "action": "undo"
}}
```

#### redo -- Redo実行

Undoで戻した操作をやり直す。

```json
{"name": "editor_operations", "arguments": {
  "action": "redo"
}}
```

#### get_selection -- 現在の選択を取得

アクティブなGameObjectと全選択オブジェクトのパスを返す。

```json
{"name": "editor_operations", "arguments": {
  "action": "get_selection"
}}
```

**Response:**
```
Active: /Canvas/StartButton [active]
Selected (2):
  /Canvas/StartButton [active]
  /Canvas/SettingsButton
```

#### set_selection -- U-Selectorで選択

`selector`が必須。マッチした全オブジェクトを選択し、最初のオブジェクトをアクティブに設定。

```json
{"name": "editor_operations", "arguments": {
  "action": "set_selection",
  "selector": ".Camera"
}}
```

#### get_undo_history -- Undo履歴を取得

現在のUndoグループ名とインデックスを返す。Unity APIの制約により完全なスタックは取得不可。

```json
{"name": "editor_operations", "arguments": {
  "action": "get_undo_history"
}}
```

---

## batch_execute

複数のMCPツール呼び出しを1リクエストで順次実行する。atomicモードでは全操作を1つのUndoステップにまとめ、失敗時は自動ロールバックする。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| commands | array | Yes | - | 実行するコマンドのリスト |
| atomic | boolean | No | `true` | true: 全操作を1 Undoグループにまとめ、失敗時ロールバック |
| stop_on_error | boolean | No | `true` | true: 最初のエラーで実行を停止 |

### Command オブジェクト

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| tool | string | Yes | 実行するツール名 |
| arguments | object | No | ツールに渡す引数 |

### 制約

- `batch_execute`の再帰呼び出しは不可（自身を`commands`に含められない）
- `commands`が空の場合はエラー
- `atomic: true`でエラー発生時、実行済みの全操作がUndoで巻き戻される

### Example: 複数オブジェクトを一括作成（atomic）

```json
{"name": "batch_execute", "arguments": {
  "commands": [
    {"tool": "create_game_object", "arguments": {"name": "Floor", "primitive_type": "Plane", "position": {"x": 0, "y": 0, "z": 0}}},
    {"tool": "create_game_object", "arguments": {"name": "Player", "primitive_type": "Capsule", "position": {"x": 0, "y": 1, "z": 0}}},
    {"tool": "create_game_object", "arguments": {"name": "Light", "primitive_type": "Cube", "position": {"x": 5, "y": 5, "z": 0}}}
  ],
  "atomic": true
}}
```

**Response (成功時):**
```
Batch Execute: 3/3 succeeded (atomic)

[0] OK create_game_object: Created 'Floor' (Plane) at (0, 0, 0)
[1] OK create_game_object: Created 'Player' (Capsule) at (0, 1, 0)
[2] OK create_game_object: Created 'Light' (Cube) at (5, 5, 0)
```

### Example: エラー時のロールバック

```json
{"name": "batch_execute", "arguments": {
  "commands": [
    {"tool": "create_game_object", "arguments": {"name": "A", "primitive_type": "Cube"}},
    {"tool": "nonexistent_tool", "arguments": {}},
    {"tool": "create_game_object", "arguments": {"name": "C", "primitive_type": "Sphere"}}
  ],
  "atomic": true,
  "stop_on_error": true
}}
```

**Response (失敗時):**
```
Batch Execute: 1/3 succeeded (atomic) [ROLLED BACK]

[0] OK create_game_object: Created 'A' (Cube) at (0, 0, 0)
[1] FAIL nonexistent_tool: Tool 'nonexistent_tool' not found

(1 command(s) skipped due to earlier error)
```

### Example: 非atomicで全コマンド実行

```json
{"name": "batch_execute", "arguments": {
  "commands": [
    {"tool": "modify_game_object", "arguments": {"object_path": "Player", "position": {"x": 0, "y": 2, "z": 0}}},
    {"tool": "modify_game_object", "arguments": {"object_path": "Enemy", "position": {"x": 5, "y": 1, "z": 3}}}
  ],
  "atomic": false,
  "stop_on_error": false
}}
```
