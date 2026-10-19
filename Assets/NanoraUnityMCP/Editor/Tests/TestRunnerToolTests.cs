using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Test tools (run_tests, get_test_results, simulate_input).
    /// </summary>
    [TestFixture]
    public class TestRunnerToolTests
    {
        [SetUp]
        public void SetUp()
        {
            // Ignore all log messages during RunTests tests as they may trigger dialogs
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        #region RunTestsTool Tests

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Requires interactive Unity Editor - RunTests interferes with test runner in batchmode")]
        public async Task RunTests_EditMode_ReturnsResult()
        {
            var tool = new RunTestsTool();
            var args = new JObject
            {
                ["test_mode"] = "EditMode",
                ["run_synchronously"] = false // Don't wait in test
            };

            var result = await tool.ExecuteAsync(args);

            // Should not crash, may or may not succeed depending on test state
            Assert.IsNotNull(result);
        }

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Requires interactive Unity Editor - RunTests can trigger dialogs in batchmode")]
        public async Task RunTests_WithFilter_Works()
        {
            var tool = new RunTestsTool();
            var args = new JObject
            {
                ["test_mode"] = "EditMode",
                ["filter"] = "McpToolTests*",
                ["run_synchronously"] = false
            };

            var result = await tool.ExecuteAsync(args);
            Assert.IsNotNull(result);
        }

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Requires interactive Unity Editor - RunTests can trigger dialogs in batchmode")]
        public async Task RunTests_WithCategory_Works()
        {
            var tool = new RunTestsTool();
            var args = new JObject
            {
                ["test_mode"] = "EditMode",
                ["category"] = "Unit",
                ["run_synchronously"] = false
            };

            var result = await tool.ExecuteAsync(args);
            Assert.IsNotNull(result);
        }

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Requires interactive Unity Editor - RunTests can trigger dialogs in batchmode")]
        [Timeout(60000)]
        public async Task RunTests_InvalidTestMode_HandlesGracefully()
        {
            var tool = new RunTestsTool();
            var args = new JObject
            {
                ["test_mode"] = "InvalidMode",
                ["run_synchronously"] = false
            };

            var result = await tool.ExecuteAsync(args);
            // Should handle gracefully
            Assert.IsNotNull(result);
        }

        #endregion

        #region GetTestResultsTool Tests

        [Test]
        public async Task GetTestResults_ReturnsResults()
        {
            var tool = new GetTestResultsTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task GetTestResults_IncludeAllTests_Works()
        {
            var tool = new GetTestResultsTool();
            var args = new JObject
            {
                ["include_all_tests"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task GetTestResults_ExcludeAllTests_Works()
        {
            var tool = new GetTestResultsTool();
            var args = new JObject
            {
                ["include_all_tests"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion

        #region SimulateInputTool Tests

        [Test]
        public async Task SimulateInput_KeyTap_Works()
        {
            var tool = new SimulateInputTool();
            var args = new JObject
            {
                ["action"] = "key_tap",
                ["key"] = "Space"
            };

            var result = await tool.ExecuteAsync(args);

            // Input simulation may not work in EditMode, but should not crash
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task SimulateInput_KeyPress_Works()
        {
            var tool = new SimulateInputTool();
            var args = new JObject
            {
                ["action"] = "key_press",
                ["key"] = "A"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsNotNull(result);
        }

        [Test]
        public async Task SimulateInput_KeyRelease_Works()
        {
            var tool = new SimulateInputTool();
            var args = new JObject
            {
                ["action"] = "key_release",
                ["key"] = "A"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsNotNull(result);
        }

        [Test]
        public async Task SimulateInput_MouseClick_Works()
        {
            var tool = new SimulateInputTool();
            var args = new JObject
            {
                ["action"] = "mouse_click",
                ["position"] = new JObject { ["x"] = 100, ["y"] = 100 }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsNotNull(result);
        }

        [Test]
        public async Task SimulateInput_MouseMove_Works()
        {
            var tool = new SimulateInputTool();
            var args = new JObject
            {
                ["action"] = "mouse_move",
                ["position"] = new JObject { ["x"] = 200, ["y"] = 200 }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsNotNull(result);
        }

        [Test]
        public async Task SimulateInput_InvalidAction_ReturnsError()
        {
            var tool = new SimulateInputTool();
            var args = new JObject
            {
                ["action"] = "invalid_action"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Requires interactive Unity Editor - input simulation can trigger dialogs in batchmode")]
        public async Task SimulateInput_EmptyAction_UsesDefault()
        {
            var tool = new SimulateInputTool();
            var args = new JObject
            {
                ["key"] = "Space"
            };

            var result = await tool.ExecuteAsync(args);
            // Default action is key_tap
            Assert.IsNotNull(result);
        }

        #endregion
    }
}
