using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("get_object_details")]
    public class GetObjectDetailsTool : IMcpTool
    {
        public string Name => "get_object_details";
        public string Description => "Gets details of one active GameObject: active state, layer, tag, static flag, world and local transform, parent, child count, " +
            "and the type names of all components (missing scripts are marked). " +
            "Only a few component types add fields: MeshRenderer materials, MeshFilter mesh, Light, Camera, Collider, and Rigidbody basics. " +
            "It does not dump serialized properties of other components; read those with execute_csharp.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""object_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the GameObject to inspect (provide either this or object_path)""
                },
                ""object_path"": {
                    ""type"": ""string"",
                    ""description"": ""Full path to the GameObject (e.g., 'Parent/Child/Target'). Takes precedence over object_name""
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                GameObject target = null;

                var objectPath = arguments?["object_path"]?.Value<string>();
                var objectName = arguments?["object_name"]?.Value<string>();

                if (!string.IsNullOrEmpty(objectPath))
                {
                    target = GameObject.Find(objectPath);
                }
                else if (!string.IsNullOrEmpty(objectName))
                {
                    target = GameObject.Find(objectName);

                    // Try to find by name if path didn't work
                    if (target == null)
                    {
                        var allObjects = UnityCompatibility.FindObjectsByType<GameObject>();
                        foreach (var obj in allObjects)
                        {
                            if (obj.name == objectName)
                            {
                                target = obj;
                                break;
                            }
                        }
                    }
                }

                if (target == null)
                {
                    return McpToolResult.Error("GameObject not found");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"GameObject: {target.name}");
                sb.AppendLine($"Active: {target.activeSelf} (in hierarchy: {target.activeInHierarchy})");
                sb.AppendLine($"Layer: {LayerMask.LayerToName(target.layer)} ({target.layer})");
                sb.AppendLine($"Tag: {target.tag}");
                sb.AppendLine($"Static: {target.isStatic}");
                sb.AppendLine();

                // Transform
                var t = target.transform;
                sb.AppendLine("Transform:");
                sb.AppendLine($"  Position: {t.position}");
                sb.AppendLine($"  Local Position: {t.localPosition}");
                sb.AppendLine($"  Rotation: {t.eulerAngles}");
                sb.AppendLine($"  Local Rotation: {t.localEulerAngles}");
                sb.AppendLine($"  Scale: {t.localScale}");
                sb.AppendLine($"  Parent: {(t.parent != null ? t.parent.name : "none")}");
                sb.AppendLine($"  Child Count: {t.childCount}");
                sb.AppendLine();

                // Components with SerializedProperty inspection
                sb.AppendLine("Components:");
                var components = target.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        sb.AppendLine("  - [Missing Script]");
                        continue;
                    }

                    sb.AppendLine($"  - {comp.GetType().Name}");

                    // Use SerializedObject to expose all visible properties (Inspector-equivalent)
                    try
                    {
                        var so = new SerializedObject(comp);
                        var prop = so.GetIterator();
                        bool enterChildren = true;
                        while (prop.NextVisible(enterChildren))
                        {
                            enterChildren = false;
                            // Skip m_Script field (redundant with type name)
                            if (prop.name == "m_Script") continue;

                            sb.AppendLine($"      {prop.displayName}: {FormatSerializedProperty(prop)}");
                        }
                    }
                    catch
                    {
                        // Fallback if SerializedObject fails
                        sb.AppendLine("      (unable to read properties)");
                    }
                }

                return McpToolResult.Success(sb.ToString());
            });
        }

        private static string FormatSerializedProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("G");
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(prop.stringValue) ? "(empty)" : prop.stringValue;
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})"
                        : "(None)";
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();
                default:
                    return $"({prop.propertyType})";
            }
        }
    }
}
