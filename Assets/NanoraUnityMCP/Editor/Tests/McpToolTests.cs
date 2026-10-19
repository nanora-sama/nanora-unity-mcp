using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for MCP tools.
    /// </summary>
    [TestFixture]
    public class McpToolTests
    {
        #region GetSceneHierarchyTool Tests

        [Test]
        public async Task GetSceneHierarchy_ReturnsValidResult()
        {
            var tool = new GetSceneHierarchyTool();

            var result = await tool.ExecuteAsync(new JObject());

            Assert.IsFalse(result.IsError);
            Assert.IsNotEmpty(result.Content);
        }

        [Test]
        public async Task GetSceneHierarchy_WithMaxDepth_LimitsHierarchy()
        {
            var tool = new GetSceneHierarchyTool();
            var args = JObject.Parse("{\"max_depth\": 1}");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion

        #region GetObjectDetailsTool Tests

        [Test]
        public async Task GetObjectDetails_WithValidSelector_ReturnsDetails()
        {
            var testObj = new GameObject("TestDetailObject");
            try
            {
                var tool = new GetObjectDetailsTool();
                // GetObjectDetailsTool uses object_name, not selector
                var args = JObject.Parse("{\"object_name\": \"TestDetailObject\"}");

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("TestDetailObject")));
            }
            finally
            {
                Object.DestroyImmediate(testObj);
            }
        }

        [Test]
        public async Task GetObjectDetails_WithInvalidSelector_ReturnsError()
        {
            var tool = new GetObjectDetailsTool();
            // GetObjectDetailsTool uses object_name, not selector
            var args = JObject.Parse("{\"object_name\": \"NonExistentObject12345\"}");

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        #endregion

        #region CreateGameObjectTool Tests

        [Test]
        public async Task CreateGameObject_CreatesObject()
        {
            var tool = new CreateGameObjectTool();
            var args = JObject.Parse("{\"name\": \"CreatedByTest\", \"primitive_type\": \"Cube\"}");

            var result = await tool.ExecuteAsync(args);
            var created = GameObject.Find("CreatedByTest");

            try
            {
                Assert.IsFalse(result.IsError);
                Assert.IsNotNull(created);
            }
            finally
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }
        }

        [Test]
        public async Task CreateGameObject_WithPosition_SetsPosition()
        {
            var tool = new CreateGameObjectTool();
            var args = JObject.Parse(@"{
                ""name"": ""PositionedObject"",
                ""position"": { ""x"": 1, ""y"": 2, ""z"": 3 }
            }");

            var result = await tool.ExecuteAsync(args);
            var created = GameObject.Find("PositionedObject");

            try
            {
                Assert.IsFalse(result.IsError);
                Assert.IsNotNull(created);
                Assert.AreEqual(new Vector3(1, 2, 3), created.transform.position);
            }
            finally
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }
        }

        #endregion

        #region QueryObjectsTool Tests

        [Test]
        public async Task QueryObjects_WithValidSelector_ReturnsResults()
        {
            var testObj = new GameObject("QueryTestObject");
            try
            {
                var tool = new QueryObjectsTool();
                var args = JObject.Parse("{\"selector\": \"#QueryTestObject\"}");

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("QueryTestObject")));
            }
            finally
            {
                Object.DestroyImmediate(testObj);
            }
        }

        [Test]
        public async Task QueryObjects_WithTypeSelector_FindsComponents()
        {
            var testObj = new GameObject("CameraObject");
            testObj.AddComponent<Camera>();

            try
            {
                var tool = new QueryObjectsTool();
                var args = JObject.Parse("{\"selector\": \".Camera\"}");

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("CameraObject")));
            }
            finally
            {
                Object.DestroyImmediate(testObj);
            }
        }

        #endregion

        #region SearchAssetsTool Tests

        [Test]
        public async Task SearchAssets_ReturnsResults()
        {
            var tool = new SearchAssetsTool();
            var args = JObject.Parse("{\"query\": \"default\", \"type_filter\": \"material\"}");

            var result = await tool.ExecuteAsync(args);

            // Should not error even if no results
            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task SearchAssets_WithEmptyQuery_ReturnsError()
        {
            var tool = new SearchAssetsTool();
            var args = JObject.Parse("{\"query\": \"\"}");

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        #endregion

        #region AnalyzeMemoryTool Tests

        [Test]
        public async Task AnalyzeMemory_ReturnsReport()
        {
            var tool = new AnalyzeMemoryTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Memory")));
        }

        #endregion

        #region AnalyzeFrameTool Tests

        [Test]
        public async Task AnalyzeFrame_ReturnsReport()
        {
            var tool = new AnalyzeFrameTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Frame")));
        }

        #endregion

        #region GetProfilerStatsTool Tests

        [Test]
        public async Task GetProfilerStats_ReturnsStats()
        {
            var tool = new GetProfilerStatsTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Memory")));
        }

        #endregion
    }
}
