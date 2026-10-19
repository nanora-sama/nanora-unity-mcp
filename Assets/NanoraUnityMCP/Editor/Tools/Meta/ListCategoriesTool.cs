using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools.Meta
{
    /// <summary>
    /// Meta-tool that lists all available tool categories.
    /// Part of the Token SuperSave system to reduce context token consumption.
    /// </summary>
    [McpTool("list_categories")]
    public class ListCategoriesTool : IMcpTool
    {
        public string Name => "list_categories";
        public string Description => "List the Unity tool categories with the number of tools in each (e.g. 'scene (5), query (4)'). " +
            "In SuperSave mode (NANORA_MCP_SUPERSAVE) these meta tools are the only ones exposed: call this first, then list_tools for a category, then execute.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {}
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var summary = ToolCategories.GetCategorySummary();
            return Task.FromResult(McpToolResult.Success(summary));
        }
    }
}
