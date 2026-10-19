using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Tools;
using NanoraUnityMCP.Tools.Meta;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Meta tools (Token SuperSave system).
    /// </summary>
    [TestFixture]
    public class MetaToolTests
    {
        #region ListCategoriesTool Tests

        [Test]
        public async Task ListCategories_ReturnsAllCategories()
        {
            var tool = new ListCategoriesTool();
            var result = await tool.ExecuteAsync(new JObject());

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            // Verify all categories are present
            Assert.IsTrue(text.Contains("scene"));
            Assert.IsTrue(text.Contains("query"));
            Assert.IsTrue(text.Contains("batch"));
            Assert.IsTrue(text.Contains("code"));
            Assert.IsTrue(text.Contains("playmode"));
            Assert.IsTrue(text.Contains("test"));
            Assert.IsTrue(text.Contains("performance"));
            Assert.IsTrue(text.Contains("materials"));
            Assert.IsTrue(text.Contains("diagnostics"));
            Assert.IsTrue(text.Contains("scene_mgmt"));
        }

        [Test]
        public async Task ListCategories_IncludesToolCounts()
        {
            var tool = new ListCategoriesTool();
            var result = await tool.ExecuteAsync(new JObject());

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            // Verify counts are present (format: "category (count)")
            Assert.IsTrue(text.Contains("scene (5)"));
            Assert.IsTrue(text.Contains("query (4)"));
            Assert.IsTrue(text.Contains("batch (3)"));
        }

        #endregion

        #region ListToolsTool Tests

        [Test]
        public async Task ListTools_SceneCategory_ReturnsSceneTools()
        {
            var tool = new ListToolsTool();
            var args = new JObject { ["category"] = "scene" };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("get_scene_hierarchy"));
            Assert.IsTrue(text.Contains("get_object_details"));
            Assert.IsTrue(text.Contains("create_game_object"));
            Assert.IsTrue(text.Contains("modify_game_object"));
            Assert.IsTrue(text.Contains("delete_game_object"));
        }

        [Test]
        public async Task ListTools_CodeCategory_ReturnsCodeTools()
        {
            var tool = new ListToolsTool();
            var args = new JObject { ["category"] = "code" };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("execute_csharp"));
            Assert.IsTrue(text.Contains("execute_menu_item"));
            Assert.IsTrue(text.Contains("compile_project"));
            Assert.IsTrue(text.Contains("get_compilation_errors"));
        }

        [Test]
        public async Task ListTools_InvalidCategory_ReturnsError()
        {
            var tool = new ListToolsTool();
            var args = new JObject { ["category"] = "invalid_category" };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Unknown category"));
        }

        [Test]
        public async Task ListTools_EmptyCategory_ReturnsError()
        {
            var tool = new ListToolsTool();
            var args = new JObject { ["category"] = "" };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("required"));
        }

        [Test]
        public async Task ListTools_CaseInsensitive_Works()
        {
            var tool = new ListToolsTool();
            var args = new JObject { ["category"] = "SCENE" };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("get_scene_hierarchy"));
        }

        #endregion

        #region ExecuteTool Tests

        [Test]
        public async Task Execute_ValidTool_ExecutesSuccessfully()
        {
            var registry = new ToolRegistry();
            var executeTool = new ExecuteTool();
            executeTool.SetRegistry(registry);

            var args = new JObject
            {
                ["tool"] = "get_scene_hierarchy",
                ["args"] = new JObject()
            };

            var result = await executeTool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Scene:"));
        }

        [Test]
        public async Task Execute_InvalidTool_ReturnsError()
        {
            var registry = new ToolRegistry();
            var executeTool = new ExecuteTool();
            executeTool.SetRegistry(registry);

            var args = new JObject
            {
                ["tool"] = "nonexistent_tool",
                ["args"] = new JObject()
            };

            var result = await executeTool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("not found"));
        }

        [Test]
        public async Task Execute_EmptyToolName_ReturnsError()
        {
            var registry = new ToolRegistry();
            var executeTool = new ExecuteTool();
            executeTool.SetRegistry(registry);

            var args = new JObject { ["tool"] = "" };

            var result = await executeTool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("required"));
        }

        [Test]
        public async Task Execute_NoRegistry_ReturnsError()
        {
            var executeTool = new ExecuteTool();
            // Not setting registry

            var args = new JObject { ["tool"] = "get_scene_hierarchy" };

            var result = await executeTool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("not initialized"));
        }

        [Test]
        public async Task Execute_MetaTool_Blocked()
        {
            var registry = new ToolRegistry();
            var executeTool = new ExecuteTool();
            executeTool.SetRegistry(registry);

            var args = new JObject { ["tool"] = "execute" };

            var result = await executeTool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Cannot execute meta-tool"));
        }

        [Test]
        public async Task Execute_WithArgs_PassesArguments()
        {
            var testObj = new GameObject("ExecuteTestObject");
            try
            {
                var registry = new ToolRegistry();
                var executeTool = new ExecuteTool();
                executeTool.SetRegistry(registry);

                var args = new JObject
                {
                    ["tool"] = "query_objects",
                    ["args"] = new JObject { ["selector"] = "#ExecuteTestObject" }
                };

                var result = await executeTool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("ExecuteTestObject"));
            }
            finally
            {
                Object.DestroyImmediate(testObj);
            }
        }

        #endregion

        #region ToolCategories Tests

        [Test]
        public void GetCategoryForTool_ValidTool_ReturnsCategory()
        {
            var category = ToolCategories.GetCategoryForTool("get_scene_hierarchy");
            Assert.AreEqual("scene", category);
        }

        [Test]
        public void GetCategoryForTool_InvalidTool_ReturnsNull()
        {
            var category = ToolCategories.GetCategoryForTool("nonexistent_tool");
            Assert.IsNull(category);
        }

        [Test]
        public void GetToolsInCategory_ValidCategory_ReturnsTools()
        {
            var tools = ToolCategories.GetToolsInCategory("scene").ToList();

            Assert.AreEqual(5, tools.Count);
            Assert.Contains("get_scene_hierarchy", tools);
        }

        [Test]
        public void GetToolsInCategory_InvalidCategory_ReturnsEmpty()
        {
            var tools = ToolCategories.GetToolsInCategory("invalid").ToList();
            Assert.AreEqual(0, tools.Count);
        }

        [Test]
        public void ToolBriefs_ContainsAllTools()
        {
            // Count all tools from categories
            int totalFromCategories = ToolCategories.Categories.Values
                .SelectMany(c => c.ToolNames)
                .Count();

            Assert.AreEqual(totalFromCategories, ToolCategories.ToolBriefs.Count);
        }

        [Test]
        public void ToolBriefs_CoverEveryPackageTool()
        {
            // SuperSave mode can only discover tools listed in ToolCategories.
            var missing = typeof(IMcpTool).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IMcpTool).IsAssignableFrom(t))
                .Select(t => ((IMcpTool)System.Activator.CreateInstance(t)).Name)
                .Where(name => !ToolRegistry.IsMetaTool(name) && !ToolCategories.ToolBriefs.ContainsKey(name))
                .ToList();

            Assert.IsEmpty(missing, "Tools missing from ToolCategories: " + string.Join(", ", missing));
        }

        [Test]
        public async Task Initialize_ReturnsInstructionsAtTopLevel()
        {
            // MCP clients read InitializeResult.instructions; nested under serverInfo it is ignored.
            var protocol = new NanoraUnityMCP.Protocol.McpProtocol(new ToolRegistry());
            var response = JObject.Parse(await protocol.ProcessMessageAsync(
                @"{""jsonrpc"":""2.0"",""id"":1,""method"":""initialize"",""params"":{}}"));

            var instructions = (string)response["result"]["instructions"];
            Assert.IsNotNull(instructions, "initialize result has no top-level instructions");
            StringAssert.Contains("Unity project", instructions);
            Assert.IsNull(response["result"]["serverInfo"]["instructions"]);
        }

        [Test]
        public void ListTools_CategoryEnumMatchesCategories()
        {
            var values = new ListToolsTool().InputSchema["properties"]["category"]["enum"]
                .Select(v => (string)v);

            CollectionAssert.AreEquivalent(ToolCategories.Categories.Keys, values);
        }

        #endregion

        #region ToolRegistry SuperSave Mode Tests

        [Test]
        public void ToolRegistry_IsMetaTool_IdentifiesCorrectly()
        {
            Assert.IsTrue(ToolRegistry.IsMetaTool("list_categories"));
            Assert.IsTrue(ToolRegistry.IsMetaTool("list_tools"));
            Assert.IsTrue(ToolRegistry.IsMetaTool("execute"));
            Assert.IsFalse(ToolRegistry.IsMetaTool("get_scene_hierarchy"));
        }

        [Test]
        public void ToolRegistry_TotalToolCount_IncludesAllTools()
        {
            var registry = new ToolRegistry();
            // Should include all tools including meta tools
            Assert.IsTrue(registry.TotalToolCount >= 34); // 31 original + 3 meta
        }

        #endregion
    }
}
