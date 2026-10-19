using NUnit.Framework;
using NanoraUnityMCP.Core;

namespace NanoraUnityMCP.Tests
{
    [TestFixture]
    public class ClientConfigManagerTests
    {
        #region MergeConfigJson Tests

        [Test]
        public void MergeConfigJson_PreservesExistingServers()
        {
            var existing = @"{
  ""mcpServers"": {
    ""other-server"": {
      ""type"": ""sse"",
      ""url"": ""http://localhost:9999/sse""
    }
  }
}";
            var result = ClientConfigManager.MergeConfigJson(existing, "mcpServers", "nanora-unity-mcp");

            Assert.IsTrue(result.Contains("other-server"));
            Assert.IsTrue(result.Contains("nanora-unity-mcp"));
            Assert.IsTrue(result.Contains("9999"));
        }

        [Test]
        public void MergeConfigJson_UpdatesStalePort()
        {
            var existing = @"{
  ""mcpServers"": {
    ""nanora-unity-mcp"": {
      ""type"": ""sse"",
      ""url"": ""http://localhost:99999/sse""
    }
  }
}";
            var result = ClientConfigManager.MergeConfigJson(existing, "mcpServers", "nanora-unity-mcp");

            // Should contain the current port, not the old one
            Assert.IsTrue(result.Contains(McpServer.Port.ToString()));
        }

        [Test]
        public void MergeConfigJson_CreatesNewOnInvalidJson()
        {
            var invalidJson = "this is not json {{{";

            var result = ClientConfigManager.MergeConfigJson(invalidJson, "mcpServers", "nanora-unity-mcp");

            Assert.IsTrue(result.Contains("mcpServers"));
            Assert.IsTrue(result.Contains("nanora-unity-mcp"));
        }

        [Test]
        public void MergeConfigJson_PreservesOtherTopLevelKeys()
        {
            var existing = @"{
  ""globalShortcut"": ""Ctrl+Space"",
  ""theme"": ""dark"",
  ""mcpServers"": {}
}";
            var result = ClientConfigManager.MergeConfigJson(existing, "mcpServers", "nanora-unity-mcp");

            Assert.IsTrue(result.Contains("globalShortcut"));
            Assert.IsTrue(result.Contains("theme"));
            Assert.IsTrue(result.Contains("nanora-unity-mcp"));
        }

        [Test]
        public void MergeConfigJson_CreatesRootKeyIfMissing()
        {
            var existing = @"{ ""someOtherKey"": true }";

            var result = ClientConfigManager.MergeConfigJson(existing, "mcpServers", "nanora-unity-mcp");

            Assert.IsTrue(result.Contains("mcpServers"));
            Assert.IsTrue(result.Contains("nanora-unity-mcp"));
            Assert.IsTrue(result.Contains("someOtherKey"));
        }

        #endregion

        #region MergePermissionsConfig Tests

        [Test]
        public void MergePermissions_PreservesExistingEntries()
        {
            var existing = @"{
  ""permissions"": {
    ""allow"": [""Bash(npm *)"", ""Read""],
    ""deny"": []
  }
}";
            var newConfig = ClientConfigManager.GeneratePermissionsConfig();
            var result = ClientConfigManager.MergePermissionsConfig(existing, newConfig);

            Assert.IsTrue(result.Contains("Bash(npm *)"));
            Assert.IsTrue(result.Contains("Read"));
            Assert.IsTrue(result.Contains("mcp__nanora-unity-mcp__*"));
        }

        [Test]
        public void MergePermissions_DoesNotDuplicate()
        {
            var existing = @"{
  ""permissions"": {
    ""allow"": [""mcp__nanora-unity-mcp__*""],
    ""deny"": []
  }
}";
            var newConfig = ClientConfigManager.GeneratePermissionsConfig();
            var result = ClientConfigManager.MergePermissionsConfig(existing, newConfig);

            // Should return original (no change)
            Assert.AreEqual(existing, result);
        }

        [Test]
        public void MergePermissions_CreatesOnInvalidJson()
        {
            var invalidJson = "not json";
            var newConfig = ClientConfigManager.GeneratePermissionsConfig();

            var result = ClientConfigManager.MergePermissionsConfig(invalidJson, newConfig);

            Assert.AreEqual(newConfig, result);
        }

        [Test]
        public void MergePermissions_CreatesAllowArrayIfMissing()
        {
            var existing = @"{ ""permissions"": {} }";
            var newConfig = ClientConfigManager.GeneratePermissionsConfig();

            var result = ClientConfigManager.MergePermissionsConfig(existing, newConfig);

            Assert.IsTrue(result.Contains("allow"));
            Assert.IsTrue(result.Contains("mcp__nanora-unity-mcp__*"));
        }

        #endregion

        #region GetAllClientStatuses Tests

        [Test]
        public void GetAllClientStatuses_ReturnsAllThreeClients()
        {
            var statuses = ClientConfigManager.GetAllClientStatuses();

            Assert.AreEqual(3, statuses.Length);

            bool hasDesktop = false, hasCode = false, hasCursor = false;
            foreach (var s in statuses)
            {
                if (s.Type == ClientType.ClaudeDesktop) hasDesktop = true;
                if (s.Type == ClientType.ClaudeCode) hasCode = true;
                if (s.Type == ClientType.Cursor) hasCursor = true;
            }

            Assert.IsTrue(hasDesktop, "Missing ClaudeDesktop status");
            Assert.IsTrue(hasCode, "Missing ClaudeCode status");
            Assert.IsTrue(hasCursor, "Missing Cursor status");
        }

        [Test]
        public void GetClientStatus_HasNonNullMessage()
        {
            var status = ClientConfigManager.GetClientStatus(ClientType.ClaudeDesktop);

            Assert.IsNotNull(status.Message);
            Assert.IsNotEmpty(status.Message);
        }

        #endregion

        #region NormalizeJson Tests

        [Test]
        public void NormalizeJson_ProducesSameOutputForEquivalentJson()
        {
            var json1 = "{ \"a\": 1, \"b\": 2 }";
            var json2 = "{\"b\":2,\"a\":1}";

            // Both should normalize to the same output
            var norm1 = ClientConfigManager.NormalizeJson(json1);
            var norm2 = ClientConfigManager.NormalizeJson(json2);

            Assert.AreEqual(norm1, norm2);
        }

        [Test]
        public void NormalizeJson_ReturnsOriginalOnInvalidJson()
        {
            var invalid = "not json {{{";
            var result = ClientConfigManager.NormalizeJson(invalid);

            Assert.AreEqual(invalid, result);
        }

        #endregion

        #region GetSetupStatus Tests

        [Test]
        public void GetSetupStatus_ReturnsNonEmptyReport()
        {
            var report = ClientConfigManager.GetSetupStatus();

            Assert.IsNotNull(report);
            Assert.IsNotEmpty(report);
            Assert.IsTrue(report.Contains("MCP Setup Status Report"));
        }

        #endregion

        #region Path Detection Tests

        [Test]
        public void DetectConfigPaths_ReturnsAllThreeClients()
        {
            var paths = ClientConfigManager.DetectConfigPaths();

            Assert.AreEqual(3, paths.Count);
            Assert.IsTrue(paths.ContainsKey(ClientType.ClaudeDesktop));
            Assert.IsTrue(paths.ContainsKey(ClientType.ClaudeCode));
            Assert.IsTrue(paths.ContainsKey(ClientType.Cursor));
        }

        [Test]
        public void GetMcpJsonPath_ContainsMcpJson()
        {
            var path = ClientConfigManager.GetMcpJsonPath();

            Assert.IsTrue(path.EndsWith(".mcp.json"));
        }

        [Test]
        public void GetCursorConfigPath_ContainsCursorMcpJson()
        {
            var path = ClientConfigManager.GetCursorConfigPath();

            Assert.IsTrue(path.Contains(".cursor"));
            Assert.IsTrue(path.EndsWith("mcp.json"));
        }

        #endregion
    }
}
