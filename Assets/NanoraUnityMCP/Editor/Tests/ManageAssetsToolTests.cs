using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for ManageAssetsTool.
    /// Validates tool properties, input schema, and argument handling.
    /// </summary>
    [TestFixture]
    public class ManageAssetsToolTests
    {
        private ManageAssetsTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new ManageAssetsTool();
        }

        #region Tool Properties

        [Test]
        public void Name_IsCorrect()
        {
            Assert.AreEqual("manage_assets", m_tool.Name);
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
            Assert.AreEqual(1, required.Count);
            Assert.AreEqual("action", required[0].Value<string>());
        }

        [Test]
        public void InputSchema_HasActionProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["action"]);
            Assert.AreEqual("string", props["action"]["type"]?.Value<string>());

            var enumValues = props["action"]["enum"] as JArray;
            Assert.IsNotNull(enumValues);
            Assert.AreEqual(7, enumValues.Count);
        }

        [Test]
        public void InputSchema_HasPathProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["path"]);
            Assert.AreEqual("string", props["path"]["type"]?.Value<string>());
        }

        [Test]
        public void InputSchema_HasNewPathProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["new_path"]);
            Assert.AreEqual("string", props["new_path"]["type"]?.Value<string>());
        }

        [Test]
        public void InputSchema_HasParentFolderProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["parent_folder"]);
        }

        [Test]
        public void InputSchema_HasFolderNameProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["folder_name"]);
        }

        [Test]
        public void InputSchema_HasSourcePathProperty()
        {
            var props = m_tool.InputSchema["properties"] as JObject;
            Assert.IsNotNull(props["source_path"]);
        }

        [Test]
        public void InputSchema_ActionEnum_ContainsAllActions()
        {
            var enumValues = m_tool.InputSchema["properties"]["action"]["enum"] as JArray;
            var actions = new[] { "get_info", "create_folder", "delete", "move", "rename", "duplicate", "import" };

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
            var args = new JObject { ["path"] = "Assets/SomeAsset.mat" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_InvalidAction_ReturnsError()
        {
            var args = new JObject { ["action"] = "invalid_action" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_GetInfoWithoutPath_ReturnsError()
        {
            var args = new JObject { ["action"] = "get_info" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_GetInfoNonExistentAsset_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "get_info",
                ["path"] = "Assets/NonExistent_12345.mat"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_CreateFolderWithoutParent_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "create_folder",
                ["folder_name"] = "TestFolder"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_CreateFolderWithoutName_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "create_folder",
                ["parent_folder"] = "Assets"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_DeleteWithoutPath_ReturnsError()
        {
            var args = new JObject { ["action"] = "delete" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_MoveWithoutNewPath_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "move",
                ["path"] = "Assets/SomeAsset.mat"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_RenameWithoutNewName_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "rename",
                ["path"] = "Assets/SomeAsset.mat"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_ImportWithoutSourcePath_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "import",
                ["path"] = "Assets/SomeAsset.mat"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteAsync_DeleteRootFolder_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "delete",
                ["path"] = "Assets"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
        }

        #endregion
    }
}
