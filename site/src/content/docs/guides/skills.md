---
title: Bundled skills for Claude Code
description: nanora-unity-mcp installs Claude Code skills that make the AI verify its Unity work — verify-applied, pre-PR checks, E2E smoke tests, material repair, performance budgets and scene setup.
sidebar:
  order: 2
---

The package ships six Claude Code skills. When the Claude Code connection is configured they are copied to `<Project>/.claude/skills/`. Updates keep your own edits; [`install_skills`](/nanora-unity-mcp/tools/install-skills/) with `force: true` overwrites them.

| Skill | What it does |
|---|---|
| `/unity-verify-applied` | Before saying "done", checks the saved files, the running state and the visible result |
| `/unity-pre-pr-check` | Before a PR, checks the revision, tests, saved state and visible behaviour |
| `/unity-e2e-test` | A Play Mode smoke test: wait, input, assert, screenshot |
| `/unity-material-repair` | Finds and fixes pink materials, with before and after screenshots |
| `/unity-performance-budget` | Compares FPS, draw calls and memory with a budget |
| `/unity-scene-setup` | Creates a new scene with lights, a camera and ground |

Skills are a Claude Code feature. Other clients can use the same tools directly; the skills are plain Markdown, so you can read them as checklists.
