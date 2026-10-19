---
name: unity-material-repair
description: Diagnose and repair broken/pink materials in the open Unity scene. Use when the user reports missing textures, magenta materials, or shader errors. Runs diagnose_materials → repair_materials → captures a before/after screenshot.
---

# Unity Material Repair

Fix broken materials (pink/magenta) in the active scene.

## Prerequisites

Unity Editor must be running with the MCP server connected.

## Steps

1. **Confirm the problem is real** — ask which scene/object if not obvious, then capture a screenshot for before-evidence:
   - Tool: `mcp__nanora-unity-mcp__capture_screenshot`
   - Save to `./Build/Reports/materials-before.png`

2. **Diagnose** — scan the scene for broken materials:
   - Tool: `mcp__nanora-unity-mcp__diagnose_materials`
   - Note: reports missing shaders, missing textures, and magenta-fallback renderers.

3. **Repair** — apply fixes based on the diagnosis:
   - Tool: `mcp__nanora-unity-mcp__repair_materials`
   - Parameters: `scope` (`scene` / `project` / `selection`, default `scene`), `target_shader` (auto-detected if omitted), `fix_missing_shaders`, `upgrade_legacy`.
   - `dry_run` defaults to `true` (preview only): review the preview first, then call again with `dry_run: false` to apply.
   - Prefer the least destructive strategy first; escalate only if still broken.

4. **Verify** — re-run diagnosis and take an after-screenshot:
   - Tool: `mcp__nanora-unity-mcp__diagnose_materials` (should report zero issues)
   - Tool: `mcp__nanora-unity-mcp__capture_screenshot` → `./Build/Reports/materials-after.png`

5. **Check logs** — confirm no new errors:
   - Tool: `mcp__nanora-unity-mcp__get_console_logs` with `log_type: "Error"`

6. **Report** — summarize:
   - Materials scanned / repaired / unresolved
   - Before/after screenshot paths
   - Any residual errors

## Failure Modes

- If `diagnose_materials` reports shader errors not fixable by `repair_materials`: surface them and ask whether to reimport the SRP package.
- If repairs apply but visuals remain pink after domain reload: trigger `execute_menu_item` with `menu_path: "Assets/Refresh"`, then re-verify.
