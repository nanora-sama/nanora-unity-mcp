using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for UnityReflectTool.
    /// Tests input validation, type resolution, search, and member inspection.
    /// </summary>
    [TestFixture]
    public class UnityReflectToolTests
    {
        private UnityReflectTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new UnityReflectTool();
        }

        #region Tool Properties

        [Test]
        public void Name_IsCorrect()
        {
            Assert.AreEqual("unity_reflect", m_tool.Name);
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
            var args = new JObject { ["action"] = "unknown_action" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Unknown action"));
        }

        [Test]
        public async Task GetType_MissingTypeName_ReturnsError()
        {
            var args = new JObject { ["action"] = "get_type" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("type_name"));
        }

        [Test]
        public async Task GetType_NonexistentType_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "get_type",
                ["type_name"] = "NonExistent.FakeType.DoesNotExist"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("not found"));
        }

        [Test]
        public async Task SearchType_MissingQuery_ReturnsError()
        {
            var args = new JObject { ["action"] = "search_type" };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("search_query"));
        }

        [Test]
        public async Task GetMember_MissingTypeName_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "get_member",
                ["member_name"] = "Length"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("type_name"));
        }

        [Test]
        public async Task GetMember_MissingMemberName_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "get_member",
                ["type_name"] = "System.String"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("member_name"));
        }

        [Test]
        public async Task InvokeMethod_MissingTypeName_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "invoke_method",
                ["member_name"] = "IsNullOrEmpty"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("type_name"));
        }

        [Test]
        public async Task InvokeMethod_MissingMemberName_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "invoke_method",
                ["type_name"] = "System.String"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("member_name"));
        }

        #endregion

        #region GetType

        [Test]
        public async Task GetType_SystemString_ReturnsInfo()
        {
            var args = new JObject
            {
                ["action"] = "get_type",
                ["type_name"] = "System.String"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("System.String"));
            Assert.IsTrue(text.Contains("IsClass: True"));
        }

        [Test]
        public async Task GetType_Enum_ShowsValues()
        {
            var args = new JObject
            {
                ["action"] = "get_type",
                ["type_name"] = "System.DayOfWeek"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("IsEnum: True"));
            Assert.IsTrue(text.Contains("Monday"));
        }

        #endregion

        #region SearchType

        [Test]
        public async Task SearchType_FindsSystemString()
        {
            var args = new JObject
            {
                ["action"] = "search_type",
                ["search_query"] = "System.String"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("System.String"));
        }

        [Test]
        public async Task SearchType_NoResults_ReturnsMessage()
        {
            var args = new JObject
            {
                ["action"] = "search_type",
                ["search_query"] = "XyzAbcNonExistentTypeName12345"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("No types found"));
        }

        #endregion

        #region GetMember

        [Test]
        public async Task GetMember_StringLength_ReturnsPropertyInfo()
        {
            var args = new JObject
            {
                ["action"] = "get_member",
                ["type_name"] = "System.String",
                ["member_name"] = "Length"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Property"));
            Assert.IsTrue(text.Contains("CanRead: True"));
        }

        [Test]
        public async Task GetMember_NonexistentMember_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "get_member",
                ["type_name"] = "System.String",
                ["member_name"] = "NonExistentMember12345"
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("not found"));
        }

        #endregion

        #region InvokeMethod

        [Test]
        public async Task InvokeMethod_StringIsNullOrEmpty_Works()
        {
            var args = new JObject
            {
                ["action"] = "invoke_method",
                ["type_name"] = "System.String",
                ["member_name"] = "IsNullOrEmpty",
                ["method_args"] = new JArray { "" }
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("True"));
        }

        [Test]
        public async Task InvokeMethod_NonStaticMethod_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "invoke_method",
                ["type_name"] = "System.String",
                ["member_name"] = "Contains",
                ["method_args"] = new JArray { "test" }
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("No public static method"));
        }

        [Test]
        public async Task InvokeMethod_WrongArgCount_ReturnsError()
        {
            var args = new JObject
            {
                ["action"] = "invoke_method",
                ["type_name"] = "System.String",
                ["member_name"] = "IsNullOrEmpty",
                ["method_args"] = new JArray { "a", "b", "c" }
            };
            var result = await m_tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("No overload"));
        }

        #endregion

        #region ResolveType Helper

        [Test]
        public async Task ResolveType_ViaGetType_StandardType()
        {
            // ResolveType is internal; test via get_type action instead
            var tool = new UnityReflectTool();
            var args = JObject.Parse("{\"action\": \"get_type\", \"type_name\": \"System.String\"}");
            var result = await tool.ExecuteAsync(args);
            Assert.IsFalse(result.IsError, "get_type for System.String should succeed");
        }

        [Test]
        public async Task ResolveType_ViaGetType_InvalidType()
        {
            var tool = new UnityReflectTool();
            var args = JObject.Parse("{\"action\": \"get_type\", \"type_name\": \"NonExistent.Type.That.Does.Not.Exist\"}");
            var result = await tool.ExecuteAsync(args);
            Assert.IsTrue(result.IsError, "get_type for invalid type should fail");
        }

        #endregion
    }
}
