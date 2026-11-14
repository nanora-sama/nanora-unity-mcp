---
title: Install nanora-unity-mcp
description: Install the nanora-unity-mcp package in Unity 2021.3 or later from a Git URL, a tarball or a .unitypackage. Dependencies are installed automatically except with the .unitypackage.
sidebar:
  order: 1
---

nanora-unity-mcp is a Unity package. It needs **Unity 2021.3 LTS or later** (used daily on Unity 6) and nothing else. Input simulation needs the Input System package 1.4 or later.

## Choose a method

| Method | Steps | Best for |
|---|---|---|
| **Git URL** | *Window > Package Manager > **+** > Add package from git URL…* and paste the URL below | Always using the latest version |
| **Tarball (.tgz)** | Download `com.nanora.nanora-unity-mcp-<version>.tgz` from [Releases](https://github.com/nanora-sama/nanora-unity-mcp/releases) or BOOTH, then *Add package from tarball…* | Pinning one version (recommended) |
| **.unitypackage** | First *Add package by name…* `com.unity.nuget.newtonsoft-json`, then *Assets > Import Package > Custom Package…* | Keeping the files under Assets |

```text
https://github.com/nanora-sama/nanora-unity-mcp.git?path=Assets/NanoraUnityMCP
```

- The Git URL and the tarball also install the dependencies (Newtonsoft JSON, Test Framework, uGUI). The .unitypackage cannot, so install Newtonsoft JSON first. It also leaves out the sample.
- Keep the .tgz somewhere outside the project that you will not delete; Unity keeps referring to it.

## Check that the server is running

The server starts with the Editor. On the first start the **Setup Wizard** opens (*Window > Unity MCP > Setup Wizard*). *Window > Unity MCP Server* shows `Running` and the port.

```sh
curl http://127.0.0.1:<PORT>/health
```

If `projectPath` in the answer is your project, you are ready to [connect your AI](/connect/claude-code/).

## Recommended settings

- **Optimize for MCP (Recommended)** in the Setup Wizard keeps Unity answering while it is in the background. AI clients usually work from another window, so turn this on. The `optimize_for_mcp` action of [`configure_editor`](/tools/configure-editor/) does the same.
- Optional: untick *Reload Domain* in *Project Settings > Editor > Enter Play Mode Settings* so entering Play Mode does not pause the server. See [Domain reload](/guides/domain-reload/).

## Update or remove

- **Update** — Git URL: *Update* in the Package Manager. Tarball: pick the new .tgz the same way. .unitypackage: import the new one over the old.
- **Remove** — *Remove* in the Package Manager (.unitypackage: delete `Assets/NanoraUnityMCP`). Delete the generated `.mcp.json` and `.claude/skills/unity-*` if you no longer want them.
