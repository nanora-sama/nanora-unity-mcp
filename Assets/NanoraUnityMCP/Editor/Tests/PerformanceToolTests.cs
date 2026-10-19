using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Performance tools (profile_cpu).
    /// Note: analyze_frame, analyze_memory, get_profiler_stats are tested in McpToolTests.cs
    /// </summary>
    [TestFixture]
    public class PerformanceToolTests
    {
        #region ProfileCpuTool Tests

        [Test]
        public async Task ProfileCpu_ReturnsReport()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("CPU") || text.Contains("Profile") || text.Contains("profil"));
        }

        [Test]
        public async Task ProfileCpu_WithMarkers_Works()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject
            {
                ["markers"] = new JArray { "common" }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task ProfileCpu_WithSampleCount_Works()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject
            {
                ["sample_count"] = 30
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task ProfileCpu_WithGC_Works()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject
            {
                ["include_gc"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task ProfileCpu_WithoutGC_Works()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject
            {
                ["include_gc"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task ProfileCpu_WithSystemStats_Works()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject
            {
                ["show_system_stats"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task ProfileCpu_WithoutSystemStats_Works()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject
            {
                ["show_system_stats"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task ProfileCpu_CustomMarkers_Works()
        {
            var tool = new ProfileCpuTool();
            var args = new JObject
            {
                ["markers"] = new JArray { "Main Thread", "Rendering" }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion
    }
}
