# Code Tools Reference

## execute_csharp

Unity Editor内でC#コードを動的実行する。Roslynコンパイル + リフレクションフォールバック。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| code | string | Yes | - | 実行するC#コード。`return value;` で結果を返す |
| timeout_ms | integer | No | 5000 | 最大実行時間（ミリ秒） |
| include_usings | array[string] | No | [] | 追加のusing文 |
| force_roslyn | boolean | No | false | Roslynモード強制（リフレクションフォールバック無効） |

### デフォルトusing

- `System`
- `System.Collections.Generic`
- `System.Linq`
- `UnityEngine`
- `UnityEditor`

### セキュリティ制限

以下のAPIはブロックされる:
- ファイル操作: `File.Delete`, `File.WriteAllText`, `StreamWriter` 等
- プロセス: `Process.Start`, `ProcessStartInfo`
- ネットワーク: `HttpClient`, `WebRequest`, `Socket` 等
- リフレクション: `Assembly.Load`, `Activator.CreateInstance`
- 環境: `Environment.Exit`, `Environment.SetEnvironmentVariable`
- エディタ破壊: `Application.Quit`, `AssetDatabase.DeleteAsset`
- 無限ループ: `while(true)`, `for(;;)`, `goto`

### 自動Roslyn検出

以下の場合、自動的にRoslynモードが選択される:
- `var` 宣言 + 後続ステートメントがある
- 複数のセミコロン（複数ステートメント）

### Roslynモードを使うべきケース（明示的に `force_roslyn: true` を付けること）

自動検出が期待通りに効かず、reflectionモードに落ちて `Expression not directly evaluable ...` エラーになる失敗例が実運用で観測されている。以下のいずれかに該当するコードは **必ず `force_roslyn: true` を指定**すること:

- コメント行を含む（`//` や `/* */`）
- `using` 文を含む
- ローカルの `class` や `static` メソッド定義を含む
- 3行以上のステートメント
- Generic型 (`AssetDatabase.LoadAssetAtPath<T>(...)`) を複数回使う
- ネストしたラムダ/LINQ

**誤**: reflectionに落ちて失敗する例
```json
{"name": "execute_csharp", "arguments": {
  "code": "// setup\nvar go = new GameObject(\"X\");\nvar c = go.AddComponent<MyComponent>();\nreturn c != null;"
}}
```

**正**: `force_roslyn: true` を付ける
```json
{"name": "execute_csharp", "arguments": {
  "code": "// setup\nvar go = new GameObject(\"X\");\nvar c = go.AddComponent<MyComponent>();\nreturn c != null;",
  "force_roslyn": true
}}
```

### Example

```json
{"name": "execute_csharp", "arguments": {
  "code": "var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None); return string.Join(\"\\n\", cameras.Select(c => $\"{c.name}: fov={c.fieldOfView}\"));",
  "include_usings": ["System.Linq"]
}}
```

---

## execute_menu_item

Unityメニューアイテムをパスで実行する。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| menu_path | string | Yes | メニューアイテムのフルパス |

### ドメインリロードを引き起こすメニュー

以下のメニューは実行後にドメインリロードが発生する:
- `Assets/Refresh`
- `Assets/Reimport All`
- `File/Build Settings`
- `Edit/Project Settings`

### Example

```json
{"name": "execute_menu_item", "arguments": {"menu_path": "File/Save Scene"}}
```

---

## compile_project

プロジェクトのスクリプトコンパイルをトリガーし、完了を待機する。

**重要:** コンパイル成功時はドメインリロードが発生し、サーバーが3-10秒停止する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| timeout_seconds | integer | No | 60 | コンパイル待機の最大時間（秒） |
| include_warnings | boolean | No | false | 警告を結果に含めるか |

### Example

```json
{"name": "compile_project", "arguments": {"timeout_seconds": 60, "include_warnings": true}}
```

### Response

```
## Compilation Result: SUCCESS

Status: Compilation succeeded
Errors: 0
Warnings: 2

### JSON Summary:
{
  "success": true,
  "error_count": 0,
  "warning_count": 2,
  "errors": []
}
```

---

## get_compilation_errors

現在のコンパイルエラー・警告を取得する（再コンパイルをトリガーしない）。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| include_warnings | boolean | No | true | 警告を含めるか |
| file_filter | string | No | - | ファイルパスでフィルタ（部分一致） |
| include_source_context | boolean | No | true | エラー周辺のソースコードを含めるか |

### Example

```json
{"name": "get_compilation_errors", "arguments": {
  "file_filter": "PlayerController",
  "include_source_context": true
}}
```

### Response

```
## Compilation Status: FAILED

Last compilation: 2026-03-28 14:30:00
Errors: 1
Warnings: 0

### Errors:

**Assets/Scripts/PlayerController.cs:42:13**
```
error CS1061: 'Transform' does not contain a definition for 'MoveForward'
```
Source:
```csharp
    40:     void Update()
    41:     {
>>> 42:         transform.MoveForward(speed * Time.deltaTime);
    43:     }
```
```

---

## get_console_logs

Unityコンソールのログを取得する。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| count | integer | No | 50 | 最大取得数 |
| log_type | string | No | "All" | フィルタ: `All`, `Log`, `Warning`, `Error` |
| clear | boolean | No | false | 取得後にコンソールをクリアするか |

### Example

```json
{"name": "get_console_logs", "arguments": {"count": 10, "log_type": "Error"}}
```
