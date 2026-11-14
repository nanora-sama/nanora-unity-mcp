---
title: U-Selector syntax
description: U-Selector is the CSS-like query language of nanora-unity-mcp for finding Unity GameObjects and UI Toolkit elements by name, component, hierarchy path or attribute.
sidebar:
  order: 1
---

U-Selector finds GameObjects the way CSS selectors find HTML elements. [`query_objects`](/nanora-unity-mcp/tools/query-objects/), [`wait_for_selector`](/nanora-unity-mcp/tools/wait-for-selector/), [`assert_ui_state`](/nanora-unity-mcp/tools/assert-ui-state/), [`simulate_input`](/nanora-unity-mcp/tools/simulate-input/) (`ui_click`), [`batch_modify`](/nanora-unity-mcp/tools/batch-modify/) and the steps of [`run_e2e_test`](/nanora-unity-mcp/tools/run-e2e-test/) all take one.

```text
#Player                 by name
#Enemy*                 name with wildcards (* any text, ? one character)
.Camera                 by component type
/Canvas/Menu/PlayButton by hierarchy path
[tag=Player]            by attribute
.Enemy[tag=Boss]        component type and attribute
ui:#StartButton         UI Toolkit element by name
ui:.menu-item           UI Toolkit elements by USS class
ui:Button               UI Toolkit elements by type
```

## Name — `#Name`

Matches the GameObject name exactly. `*` and `?` make it a pattern: `#Enemy*` (starts with), `#*Spawner` (ends with), `#*Boss*` (contains). A selector without a prefix is also treated as a name.

## Component — `.Type`

Matches GameObjects that have a component of that type, such as `.Camera`, `.Rigidbody` or your own `.EnemyController`.

## Path — `/Parent/Child`

Matches by hierarchy path from a root object.

## Attribute — `[name op value]`

Operators: `=`, `!=`, `>`, `<`. Attribute names:

| Attribute | Value |
|---|---|
| `name` | GameObject name |
| `tag` | Tag |
| `layer` | Layer number |
| `active` | Active in the hierarchy |
| `activeSelf` | Own active flag |
| anything else | A public property of one of the object's components, e.g. `[enabled=false]` |

Combine with a type to narrow the search: `.Enemy[tag=Boss]`.

## UI Toolkit — `ui:`

A `ui:` prefix searches the visual trees of all `UIDocument`s in the scene: `ui:#name`, `ui:.uss-class` or a type name such as `ui:Button`.
