# Batch Tools Reference

## batch_modify

セレクタにマッチする複数のGameObjectを一括変更する。全変更はUndo対応。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| selector | string | Yes | - | U-Selector |
| modifications | array | Yes | - | 変更リスト |
| include_inactive | boolean | No | false | 非アクティブオブジェクトを含める |
| dry_run | boolean | No | false | プレビュー（実際には変更しない） |

### Modification オブジェクト

| フィールド | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| target | string | Yes | - | プロパティパス（例: `Transform.localScale`） |
| operation | string | No | "set" | `set`, `add`, `multiply`, `toggle` |
| value | any | Yes | - | 設定する値 |

### サポートされる型

- `float`, `int` -- set, add, multiply
- `bool` -- set, toggle
- `string` -- set
- `Vector3` -- set, add, multiply (`{x, y, z}` または `[x, y, z]` または uniform値)
- `Vector2` -- set, add, multiply
- `Color` -- set (`{r, g, b, a}` または `[r, g, b, a]` または `"#RRGGBB"`)
- `Quaternion` -- set（オイラー角を`{x, y, z}`で指定）
- `enum` -- set（文字列で指定）

### Example: スケール一括変更

```json
{"name": "batch_modify", "arguments": {
  "selector": ".Enemy",
  "modifications": [
    {"target": "Transform.localScale", "operation": "set", "value": {"x": 2, "y": 2, "z": 2}}
  ]
}}
```

### Example: 位置を加算

```json
{"name": "batch_modify", "arguments": {
  "selector": "[tag=Pickup]",
  "modifications": [
    {"target": "Transform.localPosition", "operation": "add", "value": {"x": 0, "y": 1, "z": 0}}
  ]
}}
```

### Example: ドライラン

```json
{"name": "batch_modify", "arguments": {
  "selector": ".MeshRenderer",
  "modifications": [
    {"target": "MeshRenderer.enabled", "operation": "toggle", "value": true}
  ],
  "dry_run": true
}}
```

### Response

```
# Batch Modification Results

**Targets:** 5 object(s)
**Modifications:** 1

## Summary
- Successful: 5
- Errors: 0
```

---

## set_active

セレクタにマッチするオブジェクトのActive状態を一括設定する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| selector | string | Yes | - | U-Selector |
| active | boolean | Yes | - | Active状態 |
| include_inactive | boolean | No | true | 非アクティブオブジェクトも検索対象に含める |

### Example

```json
{"name": "set_active", "arguments": {
  "selector": "#DebugUI",
  "active": false,
  "include_inactive": true
}}
```

### Response

```
Set 3 object(s) to inactive:
- DebugUI
- DebugPanel
- DebugConsole
```

---

## destroy_objects

セレクタにマッチする複数オブジェクトを一括削除する。Undo対応。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| selector | string | Yes | - | U-Selector |
| include_inactive | boolean | No | false | 非アクティブオブジェクトを含める |
| confirm | boolean | Yes | false | 削除確認（trueでないと実行されない） |

### Example

```json
{"name": "destroy_objects", "arguments": {
  "selector": "[tag=Temporary]",
  "confirm": true
}}
```

### Response

```
Destroyed 7 object(s):
- TempObject_01
- TempObject_02
- TempObject_03
...
```
