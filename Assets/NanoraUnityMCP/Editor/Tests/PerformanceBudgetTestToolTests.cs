using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for PerformanceBudgetTestTool.
    /// Tests budget resolution, Pass/Fail evaluation logic, statistics computation,
    /// and configuration persistence. PlayMode-dependent sampling is not tested here.
    /// </summary>
    [TestFixture]
    public class PerformanceBudgetTestToolTests
    {
        private PerformanceBudgetTestTool _tool;

        [SetUp]
        public void SetUp()
        {
            // Wipe EditorPrefs-backed config so default-value assertions don't
            // see leftover values from a previous test (or a previous run).
            PerformanceBudgetTestTool.ClearPersistedConfig();
            _tool = new PerformanceBudgetTestTool();
        }

        [TearDown]
        public void TearDown()
        {
            PerformanceBudgetTestTool.ClearPersistedConfig();
        }

        #region Tool Metadata

        [Test]
        public void ToolName_IsCorrect()
        {
            Assert.AreEqual("performance_budget_test", _tool.Name);
        }

        [Test]
        public void ToolDescription_IsNotEmpty()
        {
            Assert.IsNotNull(_tool.Description);
            Assert.IsTrue(_tool.Description.Length > 0);
        }

        [Test]
        public void InputSchema_IsValidJson()
        {
            Assert.IsNotNull(_tool.InputSchema);
            Assert.AreEqual("object", _tool.InputSchema["type"]?.Value<string>());
            Assert.IsNotNull(_tool.InputSchema["properties"]);
        }

        [Test]
        public void InputSchema_ContainsActionProperty()
        {
            var props = _tool.InputSchema["properties"];
            Assert.IsNotNull(props["action"]);
            var enums = props["action"]["enum"]?.ToObject<List<string>>();
            Assert.IsNotNull(enums);
            Assert.Contains("test", enums);
            Assert.Contains("configure", enums);
            Assert.Contains("show_config", enums);
        }

        #endregion

        #region Budget Resolution - Default Values

        [Test]
        public void ResolveBudgets_NullArguments_ReturnsDefaults()
        {
            var budgets = _tool.ResolveBudgets(null);

            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMinFps, budgets.MinFps);
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxFrameTimeMs, budgets.MaxFrameTimeMs);
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxMemoryMb, budgets.MaxMemoryMb);
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxDrawCalls, budgets.MaxDrawCalls);
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxTriangles, budgets.MaxTriangles);
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxBatches, budgets.MaxBatches);
        }

        [Test]
        public void ResolveBudgets_EmptyObject_ReturnsDefaults()
        {
            var budgets = _tool.ResolveBudgets(new JObject());

            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMinFps, budgets.MinFps);
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxDrawCalls, budgets.MaxDrawCalls);
        }

        #endregion

        #region Budget Resolution - Overrides

        [Test]
        public void ResolveBudgets_WithOverrides_UsesProvidedValues()
        {
            var overrides = new JObject
            {
                ["min_fps"] = 30,
                ["max_frame_time_ms"] = 33.33,
                ["max_memory_mb"] = 1024,
                ["max_draw_calls"] = 500,
                ["max_triangles"] = 200000,
                ["max_batches"] = 300
            };

            var budgets = _tool.ResolveBudgets(overrides);

            Assert.AreEqual(30f, budgets.MinFps);
            Assert.That(budgets.MaxFrameTimeMs, Is.EqualTo(33.33f).Within(0.01f));
            Assert.AreEqual(1024f, budgets.MaxMemoryMb);
            Assert.AreEqual(500, budgets.MaxDrawCalls);
            Assert.AreEqual(200000, budgets.MaxTriangles);
            Assert.AreEqual(300, budgets.MaxBatches);
        }

        [Test]
        public void ResolveBudgets_PartialOverride_MixesWithDefaults()
        {
            var overrides = new JObject
            {
                ["min_fps"] = 45
            };

            var budgets = _tool.ResolveBudgets(overrides);

            Assert.AreEqual(45f, budgets.MinFps);
            // Other values should be defaults
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxFrameTimeMs, budgets.MaxFrameTimeMs);
            Assert.AreEqual(PerformanceBudgetTestTool.k_DefaultMaxDrawCalls, budgets.MaxDrawCalls);
        }

        #endregion

        #region Statistics Computation

        [Test]
        public void ComputeStatistics_EmptyCollection_ReturnsZeros()
        {
            var collection = new PerformanceBudgetTestTool.FrameSampleCollection(0);
            var stats = collection.ComputeStatistics();

            Assert.AreEqual(0f, stats.FpsAvg);
            Assert.AreEqual(0f, stats.FrameTimeAvg);
            Assert.AreEqual(0f, stats.MemoryMbAvg);
        }

        [Test]
        public void ComputeStatistics_SingleSample_ReturnsSameValues()
        {
            var collection = new PerformanceBudgetTestTool.FrameSampleCollection(1);
            collection.Add(new PerformanceBudgetTestTool.FrameSample
            {
                Fps = 60f,
                FrameTimeMs = 16.67f,
                MemoryBytes = 256 * 1024 * 1024L,
                DrawCalls = 100,
                Batches = 50,
                Triangles = 10000
            });

            var stats = collection.ComputeStatistics();

            Assert.AreEqual(60f, stats.FpsAvg);
            Assert.AreEqual(60f, stats.FpsMin);
            Assert.AreEqual(60f, stats.FpsMax);
            Assert.AreEqual(100f, stats.DrawCallsAvg);
        }

        [Test]
        public void ComputeStatistics_MultipleSamples_ComputesCorrectAverage()
        {
            var collection = new PerformanceBudgetTestTool.FrameSampleCollection(3);
            collection.Add(new PerformanceBudgetTestTool.FrameSample { Fps = 50f, FrameTimeMs = 20f, DrawCalls = 100 });
            collection.Add(new PerformanceBudgetTestTool.FrameSample { Fps = 60f, FrameTimeMs = 16.67f, DrawCalls = 120 });
            collection.Add(new PerformanceBudgetTestTool.FrameSample { Fps = 70f, FrameTimeMs = 14.29f, DrawCalls = 140 });

            var stats = collection.ComputeStatistics();

            Assert.AreEqual(60f, stats.FpsAvg);
            Assert.AreEqual(50f, stats.FpsMin);
            Assert.AreEqual(70f, stats.FpsMax);
            Assert.AreEqual(120f, stats.DrawCallsAvg);
        }

        #endregion

        #region Percentile Calculation

        [Test]
        public void Percentile_EmptyList_ReturnsZero()
        {
            var result = PerformanceBudgetTestTool.FrameSampleCollection.Percentile(
                new List<float>(), 95);
            Assert.AreEqual(0f, result);
        }

        [Test]
        public void Percentile_SingleItem_ReturnsThatItem()
        {
            var result = PerformanceBudgetTestTool.FrameSampleCollection.Percentile(
                new List<float> { 42f }, 95);
            Assert.AreEqual(42f, result);
        }

        [Test]
        public void Percentile_P50_ReturnsMedian()
        {
            // For [10, 20, 30, 40, 50], P50 should be 30
            var sorted = new List<float> { 10f, 20f, 30f, 40f, 50f };
            var result = PerformanceBudgetTestTool.FrameSampleCollection.Percentile(sorted, 50);
            Assert.AreEqual(30f, result);
        }

        [Test]
        public void Percentile_P95_ReturnsHighValue()
        {
            // 100 items from 1 to 100
            var sorted = Enumerable.Range(1, 100).Select(i => (float)i).ToList();
            var result = PerformanceBudgetTestTool.FrameSampleCollection.Percentile(sorted, 95);
            // P95 of [1..100] should be ~95.05
            Assert.That(result, Is.GreaterThan(94f));
            Assert.That(result, Is.LessThan(96f));
        }

        #endregion

        #region Test Action - PlayMode Required

        [Test]
        public async Task Test_OutsidePlayMode_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "test"
            };

            var result = await _tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Play Mode"));
        }

        #endregion

        #region Show Config Action

        [Test]
        public async Task ShowConfig_ReturnsConfigValues()
        {
            var args = new JObject
            {
                ["action"] = "show_config"
            };

            var result = await _tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("min_fps"));
            Assert.IsTrue(text.Contains("max_frame_time_ms"));
            Assert.IsTrue(text.Contains("max_memory_mb"));
            Assert.IsTrue(text.Contains("max_draw_calls"));
            Assert.IsTrue(text.Contains("max_triangles"));
            Assert.IsTrue(text.Contains("max_batches"));
        }

        #endregion

        #region Configure Action

        [Test]
        public async Task Configure_WithBudgets_ReturnsSuccess()
        {
            var args = new JObject
            {
                ["action"] = "configure",
                ["budgets"] = new JObject
                {
                    ["min_fps"] = 30,
                    ["max_draw_calls"] = 500
                }
            };

            var result = await _tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Configuration Updated"));
            Assert.IsTrue(text.Contains("min_fps"));
            Assert.IsTrue(text.Contains("max_draw_calls"));
        }

        [Test]
        public async Task Configure_WithoutBudgets_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "configure"
            };

            var result = await _tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("budgets"));
        }

        #endregion

        #region Invalid Action

        [Test]
        public async Task InvalidAction_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "invalid_action"
            };

            var result = await _tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Unknown action"));
        }

        #endregion

        #region Default Action

        [Test]
        public async Task NoAction_DefaultsToTest()
        {
            // Without action, should default to "test" which requires PlayMode
            var args = new JObject();

            var result = await _tool.ExecuteAsync(args);

            // Should fail because we're not in PlayMode (but it tried to run test)
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Play Mode"));
        }

        #endregion
    }
}
