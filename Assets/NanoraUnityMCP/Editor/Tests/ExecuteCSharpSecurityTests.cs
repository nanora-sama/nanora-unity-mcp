using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// Security tests for ExecuteCSharpTool.
    /// Verifies that forbidden patterns are blocked and valid code passes.
    /// </summary>
    [TestFixture]
    public class ExecuteCSharpSecurityTests
    {
        private ExecuteCSharpTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new ExecuteCSharpTool();
        }

        #region Forbidden Pattern Tests

        [Test]
        public void CheckSecurity_FileDelete_Blocked()
        {
            var result = m_tool.CheckSecurity("System.IO.File.Delete(\"test.txt\");");
            Assert.IsFalse(result.isAllowed);
            Assert.IsTrue(result.reason.Contains("Forbidden API call"));
        }

        [Test]
        public void CheckSecurity_DirectoryDelete_Blocked()
        {
            var result = m_tool.CheckSecurity("System.IO.Directory.Delete(\"/tmp/test\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_FileWriteAllText_Blocked()
        {
            var result = m_tool.CheckSecurity("File.WriteAllText(\"test.txt\", \"data\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_FileWriteAllBytes_Blocked()
        {
            var result = m_tool.CheckSecurity("File.WriteAllBytes(\"test.bin\", bytes);");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_FileAppendAllText_Blocked()
        {
            var result = m_tool.CheckSecurity("File.AppendAllText(\"log.txt\", \"line\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_StreamWriter_Blocked()
        {
            var result = m_tool.CheckSecurity("var sw = new StreamWriter(\"out.txt\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_ProcessStart_Blocked()
        {
            var result = m_tool.CheckSecurity("Process.Start(\"cmd\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_ProcessStartInfo_Blocked()
        {
            var result = m_tool.CheckSecurity("var psi = new ProcessStartInfo(\"bash\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_HttpClient_Blocked()
        {
            var result = m_tool.CheckSecurity("var client = new HttpClient();");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_WebRequest_Blocked()
        {
            var result = m_tool.CheckSecurity("WebRequest.Create(\"http://evil.com\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_TcpClient_Blocked()
        {
            var result = m_tool.CheckSecurity("var tcp = new TcpClient(\"host\", 80);");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_Socket_Blocked()
        {
            var result = m_tool.CheckSecurity("var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_AssemblyLoad_Blocked()
        {
            var result = m_tool.CheckSecurity("Assembly.Load(bytes);");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_AssemblyLoadFrom_Blocked()
        {
            var result = m_tool.CheckSecurity("Assembly.LoadFrom(\"/path/to/evil.dll\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_ActivatorCreateInstance_Blocked()
        {
            var result = m_tool.CheckSecurity("Activator.CreateInstance(typeof(Dangerous));");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_EnvironmentSetVariable_Blocked()
        {
            var result = m_tool.CheckSecurity("Environment.SetEnvironmentVariable(\"PATH\", \"/evil\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_ApplicationQuit_Blocked()
        {
            var result = m_tool.CheckSecurity("Application.Quit();");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_EditorApplicationExit_Blocked()
        {
            var result = m_tool.CheckSecurity("EditorApplication.Exit(0);");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_AssetDatabaseDeleteAsset_Blocked()
        {
            var result = m_tool.CheckSecurity("AssetDatabase.DeleteAsset(\"Assets/Important.cs\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_AssetDatabaseMoveAsset_Blocked()
        {
            var result = m_tool.CheckSecurity("AssetDatabase.MoveAsset(\"Assets/A.cs\", \"Assets/B.cs\");");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_FileUtilDelete_Blocked()
        {
            var result = m_tool.CheckSecurity("FileUtil.DeleteFileOrDirectory(\"Assets/Scripts\");");
            Assert.IsFalse(result.isAllowed);
        }

        #endregion

        #region Infinite Loop Detection Tests

        [Test]
        public void CheckSecurity_WhileTrue_Blocked()
        {
            var result = m_tool.CheckSecurity("while(true) { }");
            Assert.IsFalse(result.isAllowed);
            Assert.IsTrue(result.reason.Contains("Infinite loops"));
        }

        [Test]
        public void CheckSecurity_WhileTrueWithSpaces_Blocked()
        {
            var result = m_tool.CheckSecurity("while ( true ) { DoWork(); }");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_ForSemicolons_Blocked()
        {
            var result = m_tool.CheckSecurity("for(;;) { count++; }");
            Assert.IsFalse(result.isAllowed);
            Assert.IsTrue(result.reason.Contains("Infinite loops"));
        }

        [Test]
        public void CheckSecurity_ForSemicolonsWithSpaces_Blocked()
        {
            var result = m_tool.CheckSecurity("for ( ; ; ) { count++; }");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_WhileOne_Blocked()
        {
            var result = m_tool.CheckSecurity("while(1) { }");
            Assert.IsFalse(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_GotoLabel_Blocked()
        {
            var result = m_tool.CheckSecurity("start: DoWork(); goto start;");
            Assert.IsFalse(result.isAllowed);
            Assert.IsTrue(result.reason.Contains("goto"));
        }

        #endregion

        #region Valid Code Tests

        [Test]
        public void CheckSecurity_SimpleReturn_Allowed()
        {
            var result = m_tool.CheckSecurity("return 1 + 1;");
            Assert.IsTrue(result.isAllowed);
            Assert.IsNull(result.reason);
        }

        [Test]
        public void CheckSecurity_UnityApiCall_Allowed()
        {
            var result = m_tool.CheckSecurity("return Application.unityVersion;");
            Assert.IsTrue(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_GameObjectCreate_Allowed()
        {
            var result = m_tool.CheckSecurity("var go = new GameObject(\"Test\"); return go.name;");
            Assert.IsTrue(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_LinqQuery_Allowed()
        {
            var result = m_tool.CheckSecurity("return GameObject.FindObjectsOfType<Light>().Length;");
            Assert.IsTrue(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_ForLoopBounded_Allowed()
        {
            var result = m_tool.CheckSecurity("int sum = 0; for(int i = 0; i < 10; i++) sum += i; return sum;");
            Assert.IsTrue(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_WhileWithCondition_Allowed()
        {
            var result = m_tool.CheckSecurity("int i = 0; while(i < 10) { i++; } return i;");
            Assert.IsTrue(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_FileReadAllText_Allowed()
        {
            // Reading files is allowed; only writing/deleting is blocked
            var result = m_tool.CheckSecurity("return System.IO.File.ReadAllText(\"test.txt\");");
            Assert.IsTrue(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_AssetDatabaseRefresh_Allowed()
        {
            var result = m_tool.CheckSecurity("AssetDatabase.Refresh();");
            Assert.IsTrue(result.isAllowed);
        }

        [Test]
        public void CheckSecurity_AssetDatabaseFindAssets_Allowed()
        {
            var result = m_tool.CheckSecurity("return AssetDatabase.FindAssets(\"t:Script\").Length;");
            Assert.IsTrue(result.isAllowed);
        }

        #endregion

        #region EditorPrefs Disable Tests

        [Test]
        public async Task ExecuteCSharp_WhenDisabled_ReturnsError()
        {
            // Save current state
            bool wasEnabled = UnityEditor.EditorPrefs.GetBool("NanoraUnityMCP.ExecuteCSharpEnabled", true);

            try
            {
                // Disable execute_csharp
                UnityEditor.EditorPrefs.SetBool("NanoraUnityMCP.ExecuteCSharpEnabled", false);

                var args = new JObject
                {
                    ["code"] = "return 1 + 1;"
                };

                var result = await m_tool.ExecuteAsync(args);

                Assert.IsTrue(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("disabled"));
            }
            finally
            {
                // Restore
                UnityEditor.EditorPrefs.SetBool("NanoraUnityMCP.ExecuteCSharpEnabled", wasEnabled);
            }
        }

        #endregion
    }
}
