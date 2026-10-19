# Package Tools Reference

## manage_packages

パッケージの一覧・検索・情報取得・Unity Account確認を行う。

### Parameters

| パラメータ | 型 | 必須 | デフォルト | 説明 |
|-----------|-----|------|-----------|------|
| action | string | Yes | - | `list`, `search`, `search_all`, `info`, `check_login`, `open_my_assets`, `open_package_manager` |
| package_name | string | No | - | パッケージ名または検索クエリ（`search`, `info` で使用） |
| include_indirect | boolean | No | false | 間接依存を含める（`list` で使用） |

### Example: インストール済み一覧

```json
{"name": "manage_packages", "arguments": {"action": "list"}}
```

### Example: パッケージ検索

```json
{"name": "manage_packages", "arguments": {"action": "search", "package_name": "cinemachine"}}
```

### Example: パッケージ詳細

```json
{"name": "manage_packages", "arguments": {"action": "info", "package_name": "com.unity.inputsystem"}}
```

### Example: Unity Accountログイン確認

```json
{"name": "manage_packages", "arguments": {"action": "check_login"}}
```

### Example: My Assetsを開く

```json
{"name": "manage_packages", "arguments": {"action": "open_my_assets"}}
```

---

## install_package

パッケージをインストールする。Registry, Git, ローカル, .unitypackageに対応。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| package_id | string | No | パッケージID（例: `com.unity.inputsystem`） |
| version | string | No | バージョン指定（省略で最新） |
| git_url | string | No | GitリポジトリURL |
| local_path | string | No | ローカルパス（`file:../path`） |
| unitypackage_path | string | No | .unitypackageファイルパス |
| interactive | boolean | No | .unitypackageのインポートダイアログ表示 |

### Example: Registryから

```json
{"name": "install_package", "arguments": {"package_id": "com.unity.inputsystem"}}
```

### Example: バージョン指定

```json
{"name": "install_package", "arguments": {"package_id": "com.unity.inputsystem", "version": "1.7.0"}}
```

### Example: Gitから

```json
{"name": "install_package", "arguments": {"git_url": "https://github.com/user/repo.git"}}
```

### Example: .unitypackage

```json
{"name": "install_package", "arguments": {
  "unitypackage_path": "/path/to/asset.unitypackage",
  "interactive": false
}}
```

**注意:** インストール後はドメインリロードが発生する。

---

## uninstall_package

パッケージをアンインストールする。

### Parameters

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| package_id | string | Yes | アンインストールするパッケージID |

### Example

```json
{"name": "uninstall_package", "arguments": {"package_id": "com.unity.inputsystem"}}
```

**注意:** アンインストール後はドメインリロードが発生する。
