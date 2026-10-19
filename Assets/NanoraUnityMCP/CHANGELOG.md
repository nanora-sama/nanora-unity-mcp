# Nanora Unity MCP - Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Skill recipes and references use `FindObjectsByType(FindObjectsSortMode.None)` instead of the Unity 6-deprecated `FindObjectsOfType`, and `unity-mcp-skill` has a "Writing Unity code" section (find APIs, per-frame work, static state with domain reload disabled, Awaitable) linked to the Unity 6.3 docs. `unity-pre-pr-check` treats new obsolete-API warnings in changed files as findings.
- `execute_csharp` reflection mode accepts `FindAnyObjectByType<T>()` and `FindFirstObjectByType<T>()` as well as `FindObjectOfType<T>()`, and resolves them without reflecting on the deprecated method.
- `query_objects` type selectors call `FindObjectsByType` through `UnityCompatibility` instead of reflecting on `FindObjectsOfType`.

## [1.2.0] - 2026-10-19

Open-source release. The package is now MIT licensed and the testing tools of the former paid Test Pro edition are part of it.

### Added

- **Streamable HTTP** at `POST /mcp` for Codex and other HTTP clients, next to legacy SSE for Claude Code, Claude Desktop and Cursor.
- Read-only resources `unity://project/info` and `unity://editor/state` (compile failure, play/pause state, loaded and unsaved scenes).
- `/health` reports project path, process id, version, protocol version and transports, so a client can tell which Editor it reached.
- `mcp_status` and `get_console_logs` return `structuredContent`.
- Testing tools formerly in Test Pro, now free: `run_e2e_test`, `visual_regression_test`, `performance_budget_test`, `generate_test_report` (category `test`).
- **Shooting Game sample**: scripts plus **Tools > Nanora Unity MCP > Samples > Build Shooting Game Scene**, which builds the scene in the importing project's Unity version and render pipeline. `#ScoreText`, `#GameOver` and Input System actions make it a target for `simulate_input`, `wait_for_selector` and `assert_ui_state`.
- `scripts/mcp/verify-transport.mjs` and `verify-codex.mjs`: checks against a running Editor and an installed Codex client.

### Changed

- `package.json` declares `com.unity.test-framework` and `com.unity.ugui`, which the package always needed; a tarball or Git install into an empty project now compiles without adding them by hand.
- **License: MIT** (was the Unity Asset Store EULA). Bundled Roslyn and System.* assemblies are listed in `THIRD-PARTY-NOTICES.md`.
- Legacy SSE gives each connection its own `/message?sessionId=...` endpoint; replies go only to the requesting client even when JSON-RPC ids overlap. `/message` without a session replies directly instead of broadcasting.
- Browser `Origin` headers are validated against loopback; request bodies are bounded. Protocol versions 2024-11-05 through 2025-11-25 are negotiated.
- Console logs are filtered by type before the count limit, and errors, assertions, exceptions and warnings are told apart.

### Removed

- The Test Pro package, its license keys and license window. Nothing needs activating.
- `AssetStoreCaptureUtility` (Asset Store screenshot menu) and the old `NanoraUnityMCP-Reference.html`, which listed 31 tools.

### Fixed

- `simulate_input` keys and mouse reach the game while the Unity window is in the background. The Input System disables the keyboard and mouse when Unity loses focus and gives keys to the game only while the Game view is focused, so an AI driving Unity from a terminal had its key taps dropped while the tool reported them sent. During Play Mode the tool now routes input to the game regardless of focus while simulated keys are held, and restores the project settings when the last key is released or Play Mode ends.
- The HTTP/SSE server falls back to the next port when the configured one is taken, on any OS language. "Port in use" is now detected from error codes (`SocketError.AddressAlreadyInUse`, Win32 10048/183/32, Mono's duplicate-listener error) instead of English words in the message, so Japanese Windows no longer gives up after the first attempt. The failure log reports the attempts actually made.
- HTTP `/message` no longer cuts tool calls off at 30 s. A `tools/call` request stays open for the longer of its `timeout_seconds`/`timeout_ms` argument and 300 s (the `run_tests` default), plus 30 s for the tool to report its own timeout, so `compile_project`, `manage_playmode` with `wait_for_ready`, `run_tests` and package installs return their results instead of a bare 504. When the limit is reached, the 504 body is a JSON-RPC error carrying the request id and says the request may still be running or may already have completed.
- Main-thread timeouts say whether the operation ran. An operation that never reached the main thread is dropped and reported as not started; one that started (for example a player build run through `execute_menu_item`) keeps running and is reported as still running and possibly completing, instead of advising to focus the Editor. Previously a timed-out operation that had not started still ran later.

- Server instructions are returned in the top-level `instructions` field of the initialize result, where MCP clients read them, instead of inside `serverInfo`.
- Tool descriptions match the implementation: `manage_scene` states that `new`/`load` save or discard unsaved changes, `capture_screenshot` states what the game view capture omits, `manage_playmode` documents the immediate "Already in Play Mode" return, and connection-retry advice no longer tells clients to replay mutations.
- `assert_ui_state`, `install_skills`, `manage_assets`, and `manage_prefabs` are listed by `list_categories`/`list_tools` in SuperSave mode; `list_tools` takes its category enum from the category table.

- Bundled skills resolve their actual UPM package location, including local and cached packages.
- Empty batch commands return an input error before registry lookup; missing or ambiguous Prefab selectors return MCP errors.
- CI creates clean install fixtures, serializes Unity license use, rejects missing test evidence, and avoids blocking the editor thread in Test Pro tests.

- `execute_csharp` now returns compilation and runtime failures as MCP errors without retrying the script through reflection. Reflection fallback is limited to unavailable Roslyn assemblies, preventing false success responses and repeated scene mutations after a runtime exception.

- `execute_csharp` (Roslyn) references `System.dll`, so `SortedDictionary`, `Queue`, `Stack`, `LinkedList`, `Regex` and `Uri` compile on Unity's Mono profile instead of failing with CS1069/CS0103.

- Reflection fallback now rejects unsupported expressions as MCP errors, accepts optional return/semicolon syntax for supported reads, and preserves forced Roslyn runtime diagnostics.

## [1.1.0] - 2026-04-19

### Added

- **Bundled Claude Code skills**: 5 Unity workflow skills (`unity-material-repair`, `unity-pre-pr-check`, `unity-scene-setup`, `unity-e2e-test`, `unity-performance-budget`) auto-installed into `<Project>/.claude/skills/` when Claude Code is configured.
- **`install_skills` MCP tool**: Refresh/update bundled skills on demand. Supports `dry_run` and `force` modes.
- **Skill install manifest** (`.claude/.nanora-unity-skills.json`): SHA256 fingerprints track what was installed so user-modified skill files are preserved on update.
- **Setup Wizard skills UI**: Configure step shows install status per skill, with "Install / Update" and "Force Overwrite" buttons.
- **Existing-user upgrade path**: Setup version bumped to 5 so already-configured users get skills installed silently on next Editor launch.
- **Test Pro edition**: Introduced a separate paid package (`com.nanora.nanora-unity-mcp-test-pro`) adding visual regression, performance-budget gates, E2E test runner, and test reporting. The free edition remains unchanged.
- **`AssetStoreCaptureUtility`**: Editor menu (`Window > Unity MCP > Dev > Capture Asset Store Screenshot`) for repeatable Game View captures at the Asset Store aspect ratio.

### Changed

- **`PackageExporter` refactor**: now exposes a generic `Export(outputPath, packageRoot, excludePatterns)` public API so the Test Pro exporter can reuse it.
- **Tool count**: base package now ships **56 MCP tools** (previously 31 at initial release).

## [1.0.0] - 2026-01-19

### Added

- Initial release with 31 MCP tools
- HTTP/SSE transport for AI client communication
- Setup Wizard with multi-client support (Claude Desktop, Claude Code, Cursor)
- Scene manipulation tools (create, modify, delete, query objects)
- U-Selector query engine (CSS-like object selection)
- Roslyn-based C# code execution
- Playwright-style E2E testing (wait_for_selector, simulate_input)
- Screenshot capture (Game View, Scene View)
- Performance analysis tools (CPU profiler, memory analyzer, frame debugger)
- Material diagnostics and repair
- Compilation state awareness with automatic warnings
- Component diagnostics (Missing Scripts, unassigned references)
- Play Mode control with auto-compile support
- Scene management (create, save, load)
- Background execution support

### Tools

```
analyze_frame          analyze_memory         batch_modify
capture_screenshot     compile_project        configure_editor
create_game_object     delete_game_object     destroy_objects
diagnose_materials     execute_csharp         execute_menu_item
get_compilation_errors get_console_logs       get_object_details
get_profiler_stats     get_scene_hierarchy    get_test_results
instantiate_asset      manage_playmode        manage_scene
mcp_status             modify_game_object     profile_cpu
query_objects          repair_materials       run_tests
search_assets          set_active             simulate_input
wait_for_selector
```
