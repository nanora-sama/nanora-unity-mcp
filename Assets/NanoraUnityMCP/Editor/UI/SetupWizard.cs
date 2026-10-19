using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.UI
{
    /// <summary>
    /// Setup wizard for first-time configuration of Unity MCP.
    /// 3-step wizard: Welcome → Auto Configure → Complete.
    /// </summary>
    public class SetupWizard : EditorWindow
    {
        private const string k_SetupCompletedKey = "UnityMCP_SetupCompleted";
        private const string k_SetupVersionKey = "UnityMCP_SetupVersion";
        private const int k_CurrentSetupVersion = 5;

        private int m_currentStep = 0;
        private Vector2 m_scrollPos;
        private bool m_isTestingConnection;
        private string m_connectionTestResult;
        private bool m_connectionTestSuccess;

        // Auto Configure state
        private ClientStatus[] m_clientStatuses;
        private bool m_autoConfigExecuted;
        private Dictionary<ClientType, ConfigResult> m_autoConfigResults;
        private bool m_showManualConfig;

        // Skill install state
        private SkillStatus[] m_skillStatuses;
        private SkillInstallReport[] m_skillInstallReports;

        // Auto test on Complete step
        private bool m_autoTestStarted;

        // File I/O cache
        private Dictionary<string, (bool exists, string content, double checkedAt)> m_fileCache;

        private static readonly string[] s_StepTitles = new[]
        {
            "Welcome",
            "Configure",
            "Complete"
        };

        // --- GUIStyle Cache ---
        private static class Styles
        {
            public static GUIStyle Header;
            public static GUIStyle ProgressBox;
            public static GUIStyle ProgressLine;
            public static GUIStyle StatusGreen;
            public static GUIStyle StatusRed;
            public static GUIStyle PortWarning;
            public static GUIStyle MiniLabelGreen;
            public static GUIStyle MiniLabelWarning;
            public static GUIStyle MiniLabelGrey;
            public static GUIStyle ConfiguredBadge;
            public static GUIStyle ClientBox;
            public static GUIStyle CheckMark;
            public static GUIStyle ConfigFileStatus;
            public static GUIStyle ResultLabel;

            private static bool s_initialized;

            public static void EnsureInitialized()
            {
                if (s_initialized) return;
                s_initialized = true;

                Header = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };

                ProgressBox = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };

                ProgressLine = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter
                };

                StatusGreen = new GUIStyle(EditorStyles.label);
                StatusGreen.normal.textColor = new Color(0.2f, 0.8f, 0.2f);

                StatusRed = new GUIStyle(EditorStyles.label);
                StatusRed.normal.textColor = new Color(0.8f, 0.2f, 0.2f);

                PortWarning = new GUIStyle(EditorStyles.miniLabel);
                PortWarning.normal.textColor = new Color(0.9f, 0.6f, 0.1f);

                MiniLabelGreen = new GUIStyle(EditorStyles.miniLabel);
                MiniLabelGreen.normal.textColor = new Color(0.2f, 0.8f, 0.2f);

                MiniLabelWarning = new GUIStyle(EditorStyles.miniLabel);
                MiniLabelWarning.normal.textColor = new Color(0.9f, 0.6f, 0.1f);

                MiniLabelGrey = new GUIStyle(EditorStyles.miniLabel);
                MiniLabelGrey.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

                ConfiguredBadge = new GUIStyle(EditorStyles.miniLabel);
                ConfiguredBadge.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                ConfiguredBadge.fontStyle = FontStyle.Bold;

                ClientBox = new GUIStyle(EditorStyles.helpBox);

                CheckMark = new GUIStyle(EditorStyles.miniLabel);

                ConfigFileStatus = new GUIStyle(EditorStyles.miniLabel);

                ResultLabel = new GUIStyle(EditorStyles.label);
            }
        }

        // --- Initialization ---

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += CheckFirstLaunch;
        }

        private static void CheckFirstLaunch()
        {
            bool setupCompleted = ProjectPrefs.GetBool(k_SetupCompletedKey, false);
            int setupVersion = ProjectPrefs.GetInt(k_SetupVersionKey, 0);

            if (!setupCompleted || setupVersion < k_CurrentSetupVersion)
            {
                if (IsSetupEffectivelyComplete())
                {
                    MarkSetupCompleteStatic();
                    return;
                }

                EditorApplication.delayCall += ShowWizard;
            }
        }

        private static bool IsSetupEffectivelyComplete()
        {
            if (!McpServer.IsRunning) return false;

            // Check .mcp.json
            var mcpJsonPath = ClientConfigManager.GetMcpJsonPath();
            if (File.Exists(mcpJsonPath))
            {
                try
                {
                    var content = File.ReadAllText(mcpJsonPath);
                    if (content.Contains(McpServer.Port.ToString()))
                        return true;
                }
                catch { }
            }

            // Check Claude Desktop
            var claudeDesktopPath = ClientConfigManager.GetClaudeDesktopConfigPath();
            if (File.Exists(claudeDesktopPath))
            {
                try
                {
                    var content = File.ReadAllText(claudeDesktopPath);
                    if (content.Contains(McpServer.ServerName)) return true;
                }
                catch { }
            }

            // Check Cursor
            var cursorPath = ClientConfigManager.GetCursorConfigPath();
            if (File.Exists(cursorPath))
            {
                try
                {
                    var content = File.ReadAllText(cursorPath);
                    if (content.Contains(McpServer.ServerName)) return true;
                }
                catch { }
            }

            return false;
        }

        private static void MarkSetupCompleteStatic()
        {
            // Existing users upgrading to v5+ get the bundled skills installed silently
            // (pre-existing or user-modified skill files are preserved — see SkillInstaller).
            try
            {
                SkillInstaller.InstallAll(dryRun: false, force: false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SetupWizard] Skill install on upgrade failed (non-fatal): {ex.Message}");
            }

            ProjectPrefs.SetBool(k_SetupCompletedKey, true);
            ProjectPrefs.SetInt(k_SetupVersionKey, k_CurrentSetupVersion);
            Debug.Log("[SetupWizard] Setup auto-detected as complete (skipping wizard)");
        }

        [MenuItem("Window/Unity MCP/Setup Wizard")]
        public static void ShowWizard()
        {
            var window = GetWindow<SetupWizard>(true, "Unity MCP Setup Wizard", true);
            window.minSize = new Vector2(550, 500);
            window.maxSize = new Vector2(550, 600);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            m_currentStep = 0;
            m_connectionTestResult = null;
            m_autoConfigExecuted = false;
            m_autoTestStarted = false;
            m_showManualConfig = false;
            m_fileCache = new Dictionary<string, (bool, string, double)>();
            RefreshClientStatuses();

            // Auto-start server if not running
            if (!McpServer.IsRunning)
            {
                McpServer.Start();
            }
        }

        private void OnFocus()
        {
            m_fileCache?.Clear();
            RefreshClientStatuses();
        }

        private void RefreshClientStatuses()
        {
            m_clientStatuses = ClientConfigManager.GetAllClientStatuses();
            m_skillStatuses = SkillInstaller.GetAllSkillStatuses();
        }

        // --- File I/O Cache ---

        private (bool exists, string content) CachedFileRead(string path)
        {
            if (m_fileCache == null)
                m_fileCache = new Dictionary<string, (bool, string, double)>();

            double now = EditorApplication.timeSinceStartup;

            if (m_fileCache.TryGetValue(path, out var cached) && (now - cached.checkedAt) < 2.0)
            {
                return (cached.exists, cached.content);
            }

            bool exists = File.Exists(path);
            string content = null;
            if (exists)
            {
                try { content = File.ReadAllText(path); } catch { }
            }

            m_fileCache[path] = (exists, content, now);
            return (exists, content);
        }

        private void InvalidateFileCache(string path)
        {
            m_fileCache?.Remove(path);
        }

        // --- OnGUI ---

        private void OnGUI()
        {
            Styles.EnsureInitialized();

            GUILayout.Space(10);
            DrawHeader();
            GUILayout.Space(10);
            DrawProgressBar();
            GUILayout.Space(20);

            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
            {
                switch (m_currentStep)
                {
                    case 0: DrawWelcomeStep(); break;
                    case 1: DrawConfigureStep(); break;
                    case 2: DrawCompleteStep(); break;
                }
            }
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            DrawNavigationButtons();
            GUILayout.Space(10);
        }

        // --- Header & Progress ---

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Unity MCP Setup Wizard", Styles.Header);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Step {m_currentStep + 1} of {s_StepTitles.Length}: {s_StepTitles[m_currentStep]}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProgressBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int i = 0; i < s_StepTitles.Length; i++)
            {
                if (i < m_currentStep)
                {
                    GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                    Styles.ProgressBox.normal.textColor = Color.white;
                    Styles.ProgressBox.fontSize = 0;
                }
                else if (i == m_currentStep)
                {
                    GUI.backgroundColor = new Color(0.0f, 0.8f, 1.0f);
                    Styles.ProgressBox.normal.textColor = Color.black;
                    Styles.ProgressBox.fontSize = 14;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                    Styles.ProgressBox.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                    Styles.ProgressBox.fontSize = 0;
                }

                GUILayout.Box((i + 1).ToString(), Styles.ProgressBox, GUILayout.Width(30), GUILayout.Height(30));
                GUI.backgroundColor = Color.white;

                if (i < s_StepTitles.Length - 1)
                {
                    var lineColor = i < m_currentStep ? new Color(0.2f, 0.8f, 0.2f) : Styles.ProgressLine.normal.textColor;
                    var prevColor = Styles.ProgressLine.normal.textColor;
                    Styles.ProgressLine.normal.textColor = lineColor;
                    GUILayout.Label("\u2014", Styles.ProgressLine, GUILayout.Width(15));
                    Styles.ProgressLine.normal.textColor = prevColor;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // --- Step 0: Welcome + Server Status ---

        private void DrawWelcomeStep()
        {
            EditorGUILayout.LabelField("Welcome to Unity MCP!", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Unity MCP allows AI assistants to interact with your Unity Editor.\n\n" +
                "Supported Clients:\n" +
                "\u2022 Claude Desktop (GUI application)\n" +
                "\u2022 Claude Code (CLI / Terminal)\n" +
                "\u2022 Cursor (AI-powered IDE)\n" +
                "\u2022 Any MCP-compatible client\n\n" +
                "This wizard will auto-detect and configure your installed clients.",
                MessageType.Info
            );

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Features:", EditorStyles.boldLabel);
            var features = new[]
            {
                "\u2022 Scene hierarchy exploration and manipulation",
                "\u2022 Dynamic C# code execution (Roslyn)",
                "\u2022 Screenshot capture",
                "\u2022 Input simulation (Playwright-style)",
                "\u2022 Performance analysis",
                "\u2022 Material diagnosis and repair",
                $"\u2022 And {ToolCategories.ToolBriefs.Count} tools in total!"
            };

            foreach (var feature in features)
            {
                EditorGUILayout.LabelField(feature);
            }

            GUILayout.Space(15);

            // Server Status
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Status:", GUILayout.Width(80));
                if (McpServer.IsRunning)
                {
                    GUILayout.Label("\u25cf Running", Styles.StatusGreen);
                }
                else
                {
                    GUILayout.Label("\u25cf Stopped", Styles.StatusRed);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Port:", GUILayout.Width(80));
                GUILayout.Label(McpServer.Port.ToString());

                if (McpServer.IsRunning && McpServer.Port != McpServer.RequestedPort)
                {
                    GUILayout.Label($"(configured: {McpServer.RequestedPort}, fallback)", Styles.PortWarning);
                }
                EditorGUILayout.EndHorizontal();

                if (McpServer.IsRunning)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("URL:", GUILayout.Width(80));
                    var url = $"http://localhost:{McpServer.Port}/sse";
                    if (GUILayout.Button(url, EditorStyles.linkLabel))
                    {
                        EditorGUIUtility.systemCopyBuffer = url;
                        Debug.Log($"[SetupWizard] Copied URL to clipboard: {url}");
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

            if (!McpServer.IsRunning)
            {
                GUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "The MCP server is not running. Click the button below to start it.",
                    MessageType.Warning
                );

                if (GUILayout.Button("Start Server", GUILayout.Height(35)))
                {
                    McpServer.Start();
                }
            }
            else
            {
                GUILayout.Space(10);
                DrawBackgroundExecutionSettings();
            }
        }

        private void DrawBackgroundExecutionSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Quick Setup", EditorStyles.miniBoldLabel);

                bool allOptimized = ConfigureEditorStartup.GetRunInBackground() && McpServer.GetAutoStart();

                if (allOptimized)
                {
                    EditorGUILayout.LabelField("\u2713 All recommended settings are applied", Styles.MiniLabelGreen);
                }
                else
                {
                    EditorGUILayout.LabelField("Apply all recommended settings for optimal MCP experience:", EditorStyles.miniLabel);

                    if (GUILayout.Button("Optimize for MCP (Recommended)", GUILayout.Height(28)))
                    {
                        ConfigureEditorStartup.SetRunInBackground(true);
                        PlayerSettings.runInBackground = true;
                        McpServer.SetAutoStart(true);

                        Debug.Log("[SetupWizard] All MCP optimizations applied (persistent)");
                        EditorUtility.DisplayDialog(
                            "Optimization Complete",
                            "All recommended settings have been applied:\n\n" +
                            "\u2713 Background execution enabled (persistent)\n" +
                            "\u2713 Auto-start on Unity launch enabled\n" +
                            "\u2713 PlayerSettings.runInBackground enabled",
                            "OK"
                        );
                    }
                }

                GUILayout.Space(3);

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Status:", GUILayout.Width(50));

                    Styles.CheckMark.normal.textColor = ConfigureEditorStartup.GetRunInBackground()
                        ? new Color(0.2f, 0.8f, 0.2f)
                        : new Color(0.8f, 0.4f, 0.4f);
                    GUILayout.Label(ConfigureEditorStartup.GetRunInBackground() ? "\u2713 BG" : "\u2717 BG",
                        Styles.CheckMark, GUILayout.Width(35));

                    Styles.CheckMark.normal.textColor = McpServer.GetAutoStart()
                        ? new Color(0.2f, 0.8f, 0.2f)
                        : new Color(0.8f, 0.4f, 0.4f);
                    GUILayout.Label(McpServer.GetAutoStart() ? "\u2713 Auto" : "\u2717 Auto",
                        Styles.CheckMark, GUILayout.Width(45));

                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        // --- Step 1: Auto Configure ---

        private void DrawConfigureStep()
        {
            EditorGUILayout.LabelField("Configure Clients", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Detected clients are shown below. Click 'Configure All' to set up everything automatically.",
                MessageType.Info
            );

            GUILayout.Space(10);

            // Auto-detect results table
            EditorGUILayout.LabelField("Detected Clients:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // Table header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Client", EditorStyles.miniBoldLabel, GUILayout.Width(140));
                EditorGUILayout.LabelField("Status", EditorStyles.miniBoldLabel);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(3);

                if (m_clientStatuses != null)
                {
                    foreach (var status in m_clientStatuses)
                    {
                        DrawClientStatusRow(status);
                    }
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Configure All button
            if (!m_autoConfigExecuted)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("Configure All Detected Clients", GUILayout.Height(40)))
                {
                    m_autoConfigResults = ClientConfigManager.ConfigureAllDetected(false);
                    m_skillInstallReports = SkillInstaller.InstallAll(dryRun: false, force: false);
                    m_autoConfigExecuted = true;
                    RefreshClientStatuses();
                    m_fileCache?.Clear();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                // Show results
                EditorGUILayout.LabelField("Configuration Results:", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    foreach (var kvp in m_autoConfigResults)
                    {
                        string icon;
                        GUIStyle style;
                        switch (kvp.Value)
                        {
                            case ConfigResult.Configured:
                                icon = "\u2713";
                                style = Styles.MiniLabelGreen;
                                break;
                            case ConfigResult.AlreadyUpToDate:
                                icon = "\u2713";
                                style = Styles.MiniLabelGreen;
                                break;
                            case ConfigResult.NotInstalled:
                                icon = "-";
                                style = Styles.MiniLabelGrey;
                                break;
                            default:
                                icon = "\u2717";
                                style = Styles.MiniLabelWarning;
                                break;
                        }
                        EditorGUILayout.LabelField(
                            $"{icon} {ClientConfigManager.GetConfigResultMessage(kvp.Key, kvp.Value)}",
                            style
                        );
                    }
                }
                EditorGUILayout.EndVertical();

                GUILayout.Space(5);

                if (GUILayout.Button("Re-configure", GUILayout.Height(25)))
                {
                    m_autoConfigExecuted = false;
                    RefreshClientStatuses();
                    m_fileCache?.Clear();
                }
            }

            GUILayout.Space(15);

            // Bundled Skills section
            DrawSkillsSection();

            GUILayout.Space(15);

            // Manual config foldout
            m_showManualConfig = EditorGUILayout.Foldout(m_showManualConfig, "Manual Configuration", true);
            if (m_showManualConfig)
            {
                GUILayout.Space(5);

                DrawManualClaudeDesktopConfig();
                GUILayout.Space(10);
                DrawManualClaudeCodeConfig();
                GUILayout.Space(10);
                DrawManualCursorConfig();
                GUILayout.Space(10);
                DrawOtherConfig();
            }
        }

        private void DrawClientStatusRow(ClientStatus status)
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField(ClientConfigManager.GetClientDisplayName(status.Type),
                    GUILayout.Width(140));

                if (!status.IsInstalled)
                {
                    GUILayout.Label("- Not Found", Styles.MiniLabelGrey);
                }
                else if (status.IsConfigured && status.HasCorrectPort)
                {
                    GUILayout.Label("\u2713 Configured", Styles.MiniLabelGreen);
                }
                else if (status.IsConfigured)
                {
                    GUILayout.Label("\u26a0 Port mismatch", Styles.MiniLabelWarning);
                }
                else
                {
                    GUILayout.Label("\u2713 Installed", Styles.MiniLabelGreen);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSkillsSection()
        {
            EditorGUILayout.LabelField("Bundled Skills (Claude Code)", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Installs workflow skills into .claude/skills/. These let Claude chain MCP tools for common Unity tasks.",
                MessageType.None
            );

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                if (m_skillStatuses == null || m_skillStatuses.Length == 0)
                {
                    EditorGUILayout.LabelField("No bundled skills found.", Styles.MiniLabelGrey);
                }
                else
                {
                    foreach (var s in m_skillStatuses)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(s.Name, GUILayout.Width(240));

                        string icon;
                        GUIStyle style;
                        if (!s.IsInstalled)
                        {
                            icon = "- Not installed"; style = Styles.MiniLabelGrey;
                        }
                        else if (s.IsUserModified)
                        {
                            icon = "\u270e User-modified"; style = Styles.MiniLabelWarning;
                        }
                        else if (!s.IsUpToDate)
                        {
                            icon = "\u26a0 Outdated"; style = Styles.MiniLabelWarning;
                        }
                        else
                        {
                            icon = "\u2713 Installed"; style = Styles.MiniLabelGreen;
                        }
                        GUILayout.Label(icon, style);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            EditorGUILayout.EndVertical();

            if (m_skillInstallReports != null && m_skillInstallReports.Length > 0)
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField(SkillInstaller.GetInstallSummary(m_skillInstallReports), Styles.MiniLabelGreen);
            }

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Install / Update Skills", GUILayout.Height(22)))
            {
                m_skillInstallReports = SkillInstaller.InstallAll(dryRun: false, force: false);
                RefreshClientStatuses();
            }
            GUI.enabled = m_skillStatuses != null && m_skillStatuses.Any(s => s.IsUserModified);
            if (GUILayout.Button("Force Overwrite (user-modified)", GUILayout.Height(22)))
            {
                if (UnityEditor.EditorUtility.DisplayDialog(
                    "Force overwrite user-modified skills?",
                    "This overwrites locally edited skill files. A .bak backup is created first. Continue?",
                    "Overwrite", "Cancel"))
                {
                    m_skillInstallReports = SkillInstaller.InstallAll(dryRun: false, force: true);
                    RefreshClientStatuses();
                }
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // --- Manual Config Sections ---

        private void DrawManualClaudeDesktopConfig()
        {
            var configPath = ClientConfigManager.GetClaudeDesktopConfigPath();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Claude Desktop", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("Config file:", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(configPath, EditorStyles.textField, GUILayout.Height(20));

                DrawConfigFileStatus(configPath);

                GUILayout.Space(5);

                var config = ClientConfigManager.GenerateServerConfig();
                EditorGUILayout.TextArea(config, GUILayout.Height(80));

                DrawConfigButtons(config, configPath, "mcpServers", McpServer.ServerName);

                GUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "After saving, restart Claude Desktop for changes to take effect.",
                    MessageType.Info
                );
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawManualClaudeCodeConfig()
        {
            var mcpJsonPath = ClientConfigManager.GetMcpJsonPath();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Claude Code (CLI)", EditorStyles.boldLabel);

                var (mcpJsonExists, _) = CachedFileRead(mcpJsonPath);

                if (mcpJsonExists)
                {
                    EditorGUILayout.LabelField("\u2713 .mcp.json exists (auto-generated by server)", Styles.MiniLabelGreen);
                }
                else
                {
                    EditorGUILayout.LabelField(".mcp.json will be created automatically when the server starts.", EditorStyles.miniLabel);
                }

                GUILayout.Space(5);

                var config = ClientConfigManager.GenerateServerConfig();
                EditorGUILayout.TextArea(config, GUILayout.Height(80));

                if (!mcpJsonExists)
                {
                    if (GUILayout.Button("Create .mcp.json Now", GUILayout.Height(25)))
                    {
                        CreateMcpJsonFile(mcpJsonPath, config);
                        InvalidateFileCache(mcpJsonPath);
                    }
                }

                GUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "The server auto-creates .mcp.json on startup. Add .mcp.json to .gitignore.",
                    MessageType.Info
                );
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawManualCursorConfig()
        {
            var configPath = ClientConfigManager.GetCursorConfigPath();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Cursor", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("Config file (project-level):", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(configPath, EditorStyles.textField, GUILayout.Height(20));

                DrawConfigFileStatus(configPath);

                GUILayout.Space(5);

                var config = ClientConfigManager.GenerateServerConfig();
                EditorGUILayout.TextArea(config, GUILayout.Height(80));

                DrawConfigButtons(config, configPath, "mcpServers", McpServer.ServerName);

                GUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Open this project folder in Cursor to auto-detect the MCP config.",
                    MessageType.Info
                );
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawOtherConfig()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Other MCP Clients", EditorStyles.boldLabel);
                GUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Transport:", GUILayout.Width(100));
                GUILayout.Label("HTTP + SSE (Server-Sent Events)");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("SSE Endpoint:", GUILayout.Width(100));
                var sseUrl = $"http://localhost:{McpServer.Port}/sse";
                if (GUILayout.Button(sseUrl, EditorStyles.linkLabel))
                {
                    EditorGUIUtility.systemCopyBuffer = sseUrl;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Message POST:", GUILayout.Width(100));
                var msgUrl = $"http://localhost:{McpServer.Port}/message";
                if (GUILayout.Button(msgUrl, EditorStyles.linkLabel))
                {
                    EditorGUIUtility.systemCopyBuffer = msgUrl;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Health Check:", GUILayout.Width(100));
                var healthUrl = $"http://localhost:{McpServer.Port}/health";
                if (GUILayout.Button(healthUrl, EditorStyles.linkLabel))
                {
                    EditorGUIUtility.systemCopyBuffer = healthUrl;
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                var genericConfig = ClientConfigManager.GenerateServerConfig();
                EditorGUILayout.TextArea(genericConfig, GUILayout.Height(80));

                if (GUILayout.Button("Copy Configuration", GUILayout.Height(25)))
                {
                    EditorGUIUtility.systemCopyBuffer = genericConfig;
                    Debug.Log("[SetupWizard] Configuration copied to clipboard");
                }
            }
            EditorGUILayout.EndVertical();
        }

        // --- Step 2: Complete ---

        private void DrawCompleteStep()
        {
            // Auto-test on step entry
            if (!m_autoTestStarted)
            {
                m_autoTestStarted = true;
                if (McpServer.IsRunning)
                {
                    TestConnection();
                }
            }

            EditorGUILayout.LabelField("Setup Complete!", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Test result
            if (m_isTestingConnection)
            {
                EditorGUILayout.HelpBox("Testing connection...", MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(m_connectionTestResult))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    if (m_connectionTestSuccess)
                    {
                        Styles.ResultLabel.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                        EditorGUILayout.LabelField("\u2713 Connection Successful!", Styles.ResultLabel);
                    }
                    else
                    {
                        Styles.ResultLabel.normal.textColor = new Color(0.8f, 0.2f, 0.2f);
                        EditorGUILayout.LabelField("\u2717 Connection Failed", Styles.ResultLabel);
                    }

                    GUILayout.Space(3);
                    EditorGUILayout.LabelField("Details:", EditorStyles.miniLabel);
                    EditorGUILayout.TextArea(m_connectionTestResult, GUILayout.Height(50));
                }
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(10);

            // Configured clients
            if (m_clientStatuses != null)
            {
                EditorGUILayout.LabelField("Configured Clients:", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    foreach (var status in m_clientStatuses)
                    {
                        if (status.IsConfigured)
                        {
                            EditorGUILayout.LabelField(
                                $"\u2713 {ClientConfigManager.GetClientDisplayName(status.Type)}",
                                Styles.MiniLabelGreen
                            );
                        }
                    }
                }
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(10);

            // Quick reference
            EditorGUILayout.LabelField("Quick Reference:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("\u2022 MCP Server Window: Window > Unity MCP Server");
                EditorGUILayout.LabelField("\u2022 Re-run Setup: Window > Unity MCP > Setup Wizard");
                EditorGUILayout.LabelField($"\u2022 Server URL: http://localhost:{McpServer.Port}/sse");
                EditorGUILayout.LabelField($"\u2022 Available Tools: {McpServer.GetRegisteredTools().Length}");
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // .gitignore suggestion
            DrawGitignoreSuggestion();

            GUILayout.Space(10);

            // Example prompts
            EditorGUILayout.LabelField("Example Prompts:", EditorStyles.boldLabel);
            var examples = new[]
            {
                "\"Show me the scene hierarchy\"",
                "\"Create a sphere at position (0, 2, 0)\"",
                "\"Execute C#: Debug.Log(Application.unityVersion)\"",
                "\"Take a screenshot of the game view\"",
                "\"Find all objects with 'Player' in their name\"",
                "\"Analyze the frame performance\""
            };

            foreach (var example in examples)
            {
                EditorGUILayout.LabelField($"  \u2022 {example}");
            }
        }

        private void DrawGitignoreSuggestion()
        {
            var gitignorePath = Path.Combine(ClientConfigManager.GetProjectRoot(), ".gitignore");
            var (gitignoreExists, gitignoreContent) = CachedFileRead(gitignorePath);
            bool mcpJsonIgnored = gitignoreExists && gitignoreContent != null && gitignoreContent.Contains(".mcp.json");

            if (!mcpJsonIgnored)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    EditorGUILayout.LabelField("Recommended: Add .mcp.json to .gitignore", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        ".mcp.json contains localhost URLs with machine-specific ports.\nIt should not be committed to version control.",
                        EditorStyles.wordWrappedMiniLabel
                    );

                    if (GUILayout.Button("Add to .gitignore", GUILayout.Height(25)))
                    {
                        AddMcpJsonToGitignore(gitignorePath);
                        InvalidateFileCache(gitignorePath);
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }

        // --- Shared UI Components ---

        private void DrawConfigFileStatus(string path)
        {
            EditorGUILayout.BeginHorizontal();
            {
                var (exists, content) = CachedFileRead(path);

                if (exists)
                {
                    string statusText = "\u2713 Config file exists";

                    if (content != null)
                    {
                        try
                        {
                            var json = JObject.Parse(content);
                            var servers = json["mcpServers"] as JObject;
                            if (servers != null)
                            {
                                int count = servers.Count;
                                statusText += $" ({count} server{(count != 1 ? "s" : "")} configured)";
                            }

                            if (content.Contains($"\"{McpServer.ServerName}\""))
                            {
                                statusText += " - Unity MCP already present";
                            }
                        }
                        catch { }
                    }

                    GUILayout.Label(statusText, Styles.MiniLabelGreen);
                }
                else
                {
                    GUILayout.Label("\u25cb Config file will be created", Styles.MiniLabelGrey);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                {
                    var folder = Path.GetDirectoryName(path);
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }
                    EditorUtility.RevealInFinder(folder);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigButtons(string config, string path, string rootKey, string serverKey)
        {
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(28)))
                {
                    EditorGUIUtility.systemCopyBuffer = config;
                    Debug.Log("[SetupWizard] Config copied to clipboard");
                    EditorUtility.DisplayDialog("Copied", "Configuration copied to clipboard!", "OK");
                }

                var (fileExists, fileContent) = CachedFileRead(path);
                string buttonLabel;

                if (!fileExists)
                {
                    buttonLabel = "Create Config File";
                }
                else if (fileContent != null && fileContent.Contains($"\"{serverKey}\""))
                {
                    buttonLabel = "Update Server URL";
                }
                else
                {
                    buttonLabel = "Add Server to Config";
                }

                if (GUILayout.Button(buttonLabel, GUILayout.Height(28)))
                {
                    SaveConfigToFile(config, path, rootKey, serverKey);
                    InvalidateFileCache(path);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // --- Navigation ---

        private void DrawNavigationButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (m_currentStep == 0)
            {
                if (GUILayout.Button("Skip Setup", GUILayout.Width(100)))
                {
                    MarkSetupComplete();
                    Close();
                }
            }
            else
            {
                GUILayout.Space(100);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = m_currentStep > 0;
            if (GUILayout.Button("\u2190 Back", GUILayout.Width(80)))
            {
                m_currentStep--;
                if (m_currentStep == 1)
                {
                    m_autoTestStarted = false;
                }
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            if (m_currentStep < s_StepTitles.Length - 1)
            {
                bool canProceed = CanProceedToNextStep();
                GUI.enabled = canProceed;
                if (GUILayout.Button("Next \u2192", GUILayout.Width(80)))
                {
                    m_currentStep++;
                }
                GUI.enabled = true;
            }
            else
            {
                if (GUILayout.Button("Finish", GUILayout.Width(80)))
                {
                    MarkSetupComplete();
                    Close();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool CanProceedToNextStep()
        {
            switch (m_currentStep)
            {
                case 0: // Welcome + Server Status
                    return McpServer.IsRunning;
                default:
                    return true;
            }
        }

        // --- File Operations ---

        private void CreateMcpJsonFile(string path, string content)
        {
            try
            {
                bool fileExists = File.Exists(path);
                string finalContent = content;

                if (fileExists)
                {
                    var existingContent = File.ReadAllText(path);
                    finalContent = ClientConfigManager.MergeConfigJson(existingContent, "mcpServers", McpServer.ServerName);

                    if (finalContent == existingContent)
                    {
                        EditorUtility.DisplayDialog(
                            "No Changes Needed",
                            $".mcp.json already has the correct settings for {McpServer.ServerName}.",
                            "OK"
                        );
                        return;
                    }

                    if (!ShowBeforeAfterApproval(path, existingContent, finalContent, "Add Server to .mcp.json"))
                    {
                        return;
                    }

                    ClientConfigManager.CreateBackup(path);
                }

                File.WriteAllText(path, finalContent);
                ClientConfigManager.ConfigureClaudeCodePermissions();

                string actionDescription = fileExists ? "updated" : "created";
                EditorUtility.DisplayDialog(
                    fileExists ? "Updated" : "Created",
                    $".mcp.json file {actionDescription} at:\n{path}\n\n" +
                    (fileExists ? "Your existing settings have been preserved.\n\n" : "") +
                    "Auto-permissions configured for Unity MCP tools.\n\n" +
                    "Claude Code will detect this when you open this project folder.",
                    "OK"
                );
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"Failed to create .mcp.json:\n{ex.Message}",
                    "OK"
                );
            }
        }

        private void SaveConfigToFile(string config, string path, string rootKey, string serverKey)
        {
            try
            {
                var folder = Path.GetDirectoryName(path);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string beforeContent = "";
                string afterContent;
                bool fileExists = File.Exists(path);

                if (fileExists)
                {
                    beforeContent = File.ReadAllText(path);
                    afterContent = ClientConfigManager.MergeConfigJson(beforeContent, rootKey, serverKey);

                    if (afterContent == beforeContent)
                    {
                        EditorUtility.DisplayDialog(
                            "No Changes Needed",
                            $"The configuration already has the correct settings for {serverKey}.",
                            "OK"
                        );
                        return;
                    }

                    string dialogTitle;
                    if (beforeContent.Contains($"\"{serverKey}\""))
                        dialogTitle = "Update Server URL";
                    else if (beforeContent.Contains($"\"{rootKey}\""))
                        dialogTitle = "Add Server to Configuration";
                    else
                        dialogTitle = "Add MCP Configuration";

                    if (!ShowBeforeAfterApproval(path, beforeContent, afterContent, dialogTitle))
                    {
                        return;
                    }

                    ClientConfigManager.CreateBackup(path);
                }
                else
                {
                    afterContent = config;
                }

                File.WriteAllText(path, afterContent);

                string actionDescription = fileExists ? "merged into" : "created at";
                EditorUtility.DisplayDialog(
                    "Configuration Saved",
                    $"Configuration {actionDescription}:\n{path}\n\n" +
                    (fileExists ? "Your existing settings have been preserved.\n\n" : "") +
                    "Please restart your client for changes to take effect.",
                    "OK"
                );
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"Failed to save configuration:\n{ex.Message}\n\n" +
                    "Please copy the configuration manually.",
                    "OK"
                );
            }
        }

        private bool ShowBeforeAfterApproval(string path, string before, string after, string title)
        {
            var result = EditorUtility.DisplayDialogComplex(
                title,
                $"File: {Path.GetFileName(path)}\n\n" +
                $"\u2550\u2550\u2550 BEFORE \u2550\u2550\u2550\n{TruncateForDialog(before)}\n\n" +
                $"\u2550\u2550\u2550 AFTER \u2550\u2550\u2550\n{TruncateForDialog(after)}",
                "Apply Changes",
                "Cancel",
                "Open in Editor"
            );

            if (result == 0)
            {
                return true;
            }
            else if (result == 2)
            {
                EditorUtility.RevealInFinder(path);
                EditorUtility.DisplayDialog(
                    "Manual Edit",
                    "Please edit the configuration file manually.\n\n" +
                    "Copy the configuration from the wizard and paste it into the file.",
                    "OK"
                );
                return false;
            }

            return false;
        }

        private string TruncateForDialog(string content)
        {
            const int maxLength = 500;
            if (content.Length > maxLength)
            {
                return content.Substring(0, maxLength) + "\n... (truncated)";
            }
            return content;
        }

        private void AddMcpJsonToGitignore(string gitignorePath)
        {
            try
            {
                string entry = "\n# Unity MCP (machine-specific port)\n.mcp.json\n";
                File.AppendAllText(gitignorePath, entry);
                Debug.Log("[SetupWizard] Added .mcp.json to .gitignore");
                EditorUtility.DisplayDialog("Done", ".mcp.json has been added to .gitignore.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to update .gitignore:\n{ex.Message}", "OK");
            }
        }

        // --- Connection Test ---

        private async void TestConnection()
        {
            m_isTestingConnection = true;
            m_connectionTestResult = null;
            Repaint();

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var healthUrl = $"http://localhost:{McpServer.Port}/health";
                    var response = await client.GetStringAsync(healthUrl);

                    if (response.Contains("ok"))
                    {
                        m_connectionTestSuccess = true;
                        m_connectionTestResult = $"Health check: OK\n" +
                                                 $"Response: {response}\n" +
                                                 $"Server is ready to accept connections.";
                    }
                    else
                    {
                        m_connectionTestSuccess = false;
                        m_connectionTestResult = $"Unexpected response: {response}";
                    }
                }
            }
            catch (Exception ex)
            {
                m_connectionTestSuccess = false;
                m_connectionTestResult = $"Error: {ex.Message}\n\n" +
                                         "Make sure the MCP server is running.";
            }

            m_isTestingConnection = false;
            Repaint();
        }

        // --- Setup Lifecycle ---

        private void MarkSetupComplete()
        {
            ProjectPrefs.SetBool(k_SetupCompletedKey, true);
            ProjectPrefs.SetInt(k_SetupVersionKey, k_CurrentSetupVersion);
            ClientConfigManager.EnsureClaudeMdHasMcpSection();
            Debug.Log("[SetupWizard] Setup marked as complete for this project");
        }

        [MenuItem("Window/Unity MCP/Reset Setup Wizard")]
        public static void ResetSetup()
        {
            ProjectPrefs.DeleteKey(k_SetupCompletedKey);
            ProjectPrefs.DeleteKey(k_SetupVersionKey);
            Debug.Log("[SetupWizard] Setup has been reset for this project. Wizard will show on next domain reload.");
        }

        [MenuItem("Window/Unity MCP/Add CLAUDE.md Instructions")]
        public static void AddClaudeMdInstructions()
        {
            ClientConfigManager.EnsureClaudeMdHasMcpSection();
            EditorUtility.DisplayDialog("Done", "MCP troubleshooting section has been added to CLAUDE.md.", "OK");
        }
    }
}
