# U-Selector 構文リファレンス

## 概要

U-SelectorはCSS/PlaywrightセレクタにインスパイアされたUnity GameObject検索構文。
`query_objects`, `wait_for_selector`, `assert_ui_state`, `simulate_input` (ui_click),
`batch_modify`, `set_active`, `destroy_objects`, `instantiate_asset` (parent_selector) で使用される。

## セレクタ型

### ID セレクタ: `#Name`

GameObjectの名前で完全一致検索。

```
#Player          -- 名前が "Player" のオブジェクト
#Main Camera     -- 名前が "Main Camera" のオブジェクト
#Canvas          -- 名前が "Canvas" のオブジェクト
```

### ワイルドカード: `#Name*`, `#*Name`, `#Na*me`

グロブパターンによる名前検索。`*` は任意の文字列、`?` は任意の1文字にマッチ。

```
#Enemy*          -- "Enemy" で始まるオブジェクト (Enemy_01, EnemyBoss, ...)
#*Button         -- "Button" で終わるオブジェクト (StartButton, QuitButton, ...)
#*Player*        -- "Player" を含むオブジェクト
#Enemy_??        -- "Enemy_" + 2文字のオブジェクト (Enemy_01, Enemy_AB, ...)
```

### 型セレクタ: `.ComponentType`

指定したコンポーネントを持つオブジェクトを検索。

```
.Camera          -- Cameraコンポーネントを持つオブジェクト
.Light           -- Lightコンポーネントを持つオブジェクト
.MeshRenderer    -- MeshRendererを持つオブジェクト
.Rigidbody       -- Rigidbodyを持つオブジェクト
.AudioSource     -- AudioSourceを持つオブジェクト
.Canvas          -- Canvasを持つオブジェクト
.Animator        -- Animatorを持つオブジェクト
```

**ショートハンドマッピング（大文字小文字不問）:**

| 短縮名 | フル名 |
|--------|--------|
| Camera | UnityEngine.Camera |
| Light | UnityEngine.Light |
| MeshRenderer | UnityEngine.MeshRenderer |
| MeshFilter | UnityEngine.MeshFilter |
| BoxCollider | UnityEngine.BoxCollider |
| SphereCollider | UnityEngine.SphereCollider |
| CapsuleCollider | UnityEngine.CapsuleCollider |
| Rigidbody | UnityEngine.Rigidbody |
| AudioSource | UnityEngine.AudioSource |
| Canvas | UnityEngine.Canvas |
| Text | UnityEngine.UI.Text |
| Image | UnityEngine.UI.Image |
| Button | UnityEngine.UI.Button |
| Animator | UnityEngine.Animator |
| ParticleSystem | UnityEngine.ParticleSystem |

カスタムコンポーネントも名前で検索可能:
```
.PlayerController   -- PlayerControllerコンポーネントを持つオブジェクト
.EnemyAI            -- EnemyAIコンポーネントを持つオブジェクト
```

### パスセレクタ: `/Parent/Child`

ヒエラルキーパスで検索。

```
/Canvas/Menu/StartButton  -- Canvas > Menu > StartButton
/Environment/Trees        -- Environment > Trees
/Player                   -- ルート直下の "Player"
```

### 属性セレクタ: `[property=value]`

オブジェクトやコンポーネントのプロパティでフィルタ。

```
[tag=Player]         -- タグが "Player" のオブジェクト
[layer=5]            -- レイヤーが 5 のオブジェクト
[name=Enemy]         -- 名前が "Enemy" のオブジェクト
[active=true]        -- アクティブなオブジェクト
[activeSelf=false]   -- 自身が非アクティブなオブジェクト
```

**比較演算子:**

| 演算子 | 説明 |
|--------|------|
| `=` | 等しい |
| `!=` | 等しくない |
| `>` | より大きい |
| `<` | より小さい |
| `>=` | 以上 |
| `<=` | 以下 |

**組み込みプロパティ:**

| プロパティ | 説明 |
|-----------|------|
| name | オブジェクト名 |
| tag | タグ |
| layer | レイヤー（数値） |
| active / activeInHierarchy | ヒエラルキー内のアクティブ状態 |
| activeSelf | 自身のアクティブ状態 |

コンポーネントのプロパティ・フィールドにもアクセス可能:
```
[Health>50]          -- Healthプロパティが50以上
[Speed<=10]          -- Speedフィールドが10以下
[isAlive=true]       -- isAliveプロパティがtrue
```

### 複合セレクタ: `.Type[prop=value]`

型セレクタと属性セレクタを組み合わせる。

```
.Enemy[Health>50]       -- Enemyコンポーネント持ちでHealth > 50
.Light[intensity>1]     -- Lightコンポーネント持ちでintensity > 1
```

### UI Toolkit セレクタ: `ui:#name`, `ui:.class`

UI Toolkit（UIElements）のVisualElementを検索する。

```
ui:#submit-button    -- name="submit-button" のVisualElement
ui:.menu-item        -- class="menu-item" のVisualElement
ui:Button            -- Button型のVisualElement
```

## セレクタの解決順序

1. `ui:` プレフィックスがあれば UI Toolkit 検索
2. `#Name` パターンにマッチすれば名前検索
3. `.Type` パターンにマッチすればコンポーネント検索
4. `/Path` パターンにマッチすればパス検索
5. `[attr]` を含む場合は属性検索
6. いずれにも該当しなければ名前検索にフォールバック

## include_inactive の挙動

- `false`（デフォルト）: `activeInHierarchy == true` のオブジェクトのみ
- `true`: 非アクティブオブジェクトも検索対象に含める

## QueryOne と曖昧性エラー

`wait_for_selector` や `simulate_input` (ui_click) は内部的に `SelectorEngine.QueryOne` を使用。
複数オブジェクトがマッチすると `AmbiguousSelectorException` が発生する。

**対処法:**
- より具体的なセレクタを使用: `/Canvas/Menu/Button` (パス指定)
- 属性フィルタを追加: `#Button[tag=MainButton]`
- ワイルドカードを避けて正確な名前を使用

## 使用例

### 全ての敵を検索
```json
{"name": "query_objects", "arguments": {"selector": ".EnemyController"}}
```

### HPが低い敵を検索
```json
{"name": "query_objects", "arguments": {"selector": ".EnemyController[Health<20]"}}
```

### 特定のUIボタンが操作可能になるまで待機
```json
{"name": "wait_for_selector", "arguments": {"selector": "/Canvas/GameUI/ActionButton", "state": "interactable"}}
```

### 全てのPickupアイテムを非アクティブに
```json
{"name": "set_active", "arguments": {"selector": "[tag=Pickup]", "active": false}}
```

### 名前が "Temp" で始まるオブジェクトを全削除
```json
{"name": "destroy_objects", "arguments": {"selector": "#Temp*", "confirm": true}}
```

### UI Toolkitのボタンを含むUIDocumentを検索
```json
{"name": "query_objects", "arguments": {"selector": "ui:.primary-button"}}
```
