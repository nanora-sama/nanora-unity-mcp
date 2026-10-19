using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Batch modification of GameObjects using U-Selector and property expressions.
    /// Supports natural language-like bulk operations on scene objects.
    /// </summary>
    [McpTool("batch_modify")]
    public class BatchModifyTool : IMcpTool
    {
        public string Name => "batch_modify";
        public string Description => "Batch modify multiple GameObjects matching a selector. " +
            "Supports modifying transform, components, and properties. All changes are undoable.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector to match target objects (e.g., '.Enemy', '#Player', '[tag=Pickup]')""
                },
                ""modifications"": {
                    ""type"": ""array"",
                    ""description"": ""List of modifications to apply"",
                    ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""target"": {
                                ""type"": ""string"",
                                ""description"": ""Component or property path (e.g., 'Transform.localScale', 'Renderer.material.color')""
                            },
                            ""operation"": {
                                ""type"": ""string"",
                                ""enum"": [""set"", ""add"", ""multiply"", ""toggle""],
                                ""default"": ""set""
                            },
                            ""value"": {
                                ""description"": ""Value to apply (format depends on target type)""
                            }
                        },
                        ""required"": [""target"", ""value""]
                    }
                },
                ""include_inactive"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include inactive GameObjects"",
                    ""default"": false
                },
                ""dry_run"": {
                    ""type"": ""boolean"",
                    ""description"": ""Preview changes without applying"",
                    ""default"": false
                }
            },
            ""required"": [""selector"", ""modifications""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string selector = arguments?["selector"]?.Value<string>();
                var modifications = arguments?["modifications"]?.ToObject<List<ModificationSpec>>();
                bool includeInactive = arguments?["include_inactive"]?.Value<bool>() ?? false;
                bool dryRun = arguments?["dry_run"]?.Value<bool>() ?? false;

                if (string.IsNullOrWhiteSpace(selector))
                {
                    return McpToolResult.Error("Selector is required");
                }

                if (modifications == null || modifications.Count == 0)
                {
                    return McpToolResult.Error("At least one modification is required");
                }

                try
                {
                    var targets = SelectorEngine.Query(selector, includeInactive);

                    if (targets.Count == 0)
                    {
                        return McpToolResult.Error($"No objects found matching selector: {selector}");
                    }

                    var result = ApplyModifications(targets, modifications, dryRun);
                    return McpToolResult.Success(result);
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Batch modification failed: {ex.Message}");
                }
            });
        }

        private string ApplyModifications(List<GameObject> targets, List<ModificationSpec> modifications, bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Batch Modification {(dryRun ? "(Dry Run)" : "Results")}");
            sb.AppendLine();
            sb.AppendLine($"**Targets:** {targets.Count} object(s)");
            sb.AppendLine($"**Modifications:** {modifications.Count}");
            sb.AppendLine();

            int successCount = 0;
            int errorCount = 0;
            var errors = new List<string>();

            // Create undo group
            if (!dryRun)
            {
                Undo.SetCurrentGroupName("Batch Modify");
            }

            foreach (var target in targets)
            {
                foreach (var mod in modifications)
                {
                    try
                    {
                        if (dryRun)
                        {
                            var preview = PreviewModification(target, mod);
                            sb.AppendLine($"- {target.name}: {mod.Target} {mod.Operation} {mod.Value} → {preview}");
                        }
                        else
                        {
                            ApplyModification(target, mod);
                        }
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"{target.name}.{mod.Target}: {ex.Message}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"## Summary");
            sb.AppendLine($"- Successful: {successCount}");
            sb.AppendLine($"- Errors: {errorCount}");

            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Errors");
                foreach (var error in errors.Take(10))
                {
                    sb.AppendLine($"- {error}");
                }
                if (errors.Count > 10)
                {
                    sb.AppendLine($"- ... and {errors.Count - 10} more");
                }
            }

            return sb.ToString();
        }

        private string PreviewModification(GameObject target, ModificationSpec mod)
        {
            var (component, property) = ResolvePropertyPath(target, mod.Target);
            if (property != null)
            {
                var currentValue = property.GetValue(component);
                return $"Current: {FormatValue(currentValue)}";
            }
            return "(property not found)";
        }

        private void ApplyModification(GameObject target, ModificationSpec mod)
        {
            var (component, property) = ResolvePropertyPath(target, mod.Target);

            if (property == null)
            {
                throw new Exception($"Property not found: {mod.Target}");
            }

            // Record for undo
            if (component is UnityEngine.Object unityObj)
            {
                Undo.RecordObject(unityObj, "Batch Modify Property");
            }

            var currentValue = property.GetValue(component);
            var newValue = CalculateNewValue(currentValue, mod.Value, mod.Operation, property.PropertyType);

            property.SetValue(component, newValue);

            // Mark dirty
            if (component is UnityEngine.Object obj)
            {
                EditorUtility.SetDirty(obj);
            }
        }

        private (object component, PropertyInfo property) ResolvePropertyPath(GameObject target, string path)
        {
            var parts = path.Split('.');
            if (parts.Length < 2)
            {
                throw new Exception($"Invalid property path: {path}. Expected format: Component.property");
            }

            // Get component
            string componentName = parts[0];
            Component component = null;

            // Handle special cases
            if (componentName.Equals("Transform", StringComparison.OrdinalIgnoreCase))
            {
                component = target.transform;
            }
            else if (componentName.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
            {
                // Handle GameObject properties directly
                return ResolveGameObjectProperty(target, parts);
            }
            else
            {
                // Try to find component by type name
                var components = target.GetComponents<Component>();
                component = components.FirstOrDefault(c =>
                    c.GetType().Name.Equals(componentName, StringComparison.OrdinalIgnoreCase));
            }

            if (component == null)
            {
                throw new Exception($"Component not found: {componentName}");
            }

            // Traverse property path
            object current = component;
            PropertyInfo property = null;

            for (int i = 1; i < parts.Length; i++)
            {
                var type = current.GetType();
                property = type.GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property == null)
                {
                    throw new Exception($"Property not found: {parts[i]} on {type.Name}");
                }

                if (i < parts.Length - 1)
                {
                    current = property.GetValue(current);
                    if (current == null)
                    {
                        throw new Exception($"Null reference at {parts[i]}");
                    }
                }
            }

            return (current, property);
        }

        private (object component, PropertyInfo property) ResolveGameObjectProperty(GameObject target, string[] parts)
        {
            if (parts.Length != 2)
            {
                throw new Exception("Only direct GameObject properties are supported");
            }

            var property = typeof(GameObject).GetProperty(parts[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return (target, property);
        }

        private object CalculateNewValue(object currentValue, JToken newValueToken, string operation, Type targetType)
        {
            // Handle common types
            if (targetType == typeof(float))
            {
                float current = currentValue != null ? (float)currentValue : 0;
                float newVal = newValueToken.Value<float>();
                return operation switch
                {
                    "add" => current + newVal,
                    "multiply" => current * newVal,
                    _ => newVal
                };
            }

            if (targetType == typeof(int))
            {
                int current = currentValue != null ? (int)currentValue : 0;
                int newVal = newValueToken.Value<int>();
                return operation switch
                {
                    "add" => current + newVal,
                    "multiply" => current * newVal,
                    _ => newVal
                };
            }

            if (targetType == typeof(bool))
            {
                bool current = currentValue != null && (bool)currentValue;
                return operation switch
                {
                    "toggle" => !current,
                    _ => newValueToken.Value<bool>()
                };
            }

            if (targetType == typeof(string))
            {
                return newValueToken.Value<string>();
            }

            if (targetType == typeof(Vector3))
            {
                Vector3 current = currentValue != null ? (Vector3)currentValue : Vector3.zero;
                Vector3 newVal = ParseVector3(newValueToken);
                return operation switch
                {
                    "add" => current + newVal,
                    "multiply" => Vector3.Scale(current, newVal),
                    _ => newVal
                };
            }

            if (targetType == typeof(Vector2))
            {
                Vector2 current = currentValue != null ? (Vector2)currentValue : Vector2.zero;
                Vector2 newVal = ParseVector2(newValueToken);
                return operation switch
                {
                    "add" => current + newVal,
                    "multiply" => Vector2.Scale(current, newVal),
                    _ => newVal
                };
            }

            if (targetType == typeof(Color))
            {
                Color newVal = ParseColor(newValueToken);
                return newVal;
            }

            if (targetType == typeof(Quaternion))
            {
                if (operation == "set")
                {
                    Vector3 euler = ParseVector3(newValueToken);
                    return Quaternion.Euler(euler);
                }
                throw new Exception("Quaternion only supports 'set' operation with Euler angles");
            }

            // For enums
            if (targetType.IsEnum)
            {
                string enumValue = newValueToken.Value<string>();
                return Enum.Parse(targetType, enumValue, true);
            }

            throw new Exception($"Unsupported property type: {targetType.Name}");
        }

        private Vector3 ParseVector3(JToken token)
        {
            if (token is JObject obj)
            {
                return new Vector3(
                    obj["x"]?.Value<float>() ?? 0,
                    obj["y"]?.Value<float>() ?? 0,
                    obj["z"]?.Value<float>() ?? 0
                );
            }

            if (token is JArray arr && arr.Count >= 3)
            {
                return new Vector3(
                    arr[0].Value<float>(),
                    arr[1].Value<float>(),
                    arr[2].Value<float>()
                );
            }

            // Uniform value
            float uniform = token.Value<float>();
            return new Vector3(uniform, uniform, uniform);
        }

        private Vector2 ParseVector2(JToken token)
        {
            if (token is JObject obj)
            {
                return new Vector2(
                    obj["x"]?.Value<float>() ?? 0,
                    obj["y"]?.Value<float>() ?? 0
                );
            }

            if (token is JArray arr && arr.Count >= 2)
            {
                return new Vector2(
                    arr[0].Value<float>(),
                    arr[1].Value<float>()
                );
            }

            float uniform = token.Value<float>();
            return new Vector2(uniform, uniform);
        }

        private Color ParseColor(JToken token)
        {
            if (token is JObject obj)
            {
                return new Color(
                    obj["r"]?.Value<float>() ?? 1,
                    obj["g"]?.Value<float>() ?? 1,
                    obj["b"]?.Value<float>() ?? 1,
                    obj["a"]?.Value<float>() ?? 1
                );
            }

            if (token is JArray arr)
            {
                return new Color(
                    arr.Count > 0 ? arr[0].Value<float>() : 1,
                    arr.Count > 1 ? arr[1].Value<float>() : 1,
                    arr.Count > 2 ? arr[2].Value<float>() : 1,
                    arr.Count > 3 ? arr[3].Value<float>() : 1
                );
            }

            // Try parsing hex color
            string hexStr = token.Value<string>();
            if (!string.IsNullOrEmpty(hexStr))
            {
                if (ColorUtility.TryParseHtmlString(hexStr, out Color color))
                {
                    return color;
                }
            }

            return Color.white;
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is Vector3 v3) return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";
            if (value is Vector2 v2) return $"({v2.x:F2}, {v2.y:F2})";
            if (value is Color c) return $"RGBA({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";
            if (value is Quaternion q) return $"Euler{q.eulerAngles}";
            return value.ToString();
        }

        private class ModificationSpec
        {
            public string Target { get; set; }
            public string Operation { get; set; } = "set";
            public JToken Value { get; set; }
        }
    }

    /// <summary>
    /// Sets the active state of GameObjects matching a selector.
    /// </summary>
    [McpTool("set_active")]
    public class SetActiveTool : IMcpTool
    {
        public string Name => "set_active";
        public string Description => "Set the active state of GameObjects matching a selector.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector to match target objects""
                },
                ""active"": {
                    ""type"": ""boolean"",
                    ""description"": ""Active state to set""
                },
                ""include_inactive"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include inactive objects in search"",
                    ""default"": true
                }
            },
            ""required"": [""selector"", ""active""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string selector = arguments?["selector"]?.Value<string>();
                bool active = arguments?["active"]?.Value<bool>() ?? true;
                bool includeInactive = arguments?["include_inactive"]?.Value<bool>() ?? true;

                if (string.IsNullOrWhiteSpace(selector))
                {
                    return McpToolResult.Error("Selector is required");
                }

                try
                {
                    var targets = SelectorEngine.Query(selector, includeInactive);

                    if (targets.Count == 0)
                    {
                        return McpToolResult.Error($"No objects found matching selector: {selector}");
                    }

                    Undo.SetCurrentGroupName($"Set Active ({(active ? "On" : "Off")})");

                    foreach (var target in targets)
                    {
                        Undo.RecordObject(target, "Set Active");
                        target.SetActive(active);
                    }

                    return McpToolResult.Success(
                        $"Set {targets.Count} object(s) to {(active ? "active" : "inactive")}:\n" +
                        string.Join("\n", targets.Take(10).Select(t => $"- {t.name}")) +
                        (targets.Count > 10 ? $"\n... and {targets.Count - 10} more" : ""));
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Set active failed: {ex.Message}");
                }
            });
        }
    }

    /// <summary>
    /// Destroys GameObjects matching a selector.
    /// </summary>
    [McpTool("destroy_objects")]
    public class DestroyObjectsTool : IMcpTool
    {
        public string Name => "destroy_objects";
        public string Description => "Destroy multiple GameObjects matching a selector. Supports undo.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector to match target objects""
                },
                ""include_inactive"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include inactive objects"",
                    ""default"": false
                },
                ""confirm"": {
                    ""type"": ""boolean"",
                    ""description"": ""Confirm destruction (must be true to proceed)"",
                    ""default"": false
                }
            },
            ""required"": [""selector"", ""confirm""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string selector = arguments?["selector"]?.Value<string>();
                bool includeInactive = arguments?["include_inactive"]?.Value<bool>() ?? false;
                bool confirm = arguments?["confirm"]?.Value<bool>() ?? false;

                if (string.IsNullOrWhiteSpace(selector))
                {
                    return McpToolResult.Error("Selector is required");
                }

                if (!confirm)
                {
                    return McpToolResult.Error("Destruction not confirmed. Set 'confirm' to true to proceed.");
                }

                try
                {
                    var targets = SelectorEngine.Query(selector, includeInactive);

                    if (targets.Count == 0)
                    {
                        return McpToolResult.Error($"No objects found matching selector: {selector}");
                    }

                    // Create list of names before destruction
                    var names = targets.Select(t => t.name).ToList();

                    Undo.SetCurrentGroupName("Destroy Objects");

                    foreach (var target in targets)
                    {
                        Undo.DestroyObjectImmediate(target);
                    }

                    return McpToolResult.Success(
                        $"Destroyed {names.Count} object(s):\n" +
                        string.Join("\n", names.Take(10).Select(n => $"- {n}")) +
                        (names.Count > 10 ? $"\n... and {names.Count - 10} more" : ""));
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Destruction failed: {ex.Message}");
                }
            });
        }
    }
}
