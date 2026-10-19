nanora-unity-mcp {VERSION}
==========================

Unity Editor の中だけで動く MCP サーバーです。Claude Code・Codex・Cursor などの AI から、
シーンの作成・C# の実行・Play・入力・UI の検証・スクリーンショット・テスト・プロファイルまで操作できます。
Node.js や Python、別のサーバーは要りません。

■ 同梱物
  com.nanora.nanora-unity-mcp-{VERSION}.tgz 本体（Package Manager 用。おすすめ）
  nanora-unity-mcp-{VERSION}.unitypackage  本体（Assets に直接入れる形）
  Samples/ShootingGame/                     試せるシューティングゲームのサンプル（任意）
  LICENSE                                   MIT License
  THIRD-PARTY-NOTICES.md                    同梱 DLL（Roslyn ほか、MIT）のライセンス表示

■ 動作環境
  Unity 2021.3 LTS 以降（Unity 6 で日常使用）
  MCP クライアント: Claude Code / Claude Desktop / Codex / Cursor など

■ 導入（どれか 1 つ）
  A. 同梱の .tgz（おすすめ）
     .tgz をプロジェクトの外の消さない場所に置き、
     Window > Package Manager > + > Add package from tarball... で選ぶ。
     必要なパッケージ（Newtonsoft JSON・Test Framework・uGUI）も自動で入ります。
  B. Git URL（いつでも最新版）
     Window > Package Manager > + > Add package from git URL... に次を入力
     https://github.com/nanora-sama/nanora-unity-mcp.git?path=Assets/NanoraUnityMCP
  C. 同梱の .unitypackage
     先に Package Manager > + > Add package by name... で com.unity.nuget.newtonsoft-json を入れてから、
     Assets > Import Package > Custom Package... で nanora-unity-mcp-{VERSION}.unitypackage を選ぶ。
     （.unitypackage は依存パッケージを入れられません。Test Framework と uGUI は通常のテンプレートに入っています）

  Unity を開くとサーバーが自動で起動し、初回は Setup Wizard が開きます。

■ AI とつなぐ
  ポートはプロジェクトごとに決まります（Window > Unity MCP Server に表示）。
  Claude Code : プロジェクトのフォルダで claude を起動するだけ（.mcp.json は自動で作られます）
  Codex       : codex mcp add nanora-unity-mcp --url http://127.0.0.1:<ポート>/mcp
  Cursor など : http://localhost:<ポート>/sse を MCP サーバーとして登録

  Unity が裏にあるときも応答するよう、Setup Wizard で Background Execution を ON にしてください。

■ サンプルを試す
  A・B で入れた場合 : Package Manager の nanora-unity-mcp > Samples > Shooting Game Sample > Import
  C で入れた場合   : Samples/ShootingGame フォルダを Assets の下にコピー
  そのあと Tools > Nanora Unity MCP > Samples > Build Shooting Game Scene でシーンができます。
  AI に「シューティングゲームを Play して、Space を 5 回押して、スコアが 0 より大きいか確かめて、スクショを見せて」
  と頼んでみてください。キー入力のシミュレートには Input System パッケージが必要です（無くても手では遊べます）。

■ 注意
  execute_csharp・execute_menu_item は、あなたの権限で Editor 内のコードを実行します。
  サーバーは 127.0.0.1 だけで待ち受けます。AI に許可する操作は各クライアントの設定で確認してください。

■ 困ったとき
  クイックスタートの「困ったとき」に、よくある症状と対処をまとめています:
  https://github.com/nanora-sama/nanora-unity-mcp/blob/main/Assets/NanoraUnityMCP/Documentation~/QuickStart_JP.md
  （同じ文書が .tgz の中の package/Documentation~/QuickStart_JP.md にも入っています）

■ ライセンス・サポート
  MIT License（ソースは GitHub で公開）。無料版と応援版の中身は同じです（応援版に優先サポートは付きません）。
  不具合・要望: https://github.com/nanora-sama/nanora-unity-mcp/issues
    （Unity のバージョン、この版の番号、AI クライアント、頼んだ文、mcp_status の結果を書いてもらえると早く直せます）
  BOOTH でのダウンロード・購入の問題: BOOTH のショップへのメッセージ
  個人で開発しているため、返答はできる範囲になります。
