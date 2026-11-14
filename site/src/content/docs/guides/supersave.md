---
title: SuperSave mode (fewer tools in context)
description: Start Unity with NANORA_MCP_SUPERSAVE=1 and nanora-unity-mcp exposes only three meta tools, so the full tool list does not fill the AI's context.
sidebar:
  order: 3
---

Every tool's name and schema goes into the AI's context. With 57 tools that is a lot of text. **SuperSave mode** shows the client only three meta tools instead:

1. [`list_categories`](/nanora-unity-mcp/tools/list-categories/) — the categories
2. [`list_tools`](/nanora-unity-mcp/tools/list-tools/) — the tools in one category, one line each
3. [`execute`](/nanora-unity-mcp/tools/execute/) — call a tool by name: `{"tool": "capture_screenshot", "args": { … }}`

## Turn it on

Set the environment variable to `1` or `true` before Unity starts:

```powershell
# PowerShell: start Unity from the same window afterwards
$env:NANORA_MCP_SUPERSAVE = "1"
```

```sh
# macOS / Linux
NANORA_MCP_SUPERSAVE=1 /path/to/Unity -projectPath .
```

The Console shows `[UnityMCP] SuperSave mode enabled - only meta-tools will be exposed`.

`list_tools` returns short descriptions without parameter schemas. The parameters of every tool are on its page in the [tool reference](/nanora-unity-mcp/tools/).
