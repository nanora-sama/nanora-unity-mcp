---
name: unity-performance-budget
description: Check a Unity scene against a performance budget (FPS, draw calls, memory). Use when the user asks "is this fast enough?", "check FPS", "profile this scene", or mentions performance targets.
---

# Unity Performance Budget Check

Profile the active scene against a configurable budget.

## Before Starting

Confirm or infer the budget (default: mid-tier mobile):
- Target FPS: `60`
- Max draw calls: `200`
- Max tris rendered: `200000`
- Max managed heap MB: `256`
- Duration to sample: `10s`

## Steps

1. **Enter Play Mode** to get runtime numbers (editor numbers lie):
   - Tool: `mcp__nanora-unity-mcp__manage_playmode` with `action: "status"` first; if not already playing, call it with `action: "play"`.
   - Wait for first frame.

2. **Warm-up** — let the scene settle ~2s before measuring (avoid first-frame spikes).

3. **Run budget test**:
   - Tool: `mcp__nanora-unity-mcp__performance_budget_test` with the thresholds above and the sample duration.
   - This samples FPS/frame time/draw-call averages over the duration and reports pass/fail per metric.

4. **Capture profiler stats** for breakdown:
   - Tool: `mcp__nanora-unity-mcp__get_profiler_stats`
   - Note top categories: Rendering, Scripts, Physics, Animation.

5. **Analyze frame** if anything is over budget:
   - Tool: `mcp__nanora-unity-mcp__analyze_frame` — identifies hot draw calls / overdraw.
   - Tool: `mcp__nanora-unity-mcp__analyze_memory` — surfaces high allocators.

6. **CPU profile** for script-side hotspots (skip if GPU-bound):
   - Tool: `mcp__nanora-unity-mcp__profile_cpu` with a short duration.

7. **Exit Play Mode**:
   - Tool: `mcp__nanora-unity-mcp__manage_playmode` with `action: "stop"` — run this even on failure, if this run started Play Mode.

8. **Report** — table format:
   - `Metric | Budget | Actual | Verdict`
   - FPS / Frame time / Draw calls / Tris / Managed heap
   - Call out the top 3 hotspots from analyze_frame/profile_cpu
   - Verdict: **PASS** if all within budget, else **OVER** with specific offenders.

## Notes

- If the user did NOT provide a budget, ask once — do not guess silently for shipping-critical calls.
- Warn if running in Editor with low-quality settings: numbers will differ from standalone builds.
- For authoritative shipping benchmarks, advise building a standalone player (outside this skill's scope).
