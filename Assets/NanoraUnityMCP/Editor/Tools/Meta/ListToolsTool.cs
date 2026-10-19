using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools.Meta
{
    /// <summary>
    /// Meta-tool that lists tools in a specific category with brief descriptions.
    /// Part of the Token SuperSave system to reduce context token consumption.
    /// </summary>
    [McpTool("list_tools")]
    public class ListToolsTool : IMcpTool
    {
        public string Name => "list_tools";
        public string Description => "List the Unity tools in one category, one line each with a brief description. " +
            "Use it to find a tool name for execute. The briefs do not include parameter schemas.";

        public JObject InputSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["category"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray(ToolCategories.Categories.Keys),
                    ["description"] = "Category name, as listed by list_categories"
                }
            },
            ["required"] = new JArray("category")
        };

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var category = arguments?["category"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(category))
            {
                return Task.FromResult(McpToolResult.Error("Category is required"));
            }

            var toolList = ToolCategories.GetCategoryToolList(category);

            if (toolList == null)
            {
                var available = ToolCategories.GetCategorySummary();
                return Task.FromResult(McpToolResult.Error($"Unknown category: {category}. Available: {available}"));
            }

            return Task.FromResult(McpToolResult.Success(toolList));
        }
    }
}
