using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NanoraUnityMCP.Core
{
    public enum ClientType { ClaudeDesktop, ClaudeCode, Cursor }
    public enum ConfigResult { Configured, AlreadyUpToDate, Failed, NotInstalled }

    public struct ClientStatus
    {
        public ClientType Type;
        public bool IsInstalled;
        public bool IsConfigured;
        public bool HasCorrectPort;
        public string ConfigPath;
        public string Message;
    }

    /// <summary>
    /// Centralized MCP client configuration management.
    /// Handles detection, configuration, and status reporting for all supported clients.
    /// </summary>
    public static class ClientConfigManager
    {
        private const string k_ServerPattern = "mcp__nanora-unity-mcp__*";

        // --- Path Detection ---

        public static string GetClaudeDesktopConfigPath()
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return Path.Combine(homeDir, "Library/Application Support/Claude/claude_desktop_config.json");
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Claude/claude_desktop_config.json");
            }
            else
            {
                return Path.Combine(homeDir, ".config/Claude/claude_desktop_config.json");
            }
        }

        public static string GetMcpJsonPath()
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), ".mcp.json");
        }

        public static string GetCursorConfigPath()
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), ".cursor/mcp.json");
        }

        public static string GetClaudeSettingsPath()
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), ".claude/settings.local.json");
        }

        public static string GetProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }

        public static Dictionary<ClientType, string> DetectConfigPaths()
        {
            return new Dictionary<ClientType, string>
            {
                [ClientType.ClaudeDesktop] = GetClaudeDesktopConfigPath(),
                [ClientType.ClaudeCode] = GetMcpJsonPath(),
                [ClientType.Cursor] = GetCursorConfigPath()
            };
        }

        // --- Status Detection ---

        public static bool IsServerConfiguredInFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var content = File.ReadAllText(path);
                var json = JObject.Parse(content);
                var servers = json["mcpServers"] as JObject;
                if (servers == null) return false;
                var server = servers[McpServer.ServerName] as JObject;
                return server != null && server["url"] != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasCorrectPort(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var content = File.ReadAllText(path);
                return content.Contains($"localhost:{McpServer.Port}");
            }
            catch
            {
                return false;
            }
        }

        public static bool IsClaudeDesktopInstalled()
        {
            var path = GetClaudeDesktopConfigPath();
            var dir = Path.GetDirectoryName(path);
            return Directory.Exists(dir);
        }

        public static bool IsCursorInstalled()
        {
            // Check if Cursor app exists or if .cursor directory exists in project
            var cursorDir = Path.Combine(GetProjectRoot(), ".cursor");
            if (Directory.Exists(cursorDir)) return true;

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return Directory.Exists("/Applications/Cursor.app");
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Directory.Exists(Path.Combine(localAppData, "Programs/cursor"));
            }
            return false;
        }

        public static bool IsClaudeCodeInstalled()
        {
            // Claude Code is installed if the `claude` command is available
            // Or if .mcp.json exists (indicating prior use)
            if (File.Exists(GetMcpJsonPath())) return true;

            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/c where claude";
                }
                else
                {
                    startInfo.FileName = "/bin/bash";
                    startInfo.Arguments = "-c \"which claude\"";
                }

                var process = System.Diagnostics.Process.Start(startInfo);
                process.WaitForExit(3000);
                if (!process.HasExited) { process.Kill(); return false; }
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static ClientStatus GetClientStatus(ClientType type)
        {
            var status = new ClientStatus { Type = type };

            switch (type)
            {
                case ClientType.ClaudeDesktop:
                    status.ConfigPath = GetClaudeDesktopConfigPath();
                    status.IsInstalled = IsClaudeDesktopInstalled();
                    status.IsConfigured = IsServerConfiguredInFile(status.ConfigPath);
                    status.HasCorrectPort = HasCorrectPort(status.ConfigPath);
                    break;

                case ClientType.ClaudeCode:
                    status.ConfigPath = GetMcpJsonPath();
                    status.IsInstalled = IsClaudeCodeInstalled();
                    status.IsConfigured = IsServerConfiguredInFile(status.ConfigPath);
                    status.HasCorrectPort = HasCorrectPort(status.ConfigPath);
                    break;

                case ClientType.Cursor:
                    status.ConfigPath = GetCursorConfigPath();
                    status.IsInstalled = IsCursorInstalled();
                    status.IsConfigured = IsServerConfiguredInFile(status.ConfigPath);
                    status.HasCorrectPort = HasCorrectPort(status.ConfigPath);
                    break;
            }

            if (!status.IsInstalled)
                status.Message = "Not installed";
            else if (!status.IsConfigured)
                status.Message = "Installed, not configured";
            else if (!status.HasCorrectPort)
                status.Message = $"Configured but port mismatch (server: {McpServer.Port})";
            else
                status.Message = "Configured and up to date";

            return status;
        }

        public static ClientStatus[] GetAllClientStatuses()
        {
            return new[]
            {
                GetClientStatus(ClientType.ClaudeDesktop),
                GetClientStatus(ClientType.ClaudeCode),
                GetClientStatus(ClientType.Cursor)
            };
        }

        // --- Configuration ---

        public static ConfigResult ConfigureClient(ClientType type, bool dryRun = false)
        {
            var status = GetClientStatus(type);

            if (!status.IsInstalled && type != ClientType.ClaudeCode)
                return ConfigResult.NotInstalled;

            switch (type)
            {
                case ClientType.ClaudeDesktop:
                    return ConfigureFileBasedClient(status.ConfigPath, dryRun);

                case ClientType.ClaudeCode:
                    return ConfigureClaudeCode(dryRun);

                case ClientType.Cursor:
                    return ConfigureCursorClient(status.ConfigPath, dryRun);

                default:
                    return ConfigResult.Failed;
            }
        }

        public static Dictionary<ClientType, ConfigResult> ConfigureAllDetected(bool dryRun = false)
        {
            var results = new Dictionary<ClientType, ConfigResult>();

            foreach (ClientType type in Enum.GetValues(typeof(ClientType)))
            {
                results[type] = ConfigureClient(type, dryRun);
            }

            return results;
        }

        private static ConfigResult ConfigureFileBasedClient(string configPath, bool dryRun)
        {
            try
            {
                var folder = Path.GetDirectoryName(configPath);

                if (!dryRun && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string existingContent = "";
                bool fileExists = File.Exists(configPath);

                if (fileExists)
                {
                    existingContent = File.ReadAllText(configPath);
                    var merged = MergeConfigJson(existingContent, "mcpServers", McpServer.ServerName);

                    if (merged == existingContent)
                        return ConfigResult.AlreadyUpToDate;

                    if (!dryRun)
                    {
                        CreateBackup(configPath);
                        File.WriteAllText(configPath, merged);
                    }
                }
                else
                {
                    if (!dryRun)
                    {
                        var config = GenerateServerConfig();
                        File.WriteAllText(configPath, config);
                    }
                }

                return ConfigResult.Configured;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClientConfigManager] Failed to configure {configPath}: {ex.Message}");
                return ConfigResult.Failed;
            }
        }

        private static ConfigResult ConfigureClaudeCode(bool dryRun)
        {
            try
            {
                var mcpJsonPath = GetMcpJsonPath();
                var fileResult = ConfigureFileBasedClient(mcpJsonPath, dryRun);

                if (!dryRun && fileResult != ConfigResult.Failed)
                {
                    ConfigureClaudeCodePermissions();
                    SkillInstaller.InstallAll(dryRun: false, force: false);
                }

                return fileResult;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClientConfigManager] Failed to configure Claude Code: {ex.Message}");
                return ConfigResult.Failed;
            }
        }

        private static ConfigResult ConfigureCursorClient(string configPath, bool dryRun)
        {
            return ConfigureFileBasedClient(configPath, dryRun);
        }

        // --- Config Generation ---

        public static string GenerateServerConfig()
        {
            return $@"{{
  ""mcpServers"": {{
    ""{McpServer.ServerName}"": {{
      ""type"": ""sse"",
      ""url"": ""http://localhost:{McpServer.Port}/sse""
    }}
  }}
}}";
        }

        public static string GeneratePermissionsConfig()
        {
            return $@"{{
  ""permissions"": {{
    ""allow"": [
      ""{k_ServerPattern}""
    ],
    ""deny"": []
  }}
}}";
        }

        // --- JSON Operations ---

        public static string MergeConfigJson(string existingContent, string rootKey, string serverKey)
        {
            var newUrl = $"http://localhost:{McpServer.Port}/sse";

            JObject existing;
            try
            {
                existing = JObject.Parse(existingContent);
            }
            catch
            {
                var fresh = new JObject
                {
                    [rootKey] = new JObject
                    {
                        [serverKey] = new JObject { ["type"] = "sse", ["url"] = newUrl }
                    }
                };
                return fresh.ToString(Formatting.Indented);
            }

            if (existing[rootKey] == null || existing[rootKey].Type != JTokenType.Object)
            {
                existing[rootKey] = new JObject();
            }

            var servers = (JObject)existing[rootKey];
            servers[serverKey] = new JObject { ["type"] = "sse", ["url"] = newUrl };

            var result = existing.ToString(Formatting.Indented);

            if (NormalizeJson(existingContent) == NormalizeJson(result))
            {
                return existingContent;
            }

            return result;
        }

        public static string MergePermissionsConfig(string existingJson, string newJson)
        {
            JObject existing;
            try
            {
                existing = JObject.Parse(existingJson);
            }
            catch
            {
                return newJson;
            }

            if (existing["permissions"] == null || existing["permissions"].Type != JTokenType.Object)
            {
                existing["permissions"] = new JObject();
            }

            var permissions = (JObject)existing["permissions"];

            if (permissions["allow"] == null || permissions["allow"].Type != JTokenType.Array)
            {
                permissions["allow"] = new JArray();
            }

            var allowArray = (JArray)permissions["allow"];

            foreach (var item in allowArray)
            {
                if (item.Value<string>() == k_ServerPattern)
                {
                    return existingJson;
                }
            }

            allowArray.Add(k_ServerPattern);

            return existing.ToString(Formatting.Indented);
        }

        public static string NormalizeJson(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                var sorted = SortProperties(obj);
                return sorted.ToString(Formatting.None);
            }
            catch
            {
                return json;
            }
        }

        private static JObject SortProperties(JObject obj)
        {
            var sorted = new JObject();
            foreach (var prop in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                sorted.Add(prop.Name, prop.Value is JObject child ? SortProperties(child) : prop.Value);
            }
            return sorted;
        }

        public static void CreateBackup(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var backupPath = path + ".bak";
                    File.Copy(path, backupPath, overwrite: true);
                    Debug.Log($"[ClientConfigManager] Backup created: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClientConfigManager] Failed to create backup: {ex.Message}");
            }
        }

        // --- Permissions ---

        public static void ConfigureClaudeCodePermissions()
        {
            try
            {
                var claudeDir = Path.Combine(GetProjectRoot(), ".claude");
                var settingsPath = Path.Combine(claudeDir, "settings.local.json");

                if (!Directory.Exists(claudeDir))
                {
                    Directory.CreateDirectory(claudeDir);
                }

                var permissionsConfig = GeneratePermissionsConfig();

                if (File.Exists(settingsPath))
                {
                    var existingContent = File.ReadAllText(settingsPath);
                    try
                    {
                        var merged = MergePermissionsConfig(existingContent, permissionsConfig);
                        if (merged != existingContent)
                        {
                            CreateBackup(settingsPath);
                        }
                        File.WriteAllText(settingsPath, merged);
                    }
                    catch (Exception mergeEx)
                    {
                        Debug.LogWarning($"[ClientConfigManager] Failed to merge permissions, overwriting: {mergeEx.Message}");
                        CreateBackup(settingsPath);
                        File.WriteAllText(settingsPath, permissionsConfig);
                    }
                }
                else
                {
                    File.WriteAllText(settingsPath, permissionsConfig);
                }

                Debug.Log($"[ClientConfigManager] Permissions configured at: {settingsPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClientConfigManager] Failed to configure permissions: {ex.Message}");
            }
        }

        // --- CLAUDE.md ---

        private const string k_ClaudeMdSectionStart = "<!-- nanora-unity-mcp:start -->";
        private const string k_ClaudeMdSectionEnd = "<!-- nanora-unity-mcp:end -->";

        public static void EnsureClaudeMdHasMcpSection()
        {
            try
            {
                var claudeMdPath = Path.Combine(GetProjectRoot(), "CLAUDE.md");
                string content = File.Exists(claudeMdPath) ? File.ReadAllText(claudeMdPath) : "";
                var mcpSection = GetMcpClaudeMdSection();

                if (content.Contains(k_ClaudeMdSectionStart))
                {
                    int startIdx = content.IndexOf(k_ClaudeMdSectionStart);
                    int endIdx = content.IndexOf(k_ClaudeMdSectionEnd);
                    if (endIdx > startIdx)
                    {
                        endIdx += k_ClaudeMdSectionEnd.Length;
                        content = content.Substring(0, startIdx) + mcpSection + content.Substring(endIdx);
                        File.WriteAllText(claudeMdPath, content);
                        Debug.Log("[ClientConfigManager] Updated MCP section in CLAUDE.md");
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        content = mcpSection;
                    }
                    else
                    {
                        content = content.TrimEnd() + "\n\n" + mcpSection;
                    }
                    File.WriteAllText(claudeMdPath, content);
                    Debug.Log("[ClientConfigManager] Added MCP section to CLAUDE.md");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClientConfigManager] Failed to update CLAUDE.md: {ex.Message}");
            }
        }

        public static string GetMcpClaudeMdSection()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(k_ClaudeMdSectionStart);
            sb.AppendLine("## Unity MCP (nanora-unity-mcp)");
            sb.AppendLine();
            sb.AppendLine("This project uses nanora-unity-mcp to connect AI tools to Unity Editor.");
            sb.AppendLine();
            sb.AppendLine("### When MCP tools are unavailable");
            sb.AppendLine();
            sb.AppendLine("1. **Check `.mcp.json`** in project root for the server port");
            sb.AppendLine("2. **Verify server is running**:");
            sb.AppendLine("   ```bash");
            sb.AppendLine("   curl -s http://localhost:{port}/health");
            sb.AppendLine("   ```");
            sb.AppendLine("3. **Check global registry** for all running Unity MCP servers:");
            sb.AppendLine("   ```bash");
            sb.AppendLine("   cat ~/.unity-mcp-registry.json");
            sb.AppendLine("   ```");
            sb.AppendLine("4. **Port mismatch**: Update `.mcp.json` URL and restart the AI client session");
            sb.AppendLine();
            sb.AppendLine("### Common causes");
            sb.AppendLine("- Unity Editor is not running");
            sb.AppendLine("- `.mcp.json` has a stale port (Unity restarted)");
            sb.AppendLine("- Connected to wrong Unity project (check `projectPath` in `/health`)");
            sb.AppendLine("- Domain reload in progress (retry after 3-10 seconds)");
            sb.AppendLine(k_ClaudeMdSectionEnd);
            return sb.ToString();
        }

        // --- Setup Status Report ---

        public static string GetSetupStatus()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# MCP Setup Status Report");
            sb.AppendLine();

            // Server
            sb.AppendLine("## Server");
            sb.AppendLine($"- Running: {McpServer.IsRunning}");
            sb.AppendLine($"- Port: {McpServer.Port}");
            sb.AppendLine($"- Tools: {McpServer.GetRegisteredTools().Length}");
            sb.AppendLine();

            // Clients
            sb.AppendLine("## Clients");
            var statuses = GetAllClientStatuses();
            foreach (var status in statuses)
            {
                string icon = status.IsConfigured && status.HasCorrectPort ? "OK" :
                              status.IsConfigured ? "STALE" :
                              status.IsInstalled ? "UNCONFIGURED" : "NOT_INSTALLED";
                sb.AppendLine($"- {status.Type}: [{icon}] {status.Message}");
                if (!string.IsNullOrEmpty(status.ConfigPath))
                    sb.AppendLine($"  Path: {status.ConfigPath}");
            }
            sb.AppendLine();

            // Permissions
            sb.AppendLine("## Claude Code Permissions");
            var settingsPath = GetClaudeSettingsPath();
            if (File.Exists(settingsPath))
            {
                try
                {
                    var content = File.ReadAllText(settingsPath);
                    bool hasPattern = content.Contains(k_ServerPattern);
                    sb.AppendLine($"- settings.local.json: exists");
                    sb.AppendLine($"- MCP permissions: {(hasPattern ? "configured" : "missing")}");
                }
                catch
                {
                    sb.AppendLine($"- settings.local.json: error reading");
                }
            }
            else
            {
                sb.AppendLine($"- settings.local.json: not found");
            }
            sb.AppendLine();

            // Skills
            sb.AppendLine("## Bundled Skills");
            try
            {
                var skillStatuses = SkillInstaller.GetAllSkillStatuses();
                if (skillStatuses.Length == 0)
                {
                    sb.AppendLine("- No bundled skills found");
                }
                else
                {
                    int installed = 0, outdated = 0, modified = 0, missing = 0;
                    foreach (var s in skillStatuses)
                    {
                        if (!s.IsInstalled) missing++;
                        else if (s.IsUserModified) modified++;
                        else if (!s.IsUpToDate) outdated++;
                        else installed++;
                    }
                    sb.AppendLine($"- Total: {skillStatuses.Length}");
                    sb.AppendLine($"- Installed (up to date): {installed}");
                    if (outdated > 0) sb.AppendLine($"- Outdated: {outdated}");
                    if (modified > 0) sb.AppendLine($"- User-modified: {modified}");
                    if (missing > 0) sb.AppendLine($"- Not installed: {missing}");
                    foreach (var s in skillStatuses)
                    {
                        string icon = !s.IsInstalled ? "MISSING" :
                                      s.IsUserModified ? "MODIFIED" :
                                      !s.IsUpToDate ? "OUTDATED" : "OK";
                        sb.AppendLine($"  - [{icon}] {s.Name}: {s.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"- Error reading skill status: {ex.Message}");
            }
            sb.AppendLine();

            // CLAUDE.md
            sb.AppendLine("## CLAUDE.md");
            var claudeMdPath = Path.Combine(GetProjectRoot(), "CLAUDE.md");
            if (File.Exists(claudeMdPath))
            {
                var claudeMd = File.ReadAllText(claudeMdPath);
                bool hasMcpSection = claudeMd.Contains(k_ClaudeMdSectionStart);
                sb.AppendLine($"- Exists: true");
                sb.AppendLine($"- MCP section: {(hasMcpSection ? "present" : "missing")}");
            }
            else
            {
                sb.AppendLine($"- Exists: false");
            }

            return sb.ToString();
        }

        // --- Utility ---

        public static string GetClientDisplayName(ClientType type)
        {
            switch (type)
            {
                case ClientType.ClaudeDesktop: return "Claude Desktop";
                case ClientType.ClaudeCode: return "Claude Code";
                case ClientType.Cursor: return "Cursor";
                default: return type.ToString();
            }
        }

        public static string GetConfigResultMessage(ClientType type, ConfigResult result)
        {
            var name = GetClientDisplayName(type);
            switch (result)
            {
                case ConfigResult.Configured: return $"{name} - Config updated (backup: .bak)";
                case ConfigResult.AlreadyUpToDate: return $"{name} - Already up to date";
                case ConfigResult.Failed: return $"{name} - Configuration failed";
                case ConfigResult.NotInstalled: return $"{name} - Not installed (skipped)";
                default: return $"{name} - Unknown";
            }
        }
    }
}
