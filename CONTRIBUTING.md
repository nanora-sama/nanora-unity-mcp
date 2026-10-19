# Contributing

Issues and pull requests are welcome. 日本語でも英語でも大丈夫です。

- The package lives in `Assets/NanoraUnityMCP`; open the repository root as a Unity project (CI uses Unity 6000.3).
- A tool is a class with `[McpTool("name")]` implementing `IMcpTool` under `Editor/Tools/`; add it to the category table in `Editor/Core/ToolCategories.cs`.
- Run the EditMode tests (Window > General > Test Runner, or `-runTests -testPlatform EditMode` in batch mode) before opening a pull request. CI runs them on every pull request.
- With the Editor open, `node scripts/mcp/verify-transport.mjs` checks the HTTP/SSE transport end to end.
- Add a line to `Assets/NanoraUnityMCP/CHANGELOG.md` under `[Unreleased]`.

By contributing you agree that your contribution is licensed under the repository's MIT License.
