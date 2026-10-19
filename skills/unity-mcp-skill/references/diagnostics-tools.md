# Diagnostics Tools Reference

## diagnose_materials

シーン内のマテリアル問題を診断する。

### Parameters

パラメータなし。

### Example

```json
{"name": "diagnose_materials", "arguments": {}}
```

---

## repair_materials

診断で検出されたマテリアル問題を修復する。

### Parameters

パラメータなし。

### Example

```json
{"name": "repair_materials", "arguments": {}}
```

---

## mcp_status

MCPサーバーのステータス・接続情報・利用可能ツール一覧を取得する。接続確認に使用。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| include_tool_list | boolean | No | true | ツール一覧を含める |
| include_system_info | boolean | No | true | Unityシステム情報を含める |

### Example

```json
{"name": "mcp_status", "arguments": {"include_tool_list": false, "include_system_info": true}}
```

### Response

```
# Unity MCP Server Status

## Connection
- Status: Connected
- Server URL: http://localhost:51754/sse
- Port: 51754
- Running: True
- Uptime: 5.2 minutes
- Restart Count: 1 (domain reloads)
- Is Compiling: False

## Background Execution
- Application.runInBackground: True
- PlayerSettings.runInBackground: True

## Unity Info
- Unity Version: 2022.3.20f1
- Platform: WindowsEditor
- Project: MyGame
```

---

## configure_editor

Unity Editor設定の最適化（バックグラウンド実行の有効化等）。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| action | string | Yes | `status`, `enable_background`, `disable_background`, `optimize_for_mcp` |

### Action の説明

| action | 効果 |
|--------|------|
| status | 現在の設定を表示 |
| enable_background | `runInBackground` を有効化（永続化） |
| disable_background | `runInBackground` を無効化 |
| optimize_for_mcp | バックグラウンド実行 + 自動起動を有効化 |

### Example

```json
{"name": "configure_editor", "arguments": {"action": "optimize_for_mcp"}}
```

### Response

```
# MCP Optimization Applied

## Changes:
- Background execution: enabled (persistent)
- PlayerSettings.runInBackground: enabled (for builds)
- MCP Auto-start: already enabled

## Result:
Unity Editor is now optimized for MCP operation.
All settings are **persisted** and will apply on Unity restart.
```

---

## setup_status

MCPセットアップの状態を確認する。

### Parameters

パラメータなし。

### Example

```json
{"name": "setup_status", "arguments": {}}
```

---

## reconfigure_client

MCPクライアント設定を再構成する。

### Parameters

パラメータなし。

### Example

```json
{"name": "reconfigure_client", "arguments": {}}
```

---

## list_tools

利用可能なMCPツール一覧を取得する。

### Parameters

パラメータなし。

### Example

```json
{"name": "list_tools", "arguments": {}}
```

---

## list_categories

ツールカテゴリ一覧を取得する。

### Parameters

パラメータなし。

### Example

```json
{"name": "list_categories", "arguments": {}}
```

---

## execute

ツール名を指定して動的に実行する（メタツール）。

### Parameters

ツールに応じて異なる。

### Example

```json
{"name": "execute", "arguments": {"tool_name": "get_scene_hierarchy", "arguments": {}}}
```
