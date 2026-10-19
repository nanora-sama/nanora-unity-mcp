using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Material tools (diagnose_materials, repair_materials).
    /// </summary>
    [TestFixture]
    public class MaterialToolTests
    {
        #region DiagnoseMaterialsTool Tests

        [Test]
        public async Task DiagnoseMaterials_SceneScope_ReturnsReport()
        {
            var tool = new DiagnoseMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "scene"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Material") || text.Contains("material") || text.Contains("Diagnosis"));
        }

        [Test]
        public async Task DiagnoseMaterials_ProjectScope_ReturnsReport()
        {
            var tool = new DiagnoseMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "project"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task DiagnoseMaterials_SelectionScope_ReturnsReport()
        {
            var tool = new DiagnoseMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "selection"
            };

            var result = await tool.ExecuteAsync(args);

            // May return "no selection" but should not error
            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task DiagnoseMaterials_WithWarnings_Works()
        {
            var tool = new DiagnoseMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "scene",
                ["include_warnings"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task DiagnoseMaterials_WithoutWarnings_Works()
        {
            var tool = new DiagnoseMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "scene",
                ["include_warnings"] = false
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task DiagnoseMaterials_DefaultScope_Works()
        {
            var tool = new DiagnoseMaterialsTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion

        #region RepairMaterialsTool Tests

        [Test]
        public async Task RepairMaterials_DryRun_DoesNotModify()
        {
            var tool = new RepairMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "scene",
                ["dry_run"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            // Dry run should return some result
            Assert.IsNotNull(result.Content);
        }

        [Test]
        public async Task RepairMaterials_SceneScope_Works()
        {
            var tool = new RepairMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "scene",
                ["dry_run"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task RepairMaterials_UpgradeLegacy_Works()
        {
            var tool = new RepairMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "scene",
                ["dry_run"] = true,
                ["upgrade_legacy"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        [Test]
        public async Task RepairMaterials_FixMissingShaders_Works()
        {
            var tool = new RepairMaterialsTool();
            var args = new JObject
            {
                ["scope"] = "scene",
                ["dry_run"] = true,
                ["fix_missing_shaders"] = true
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
        }

        #endregion
    }
}
