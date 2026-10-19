using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for EditorOperationsTool.
    /// Tests input validation and tool properties.
    /// Actual undo/redo/selection operations require Unity Editor main thread.
    /// </summary>
    [TestFixture]
    public class EditorOperationsToolTests
    {
        private EditorOperationsTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new EditorOperationsTool();
        }

        #region Tool Properties

        [Test]
        public void Name_IsCorrect()
        {
            Assert.AreEqual("editor_operations", m_tool.Name);
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
        public void InputSchema_HasActionEnum()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props["action"]);
            var enumValues = props["action"]["enum"] as JArray;
            Assert.IsNotNull(enumValues);
            Assert.IsTrue(enumValues.Count == 5);
        }

        [Test]
        public void InputSchema_HasSelectorProperty()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props["selector"]);
        }

        #endregion

        #region Input Validation

        [Test]
        public async Task MissingAction_ReturnsError()
        {
            var args = new JObject();
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("action"));
        }

        [Test]
        public async Task UnknownAction_ReturnsError()
        {
            var args = new JObject { ["action"] = "invalid_action" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Unknown action"));
        }

        [Test]
        public async Task SetSelection_MissingSelector_ReturnsError()
        {
            var args = new JObject { ["action"] = "set_selection" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("selector"));
        }

        [Test]
        public async Task SetSelection_EmptySelector_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "set_selection",
                ["selector"] = ""
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("selector"));
        }

        #endregion

        #region GetSelection Action

        [Test]
        public async Task GetSelection_ReturnsWithoutError()
        {
            // GetGameObjectPath is internal; test via get_selection action
            var tool = new EditorOperationsTool();
            var args = JObject.Parse("{\"action\": \"get_selection\"}");
            var result = await tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError, "get_selection should succeed");
        }

        #endregion
    }
}
