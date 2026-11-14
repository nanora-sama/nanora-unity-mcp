---
title: Security
description: nanora-unity-mcp listens only on 127.0.0.1, refuses requests from other browser origins and answers only the client that asked. execute_csharp runs code with your rights.
sidebar:
  order: 6
---

- **Local only.** The server listens on the loopback address (127.0.0.1) only. Keep it that way.
- **No requests from other web pages.** Requests with a foreign `Origin` are refused.
- **Answers go to the client that asked.** Each SSE connection is its own session.

## Code execution

[`execute_csharp`](/tools/execute-csharp/) and [`execute_menu_item`](/tools/execute-menu-item/) run code in the Editor with your rights. `execute_csharp` blocks some APIs (starting processes, web clients) and stops endless loops, but **it is not a sandbox**. Review what your AI client is allowed to call.

Report vulnerabilities privately as described in [SECURITY.md](https://github.com/nanora-sama/nanora-unity-mcp/blob/main/SECURITY.md).
