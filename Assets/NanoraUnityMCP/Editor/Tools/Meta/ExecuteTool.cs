using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools.Meta
{
    /// <summary>
    /// Meta-tool that executes other tools by name.
    /// Part of the Token SuperSave system to reduce context token consumption.
    /// </summary>
    [McpTool("execute")]
    public class ExecuteTool : IMcpTool
    {
        private ToolRegistry m_registry;

        public string Name => "execute";
        public string Description => "Execute any registered tool by name with its arguments object (args), returning that tool's result. " +
            "Use it for tools not loaded directly; find names with list_categories and list_tools. " +
            "The meta tools (execute, list_categories, list_tools) cannot be called through it; an unknown name returns an error naming the tool's category when known.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""tool"": {
                    ""type"": ""string"",
                    ""description"": ""Tool name to execute""
                },
                ""args"": {
                    ""type"": ""object"",
                    ""description"": ""Arguments to pass to the tool""
                }
            },
            ""required"": [""tool""]
        }");

        /// <summary>
        /// Sets the tool registry for executing tools.
        /// Called by ToolRegistry after instantiation.
        /// </summary>
        public void SetRegistry(ToolRegistry registry)
        {
            m_registry = registry;
        }

        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var toolName = arguments?["tool"]?.Value<string>();
            var args = arguments?["args"] as JObject ?? new JObject();

            if (string.IsNullOrWhiteSpace(toolName))
            {
                return McpToolResult.Error("Tool name is required");
            }

            if (m_registry == null)
            {
                return McpToolResult.Error("Tool registry not initialized");
            }

            // Prevent recursive execution of meta tools
            if (toolName == "execute" || toolName == "list_categories" || toolName == "list_tools")
            {
                return McpToolResult.Error($"Cannot execute meta-tool '{toolName}' via execute");
            }

            var tool = m_registry.GetTool(toolName);
            if (tool == null)
            {
                var category = ToolCategories.GetCategoryForTool(toolName);
                if (category != null)
                {
                    return McpToolResult.Error($"Tool '{toolName}' not found. It belongs to category '{category}'. Use list_tools to see available tools.");
                }
                return McpToolResult.Error($"Tool '{toolName}' not found. Use list_categories to see available categories.");
            }

            return await tool.ExecuteAsync(args);
        }
    }
}
