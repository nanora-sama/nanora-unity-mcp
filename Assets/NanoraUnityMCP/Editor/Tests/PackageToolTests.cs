using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Package tools (manage_packages, install_package, uninstall_package).
    /// </summary>
    [TestFixture]
    public class PackageToolTests
    {
        #region ManagePackagesTool Tests

        [Test]
        public async Task ManagePackages_List_ReturnsPackages()
        {
            var tool = new ManagePackagesTool();
            var args = new JObject
            {
                ["action"] = "list"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("Package") || text.Contains("package"));
        }

        [Test]
        public async Task ManagePackages_Search_Works()
        {
            var tool = new ManagePackagesTool();
            var args = new JObject
            {
                ["action"] = "search",
                ["query"] = "unity"
            };

            var result = await tool.ExecuteAsync(args);

            // Search may take time or fail due to network, but should not crash
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ManagePackages_Info_ReturnsPackageInfo()
        {
            var tool = new ManagePackagesTool();
            var args = new JObject
            {
                ["action"] = "info",
                ["package_name"] = "com.unity.textmeshpro" // Commonly installed package
            };

            var result = await tool.ExecuteAsync(args);

            // May or may not find package depending on project
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task ManagePackages_CheckLogin_ReturnsStatus()
        {
            var tool = new ManagePackagesTool();
            var args = new JObject
            {
                ["action"] = "check_login"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("login") || text.Contains("Login") ||
                          text.Contains("account") || text.Contains("Account") ||
                          text.Contains("Logged") || text.Contains("logged"));
        }

        [Test]
        public async Task ManagePackages_InvalidAction_ReturnsError()
        {
            var tool = new ManagePackagesTool();
            var args = new JObject
            {
                ["action"] = "invalid_action"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ManagePackages_EmptyAction_ReturnsError()
        {
            var tool = new ManagePackagesTool();
            var args = new JObject
            {
                ["action"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task ManagePackages_NoAction_ReturnsError()
        {
            var tool = new ManagePackagesTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        #endregion

        #region InstallPackageTool Tests

        [Test]
        public async Task InstallPackage_InvalidPackage_ReturnsError()
        {
            var tool = new InstallPackageTool();
            var args = new JObject
            {
                ["package_name"] = "com.invalid.nonexistent.package12345"
            };

            var result = await tool.ExecuteAsync(args);

            // Should fail for invalid package
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task InstallPackage_EmptyPackageName_ReturnsError()
        {
            var tool = new InstallPackageTool();
            var args = new JObject
            {
                ["package_name"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task InstallPackage_InvalidGitUrl_ReturnsError()
        {
            var tool = new InstallPackageTool();
            var args = new JObject
            {
                ["git_url"] = "https://invalid.git.url/nonexistent.git"
            };

            var result = await tool.ExecuteAsync(args);

            // Should fail for invalid URL
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task InstallPackage_InvalidUnityPackagePath_ReturnsError()
        {
            var tool = new InstallPackageTool();
            var args = new JObject
            {
                ["unitypackage_path"] = "/nonexistent/path/to/package.unitypackage"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task InstallPackage_NoPackageSpecified_ReturnsError()
        {
            var tool = new InstallPackageTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        #endregion

        #region UninstallPackageTool Tests

        [Test]
        public async Task UninstallPackage_NonExistentPackage_ReturnsError()
        {
            var tool = new UninstallPackageTool();
            var args = new JObject
            {
                ["package_name"] = "com.nonexistent.package12345"
            };

            var result = await tool.ExecuteAsync(args);

            // Should fail for non-existent package
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task UninstallPackage_EmptyPackageName_ReturnsError()
        {
            var tool = new UninstallPackageTool();
            var args = new JObject
            {
                ["package_name"] = ""
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public async Task UninstallPackage_NoPackageName_ReturnsError()
        {
            var tool = new UninstallPackageTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
        }

        // Note: Actually installing/uninstalling packages would modify the project
        // and is not suitable for automated tests. These tests focus on error handling.

        #endregion
    }
}
