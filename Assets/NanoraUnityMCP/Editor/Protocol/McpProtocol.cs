using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using NanoraUnityMCP.Core;

namespace NanoraUnityMCP.Protocol
{
    /// <summary>
    /// Handles MCP protocol message processing.
    /// </summary>
    public class McpProtocol
    {
        private readonly ToolRegistry m_toolRegistry;
        private readonly JsonSerializerSettings m_jsonSettings;
        public const string ProtocolVersion = "2025-11-25";

        public static bool IsSupportedVersion(string version) =>
            version == "2024-11-05" || version == "2025-03-26" || version == "2025-06-18" || version == ProtocolVersion;

        public McpProtocol(ToolRegistry toolRegistry)
        {
            m_toolRegistry = toolRegistry;
            m_jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public async Task<string> ProcessMessageAsync(string message)
        {
            try
            {
                var token = JToken.Parse(message);
                if (!(token is JObject envelope) || (string)envelope["jsonrpc"] != "2.0" ||
                    envelope["method"]?.Type != JTokenType.String ||
                    (envelope["id"] != null && envelope["id"].Type != JTokenType.String &&
                     envelope["id"].Type != JTokenType.Integer && envelope["id"].Type != JTokenType.Null))
                    return SerializeResponse(JsonRpcResponse.CreateError(null,
                        JsonRpcErrorCodes.InvalidRequest, "Invalid JSON-RPC 2.0 request"));

                var request = envelope.ToObject<JsonRpcRequest>();

                if (request == null || string.IsNullOrEmpty(request.Method))
                {
                    return SerializeResponse(JsonRpcResponse.CreateError(
                        null,
                        JsonRpcErrorCodes.InvalidRequest,
                        "Invalid request"));
                }

                // Notifications never have replies, including unknown notifications.
                // No per-client initialization state is needed for stateless HTTP.
                if (request.IsNotification) return null;
                if (request.Params != null && request.Params.Type != JTokenType.Object)
                    return SerializeResponse(JsonRpcResponse.CreateError(request.Id,
                        JsonRpcErrorCodes.InvalidParams, "Params must be an object"));

                var response = await HandleMethodAsync(request);
                return SerializeResponse(response);
            }
            catch (JsonException)
            {
                return SerializeResponse(JsonRpcResponse.CreateError(
                    null,
                    JsonRpcErrorCodes.ParseError,
                    "Parse error"));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Processing error: {ex.Message}");
                return SerializeResponse(JsonRpcResponse.CreateError(
                    null,
                    JsonRpcErrorCodes.InternalError,
                    ex.Message));
            }
        }

        private async Task<JsonRpcResponse> HandleMethodAsync(JsonRpcRequest request)
        {
            switch (request.Method)
            {
                case "initialize":
                    return HandleInitialize(request);

                case "tools/list":
                    return HandleToolsList(request);

                case "resources/list":
                    return JsonRpcResponse.Success(request.Id, EditorResources.List());

                case "resources/templates/list":
                    return JsonRpcResponse.Success(request.Id, new { resourceTemplates = new object[0] });

                case "resources/read":
                    return await EditorResources.ReadAsync(request);

                case "tools/call":
                    return await HandleToolsCallAsync(request);

                case "ping":
                    return JsonRpcResponse.Success(request.Id, new { });

                default:
                    return JsonRpcResponse.CreateError(
                        request.Id,
                        JsonRpcErrorCodes.MethodNotFound,
                        $"Method not found: {request.Method}");
            }
        }

        private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
        {
            var result = new McpInitializeResult
            {
                ProtocolVersion = IsSupportedVersion((string)request.Params?["protocolVersion"])
                    ? (string)request.Params["protocolVersion"] : ProtocolVersion,
                Capabilities = new McpServerCapabilities
                {
                    Tools = new McpToolsCapability { ListChanged = false }
                },
                ServerInfo = new McpServerInfo
                {
                    Name = McpServer.ServerName,
                    Version = McpServer.Version
                },
                Instructions =
                        $"This MCP server is serving Unity project \"{McpServer.ProjectName}\" " +
                        $"at \"{McpServer.ProjectPath}\". " +
                        "If this does not match your current working directory, you are connected to the wrong Unity Editor " +
                        "instance: tell the user and suggest restarting the session instead of editing. " +
                        "Read unity://editor/state for compile/play state and unsaved scenes. " +
                        "Scene edits made by tools are undoable but are not saved to disk until manage_scene save; " +
                        "edits made during Play Mode are lost when Play Mode exits. " +
                        "Compiling, entering Play Mode, and some menu items reload the script domain, which drops the " +
                        $"connection for several seconds; poll http://127.0.0.1:{McpServer.Port}/health until it responds before continuing. " +
                        "Never blindly replay a mutation after a connection failure: it may have completed, so check state first."
            };

            return JsonRpcResponse.Success(request.Id, result);
        }

        private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
        {
            var tools = m_toolRegistry.GetToolDefinitions();
            var result = new McpToolsListResult { Tools = tools };
            return JsonRpcResponse.Success(request.Id, result);
        }

        private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
        {
            try
            {
                if (request.Params == null)
                {
                    return JsonRpcResponse.CreateError(
                        request.Id,
                        JsonRpcErrorCodes.InvalidParams,
                        "Missing params");
                }

                var callParams = request.Params.ToObject<McpToolCallParams>();

                if (string.IsNullOrEmpty(callParams?.Name))
                {
                    return JsonRpcResponse.CreateError(
                        request.Id,
                        JsonRpcErrorCodes.InvalidParams,
                        "Missing tool name");
                }

                var tool = m_toolRegistry.GetTool(callParams.Name);
                if (tool == null)
                {
                    return JsonRpcResponse.CreateError(
                        request.Id,
                        JsonRpcErrorCodes.InvalidParams,
                        $"Tool not found: {callParams.Name}");
                }

                // Check compilation state and add note if needed
                // Note: These are informational only - the tool has completed successfully
                string compilationNote = null;
                if (McpServer.IsCompiling)
                {
                    compilationNote = "ℹ️ Note: Unity is currently compiling. This result was obtained successfully, but code-dependent data may change after compilation completes.";
                }
                else if (McpServer.HasUncompiledChanges)
                {
                    compilationNote = "ℹ️ Note: Script files have been modified since last compilation. Consider running 'Assets/Refresh' or compile_project if you need the latest behavior.";
                }
                else if (McpServer.LastCompilationTime.HasValue)
                {
                    var timeSinceCompile = DateTime.Now - McpServer.LastCompilationTime.Value;
                    if (timeSinceCompile.TotalSeconds < 3)
                    {
                        compilationNote = $"ℹ️ Note: Compilation completed {timeSinceCompile.TotalSeconds:F1}s ago. This result reflects the newly compiled code.";
                    }
                }

                // Execute tool
                var result = await tool.ExecuteAsync(callParams.Arguments);

                // Prepend compilation note if present (only for text content)
                if (!string.IsNullOrEmpty(compilationNote) && !result.IsError &&
                    result.Content.Count > 0 && result.Content[0].Type == "text")
                {
                    var originalText = result.Content[0].Text ?? "";
                    result.Content[0].Text = $"{compilationNote}\n\n---\n\n{originalText}";
                }

                return JsonRpcResponse.Success(request.Id, result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Tool execution error: {ex.Message}");
                return JsonRpcResponse.Success(request.Id, McpToolResult.Error(ex.Message));
            }
        }

        private string SerializeResponse(JsonRpcResponse response)
        {
            if (response == null) return null;
            return JsonConvert.SerializeObject(response, m_jsonSettings);
        }
    }
}
