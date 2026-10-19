using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Batch execution tool that runs multiple MCP tool calls in a single request.
    /// Supports atomic mode with Undo grouping: all operations collapse into one undo step,
    /// with automatic rollback on failure.
    /// </summary>
    [McpTool("batch_execute")]
    public class BatchExecuteTool : IMcpTool
    {
        private ToolRegistry m_registry;

        public string Name => "batch_execute";
        public string Description => "Execute multiple MCP tool calls in a single request. " +
            "Supports atomic mode (all operations as one undo group with rollback on failure).";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""commands"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""tool"": {
                                ""type"": ""string"",
                                ""description"": ""Tool name to execute""
                            },
                            ""arguments"": {
                                ""type"": ""object"",
                                ""description"": ""Arguments to pass to the tool""
                            }
                        },
                        ""required"": [""tool""]
                    },
                    ""description"": ""List of tool calls to execute sequentially""
                },
                ""atomic"": {
                    ""type"": ""boolean"",
                    ""description"": ""If true, all operations are grouped as a single undo step with rollback on failure"",
                    ""default"": true
                },
                ""stop_on_error"": {
                    ""type"": ""boolean"",
                    ""description"": ""If true, stop execution on first error"",
                    ""default"": true
                }
            },
            ""required"": [""commands""]
        }");

        /// <summary>
        /// Sets the tool registry for resolving tool references.
        /// Called by ToolRegistry after instantiation via reflection.
        /// </summary>
        public void SetRegistry(ToolRegistry registry)
        {
            m_registry = registry;
        }

        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var commands = arguments?["commands"] as JArray;
            if (commands == null || commands.Count == 0)
            {
                return McpToolResult.Error("'commands' array is required and must not be empty");
            }

            if (m_registry == null)
            {
                return McpToolResult.Error("ToolRegistry not initialized. BatchExecuteTool requires registry injection.");
            }

            bool atomic = arguments?["atomic"]?.Value<bool>() ?? true;
            bool stopOnError = arguments?["stop_on_error"]?.Value<bool>() ?? true;

            var results = new List<CommandResult>();
            int undoGroup = -1;
            bool hasError = false;

            // Start undo group on main thread if atomic
            if (atomic)
            {
                undoGroup = await MainThreadDispatcher.RunAsync(() =>
                {
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("BatchExecute");
                    return group;
                });
            }

            try
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    var cmd = commands[i] as JObject;
                    if (cmd == null)
                    {
                        results.Add(new CommandResult
                        {
                            Index = i,
                            Tool = "(invalid)",
                            Success = false,
                            Message = "Command entry is not a valid object"
                        });
                        hasError = true;
                        if (stopOnError) break;
                        continue;
                    }

                    var toolName = cmd["tool"]?.Value<string>();
                    var toolArgs = cmd["arguments"] as JObject ?? new JObject();

                    if (string.IsNullOrWhiteSpace(toolName))
                    {
                        results.Add(new CommandResult
                        {
                            Index = i,
                            Tool = "(empty)",
                            Success = false,
                            Message = "'tool' name is required for each command"
                        });
                        hasError = true;
                        if (stopOnError) break;
                        continue;
                    }

                    // Prevent recursive batch_execute
                    if (toolName == "batch_execute")
                    {
                        results.Add(new CommandResult
                        {
                            Index = i,
                            Tool = toolName,
                            Success = false,
                            Message = "Cannot call batch_execute recursively"
                        });
                        hasError = true;
                        if (stopOnError) break;
                        continue;
                    }

                    var tool = m_registry.GetTool(toolName);
                    if (tool == null)
                    {
                        results.Add(new CommandResult
                        {
                            Index = i,
                            Tool = toolName,
                            Success = false,
                            Message = $"Tool '{toolName}' not found"
                        });
                        hasError = true;
                        if (stopOnError) break;
                        continue;
                    }

                    try
                    {
                        var result = await tool.ExecuteAsync(toolArgs);
                        var text = result.Content?.Count > 0 ? result.Content[0].Text : "(no output)";

                        results.Add(new CommandResult
                        {
                            Index = i,
                            Tool = toolName,
                            Success = !result.IsError,
                            Message = text
                        });

                        if (result.IsError)
                        {
                            hasError = true;
                            if (stopOnError) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new CommandResult
                        {
                            Index = i,
                            Tool = toolName,
                            Success = false,
                            Message = $"Exception: {ex.Message}"
                        });
                        hasError = true;
                        if (stopOnError) break;
                    }
                }
            }
            finally
            {
                // Handle undo group finalization on main thread
                if (atomic)
                {
                    if (hasError)
                    {
                        // Rollback all operations in the current group
                        await MainThreadDispatcher.RunAsync(() =>
                        {
                            Undo.RevertAllInCurrentGroup();
                            return 0;
                        });
                    }
                    else
                    {
                        // Collapse all operations into one undo step
                        await MainThreadDispatcher.RunAsync(() =>
                        {
                            Undo.CollapseUndoOperations(undoGroup);
                            return 0;
                        });
                    }
                }
            }

            return FormatResults(results, commands.Count, atomic, hasError);
        }

        #region Result Formatting

        private McpToolResult FormatResults(List<CommandResult> results, int totalCommands,
            bool atomic, bool hasError)
        {
            var sb = new StringBuilder();
            var successCount = 0;
            var failCount = 0;

            foreach (var r in results)
            {
                if (r.Success) successCount++;
                else failCount++;
            }

            sb.AppendLine($"Batch Execute: {successCount}/{totalCommands} succeeded" +
                (atomic ? " (atomic)" : "") +
                (hasError && atomic ? " [ROLLED BACK]" : ""));
            sb.AppendLine();

            foreach (var r in results)
            {
                var status = r.Success ? "OK" : "FAIL";
                sb.AppendLine($"[{r.Index}] {status} {r.Tool}: {TruncateMessage(r.Message, 200)}");
            }

            if (results.Count < totalCommands)
            {
                sb.AppendLine($"\n({totalCommands - results.Count} command(s) skipped due to earlier error)");
            }

            return hasError ? McpToolResult.Error(sb.ToString()) : McpToolResult.Success(sb.ToString());
        }

        private static string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message)) return "(empty)";
            // Take first line only for summary
            var firstLine = message.Split('\n')[0];
            if (firstLine.Length <= maxLength) return firstLine;
            return firstLine.Substring(0, maxLength) + "...";
        }

        #endregion

        #region Internal Types

        private class CommandResult
        {
            public int Index;
            public string Tool;
            public bool Success;
            public string Message;
        }

        #endregion
    }
}
