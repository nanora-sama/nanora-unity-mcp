---
title: Endpoints and resources
description: HTTP endpoints of nanora-unity-mcp — Streamable HTTP at /mcp, SSE at /sse, JSON-RPC at /message, /health — and the MCP resources unity://project/info and unity://editor/state.
sidebar:
  order: 2
---

All endpoints are on `http://127.0.0.1:<PORT>`. The port is shown in *Window > Unity MCP Server*.

| Path | Use |
|---|---|
| `POST /mcp` | Streamable HTTP. Stateless; answers in JSON. Notifications get `202`. Use this for Codex and other Streamable HTTP clients |
| `GET /sse` + `POST /message?sessionId=…` | Classic SSE. Each connection is its own session |
| `POST /message` | A JSON-RPC request and its answer, directly — handy for scripts |
| `GET /health` | Project name and path, process ID, package version, protocol version and transports. Answers during compilation too |

MCP protocol versions **2024-11-05 to 2025-11-25** are negotiated.

## Resources

Read-only MCP resources the AI can read at any time:

| URI | Contents |
|---|---|
| `unity://project/info` | Which project this server belongs to |
| `unity://editor/state` | Compile failures, Play / paused state, scenes being loaded and unsaved scenes |

Read `unity://editor/state` after a [domain reload](/nanora-unity-mcp/guides/domain-reload/) before repeating a change.

## Calling a tool from a script

```sh
curl -s http://127.0.0.1:<PORT>/message \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"mcp_status","arguments":{}}}'
```
