---
title: Domain reload and "state unknown" errors
description: Unity reloads scripts after compiling and when entering Play Mode, and the MCP server pauses for a few seconds. How nanora-unity-mcp reports it and how an AI should recover.
sidebar:
  order: 5
---

When Unity compiles or enters Play Mode it reloads all scripts (a *domain reload*). The server is part of the Editor, so it stops for a few seconds.

A request cut off by a reload gets a JSON-RPC error that says so. **The tool may already have finished.** So:

1. Wait until `/health` answers again (it also answers during compilation).
2. Read the state — [`mcp_status`](/nanora-unity-mcp/tools/mcp-status/), the [`unity://editor/state`](/nanora-unity-mcp/reference/endpoints/#resources) resource, or the `status` action of [`manage_playmode`](/nanora-unity-mcp/tools/manage-playmode/).
3. Only then decide whether to repeat the change. **Do not repeat a change blindly**; it may already have happened.

## Avoid the reload on Play

In *Project Settings > Editor > Enter Play Mode Settings*, untick *Reload Domain*. Entering Play Mode then no longer restarts the server. Your own scripts must then cope with static fields that are not reset.
