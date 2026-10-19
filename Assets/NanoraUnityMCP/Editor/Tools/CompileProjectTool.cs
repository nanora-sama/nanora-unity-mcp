using System;
using System.Collections.Generic;
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
    /// Triggers Unity's script compilation and waits for completion.
    /// Essential for Playwright-style automation where compilation must succeed before proceeding.
    ///
    /// Difference from execute_csharp:
    /// - execute_csharp: Compiles and runs code snippets in memory using Roslyn (for queries/actions)
    /// - compile_project: Triggers Unity's IDE compilation of project scripts (for build verification)
    /// </summary>
    [McpTool("compile_project")]
    public class CompileProjectTool : IMcpTool
    {
        public string Name => "compile_project";
        public string Description => "Trigger Unity script compilation and wait for completion (up to timeout_seconds). " +
            "Returns success/failure with file, line, and message for each error (warnings only with include_warnings). " +
            "Use this to verify script changes compile before entering Play Mode or running tests. " +
            "A successful compile reloads the script domain, so the connection may drop before the response arrives. " +
            "The result is kept across the reload: after the server is back, read it with get_compilation_errors instead of compiling again.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""timeout_seconds"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum time to wait for compilation (default: 60 seconds)"",
                    ""default"": 60
                },
                ""include_warnings"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include warnings in the result (default: false)"",
                    ""default"": false
                }
            },
            ""additionalProperties"": false
        }");

        private static readonly object s_Lock = new object();
        private static List<CompilationMessage> s_Messages = new List<CompilationMessage>();
        private static bool s_CompilationSucceeded;
        private static DateTime s_CompilationStartTime;

        // Active TCS for immediate resolution from compilationFinished event
        private static TaskCompletionSource<McpToolResult> s_ActiveTcs;
        private static bool s_ActiveIncludeWarnings;

        private class CompilationMessage
        {
            public string File { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            public string Message { get; set; }
            public string Severity { get; set; } // "error" or "warning"
        }

        static CompileProjectTool()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        }

        private static void OnCompilationStarted(object context)
        {
            lock (s_Lock)
            {
                s_Messages.Clear();
                s_CompilationSucceeded = true;
                s_CompilationStartTime = DateTime.Now;
                Debug.Log("[CompileProject] Compilation started");
            }
        }

        private static void OnCompilationFinished(object context)
        {
            lock (s_Lock)
            {
                var duration = DateTime.Now - s_CompilationStartTime;
                bool success = s_CompilationSucceeded;
                var messages = new List<CompilationMessage>(s_Messages);
                Debug.Log($"[CompileProject] Compilation finished in {duration.TotalSeconds:F1}s - Success: {success}");

                // Store result in SessionState so it survives domain reload
                string status = success ? "Compilation succeeded" : "Compilation failed";
                string resultText = FormatResult(success, status, messages, true);
                SessionState.SetString("UnityMCP_LastCompileResult", resultText);

                // Immediately resolve the active TCS before domain reload starts.
                // This is critical: compilationFinished fires BEFORE domain reload,
                // so resolving here gives the HTTP response the best chance of being sent.
                var tcs = s_ActiveTcs;
                if (tcs != null)
                {
                    bool includeWarnings = s_ActiveIncludeWarnings;
                    s_ActiveTcs = null;
                    string formattedResult = FormatResult(success, status, messages, includeWarnings);
                    tcs.TrySetResult(McpToolResult.Success(formattedResult));
                    Debug.Log("[CompileProject] Result resolved immediately from compilationFinished event");
                }
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
                        s_CompilationSucceeded = false;
                        s_Messages.Add(new CompilationMessage
                        {
                            File = msg.file,
                            Line = msg.line,
                            Column = msg.column,
                            Message = msg.message,
                            Severity = "error"
                        });
                    }
                    else if (msg.type == CompilerMessageType.Warning)
                    {
                        s_Messages.Add(new CompilationMessage
                        {
                            File = msg.file,
                            Line = msg.line,
                            Column = msg.column,
                            Message = msg.message,
                            Severity = "warning"
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Triggers compilation on main thread, then uses compilationFinished event
        /// for immediate resolution (before domain reload) and EditorApplication.update
        /// as a fallback. Result is delivered via TaskCompletionSource.
        /// </summary>
        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            int timeoutSeconds = arguments?["timeout_seconds"]?.Value<int>() ?? 60;
            bool includeWarnings = arguments?["include_warnings"]?.Value<bool>() ?? false;

            var tcs = new TaskCompletionSource<McpToolResult>();

            // Store TCS for immediate resolution from compilationFinished event
            lock (s_Lock)
            {
                s_ActiveTcs = tcs;
                s_ActiveIncludeWarnings = includeWarnings;
            }

            // Everything runs on main thread via a single dispatch
            MainThreadDispatcher.Enqueue(() =>
            {
                // Clear previous results and trigger compilation. Keep the last messages: when nothing changed
                // since a failed compile, Unity does not compile again and they are still the current errors.
                List<CompilationMessage> previousMessages;
                lock (s_Lock)
                {
                    previousMessages = new List<CompilationMessage>(s_Messages);
                    s_Messages.Clear();
                    s_CompilationSucceeded = true;
                }

                Debug.Log("[CompileProject] Refreshing AssetDatabase to detect external file changes...");
                // Only use AssetDatabase.Refresh to detect and compile changes.
                // Do NOT call RequestScriptCompilation() - it forces a full recompile
                // even with no changes, always triggering domain reload and killing the server.
                AssetDatabase.Refresh(ImportAssetOptions.Default);

                // Set up polling via EditorApplication.update as fallback
                // Primary resolution happens in OnCompilationFinished for speed
                var startTime = DateTime.Now;
                var timeout = TimeSpan.FromSeconds(timeoutSeconds);
                bool wasEverCompiling = EditorApplication.isCompiling;
                Debug.Log($"[CompileProject] After Refresh: isCompiling={wasEverCompiling}");

                EditorApplication.CallbackFunction poll = null;
                poll = () =>
                {
                    // Already resolved by compilationFinished event
                    if (tcs.Task.IsCompleted)
                    {
                        EditorApplication.update -= poll;
                        return;
                    }

                    // Timeout check
                    if (DateTime.Now - startTime > timeout)
                    {
                        EditorApplication.update -= poll;
                        Debug.Log("[CompileProject] Compilation timed out");
                        lock (s_Lock) { s_ActiveTcs = null; }
                        tcs.TrySetResult(McpToolResult.Success(
                            FormatResult(false, "Compilation timed out", new List<CompilationMessage>(), includeWarnings)));
                        return;
                    }

                    bool isCompiling = EditorApplication.isCompiling;

                    if (isCompiling)
                    {
                        wasEverCompiling = true;
                        return; // Still compiling, keep polling
                    }

                    // Not compiling now
                    if (wasEverCompiling)
                    {
                        // Was compiling, now finished - fallback if compilationFinished didn't resolve
                        EditorApplication.update -= poll;
                        Debug.Log("[CompileProject] Compilation finished (detected via polling fallback)");

                        bool success;
                        List<CompilationMessage> messages;
                        lock (s_Lock)
                        {
                            s_ActiveTcs = null;
                            success = s_CompilationSucceeded;
                            messages = new List<CompilationMessage>(s_Messages);
                        }

                        string status = success ? "Compilation succeeded" : "Compilation failed";
                        tcs.TrySetResult(McpToolResult.Success(FormatResult(success, status, messages, includeWarnings)));
                        return;
                    }

                    // Never started compiling - grace period of 2 seconds
                    if ((DateTime.Now - startTime).TotalSeconds > 2)
                    {
                        EditorApplication.update -= poll;
                        Debug.Log("[CompileProject] No compilation needed (no changes detected)");

                        bool success;
                        List<CompilationMessage> messages;
                        lock (s_Lock)
                        {
                            s_ActiveTcs = null;
                            success = s_CompilationSucceeded;
                            messages = new List<CompilationMessage>(s_Messages);
                            if (EditorUtility.scriptCompilationFailed)
                            {
                                // Nothing recompiled, but the scripts still fail from the last compile: report that,
                                // not "succeeded" with 0 errors.
                                success = false;
                                s_CompilationSucceeded = false;
                                if (messages.Count == 0) s_Messages.AddRange(messages = previousMessages);
                            }
                        }

                        if (!success && messages.Count == 0)
                        {
                            tcs.TrySetResult(McpToolResult.Success(FormatResult(false,
                                "Compilation failed earlier and nothing changed since; the errors are in the Console",
                                messages, includeWarnings)));
                            return;
                        }

                        string status = (messages.Count == 0 && success)
                            ? "No compilation needed (no changes detected)"
                            : (success ? "Compilation succeeded" : "Compilation failed");
                        tcs.TrySetResult(McpToolResult.Success(FormatResult(success, status, messages, includeWarnings)));
                    }
                };

                EditorApplication.update += poll;
            });

            // Wait for result with a safety timeout (slightly longer than tool timeout)
            var safetyTimeout = Task.Delay((timeoutSeconds + 10) * 1000);
            var completedTask = await Task.WhenAny(tcs.Task, safetyTimeout);

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            lock (s_Lock) { s_ActiveTcs = null; }
            return McpToolResult.Success(FormatResult(false,
                "Safety timeout reached (main thread may be unresponsive)",
                new List<CompilationMessage>(), includeWarnings));
        }

        private static string FormatResult(bool success, string status, List<CompilationMessage> messages, bool includeWarnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Compilation Result: {(success ? "SUCCESS" : "FAILED")}");
            sb.AppendLine();
            sb.AppendLine($"Status: {status}");

            var errors = messages.FindAll(m => m.Severity == "error");
            var warnings = messages.FindAll(m => m.Severity == "warning");

            sb.AppendLine($"Errors: {errors.Count}");
            sb.AppendLine($"Warnings: {warnings.Count}");

            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Errors:");
                foreach (var error in errors)
                {
                    sb.AppendLine($"- **{error.File}:{error.Line}:{error.Column}**");
                    sb.AppendLine($"  {error.Message}");
                }
            }

            if (includeWarnings && warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Warnings:");
                foreach (var warning in warnings)
                {
                    sb.AppendLine($"- {warning.File}:{warning.Line}:{warning.Column}");
                    sb.AppendLine($"  {warning.Message}");
                }
            }

            // Add JSON summary for programmatic access
            sb.AppendLine();
            sb.AppendLine("### JSON Summary:");
            var jsonResult = new
            {
                success = success,
                error_count = errors.Count,
                warning_count = warnings.Count,
                errors = errors.ConvertAll(e => new { file = e.File, line = e.Line, column = e.Column, message = e.Message })
            };
            sb.AppendLine("```json");
            sb.AppendLine(JsonConvert.SerializeObject(jsonResult, Formatting.Indented));
            sb.AppendLine("```");

            return sb.ToString();
        }
    }
}
