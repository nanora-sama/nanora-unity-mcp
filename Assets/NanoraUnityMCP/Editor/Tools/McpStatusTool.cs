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
    /// Diagnostic tool for checking MCP server status and available tools.
    /// Use this to verify the connection is working.
    /// </summary>
    [McpTool("mcp_status")]
    public class McpStatusTool : IMcpTool
    {
        public string Name => "mcp_status";
        public string Description => "Get Unity MCP server status: version, project path, port and URLs, uptime, " +
            "and optionally the registered tool list and Unity system info. " +
            "Use this to confirm which project the server is attached to. Read-only; safe to retry.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""include_tool_list"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include full list of available tools"",
                    ""default"": true
                },
                ""include_system_info"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include Unity system information"",
                    ""default"": true
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                bool includeToolList = arguments?["include_tool_list"]?.Value<bool>() ?? true;
                bool includeSystemInfo = arguments?["include_system_info"]?.Value<bool>() ?? true;

                var sb = new StringBuilder();

                // Connection status
                sb.AppendLine("# Unity MCP Server Status");
                sb.AppendLine();
                sb.AppendLine("## Connection");
                sb.AppendLine($"- Status: ✓ Connected");
                sb.AppendLine($"- Version: {McpServer.Version}");
                sb.AppendLine($"- Project: {McpServer.ProjectPath}");
                sb.AppendLine($"- HTTP URL: http://127.0.0.1:{McpServer.Port}/mcp");
                sb.AppendLine($"- Legacy SSE URL: http://localhost:{McpServer.Port}/sse");
                sb.AppendLine($"- Port: {McpServer.Port}");
                sb.AppendLine($"- Running: {McpServer.IsRunning}");
                sb.AppendLine($"- Uptime: {FormatUptime(McpServer.Uptime)}");
                sb.AppendLine($"- Restart Count: {McpServer.RestartCount} (domain reloads)");
                sb.AppendLine($"- Is Compiling: {McpServer.IsCompiling}");
                sb.AppendLine($"- Has Uncompiled Changes: {McpServer.HasUncompiledChanges}");
                if (McpServer.LastCompilationTime.HasValue)
                {
                    sb.AppendLine($"- Last Compilation: {McpServer.LastCompilationTime.Value:HH:mm:ss}");
                }
                if (McpServer.HasUncompiledChanges)
                {
                    sb.AppendLine("  ⚠ Warning: Scripts have been modified since last compilation.");
                    sb.AppendLine("  → Use 'compile_project' tool or run Assets/Refresh before testing.");
                }
                sb.AppendLine();

                // Background execution settings
                sb.AppendLine("## Background Execution");
                sb.AppendLine($"- Application.runInBackground: {Application.runInBackground}");
                sb.AppendLine($"- PlayerSettings.runInBackground: {PlayerSettings.runInBackground}");
                if (!Application.runInBackground)
                {
                    sb.AppendLine("  ⚠ Warning: MCP may not respond when Unity loses focus.");
                    sb.AppendLine("  → Use 'configure_editor' tool to enable background execution.");
                }
                sb.AppendLine();

                // Unity Account status (for Asset Store / My Assets)
                sb.AppendLine("## Unity Account");
                bool isLoggedIn = !string.IsNullOrEmpty(CloudProjectSettings.accessToken);
                if (isLoggedIn)
                {
                    string userName = CloudProjectSettings.userName;
                    sb.AppendLine($"- Status: ✓ Logged in{(!string.IsNullOrEmpty(userName) ? $" ({userName})" : "")}");
                    sb.AppendLine("- My Assets: Available");
                }
                else
                {
                    sb.AppendLine("- Status: ❌ Not logged in");
                    sb.AppendLine("- My Assets: Unavailable (sign in via Unity Hub)");
                }
                sb.AppendLine();

                // System info
                if (includeSystemInfo)
                {
                    sb.AppendLine("## Unity Info");
                    sb.AppendLine($"- Unity Version: {Application.unityVersion}");
                    sb.AppendLine($"- Platform: {Application.platform}");
                    sb.AppendLine($"- Project: {McpServer.ProjectName}");
                    sb.AppendLine($"- Project Path: {McpServer.ProjectPath}");
                    sb.AppendLine($"- Data Path: {Application.dataPath}");
                    sb.AppendLine();
                }

                // Tool list
                if (includeToolList)
                {
                    var tools = McpServer.GetRegisteredTools();
                    sb.AppendLine($"## Available Tools ({tools.Length})");
                    sb.AppendLine();

                    foreach (var tool in tools)
                    {
                        sb.AppendLine($"- `{tool}`");
                    }
                    sb.AppendLine();

                    // Categories
                    sb.AppendLine("## Tool Categories");
                    sb.AppendLine("- **Scene**: get_scene_hierarchy, get_object_details, create_game_object, modify_game_object, delete_game_object");
                    sb.AppendLine("- **Query**: query_objects, search_assets, wait_for_selector");
                    sb.AppendLine("- **Code**: execute_csharp, execute_menu_item, compile_project, get_compilation_errors");
                    sb.AppendLine("- **Test**: run_tests, get_test_results, simulate_input, capture_screenshot");
                    sb.AppendLine("- **Performance**: analyze_frame, analyze_memory, profile_cpu, get_profiler_stats");
                    sb.AppendLine("- **Batch**: batch_modify, destroy_objects, set_active, instantiate_asset");
                    sb.AppendLine("- **Diagnostics**: diagnose_materials, repair_materials, get_console_logs, mcp_status");
                }

                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine($"*Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");

                var result = McpToolResult.Success(sb.ToString());
                result.StructuredContent = JObject.FromObject(new
                {
                    project = McpServer.ProjectName, projectPath = McpServer.ProjectPath,
                    version = McpServer.Version, isRunning = McpServer.IsRunning,
                    isCompiling = EditorApplication.isCompiling, compilationFailed = EditorUtility.scriptCompilationFailed,
                    isPlaying = EditorApplication.isPlaying, isPaused = EditorApplication.isPaused,
                    toolCount = McpServer.GetRegisteredTools().Length, restartCount = McpServer.RestartCount
                });
                return result;
            });
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalSeconds < 60)
            {
                return $"{uptime.TotalSeconds:F0} seconds";
            }
            else if (uptime.TotalMinutes < 60)
            {
                return $"{uptime.TotalMinutes:F1} minutes";
            }
            else
            {
                return $"{uptime.TotalHours:F1} hours";
            }
        }
    }
}
