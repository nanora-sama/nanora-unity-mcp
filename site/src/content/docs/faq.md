---
title: FAQ
description: Frequently asked questions about nanora-unity-mcp — price and license, supported Unity versions and AI clients, what needs to be installed, and how it differs from other Unity MCP servers.
---

## Is it free?

Yes. nanora-unity-mcp is open source under the [MIT license](https://github.com/nanora-sama/nanora-unity-mcp/blob/main/LICENSE). It is also on BOOTH for free; the paid "support" variant there has the same contents.

## Which Unity versions are supported?

Unity 2021.3 LTS or later, including Unity 6.

## Which AI clients work?

Any MCP client that speaks Streamable HTTP or SSE. It is tested with Claude Code, Codex, Cursor and Claude Desktop. See [Connect any MCP client](/nanora-unity-mcp/connect/other-clients/).

## Do I need Node.js or Python?

No. The server is C# that runs inside the Unity Editor. Node.js is used only by the optional [transport checks](/nanora-unity-mcp/connect/other-clients/#check-the-transport).

## Does it send my project anywhere?

The server only listens on 127.0.0.1. What your AI client sends to its own provider is up to that client. See [Security](/nanora-unity-mcp/guides/security/).

## How many tools are there?

57 tools in 18 categories, plus three meta tools for [SuperSave mode](/nanora-unity-mcp/guides/supersave/). They are all in the [tool reference](/nanora-unity-mcp/tools/).

## Can the AI test what it built?

Yes — that is the point of the testing tools. See [Let the AI test your game](/nanora-unity-mcp/guides/testing/).

## Does it work in a built player?

No. It is an Editor tool; the server runs in the Unity Editor.

## How is it different from other Unity MCP servers?

The main difference is what you need besides Unity. See the [comparison](/nanora-unity-mcp/comparison/).

## Where do I ask questions?

[GitHub Issues](https://github.com/nanora-sama/nanora-unity-mcp/issues/new/choose). Japanese is fine. Help is best effort; it is maintained by one person.
