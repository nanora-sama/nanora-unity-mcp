using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("delete_game_object")]
    public class DeleteGameObjectTool : IMcpTool
    {
        public string Name => "delete_game_object";
        public string Description => "Deletes a GameObject (and all of its children) from the open scene, with Undo support. " +
            "object_path is resolved as a hierarchy path first, then as an exact name across all loaded objects (first match wins). " +
            "Returns an error when nothing matches. To delete several objects by selector, use destroy_objects.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""object_path"": {
                    ""type"": ""string"",
                    ""description"": ""Path to the GameObject to delete""
                }
            },
            ""required"": [""object_path""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                var objectPath = arguments?["object_path"]?.Value<string>();

                if (string.IsNullOrEmpty(objectPath))
                {
                    return McpToolResult.Error("object_path is required");
                }

                var target = GameObject.Find(objectPath);

                if (target == null)
                {
                    // Try finding by name in all objects
                    var allObjects = UnityCompatibility.FindObjectsByType<GameObject>();
                    foreach (var obj in allObjects)
                    {
                        if (obj.name == objectPath)
                        {
                            target = obj;
                            break;
                        }
                    }
                }

                if (target == null)
                {
                    return McpToolResult.Error($"GameObject not found: {objectPath}");
                }

                var name = target.name;
                var childCount = target.transform.childCount;

                // Use Undo.DestroyObjectImmediate for proper undo support
                Undo.DestroyObjectImmediate(target);

                var message = $"Deleted GameObject '{name}'";
                if (childCount > 0)
                {
                    message += $" (including {childCount} children)";
                }

                return McpToolResult.Success(message);
            });
        }
    }
}
