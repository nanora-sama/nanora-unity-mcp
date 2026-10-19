using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// CPU profiling using ProfilerRecorder API.
    /// Provides detailed performance metrics for specific markers.
    /// </summary>
    [McpTool("profile_cpu")]
    public class ProfileCpuTool : IMcpTool
    {
        private const int k_DefaultSampleCount = 60;

        // Common Unity profiler markers
        private static readonly string[] s_CommonMarkers = new[]
        {
            "PlayerLoop",
            "Update.ScriptRunBehaviourUpdate",
            "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "FixedUpdate.ScriptRunBehaviourFixedUpdate",
            "Update.DirectorUpdate",
            "PreLateUpdate.DirectorUpdateAnimationEnd",
            "Render.TransparentGeometry",
            "Render.OpaqueGeometry",
            "Camera.Render",
            "Gfx.WaitForPresentOnGfxThread",
            "Physics.Processing",
            "Physics.Simulate",
            "BehaviourUpdate",
            "CoroutinesDelayedCalls",
            "GUI.Repaint"
        };

        public string Name => "profile_cpu";
        public string Description => "Profile CPU performance using Unity's ProfilerRecorder API. " +
            "Measures execution time of specific markers or system areas.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""markers"": {
                    ""type"": ""array"",
                    ""description"": ""Profiler marker names to track (use 'common' for common markers)"",
                    ""items"": { ""type"": ""string"" }
                },
                ""sample_count"": {
                    ""type"": ""integer"",
                    ""description"": ""Number of frames to sample"",
                    ""default"": 60
                },
                ""include_gc"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include GC allocation tracking"",
                    ""default"": true
                },
                ""show_system_stats"": {
                    ""type"": ""boolean"",
                    ""description"": ""Show system-wide CPU statistics"",
                    ""default"": true
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                var markerNames = arguments?["markers"]?.ToObject<List<string>>() ?? new List<string>();
                int sampleCount = arguments?["sample_count"]?.Value<int>() ?? k_DefaultSampleCount;
                bool includeGc = arguments?["include_gc"]?.Value<bool>() ?? true;
                bool showSystemStats = arguments?["show_system_stats"]?.Value<bool>() ?? true;

                // Expand 'common' to common markers
                if (markerNames.Contains("common"))
                {
                    markerNames.Remove("common");
                    markerNames.AddRange(s_CommonMarkers);
                }

                // Default to common markers if none specified
                if (markerNames.Count == 0)
                {
                    markerNames.AddRange(s_CommonMarkers.Take(5));
                }

                try
                {
                    var result = ProfileMarkers(markerNames.Distinct().ToList(), sampleCount, includeGc, showSystemStats);
                    return McpToolResult.Success(result);
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Profiling failed: {ex.Message}");
                }
            });
        }

        private string ProfileMarkers(List<string> markerNames, int sampleCount, bool includeGc, bool showSystemStats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# CPU Profile Report");
            sb.AppendLine();
            sb.AppendLine($"**Sample Count:** {sampleCount} frames (requested)");
            sb.AppendLine($"**Timestamp:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**Mode:** {(EditorApplication.isPlaying ? "Play Mode" : "Editor Mode")}");
            sb.AppendLine();

            // FrameTimingManager - reliable frame timing data
            sb.AppendLine("## Frame Timing (FrameTimingManager)");
            sb.AppendLine();
            FrameTiming[] frameTimings = new FrameTiming[1];
            uint numTimings = FrameTimingManager.GetLatestTimings(1, frameTimings);

            if (numTimings > 0)
            {
                var timing = frameTimings[0];
                sb.AppendLine($"| Metric | Value |");
                sb.AppendLine($"|--------|-------|");
                sb.AppendLine($"| CPU Frame Time | {timing.cpuFrameTime:F2} ms |");
#if UNITY_2022_1_OR_NEWER
                sb.AppendLine($"| CPU Main Thread | {timing.cpuMainThreadFrameTime:F2} ms |");
                sb.AppendLine($"| CPU Render Thread | {timing.cpuRenderThreadFrameTime:F2} ms |");
#endif
                sb.AppendLine($"| GPU Frame Time | {timing.gpuFrameTime:F2} ms |");

                // Calculate estimated FPS
                if (timing.cpuFrameTime > 0)
                {
                    double estimatedFps = 1000.0 / timing.cpuFrameTime;
                    sb.AppendLine($"| Estimated FPS | {estimatedFps:F1} |");
                }
            }
            else
            {
                sb.AppendLine("*Frame timing not available. Requires Play mode or specific platform support.*");
                sb.AppendLine();
                sb.AppendLine("**Note:** FrameTimingManager data is most reliable in Play mode.");
            }
            sb.AppendLine();

            // System stats
            if (showSystemStats)
            {
                sb.AppendLine("## System Overview");
                sb.AppendLine($"- System Memory: {SystemInfo.systemMemorySize} MB");
                sb.AppendLine($"- Processor: {SystemInfo.processorType}");
                sb.AppendLine($"- Processor Count: {SystemInfo.processorCount}");
                sb.AppendLine($"- Graphics Device: {SystemInfo.graphicsDeviceName}");
                sb.AppendLine();

                // Current memory usage
                sb.AppendLine("## Memory Usage");
                sb.AppendLine($"- Total Allocated: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())}");
                sb.AppendLine($"- Total Reserved: {FormatBytes(Profiler.GetTotalReservedMemoryLong())}");
                sb.AppendLine($"- Mono Heap: {FormatBytes(Profiler.GetMonoHeapSizeLong())}");
                sb.AppendLine($"- Mono Used: {FormatBytes(Profiler.GetMonoUsedSizeLong())}");
                sb.AppendLine($"- GFX Driver: {FormatBytes(Profiler.GetAllocatedMemoryForGraphicsDriver())}");
                sb.AppendLine();
            }

            // Create recorders for each marker
            var recorders = new Dictionary<string, ProfilerRecorder>();
            var validMarkers = new List<string>();

            // Try multiple categories for better marker detection
            var categoriesToTry = new ProfilerCategory[]
            {
                ProfilerCategory.Scripts,
                ProfilerCategory.Internal,
                ProfilerCategory.Render,
                ProfilerCategory.Memory,
                ProfilerCategory.Physics,
            };

            foreach (var marker in markerNames)
            {
                bool found = false;
                foreach (var category in categoriesToTry)
                {
                    try
                    {
                        var recorder = ProfilerRecorder.StartNew(category, marker);
                        if (recorder.Valid)
                        {
                            recorders[marker] = recorder;
                            validMarkers.Add(marker);
                            found = true;
                            break;
                        }
                        recorder.Dispose();
                    }
                    catch
                    {
                        // Try next category
                    }
                }

                if (!found)
                {
                    // Debug log for missing markers
                    Debug.Log($"[ProfileCpu] Marker not found: {marker}");
                }
            }

            // GC recorder
            ProfilerRecorder gcRecorder = default;
            if (includeGc)
            {
                try
                {
                    gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
                }
                catch
                {
                    // GC tracking not available
                }
            }

            // Profiler Markers section
            sb.AppendLine("## Profiler Markers");
            sb.AppendLine();

            if (validMarkers.Count == 0)
            {
                sb.AppendLine("*No valid markers found for the requested names.*");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("| Marker | Last (ms) | Count | Status |");
                sb.AppendLine("|--------|-----------|-------|--------|");

                bool allZero = true;
                foreach (var marker in validMarkers)
                {
                    var recorder = recorders[marker];
                    double lastMs = recorder.LastValue / 1000000.0; // ns to ms
                    long count = recorder.Count;
                    string status = recorder.Valid ? "Active" : "Invalid";

                    if (lastMs > 0 || count > 0) allZero = false;
                    sb.AppendLine($"| {marker} | {lastMs:F3} | {count} | {status} |");
                }

                // Explain why values might be zero
                if (allZero)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Note:** All markers show 0.000ms because:");
                    sb.AppendLine("- ProfilerRecorder requires frames to pass to accumulate data");
                    sb.AppendLine("- This is a point-in-time snapshot (recorders were just created)");
                    sb.AppendLine("- For accurate data, use Unity's Profiler window or run in Play mode");
                    sb.AppendLine();
                    sb.AppendLine("**Recommended alternatives:**");
                    sb.AppendLine("- Use `get_profiler_stats` for FrameTimingManager data (more reliable)");
                    sb.AppendLine("- Use `analyze_frame` for Frame Debugger draw call analysis");
                    sb.AppendLine("- Use `analyze_memory` for memory breakdown by category");
                }
            }

            // GC stats
            if (gcRecorder.Valid)
            {
                sb.AppendLine();
                sb.AppendLine("## GC Allocations");
                long gcValue = gcRecorder.LastValue;
                if (gcValue > 0)
                {
                    sb.AppendLine($"- Last Frame: {FormatBytes(gcValue)}");
                }
                else
                {
                    sb.AppendLine($"- Last Frame: 0 B (no allocations detected or insufficient samples)");
                }
                sb.AppendLine($"- Sample Count: {gcRecorder.Count}");

                if (gcRecorder.Count == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("*Note: GC.Alloc tracking requires Play mode and multiple frames to accumulate.*");
                }
            }

            // Cleanup
            foreach (var recorder in recorders.Values)
            {
                recorder.Dispose();
            }
            if (gcRecorder.Valid)
            {
                gcRecorder.Dispose();
            }

            // Usage recommendations
            sb.AppendLine();
            sb.AppendLine("## Recommended Profiling Workflow");
            sb.AppendLine();
            sb.AppendLine("1. **For frame timing:** Use `get_profiler_stats` (FrameTimingManager)");
            sb.AppendLine("2. **For draw calls:** Use `analyze_frame` (Frame Debugger)");
            sb.AppendLine("3. **For memory:** Use `analyze_memory` (detailed breakdown)");
            sb.AppendLine("4. **For deep CPU analysis:** Open Window > Analysis > Profiler in Unity");
            sb.AppendLine();
            sb.AppendLine("ProfilerRecorder is designed for continuous monitoring over multiple frames,");
            sb.AppendLine("which is not well-suited for single-request MCP tools.");

            return sb.ToString();
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// Gets available profiler statistics and markers.
    /// </summary>
    [McpTool("get_profiler_stats")]
    public class GetProfilerStatsTool : IMcpTool
    {
        public string Name => "get_profiler_stats";
        public string Description => "Get available profiler statistics and current performance metrics.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""include_frame_timing"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include frame timing statistics"",
                    ""default"": true
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                bool includeFrameTiming = arguments?["include_frame_timing"]?.Value<bool>() ?? true;

                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("# Profiler Statistics");
                    sb.AppendLine();

                    // Memory stats
                    sb.AppendLine("## Memory Usage");
                    sb.AppendLine($"- Total Reserved: {FormatBytes(Profiler.GetTotalReservedMemoryLong())}");
                    sb.AppendLine($"- Total Allocated: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())}");
                    sb.AppendLine($"- Total Unused Reserved: {FormatBytes(Profiler.GetTotalUnusedReservedMemoryLong())}");
                    sb.AppendLine($"- Mono Heap Size: {FormatBytes(Profiler.GetMonoHeapSizeLong())}");
                    sb.AppendLine($"- Mono Used Size: {FormatBytes(Profiler.GetMonoUsedSizeLong())}");
                    sb.AppendLine($"- Temp Allocator Size: {FormatBytes(Profiler.GetTempAllocatorSize())}");
                    sb.AppendLine($"- GFX Driver Memory: {FormatBytes(Profiler.GetAllocatedMemoryForGraphicsDriver())}");
                    sb.AppendLine();

                    // Rendering stats
                    sb.AppendLine("## Rendering Stats");
                    sb.AppendLine($"- Supported Render Target Count: {SystemInfo.supportedRenderTargetCount}");
                    sb.AppendLine($"- Max Texture Size: {SystemInfo.maxTextureSize}");
                    sb.AppendLine($"- Max Cubemap Size: {SystemInfo.maxCubemapSize}");
                    sb.AppendLine($"- NPOT Support: {SystemInfo.npotSupport}");
                    sb.AppendLine($"- Copy Texture Support: {SystemInfo.copyTextureSupport}");
                    sb.AppendLine();

                    // Frame timing
                    if (includeFrameTiming)
                    {
                        sb.AppendLine("## Frame Timing");

                        // Try to get frame timing using FrameTimingManager
                        FrameTiming[] frameTimings = new FrameTiming[1];
                        uint numTimings = FrameTimingManager.GetLatestTimings(1, frameTimings);

                        if (numTimings > 0)
                        {
                            var timing = frameTimings[0];
                            sb.AppendLine($"- CPU Frame Time: {timing.cpuFrameTime:F2} ms");
#if UNITY_2022_1_OR_NEWER
                            sb.AppendLine($"- CPU Main Thread Frame Time: {timing.cpuMainThreadFrameTime:F2} ms");
                            sb.AppendLine($"- CPU Render Thread Frame Time: {timing.cpuRenderThreadFrameTime:F2} ms");
#endif
                            sb.AppendLine($"- GPU Frame Time: {timing.gpuFrameTime:F2} ms");
                        }
                        else
                        {
                            sb.AppendLine("*Frame timing not available (requires Play mode or specific platform support)*");
                        }
                        sb.AppendLine();
                    }

                    // Profiler state
                    sb.AppendLine("## Profiler State");
                    sb.AppendLine($"- Profiler Enabled: {Profiler.enabled}");
                    sb.AppendLine($"- Profiler Supported: {Profiler.supported}");
                    sb.AppendLine($"- Area Count: {Profiler.areaCount}");
                    sb.AppendLine();

                    // Common profiler categories
                    sb.AppendLine("## Available Profiler Categories");
                    sb.AppendLine("- Scripts");
                    sb.AppendLine("- Render");
                    sb.AppendLine("- Memory");
                    sb.AppendLine("- Physics");
                    sb.AppendLine("- Audio");
                    sb.AppendLine("- Animation");
                    sb.AppendLine("- AI");
                    sb.AppendLine("- UI");
                    sb.AppendLine("- Internal");

                    return McpToolResult.Success(sb.ToString());
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Failed to get profiler stats: {ex.Message}");
                }
            });
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }
}
