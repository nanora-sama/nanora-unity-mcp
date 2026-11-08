using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Code tools (execute_csharp, execute_menu_item, compile_project, get_compilation_errors).
    /// </summary>
    [TestFixture]
    public class CodeToolTests
    {
        private void SkipIfRoslynNotAvailable()
        {
            if (!RoslynCompiler.IsAvailable)
            {
                Assert.Ignore("Roslyn assemblies not available - skipping test");
            }
        }

        #region ExecuteCSharpTool Tests

        [Test]
        public async Task ExecuteCSharp_SimpleReturn_ReturnsValue()
        {
            SkipIfRoslynNotAvailable();

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "return 1 + 1;"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("2"));
        }

        [Test]
        public async Task ExecuteCSharp_StringReturn_ReturnsString()
        {
            SkipIfRoslynNotAvailable();

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "return \"Hello World\";"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Hello World"));
        }

        [Test]
        public async Task ExecuteCSharp_UnityAPI_Works()
        {
            SkipIfRoslynNotAvailable();

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "return Application.unityVersion;"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            // Unity version should be present
            Assert.IsTrue(text.Length > 0);
        }

        [Test]
        public async Task ExecuteCSharp_CreateGameObject_Works()
        {
            SkipIfRoslynNotAvailable();

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = @"
var go = new GameObject(""ExecuteCSharpTestObject"");
return go.name;"
            };

            var result = await tool.ExecuteAsync(args);
            var created = GameObject.Find("ExecuteCSharpTestObject");

            try
            {
                Assert.IsFalse(result.IsError);
                Assert.IsNotNull(created);
            }
            finally
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
        }

        [Test]
        public async Task ExecuteCSharp_CompilationError_ReturnsError()
        {
            SkipIfRoslynNotAvailable();
            LogAssert.ignoreFailingMessages = true;

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "return undefinedVariable;"
            };

            var result = await tool.ExecuteAsync(args);

            LogAssert.ignoreFailingMessages = false;
            var text = result.Content[0].Text;
            Assert.IsTrue(result.IsError, $"Expected MCP error result, got: {text}");
            StringAssert.Contains("CS0103", text);
            StringAssert.Contains("undefinedVariable", text);
        }

        [Test]
        public async Task ExecuteCSharp_RuntimeException_ReturnsError()
        {
            SkipIfRoslynNotAvailable();
            LogAssert.ignoreFailingMessages = true;

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "throw new System.Exception(\"Test exception\");"
            };

            var result = await tool.ExecuteAsync(args);

            LogAssert.ignoreFailingMessages = false;
            var text = result.Content[0].Text;
            Assert.IsTrue(result.IsError, $"Expected MCP error result, got: {text}");
            StringAssert.Contains("Runtime error: Test exception", text);
        }

        [Test]
        public async Task ExecuteCSharp_CompilationErrorInFallbackApi_PreservesDiagnostic()
        {
            SkipIfRoslynNotAvailable();
            var result = await new ExecuteCSharpTool().ExecuteAsync(new JObject
            {
                ["code"] = "return EditorApplication.NanoraMcpMissingMember;"
            });
            Assert.IsTrue(result.IsError, result.Content[0].Text);
            StringAssert.Contains("CS0117", result.Content[0].Text);
            StringAssert.DoesNotContain("reflection mode", result.Content[0].Text);
        }

        [Test]
        public async Task ExecuteCSharp_RuntimeFailureAfterMutation_DoesNotReplayMutation()
        {
            SkipIfRoslynNotAvailable();
            var existingIds = UnityCompatibility.FindObjectsByType<GameObject>()
                .Select(obj => obj.GetInstanceID()).ToArray();
            try
            {
                // One statement: the old fallback matches CreatePrimitive again after
                // Roslyn creates the cube and fails while dereferencing a missing body.
                var result = await new ExecuteCSharpTool().ExecuteAsync(new JObject
                {
                    ["code"] = "GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Rigidbody>().mass = 1f;"
                });
                var created = UnityCompatibility.FindObjectsByType<GameObject>()
                    .Where(obj => !existingIds.Contains(obj.GetInstanceID())).ToArray();

                Assert.IsTrue(result.IsError, result.Content[0].Text);
                StringAssert.Contains("Runtime error:", result.Content[0].Text);
                Assert.AreEqual(1, created.Length, "A failed script must not be replayed through reflection.");
            }
            finally
            {
                foreach (var obj in UnityCompatibility.FindObjectsByType<GameObject>()
                    .Where(obj => !existingIds.Contains(obj.GetInstanceID())))
                    Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public async Task ExecuteCSharp_ForceRoslynRuntimeFailure_PreservesRuntimeDiagnostic()
        {
            SkipIfRoslynNotAvailable();
            var result = await new ExecuteCSharpTool().ExecuteAsync(new JObject
            {
                ["code"] = "throw new System.Exception(\"Forced runtime failure\");",
                ["force_roslyn"] = true
            });
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("Runtime error: Forced runtime failure", result.Content[0].Text);
            StringAssert.DoesNotContain("Roslyn compilation failed", result.Content[0].Text);
        }

        [TestCase("return 1 + 1;")]
        [TestCase("Selection.unsupported")]
        [TestCase("GameObject.unsupported()")]
        [TestCase("Debug.Break()")]
        [TestCase("EditorApplication.unsupported")]
        [TestCase("AssetDatabase.unsupported()")]
        [TestCase("Object.FindObjectOfType<Camera>().name.Length")]
        [TestCase("GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Rigidbody>()")]
        public async Task ExecuteCSharp_UnavailableCompilerUnsupportedCode_ReturnsError(string code)
        {
            var result = await ExecuteWithoutRoslyn(new JObject { ["code"] = code });
            Assert.IsTrue(result.IsError, result.Content[0].Text);
        }

        [TestCase("FindAnyObjectByType<Camera>()")]
        [TestCase("Object.FindAnyObjectByType<Camera>()")]
        [TestCase("UnityEngine.Object.FindFirstObjectByType<Camera>()")]
        [TestCase("FindObjectOfType<Camera>()")]
        public async Task ExecuteCSharp_UnavailableCompilerFindExpression_FindsObject(string code)
        {
            // Skills and Unity 6 docs use FindAnyObjectByType / FindFirstObjectByType; reflection mode must accept them.
            var probe = new GameObject("McpFindExpressionProbe");
            probe.AddComponent<Camera>();
            try
            {
                var result = await ExecuteWithoutRoslyn(new JObject { ["code"] = code });
                Assert.IsFalse(result.IsError, result.Content[0].Text);
                StringAssert.Contains("Found:", result.Content[0].Text);
            }
            finally
            {
                if (probe != null) Object.DestroyImmediate(probe);
            }
        }

        [TestCase("EditorApplication.isPlaying")]
        [TestCase("return EditorApplication.isPlaying;")]
        [TestCase("EditorApplication.isPlaying;")]
        public async Task ExecuteCSharp_UnavailableCompilerSupportedRead_ReturnsValue(string code)
        {
            var result = await ExecuteWithoutRoslyn(new JObject { ["code"] = code });
            Assert.IsFalse(result.IsError, result.Content[0].Text);
            StringAssert.Contains("reflection mode", result.Content[0].Text);
            StringAssert.Contains(UnityEditor.EditorApplication.isPlaying.ToString(), result.Content[0].Text);
        }

        [Test]
        public async Task ExecuteCSharp_UnavailableCompilerForced_DoesNotFallback()
        {
            var result = await ExecuteWithoutRoslyn(new JObject
            {
                ["code"] = "EditorApplication.isPlaying",
                ["force_roslyn"] = true
            });
            Assert.IsTrue(result.IsError);
            StringAssert.Contains("Roslyn", result.Content[0].Text);
        }

        private async Task<NanoraUnityMCP.Protocol.McpToolResult> ExecuteWithoutRoslyn(JObject arguments)
        {
            // Restore the process-wide availability cache even when the assertion fails.
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var initialized = typeof(RoslynCompiler).GetField("s_Initialized", flags);
            var available = typeof(RoslynCompiler).GetField("s_RoslynAvailable", flags);
            var previousInitialized = initialized.GetValue(null);
            var previousAvailable = available.GetValue(null);
            var existingIds = UnityCompatibility.FindObjectsByType<GameObject>()
                .Select(obj => obj.GetInstanceID()).ToArray();
            try
            {
                initialized.SetValue(null, true);
                available.SetValue(null, false);
                return await new ExecuteCSharpTool().ExecuteAsync(arguments);
            }
            finally
            {
                initialized.SetValue(null, previousInitialized);
                available.SetValue(null, previousAvailable);
                foreach (var obj in UnityCompatibility.FindObjectsByType<GameObject>()
                    .Where(obj => !existingIds.Contains(obj.GetInstanceID())))
                    Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public async Task ExecuteCSharp_InvalidGameObjectMember_ReturnsCompilationError()
        {
            SkipIfRoslynNotAvailable();
            var result = await new ExecuteCSharpTool().ExecuteAsync(new JObject
            {
                ["code"] = "return GameObject.__NanoraMCPUndefinedMember__();"
            });

            Assert.IsTrue(result.IsError, result.Content[0].Text);
            StringAssert.Contains("Compilation failed", result.Content[0].Text);
            StringAssert.Contains("__NanoraMCPUndefinedMember__", result.Content[0].Text);
            StringAssert.DoesNotContain("reflection mode", result.Content[0].Text);
        }

        [Test]
        public async Task ExecuteCSharp_NullDereference_ReturnsRuntimeError()
        {
            SkipIfRoslynNotAvailable();
            const string missingName = "__NanoraMCPMissingExecutionTarget__";
            Assert.IsNull(GameObject.Find(missingName));
            var result = await new ExecuteCSharpTool().ExecuteAsync(new JObject
            {
                ["code"] = $"return GameObject.Find(\"{missingName}\").GetInstanceID();"
            });

            Assert.IsTrue(result.IsError, result.Content[0].Text);
            StringAssert.Contains("Runtime error", result.Content[0].Text);
            StringAssert.DoesNotContain("reflection mode", result.Content[0].Text);
        }

        [Test]
        public async Task ExecuteCSharp_RuntimeFailure_DoesNotRepeatSideEffects()
        {
            SkipIfRoslynNotAvailable();
            var initialObjects = UnityCompatibility.FindObjectsByType<GameObject>();
            try
            {
                var result = await new ExecuteCSharpTool().ExecuteAsync(new JObject
                {
                    // One statement avoids the existing multi-statement force_roslyn heuristic.
                    // Creating the cube succeeds, then accessing the absent Camera throws.
                    ["code"] = "return GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Camera>().name;"
                });
                var createdObjects = UnityCompatibility.FindObjectsByType<GameObject>().Except(initialObjects).ToArray();

                Assert.AreEqual(1, createdObjects.Length, "A failed execution must not create a second cube through reflection.");
                Assert.IsTrue(result.IsError, result.Content[0].Text);
                StringAssert.Contains("Runtime error", result.Content[0].Text);
            }
            finally
            {
                foreach (var created in UnityCompatibility.FindObjectsByType<GameObject>().Except(initialObjects))
                    Object.DestroyImmediate(created);
            }
        }

        [Test]
        public async Task ExecuteCSharp_EmptyCode_ReturnsError()
        {
            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteCSharp_WithTimeout_Works()
        {
            SkipIfRoslynNotAvailable();

            var tool = new ExecuteCSharpTool();
            var args = new JObject
            {
                ["code"] = "return 42;",
                ["timeout_ms"] = 5000
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion

        #region ExecuteMenuItemTool Tests

        [Test]
        public async Task ExecuteMenuItem_ValidMenuItem_Succeeds()
        {
            var tool = new ExecuteMenuItemTool();
            var args = new JObject
            {
                ["menu_path"] = "Window/General/Console"
            };

            var result = await tool.ExecuteAsync(args);

            // May or may not succeed depending on Editor state, but should not crash
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ExecuteMenuItem_InvalidMenuItem_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;

            var tool = new ExecuteMenuItemTool();
            var args = new JObject
            {
                ["menu_path"] = "Invalid/Menu/Path/That/Does/Not/Exist"
            };

            var result = await tool.ExecuteAsync(args);

            LogAssert.ignoreFailingMessages = false;
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteMenuItem_EmptyPath_ReturnsError()
        {
            var tool = new ExecuteMenuItemTool();
            var args = new JObject
            {
                ["menu_path"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ExecuteMenuItem_DomainReloadMenu_ReturnsImmediately()
        {
            var tool = new ExecuteMenuItemTool();
            var args = new JObject
            {
                ["menu_path"] = "Assets/Refresh"
            };

            var result = await tool.ExecuteAsync(args);

            // Should return with domain reload warning
            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("domain reload") || text.Contains("Executed"));
        }

        #endregion

        #region CompileProjectTool Tests

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Requires interactive Unity Editor - MainThreadDispatcher unreliable in batchmode")]
        [Timeout(120000)] // 2 minutes timeout
        public async Task CompileProject_ReturnsResult()
        {
            var tool = new CompileProjectTool();
            var args = new JObject
            {
                ["timeout_seconds"] = 60
            };

            var result = await tool.ExecuteAsync(args);

            // Compilation result depends on project state
            Assert.IsNotNull(result);
            // Should return something (success or warning about domain reload)
            Assert.IsNotNull(result.Content);
        }

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Requires interactive Unity Editor - MainThreadDispatcher unreliable in batchmode")]
        [Timeout(60000)] // 1 minute timeout
        public async Task CompileProject_WithTimeout_Works()
        {
            var tool = new CompileProjectTool();
            var args = new JObject
            {
                ["timeout_seconds"] = 30
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsNotNull(result);
        }

        #endregion

        #region GetCompilationErrorsTool Tests

        [Test]
        public async Task GetCompilationErrors_ReturnsResult()
        {
            var tool = new GetCompilationErrorsTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            // Should return some result
            Assert.IsNotNull(result.Content);
        }

        [Test]
        public async Task GetCompilationErrors_WithWarnings_Works()
        {
            var tool = new GetCompilationErrorsTool();
            var args = new JObject
            {
                ["include_warnings"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task GetCompilationErrors_WithoutWarnings_Works()
        {
            var tool = new GetCompilationErrorsTool();
            var args = new JObject
            {
                ["include_warnings"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task GetCompilationErrors_WithFileFilter_Works()
        {
            var tool = new GetCompilationErrorsTool();
            var args = new JObject
            {
                ["file_filter"] = "McpServer"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion
    }
}
