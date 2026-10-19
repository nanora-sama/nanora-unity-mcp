using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;

namespace NanoraUnityMCP.UI
{
    /// <summary>
    /// Displays a small MCP connection status badge in the SceneView.
    /// Green when running, red when stopped. Click to open McpServerWindow.
    /// </summary>
    [InitializeOnLoad]
    public static class McpStatusOverlay
    {
        private const float k_BadgeWidth = 160f;
        private const float k_BadgeHeight = 22f;
        private const float k_Margin = 10f;

        static McpStatusOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Handles.BeginGUI();

            var rect = new Rect(
                k_Margin,
                sceneView.position.height - k_BadgeHeight - k_Margin - 20f,
                k_BadgeWidth,
                k_BadgeHeight);

            // Background
            var bgColor = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.2f, 0.2f, 0.85f)
                : new Color(0.75f, 0.75f, 0.75f, 0.85f);
            EditorGUI.DrawRect(rect, bgColor);

            // Border
            var borderColor = McpServer.IsRunning
                ? new Color(0.2f, 0.7f, 0.2f, 0.9f)
                : new Color(0.7f, 0.2f, 0.2f, 0.9f);
            DrawBorder(rect, borderColor);

            // Content
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                padding = new RectOffset(6, 6, 0, 0)
            };
            labelStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? Color.white
                : Color.black;

            string statusText;
            if (McpServer.IsRunning)
            {
                statusText = $"MCP Running :{McpServer.Port}";
            }
            else
            {
                statusText = "MCP Stopped";
            }

            // Dot indicator
            var dotRect = new Rect(rect.x + 6, rect.y + (rect.height - 8) / 2, 8, 8);
            var dotColor = McpServer.IsRunning
                ? new Color(0.3f, 0.9f, 0.3f)
                : new Color(0.9f, 0.3f, 0.3f);
            EditorGUI.DrawRect(dotRect, dotColor);

            // Label (offset for dot)
            var labelRect = new Rect(rect.x + 18, rect.y, rect.width - 18, rect.height);

            // Make entire badge clickable
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                McpServerWindow.ShowWindow();
            }

            // Draw label on top of the button
            GUI.Label(labelRect, statusText, labelStyle);

            // Change cursor to hand on hover
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            Handles.EndGUI();
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            // Left
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            // Right
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }
    }
}
