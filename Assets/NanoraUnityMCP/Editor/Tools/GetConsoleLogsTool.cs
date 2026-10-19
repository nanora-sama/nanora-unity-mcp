using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("get_console_logs")]
    public class GetConsoleLogsTool : IMcpTool
    {
        public string Name => "get_console_logs";
        public string Description => "Gets recent log messages from the Unity Console";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""count"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum number of log entries to return"",
                    ""default"": 50
                },
                ""max_message_length"": {
                    ""type"": ""integer"", ""default"": 4000,
                    ""description"": ""Maximum characters per entry (1-64000); truncated entries are marked.""
                },
                ""log_type"": {
                    ""type"": ""string"",
                    ""description"": ""Filter by log type (All, Log, Warning, Error)"",
                    ""enum"": [""All"", ""Log"", ""Warning"", ""Error""],
                    ""default"": ""All""
                },
                ""clear"": {
                    ""type"": ""boolean"",
                    ""description"": ""Clear the console after retrieving logs"",
                    ""default"": false
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                int count = Math.Max(1, Math.Min(1000, arguments?["count"]?.Value<int>() ?? 50));
                int maxLength = Math.Max(1, Math.Min(64000, arguments?["max_message_length"]?.Value<int>() ?? 4000));
                string logType = arguments?["log_type"]?.Value<string>() ?? "All";
                bool clear = arguments?["clear"]?.Value<bool>() ?? false;

                var logs = GetConsoleLogs(count, logType, maxLength);

                if (clear)
                {
                    ClearConsole();
                }

                if (logs.Count == 0)
                {
                    var emptyResult = McpToolResult.Success("No log entries found.");
                    emptyResult.StructuredContent = JObject.FromObject(new { count = 0, entries = logs, filter = logType });
                    return emptyResult;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Console Logs ({logs.Count} entries):");
                sb.AppendLine();

                foreach (var log in logs)
                {
                    sb.AppendLine(log);
                    sb.AppendLine();
                }

                var result = McpToolResult.Success(sb.ToString());
                result.StructuredContent = JObject.FromObject(new { count = logs.Count, entries = logs, filter = logType });
                return result;
            });
        }

        private List<string> GetConsoleLogs(int maxCount, string logType, int maxLength)
        {
            var logs = new List<string>();

            try
            {
                // Use reflection to access the internal LogEntries class
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType == null)
                    throw new InvalidOperationException("Unity Console API is unavailable.");

                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static);
                var startMethod = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Public | BindingFlags.Static);
                var endMethod = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Public | BindingFlags.Static);
                var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Public | BindingFlags.Static);

                if (getCountMethod == null || startMethod == null || endMethod == null || getEntryMethod == null)
                    throw new InvalidOperationException("Unity Console entry methods are unavailable.");

                // Get LogEntry type
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");
                if (logEntryType == null)
                    throw new InvalidOperationException("Unity LogEntry API is unavailable.");

                var messageField = logEntryType.GetField("message", BindingFlags.Public | BindingFlags.Instance);
                var modeField = logEntryType.GetField("mode", BindingFlags.Public | BindingFlags.Instance);

                startMethod.Invoke(null, null);

                try
                {
                    int totalCount = (int)getCountMethod.Invoke(null, null);
                    // Filter before applying count, so newer informational logs do not hide errors.
                    for (int i = totalCount - 1; i >= 0 && logs.Count < maxCount; i--)
                    {
                        var entry = Activator.CreateInstance(logEntryType);
                        getEntryMethod.Invoke(null, new object[] { i, entry });

                        var message = (string)messageField.GetValue(entry);
                        var mode = (int)modeField.GetValue(entry);

                        // Filter by log type
                        string entryType = GetLogTypeFromMode(mode);
                        if (logType != "All" && !string.Equals(entryType, logType, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (message != null && message.Length > maxLength)
                            message = message.Substring(0, maxLength) + "\n[truncated; increase max_message_length for more]";
                        logs.Add($"[{entryType}] {message}");
                    }
                    logs.Reverse();
                }
                finally
                {
                    endMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error retrieving Unity Console logs: {ex.Message}", ex);
            }

            return logs;
        }

        private string GetLogTypeFromMode(int mode)
        {
            // UnityEditor.ConsoleWindow.Mode: assertions are errors, not warnings.
            const int errorMask = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 6) | (1 << 8) |
                (1 << 11) | (1 << 13) | (1 << 17) | (1 << 20) | (1 << 21) | (1 << 22);
            const int warningMask = (1 << 7) | (1 << 9) | (1 << 12);
            if ((mode & errorMask) != 0) return "Error";
            if ((mode & warningMask) != 0) return "Warning";
            return "Log";
        }

        private void ClearConsole()
        {
            try
            {
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                var clearMethod = logEntriesType?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
                clearMethod?.Invoke(null, null);
            }
            catch { }
        }
    }
}
