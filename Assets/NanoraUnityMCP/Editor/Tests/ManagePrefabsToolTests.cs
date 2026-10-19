using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for ManagePrefabsTool.
    /// Validates tool properties, input schema, and argument handling.
    /// </summary>
    [TestFixture]
    public class ManagePrefabsToolTests
    {
        private ManagePrefabsTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new ManagePrefabsTool();
        }

        #region Tool Properties

        [Test]
        public void Name_IsCorrect()
        {
            Assert.AreEqual("manage_prefabs", m_tool.Name);
        }

        [Test]
        public void Description_IsNotEmpty()
        {
            Assert.IsNotNull(m_tool.Description);
            Assert.IsTrue(m_tool.Description.Length > 0);
        }

        #endregion

        #region Input Schema

        [Test]
        public void InputSchema_IsValid()
        {
            var schema = m_tool.InputSchema;
            Assert.IsNotNull(schema);
            Assert.AreEqual("object", schema["type"]?.Value<string>());

            var required = schema["required"] as JArray;
            Assert.IsNotNull(required);
            Assert.IsTrue(required.Count >= 2);
        }

        [Test]
        public void InputSchema_HasActionProperty()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props);
            Assert.IsNotNull(props["action"]);
            Assert.AreEqual("string", props["action"]["type"]?.Value<string>());

            var enumValues = props["action"]["enum"] as JArray;
            Assert.IsNotNull(enumValues);
            Assert.AreEqual(7, enumValues.Count);
        }

        [Test]
        public void InputSchema_HasSelectorProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["selector"]);
            Assert.AreEqual("string", props["selector"]["type"]?.Value<string>());
        }

        [Test]
        public void InputSchema_HasPathProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["path"]);
            Assert.AreEqual("string", props["path"]["type"]?.Value<string>());
        }

        [Test]
        public void InputSchema_HasUnpackModeProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["unpack_mode"]);

            var enumValues = props["unpack_mode"]["enum"] as JArray;
            Assert.IsNotNull(enumValues);
            Assert.AreEqual(2, enumValues.Count);
        }

        [Test]
        public void InputSchema_ActionEnum_ContainsAllActions()
        {
            var enumValues = m_tool.InputSchema["properties"]["action"]["enum"] as JArray;
            var actions = new[] { "create", "apply", "unpack", "save", "create_variant", "revert", "get_info" };

            foreach (var action in actions)
            {
                bool found = false;
                foreach (var val in enumValues)
                {
                    if (val.Value<string>() == action)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, $"Action '{action}' not found in schema enum");
            }
        }

        #endregion

        #region ExecuteAsync Validation

        [Test]
        public async Task ExecuteAsync_MissingAction_ReturnsError()
        {
            var args = new JObject { ["selector"] = "//MyObject" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_MissingSelector_ReturnsError()
        {
            var args = new JObject { ["action"] = "get_info" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_InvalidAction_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "invalid_action",
                ["selector"] = "//MyObject"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_NonExistentSelector_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "get_info",
                ["selector"] = "//NonExistentObject_12345"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("NonExistentObject_12345", result.Content[0].Text);
        }

        [Test]
        public async Task ExecuteAsync_CreateWithoutPath_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "create",
                ["selector"] = "//NonExistentObject_12345"
            };
            // Will fail on selector resolution first, which is fine for validation
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        #endregion
    }
}
