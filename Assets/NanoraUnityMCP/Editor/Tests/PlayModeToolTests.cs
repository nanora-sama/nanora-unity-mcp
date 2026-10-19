using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for PlayMode tools (manage_playmode).
    /// Note: Actually entering/exiting play mode is difficult to test in EditMode tests.
    /// These tests focus on the status action and parameter validation.
    /// </summary>
    [TestFixture]
    public class PlayModeToolTests
    {
        #region ManagePlayModeTool Tests

        [Test]
        public async Task ManagePlayMode_Status_ReturnsCurrentState()
        {
            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "status"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Play Mode Status") || text.Contains("State"));
        }

        [Test]
        public async Task ManagePlayMode_Status_ShowsNotPlaying()
        {
            // In EditMode tests, we should not be in play mode
            Assert.IsFalse(EditorApplication.isPlaying);

            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "status"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            // Should indicate not playing
            Assert.IsTrue(text.Contains("Stopped") || text.Contains("Edit") || text.Contains("Not playing"));
        }

        [Test]
        public async Task ManagePlayMode_InvalidAction_ReturnsError()
        {
            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "invalid_action"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ManagePlayMode_EmptyAction_ReturnsError()
        {
            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ManagePlayMode_NoAction_ReturnsError()
        {
            var tool = new ManagePlayModeTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        // Note: Testing actual play/stop/pause/resume would require PlayMode tests
        // and careful handling of domain reloads. These are skipped for EditMode.

        [Test]
        public async Task ManagePlayMode_StopWhenNotPlaying_HandlesGracefully()
        {
            // When not in play mode, stop should handle gracefully
            Assert.IsFalse(EditorApplication.isPlaying);

            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "stop"
            };

            var result = await tool.ExecuteAsync(args);

            // Should not crash, may return success or info that already stopped
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ManagePlayMode_PauseWhenNotPlaying_HandlesGracefully()
        {
            Assert.IsFalse(EditorApplication.isPlaying);

            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "pause"
            };

            var result = await tool.ExecuteAsync(args);

            // Should not crash
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ManagePlayMode_ResumeWhenNotPlaying_HandlesGracefully()
        {
            Assert.IsFalse(EditorApplication.isPlaying);

            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "resume"
            };

            var result = await tool.ExecuteAsync(args);

            // Should not crash
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ManagePlayMode_StepWhenNotPlaying_HandlesGracefully()
        {
            Assert.IsFalse(EditorApplication.isPlaying);

            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "step"
            };

            var result = await tool.ExecuteAsync(args);

            // Should not crash
            Assert.IsNotNull(result);
        }

        #endregion
    }
}
