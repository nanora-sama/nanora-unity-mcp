---
title: U-Selector の書き方
description: U-Selector は nanora-unity-mcp の CSS 風の検索式。Unity の GameObject と UI Toolkit の要素を、名前・コンポーネント・階層のパス・属性で探します。
sidebar:
  order: 1
---

U-Selector は、CSS のセレクタが HTML の要素を探すように GameObject を探します。[`query_objects`](/nanora-unity-mcp/ja/tools/query-objects/)・[`wait_for_selector`](/nanora-unity-mcp/ja/tools/wait-for-selector/)・[`assert_ui_state`](/nanora-unity-mcp/ja/tools/assert-ui-state/)・[`simulate_input`](/nanora-unity-mcp/ja/tools/simulate-input/)（`ui_click`）・[`batch_modify`](/nanora-unity-mcp/ja/tools/batch-modify/)・[`run_e2e_test`](/nanora-unity-mcp/ja/tools/run-e2e-test/) の手順で使えます。

```text
#Player                 名前で
#Enemy*                 名前をワイルドカードで（* は任意の文字列、? は 1 文字）
.Camera                 コンポーネントの型で
/Canvas/Menu/PlayButton 階層のパスで
[tag=Player]            属性で
.Enemy[tag=Boss]        型と属性で
ui:#StartButton         UI Toolkit の要素を名前で
ui:.menu-item           UI Toolkit の要素を USS クラスで
ui:Button               UI Toolkit の要素を型で
```

## 名前 — `#Name`

GameObject の名前と完全に一致するものを探します。`*` と `?` でパターンになります: `#Enemy*`（前方一致）、`#*Spawner`（後方一致）、`#*Boss*`（部分一致）。記号の付かない文字列も名前として扱います。

## コンポーネント — `.Type`

その型のコンポーネントを持つ GameObject を探します。`.Camera`・`.Rigidbody`・自作の `.EnemyController` など。

## パス — `/Parent/Child`

ルートのオブジェクトからの階層のパスで探します。

## 属性 — `[名前 演算子 値]`

演算子は `=`・`!=`・`>`・`<`。使える属性:

| 属性 | 値 |
|---|---|
| `name` | GameObject の名前 |
| `tag` | タグ |
| `layer` | レイヤー番号 |
| `active` | 階層の中で有効か |
| `activeSelf` | 自分の有効フラグ |
| それ以外 | オブジェクトのコンポーネントの public プロパティ（例: `[enabled=false]`） |

型と組み合わせて絞り込めます: `.Enemy[tag=Boss]`。

## UI Toolkit — `ui:`

`ui:` を付けると、シーンにあるすべての `UIDocument` の要素から探します: `ui:#名前`、`ui:.USSクラス`、`ui:Button` のような型名。
