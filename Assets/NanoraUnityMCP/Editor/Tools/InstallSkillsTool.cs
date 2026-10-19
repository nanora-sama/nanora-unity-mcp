using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("install_skills")]
    public class InstallSkillsTool : IMcpTool
    {
        public string Name => "install_skills";
        public string Description => "Install or update bundled Unity workflow skills into .claude/skills/. Use when you want to refresh the Unity AI workflow library or after updating nanora-unity-mcp. Detects and preserves user-modified skills unless force is set.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""dry_run"": {
                    ""type"": ""boolean"",
                    ""description"": ""If true, report what would change without modifying files."",
                    ""default"": false
                },
                ""force"": {
                    ""type"": ""boolean"",
                    ""description"": ""If true, overwrite user-modified skill files (a .bak is created first)."",
                    ""default"": false
                }
            },
            ""required"": []
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() => Execute(arguments));
        }

        private McpToolResult Execute(JObject args)
        {
            var dryRun = args?["dry_run"]?.Value<bool>() ?? false;
            var force = args?["force"]?.Value<bool>() ?? false;

            var reports = SkillInstaller.InstallAll(dryRun, force);

            var sb = new StringBuilder();
            sb.AppendLine(dryRun ? "# Install Skills (Dry Run)" : "# Install Skills");
            sb.AppendLine();
            sb.AppendLine($"Source: {SkillInstaller.GetBundledSkillsDir()}");
            sb.AppendLine($"Target: {SkillInstaller.GetTargetSkillsDir()}");
            sb.AppendLine();

            if (reports.Length == 0)
            {
                sb.AppendLine("No bundled skills found.");
                return McpToolResult.Success(sb.ToString());
            }

            foreach (var r in reports)
            {
                string icon;
                switch (r.Result)
                {
                    case SkillInstallResult.Installed: icon = "NEW"; break;
                    case SkillInstallResult.Updated: icon = "UPD"; break;
                    case SkillInstallResult.Unchanged: icon = "OK"; break;
                    case SkillInstallResult.SkippedUserModified: icon = "SKIP"; break;
                    default: icon = "FAIL"; break;
                }
                sb.AppendLine($"- [{icon}] {r.Name}: {r.Message}");
            }

            sb.AppendLine();
            sb.AppendLine(SkillInstaller.GetInstallSummary(reports));

            return McpToolResult.Success(sb.ToString());
        }
    }
}
