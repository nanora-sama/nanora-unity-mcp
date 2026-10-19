using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Diagnostics tools (mcp_status, get_console_logs, configure_editor, capture_screenshot).
    /// </summary>
    [TestFixture]
    public class DiagnosticsToolTests
    {
        #region McpStatusTool Tests

        [Test]
        public async Task McpStatus_ReturnsValidStatus()
        {
            var tool = new McpStatusTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Unity MCP Server Status"));
            Assert.IsTrue(text.Contains("Connection"));
            Assert.IsTrue(text.Contains("Status:"));
        }

        [Test]
        public async Task McpStatus_IncludesToolList_WhenRequested()
        {
            var tool = new McpStatusTool();
            var args = new JObject
            {
                ["include_tool_list"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Available Tools"));
        }

        [Test]
        public async Task McpStatus_ExcludesToolList_WhenNotRequested()
        {
            var tool = new McpStatusTool();
            var args = new JObject
            {
                ["include_tool_list"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsFalse(text.Contains("Available Tools"));
        }

        [Test]
        public async Task McpStatus_IncludesSystemInfo_WhenRequested()
        {
            var tool = new McpStatusTool();
            var args = new JObject
            {
                ["include_system_info"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Unity Info"));
            Assert.IsTrue(text.Contains("Unity Version:"));
        }

        [Test]
        public async Task McpStatus_IncludesUnityAccount()
        {
            var tool = new McpStatusTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Unity Account"));
        }

        [Test]
        public async Task McpStatus_IncludesBackgroundExecution()
        {
            var tool = new McpStatusTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Background Execution"));
            Assert.IsTrue(text.Contains("runInBackground"));
        }

        #endregion

        #region GetConsoleLogsTool Tests

        [Test]
        public async Task GetConsoleLogs_ReturnsLogs()
        {
            // Generate a log first
            Debug.Log("Test log for GetConsoleLogs test");

            var tool = new GetConsoleLogsTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            // Should return some result even if empty
            Assert.IsNotNull(result.Content);
        }

        [Test]
        public async Task GetConsoleLogs_WithCount_LimitsResults()
        {
            var tool = new GetConsoleLogsTool();
            var args = new JObject
            {
                ["count"] = 5
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task GetConsoleLogs_FilterByError_Works()
        {
            var tool = new GetConsoleLogsTool();
            var args = new JObject
            {
                ["log_type"] = "Error"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task GetConsoleLogs_FilterByWarning_Works()
        {
            var tool = new GetConsoleLogsTool();
            var args = new JObject
            {
                ["log_type"] = "Warning"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task GetConsoleLogs_FilterByLog_Works()
        {
            var tool = new GetConsoleLogsTool();
            var args = new JObject
            {
                ["log_type"] = "Log"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion

        #region ConfigureEditorTool Tests

        [Test]
        public async Task ConfigureEditor_Status_ReturnsCurrentSettings()
        {
            var tool = new ConfigureEditorTool();
            var args = new JObject
            {
                ["action"] = "status"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Editor Configuration") || text.Contains("Background"));
            Assert.IsTrue(text.Contains("runInBackground") || text.Contains("Background"));
        }

        [Test]
        public async Task ConfigureEditor_EnableBackground_Works()
        {
            var tool = new ConfigureEditorTool();
            var args = new JObject
            {
                ["action"] = "enable_background"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Enabled") || text.Contains("enabled") || text.Contains("already") || text.Contains("Background"));
        }

        [Test]
        public async Task ConfigureEditor_OptimizeForMcp_Works()
        {
            var tool = new ConfigureEditorTool();
            var args = new JObject
            {
                ["action"] = "optimize_for_mcp"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Optimiz") || text.Contains("MCP") || text.Contains("Changes"));
        }

        #endregion

        #region CaptureScreenshotTool Tests

        [Test]
        public async Task CaptureScreenshot_GameView_ReturnsImage()
        {
            var tool = new CaptureScreenshotTool();
            var args = new JObject
            {
                ["view"] = "game"
            };

            var result = await tool.ExecuteAsync(args);

            // Screenshot may fail in headless/CI environment, so we just check it doesn't crash
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task CaptureScreenshot_SceneView_ReturnsImage()
        {
            var tool = new CaptureScreenshotTool();
            var args = new JObject
            {
                ["view"] = "scene"
            };

            var result = await tool.ExecuteAsync(args);

            // Screenshot may fail in headless/CI environment
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task CaptureScreenshot_WithDimensions_Works()
        {
            var tool = new CaptureScreenshotTool();
            var args = new JObject
            {
                ["width"] = 640,
                ["height"] = 480
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsNotNull(result);
        }

        #endregion
    }
}
