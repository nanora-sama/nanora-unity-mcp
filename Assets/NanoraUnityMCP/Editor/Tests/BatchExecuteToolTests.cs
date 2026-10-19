using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for BatchExecuteTool.
    /// Tests input validation and tool properties.
    /// Actual batch execution requires ToolRegistry and Unity main thread.
    /// </summary>
    [TestFixture]
    public class BatchExecuteToolTests
    {
        private BatchExecuteTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new BatchExecuteTool();
        }

        #region Tool Properties

        [Test]
        public void Name_IsCorrect()
        {
            Assert.AreEqual("batch_execute", m_tool.Name);
        }

        [Test]
        public void Description_IsNotEmpty()
        {
            Assert.IsNotNull(m_tool.Description);
            Assert.IsTrue(m_tool.Description.Length > 0);
        }

        [Test]
        public void InputSchema_IsValid()
        {
            var schema = m_tool.InputSchema;
            Assert.IsNotNull(schema);
            Assert.AreEqual("object", schema["type"]?.Value<string>());
            var required = schema["required"] as JArray;
            Assert.IsNotNull(required);
            Assert.IsTrue(required.Count >= 1);
        }

        [Test]
        public void InputSchema_HasCommandsArray()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props["commands"]);
            Assert.AreEqual("array", props["commands"]["type"]?.Value<string>());
        }

        [Test]
        public void InputSchema_HasAtomicBoolean()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props["atomic"]);
            Assert.AreEqual("boolean", props["atomic"]["type"]?.Value<string>());
        }

        [Test]
        public void InputSchema_HasStopOnErrorBoolean()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props["stop_on_error"]);
            Assert.AreEqual("boolean", props["stop_on_error"]["type"]?.Value<string>());
        }

        #endregion

        #region Input Validation

        [Test]
        public async Task NoRegistry_ReturnsError()
        {
            var args = new JObject
            {
                ["commands"] = new JArray
                {
                    new JObject { ["tool"] = "get_scene_hierarchy" }
                }
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("ToolRegistry"));
        }

        [Test]
        public async Task EmptyCommands_ReturnsError()
        {
            // Even without registry, empty commands should fail first
            var args = new JObject
            {
                ["commands"] = new JArray()
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("commands"));
        }

        [Test]
        public async Task NullCommands_ReturnsError()
        {
            var args = new JObject();
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("commands"));
        }

        [Test]
        public async Task NullArguments_ReturnsError()
        {
            var result = await m_tool.ExecuteAsync(null);
            Assert.IsTrue(result.IsError);
        }

        #endregion
    }
}
