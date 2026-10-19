using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Interface for MCP tools.
    /// </summary>
    public interface IMcpTool
    {
        /// <summary>
        /// Unique name of the tool (e.g., "get_scene_hierarchy").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Human-readable description of what the tool does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// JSON Schema defining the input parameters.
        /// </summary>
        JObject InputSchema { get; }

        /// <summary>
        /// Executes the tool with the given arguments.
        /// </summary>
        Task<McpToolResult> ExecuteAsync(JObject arguments);
    }
}
