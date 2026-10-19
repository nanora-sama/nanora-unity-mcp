using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("modify_game_object")]
    public class ModifyGameObjectTool : IMcpTool
    {
        public string Name => "modify_game_object";
        public string Description => "Modifies one existing GameObject: name, active state, local position/rotation/scale, parent, layer, or tag. " +
            "object_path is a hierarchy path or a plain name; only active GameObjects are found (use query_objects with include_inactive for inactive ones). " +
            "Only the fields you pass are changed. An unknown layer, tag, or parent does not fail the call - it is reported inline in the returned change list, so read it. " +
            "For many objects at once use batch_modify; for component fields use batch_modify or execute_csharp.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""object_path"": {
                    ""type"": ""string"",
                    ""description"": ""Path to the GameObject to modify""
                },
                ""new_name"": {
                    ""type"": ""string"",
                    ""description"": ""New name for the GameObject""
                },
                ""active"": {
                    ""type"": ""boolean"",
                    ""description"": ""Set active state""
                },
                ""position"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    },
                    ""description"": ""New local position""
                },
                ""rotation"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    },
                    ""description"": ""New local rotation in euler angles""
                },
                ""scale"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    },
                    ""description"": ""New local scale""
                },
                ""new_parent_path"": {
                    ""type"": ""string"",
                    ""description"": ""Path to new parent GameObject (empty string to unparent)""
                },
                ""layer"": {
                    ""type"": ""string"",
                    ""description"": ""Layer name to set""
                },
                ""tag"": {
                    ""type"": ""string"",
                    ""description"": ""Tag to set""
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

                // Record undo
                Undo.RecordObject(target, $"Modify {target.name}");
                Undo.RecordObject(target.transform, $"Modify {target.name} Transform");

                var changes = new StringBuilder();
                changes.AppendLine($"Modified GameObject: {target.name}");

                // Apply modifications
                if (arguments?["new_name"] != null)
                {
                    var newName = arguments["new_name"].Value<string>();
                    target.name = newName;
                    changes.AppendLine($"  Name -> {newName}");
                }

                if (arguments?["active"] != null)
                {
                    var active = arguments["active"].Value<bool>();
                    target.SetActive(active);
                    changes.AppendLine($"  Active -> {active}");
                }

                if (arguments?["position"] is JObject posObj)
                {
                    var pos = ParseVector3(posObj, target.transform.localPosition);
                    target.transform.localPosition = pos;
                    changes.AppendLine($"  Position -> {pos}");
                }

                if (arguments?["rotation"] is JObject rotObj)
                {
                    var rot = ParseVector3(rotObj, target.transform.localEulerAngles);
                    target.transform.localEulerAngles = rot;
                    changes.AppendLine($"  Rotation -> {rot}");
                }

                if (arguments?["scale"] is JObject scaleObj)
                {
                    var scale = ParseVector3(scaleObj, target.transform.localScale);
                    target.transform.localScale = scale;
                    changes.AppendLine($"  Scale -> {scale}");
                }

                if (arguments?["new_parent_path"] != null)
                {
                    var parentPath = arguments["new_parent_path"].Value<string>();
                    if (string.IsNullOrEmpty(parentPath))
                    {
                        target.transform.SetParent(null);
                        changes.AppendLine("  Parent -> (root)");
                    }
                    else
                    {
                        var parent = GameObject.Find(parentPath);
                        if (parent != null)
                        {
                            target.transform.SetParent(parent.transform, true);
                            changes.AppendLine($"  Parent -> {parent.name}");
                        }
                        else
                        {
                            changes.AppendLine($"  Parent -> (not found: {parentPath})");
                        }
                    }
                }

                if (arguments?["layer"] != null)
                {
                    var layerName = arguments["layer"].Value<string>();
                    var layer = LayerMask.NameToLayer(layerName);
                    if (layer != -1)
                    {
                        target.layer = layer;
                        changes.AppendLine($"  Layer -> {layerName}");
                    }
                    else
                    {
                        changes.AppendLine($"  Layer -> (invalid: {layerName})");
                    }
                }

                if (arguments?["tag"] != null)
                {
                    var tag = arguments["tag"].Value<string>();
                    try
                    {
                        target.tag = tag;
                        changes.AppendLine($"  Tag -> {tag}");
                    }
                    catch
                    {
                        changes.AppendLine($"  Tag -> (invalid: {tag})");
                    }
                }

                EditorUtility.SetDirty(target);

                return McpToolResult.Success(changes.ToString());
            });
        }

        private Vector3 ParseVector3(JObject obj, Vector3 defaultValue)
        {
            if (obj == null) return defaultValue;

            float x = obj["x"]?.Value<float>() ?? defaultValue.x;
            float y = obj["y"]?.Value<float>() ?? defaultValue.y;
            float z = obj["z"]?.Value<float>() ?? defaultValue.z;

            return new Vector3(x, y, z);
        }
    }
}
