using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Retrieves current compilation errors without triggering a new compilation.
    /// Useful for checking the current state of the project.
    ///
    /// Difference from compile_project:
    /// - compile_project: Triggers compilation and waits for result
    /// - get_compilation_errors: Returns current/cached errors without recompiling
    ///
    /// Uses SessionState to persist data across domain reloads so that
    /// successful compilation results are not lost.
    /// </summary>
    [McpTool("get_compilation_errors")]
    public class GetCompilationErrorsTool : IMcpTool
    {
        public string Name => "get_compilation_errors";
        public string Description => "Get current compilation errors and warnings without triggering a new compilation. Returns cached results from the last compilation.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""include_warnings"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include warnings in the result (default: true)"",
                    ""default"": true
                },
                ""file_filter"": {
                    ""type"": ""string"",
                    ""description"": ""Filter errors by file path (partial match)""
                },
                ""include_source_context"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include source code context around errors (default: true)"",
                    ""default"": true
                }
            },
            ""additionalProperties"": false
        }");

        // SessionState keys for persistence across domain reloads
        private const string k_SessionKeySucceeded = "NanoraUnityMCP.CompilationSucceeded";
        private const string k_SessionKeyTime = "NanoraUnityMCP.CompilationTime";
        private const string k_SessionKeyMessages = "NanoraUnityMCP.CompilationMessages";

        // Shared state with CompileProjectTool
        private static readonly object s_Lock = new object();
        private static List<StoredMessage> s_CachedMessages = new List<StoredMessage>();
        private static bool s_LastCompilationSucceeded = true;
        private static DateTime s_LastCompilationTime = DateTime.MinValue;

        private class StoredMessage
        {
            public string File { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            public string Message { get; set; }
            public string Severity { get; set; }
            public string AssemblyPath { get; set; }
        }

        static GetCompilationErrorsTool()
        {
            // Restore state from SessionState (survives domain reload)
            RestoreFromSessionState();

            // Subscribe to compilation events to cache messages
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

            // Save state before domain reload
            AssemblyReloadEvents.beforeAssemblyReload += SaveToSessionState;
        }

        private static void SaveToSessionState()
        {
            lock (s_Lock)
            {
                SessionState.SetBool(k_SessionKeySucceeded, s_LastCompilationSucceeded);
                SessionState.SetString(k_SessionKeyTime, s_LastCompilationTime.Ticks.ToString());

                try
                {
                    var json = JsonConvert.SerializeObject(s_CachedMessages);
                    SessionState.SetString(k_SessionKeyMessages, json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GetCompilationErrors] Failed to save messages to SessionState: {ex.Message}");
                }
            }
        }

        private static void RestoreFromSessionState()
        {
            lock (s_Lock)
            {
                var timeStr = SessionState.GetString(k_SessionKeyTime, "");
                if (!string.IsNullOrEmpty(timeStr) && long.TryParse(timeStr, out long ticks) && ticks > 0)
                {
                    s_LastCompilationTime = new DateTime(ticks);
                    s_LastCompilationSucceeded = SessionState.GetBool(k_SessionKeySucceeded, true);

                    var messagesJson = SessionState.GetString(k_SessionKeyMessages, "");
                    if (!string.IsNullOrEmpty(messagesJson))
                    {
                        try
                        {
                            var restored = JsonConvert.DeserializeObject<List<StoredMessage>>(messagesJson);
                            if (restored != null)
                            {
                                s_CachedMessages = restored;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[GetCompilationErrors] Failed to restore messages from SessionState: {ex.Message}");
                        }
                    }

                    Debug.Log($"[GetCompilationErrors] Restored compilation data from SessionState - Success: {s_LastCompilationSucceeded}, Messages: {s_CachedMessages.Count}");
                }
            }
        }

        private static void OnCompilationStarted(object context)
        {
            lock (s_Lock)
            {
                s_CachedMessages.Clear();
                s_LastCompilationSucceeded = true;
            }
        }

        private static void OnCompilationFinished(object context)
        {
            lock (s_Lock)
            {
                s_LastCompilationTime = DateTime.Now;
            }
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            lock (s_Lock)
            {
                foreach (var msg in messages)
                {
                    if (msg.type == CompilerMessageType.Error)
                    {
                        s_LastCompilationSucceeded = false;
                    }

                    s_CachedMessages.Add(new StoredMessage
                    {
                        File = msg.file,
                        Line = msg.line,
                        Column = msg.column,
                        Message = msg.message,
                        Severity = msg.type == CompilerMessageType.Error ? "error" : "warning",
                        AssemblyPath = assemblyPath
                    });
                }
            }
        }

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() => Execute(arguments));
        }

        private McpToolResult Execute(JObject args)
        {
            bool includeWarnings = args?["include_warnings"]?.Value<bool>() ?? true;
            string fileFilter = args?["file_filter"]?.Value<string>();
            bool includeSourceContext = args?["include_source_context"]?.Value<bool>() ?? true;

            List<StoredMessage> messages;
            bool success;
            DateTime compilationTime;

            lock (s_Lock)
            {
                messages = new List<StoredMessage>(s_CachedMessages);
                success = s_LastCompilationSucceeded;
                compilationTime = s_LastCompilationTime;
            }

            // Filter by file if specified
            if (!string.IsNullOrEmpty(fileFilter))
            {
                messages = messages.Where(m =>
                    m.File != null && m.File.IndexOf(fileFilter, StringComparison.OrdinalIgnoreCase) >= 0
                ).ToList();
            }

            // Filter out warnings if not requested
            if (!includeWarnings)
            {
                messages = messages.Where(m => m.Severity == "error").ToList();
            }

            return McpToolResult.Success(FormatResult(success, compilationTime, messages, includeSourceContext));
        }

        private string FormatResult(bool success, DateTime compilationTime, List<StoredMessage> messages, bool includeSourceContext)
        {
            var sb = new StringBuilder();

            // Header
            if (compilationTime == DateTime.MinValue)
            {
                sb.AppendLine("## Compilation Status: No compilation data available");
                sb.AppendLine();
                sb.AppendLine("No compilation has occurred since the editor started, or the data was not captured.");
                return sb.ToString();
            }

            sb.AppendLine($"## Compilation Status: {(success ? "SUCCESS" : "FAILED")}");
            sb.AppendLine();
            sb.AppendLine($"Last compilation: {compilationTime:yyyy-MM-dd HH:mm:ss}");

            var errors = messages.Where(m => m.Severity == "error").ToList();
            var warnings = messages.Where(m => m.Severity == "warning").ToList();

            sb.AppendLine($"Errors: {errors.Count}");
            sb.AppendLine($"Warnings: {warnings.Count}");

            // Errors section
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Errors:");
                foreach (var error in errors)
                {
                    sb.AppendLine();
                    sb.AppendLine($"**{GetRelativePath(error.File)}:{error.Line}:{error.Column}**");
                    sb.AppendLine($"```");
                    sb.AppendLine(error.Message);
                    sb.AppendLine($"```");

                    if (includeSourceContext)
                    {
                        var sourceContext = GetSourceContext(error.File, error.Line);
                        if (!string.IsNullOrEmpty(sourceContext))
                        {
                            sb.AppendLine("Source:");
                            sb.AppendLine("```csharp");
                            sb.AppendLine(sourceContext);
                            sb.AppendLine("```");
                        }
                    }
                }
            }

            // Warnings section
            if (warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Warnings:");
                foreach (var warning in warnings)
                {
                    sb.AppendLine($"- `{GetRelativePath(warning.File)}:{warning.Line}`: {warning.Message}");
                }
            }

            // JSON summary
            sb.AppendLine();
            sb.AppendLine("### JSON Summary:");
            var jsonResult = new
            {
                success = success,
                last_compilation = compilationTime.ToString("o"),
                error_count = errors.Count,
                warning_count = warnings.Count,
                errors = errors.Select(e => new
                {
                    file = GetRelativePath(e.File),
                    line = e.Line,
                    column = e.Column,
                    message = e.Message
                }).ToList()
            };
            sb.AppendLine("```json");
            sb.AppendLine(JsonConvert.SerializeObject(jsonResult, Formatting.Indented));
            sb.AppendLine("```");

            return sb.ToString();
        }

        private string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return "(unknown)";

            var dataPath = Application.dataPath;
            if (fullPath.StartsWith(dataPath))
            {
                return "Assets" + fullPath.Substring(dataPath.Length);
            }

            return fullPath;
        }

        private string GetSourceContext(string filePath, int line, int contextLines = 2)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;

                var lines = File.ReadAllLines(filePath);
                if (line < 1 || line > lines.Length)
                    return null;

                var startLine = Math.Max(0, line - 1 - contextLines);
                var endLine = Math.Min(lines.Length - 1, line - 1 + contextLines);

                var sb = new StringBuilder();
                for (int i = startLine; i <= endLine; i++)
                {
                    var lineNum = i + 1;
                    var marker = lineNum == line ? ">>>" : "   ";
                    sb.AppendLine($"{marker} {lineNum,4}: {lines[i]}");
                }

                return sb.ToString().TrimEnd();
            }
            catch
            {
                return null;
            }
        }
    }
}
