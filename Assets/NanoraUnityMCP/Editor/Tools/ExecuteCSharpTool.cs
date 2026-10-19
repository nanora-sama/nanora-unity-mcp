using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Executes arbitrary C# code using Roslyn/Mono.CSharp compilation.
    /// Provides AI with the ability to run dynamic scripts for complex operations.
    /// </summary>
    [McpTool("execute_csharp")]
    public class ExecuteCSharpTool : IMcpTool
    {
        private const int k_DefaultTimeoutMs = 5000;

        public string Name => "execute_csharp";
        public string Description => "Execute C# code dynamically in the Unity Editor context. " +
            "The code has access to UnityEngine, UnityEditor, and project assemblies. " +
            "Use 'return value;' to return a result.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""code"": {
                    ""type"": ""string"",
                    ""description"": ""C# statements or a full script. Include statement semicolons and use 'return value;' to return results. Compiler and runtime failures are returned as errors without retrying the script.""
                },
                ""timeout_ms"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum execution time in milliseconds"",
                    ""default"": 5000
                },
                ""include_usings"": {
                    ""type"": ""array"",
                    ""items"": { ""type"": ""string"" },
                    ""description"": ""Additional using statements to include"",
                    ""default"": []
                },
                ""force_roslyn"": {
                    ""type"": ""boolean"",
                    ""description"": ""Force Roslyn compilation mode, disable fallback to reflection. Use for multi-statement code with var declarations."",
                    ""default"": false
                }
            },
            ""required"": [""code""]
        }");

        private const string k_ExecuteCSharpEnabledKey = "NanoraUnityMCP.ExecuteCSharpEnabled";

        // Security: Forbidden namespaces and types
        private static readonly HashSet<string> s_ForbiddenPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // File system destructive operations
            "System.IO.File.Delete",
            "System.IO.Directory.Delete",
            "File.WriteAllText",
            "File.WriteAllBytes",
            "File.AppendAllText",
            "File.Move",
            "File.Copy",
            "Directory.Move",
            "Directory.CreateDirectory",
            "StreamWriter",
            // Process and network
            "System.Diagnostics.Process",
            "Process.Start",
            "ProcessStartInfo",
            "System.Net.Http",
            "System.Net.WebClient",
            "WebRequest",
            "HttpClient",
            "TcpClient",
            "UdpClient",
            "Socket",
            // Reflection abuse
            "Assembly.Load",
            "Assembly.LoadFrom",
            "Assembly.LoadFile",
            "Activator.CreateInstance",
            // Environment manipulation
            "System.Environment.Exit",
            "Environment.SetEnvironmentVariable",
            "Registry.",
            "RegistryKey",
            // Editor destructive operations
            "Application.Quit",
            "EditorApplication.Exit",
            "FileUtil.DeleteFileOrDirectory",
            "AssetDatabase.DeleteAsset",
            "AssetDatabase.MoveAsset",
        };

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                // Check if execute_csharp is enabled via EditorPrefs
                if (!EditorPrefs.GetBool(k_ExecuteCSharpEnabledKey, true))
                {
                    return McpToolResult.Error("execute_csharp is disabled in MCP Server settings. " +
                        "Enable it via Window > Unity MCP Server > Security.");
                }

                string code = arguments?["code"]?.Value<string>();
                int timeoutMs = arguments?["timeout_ms"]?.Value<int>() ?? k_DefaultTimeoutMs;
                var additionalUsings = arguments?["include_usings"]?.Values<string>()?.ToList() ?? new List<string>();
                bool forceRoslyn = arguments?["force_roslyn"]?.Value<bool>() ?? false;

                if (string.IsNullOrWhiteSpace(code))
                {
                    return McpToolResult.Error("Code is required");
                }

                // Auto-detect if Roslyn should be forced (multi-statement with var)
                if (!forceRoslyn && ShouldForceRoslyn(code))
                {
                    forceRoslyn = true;
                }

                // Security check
                var securityCheck = CheckSecurity(code);
                if (!securityCheck.isAllowed)
                {
                    return McpToolResult.Error($"Security violation: {securityCheck.reason}");
                }

                try
                {
                    var result = ExecuteCode(code, additionalUsings, timeoutMs, forceRoslyn);
                    return McpToolResult.Success(result);
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Execution failed: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }

        internal (bool isAllowed, string reason) CheckSecurity(string code)
        {
            foreach (var pattern in s_ForbiddenPatterns)
            {
                if (code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Forbidden API call: {pattern}");
                }
            }

            // Check for infinite loop patterns
            if (Regex.IsMatch(code, @"while\s*\(\s*true\s*\)", RegexOptions.IgnoreCase))
            {
                return (false, "Infinite loops are not allowed (while(true))");
            }

            if (Regex.IsMatch(code, @"for\s*\(\s*;\s*;\s*\)"))
            {
                return (false, "Infinite loops are not allowed (for(;;))");
            }

            if (Regex.IsMatch(code, @"while\s*\(\s*1\s*\)"))
            {
                return (false, "Infinite loops are not allowed (while(1))");
            }

            // Check for goto (potential loop abuse)
            if (Regex.IsMatch(code, @"\bgoto\s+\w+\s*;"))
            {
                return (false, "goto statements are not allowed");
            }

            return (true, null);
        }

        /// <summary>
        /// Detects if code should force Roslyn mode (multi-statement with var declarations).
        /// </summary>
        private bool ShouldForceRoslyn(string code)
        {
            // If code contains var declarations followed by other statements, force Roslyn
            // Pattern: var x = ...; followed by another statement
            if (Regex.IsMatch(code, @"\bvar\s+\w+\s*=.*;\s*\S", RegexOptions.Singleline))
            {
                return true;
            }

            // If code has multiple statements (multiple semicolons not in strings)
            var semicolonCount = Regex.Matches(code, @";(?=(?:[^""]*""[^""]*"")*[^""]*$)").Count;
            if (semicolonCount > 1)
            {
                return true;
            }

            return false;
        }

        private string ExecuteCode(string code, List<string> additionalUsings, int timeoutMs, bool forceRoslyn = false)
        {
            // Build the complete script with usings
            var script = BuildScript(code, additionalUsings);

            // Only an unavailable compiler permits fallback. A script may already
            // have changed Unity state before throwing; never execute it again.
            try
            {
                return ExecuteWithRoslyn(script, timeoutMs);
            }
            catch (RoslynNotAvailableException roslynEx) when (!forceRoslyn)
            {
                Debug.LogWarning($"[UnityMCP] Roslyn unavailable, trying reflection: {roslynEx.Message}");

                try
                {
                    return ExecuteWithReflection(code, timeoutMs);
                }
                catch (Exception reflectEx)
                {
                    throw new ExecutionException($"Roslyn is unavailable and reflection execution failed.\n" +
                        $"Roslyn: {roslynEx.Message}\n" +
                        $"Reflection: {reflectEx.Message}", reflectEx);
                }
            }
        }

        private string BuildScript(string code, List<string> additionalUsings)
        {
            var sb = new StringBuilder();

            // Default usings
            var usings = new List<string>
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "UnityEngine",
                "UnityEditor",
            };

            usings.AddRange(additionalUsings);

            foreach (var u in usings.Distinct())
            {
                sb.AppendLine($"using {u};");
            }

            sb.AppendLine();

            // Wrap code in a class and method if it's not already
            if (!code.Contains("class ") && !code.Contains("namespace "))
            {
                sb.AppendLine("public class DynamicScript");
                sb.AppendLine("{");
                sb.AppendLine("    public static object Execute()");
                sb.AppendLine("    {");

                // If code doesn't have a return, add one
                if (!code.Trim().StartsWith("return ") && !code.Contains("return "))
                {
                    sb.AppendLine($"        {code}");
                    sb.AppendLine("        return \"Executed successfully\";");
                }
                else
                {
                    sb.AppendLine($"        {code}");
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");
            }
            else
            {
                sb.Append(code);
            }

            return sb.ToString();
        }

        private string ExecuteWithRoslyn(string script, int timeoutMs)
        {
            // Use the RoslynCompiler to compile and execute
            var result = RoslynCompiler.CompileAndExecute(script, "DynamicScript", "Execute");

            var sb = new StringBuilder();
            sb.AppendLine("Code execution (Roslyn mode):");
            sb.AppendLine("---");
            sb.AppendLine(result);

            return sb.ToString();
        }

        private string ExecuteWithReflection(string code, int timeoutMs)
        {
            // For simple expressions, try to evaluate them directly
            // This is limited but works for many common cases

            var result = new StringBuilder();
            result.AppendLine("Code execution (reflection mode):");
            result.AppendLine("---");

            // Handle simple expressions
            code = code.Trim();

            // Remove return statement if present
            if (code.StartsWith("return "))
            {
                code = code.Substring(7).Trim();
            }
            if (code.EndsWith(";"))
                code = code.Substring(0, code.Length - 1).TrimEnd();

            // Try to evaluate as a simple expression
            object evalResult = null;

            // Selection operations
            if (code.StartsWith("Selection."))
            {
                evalResult = EvaluateSelectionExpression(code);
            }
            // GameObject operations
            else if (code.StartsWith("GameObject."))
            {
                evalResult = EvaluateGameObjectExpression(code);
            }
            // Debug operations
            else if (code.StartsWith("Debug."))
            {
                EvaluateDebugExpression(code);
                evalResult = "Debug command executed";
            }
            // EditorApplication operations
            else if (code.StartsWith("EditorApplication."))
            {
                evalResult = EvaluateEditorApplicationExpression(code);
            }
            // AssetDatabase operations
            else if (code.StartsWith("AssetDatabase."))
            {
                evalResult = EvaluateAssetDatabaseExpression(code);
            }
            // FindObjectOfType<T>() or Object.FindObjectOfType<T>()
            else if (code.Contains("FindObjectOfType"))
            {
                evalResult = EvaluateFindObjectOfTypeExpression(code);
            }
            // Singleton pattern: TypeName.Instance or TypeName.Instance.Property
            else if (Regex.IsMatch(code, @"^\w+\.Instance(?:\.\w+)?$"))
            {
                evalResult = EvaluateFindObjectOfTypeExpression(code);
            }
            // General expression
            else
            {
                throw new ExecutionException($"Expression not directly evaluable in reflection mode: {code}\n" +
                    "For complex code, Roslyn compilation is required.");
            }

            if (evalResult != null)
            {
                result.AppendLine(FormatResult(evalResult));
            }

            return result.ToString();
        }

        private object EvaluateSelectionExpression(string code)
        {
            if (code == "Selection.activeGameObject" || code == "Selection.activeGameObject.name")
            {
                var obj = Selection.activeGameObject;
                return obj != null ? (code.EndsWith(".name") ? obj.name : obj.ToString()) : "(null)";
            }

            if (code == "Selection.gameObjects" || code == "Selection.gameObjects.Length")
            {
                var objs = Selection.gameObjects;
                if (code.EndsWith(".Length"))
                {
                    return objs.Length;
                }
                return string.Join("\n", objs.Select(o => o.name));
            }

            if (code.StartsWith("Selection.activeGameObject = "))
            {
                var targetName = code.Substring("Selection.activeGameObject = ".Length).Trim('"', '\'', ' ');
                var obj = GameObject.Find(targetName);
                if (obj != null)
                {
                    Selection.activeGameObject = obj;
                    return $"Selected: {obj.name}";
                }
                throw new ExecutionException($"Object not found: {targetName}");
            }

            throw new ExecutionException($"Unsupported Selection expression: {code}");
        }

        private object EvaluateGameObjectExpression(string code)
        {
            // GameObject.Find("name")
            var findMatch = Regex.Match(code, @"^GameObject\.Find\s*\(\s*""([^""]+)""\s*\)$");
            if (findMatch.Success)
            {
                var name = findMatch.Groups[1].Value;
                var obj = GameObject.Find(name);
                return obj != null ? $"Found: {obj.name} (ID: {obj.GetInstanceID()})" : "(null)";
            }

            // GameObject.CreatePrimitive(PrimitiveType.Cube)
            var primitiveMatch = Regex.Match(code, @"^GameObject\.CreatePrimitive\s*\(\s*PrimitiveType\.(\w+)\s*\)$");
            if (primitiveMatch.Success)
            {
                var typeName = primitiveMatch.Groups[1].Value;
                if (Enum.TryParse<PrimitiveType>(typeName, out var primitiveType))
                {
                    var obj = GameObject.CreatePrimitive(primitiveType);
                    Undo.RegisterCreatedObjectUndo(obj, "Create Primitive");
                    return $"Created: {obj.name}";
                }
            }

            throw new ExecutionException($"Unsupported GameObject expression: {code}");
        }

        private void EvaluateDebugExpression(string code)
        {
            // Debug.Log("message")
            var logMatch = Regex.Match(code, @"^Debug\.Log\s*\(\s*(.+)\s*\)$");
            if (logMatch.Success)
            {
                var message = logMatch.Groups[1].Value.Trim('"');
                Debug.Log($"[MCP Script] {message}");
                return;
            }

            // Debug.LogWarning
            var warnMatch = Regex.Match(code, @"^Debug\.LogWarning\s*\(\s*(.+)\s*\)$");
            if (warnMatch.Success)
            {
                var message = warnMatch.Groups[1].Value.Trim('"');
                Debug.LogWarning($"[MCP Script] {message}");
                return;
            }

            // Debug.LogError
            var errorMatch = Regex.Match(code, @"^Debug\.LogError\s*\(\s*(.+)\s*\)$");
            if (errorMatch.Success)
            {
                var message = errorMatch.Groups[1].Value.Trim('"');
                Debug.LogError($"[MCP Script] {message}");
                return;
            }
            throw new ExecutionException($"Unsupported Debug expression: {code}");
        }

        private object EvaluateEditorApplicationExpression(string code)
        {
            if (code == "EditorApplication.isPlaying")
            {
                return EditorApplication.isPlaying;
            }

            if (code == "EditorApplication.isPlaying = true")
            {
                EditorApplication.isPlaying = true;
                return "Entering Play Mode...";
            }

            if (code == "EditorApplication.isPlaying = false")
            {
                EditorApplication.isPlaying = false;
                return "Exiting Play Mode...";
            }

            if (code == "EditorApplication.isPaused")
            {
                return EditorApplication.isPaused;
            }

            if (code.StartsWith("EditorApplication.ExecuteMenuItem"))
            {
                var menuMatch = Regex.Match(code, @"^EditorApplication\.ExecuteMenuItem\s*\(\s*""([^""]+)""\s*\)$");
                if (menuMatch.Success)
                {
                    var menuItem = menuMatch.Groups[1].Value;
                    if (!EditorApplication.ExecuteMenuItem(menuItem))
                        throw new ExecutionException($"Menu item could not be executed: {menuItem}");
                    return $"Executed menu item: {menuItem}";
                }
            }

            throw new ExecutionException($"Unsupported EditorApplication expression: {code}");
        }

        private object EvaluateAssetDatabaseExpression(string code)
        {
            // AssetDatabase.Refresh()
            if (Regex.IsMatch(code, @"^AssetDatabase\.Refresh\s*\(\s*\)$"))
            {
                AssetDatabase.Refresh();
                return "Asset database refreshed";
            }

            // AssetDatabase.FindAssets
            var findAssetsMatch = Regex.Match(code, @"^AssetDatabase\.FindAssets\s*\(\s*""([^""]*)""\s*\)$");
            if (findAssetsMatch.Success)
            {
                var filter = findAssetsMatch.Groups[1].Value;
                var guids = AssetDatabase.FindAssets(filter);
                var paths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).Take(20);
                return $"Found {guids.Length} assets:\n" + string.Join("\n", paths);
            }

            throw new ExecutionException($"Unsupported AssetDatabase expression: {code}");
        }

        private object EvaluateFindObjectOfTypeExpression(string code)
        {
            // Patterns:
            // FindObjectOfType<TypeName>()
            // Object.FindObjectOfType<TypeName>()
            // UnityEngine.Object.FindObjectOfType<TypeName>()
            // TypeName.Instance (static singleton)
            // Any of above followed by .PropertyName

            // Try static singleton pattern first: TypeName.Instance or TypeName.Instance.Property
            var singletonMatch = Regex.Match(code, @"^(\w+)\.Instance(?:\.(\w+))?$");
            if (singletonMatch.Success)
            {
                var typeName = singletonMatch.Groups[1].Value;
                var propertyName = singletonMatch.Groups[2].Success ? singletonMatch.Groups[2].Value : null;
                return GetSingletonValue(typeName, propertyName);
            }

            // FindObjectOfType pattern
            var match = Regex.Match(code, @"^(?:UnityEngine\.Object\.|Object\.)?FindObjectOfType\s*<\s*(\w+)\s*>\s*\(\s*\)(?:\.(\w+))?$");
            if (match.Success)
            {
                var typeName = match.Groups[1].Value;
                var propertyName = match.Groups[2].Success ? match.Groups[2].Value : null;
                return GetFindObjectOfTypeValue(typeName, propertyName);
            }

            throw new ExecutionException($"Unsupported FindObjectOfType expression: {code}");
        }

        private object GetSingletonValue(string typeName, string propertyName)
        {
            // Find the type in loaded assemblies
            Type type = FindTypeByName(typeName);
            if (type == null)
            {
                throw new ExecutionException($"Type not found: {typeName}");
            }

            // Get the Instance property (static singleton)
            var instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null)
            {
                throw new ExecutionException($"No static Instance property on type: {typeName}");
            }

            var instance = instanceProp.GetValue(null);
            if (instance == null)
            {
                return $"{typeName}.Instance is null";
            }

            // If no property specified, return the instance info
            if (string.IsNullOrEmpty(propertyName))
            {
                return $"{typeName}.Instance = {instance}";
            }

            // Get the specified property
            return GetPropertyValue(instance, type, propertyName);
        }

        private object GetFindObjectOfTypeValue(string typeName, string propertyName)
        {
            // Find the type in loaded assemblies
            Type type = FindTypeByName(typeName);
            if (type == null)
            {
                throw new ExecutionException($"Type not found: {typeName}");
            }

            // Call FindObjectOfType using reflection
            var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[0]);
            var genericMethod = findMethod.MakeGenericMethod(type);
            var instance = genericMethod.Invoke(null, null);

            if (instance == null)
            {
                return $"FindObjectOfType<{typeName}>() returned null";
            }

            // If no property specified, return basic info
            if (string.IsNullOrEmpty(propertyName))
            {
                return $"Found: {instance}";
            }

            // Get the specified property
            return GetPropertyValue(instance, type, propertyName);
        }

        private object GetPropertyValue(object instance, Type type, string propertyName)
        {
            // Try property first
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var value = prop.GetValue(instance);
                return $"{type.Name}.{propertyName} = {value}";
            }

            // Try field
            var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(instance);
                return $"{type.Name}.{propertyName} = {value}";
            }

            throw new ExecutionException($"Property/field not found: {type.Name}.{propertyName}");
        }

        private Type FindTypeByName(string typeName)
        {
            // Search in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Try exact match first
                var type = assembly.GetType(typeName);
                if (type != null) return type;

                // Try with common namespaces
                foreach (var prefix in new[] { "UnityEngine.", "UnityEditor.", "" })
                {
                    type = assembly.GetType(prefix + typeName);
                    if (type != null) return type;
                }

                // Search by simple name
                try
                {
                    var types = assembly.GetTypes();
                    var found = types.FirstOrDefault(t => t.Name == typeName);
                    if (found != null) return found;
                }
                catch
                {
                    // Some assemblies may throw on GetTypes()
                }
            }

            return null;
        }

        private string FormatResult(object result)
        {
            if (result == null)
            {
                return "(null)";
            }

            if (result is string str)
            {
                return str;
            }

            if (result is IEnumerable<object> enumerable)
            {
                return string.Join("\n", enumerable.Select(o => o?.ToString() ?? "(null)"));
            }

            return result.ToString();
        }
    }
}
