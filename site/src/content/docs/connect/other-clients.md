---
title: Connect any MCP client
description: Any MCP client can use nanora-unity-mcp. Register http://127.0.0.1:<PORT>/mcp (Streamable HTTP) or /sse (SSE). Protocol versions 2024-11-05 to 2025-11-25.
sidebar:
  order: 5
---

nanora-unity-mcp listens on two transports on the same port. Register whichever your client supports:

| Transport | URL |
|---|---|
| Streamable HTTP | `http://127.0.0.1:<PORT>/mcp` |
| SSE | `http://127.0.0.1:<PORT>/sse` |

The server negotiates MCP protocol versions **2024-11-05 to 2025-11-25**. The port (51726–51825) comes from the project path and is shown in *Window > Unity MCP Server*; see [Several projects at once](/guides/multiple-projects/).

For scripts, `POST /message` takes a JSON-RPC request and returns the answer directly. All endpoints are listed in [Endpoints](/reference/endpoints/).

## Check the transport

```sh
node scripts/mcp/verify-transport.mjs
```

It checks the handshake, notifications, errors, the Origin check, SSE session isolation and a tool call (Node.js 20 or later, only for this check).
