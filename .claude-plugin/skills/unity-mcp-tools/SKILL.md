---
name: unity-mcp-tools
description: Reference for the nanora-unity-mcp server tools (scene, query, code, batch, test, performance, materials, diagnostics, play mode, packages) and U-Selector syntax. Use when operating the Unity Editor through the nanora-unity-mcp MCP server.
---

# Unity MCP Tools Reference

This skill provides a reference for all Unity MCP tools available via the nanora-unity-mcp server.

## Connection

The MCP server runs inside Unity Editor. Check `.mcp.json` in the project root for the server URL and port.

## Tool Categories

### Scene (6 tools)
- `get_scene_hierarchy` - Get complete GameObject hierarchy tree
- `get_object_details` - Inspect GameObject and all its components
- `create_game_object` - Create new GameObject (primitives, empties, with components)
- `modify_game_object` - Modify transforms, components, properties
- `delete_game_object` - Delete GameObject (supports undo)
- `manage_scene` - Create, save, load scenes

### Query (4 tools)
- `query_objects` - Find objects using U-Selector (CSS-like syntax: `#Name`, `.Component`, `/Path`, `[prop=value]`)
- `search_assets` - Search project assets by name/type/label
- `wait_for_selector` - Playwright-style auto-wait for object states
- `instantiate_asset` - Instantiate prefabs/assets into scene

### Code (4 tools)
- `execute_csharp` - Execute C# dynamically via Roslyn
- `execute_menu_item` - Execute Unity menu items
- `compile_project` - Trigger compilation (causes domain reload on success)
- `get_compilation_errors` - Get compilation errors with source context

### Batch (3 tools)
- `batch_modify` - Modify multiple objects by selector
- `set_active` - Activate/deactivate objects by selector
- `destroy_objects` - Delete objects by selector

### Test (3 tools)
- `run_tests` - Run EditMode/PlayMode tests
- `get_test_results` - Get test results with stack traces
- `simulate_input` - Simulate keyboard/mouse/UI input

### Performance (4 tools)
- `analyze_frame` - Analyze rendering performance
- `analyze_memory` - Memory usage analysis
- `profile_cpu` - CPU profiling with hotspot detection
- `get_profiler_stats` - Get Unity Profiler statistics

### Materials (2 tools)
- `diagnose_materials` - Detect broken materials, missing scripts
- `repair_materials` - Auto-repair shader issues

### Diagnostics (6 tools)
- `mcp_status` - Server status and tool list
- `get_console_logs` - Unity console logs with filtering
- `configure_editor` - Configure editor settings
- `capture_screenshot` - Capture Game/Scene view screenshots
- `setup_status` - Check all client configuration health
- `reconfigure_client` - Fix/update client configurations

### PlayMode (1 tool)
- `manage_playmode` - Control play mode (play/stop/pause/step)

### Scene Management (1 tool)
- `manage_scene` - Scene file operations

### Packages (3 tools)
- `manage_packages` - Search Unity Registry packages
- `install_package` - Install packages from registry/git/local
- `uninstall_package` - Remove packages

## Common Patterns

### Scene Building
```
1. get_scene_hierarchy → understand current state
2. create_game_object → add objects
3. modify_game_object → set transforms/components
4. capture_screenshot → verify result
```

### Compile-Test Loop
```
1. (edit scripts externally)
2. compile_project → wait for compilation
3. get_compilation_errors → check for errors
4. run_tests → verify behavior
5. get_test_results → check results
```

### Troubleshooting
```
1. setup_status → check all config health
2. reconfigure_client client=all → fix configs
3. mcp_status → verify server state
```

## Writing Unity code (Unity 6)
- Use `FindAnyObjectByType<T>()` / `FindObjectsByType<T>(FindObjectsSortMode.None)`; `FindObjectOfType` / `FindObjectsOfType` are deprecated in Unity 6. In `execute_csharp`, qualify as `UnityEngine.Object.`
- Do not search, call `GetComponent`, `Camera.main`, LINQ or build strings in `Update`; cache references in `Awake` / `OnEnable`
- Reset static state with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` so it works with domain reload disabled
- After editing scripts, run `compile_project` with `include_warnings: true` and check the changed files for new `CS0618` warnings

## U-Selector Syntax
- `#ObjectName` - by exact name; `*` and `?` wildcards (`#Enemy*`)
- `.ComponentType` - has component (`.Camera`, `.Rigidbody`)
- `/Parent/Child` - by hierarchy path
- `[property=value]` - by attribute, including tag (`[tag=Player]`); comparison operators supported
- Compound: `.Type[prop=value]` (`.Light[intensity>1]`)
- UI Toolkit: `ui:#name`, `ui:.class`

Full syntax: `skills/unity-mcp-skill/references/u-selector-syntax.md`
