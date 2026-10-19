---
name: unity-scene-setup
description: Bootstrap a new Unity scene with sensible defaults (lighting, camera, ground, player spawn). Use when the user says "create a new scene", "set up a blank scene", or starts a new level/prototype.
---

# Unity Scene Setup

Create a new scene with minimal-viable gameplay scaffolding.

## Before Starting

Ask the user (if not given):
- Scene name (default: `NewScene`)
- Target genre hint: `fps` | `thirdperson` | `topdown` | `ui-only` (default: `thirdperson`)
- Skybox / ambient preference (default: `default skybox`)

## Steps

1. **Create scene** — save-on-exit safe:
   - Tool: `mcp__nanora-unity-mcp__manage_scene` with `action: "new"`, then `action: "save_as"` with `path: "Assets/Scenes/<SceneName>.unity"`

2. **Add ground plane** (skip for `ui-only`):
   - Tool: `mcp__nanora-unity-mcp__create_game_object` with `primitive: "Plane"`, `name: "Ground"`, `scale: {"x": 5, "y": 1, "z": 5}`

3. **Configure lighting**:
   - Light: if the scene has no directional light, `mcp__nanora-unity-mcp__manage_light` with `action: "create"`, `light_type: "directional"`.
   - Ambient: `mcp__nanora-unity-mcp__configure_lighting` with `action: "set_ambient"`, `ambient_mode: "skybox"`.

4. **Configure camera** via Cinemachine if installed, else default Main Camera:
   - Check: `mcp__nanora-unity-mcp__manage_packages` list → look for `com.unity.cinemachine`
   - If present: `mcp__nanora-unity-mcp__setup_cinemachine` with `action: "add_brain"`, then `action: "create_vcam"` (`vcam_type: "freelook"` for `thirdperson`, `"virtual"` otherwise).
   - Else: keep default Main Camera and `modify_game_object` to position it reasonably.

5. **Player spawn** (skip for `ui-only`):
   - Tool: `mcp__nanora-unity-mcp__create_game_object` with empty object named `PlayerSpawn` at origin.

6. **Canvas** (for `ui-only` or if user wants HUD):
   - Tool: `mcp__nanora-unity-mcp__manage_canvas` with `action: "create"`, `render_mode: "screen_space_overlay"`

7. **Save scene**:
   - Tool: `mcp__nanora-unity-mcp__manage_scene` with `action: "save"`

8. **Verify** — screenshot and hierarchy:
   - Tool: `mcp__nanora-unity-mcp__capture_screenshot` → `./Build/Reports/scene-setup.png`
   - Tool: `mcp__nanora-unity-mcp__get_scene_hierarchy`

9. **Report**: scene name, objects created, screenshot path.

## Notes

- Do not install missing packages without asking — offer to install Cinemachine if the user wants camera polish.
- For `ui-only`, skip ground/camera/spawn entirely — just Canvas + EventSystem.
