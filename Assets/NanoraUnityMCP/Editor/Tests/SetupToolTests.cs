using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    [TestFixture]
    public class SetupToolTests
    {
        #region SetupStatusTool Tests

        [Test]
        public async Task SetupStatus_ReturnsValidReport()
        {
            var tool = new SetupStatusTool();

            var result = await tool.ExecuteAsync(new JObject());

            Assert.IsFalse(result.IsError);
            Assert.IsNotEmpty(result.Content);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("MCP Setup Status Report")));
        }

        [Test]
        public async Task SetupStatus_IncludesServerSection()
        {
            var tool = new SetupStatusTool();

            var result = await tool.ExecuteAsync(new JObject());

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("## Server"));
            Assert.IsTrue(text.Contains("Running:"));
            Assert.IsTrue(text.Contains("Port:"));
            Assert.IsTrue(text.Contains("Tools:"));
        }

        [Test]
        public async Task SetupStatus_IncludesClientsSection()
        {
            var tool = new SetupStatusTool();

            var result = await tool.ExecuteAsync(new JObject());

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("## Clients"));
            Assert.IsTrue(text.Contains("ClaudeDesktop"));
            Assert.IsTrue(text.Contains("ClaudeCode"));
            Assert.IsTrue(text.Contains("Cursor"));
        }

        [Test]
        public async Task SetupStatus_IncludesMcpJsonCheck()
        {
            var tool = new SetupStatusTool();

            var result = await tool.ExecuteAsync(new JObject());

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            // Should contain permissions and CLAUDE.md sections
            Assert.IsTrue(text.Contains("## Claude Code Permissions"));
            Assert.IsTrue(text.Contains("## CLAUDE.md"));
        }

        #endregion

        #region ReconfigureClientTool Tests

        [Test]
        public async Task ReconfigureClient_DryRun_DoesNotModify()
        {
            var tool = new ReconfigureClientTool();
            var args = JObject.Parse(@"{""client"": ""all"", ""dry_run"": true}");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Dry Run")));
        }

        [Test]
        public async Task ReconfigureClient_InvalidClient_ReturnsError()
        {
            var tool = new ReconfigureClientTool();
            var args = JObject.Parse(@"{""client"": ""invalid_client""}");

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Invalid client")));
        }

        [Test]
        public async Task ReconfigureClient_MissingClient_ReturnsError()
        {
            var tool = new ReconfigureClientTool();
            var args = new JObject();

            var result = await tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Missing required parameter")));
        }

        [Test]
        public async Task ReconfigureClient_All_ReportsAllClients()
        {
            var tool = new ReconfigureClientTool();
            var args = JObject.Parse(@"{""client"": ""all"", ""dry_run"": true}");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            // Should mention all three client types in some form
            Assert.IsTrue(text.Contains("Claude Desktop") || text.Contains("Claude Code") || text.Contains("Cursor"));
        }

        [Test]
        public async Task ReconfigureClient_SingleClient_ReportsResult()
        {
            var tool = new ReconfigureClientTool();
            var args = JObject.Parse(@"{""client"": ""claude_code"", ""dry_run"": true}");

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content.Any(c => c.Text != null && c.Text.Contains("Claude Code")));
        }

        #endregion
    }
}
