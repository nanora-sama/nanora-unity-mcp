# コード実行レシピ

## 概要

`execute_csharp` を使ってUnity Editor内でC#コードを動的実行する。
Roslyn（デフォルト）とリフレクションの2つのモードがある。

例は Unity 2022.2 以降（Unity 6 を含む）の API で書いている。`FindObjectOfType` / `FindObjectsOfType` は
Unity 6 で非推奨なので、一つなら `FindAnyObjectByType<T>()`、複数なら `FindObjectsByType<T>(FindObjectsSortMode.None)` を使う。
`execute_csharp` では `System.Object` と区別するため `UnityEngine.Object.` を付ける。Unity 2021.3 では従来の `FindObjectsOfType<T>()` を使う。

## 基本パターン

### シンプルな式の評価
```json
{"name": "execute_csharp", "arguments": {
  "code": "return UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;"
}}
```

### 複数行コード
```json
{"name": "execute_csharp", "arguments": {
  "code": "var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None); var sb = new System.Text.StringBuilder(); foreach (var cam in cameras) { sb.AppendLine($\"{cam.name}: fov={cam.fieldOfView}, depth={cam.depth}\"); } return sb.ToString();"
}}
```

### 追加のusingを指定
```json
{"name": "execute_csharp", "arguments": {
  "code": "return string.Join(\"\\n\", AssetDatabase.FindAssets(\"t:Scene\").Select(g => AssetDatabase.GUIDToAssetPath(g)));",
  "include_usings": ["System.Linq"]
}}
```

### Roslynモード強制
```json
{"name": "execute_csharp", "arguments": {
  "code": "var allObjects = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None); var result = allObjects.Where(t => t.childCount > 5).Select(t => $\"{t.name}: {t.childCount} children\").ToArray(); return string.Join(\"\\n\", result);",
  "force_roslyn": true,
  "include_usings": ["System.Linq"]
}}
```

## シーン操作

### 全オブジェクトのタグ一覧
```json
{"name": "execute_csharp", "arguments": {
  "code": "var tags = new System.Collections.Generic.HashSet<string>(); foreach (var obj in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) tags.Add(obj.tag); return string.Join(\"\\n\", tags);"
}}
```

### レイヤー使用状況
```json
{"name": "execute_csharp", "arguments": {
  "code": "var layers = new System.Collections.Generic.Dictionary<string, int>(); foreach (var obj in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) { var name = LayerMask.LayerToName(obj.layer); if (!layers.ContainsKey(name)) layers[name] = 0; layers[name]++; } var sb = new System.Text.StringBuilder(); foreach (var kv in layers) sb.AppendLine($\"{kv.Key}: {kv.Value}\"); return sb.ToString();"
}}
```

### コンポーネント使用統計
```json
{"name": "execute_csharp", "arguments": {
  "code": "var stats = new System.Collections.Generic.Dictionary<string, int>(); foreach (var obj in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) { foreach (var comp in obj.GetComponents<Component>()) { if (comp == null) continue; var name = comp.GetType().Name; if (!stats.ContainsKey(name)) stats[name] = 0; stats[name]++; } } return string.Join(\"\\n\", stats.OrderByDescending(kv => kv.Value).Take(20).Select(kv => $\"{kv.Key}: {kv.Value}\"));",
  "include_usings": ["System.Linq"]
}}
```

## Editor操作

### プレイヤー設定の取得
```json
{"name": "execute_csharp", "arguments": {
  "code": "return $\"Company: {PlayerSettings.companyName}\\nProduct: {PlayerSettings.productName}\\nVersion: {PlayerSettings.bundleVersion}\\nTarget: {EditorUserBuildSettings.activeBuildTarget}\";"
}}
```

### AssetDatabaseの操作
```json
{"name": "execute_csharp", "arguments": {
  "code": "AssetDatabase.Refresh();"
}}
```

### Selectionの操作
```json
{"name": "execute_csharp", "arguments": {
  "code": "return Selection.activeGameObject?.name ?? \"(nothing selected)\";"
}}
```

## リフレクションモード

Roslynが使えない環境ではリフレクションモードにフォールバックする。
以下の式が直接評価可能:

```json
{"name": "execute_csharp", "arguments": {"code": "Selection.activeGameObject.name"}}
{"name": "execute_csharp", "arguments": {"code": "GameObject.Find(\"Player\")"}}
{"name": "execute_csharp", "arguments": {"code": "Debug.Log(\"Hello from MCP\")"}}
{"name": "execute_csharp", "arguments": {"code": "EditorApplication.isPlaying"}}
{"name": "execute_csharp", "arguments": {"code": "AssetDatabase.Refresh()"}}
```

## セキュリティ制限

以下のAPIは実行がブロックされる:
- ファイル削除・書き込み（File.Delete, StreamWriter等）
- プロセス起動（Process.Start）
- ネットワーク通信（HttpClient, WebRequest等）
- アセンブリ動的読み込み（Assembly.Load）
- 環境変数操作（Environment.SetEnvironmentVariable）
- アプリケーション終了（Application.Quit, EditorApplication.Exit）
- 無限ループ（while(true), for(;;)）
- goto文

## タイムアウト

デフォルトは5000ms。長時間処理は `timeout_ms` で延長:

```json
{"name": "execute_csharp", "arguments": {
  "code": "... 重い処理 ...",
  "timeout_ms": 30000
}}
```
