using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;

namespace NanoraUnityMCP.UI
{
    /// <summary>
    /// Editor window for MCP Server settings and status.
    /// </summary>
    public class McpServerWindow : EditorWindow
    {
        private const string k_ExecuteCSharpEnabledKey = "NanoraUnityMCP.ExecuteCSharpEnabled";
        private const string k_LogLevelKey = "NanoraUnityMCP.LogLevel";

        private static readonly string[] s_LogLevelNames = { "Error", "Warning", "Info", "Debug" };

        private int m_newPort;
        private bool m_autoStart;
        private bool m_autoGenerateMcpJson;
        private bool m_executeCSharpEnabled;
        private int m_logLevel;
        private Vector2 m_scrollPos;
        private Vector2 m_toolsScrollPos;

        [MenuItem("Window/Unity MCP Server")]
        public static void ShowWindow()
        {
            var window = GetWindow<McpServerWindow>("Unity MCP");
            window.minSize = new Vector2(300, 500);
        }

        private void OnEnable()
        {
            m_newPort = McpServer.Port;
            m_autoStart = McpServer.GetAutoStart();
            m_autoGenerateMcpJson = McpServer.GetAutoGenerateMcpJson();
            m_executeCSharpEnabled = EditorPrefs.GetBool(k_ExecuteCSharpEnabledKey, true);
            m_logLevel = EditorPrefs.GetInt(k_LogLevelKey, 2); // default: Info
            McpServer.OnServerStateChanged += OnServerStateChanged;
        }

        private void OnDisable()
        {
            McpServer.OnServerStateChanged -= OnServerStateChanged;
        }

        private void OnServerStateChanged(bool isRunning)
        {
            Repaint();
        }

        private void OnGUI()
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);

            GUILayout.Space(10);

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Unity MCP Server", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Status
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Status:", GUILayout.Width(60));

                var statusStyle = new GUIStyle(EditorStyles.label);
                if (McpServer.IsRunning)
                {
                    statusStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                    GUILayout.Label("● Running", statusStyle);
                }
                else
                {
                    statusStyle.normal.textColor = new Color(0.8f, 0.2f, 0.2f);
                    GUILayout.Label("● Stopped", statusStyle);
                }
                EditorGUILayout.EndHorizontal();

                if (McpServer.IsRunning)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("HTTP:", GUILayout.Width(60));
                    var url = $"http://127.0.0.1:{McpServer.Port}/mcp";
                    if (GUILayout.Button(url, EditorStyles.linkLabel))
                    {
                        EditorGUIUtility.systemCopyBuffer = url;
                        Debug.Log($"[UnityMCP] Copied URL to clipboard: {url}");
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.LabelField("Legacy SSE", $"http://localhost:{McpServer.Port}/sse");
                    if (GUILayout.Button("Copy Codex setup command"))
                        EditorGUIUtility.systemCopyBuffer = $"codex mcp add {McpServer.ServerName} --url {url}";
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Controls
            EditorGUILayout.BeginHorizontal();
            {
                GUI.enabled = !McpServer.IsRunning;
                if (GUILayout.Button("Start", GUILayout.Height(30)))
                {
                    McpServer.Start();
                }

                GUI.enabled = McpServer.IsRunning;
                if (GUILayout.Button("Stop", GUILayout.Height(30)))
                {
                    McpServer.Stop();
                }

                GUI.enabled = true;
                if (GUILayout.Button("Restart", GUILayout.Height(30)))
                {
                    McpServer.Restart();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);

            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // Auto-start
                EditorGUI.BeginChangeCheck();
                m_autoStart = EditorGUILayout.Toggle("Auto-start on load", m_autoStart);
                if (EditorGUI.EndChangeCheck())
                {
                    McpServer.SetAutoStart(m_autoStart);
                }

                // Auto-generate .mcp.json
                EditorGUI.BeginChangeCheck();
                m_autoGenerateMcpJson = EditorGUILayout.Toggle("Auto-generate .mcp.json", m_autoGenerateMcpJson);
                if (EditorGUI.EndChangeCheck())
                {
                    McpServer.SetAutoGenerateMcpJson(m_autoGenerateMcpJson);
                }

                // Port
                EditorGUILayout.BeginHorizontal();
                m_newPort = EditorGUILayout.IntField("Port", m_newPort);
                if (m_newPort != McpServer.Port)
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    {
                        McpServer.SetPort(m_newPort);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Security
            EditorGUILayout.LabelField("Security", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUI.BeginChangeCheck();
                m_executeCSharpEnabled = EditorGUILayout.Toggle("Enable execute_csharp", m_executeCSharpEnabled);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(k_ExecuteCSharpEnabledKey, m_executeCSharpEnabled);
                }
                EditorGUILayout.HelpBox(
                    "When disabled, the execute_csharp tool will reject all code execution requests. " +
                    "Disable this if you want to restrict AI agents from running arbitrary code.",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Log Level
            EditorGUILayout.LabelField("Log Level", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUI.BeginChangeCheck();
                m_logLevel = EditorGUILayout.Popup("Level", m_logLevel, s_LogLevelNames);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt(k_LogLevelKey, m_logLevel);
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Registered Tools
            EditorGUILayout.LabelField("Registered Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                var tools = McpServer.GetRegisteredTools();
                if (tools.Length == 0)
                {
                    GUILayout.Label("No tools registered (server not running)", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    m_toolsScrollPos = EditorGUILayout.BeginScrollView(m_toolsScrollPos, GUILayout.Height(150));
                    foreach (var tool in tools)
                    {
                        EditorGUILayout.LabelField($"  {tool}");
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // Claude Desktop Config
            EditorGUILayout.LabelField("Claude Desktop Config", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                var config = $@"{{
  ""mcpServers"": {{
    ""{McpServer.ServerName}"": {{
      ""url"": ""http://localhost:{McpServer.Port}/sse""
    }}
  }}
}}";
                EditorGUILayout.TextArea(config, GUILayout.Height(80));
                if (GUILayout.Button("Copy to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = config;
                    Debug.Log("[UnityMCP] Config copied to clipboard");
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(15);

            // About
            EditorGUILayout.LabelField("About", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Nanora Unity MCP", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Version", "1.0.0");
                EditorGUILayout.LabelField("Unity", Application.unityVersion);
                GUILayout.Space(5);
                if (GUILayout.Button("Documentation (GitHub)"))
                {
                    Application.OpenURL("https://github.com/nanora-sama/nanora-unity-mcp");
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.EndScrollView();
        }
    }
}
