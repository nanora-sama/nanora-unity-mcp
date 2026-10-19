using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("reconfigure_client")]
    public class ReconfigureClientTool : IMcpTool
    {
        public string Name => "reconfigure_client";
        public string Description => "Fix or update MCP client configurations. Can reconfigure Claude Desktop, Claude Code, Cursor, or all detected clients at once.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""client"": {
                    ""type"": ""string"",
                    ""description"": ""Which client to reconfigure"",
                    ""enum"": [""claude_desktop"", ""claude_code"", ""cursor"", ""all""]
                },
                ""dry_run"": {
                    ""type"": ""boolean"",
                    ""description"": ""If true, only check what would change without modifying files"",
                    ""default"": false
                }
            },
            ""required"": [""client""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() => Execute(arguments));
        }

        private McpToolResult Execute(JObject args)
        {
            var clientStr = args?["client"]?.Value<string>();
            var dryRun = args?["dry_run"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(clientStr))
            {
                return McpToolResult.Error("Missing required parameter: client");
            }

            var sb = new StringBuilder();
            sb.AppendLine(dryRun ? "# Reconfigure (Dry Run)" : "# Reconfigure Results");
            sb.AppendLine();

            if (clientStr == "all")
            {
                var results = ClientConfigManager.ConfigureAllDetected(dryRun);
                foreach (var kvp in results)
                {
                    string icon = kvp.Value == ConfigResult.Configured || kvp.Value == ConfigResult.AlreadyUpToDate ? "OK" :
                                  kvp.Value == ConfigResult.NotInstalled ? "SKIP" : "FAIL";
                    sb.AppendLine($"- [{icon}] {ClientConfigManager.GetConfigResultMessage(kvp.Key, kvp.Value)}");
                }
            }
            else
            {
                ClientType type;
                switch (clientStr)
                {
                    case "claude_desktop":
                        type = ClientType.ClaudeDesktop;
                        break;
                    case "claude_code":
                        type = ClientType.ClaudeCode;
                        break;
                    case "cursor":
                        type = ClientType.Cursor;
                        break;
                    default:
                        return McpToolResult.Error($"Invalid client: {clientStr}. Must be one of: claude_desktop, claude_code, cursor, all");
                }

                var result = ClientConfigManager.ConfigureClient(type, dryRun);
                string icon = result == ConfigResult.Configured || result == ConfigResult.AlreadyUpToDate ? "OK" :
                              result == ConfigResult.NotInstalled ? "SKIP" : "FAIL";
                sb.AppendLine($"- [{icon}] {ClientConfigManager.GetConfigResultMessage(type, result)}");
            }

            return McpToolResult.Success(sb.ToString());
        }
    }
}
