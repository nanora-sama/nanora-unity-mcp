using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("manage_playmode")]
    public class ManagePlayModeTool : IMcpTool
    {
        public string Name => "manage_playmode";
        public string Description => "Manage Unity Play Mode: play (enter), stop (exit), pause, resume, step one frame, or status. " +
            "play returns immediately with 'Already in Play Mode.' when already playing; otherwise it starts Play Mode asynchronously " +
            "and returns before it is ready unless wait_for_ready=true (waits up to timeout_seconds, then returns an error). " +
            "play fails while scripts are compiling. With auto_compile=true it refreshes assets first, and if that starts a compile it " +
            "returns an error instead of entering Play Mode - call play again after compilation finishes. " +
            "Entering Play Mode may reload the script domain and briefly drop the connection; check status rather than re-issuing play.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""play"", ""stop"", ""pause"", ""resume"", ""step"", ""status""],
                    ""description"": ""Action to perform: play (enter play mode), stop (exit play mode), pause, resume, step (single frame), status (get current state)""
                },
                ""auto_compile"": {
                    ""type"": ""boolean"",
                    ""description"": ""If true, compile project before entering play mode (recommended for automation). Only applies to 'play' action."",
                    ""default"": false
                },
                ""wait_for_ready"": {
                    ""type"": ""boolean"",
                    ""description"": ""If true, wait until play mode is fully ready before returning. Use timeout_seconds to control max wait time (default 30s)."",
                    ""default"": false
                },
                ""timeout_seconds"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum time to wait in seconds when wait_for_ready=true (default: 30, max: 120)"",
                    ""default"": 30
                }
            },
            ""required"": [""action""]
        }");

        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var action = arguments?["action"]?.Value<string>();
            var autoCompile = arguments?["auto_compile"]?.Value<bool>() ?? false;
            var waitForReady = arguments?["wait_for_ready"]?.Value<bool>() ?? false;
            var timeoutSeconds = arguments?["timeout_seconds"]?.Value<int>() ?? 30;
            timeoutSeconds = Math.Max(1, Math.Min(timeoutSeconds, 120));

            if (string.IsNullOrEmpty(action))
            {
                return McpToolResult.Error("Action is required");
            }

            // For play action with wait_for_ready, use async pattern
            if (action.ToLower() == "play" && waitForReady)
            {
                return await EnterPlayModeAsync(autoCompile, timeoutSeconds);
            }

            // For all other actions, use synchronous main thread execution
            return await MainThreadDispatcher.RunAsync(() =>
            {
                switch (action.ToLower())
                {
                    case "play":
                        return EnterPlayModeSync(autoCompile);
                    case "stop":
                        return ExitPlayMode();
                    case "pause":
                        return PausePlayMode();
                    case "resume":
                        return ResumePlayMode();
                    case "step":
                        return StepFrame();
                    case "status":
                        return GetStatus();
                    default:
                        return McpToolResult.Error($"Unknown action: {action}. Valid actions: play, stop, pause, resume, step, status");
                }
            });
        }

        /// <summary>
        /// Enter play mode without waiting (synchronous, non-blocking).
        /// </summary>
        private McpToolResult EnterPlayModeSync(bool autoCompile)
        {
            if (EditorApplication.isPlaying)
            {
                return McpToolResult.Success("Already in Play Mode.");
            }

            if (EditorApplication.isCompiling)
            {
                return McpToolResult.Error("Cannot enter Play Mode while compiling. Use auto_compile=true or wait for compilation to finish.");
            }

            // Note: auto_compile with blocking wait is problematic (causes domain reload).
            // For automation, use compile_project tool separately before entering play mode.
            if (autoCompile)
            {
                // Use AssetDatabase.Refresh instead of RequestScriptCompilation
                // to avoid unnecessary domain reload when there are no changes
                AssetDatabase.Refresh(ImportAssetOptions.Default);

                if (EditorApplication.isCompiling)
                {
                    return McpToolResult.Error("Compilation started. Please wait and retry, or use compile_project tool first.");
                }
            }

            EditorApplication.isPlaying = true;

            return McpToolResult.Success("Entering Play Mode...\nNote: Play Mode starts asynchronously. Use 'status' action or wait_for_ready=true to check when ready.");
        }

        /// <summary>
        /// Enter play mode and wait for ready state using non-blocking async pattern.
        /// Uses EditorApplication.update callback to poll state without blocking main thread.
        /// </summary>
        private async Task<McpToolResult> EnterPlayModeAsync(bool autoCompile, int timeoutSeconds = 30)
        {
            var tcs = new TaskCompletionSource<McpToolResult>();

            // Dispatch to main thread
            MainThreadDispatcher.Enqueue(() =>
            {
                // Fast-path: already in Play Mode. Use OR-style check (single flag) so we return
                // immediately even when Application.isPlaying hasn't yet flipped true during
                // transition. Previously requiring both flags caused callers with shorter HTTP
                // timeouts to appear to stack while we pointlessly polled the readback frame.
                if (EditorApplication.isPlaying)
                {
                    tcs.TrySetResult(McpToolResult.Success($"Already in Play Mode.\n" +
                        $"- EditorApplication.isPlaying: {EditorApplication.isPlaying}\n" +
                        $"- Application.isPlaying: {Application.isPlaying}\n" +
                        $"- Frame Count: {UnityEngine.Time.frameCount}"));
                    return;
                }

                if (EditorApplication.isCompiling)
                {
                    tcs.TrySetResult(McpToolResult.Error("Cannot enter Play Mode while compiling. Wait for compilation to finish."));
                    return;
                }

                // Trigger AssetDatabase.Refresh if auto_compile is requested
                if (autoCompile)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.Default);
                    if (EditorApplication.isCompiling)
                    {
                        tcs.TrySetResult(McpToolResult.Error("Compilation started. Please wait and retry."));
                        return;
                    }
                }

                // Enter play mode
                EditorApplication.isPlaying = true;
                Debug.Log($"[ManagePlaymode] Entering Play Mode with wait_for_ready=true, timeout={timeoutSeconds}s");

                // Set up polling via EditorApplication.update
                var startTime = DateTime.Now;
                var timeout = TimeSpan.FromSeconds(timeoutSeconds);

                EditorApplication.CallbackFunction poll = null;
                poll = () =>
                {
                    // Timeout check
                    if (DateTime.Now - startTime > timeout)
                    {
                        EditorApplication.update -= poll;
                        Debug.LogWarning($"[ManagePlaymode] Timed out waiting for Play Mode ready state after {timeoutSeconds}s");
                        tcs.TrySetResult(McpToolResult.Error(
                            $"Timed out waiting for Play Mode after {timeoutSeconds} seconds.\n" +
                            $"- EditorApplication.isPlaying: {EditorApplication.isPlaying}\n" +
                            $"- Application.isPlaying: {Application.isPlaying}\n" +
                            "Hint: Use 'status' action to check current state, or increase timeout_seconds."));
                        return;
                    }

                    // Check if both flags are true (fully in play mode)
                    if (EditorApplication.isPlaying && Application.isPlaying)
                    {
                        EditorApplication.update -= poll;
                        Debug.Log($"[ManagePlaymode] Play Mode ready after {(DateTime.Now - startTime).TotalSeconds:F1}s");
                        tcs.TrySetResult(McpToolResult.Success(
                            $"Play Mode is ready.\n" +
                            $"- Is Playing: {EditorApplication.isPlaying}\n" +
                            $"- Application.isPlaying: {Application.isPlaying}\n" +
                            $"- Frame Count: {UnityEngine.Time.frameCount}\n" +
                            $"- Time to ready: {(DateTime.Now - startTime).TotalSeconds:F1}s"));
                        return;
                    }

                    // Still waiting, check again next frame
                };

                EditorApplication.update += poll;
            });

            // Wait for result with safety timeout (5s buffer beyond user timeout)
            var safetyTimeoutMs = (timeoutSeconds + 5) * 1000;
            var safetyTimeout = Task.Delay(safetyTimeoutMs);
            var completedTask = await Task.WhenAny(tcs.Task, safetyTimeout);

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            return McpToolResult.Error(
                $"Safety timeout reached ({timeoutSeconds + 5}s). Play mode transition may have failed.\n" +
                "Use 'status' action to check current state.");
        }

        private McpToolResult ExitPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return McpToolResult.Success("Already in Edit Mode.");
            }

            EditorApplication.isPlaying = false;
            return McpToolResult.Success("Exiting Play Mode...\nNote: Exit is asynchronous. Use 'status' action to confirm.");
        }

        private McpToolResult PausePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return McpToolResult.Error("Cannot pause: Not in Play Mode.");
            }

            if (EditorApplication.isPaused)
            {
                return McpToolResult.Success("Already paused.");
            }

            EditorApplication.isPaused = true;
            return McpToolResult.Success("Play Mode paused.");
        }

        private McpToolResult ResumePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return McpToolResult.Error("Cannot resume: Not in Play Mode.");
            }

            if (!EditorApplication.isPaused)
            {
                return McpToolResult.Success("Already running (not paused).");
            }

            EditorApplication.isPaused = false;
            return McpToolResult.Success("Play Mode resumed.");
        }

        private McpToolResult StepFrame()
        {
            if (!EditorApplication.isPlaying)
            {
                return McpToolResult.Error("Cannot step: Not in Play Mode. Enter Play Mode first.");
            }

            EditorApplication.Step();
            return McpToolResult.Success("Stepped one frame.");
        }

        private McpToolResult GetStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine("=== Play Mode Status ===");
            status.AppendLine($"Is Playing: {EditorApplication.isPlaying}");
            status.AppendLine($"Is Paused: {EditorApplication.isPaused}");
            status.AppendLine($"Is Compiling: {EditorApplication.isCompiling}");
            status.AppendLine($"Is Updating: {EditorApplication.isUpdating}");

            string currentState;
            if (!EditorApplication.isPlaying)
            {
                currentState = "Edit Mode";
            }
            else if (EditorApplication.isPaused)
            {
                currentState = "Play Mode (Paused)";
            }
            else
            {
                currentState = "Play Mode (Running)";
            }
            status.AppendLine($"Current State: {currentState}");

            // Time info when in play mode
            if (EditorApplication.isPlaying)
            {
                status.AppendLine();
                status.AppendLine("=== Time Info ===");
                status.AppendLine($"Time Since Startup: {UnityEngine.Time.realtimeSinceStartup:F2}s");
                status.AppendLine($"Time Scale: {UnityEngine.Time.timeScale}");
                status.AppendLine($"Frame Count: {UnityEngine.Time.frameCount}");
            }

            return McpToolResult.Success(status.ToString());
        }
    }
}
