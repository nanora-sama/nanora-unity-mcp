using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NanoraUnityMCP.Protocol
{
    /// <summary>
    /// MCP Tool definition.
    /// </summary>
    public class McpToolDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("inputSchema")]
        public JObject InputSchema { get; set; }
    }

    /// <summary>
    /// MCP Tool call request parameters.
    /// </summary>
    public class McpToolCallParams
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public JObject Arguments { get; set; }
    }

    /// <summary>
    /// MCP Content item (text, image, etc.)
    /// </summary>
    public class McpContent
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public string Data { get; set; }

        [JsonProperty("mimeType", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeType { get; set; }

        public static McpContent TextContent(string text)
        {
            return new McpContent { Type = "text", Text = text };
        }

        public static McpContent ImageContent(string base64Data, string mimeType = "image/png")
        {
            return new McpContent { Type = "image", Data = base64Data, MimeType = mimeType };
        }
    }

    /// <summary>
    /// MCP Tool call result.
    /// </summary>
    public class McpToolResult
    {
        [JsonProperty("structuredContent", NullValueHandling = NullValueHandling.Ignore)]
        public JObject StructuredContent { get; set; }

        [JsonProperty("content")]
        public List<McpContent> Content { get; set; } = new List<McpContent>();

        [JsonProperty("isError", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsError { get; set; }

        public static McpToolResult Success(string text)
        {
            return new McpToolResult
            {
                Content = new List<McpContent> { McpContent.TextContent(text) }
            };
        }

        public static McpToolResult Error(string message)
        {
            return new McpToolResult
            {
                Content = new List<McpContent> { McpContent.TextContent(message) },
                IsError = true
            };
        }
    }

    /// <summary>
    /// MCP Server capabilities.
    /// </summary>
    public class McpServerCapabilities
    {
        [JsonProperty("resources")]
        public JObject Resources { get; set; } = new JObject();

        [JsonProperty("tools")]
        public McpToolsCapability Tools { get; set; } = new McpToolsCapability();
    }

    public class McpToolsCapability
    {
        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; } = false;
    }

    /// <summary>
    /// MCP Server info.
    /// </summary>
    public class McpServerInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

    }

    /// <summary>
    /// MCP Initialize result.
    /// </summary>
    public class McpInitializeResult
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2024-11-05";

        [JsonProperty("capabilities")]
        public McpServerCapabilities Capabilities { get; set; } = new McpServerCapabilities();

        [JsonProperty("serverInfo")]
        public McpServerInfo ServerInfo { get; set; }

        /// <summary>
        /// Instructions for AI clients on how to use this server.
        /// MCP clients read this top-level field of the initialize result, not serverInfo.
        /// </summary>
        [JsonProperty("instructions", NullValueHandling = NullValueHandling.Ignore)]
        public string Instructions { get; set; }
    }

    /// <summary>
    /// MCP Tools list result.
    /// </summary>
    public class McpToolsListResult
    {
        [JsonProperty("tools")]
        public List<McpToolDefinition> Tools { get; set; } = new List<McpToolDefinition>();
    }
}
