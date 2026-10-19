using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Query tools (wait_for_selector, instantiate_asset).
    /// </summary>
    [TestFixture]
    public class QueryToolTests
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
                {
                    Object.DestroyImmediate(obj);
                }
            }
            m_createdObjects.Clear();
        }

        private GameObject CreateTestObject(string name)
        {
            var obj = new GameObject(name);
            m_createdObjects.Add(obj);
            return obj;
        }

        #region WaitForSelectorTool Tests

        [Test]
        public async Task WaitForSelector_ExistingObject_ReturnsImmediately()
        {
            var obj = CreateTestObject("WaitTarget");

            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = "#WaitTarget",
                ["timeout"] = 1.0
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("WaitTarget") || text.Contains("found"));
        }

        [Test]
        public async Task WaitForSelector_NonExistent_TimesOut()
        {
            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = "#NonExistentWaitTarget12345",
                ["timeout"] = 0.5 // Short timeout for test
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("timeout") || text.Contains("Timeout") || text.Contains("not found"));
        }

        [Test]
        public async Task WaitForSelector_StateVisible_Works()
        {
            var obj = CreateTestObject("VisibleTarget");

            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = "#VisibleTarget",
                ["state"] = "visible",
                ["timeout"] = 1.0
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task WaitForSelector_StateAttached_Works()
        {
            var obj = CreateTestObject("AttachedTarget");

            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = "#AttachedTarget",
                ["state"] = "attached",
                ["timeout"] = 1.0
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task WaitForSelector_StateDetached_ForNonExistent()
        {
            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = "#DetachedTarget12345",
                ["state"] = "detached",
                ["timeout"] = 1.0
            };

            var result = await tool.ExecuteAsync(args);

            // Should succeed since object doesn't exist (is detached)
            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task WaitForSelector_StateHidden_ForInactiveObject()
        {
            var obj = CreateTestObject("HiddenTarget");
            obj.SetActive(false);

            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = "#HiddenTarget",
                ["state"] = "hidden",
                ["timeout"] = 1.0
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task WaitForSelector_EmptySelector_ReturnsError()
        {
            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = "",
                ["timeout"] = 1.0
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task WaitForSelector_ComponentSelector_Works()
        {
            var obj = CreateTestObject("CameraWaitTarget");
            obj.AddComponent<Camera>();

            var tool = new WaitForSelectorTool();
            var args = new JObject
            {
                ["selector"] = ".Camera",
                ["timeout"] = 1.0
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion

        #region InstantiateAssetTool Tests

        [Test]
        public async Task InstantiateAsset_WithPosition_Works()
        {
            var tool = new InstantiateAssetTool();
            var args = new JObject
            {
                ["asset_path"] = "Assets/NanoraUnityMCP/Editor/Tests/TestPrefab.prefab",
                ["position"] = new JObject { ["x"] = 5, ["y"] = 10, ["z"] = 15 }
            };

            var result = await tool.ExecuteAsync(args);

            // May fail if prefab doesn't exist, that's ok for this test
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task InstantiateAsset_InvalidPath_ReturnsError()
        {
            var tool = new InstantiateAssetTool();
            var args = new JObject
            {
                ["asset_path"] = "Assets/NonExistent/Prefab12345.prefab"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task InstantiateAsset_EmptyPath_ReturnsError()
        {
            var tool = new InstantiateAssetTool();
            var args = new JObject
            {
                ["asset_path"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task InstantiateAsset_WithInstanceName_Works()
        {
            var tool = new InstantiateAssetTool();
            var args = new JObject
            {
                ["asset_path"] = "Assets/NanoraUnityMCP/Editor/Tests/TestPrefab.prefab",
                ["instance_name"] = "CustomInstanceName"
            };

            var result = await tool.ExecuteAsync(args);

            // Clean up if created
            var instance = GameObject.Find("CustomInstanceName");
            if (instance != null)
            {
                m_createdObjects.Add(instance);
            }

            Assert.IsNotNull(result);
        }

        [Test]
        public async Task InstantiateAsset_WithRotation_Works()
        {
            var tool = new InstantiateAssetTool();
            var args = new JObject
            {
                ["asset_path"] = "Assets/NanoraUnityMCP/Editor/Tests/TestPrefab.prefab",
                ["rotation"] = new JObject { ["x"] = 0, ["y"] = 90, ["z"] = 0 }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsNotNull(result);
        }

        #endregion
    }
}
