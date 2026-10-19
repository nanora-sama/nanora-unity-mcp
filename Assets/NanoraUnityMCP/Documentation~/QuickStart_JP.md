# nanora-unity-mcp クイックスタート

Unity Editor の中で動く MCP サーバーです。Claude Code・Codex・Cursor などの AI から Unity を操作できます。
Node.js や Python、別のサーバーは要りません。導入から最初の操作まで 5 分ほどです。

## 1. 必要なもの

- Unity 2021.3 LTS 以降（Unity 6 で日常使用）
- MCP クライアント: Claude Code / Codex / Claude Desktop / Cursor など
- キー入力のシミュレートを使うなら Input System パッケージ（1.4 以降）

## 2. 入れる（どれか 1 つ）

| 方法 | 手順 | 向いている人 |
|---|---|---|
| **A. tarball（.tgz）** | Window > Package Manager > **+** > *Add package from tarball…* で `com.nanora.nanora-unity-mcp-<版>.tgz` を選ぶ | BOOTH・GitHub Releases から落とした人（おすすめ） |
| **B. Git URL** | *Add package from git URL…* に `https://github.com/nanora-sama/nanora-unity-mcp.git?path=Assets/NanoraUnityMCP` | いつも最新版を使いたい人 |
| **C. .unitypackage** | 先に *Add package by name…* で `com.unity.nuget.newtonsoft-json` を入れ、*Assets > Import Package > Custom Package…* | Assets に直接置きたい人 |

- A と B は必要なパッケージ（Newtonsoft JSON・Test Framework・uGUI）も自動で入ります。C は入らないので、先に Newtonsoft JSON を入れてください。
- A の .tgz はプロジェクトの外の、消さない場所に置いてください（Unity はその場所を参照し続けます）。

## 3. 起動を確かめる

Unity を開くとサーバーが自動で起動し、初回は Setup Wizard（**Window > Unity MCP > Setup Wizard**）が開きます。
**Window > Unity MCP Server** に `Running` とポート番号が出ていれば準備完了です。

ポートはプロジェクトの場所から決まります（51726〜51825）。同じプロジェクトはいつも同じ番号なので、Unity を複数開いても混ざりません。

ブラウザやターミナルからも確かめられます（`<PORT>` は実際の番号）:

```
curl http://127.0.0.1:<PORT>/health
```

返ってきた `projectPath` が今のプロジェクトなら、つながる先は正しいです。

## 4. AI をつなぐ

| クライアント | 設定 |
|---|---|
| **Claude Code** | プロジェクトのフォルダで `claude` を起動するだけ。`.mcp.json` はサーバーが自動で作ります。`/mcp` で `nanora-unity-mcp` が connected なら OK |
| **Codex** | `codex mcp add nanora-unity-mcp --url http://127.0.0.1:<PORT>/mcp`（または `~/.codex/config.toml` に `url = "http://127.0.0.1:<PORT>/mcp"`）。設定後に新しいセッションを始める |
| **Claude Desktop** | `claude_desktop_config.json` の `mcpServers` に `"nanora-unity-mcp": { "url": "http://localhost:<PORT>/sse" }` を足して再起動 |
| **Cursor** | プロジェクトの `.cursor/mcp.json` に同じ設定 |

Setup Wizard の **Configure All Detected Clients** で、見つかったクライアントをまとめて設定することもできます。
`.mcp.json` にはこの PC 用のアドレスが入るので、Git には入れないでください（Wizard の *Add to .gitignore*）。

## 5. おすすめ設定

- **Setup Wizard の「Optimize for MCP (Recommended)」**: Unity が裏にあっても応答するようにします（Background Execution）。AI はたいてい別のウィンドウから操作するので、ほぼ必須です。
- **Play 時のスクリプト再読み込みを切る**（任意）: *Project Settings > Editor > Enter Play Mode Settings* で *Reload Domain* を外すと、Play に入るときに接続が一瞬切れなくなります。

## 6. サンプルで試す

1. Package Manager の nanora-unity-mcp > **Samples** > *Shooting Game Sample* > **Import**（C で入れた人は配布物の `Samples/ShootingGame` を Assets にコピー）
2. **Tools > Nanora Unity MCP > Samples > Build Shooting Game Scene**
3. AI にこう頼んでみてください:

```
シューティングゲームのシーンを Play して、Space を 5 回押して撃って、
ScoreText が 0 より大きくなったか確かめて、スクリーンショットを見せて。
```

AI が `manage_playmode` → `simulate_input` → `assert_ui_state` → `capture_screenshot` を順に使います。
「同じことを run_e2e_test の 1 回でやって」と頼むと、手順ごとの合否が一覧で返ります。

## 7. 困ったとき

| 症状 | 原因と対処 |
|---|---|
| 入れた直後にコンパイルエラー（`Newtonsoft` / `JObject` / `TestTools` が見つからない） | .unitypackage で入れて、依存パッケージが無い。*Add package by name…* で `com.unity.nuget.newtonsoft-json`（と、無ければ `com.unity.test-framework`）を入れる。A か B で入れ直すのが確実 |
| `Running` にならない | Window > Unity MCP Server で Start。Console に `[UnityMCP]` のエラーが出ていないか見る。番号が使用中なら次の番号に自動で移ります（表示された番号を使う） |
| Claude Code に出てこない | プロジェクトのフォルダで起動しているか、そこに `.mcp.json` があるか。Unity を先に起動し、そのあと `claude` を起動し直す |
| Codex に出てこない | URL が `/mcp` で終わっているか、番号が今の番号か。設定後は新しいセッションで読み込まれる |
| 違うプロジェクトを触っている | `/health` の `projectPath` を見る。番号が変わったら各クライアントの設定を直す |
| Unity が裏にあると返事が遅い・来ない | 5 の「Optimize for MCP」を ON |
| 「domain reload」「state unknown」というエラーが返る | コンパイル中か Play に入るところで、サーバーが数秒止まっただけ。`/health` が返るのを待ってから状態を確かめる。**同じ変更をすぐ繰り返さない**（もう反映されていることがある） |
| `simulate_input` のキーが効かない | Play 中か。Input System パッケージが入っているか（旧 Input Manager だけだとキーは届かない）。Input System 1.4 以降なら Unity が裏にあっても届く |
| スクリーンショットに UI が写らない | `capture_screenshot` の game ビューはカメラの描画だけを撮るので、*Screen Space - Overlay* の Canvas は写らない。Canvas を *Screen Space - Camera* にする |
| `execute_csharp` が拒否される | プロセス起動やネットワークなど一部の API は使えない。エラー文に理由が出ます |
| ツールが多すぎて AI が迷う | 環境変数 `NANORA_MCP_SUPERSAVE=1` で Unity を起動すると、分類だけを見せる省エネモードになる |

## 8. 不具合の報告・質問

GitHub の Issue へどうぞ: https://github.com/nanora-sama/nanora-unity-mcp/issues

次の 4 つがあると早く直せます:

1. Unity のバージョンと OS
2. nanora-unity-mcp の版（Package Manager か `/health` の `version`）
3. 使っている AI クライアントと、頼んだ文
4. `mcp_status` の結果と、Console の `[UnityMCP]` のエラー

## 9. 更新・削除

- 更新: A は新しい .tgz を同じ手順で選び直す。B は Package Manager の *Update*。C は新しい .unitypackage を上書きインポート
- 削除: Package Manager で *Remove*（C は `Assets/NanoraUnityMCP` を削除）。自動で作られた `.mcp.json` と `.claude/skills/unity-*` は必要なら手で消してください

## 次に読むもの

- 全ツール: [ToolReference.md](ToolReference.md)
- 概要・仕組み: パッケージの [README.md](../README.md)
