using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("setup_status")]
    public class SetupStatusTool : IMcpTool
    {
        public string Name => "setup_status";
        public string Description => "Check all MCP client configuration health. Reports server status, client configs (Claude Desktop/Code/Cursor), permissions, and CLAUDE.md state.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {},
            ""required"": []
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                var report = ClientConfigManager.GetSetupStatus();
                return McpToolResult.Success(report);
            });
        }
    }
}
