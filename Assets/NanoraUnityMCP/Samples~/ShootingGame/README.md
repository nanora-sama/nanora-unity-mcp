# Shooting Game Sample

A tiny 2D shooter for trying nanora-unity-mcp end to end: build a scene, play it, press keys, wait for UI and assert on it.
It ships only scripts. The scene is generated in your project, so it works on Unity 2021.3–6 and in the Built-in, URP and HDRP pipelines.

## Set up

1. Package Manager → Nanora Unity MCP → Samples → **Shooting Game Sample** → Import.
2. **Tools > Nanora Unity MCP > Samples > Build Shooting Game Scene**. This writes and opens `Scenes/ShootingGame.unity` in the imported sample folder.
3. Press Play. Left/Right (or A/D) moves, Space fires. An enemy that reaches the bottom, or touches you, ends the game.
   The first three enemies fall down the middle, straight at the ship, so a scripted test can score without aiming.

Key simulation (`simulate_input`) drives the Input System package. With only the legacy Input Manager the game still plays by hand, but simulated keys are not delivered.

## What an AI can check

| Selector | What it is |
|---|---|
| `#Player` | The ship (`PlayerController`, `ShotsFired` counts shots) |
| `#ScoreText` | The score as a bare number, so `assert_ui_state` can use `greater_than` |
| `#GameOver` | Inactive until the game ends |
| `.Enemy`, `.Bullet` | Spawned at runtime |

## Prompts to try

```
Build the shooting game sample scene and show me its hierarchy.
Play the shooting game, tap Space five times, then wait until ScoreText is greater than 0 and take a screenshot.
Play the game without pressing anything and wait for #GameOver to appear. How long did it take?
Make the enemies fall twice as fast, play it, and tell me whether the player can still score.
```

Equivalent tool calls:

```
execute_menu_item("Tools/Nanora Unity MCP/Samples/Build Shooting Game Scene")
manage_playmode(action="play", wait_for_ready=true)
simulate_input(action="key_tap", key="Space")
assert_ui_state(selector="#ScoreText", property="text", expected=0, comparison="greater_than", timeout=10)
wait_for_selector(selector="#GameOver", timeout=30)
capture_screenshot()
manage_playmode(action="stop")
```

## Files

- `Scripts/` – `ShootingGameManager` (spawning, score, game over), `PlayerController`, `Bullet`, `Enemy`, `SampleSprite`/`SquareVisual` (visuals built from code)
- `Editor/ShootingGameSceneBuilder.cs` – the menu item that builds the scene
