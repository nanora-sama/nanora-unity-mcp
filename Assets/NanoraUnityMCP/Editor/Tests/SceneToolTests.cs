using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Scene tools (modify_game_object, delete_game_object, manage_scene).
    /// </summary>
    [TestFixture]
    public class SceneToolTests
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

        #region ModifyGameObjectTool Tests

        [Test]
        public async Task ModifyGameObject_ChangePosition_Works()
        {
            var obj = CreateTestObject("ModifyPositionTarget");

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "ModifyPositionTarget",
                ["position"] = new JObject { ["x"] = 10, ["y"] = 20, ["z"] = 30 }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(new Vector3(10, 20, 30), obj.transform.localPosition);
        }

        [Test]
        public async Task ModifyGameObject_ChangeRotation_Works()
        {
            var obj = CreateTestObject("ModifyRotationTarget");

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "ModifyRotationTarget",
                ["rotation"] = new JObject { ["x"] = 0, ["y"] = 90, ["z"] = 0 }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(90, obj.transform.localEulerAngles.y, 0.1f);
        }

        [Test]
        public async Task ModifyGameObject_ChangeScale_Works()
        {
            var obj = CreateTestObject("ModifyScaleTarget");

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "ModifyScaleTarget",
                ["scale"] = new JObject { ["x"] = 2, ["y"] = 3, ["z"] = 4 }
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(new Vector3(2, 3, 4), obj.transform.localScale);
        }

        [Test]
        public async Task ModifyGameObject_ChangeName_Works()
        {
            var obj = CreateTestObject("OriginalName");

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "OriginalName",
                ["new_name"] = "NewName"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("NewName", obj.name);
        }

        [Test]
        public async Task ModifyGameObject_ChangeActive_Works()
        {
            var obj = CreateTestObject("ActiveTarget");
            Assert.IsTrue(obj.activeSelf);

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "ActiveTarget",
                ["active"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsFalse(obj.activeSelf);
        }

        [Test]
        public async Task ModifyGameObject_ChangeParent_Works()
        {
            var parent = CreateTestObject("NewParent");
            var child = CreateTestObject("ChildToReparent");
            Assert.IsNull(child.transform.parent);

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "ChildToReparent",
                ["new_parent_path"] = "NewParent"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual(parent.transform, child.transform.parent);
        }

        [Test]
        public async Task ModifyGameObject_Unparent_Works()
        {
            var parent = CreateTestObject("ParentToLeave");
            var child = CreateTestObject("ChildToUnparent");
            child.transform.SetParent(parent.transform);
            Assert.IsNotNull(child.transform.parent);

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "ParentToLeave/ChildToUnparent",
                ["new_parent_path"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsNull(child.transform.parent);
        }

        [Test]
        public async Task ModifyGameObject_NonExistent_ReturnsError()
        {
            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "NonExistentObject12345",
                ["active"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ModifyGameObject_ChangeTag_Works()
        {
            var obj = CreateTestObject("TagTarget");

            var tool = new ModifyGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "TagTarget",
                ["tag"] = "MainCamera" // Use existing tag
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.AreEqual("MainCamera", obj.tag);
        }

        #endregion

        #region DeleteGameObjectTool Tests

        [Test]
        public async Task DeleteGameObject_DeletesObject()
        {
            var obj = CreateTestObject("DeleteTarget");
            var objName = obj.name;

            var tool = new DeleteGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "DeleteTarget"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsNull(GameObject.Find(objName));

            // Remove from cleanup list
            m_createdObjects.Remove(obj);
        }

        [Test]
        public async Task DeleteGameObject_NonExistent_ReturnsError()
        {
            var tool = new DeleteGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "NonExistentDeleteTarget12345"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task DeleteGameObject_WithChildren_DeletesAll()
        {
            var parent = CreateTestObject("ParentToDelete");
            var child = CreateTestObject("ChildOfParent");
            child.transform.SetParent(parent.transform);

            var tool = new DeleteGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = "ParentToDelete"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsNull(GameObject.Find("ParentToDelete"));
            Assert.IsNull(GameObject.Find("ChildOfParent"));

            m_createdObjects.Remove(parent);
            m_createdObjects.Remove(child);
        }

        [Test]
        public async Task DeleteGameObject_EmptyPath_ReturnsError()
        {
            var tool = new DeleteGameObjectTool();
            var args = new JObject
            {
                ["object_path"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        #endregion

        #region SceneManagementTool Tests

        [Test]
        public async Task ManageScene_Info_ReturnsSceneInfo()
        {
            var tool = new SceneManagementTool();
            var args = new JObject
            {
                ["action"] = "info"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Scene") || text.Contains("scene"));
        }

        [Test]
        public async Task ManageScene_InvalidAction_ReturnsError()
        {
            var tool = new SceneManagementTool();
            var args = new JObject
            {
                ["action"] = "invalid_action"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ManageScene_EmptyAction_ReturnsError()
        {
            var tool = new SceneManagementTool();
            var args = new JObject
            {
                ["action"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        // Note: new/save/save_as/load actions modify the scene state
        // and are difficult to test without side effects.
        // These would be better suited for integration tests.

        #endregion
    }
}
