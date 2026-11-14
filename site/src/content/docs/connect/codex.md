---
title: Connect Codex to Unity
description: Use OpenAI Codex with the Unity Editor through nanora-unity-mcp over Streamable HTTP. Register http://127.0.0.1:<PORT>/mcp with codex mcp add.
sidebar:
  order: 2
---

Codex connects over **Streamable HTTP**, which nanora-unity-mcp serves at `/mcp` on the same port as SSE.

```sh
codex mcp add nanora-unity-mcp --url http://127.0.0.1:<PORT>/mcp
```

Or add it to `~/.codex/config.toml`:

```toml
[mcp_servers.nanora-unity-mcp]
url = "http://127.0.0.1:<PORT>/mcp"
```

Start a new Codex session afterwards; the setting is read when a session starts. *Window > Unity MCP Server* shows the port and has a **Copy Codex setup command** button that copies the command above with the current port.

## Check the connection

Node.js 20 or later is needed only for this check:

```sh
node scripts/mcp/verify-codex.mjs
```

It confirms that the installed Codex client can list and call the tools.

## If it does not appear

- The URL must end in `/mcp` and use the current port.
- The [`/mcp` endpoint](/reference/endpoints/) is stateless and answers in JSON; notifications get `202`.
