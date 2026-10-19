# Query Tools Reference

## query_objects

U-Selector構文でGameObjectを検索する。CSS/Playwrightに似たセレクタ構文をサポート。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| selector | string | Yes | - | U-Selectorクエリ文字列 |
| include_inactive | boolean | No | false | 非アクティブオブジェクトを含めるか |
| max_results | integer | No | 100 | 最大結果数（0 = 無制限） |
| include_details | boolean | No | false | コンポーネント・Transform詳細を含めるか |

### Selector構文

- `#Name` -- 名前で検索（完全一致）
- `#Name*` -- 名前でワイルドカード検索（前方一致）
- `#*Name` -- 名前でワイルドカード検索（後方一致）
- `.Component` -- コンポーネント型で検索
- `/Parent/Child` -- ヒエラルキーパスで検索
- `[prop=value]` -- 属性フィルタ（=, !=, >, <, >=, <=）
- `.Type[prop>value]` -- コンポーネント型+属性フィルタ
- `ui:#name` -- UI Toolkit要素を名前で検索
- `ui:.class` -- UI Toolkit要素をクラスで検索

詳細は [U-Selector構文リファレンス](u-selector-syntax.md) を参照。

### Example

```json
{"name": "query_objects", "arguments": {
  "selector": ".Camera",
  "include_details": true
}}
```

### Response

```
Query: .Camera
Found: 1 object(s)

- /Main Camera
  InstanceID: 12345
  Position: (0.0, 1.0, -10.0)
  Rotation: (0.0, 0.0, 0.0)
  Scale: (1.0, 1.0, 1.0)
  Layer: Default
  Tag: MainCamera
  Components: Transform, Camera, AudioListener
```

---

## search_assets

プロジェクト内のアセットをファジー検索する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| query | string | Yes | - | 検索クエリ（複数単語でファジーマッチ） |
| type_filter | string | No | "all" | `all`, `prefab`, `texture`, `material`, `script`, `scene`, `audio`, `model`, `shader`, `animation` |
| path_filter | string | No | - | パスフィルタ（例: `Assets/Prefabs`） |
| max_results | integer | No | 20 | 最大結果数 |
| include_packages | boolean | No | false | Packagesフォルダのアセットを含めるか |

### Example

```json
{"name": "search_assets", "arguments": {
  "query": "player",
  "type_filter": "prefab",
  "max_results": 10
}}
```

### Response

```
# Asset Search Results for "player"

Found 2 asset(s):

## GameObject (2)
- **PlayerPrefab** (45.2 KB)
  - Path: `Assets/Prefabs/PlayerPrefab.prefab`
  - GUID: `abc123...`
- **PlayerUI** (12.1 KB)
  - Path: `Assets/Prefabs/UI/PlayerUI.prefab`
  - GUID: `def456...`
```

---

## instantiate_asset

プレハブやアセットをシーンにインスタンス化する。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| asset_path | string | No | アセットパス（例: `Assets/Prefabs/Enemy.prefab`） |
| guid | string | No | アセットGUID（pathの代替） |
| position | object | No | ワールド位置 `{x, y, z}` |
| rotation | object | No | オイラー回転 `{x, y, z}` |
| parent_selector | string | No | 親オブジェクトのU-Selector |
| place_on_floor | boolean | No | レイキャストで床に配置 |
| instance_name | string | No | インスタンスのカスタム名 |

### Example

```json
{"name": "instantiate_asset", "arguments": {
  "asset_path": "Assets/Prefabs/Enemy.prefab",
  "position": {"x": 5, "y": 0, "z": 10},
  "instance_name": "Enemy_01"
}}
```

---

## wait_for_selector

セレクタにマッチするオブジェクトが指定の状態になるまでポーリングで待機する。Playwright風のauto-waiting。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| selector | string | Yes | - | U-Selectorクエリ |
| state | string | No | "visible" | 待機する状態: `attached`, `detached`, `visible`, `hidden`, `interactable` |
| timeout | number | No | 5 | タイムアウト（秒） |
| poll_interval | number | No | 0.1 | ポーリング間隔（秒） |

### State の説明

| state | 条件 |
|-------|------|
| attached | オブジェクトが存在する（非アクティブでも可） |
| detached | オブジェクトが存在しない |
| visible | アクティブ + Renderer/Graphic有効 + CanvasGroup alpha > 0 |
| hidden | 存在しない、または非アクティブ、または非表示 |
| interactable | visible + CanvasGroup.interactable + Selectable.interactable |

### Example

```json
{"name": "wait_for_selector", "arguments": {
  "selector": "#GameOverPanel",
  "state": "visible",
  "timeout": 30
}}
```

### Response

```
Selector '#GameOverPanel' matched state 'visible' after 2.34s.
Object: /Canvas/GameOverPanel
```
