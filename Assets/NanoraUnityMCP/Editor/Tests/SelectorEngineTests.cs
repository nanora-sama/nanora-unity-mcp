using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for U-Selector engine.
    /// </summary>
    [TestFixture]
    public class SelectorEngineTests
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

        private GameObject CreateTestObject(string name, Transform parent = null)
        {
            var obj = new GameObject(name);
            if (parent != null)
            {
                obj.transform.SetParent(parent);
            }
            m_createdObjects.Add(obj);
            return obj;
        }

        #region ID Selector Tests (#Name)

        [Test]
        public void Query_IdSelector_FindsObjectByExactName()
        {
            var target = CreateTestObject("TestPlayer");
            CreateTestObject("OtherObject");

            var results = SelectorEngine.Query("#TestPlayer");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(target, results[0]);
        }

        [Test]
        public void Query_IdSelector_ReturnsEmptyForNonExistent()
        {
            CreateTestObject("SomeObject");

            var results = SelectorEngine.Query("#NonExistent");

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Query_IdSelector_FindsMultipleWithSameName()
        {
            CreateTestObject("DuplicateName");
            CreateTestObject("DuplicateName");
            CreateTestObject("DuplicateName");

            var results = SelectorEngine.Query("#DuplicateName");

            Assert.AreEqual(3, results.Count);
        }

        #endregion

        #region Type Selector Tests (.Component)

        [Test]
        public void Query_TypeSelector_FindsObjectsWithCamera()
        {
            var withCamera = CreateTestObject("WithCamera");
            withCamera.AddComponent<Camera>();
            CreateTestObject("WithoutCamera");

            var results = SelectorEngine.Query(".Camera");

            // CI環境にはデフォルトのMain Cameraがある可能性があるため、Containsでチェック
            Assert.IsTrue(results.Count >= 1);
            Assert.Contains(withCamera, results);
        }

        [Test]
        public void Query_TypeSelector_FindsObjectsWithLight()
        {
            var withLight = CreateTestObject("WithLight");
            withLight.AddComponent<Light>();
            CreateTestObject("WithoutLight");

            var results = SelectorEngine.Query(".Light");

            // CI環境にはデフォルトのDirectional Lightがある可能性があるため、Containsでチェック
            Assert.IsTrue(results.Count >= 1);
            Assert.Contains(withLight, results);
        }

        [Test]
        public void Query_TypeSelector_ReturnsEmptyForNoMatches()
        {
            CreateTestObject("NoRigidbody");

            var results = SelectorEngine.Query(".Rigidbody");

            Assert.AreEqual(0, results.Count);
        }

        #endregion

        #region Path Selector Tests (/Path)

        [Test]
        public void Query_PathSelector_FindsObjectByPath()
        {
            var parent = CreateTestObject("Parent");
            var child = CreateTestObject("Child", parent.transform);

            var results = SelectorEngine.Query("/Parent/Child");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(child, results[0]);
        }

        [Test]
        public void Query_PathSelector_FindsRootObject()
        {
            var root = CreateTestObject("RootObject");

            var results = SelectorEngine.Query("/RootObject");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(root, results[0]);
        }

        [Test]
        public void Query_PathSelector_ReturnsEmptyForInvalidPath()
        {
            var parent = CreateTestObject("Parent");
            CreateTestObject("Child", parent.transform);

            var results = SelectorEngine.Query("/Parent/WrongChild");

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Query_PathSelector_HandlesDeepHierarchy()
        {
            var root = CreateTestObject("Root");
            var level1 = CreateTestObject("Level1", root.transform);
            var level2 = CreateTestObject("Level2", level1.transform);
            var level3 = CreateTestObject("Level3", level2.transform);

            var results = SelectorEngine.Query("/Root/Level1/Level2/Level3");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(level3, results[0]);
        }

        #endregion

        #region Attribute Selector Tests ([attr=value])

        [Test]
        public void Query_AttributeSelector_FindsByTag()
        {
            var tagged = CreateTestObject("TaggedObject");
            tagged.tag = "MainCamera";
            CreateTestObject("UntaggedObject");

            var results = SelectorEngine.Query("[tag=MainCamera]");

            // CI環境にはデフォルトのMain Cameraがある可能性があるため、Containsでチェック
            Assert.IsTrue(results.Count >= 1);
            Assert.Contains(tagged, results);
        }

        [Test]
        public void Query_AttributeSelector_FindsByName()
        {
            var target = CreateTestObject("SpecificName");
            CreateTestObject("OtherName");

            var results = SelectorEngine.Query("[name=SpecificName]");

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(target, results[0]);
        }

        [Test]
        public void Query_AttributeSelector_FindsByActiveState()
        {
            var active = CreateTestObject("ActiveObject");
            var inactive = CreateTestObject("InactiveObject");
            inactive.SetActive(false);

            var results = SelectorEngine.Query("[activeSelf=true]");

            Assert.Contains(active, results);
            Assert.IsFalse(results.Contains(inactive));
        }

        #endregion

        #region Include Inactive Tests

        [Test]
        public void Query_IncludeInactive_FindsInactiveObjects()
        {
            var active = CreateTestObject("ActiveObj");
            var inactive = CreateTestObject("InactiveObj");
            inactive.SetActive(false);

            var resultsWithInactive = SelectorEngine.Query("#InactiveObj", includeInactive: true);
            var resultsWithoutInactive = SelectorEngine.Query("#InactiveObj", includeInactive: false);

            Assert.AreEqual(1, resultsWithInactive.Count);
            Assert.AreEqual(0, resultsWithoutInactive.Count);
        }

        #endregion

        #region QueryOne Tests

        [Test]
        public void QueryOne_SingleMatch_ReturnsObject()
        {
            var target = CreateTestObject("UniqueObject");

            var result = SelectorEngine.QueryOne("#UniqueObject");

            Assert.AreEqual(target, result);
        }

        [Test]
        public void QueryOne_NoMatch_ThrowsException()
        {
            Assert.Throws<System.Exception>(() =>
            {
                SelectorEngine.QueryOne("#NonExistent");
            });
        }

        [Test]
        public void QueryOne_MultipleMatches_ThrowsAmbiguousException()
        {
            CreateTestObject("Duplicate");
            CreateTestObject("Duplicate");

            Assert.Throws<AmbiguousSelectorException>(() =>
            {
                SelectorEngine.QueryOne("#Duplicate");
            });
        }

        #endregion

        #region Combined Selector Tests

        [Test]
        public void Query_CombinedTypeAndAttribute_Works()
        {
            var cameraWithTag = CreateTestObject("TaggedCamera");
            cameraWithTag.AddComponent<Camera>();
            cameraWithTag.tag = "MainCamera";

            var cameraWithoutTag = CreateTestObject("UntaggedCamera");
            cameraWithoutTag.AddComponent<Camera>();

            var results = SelectorEngine.Query(".Camera[tag=MainCamera]");

            // CI環境にはデフォルトのMain Camera（Camera + MainCameraタグ）がある可能性があるため、Containsでチェック
            Assert.IsTrue(results.Count >= 1);
            Assert.Contains(cameraWithTag, results);
            // タグなしカメラは結果に含まれないことを確認
            Assert.IsFalse(results.Contains(cameraWithoutTag));
        }

        #endregion
    }
}
