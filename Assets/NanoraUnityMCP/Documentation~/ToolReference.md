# nanora-unity-mcp Tool Reference

Parameters and examples for the most used of the 57 tools. Every tool also describes its own parameters to the client: `tools/list` (or `list_tools` in SuperSave mode) returns the full schema of each one.

---

## Table of Contents

- [Scene Manipulation](#scene-manipulation)
- [Query & Search](#query--search)
- [Code Execution](#code-execution)
- [Testing & Automation](#testing--automation)
- [Performance Analysis](#performance-analysis)
- [Batch Operations](#batch-operations)
- [Diagnostics](#diagnostics)

---

## Scene Manipulation

### get_scene_hierarchy

Gets the complete hierarchy of all GameObjects in the current scene.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `include_inactive` | boolean | true | Include inactive GameObjects |
| `max_depth` | integer | 0 | Maximum depth to traverse (0 = unlimited) |

**Example:**
```json
{
  "name": "get_scene_hierarchy",
  "arguments": {
    "include_inactive": true,
    "max_depth": 3
  }
}
```

---

### get_object_details

Gets detailed information about a specific GameObject including all components.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `object_name` | string | Name of the GameObject to inspect |
| `object_path` | string | Full path (e.g., "Parent/Child/Target") |

One of `object_name` or `object_path` is required.

**Example:**
```json
{
  "name": "get_object_details",
  "arguments": {
    "object_name": "Main Camera"
  }
}
```

---

### create_game_object

Creates a new GameObject with optional primitive type.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `name` | string | "New GameObject" | Name for the object |
| `primitive_type` | string | "Empty" | Cube, Sphere, Capsule, Cylinder, Plane, Quad, Empty |
| `parent_path` | string | null | Path to parent GameObject |
| `position` | object | {x:0,y:0,z:0} | Initial position |
| `rotation` | object | {x:0,y:0,z:0} | Initial rotation (Euler) |
| `scale` | object | {x:1,y:1,z:1} | Initial scale |

**Example:**
```json
{
  "name": "create_game_object",
  "arguments": {
    "name": "RedCube",
    "primitive_type": "Cube",
    "position": {"x": 0, "y": 2, "z": 0}
  }
}
```

---

### modify_game_object

Modifies transform, components, and properties of a GameObject.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `object_name` | string | Target object name |
| `object_path` | string | Target object path |
| `position` | object | New position |
| `rotation` | object | New rotation |
| `scale` | object | New scale |
| `active` | boolean | Set active state |

---

### delete_game_object

Deletes a GameObject from the scene (supports undo).

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `object_name` | string | Target object name |
| `object_path` | string | Target object path |

---

### manage_scene

Manage Unity scenes: create new scenes, save, load, or get information about scenes.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `action` | string | *required* | Action: new, save, save_as, load, info |
| `path` | string | null | Scene path (required for save_as, load) |
| `save_current` | boolean | true | Save current scene before loading new one |

**Actions:**
| Action | Description |
|--------|-------------|
| `new` | Create a new empty scene with default objects |
| `save` | Save current scene to its existing path |
| `save_as` | Save current scene to specified path |
| `load` | Load a scene from path |
| `info` | Get current scene information and available scenes |

**Example - Get scene info:**
```json
{
  "name": "manage_scene",
  "arguments": {
    "action": "info"
  }
}
```

**Example - Create new scene:**
```json
{
  "name": "manage_scene",
  "arguments": {
    "action": "new",
    "save_current": true
  }
}
```

**Example - Save scene as:**
```json
{
  "name": "manage_scene",
  "arguments": {
    "action": "save_as",
    "path": "Assets/Scenes/MyNewScene.unity"
  }
}
```

**Example - Load scene:**
```json
{
  "name": "manage_scene",
  "arguments": {
    "action": "load",
    "path": "Assets/Scenes/GameScene.unity",
    "save_current": true
  }
}
```

---

## Query & Search

### query_objects

Query GameObjects using U-Selector syntax (CSS-like selectors).

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `selector` | string | *required* | U-Selector query string |
| `include_inactive` | boolean | false | Include inactive objects |
| `max_results` | integer | 100 | Maximum results (0 = unlimited) |
| `include_details` | boolean | false | Include component details |

**U-Selector Syntax:**
| Pattern | Description | Example |
|---------|-------------|---------|
| `#Name` | Find by exact name | `#Player` |
| `.Type` | Find by component type | `.Camera` |
| `/Path` | Find by hierarchy path | `/Canvas/Menu` |
| `[prop=val]` | Find by attribute | `[tag=Enemy]` |
| `.Type[attr]` | Combined selector | `.Enemy[Health>50]` |
| `ui:#name` | UI Toolkit by name | `ui:#StartButton` |
| `ui:.class` | UI Toolkit by class | `ui:.menu-item` |

**Example:**
```json
{
  "name": "query_objects",
  "arguments": {
    "selector": ".Light",
    "include_details": true
  }
}
```

---

### search_assets

Search project assets by name, type, or label.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `filter` | string | Search filter (e.g., "t:Texture", "l:MyLabel") |
| `folder` | string | Folder to search in |
| `max_results` | integer | Maximum results |

**Example:**
```json
{
  "name": "search_assets",
  "arguments": {
    "filter": "t:Material",
    "folder": "Assets/Materials"
  }
}
```

---

### wait_for_selector

Playwright-style auto-waiting for objects to become actionable.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `selector` | string | *required* | U-Selector query |
| `state` | string | "visible" | attached, detached, visible, hidden, interactable |
| `timeout` | number | 5 | Timeout in seconds |
| `poll_interval` | number | 0.1 | Check interval in seconds |

**Example:**
```json
{
  "name": "wait_for_selector",
  "arguments": {
    "selector": "#LoadingScreen",
    "state": "hidden",
    "timeout": 10
  }
}
```

---

## Code Execution

### execute_csharp

Execute C# code dynamically via Roslyn compiler.

Use complete C# statements, including semicolons, and `return value;` for a result.
Compilation failures and runtime exceptions return `isError: true` with the original
diagnostic. A runtime failure can leave changes made before the exception; read back
the affected state before retrying. The tool does not roll back or replay that script.
Reflection fallback is used only when Roslyn is unavailable, and unsupported expressions
are errors. Set `force_roslyn: true` to disable fallback even when Roslyn is unavailable.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `code` | string | *required* | C# code to execute |
| `timeout_ms` | integer | 5000 | Execution timeout |
| `include_usings` | array | [] | Additional using statements |
| `force_roslyn` | boolean | false | Require Roslyn; do not use reflection fallback |

**Example:**
```json
{
  "name": "execute_csharp",
  "arguments": {
    "code": "return Application.unityVersion;"
  }
}
```

**Advanced Example:**
```json
{
  "name": "execute_csharp",
  "arguments": {
    "code": "var player = GameObject.Find(\"Player\"); player.transform.position = Vector3.zero; return player.transform.position;",
    "include_usings": ["UnityEngine.AI"]
  }
}
```

---

### execute_menu_item

Execute Unity Editor menu items.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `menu_path` | string | Menu path (e.g., "Assets/Refresh") |

**Example:**
```json
{
  "name": "execute_menu_item",
  "arguments": {
    "menu_path": "Window/General/Console"
  }
}
```

---

### compile_project

Trigger Unity project compilation and wait for result.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `refresh_first` | boolean | true | Refresh AssetDatabase first |
| `wait_for_completion` | boolean | true | Wait for compilation |
| `timeout_seconds` | integer | 60 | Compilation timeout |

**Important: Domain Reload Behavior**

When compilation **succeeds**, Unity performs a domain reload which temporarily disconnects the MCP server (approximately 3-10 seconds). This is a Unity limitation and cannot be avoided.

| Compilation Result | MCP Response |
|-------------------|--------------|
| Failed | Returns `FAILED` with error details, connection stays alive |
| No changes | Returns `No compilation needed`, connection stays alive |
| Succeeded | Connection drops (domain reload), server auto-restarts |

**Recommended Workflow:**
```
1. compile_project
2. If connection drops → wait 5-10 seconds for server restart
3. get_compilation_errors → Verify SUCCESS status
```

**Example:**
```json
{
  "name": "compile_project",
  "arguments": {
    "timeout_seconds": 120
  }
}
```

---

### get_compilation_errors

Get compilation errors with source context.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `include_warnings` | boolean | false | Include warnings |
| `include_context` | boolean | true | Include source code context |
| `context_lines` | integer | 3 | Lines of context around error |

---

## Testing & Automation

### run_tests

Run Unity Test Framework tests.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `test_mode` | string | "EditMode" | EditMode or PlayMode |
| `filter` | string | null | Test name filter |
| `categories` | array | null | Test categories to run |

---

### get_test_results

Get test execution results.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `include_passed` | boolean | true | Include passed tests |
| `include_stacktrace` | boolean | true | Include failure stack traces |

---

### simulate_input

Simulate keyboard and mouse input.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `action` | string | key_press, key_release, key_tap, mouse_click, mouse_move, mouse_drag, ui_click |
| `key` | string | Key name (e.g., "Space", "A", "Return") |
| `position` | object | {x, y} screen position |
| `button` | integer | Mouse button (0=left, 1=right, 2=middle) |
| `selector` | string | U-Selector for ui_click |

**Example:**
```json
{
  "name": "simulate_input",
  "arguments": {
    "action": "ui_click",
    "selector": "#PlayButton"
  }
}
```

---

### capture_screenshot

Capture Game View or Scene View screenshots.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `view` | string | "game" | game or scene |
| `width` | integer | 0 | Width (0 = current) |
| `height` | integer | 0 | Height (0 = current) |
| `supersampling` | integer | 1 | Quality factor (1-4) |
| `save_to_file` | string | null | Optional file path |

**Example:**
```json
{
  "name": "capture_screenshot",
  "arguments": {
    "view": "game",
    "supersampling": 2,
    "save_to_file": "Screenshots/test.png"
  }
}
```

---

### manage_playmode

Control Unity Play Mode: enter, exit, pause, resume, or step through frames.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `action` | string | *required* | Action: play, stop, pause, resume, step, status |
| `auto_compile` | boolean | false | Compile project before entering play mode (recommended for automation). Only applies to 'play' action. |
| `wait_for_ready` | boolean | false | Wait until play mode is fully ready before returning (up to 10 seconds) |

**Actions:**
| Action | Description |
|--------|-------------|
| `play` | Enter Play Mode |
| `stop` | Exit Play Mode |
| `pause` | Pause Play Mode |
| `resume` | Resume from pause |
| `step` | Step one frame (while paused) |
| `status` | Get current Play Mode status |

**Example - Enter Play Mode:**
```json
{
  "name": "manage_playmode",
  "arguments": {
    "action": "play"
  }
}
```

**Example - Enter Play Mode with auto-compile:**
```json
{
  "name": "manage_playmode",
  "arguments": {
    "action": "play",
    "auto_compile": true,
    "wait_for_ready": true
  }
}
```

**Example - Get status:**
```json
{
  "name": "manage_playmode",
  "arguments": {
    "action": "status"
  }
}
```

**Note:** Use `auto_compile=true` for automation workflows to ensure all scripts are compiled before entering play mode. This prevents errors from outdated script assemblies.

---

### run_e2e_test

Run a scenario of steps in Play Mode and report each step. Steps: `wait_for_selector`, `simulate_input`, `assert`, `screenshot`, `wait`, `execute_csharp`.

**Example:**
```json
{
  "name": "run_e2e_test",
  "arguments": {
    "name": "Score after five shots",
    "steps": [
      {"action": "wait_for_selector", "selector": "#Player", "timeout": 5},
      {"action": "simulate_input", "input_action": "key_tap", "key": "Space"},
      {"action": "wait", "ms": 500},
      {"action": "assert", "selector": "#ScoreText", "property": "text", "expected": 0, "comparison": "greater_than"},
      {"action": "screenshot", "label": "after_shots"}
    ]
  }
}
```

---

### visual_regression_test

Compare a Game or Scene view capture against a stored baseline.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `action` | string | "compare" | compare, update_baseline, list_baselines, delete_baseline |
| `name` | string | | Baseline name |
| `view` | string | "game" | game or scene |
| `threshold` | number | 1.0 | Allowed difference in percent |
| `width`, `height` | integer | 0 | Capture size (0 = current) |

---

### performance_budget_test

Sample frames and check them against budgets.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `action` | string | "test" | test, configure, show_config |
| `sample_frames` | integer | 60 | Frames to sample |
| `budgets` | object | | `min_fps`, `max_frame_time_ms`, `max_memory_mb`, `max_draw_calls`, `max_triangles`, `max_batches` |

---

### generate_test_report

Write a Markdown or HTML report from E2E, visual regression and budget results (`results[]` with `type`, `name`, `status`, `duration_ms`, `steps`, `metrics`). `save_to_file` writes it to disk.

---

## Performance Analysis

### analyze_frame

Analyze frame rendering with optimization recommendations.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `enable_frame_debugger` | boolean | true | Use Frame Debugger |
| `include_shader_details` | boolean | false | Include shader info |
| `include_recommendations` | boolean | true | Include optimization tips |

**Output includes:**
- Total draw calls
- Batched draw calls
- Set pass calls
- Triangle/vertex count
- Shader usage breakdown
- Render pipeline info
- Optimization recommendations

---

### analyze_memory

Analyze memory usage and detect potential leaks.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `include_native` | boolean | true | Include native memory |
| `include_managed` | boolean | true | Include managed memory |
| `threshold_mb` | number | 10 | Report objects above this size |

---

### profile_cpu

CPU profiling and hotspot detection.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `frames` | integer | 100 | Frames to profile |
| `include_deep_profile` | boolean | false | Enable deep profiling |

---

### get_profiler_stats

Get Unity Profiler statistics.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `categories` | array | null | Specific categories to query |
| `frames` | integer | 1 | Number of frames |

---

## Batch Operations

### batch_modify

Modify multiple GameObjects at once.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `selector` | string | *required* | U-Selector for targets |
| `modifications` | array | *required* | List of modifications |
| `include_inactive` | boolean | false | Include inactive objects |
| `dry_run` | boolean | false | Preview without applying |

**Modification spec:**
| Field | Type | Description |
|-------|------|-------------|
| `target` | string | Property path (e.g., "Transform.localScale") |
| `operation` | string | set, add, multiply, toggle |
| `value` | any | Value to apply |

**Example:**
```json
{
  "name": "batch_modify",
  "arguments": {
    "selector": ".Enemy",
    "modifications": [
      {"target": "Transform.localScale", "operation": "multiply", "value": 2},
      {"target": "Renderer.enabled", "operation": "set", "value": true}
    ],
    "dry_run": true
  }
}
```

---

### set_active

Activate/deactivate multiple objects.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `selector` | string | *required* | U-Selector for targets |
| `active` | boolean | *required* | Active state |
| `include_inactive` | boolean | true | Include inactive in search |

**Example:**
```json
{
  "name": "set_active",
  "arguments": {
    "selector": ".DebugUI",
    "active": false
  }
}
```

---

### destroy_objects

Delete multiple objects by selector.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `selector` | string | *required* | U-Selector for targets |
| `include_inactive` | boolean | false | Include inactive objects |
| `confirm` | boolean | false | Must be true to proceed |

**Example:**
```json
{
  "name": "destroy_objects",
  "arguments": {
    "selector": ".Temporary",
    "confirm": true
  }
}
```

---

### instantiate_asset

Instantiate prefabs and assets into the scene.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `asset_path` | string | Path to prefab/asset |
| `position` | object | Spawn position |
| `rotation` | object | Spawn rotation |
| `parent_path` | string | Parent object path |

---

## Diagnostics

### diagnose_materials

Comprehensive component diagnostics: detects pink/broken materials, missing sprites, missing meshes, missing scripts, and other configuration issues.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `scope` | string | "scene" | Scope: scene, selection, project |
| `include_warnings` | boolean | true | Include warnings (not just errors) |
| `include_shader_stats` | boolean | true | Include shader usage statistics |

**Detects:**
| Component | Issue | Severity |
|-----------|-------|----------|
| Material | Pink/error shader | ERROR |
| Material | Missing textures | WARNING |
| SpriteRenderer | No sprite assigned | ERROR |
| MeshFilter | No mesh assigned | ERROR |
| TrailRenderer | Missing material | WARNING |
| LineRenderer | Missing material | WARNING |
| Animator | No AnimatorController | WARNING |
| AudioSource | PlayOnAwake without clip | WARNING |
| UI Image | No sprite (visible) | WARNING |
| MonoBehaviour | Missing script | ERROR |

**Example:**
```json
{
  "name": "diagnose_materials",
  "arguments": {
    "scope": "project",
    "include_warnings": true
  }
}
```

---

### repair_materials

Auto-repair shader issues.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `selector` | string | null | Target specific objects |
| `fallback_shader` | string | null | Shader to use as fallback |
| `dry_run` | boolean | false | Preview without applying |

---

### get_console_logs

Get Unity console logs.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `count` | integer | 50 | Number of logs |
| `log_type` | string | "All" | All, Log, Warning, Error |
| `filter` | string | null | Text filter |
| `include_stacktrace` | boolean | false | Include stack traces |

**Example:**
```json
{
  "name": "get_console_logs",
  "arguments": {
    "count": 20,
    "log_type": "Error",
    "include_stacktrace": true
  }
}
```

---

### mcp_status

Get Unity MCP server status, connection info, and list of available tools. Use this to verify the connection is working.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `include_tool_list` | boolean | true | Include full list of available tools |
| `include_system_info` | boolean | true | Include Unity system information |

**Example:**
```json
{
  "name": "mcp_status",
  "arguments": {}
}
```

**Output includes:**
- Connection status (URL, port, uptime, restart count)
- Compilation state (`IsCompiling`, `HasUncompiledChanges`, `LastCompilationTime`)
- Background execution settings (`runInBackground` status)
- Unity version and platform info
- List of available tools categorized by function

**Compilation State Warnings:**
The server automatically prepends warnings to tool outputs when:
- `IsCompiling = true`: Unity is actively compiling
- `HasUncompiledChanges = true`: Scripts have been modified but not compiled
- `LastCompilationTime < 3s`: Compilation just finished

**Example - Check background execution:**
```json
{
  "name": "mcp_status",
  "arguments": {
    "include_tool_list": false,
    "include_system_info": false
  }
}
```

---

### configure_editor

Configure Unity Editor settings for optimal MCP operation. Use this to enable background execution so MCP responds even when Unity loses focus.

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `action` | string | Action: status, enable_background, disable_background, optimize_for_mcp |

**Actions:**
| Action | Description |
|--------|-------------|
| `status` | Show current editor settings |
| `enable_background` | Enable `runInBackground` (Application and PlayerSettings) |
| `disable_background` | Disable `runInBackground` |
| `optimize_for_mcp` | Apply all recommended settings (background execution, auto-start) |

**Example - Enable background execution:**
```json
{
  "name": "configure_editor",
  "arguments": {
    "action": "enable_background"
  }
}
```

**Example - Optimize for MCP:**
```json
{
  "name": "configure_editor",
  "arguments": {
    "action": "optimize_for_mcp"
  }
}
```

**Example - Check current settings:**
```json
{
  "name": "configure_editor",
  "arguments": {
    "action": "status"
  }
}
```

**Note:** When `runInBackground` is disabled, MCP server may not respond when Unity loses focus. Use `optimize_for_mcp` for best results.

---

## Setup & Skills

### setup_status

Reports MCP client configuration health: server status, client configs (Claude Desktop/Code/Cursor), permissions, CLAUDE.md section, and bundled skill install status.

**Parameters:** (none)

**Returns:** Multi-section markdown report.

---

### reconfigure_client

Update or repair MCP client configurations for Claude Desktop, Claude Code, Cursor, or all detected clients.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `client` | string | required | `claude_desktop` \| `claude_code` \| `cursor` \| `all` |
| `dry_run` | boolean | false | Report what would change without writing files |

**Behavior:** Merges with existing config (preserves other MCP servers, creates `.bak` backup on overwrite). When `claude_code` is configured, permissions and bundled skills install too.

---

### install_skills

Install or update the bundled Unity workflow skills into `<Project>/.claude/skills/`. Called automatically by the Setup Wizard on first install, and when upgrading nanora-unity-mcp to a newer setup version. Available as a standalone tool to refresh skills on demand.

**Parameters:**
| Name | Type | Default | Description |
|------|------|---------|-------------|
| `dry_run` | boolean | false | Report what would change without writing files |
| `force` | boolean | false | Overwrite pre-existing or user-modified skill files (a `.bak` is created first) |

**Result categories:**
- `NEW` — skill did not exist in target, installed
- `UPD` — bundled version is newer than last-installed, updated
- `OK` — already up to date, no change
- `SKIP` — pre-existing or user-modified, left alone (use `force: true` to replace)
- `FAIL` — I/O error, check Unity console

**Bundled skills:**
| Skill | Purpose |
|-------|---------|
| `unity-material-repair` | Diagnose + fix pink materials, capture before/after |
| `unity-pre-pr-check` | Compile + EditMode tests + scene screenshot before PR |
| `unity-scene-setup` | Bootstrap a new scene (lighting, camera, ground, spawn) |
| `unity-e2e-test` | Play-mode smoke test with assertions and capture |
| `unity-performance-budget` | FPS / draw calls / memory budget check |

**Manifest:** Install state is tracked at `<Project>/.claude/.nanora-unity-skills.json`. Deleting this file resets tracking — subsequent installs will then treat every file in `.claude/skills/` as user-owned and skip it unless `force: true`.

---

## Common Patterns

### Safe code editing workflow (Compilation-aware)
```
1. Edit C# scripts via your AI client
2. compile_project → Wait for compilation
3. (Server automatically warns if HasUncompiledChanges=true)
4. get_compilation_errors → Check for issues
5. Test/verify functionality
```

### Check for errors after code changes
```
1. compile_project → Wait for compilation
2. get_compilation_errors → Check for issues
3. get_console_logs(log_type="Error") → Check runtime errors
```

### Performance investigation
```
1. analyze_frame → Get rendering overview
2. analyze_memory → Check memory usage
3. profile_cpu → Find hotspots
```

### Automated testing
```
1. wait_for_selector("#LoadingComplete", state="visible") → Wait for scene ready
2. simulate_input(action="ui_click", selector="#PlayButton") → Interact
3. capture_screenshot → Document result
```

### Batch scene cleanup
```
1. query_objects(".DebugOnly") → Find debug objects
2. destroy_objects(selector=".DebugOnly", confirm=true) → Remove them
```

### Pre-flight component check
```
1. diagnose_materials(scope="project") → Check all prefabs for issues
2. Fix any ERROR-level issues (missing sprites, meshes, scripts)
3. Review WARNING-level issues (optional)
4. compile_project → Ensure code compiles
```

---

## Error Handling

All tools return a result with:
- `content`: Array of content items (text, images)
- `isError`: Boolean indicating failure

Error responses include descriptive messages to help diagnose issues.

---

*Last updated: 2026-02-05 (compile_project ドメインリロード動作のドキュメント追加)*
