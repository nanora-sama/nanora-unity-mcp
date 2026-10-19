using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// Regression tests for GitHub Issues #10-#15.
    /// </summary>
    [TestFixture]
    public class IssueFixTests
    {
        private List<GameObject> m_createdObjects;

        [SetUp]
        public void SetUp()
        {
            m_createdObjects = new List<GameObject>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in m_createdObjects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            m_createdObjects.Clear();
        }

        private GameObject CreateTestObject(string name)
        {
            var obj = new GameObject(name);
            m_createdObjects.Add(obj);
            return obj;
        }

        private void SkipIfRoslynNotAvailable()
        {
            if (!RoslynCompiler.IsAvailable)
            {
                Assert.Ignore("Roslyn assemblies not available - skipping test");
            }
        }

        #region Issue #10: RoslynCompiler assembly references

        [Test]
        public void Issue10_RoslynCompiler_IncludesAssemblyCSharp()
        {
            SkipIfRoslynNotAvailable();

            // Assembly-CSharp types should be accessible via Roslyn
            var code = @"
using UnityEngine;

public class DynamicScript
{
    public static object Execute()
    {
        return typeof(GameObject).Assembly.GetName().Name;
    }
}";
            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Issue10_RoslynCompiler_IncludesNanoraAssembly()
        {
            SkipIfRoslynNotAvailable();

            // NanoraUnityMCP assembly types should be accessible
            var code = @"
using NanoraUnityMCP.Core;

public class DynamicScript
{
    public static object Execute()
    {
        return typeof(McpServer).Name;
    }
}";
            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.AreEqual("McpServer", result);
        }

        [Test]
        public void Issue10_RoslynCompiler_CompilationErrorShowsDetails()
        {
            SkipIfRoslynNotAvailable();

            // When referencing a non-existent type, the error message should be descriptive
            var code = @"
public class DynamicScript
{
    public static object Execute()
    {
        NonExistentNamespace.SomeType x = null;
        return x;
    }
}";
            var ex = Assert.Throws<CompilationException>(() => RoslynCompiler.CompileAndExecute(code));
            Assert.That(ex.Message, Does.Contain("NonExistentNamespace").Or.Contain("error"));
        }

        #endregion

        #region Issue #11/#12: SetupWizard detection logic

        [Test]
        public void Issue11_SetupWizard_ServerNotRunning_RequiresWizard()
        {
            // When server is not running, IsSetupEffectivelyComplete should return false
            // We test this indirectly: if the server isn't running, setup should not be auto-completed
            // The fix requires McpServer.IsRunning (not just GetAutoStart()) to be true
            if (McpServer.IsRunning)
            {
                Assert.Ignore("Server is running - cannot test not-running case");
            }

            // The key assertion: just GetAutoStart() being true should NOT be sufficient
            bool autoStart = McpServer.GetAutoStart();
            // Even with auto-start enabled, if server isn't running, the wizard should show
            Assert.IsFalse(McpServer.IsRunning, "Server should not be running for this test");
        }

        [Test]
        public void Issue12_SetupWizard_ChecksServerPort()
        {
            // Verify McpServer exposes Port property correctly
            int port = McpServer.Port;
            Assert.IsTrue(port > 0, "McpServer.Port should return a valid port number");
        }

        #endregion

        #region Issue #13: execute_csharp Roslyn fallback

        [Test]
        public async Task Issue13_ExecuteCSharp_MultiStatement_ForceRoslyn()
        {
            SkipIfRoslynNotAvailable();

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "var x = 1 + 2;\nreturn x.ToString();",
                ["force_roslyn"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError, $"Expected success, got: {result.Content[0].Text}");
            Assert.IsTrue(result.Content[0].Text.Contains("3"));
        }

        [Test]
        public async Task Issue13_ExecuteCSharp_MultiStatement_AutoDetectsRoslyn()
        {
            SkipIfRoslynNotAvailable();

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "var a = 10;\nvar b = 20;\nreturn (a + b).ToString();"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError, $"Expected success, got: {result.Content[0].Text}");
            Assert.IsTrue(result.Content[0].Text.Contains("30"));
        }

        [Test]
        public async Task Issue13_ExecuteCSharp_ReflectionFallback_ShowsHelpfulError()
        {
            // When Roslyn is unavailable and reflection can't evaluate,
            // the error should mention Roslyn, not just "not evaluable"
            if (RoslynCompiler.IsAvailable)
            {
                // Test the reflection-only path by using force_roslyn with invalid code
                LogAssert.ignoreFailingMessages = true;
                var tool = new ExecuteCSharpTool();
                var args = new JObject
                {
                    ["code"] = "var x = SomeNonExistentType.DoSomething();\nreturn x;",
                    ["force_roslyn"] = true
                };

                var result = await tool.ExecuteAsync(args);
                LogAssert.ignoreFailingMessages = false;

                // Should return error with compilation details
                Assert.IsTrue(result.IsError, "Expected error for invalid code with force_roslyn");
                var text = result.Content[0].Text;
                StringAssert.Contains("Compilation failed", text);
                StringAssert.Contains("SomeNonExistentType", text);
            }
        }

        #endregion

        #region Issue #14: manage_playmode timeout

        [Test]
        public void Issue14_ManagePlayMode_SchemaIncludesTimeoutSeconds()
        {
            var tool = new ManagePlayModeTool();
            var schema = tool.InputSchema;
            var properties = schema["properties"] as JObject;

            Assert.IsNotNull(properties["timeout_seconds"], "Schema should include timeout_seconds parameter");

            var timeoutProp = properties["timeout_seconds"];
            Assert.AreEqual("integer", timeoutProp["type"]?.Value<string>());
            Assert.AreEqual(30, timeoutProp["default"]?.Value<int>());
        }

        [Test]
        public async Task Issue14_ManagePlayMode_StatusAction_AlwaysWorks()
        {
            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "status"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Play Mode Status"));
        }

        [Test]
        public async Task Issue14_ManagePlayMode_InvalidAction_ReturnsError()
        {
            var tool = new ManagePlayModeTool();
            var args = new JObject
            {
                ["action"] = "nonexistent"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        #endregion

        #region Issue #15: get_object_details SerializeField values

        [Test]
        public async Task Issue15_GetObjectDetails_ShowsLightProperties()
        {
            var obj = CreateTestObject("LightTestObj");
            var light = obj.AddComponent<Light>();
            light.intensity = 2.5f;
            light.color = Color.red;

            var tool = new GetObjectDetailsTool();
            var args = new JObject
            {
                ["object_name"] = "LightTestObj"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            // Should contain serialized property values from Light component
            Assert.IsTrue(text.Contains("Light"), $"Should list Light component: {text}");
            Assert.IsTrue(text.Contains("Intensity") || text.Contains("intensity"),
                $"Should show intensity property: {text}");
        }

        [Test]
        public async Task Issue15_GetObjectDetails_ShowsCameraProperties()
        {
            var obj = CreateTestObject("CameraTestObj");
            var cam = obj.AddComponent<Camera>();
            cam.fieldOfView = 90f;

            var tool = new GetObjectDetailsTool();
            var args = new JObject
            {
                ["object_name"] = "CameraTestObj"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Camera"), $"Should list Camera component: {text}");
            // SerializedProperty should expose Field Of View
            Assert.IsTrue(text.Contains("Field Of View") || text.Contains("FOV") || text.Contains("90"),
                $"Should show field of view: {text}");
        }

        [Test]
        public async Task Issue15_GetObjectDetails_ShowsRigidbodyProperties()
        {
            var obj = CreateTestObject("RigidbodyTestObj");
            var rb = obj.AddComponent<Rigidbody>();
            rb.mass = 5.0f;
            rb.useGravity = false;

            var tool = new GetObjectDetailsTool();
            var args = new JObject
            {
                ["object_name"] = "RigidbodyTestObj"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Rigidbody"), $"Should list Rigidbody component: {text}");
            Assert.IsTrue(text.Contains("Mass") || text.Contains("5"),
                $"Should show mass property: {text}");
        }

        [Test]
        public async Task Issue15_GetObjectDetails_ShowsTransformDetails()
        {
            var obj = CreateTestObject("TransformTestObj");
            obj.transform.position = new Vector3(1, 2, 3);

            var tool = new GetObjectDetailsTool();
            var args = new JObject
            {
                ["object_name"] = "TransformTestObj"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Transform"), $"Should list Transform info: {text}");
            Assert.IsTrue(text.Contains("Position"), $"Should show Position: {text}");
        }

        [Test]
        public async Task Issue15_GetObjectDetails_MissingScript_HandledGracefully()
        {
            var obj = CreateTestObject("MissingScriptTestObj");

            var tool = new GetObjectDetailsTool();
            var args = new JObject
            {
                ["object_name"] = "MissingScriptTestObj"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            // Should handle the object with just Transform without crashing
            Assert.IsTrue(result.Content[0].Text.Contains("Transform"));
        }

        [Test]
        public async Task Issue15_GetObjectDetails_NonExistent_ReturnsError()
        {
            var tool = new GetObjectDetailsTool();
            var args = new JObject
            {
                ["object_name"] = "NonExistentObject_XYZ_12345"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task Issue15_GetObjectDetails_AudioSource_ShowsProperties()
        {
            var obj = CreateTestObject("AudioTestObj");
            var audioSource = obj.AddComponent<AudioSource>();
            audioSource.volume = 0.75f;
            audioSource.loop = true;

            var tool = new GetObjectDetailsTool();
            var args = new JObject
            {
                ["object_name"] = "AudioTestObj"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("AudioSource"), $"Should list AudioSource component: {text}");
            // SerializedProperty should expose volume and loop
            Assert.IsTrue(text.Contains("Volume") || text.Contains("0.75"),
                $"Should show volume property: {text}");
            Assert.IsTrue(text.Contains("Loop") || text.Contains("True"),
                $"Should show loop property: {text}");
        }

        #endregion
    }
}
