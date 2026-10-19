using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Performance budget testing tool for CI-style quality gates.
    /// Tests FPS, frame time, memory, draw calls, triangles, and batches
    /// against configurable thresholds and returns PASS/FAIL results.
    /// </summary>
    [McpTool("performance_budget_test")]
    public class PerformanceBudgetTestTool : IMcpTool
    {
        private const string k_PrefsPrefix = "NanoraUnityMCP_PerfBudget_";

        // Default budget values
        internal const float k_DefaultMinFps = 60f;
        internal const float k_DefaultMaxFrameTimeMs = 16.67f;
        internal const float k_DefaultMaxMemoryMb = 512f;
        internal const int k_DefaultMaxDrawCalls = 200;
        internal const int k_DefaultMaxTriangles = 100000;
        internal const int k_DefaultMaxBatches = 200;
        private const int k_DefaultSampleFrames = 60;

        // Cached UnityStats reflection
        private static Type s_UnityStatsType;
        private static PropertyInfo s_DrawCallsProp;
        private static PropertyInfo s_BatchesProp;
        private static PropertyInfo s_TrianglesProp;
        private static bool s_StatsReflectionInitialized;

        public string Name => "performance_budget_test";
        public string Description => "Test if the scene meets performance budgets. " +
            "Checks FPS, frame time, memory usage, draw calls against configurable thresholds. " +
            "Returns PASS/FAIL for CI-style quality gates.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""test"", ""configure"", ""show_config""],
                    ""description"": ""Action: test (run budget test), configure (set thresholds), show_config (show current config)"",
                    ""default"": ""test""
                },
                ""sample_frames"": {
                    ""type"": ""integer"",
                    ""description"": ""Number of frames to sample for averaging (default: 60)"",
                    ""default"": 60
                },
                ""budgets"": {
                    ""type"": ""object"",
                    ""description"": ""Performance budgets (for test/configure action)"",
                    ""properties"": {
                        ""min_fps"": { ""type"": ""number"", ""description"": ""Minimum acceptable FPS (default: 60)"" },
                        ""max_frame_time_ms"": { ""type"": ""number"", ""description"": ""Maximum acceptable frame time in ms (default: 16.67)"" },
                        ""max_memory_mb"": { ""type"": ""number"", ""description"": ""Maximum memory usage in MB"" },
                        ""max_draw_calls"": { ""type"": ""integer"", ""description"": ""Maximum draw calls per frame"" },
                        ""max_triangles"": { ""type"": ""integer"", ""description"": ""Maximum triangle count"" },
                        ""max_batches"": { ""type"": ""integer"", ""description"": ""Maximum batch count"" }
                    }
                }
            }
        }");

        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var action = arguments?["action"]?.Value<string>() ?? "test";

            switch (action.ToLower())
            {
                case "test":
                    return await RunBudgetTest(arguments);
                case "configure":
                    return await MainThreadDispatcher.RunAsync(() => ConfigureBudgets(arguments));
                case "show_config":
                    return await MainThreadDispatcher.RunAsync(() => ShowConfig());
                default:
                    return McpToolResult.Error($"Unknown action: {action}. Valid actions: test, configure, show_config");
            }
        }

        #region Test Action

        private async Task<McpToolResult> RunBudgetTest(JObject arguments)
        {
            var tcs = new TaskCompletionSource<McpToolResult>();
            int sampleFrames = arguments?["sample_frames"]?.Value<int>() ?? k_DefaultSampleFrames;
            sampleFrames = Math.Max(1, Math.Min(sampleFrames, 600));

            var budgets = ResolveBudgets(arguments?["budgets"] as JObject);

            MainThreadDispatcher.Enqueue(() =>
            {
                if (!EditorApplication.isPlaying || !Application.isPlaying)
                {
                    tcs.TrySetResult(McpToolResult.Error(
                        "Performance budget test requires Play Mode. " +
                        "Use manage_playmode with action='play' and wait_for_ready=true first."));
                    return;
                }

                InitializeStatsReflection();

                var samples = new FrameSampleCollection(sampleFrames);
                int sampledCount = 0;
                int targetFrames = sampleFrames;

                EditorApplication.CallbackFunction sampleCallback = null;
                sampleCallback = () =>
                {
                    // Check if still in play mode
                    if (!EditorApplication.isPlaying || !Application.isPlaying)
                    {
                        EditorApplication.update -= sampleCallback;
                        tcs.TrySetResult(McpToolResult.Error(
                            $"Play Mode exited during sampling (collected {sampledCount}/{targetFrames} frames)."));
                        return;
                    }

                    // Collect one frame of data
                    var sample = CollectFrameSample();
                    samples.Add(sample);
                    sampledCount++;

                    // Check if we have enough samples
                    if (sampledCount >= targetFrames)
                    {
                        EditorApplication.update -= sampleCallback;
                        var report = GenerateReport(samples, budgets, targetFrames);
                        tcs.TrySetResult(report);
                    }
                };

                EditorApplication.update += sampleCallback;
            });

            // Safety timeout: allow generous time for frame sampling
            int timeoutMs = Math.Max(30000, sampleFrames * 500);
            var safetyTimeout = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(tcs.Task, safetyTimeout);

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            return McpToolResult.Error(
                $"Performance budget test timed out after {timeoutMs / 1000}s. " +
                "The editor may be unresponsive or frame rate is extremely low.");
        }

        private static FrameSample CollectFrameSample()
        {
            var sample = new FrameSample();

            // FPS and frame time from Time.unscaledDeltaTime
            float dt = Time.unscaledDeltaTime;
            sample.FrameTimeMs = dt * 1000f;
            sample.Fps = dt > 0f ? 1.0f / dt : 0f;

            // Memory
            sample.MemoryBytes = Profiler.GetTotalAllocatedMemoryLong();
            sample.ReservedMemoryBytes = Profiler.GetTotalReservedMemoryLong();

            // Draw calls, batches, triangles via UnityStats reflection
            sample.DrawCalls = GetStatsInt(s_DrawCallsProp);
            sample.Batches = GetStatsInt(s_BatchesProp);
            sample.Triangles = GetStatsInt(s_TrianglesProp);

            return sample;
        }

        private static McpToolResult GenerateReport(FrameSampleCollection samples, BudgetConfig budgets, int sampleFrames)
        {
            var stats = samples.ComputeStatistics();
            var results = EvaluateBudgets(stats, budgets);

            bool allPassed = results.TrueForAll(r => r.Passed);
            var sb = new StringBuilder();

            sb.AppendLine("=== Performance Budget Test ===");
            sb.AppendLine($"Status: {(allPassed ? "PASSED" : "FAILED")}");
            sb.AppendLine($"Sampled: {sampleFrames} frames");
            sb.AppendLine();

            // Table header
            sb.AppendLine("| Metric         | Avg      | Min      | Max      | P95      | Budget   | Result |");
            sb.AppendLine("|----------------|----------|----------|----------|----------|----------|--------|");

            foreach (var r in results)
            {
                string avg = FormatMetric(r.MetricName, r.Avg);
                string min = r.HasMinMax ? FormatMetric(r.MetricName, r.Min) : "-";
                string max = r.HasMinMax ? FormatMetric(r.MetricName, r.Max) : "-";
                string p95 = r.HasP95 ? FormatMetric(r.MetricName, r.P95) : "-";
                string budget = r.BudgetStr;
                string result = r.Passed ? "PASS" : "FAIL";

                sb.AppendLine($"| {r.MetricName,-14} | {avg,-8} | {min,-8} | {max,-8} | {p95,-8} | {budget,-8} | {result,-6} |");
            }

            // List failures
            var failures = results.Where(r => !r.Passed).ToList();
            if (failures.Count > 0)
            {
                sb.AppendLine();
                foreach (var f in failures)
                {
                    sb.AppendLine($"FAILED: {f.FailureReason}");
                }
            }

            return McpToolResult.Success(sb.ToString());
        }

        private static List<BudgetResult> EvaluateBudgets(FrameStatistics stats, BudgetConfig budgets)
        {
            var results = new List<BudgetResult>();

            // FPS: check P95 (worst-case, i.e. P5 of FPS distribution) against minimum
            if (budgets.MinFps.HasValue)
            {
                float p95Fps = stats.FpsP95;
                bool passed = p95Fps >= budgets.MinFps.Value;
                results.Add(new BudgetResult
                {
                    MetricName = "FPS",
                    Avg = stats.FpsAvg,
                    Min = stats.FpsMin,
                    Max = stats.FpsMax,
                    P95 = p95Fps,
                    HasMinMax = true,
                    HasP95 = true,
                    BudgetStr = $">= {budgets.MinFps.Value:F0}",
                    Passed = passed,
                    FailureReason = passed ? null : $"FPS below budget (P95: {p95Fps:F1} < {budgets.MinFps.Value:F0})"
                });
            }

            // Frame Time: check P95 (worst-case) against maximum
            if (budgets.MaxFrameTimeMs.HasValue)
            {
                float p95Ft = stats.FrameTimeP95;
                bool passed = p95Ft <= budgets.MaxFrameTimeMs.Value;
                results.Add(new BudgetResult
                {
                    MetricName = "Frame Time(ms)",
                    Avg = stats.FrameTimeAvg,
                    Min = stats.FrameTimeMin,
                    Max = stats.FrameTimeMax,
                    P95 = p95Ft,
                    HasMinMax = true,
                    HasP95 = true,
                    BudgetStr = $"<= {budgets.MaxFrameTimeMs.Value:F2}",
                    Passed = passed,
                    FailureReason = passed ? null : $"Frame Time exceeded budget (P95: {p95Ft:F1}ms > {budgets.MaxFrameTimeMs.Value:F2}ms)"
                });
            }

            // Memory: check average against max
            if (budgets.MaxMemoryMb.HasValue)
            {
                float memMb = stats.MemoryMbAvg;
                bool passed = memMb <= budgets.MaxMemoryMb.Value;
                results.Add(new BudgetResult
                {
                    MetricName = "Memory (MB)",
                    Avg = memMb,
                    Min = 0,
                    Max = 0,
                    P95 = 0,
                    HasMinMax = false,
                    HasP95 = false,
                    BudgetStr = $"<= {budgets.MaxMemoryMb.Value:F0}",
                    Passed = passed,
                    FailureReason = passed ? null : $"Memory exceeded budget ({memMb:F1}MB > {budgets.MaxMemoryMb.Value:F0}MB)"
                });
            }

            // Draw Calls: check P95 against max
            if (budgets.MaxDrawCalls.HasValue)
            {
                float p95Dc = stats.DrawCallsP95;
                bool passed = p95Dc <= budgets.MaxDrawCalls.Value;
                results.Add(new BudgetResult
                {
                    MetricName = "Draw Calls",
                    Avg = stats.DrawCallsAvg,
                    Min = stats.DrawCallsMin,
                    Max = stats.DrawCallsMax,
                    P95 = p95Dc,
                    HasMinMax = true,
                    HasP95 = true,
                    BudgetStr = $"<= {budgets.MaxDrawCalls.Value}",
                    Passed = passed,
                    FailureReason = passed ? null : $"Draw Calls exceeded budget (P95: {p95Dc:F0} > {budgets.MaxDrawCalls.Value})"
                });
            }

            // Triangles: check max against budget
            if (budgets.MaxTriangles.HasValue)
            {
                float maxTri = stats.TrianglesMax;
                bool passed = maxTri <= budgets.MaxTriangles.Value;
                results.Add(new BudgetResult
                {
                    MetricName = "Triangles",
                    Avg = stats.TrianglesAvg,
                    Min = 0,
                    Max = maxTri,
                    P95 = 0,
                    HasMinMax = true,
                    HasP95 = false,
                    BudgetStr = $"<= {FormatCount(budgets.MaxTriangles.Value)}",
                    Passed = passed,
                    FailureReason = passed ? null : $"Triangles exceeded budget (Max: {FormatCount((int)maxTri)} > {FormatCount(budgets.MaxTriangles.Value)})"
                });
            }

            // Batches: check P95 against max
            if (budgets.MaxBatches.HasValue)
            {
                float p95B = stats.BatchesP95;
                bool passed = p95B <= budgets.MaxBatches.Value;
                results.Add(new BudgetResult
                {
                    MetricName = "Batches",
                    Avg = stats.BatchesAvg,
                    Min = stats.BatchesMin,
                    Max = stats.BatchesMax,
                    P95 = p95B,
                    HasMinMax = true,
                    HasP95 = true,
                    BudgetStr = $"<= {budgets.MaxBatches.Value}",
                    Passed = passed,
                    FailureReason = passed ? null : $"Batches exceeded budget (P95: {p95B:F0} > {budgets.MaxBatches.Value})"
                });
            }

            return results;
        }

        #endregion

        #region Configure Action

        private McpToolResult ConfigureBudgets(JObject arguments)
        {
            var budgetsObj = arguments?["budgets"] as JObject;
            if (budgetsObj == null)
            {
                return McpToolResult.Error("budgets object is required for configure action.");
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Performance Budget Configuration Updated ===");
            sb.AppendLine();

            if (budgetsObj["min_fps"] != null)
            {
                float val = budgetsObj["min_fps"].Value<float>();
                EditorPrefs.SetFloat(k_PrefsPrefix + "min_fps", val);
                sb.AppendLine($"  min_fps: {val:F1}");
            }
            if (budgetsObj["max_frame_time_ms"] != null)
            {
                float val = budgetsObj["max_frame_time_ms"].Value<float>();
                EditorPrefs.SetFloat(k_PrefsPrefix + "max_frame_time_ms", val);
                sb.AppendLine($"  max_frame_time_ms: {val:F2}");
            }
            if (budgetsObj["max_memory_mb"] != null)
            {
                float val = budgetsObj["max_memory_mb"].Value<float>();
                EditorPrefs.SetFloat(k_PrefsPrefix + "max_memory_mb", val);
                sb.AppendLine($"  max_memory_mb: {val:F1}");
            }
            if (budgetsObj["max_draw_calls"] != null)
            {
                int val = budgetsObj["max_draw_calls"].Value<int>();
                EditorPrefs.SetInt(k_PrefsPrefix + "max_draw_calls", val);
                sb.AppendLine($"  max_draw_calls: {val}");
            }
            if (budgetsObj["max_triangles"] != null)
            {
                int val = budgetsObj["max_triangles"].Value<int>();
                EditorPrefs.SetInt(k_PrefsPrefix + "max_triangles", val);
                sb.AppendLine($"  max_triangles: {val}");
            }
            if (budgetsObj["max_batches"] != null)
            {
                int val = budgetsObj["max_batches"].Value<int>();
                EditorPrefs.SetInt(k_PrefsPrefix + "max_batches", val);
                sb.AppendLine($"  max_batches: {val}");
            }

            return McpToolResult.Success(sb.ToString());
        }

        #endregion

        #region Show Config Action

        private McpToolResult ShowConfig()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Current Performance Budget Configuration ===");
            sb.AppendLine();

            sb.AppendLine($"  min_fps:            {GetSavedFloat("min_fps", k_DefaultMinFps):F1}");
            sb.AppendLine($"  max_frame_time_ms:  {GetSavedFloat("max_frame_time_ms", k_DefaultMaxFrameTimeMs):F2}");
            sb.AppendLine($"  max_memory_mb:      {GetSavedFloat("max_memory_mb", k_DefaultMaxMemoryMb):F1}");
            sb.AppendLine($"  max_draw_calls:     {GetSavedInt("max_draw_calls", k_DefaultMaxDrawCalls)}");
            sb.AppendLine($"  max_triangles:      {GetSavedInt("max_triangles", k_DefaultMaxTriangles)}");
            sb.AppendLine($"  max_batches:        {GetSavedInt("max_batches", k_DefaultMaxBatches)}");
            sb.AppendLine();
            sb.AppendLine("Note: Values can be overridden per-test via the 'budgets' parameter.");

            return McpToolResult.Success(sb.ToString());
        }

        #endregion

        #region Budget Resolution

        /// <summary>
        /// Resolves budget values: argument overrides > EditorPrefs > defaults.
        /// </summary>
        internal BudgetConfig ResolveBudgets(JObject budgetsObj)
        {
            var config = new BudgetConfig();

            config.MinFps = GetBudgetFloat(budgetsObj, "min_fps", k_DefaultMinFps);
            config.MaxFrameTimeMs = GetBudgetFloat(budgetsObj, "max_frame_time_ms", k_DefaultMaxFrameTimeMs);
            config.MaxMemoryMb = GetBudgetFloat(budgetsObj, "max_memory_mb", k_DefaultMaxMemoryMb);
            config.MaxDrawCalls = GetBudgetInt(budgetsObj, "max_draw_calls", k_DefaultMaxDrawCalls);
            config.MaxTriangles = GetBudgetInt(budgetsObj, "max_triangles", k_DefaultMaxTriangles);
            config.MaxBatches = GetBudgetInt(budgetsObj, "max_batches", k_DefaultMaxBatches);

            return config;
        }

        private float? GetBudgetFloat(JObject budgetsObj, string key, float defaultValue)
        {
            // Argument override
            if (budgetsObj != null && budgetsObj[key] != null)
            {
                return budgetsObj[key].Value<float>();
            }
            // EditorPrefs
            return GetSavedFloat(key, defaultValue);
        }

        private int? GetBudgetInt(JObject budgetsObj, string key, int defaultValue)
        {
            if (budgetsObj != null && budgetsObj[key] != null)
            {
                return budgetsObj[key].Value<int>();
            }
            return GetSavedInt(key, defaultValue);
        }

        private static float GetSavedFloat(string key, float defaultValue)
        {
            string prefsKey = k_PrefsPrefix + key;
            return EditorPrefs.HasKey(prefsKey) ? EditorPrefs.GetFloat(prefsKey) : defaultValue;
        }

        private static int GetSavedInt(string key, int defaultValue)
        {
            string prefsKey = k_PrefsPrefix + key;
            return EditorPrefs.HasKey(prefsKey) ? EditorPrefs.GetInt(prefsKey) : defaultValue;
        }

        private static readonly string[] s_PersistedKeys =
        {
            "min_fps",
            "max_frame_time_ms",
            "max_memory_mb",
            "max_draw_calls",
            "max_triangles",
            "max_batches",
        };

        internal static void ClearPersistedConfig()
        {
            foreach (var key in s_PersistedKeys)
            {
                EditorPrefs.DeleteKey(k_PrefsPrefix + key);
            }
        }

        #endregion

        #region UnityStats Reflection

        private static void InitializeStatsReflection()
        {
            if (s_StatsReflectionInitialized) return;
            s_StatsReflectionInitialized = true;

            try
            {
                var editorAssembly = typeof(Editor).Assembly;
                s_UnityStatsType = editorAssembly.GetType("UnityEditor.UnityStats");

                if (s_UnityStatsType != null)
                {
                    s_DrawCallsProp = s_UnityStatsType.GetProperty("drawCalls",
                        BindingFlags.Public | BindingFlags.Static);
                    s_BatchesProp = s_UnityStatsType.GetProperty("batches",
                        BindingFlags.Public | BindingFlags.Static);
                    s_TrianglesProp = s_UnityStatsType.GetProperty("triangles",
                        BindingFlags.Public | BindingFlags.Static);

                    // Fallback: try alternate property names
                    if (s_DrawCallsProp == null)
                    {
                        s_DrawCallsProp = s_UnityStatsType.GetProperty("drawCallCount",
                            BindingFlags.Public | BindingFlags.Static);
                    }
                    if (s_BatchesProp == null)
                    {
                        s_BatchesProp = s_UnityStatsType.GetProperty("batchCount",
                            BindingFlags.Public | BindingFlags.Static);
                    }
                    if (s_TrianglesProp == null)
                    {
                        s_TrianglesProp = s_UnityStatsType.GetProperty("triangleCount",
                            BindingFlags.Public | BindingFlags.Static);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PerformanceBudgetTest] UnityStats reflection failed: {ex.Message}");
            }
        }

        private static int GetStatsInt(PropertyInfo prop)
        {
            if (prop == null) return 0;
            try
            {
                var value = prop.GetValue(null);
                if (value is int intVal) return intVal;
                if (value is long longVal) return (int)longVal;
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Formatting Helpers

        private static string FormatMetric(string metricName, float value)
        {
            if (metricName.Contains("FPS"))
                return value.ToString("F1");
            if (metricName.Contains("Frame Time"))
                return value.ToString("F1");
            if (metricName.Contains("Memory"))
                return value.ToString("F1");
            if (metricName.Contains("Triangles"))
                return FormatCount((int)value);
            return value.ToString("F0");
        }

        private static string FormatCount(int count)
        {
            if (count >= 1000000)
                return $"{count / 1000000f:F1}M";
            if (count >= 1000)
                return $"{count / 1000f:F1}k";
            return count.ToString();
        }

        #endregion

        #region Data Structures

        internal class BudgetConfig
        {
            public float? MinFps { get; set; }
            public float? MaxFrameTimeMs { get; set; }
            public float? MaxMemoryMb { get; set; }
            public int? MaxDrawCalls { get; set; }
            public int? MaxTriangles { get; set; }
            public int? MaxBatches { get; set; }
        }

        internal class FrameSample
        {
            public float Fps;
            public float FrameTimeMs;
            public long MemoryBytes;
            public long ReservedMemoryBytes;
            public int DrawCalls;
            public int Batches;
            public int Triangles;
        }

        internal class FrameSampleCollection
        {
            private readonly List<FrameSample> _samples;

            public FrameSampleCollection(int capacity)
            {
                _samples = new List<FrameSample>(capacity);
            }

            public void Add(FrameSample sample) => _samples.Add(sample);
            public int Count => _samples.Count;

            public FrameStatistics ComputeStatistics()
            {
                if (_samples.Count == 0)
                    return new FrameStatistics();

                var fpsList = _samples.Select(s => s.Fps).OrderBy(v => v).ToList();
                var ftList = _samples.Select(s => s.FrameTimeMs).OrderBy(v => v).ToList();
                var dcList = _samples.Select(s => (float)s.DrawCalls).OrderBy(v => v).ToList();
                var batchList = _samples.Select(s => (float)s.Batches).OrderBy(v => v).ToList();
                var triList = _samples.Select(s => (float)s.Triangles).OrderBy(v => v).ToList();

                return new FrameStatistics
                {
                    FpsAvg = fpsList.Average(),
                    FpsMin = fpsList.First(),
                    FpsMax = fpsList.Last(),
                    FpsP95 = Percentile(fpsList, 5),    // P5 of FPS = worst 5% (low FPS)

                    FrameTimeAvg = ftList.Average(),
                    FrameTimeMin = ftList.First(),
                    FrameTimeMax = ftList.Last(),
                    FrameTimeP95 = Percentile(ftList, 95), // P95 of frame time = worst 5%

                    MemoryMbAvg = (float)(_samples.Average(s => s.MemoryBytes) / (1024.0 * 1024.0)),

                    DrawCallsAvg = dcList.Average(),
                    DrawCallsMin = dcList.First(),
                    DrawCallsMax = dcList.Last(),
                    DrawCallsP95 = Percentile(dcList, 95),

                    BatchesAvg = batchList.Average(),
                    BatchesMin = batchList.First(),
                    BatchesMax = batchList.Last(),
                    BatchesP95 = Percentile(batchList, 95),

                    TrianglesAvg = triList.Average(),
                    TrianglesMax = triList.Last(),
                };
            }

            /// <summary>
            /// Computes the Nth percentile from a sorted list.
            /// </summary>
            internal static float Percentile(List<float> sorted, int percentile)
            {
                if (sorted.Count == 0) return 0f;
                if (sorted.Count == 1) return sorted[0];

                float index = (percentile / 100f) * (sorted.Count - 1);
                int lower = (int)Math.Floor(index);
                int upper = (int)Math.Ceiling(index);

                if (lower == upper || upper >= sorted.Count)
                    return sorted[lower];

                float fraction = index - lower;
                return sorted[lower] + fraction * (sorted[upper] - sorted[lower]);
            }
        }

        internal class FrameStatistics
        {
            public float FpsAvg, FpsMin, FpsMax, FpsP95;
            public float FrameTimeAvg, FrameTimeMin, FrameTimeMax, FrameTimeP95;
            public float MemoryMbAvg;
            public float DrawCallsAvg, DrawCallsMin, DrawCallsMax, DrawCallsP95;
            public float BatchesAvg, BatchesMin, BatchesMax, BatchesP95;
            public float TrianglesAvg, TrianglesMax;
        }

        private class BudgetResult
        {
            public string MetricName;
            public float Avg, Min, Max, P95;
            public bool HasMinMax;
            public bool HasP95;
            public string BudgetStr;
            public bool Passed;
            public string FailureReason;
        }

        #endregion
    }
}
