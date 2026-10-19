using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Applies saved editor settings on Unity startup.
    /// </summary>
    [InitializeOnLoad]
    internal static class ConfigureEditorStartup
    {
        private const string k_RunInBackgroundKey = "UnityMCP_RunInBackground";

        static ConfigureEditorStartup()
        {
            // Apply saved background setting on startup
            if (EditorPrefs.GetBool(k_RunInBackgroundKey, false))
            {
                Application.runInBackground = true;
            }
        }

        public static void SetRunInBackground(bool enabled)
        {
            EditorPrefs.SetBool(k_RunInBackgroundKey, enabled);
            Application.runInBackground = enabled;
        }

        public static bool GetRunInBackground()
        {
            return EditorPrefs.GetBool(k_RunInBackgroundKey, false);
        }
    }

    /// <summary>
    /// Configures Unity Editor settings for optimal MCP operation.
    /// Includes enabling background execution to keep MCP responsive when Unity loses focus.
    /// </summary>
    [McpTool("configure_editor")]
    public class ConfigureEditorTool : IMcpTool
    {
        public string Name => "configure_editor";
        public string Description => "Configure Unity Editor settings for optimal MCP operation. " +
            "Use this to enable background execution (runInBackground) so MCP responds even when Unity is not focused.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""status"", ""enable_background"", ""disable_background"", ""optimize_for_mcp""],
                    ""description"": ""Action to perform: status (show current settings), enable_background (enable runInBackground), disable_background, optimize_for_mcp (apply all recommended settings)""
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                var action = arguments?["action"]?.Value<string>();

                if (string.IsNullOrEmpty(action))
                {
                    return McpToolResult.Error("Action is required");
                }

                switch (action.ToLower())
                {
                    case "status":
                        return GetStatus();
                    case "enable_background":
                        return EnableBackground();
                    case "disable_background":
                        return DisableBackground();
                    case "optimize_for_mcp":
                        return OptimizeForMcp();
                    default:
                        return McpToolResult.Error($"Unknown action: {action}. Valid actions: status, enable_background, disable_background, optimize_for_mcp");
                }
            });
        }

        private McpToolResult GetStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Editor Configuration Status");
            sb.AppendLine();

            sb.AppendLine("## Background Execution");
            sb.AppendLine($"- Application.runInBackground: {Application.runInBackground}");
            sb.AppendLine($"- Persistent setting (EditorPrefs): {ConfigureEditorStartup.GetRunInBackground()}");
            sb.AppendLine($"- PlayerSettings.runInBackground: {PlayerSettings.runInBackground}");
            sb.AppendLine();

            if (!Application.runInBackground)
            {
                sb.AppendLine("⚠ **Warning**: Background execution is disabled.");
                sb.AppendLine("MCP server may not respond when Unity loses focus.");
                sb.AppendLine();
                sb.AppendLine("→ Use action 'enable_background' to fix this.");
            }
            else
            {
                sb.AppendLine("✓ Background execution is enabled.");
                sb.AppendLine("MCP server will respond even when Unity is not focused.");
                if (ConfigureEditorStartup.GetRunInBackground())
                {
                    sb.AppendLine("✓ Setting is persisted - will apply automatically on Unity restart.");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## Other Settings");
            sb.AppendLine($"- MCP Auto-start: {McpServer.GetAutoStart()}");
            sb.AppendLine($"- MCP Port: {McpServer.Port}");

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult EnableBackground()
        {
            var previousApp = Application.runInBackground;
            var previousPersistent = ConfigureEditorStartup.GetRunInBackground();

            // Enable and persist the setting
            ConfigureEditorStartup.SetRunInBackground(true);
            PlayerSettings.runInBackground = true;

            // Save player settings
            AssetDatabase.SaveAssets();

            var sb = new StringBuilder();
            sb.AppendLine("# Background Execution Enabled");
            sb.AppendLine();
            sb.AppendLine("## Changes Applied:");
            sb.AppendLine($"- Application.runInBackground: {previousApp} → {Application.runInBackground}");
            sb.AppendLine($"- Persistent setting (EditorPrefs): {previousPersistent} → true");
            sb.AppendLine($"- PlayerSettings.runInBackground: → true (for builds)");
            sb.AppendLine();
            sb.AppendLine("✓ Unity will now continue processing in the background.");
            sb.AppendLine("✓ MCP server will respond even when Unity loses focus.");
            sb.AppendLine("✓ **Setting is persisted** - will apply automatically on Unity restart.");

            Debug.Log("[UnityMCP] Background execution enabled for better MCP responsiveness (persistent)");

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult DisableBackground()
        {
            var previousApp = Application.runInBackground;
            var previousPersistent = ConfigureEditorStartup.GetRunInBackground();

            // Disable and clear the persistent setting
            ConfigureEditorStartup.SetRunInBackground(false);
            PlayerSettings.runInBackground = false;

            AssetDatabase.SaveAssets();

            var sb = new StringBuilder();
            sb.AppendLine("# Background Execution Disabled");
            sb.AppendLine();
            sb.AppendLine("## Changes Applied:");
            sb.AppendLine($"- Application.runInBackground: {previousApp} → {Application.runInBackground}");
            sb.AppendLine($"- Persistent setting (EditorPrefs): {previousPersistent} → false");
            sb.AppendLine();
            sb.AppendLine("⚠ Warning: MCP server may not respond when Unity loses focus.");

            Debug.Log("[UnityMCP] Background execution disabled");

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult OptimizeForMcp()
        {
            var changes = new StringBuilder();
            changes.AppendLine("# MCP Optimization Applied");
            changes.AppendLine();
            changes.AppendLine("## Changes:");

            // 1. Enable background execution (with persistence)
            if (!ConfigureEditorStartup.GetRunInBackground())
            {
                ConfigureEditorStartup.SetRunInBackground(true);
                changes.AppendLine("✓ Background execution: enabled (persistent)");
            }
            else
            {
                changes.AppendLine("- Background execution: already enabled");
            }

            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
                changes.AppendLine("✓ PlayerSettings.runInBackground: enabled (for builds)");
            }
            else
            {
                changes.AppendLine("- PlayerSettings.runInBackground: already enabled");
            }

            // 2. Ensure MCP auto-start is enabled
            if (!McpServer.GetAutoStart())
            {
                McpServer.SetAutoStart(true);
                changes.AppendLine("✓ MCP Auto-start: enabled");
            }
            else
            {
                changes.AppendLine("- MCP Auto-start: already enabled");
            }

            // Save player settings
            AssetDatabase.SaveAssets();

            changes.AppendLine();
            changes.AppendLine("## Result:");
            changes.AppendLine("✓ Unity Editor is now optimized for MCP operation.");
            changes.AppendLine("✓ All settings are **persisted** and will apply on Unity restart.");
            changes.AppendLine();
            changes.AppendLine("The MCP server will now:");
            changes.AppendLine("- Start automatically when Unity opens");
            changes.AppendLine("- Respond even when Unity is in the background");
            changes.AppendLine("- Restart quickly after script compilation");

            Debug.Log("[UnityMCP] Editor optimized for MCP operation (persistent)");

            return McpToolResult.Success(changes.ToString());
        }
    }
}
