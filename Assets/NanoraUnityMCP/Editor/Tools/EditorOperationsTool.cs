using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Editor operations tool for undo/redo, selection management, and undo history.
    /// Provides programmatic access to core Unity Editor operations.
    /// </summary>
    [McpTool("editor_operations")]
    public class EditorOperationsTool : IMcpTool
    {
        public string Name => "editor_operations";
        public string Description => "Perform editor operations: undo, redo, get/set selection, and view undo history";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""undo"", ""redo"", ""get_selection"", ""set_selection"", ""get_undo_history""],
                    ""description"": ""Action to perform""
                },
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector expression for set_selection (e.g. #MyObject, .Camera, /Canvas/Button)""
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var action = arguments?["action"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(action))
            {
                return Task.FromResult(McpToolResult.Error("'action' is required"));
            }

            switch (action)
            {
                case "undo":
                    return HandleUndo();
                case "redo":
                    return HandleRedo();
                case "get_selection":
                    return HandleGetSelection();
                case "set_selection":
                    return HandleSetSelection(arguments);
                case "get_undo_history":
                    return HandleGetUndoHistory();
                default:
                    return Task.FromResult(McpToolResult.Error(
                        $"Unknown action '{action}'. Use: undo, redo, get_selection, set_selection, get_undo_history"));
            }
        }

        #region Actions

        private Task<McpToolResult> HandleUndo()
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                Undo.PerformUndo();
                return McpToolResult.Success("Undo performed successfully");
            });
        }

        private Task<McpToolResult> HandleRedo()
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                Undo.PerformRedo();
                return McpToolResult.Success("Redo performed successfully");
            });
        }

        private Task<McpToolResult> HandleGetSelection()
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                var active = Selection.activeGameObject;
                var selected = Selection.gameObjects;

                if (active == null && (selected == null || selected.Length == 0))
                {
                    return McpToolResult.Success("No objects selected");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Active: {(active != null ? GetGameObjectPath(active) : "(none)")}");
                sb.AppendLine($"Selected ({selected?.Length ?? 0}):");

                if (selected != null)
                {
                    foreach (var go in selected)
                    {
                        if (go == null) continue;
                        var marker = go == active ? " [active]" : "";
                        sb.AppendLine($"  {GetGameObjectPath(go)}{marker}");
                    }
                }

                return McpToolResult.Success(sb.ToString());
            });
        }

        private Task<McpToolResult> HandleSetSelection(JObject arguments)
        {
            var selector = arguments?["selector"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(selector))
            {
                return Task.FromResult(McpToolResult.Error("'selector' is required for set_selection"));
            }

            return MainThreadDispatcher.RunAsync(() =>
            {
                var results = SelectorEngine.Query(selector, includeInactive: true);

                if (results == null || results.Count == 0)
                {
                    return McpToolResult.Error($"No objects found matching selector '{selector}'");
                }

                Selection.objects = results.Cast<Object>().ToArray();
                Selection.activeGameObject = results[0];

                var names = string.Join(", ", results.Select(go => go.name));
                return McpToolResult.Success(
                    $"Selected {results.Count} object(s): {names}");
            });
        }

        private Task<McpToolResult> HandleGetUndoHistory()
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                // Unity does not expose full undo history programmatically,
                // but we can get the current undo group name and group index.
                var currentGroup = Undo.GetCurrentGroupName();
                var currentGroupIndex = Undo.GetCurrentGroup();

                var sb = new StringBuilder();
                sb.AppendLine("Undo History (limited info available from Unity API):");
                sb.AppendLine($"  Current group: {currentGroupIndex}");
                sb.AppendLine($"  Current group name: {(string.IsNullOrEmpty(currentGroup) ? "(unnamed)" : currentGroup)}");

                // Selection undo info
                var active = Selection.activeGameObject;
                if (active != null)
                {
                    sb.AppendLine($"  Active selection: {GetGameObjectPath(active)}");
                }

                sb.AppendLine("\nNote: Unity does not expose the full undo stack. " +
                    "Use undo/redo actions to navigate, or execute_csharp for advanced undo inspection.");

                return McpToolResult.Success(sb.ToString());
            });
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Gets the full hierarchy path for a GameObject.
        /// </summary>
        internal static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "(null)";

            var parts = new List<string>();
            var current = go.transform;
            while (current != null)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return "/" + string.Join("/", parts);
        }

        #endregion
    }
}
