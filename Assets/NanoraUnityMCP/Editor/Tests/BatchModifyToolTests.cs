using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for BatchModifyTool and related scene manipulation tools.
    /// </summary>
    [TestFixture]
    public class BatchModifyToolTests
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

        #region BatchModifyTool Tests

        [Test]
        public async Task BatchModify_ModifiesPosition()
        {
            var obj1 = CreateTestObject("BatchTarget1");
            var obj2 = CreateTestObject("BatchTarget2");

            var tool = new BatchModifyTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#BatchTarget1"",
                ""modifications"": [
                    {
                        ""target"": ""Transform.localPosition"",
                        ""operation"": ""set"",
                        ""value"": { ""x"": 5, ""y"": 10, ""z"": 15 }
                    }
                ]
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(new Vector3(5, 10, 15), obj1.transform.localPosition);
        }

        [Test]
        public async Task BatchModify_AddOperation_Works()
        {
            var obj = CreateTestObject("AddTarget");
            obj.transform.localScale = new Vector3(1, 1, 1);

            var tool = new BatchModifyTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#AddTarget"",
                ""modifications"": [
                    {
                        ""target"": ""Transform.localScale"",
                        ""operation"": ""add"",
                        ""value"": { ""x"": 1, ""y"": 1, ""z"": 1 }
                    }
                ]
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(new Vector3(2, 2, 2), obj.transform.localScale);
        }

        [Test]
        public async Task BatchModify_MultiplyOperation_Works()
        {
            var obj = CreateTestObject("MultiplyTarget");
            obj.transform.localScale = new Vector3(2, 2, 2);

            var tool = new BatchModifyTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#MultiplyTarget"",
                ""modifications"": [
                    {
                        ""target"": ""Transform.localScale"",
                        ""operation"": ""multiply"",
                        ""value"": { ""x"": 2, ""y"": 2, ""z"": 2 }
                    }
                ]
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(new Vector3(4, 4, 4), obj.transform.localScale);
        }

        [Test]
        public async Task BatchModify_DryRun_DoesNotModify()
        {
            var obj = CreateTestObject("DryRunTarget");
            var originalPos = obj.transform.position;

            var tool = new BatchModifyTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#DryRunTarget"",
                ""modifications"": [
                    {
                        ""target"": ""Transform.localPosition"",
                        ""operation"": ""set"",
                        ""value"": { ""x"": 100, ""y"": 100, ""z"": 100 }
                    }
                ],
                ""dry_run"": true
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Dry Run")));
            Assert.AreEqual(originalPos, obj.transform.position);
        }

        [Test]
        public async Task BatchModify_NoMatchingSelector_ReturnsError()
        {
            var tool = new BatchModifyTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#NonExistent"",
                ""modifications"": [
                    { ""target"": ""Transform.localPosition"", ""value"": { ""x"": 0, ""y"": 0, ""z"": 0 } }
                ]
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        #endregion

        #region SetActiveTool Tests

        [Test]
        public async Task SetActive_DeactivatesObjects()
        {
            var obj = CreateTestObject("ActiveTarget");
            Assert.IsTrue(obj.activeSelf);

            var tool = new SetActiveTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#ActiveTarget"",
                ""active"": false
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsFalse(obj.activeSelf);
        }

        [Test]
        public async Task SetActive_ActivatesObjects()
        {
            var obj = CreateTestObject("InactiveTarget");
            obj.SetActive(false);
            Assert.IsFalse(obj.activeSelf);

            var tool = new SetActiveTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#InactiveTarget"",
                ""active"": true,
                ""include_inactive"": true
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(obj.activeSelf);
        }

        #endregion

        #region DestroyObjectsTool Tests

        [Test]
        public async Task DestroyObjects_WithoutConfirm_ReturnsError()
        {
            var obj = CreateTestObject("DestroyTarget");

            var tool = new DestroyObjectsTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#DestroyTarget"",
                ""confirm"": false
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsNotNull(obj); // Object should still exist
        }

        [Test]
        public async Task DestroyObjects_WithConfirm_DestroysObjects()
        {
            var obj = CreateTestObject("DestroyTarget");
            var objName = obj.name;

            var tool = new DestroyObjectsTool();
            var args = JObject.Parse(@"{
                ""selector"": ""#DestroyTarget"",
                ""confirm"": true
            }");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsNull(GameObject.Find(objName));

            // Remove from cleanup list since it's already destroyed
            m_createdObjects.Remove(obj);
        }

        #endregion
    }
}
