using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Reflection-based type inspection tool.
    /// Allows querying .NET/Unity type information, searching types, inspecting members,
    /// and invoking static methods at runtime.
    /// </summary>
    [McpTool("unity_reflect")]
    public class UnityReflectTool : IMcpTool
    {
        public string Name => "unity_reflect";
        public string Description => "Inspect .NET/Unity types via reflection: get type info, search types, " +
            "get member details, and invoke static methods";

        private const int k_SearchResultLimit = 50;
        private const int k_MaxMembersListed = 200;

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""get_type"", ""search_type"", ""get_member"", ""invoke_method""],
                    ""description"": ""Action to perform""
                },
                ""type_name"": {
                    ""type"": ""string"",
                    ""description"": ""Fully qualified type name (e.g. UnityEngine.GameObject)""
                },
                ""search_query"": {
                    ""type"": ""string"",
                    ""description"": ""Partial type name to search for (search_type action)""
                },
                ""member_name"": {
                    ""type"": ""string"",
                    ""description"": ""Member name to inspect (get_member action)""
                },
                ""method_args"": {
                    ""type"": ""array"",
                    ""items"": {},
                    ""description"": ""Arguments for static method invocation (invoke_method action)""
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
                case "get_type":
                    return Task.FromResult(HandleGetType(arguments));
                case "search_type":
                    return Task.FromResult(HandleSearchType(arguments));
                case "get_member":
                    return Task.FromResult(HandleGetMember(arguments));
                case "invoke_method":
                    return Task.FromResult(HandleInvokeMethod(arguments));
                default:
                    return Task.FromResult(McpToolResult.Error(
                        $"Unknown action '{action}'. Use: get_type, search_type, get_member, invoke_method"));
            }
        }

        #region Actions

        private McpToolResult HandleGetType(JObject arguments)
        {
            var typeName = arguments?["type_name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return McpToolResult.Error("'type_name' is required for get_type");
            }

            var type = ResolveType(typeName);
            if (type == null)
            {
                return McpToolResult.Error($"Type '{typeName}' not found. Use search_type to find available types.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Type: {type.FullName}");
            sb.AppendLine($"Namespace: {type.Namespace ?? "(none)"}");
            sb.AppendLine($"Assembly: {type.Assembly.GetName().Name}");
            sb.AppendLine($"IsClass: {type.IsClass}");
            sb.AppendLine($"IsInterface: {type.IsInterface}");
            sb.AppendLine($"IsEnum: {type.IsEnum}");
            sb.AppendLine($"IsAbstract: {type.IsAbstract}");
            sb.AppendLine($"IsSealed: {type.IsSealed}");
            sb.AppendLine($"IsValueType: {type.IsValueType}");

            if (type.BaseType != null)
            {
                sb.AppendLine($"BaseType: {type.BaseType.FullName}");
            }

            var interfaces = type.GetInterfaces();
            if (interfaces.Length > 0)
            {
                sb.AppendLine($"Interfaces ({interfaces.Length}): {string.Join(", ", interfaces.Select(i => i.Name))}");
            }

            // Custom attributes
            var attrs = type.GetCustomAttributes(false);
            if (attrs.Length > 0)
            {
                sb.AppendLine($"Attributes: {string.Join(", ", attrs.Select(a => a.GetType().Name))}");
            }

            // Enum values
            if (type.IsEnum)
            {
                var names = Enum.GetNames(type);
                sb.AppendLine($"Enum values ({names.Length}): {string.Join(", ", names.Take(50))}");
                if (names.Length > 50) sb.AppendLine("  ...(truncated)");
            }

            // Public members summary
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            sb.AppendLine($"\nPublic members (declared, {members.Length} total):");

            int count = 0;
            foreach (var member in members.Take(k_MaxMembersListed))
            {
                sb.AppendLine($"  [{member.MemberType}] {FormatMemberBrief(member)}");
                count++;
            }

            if (members.Length > k_MaxMembersListed)
            {
                sb.AppendLine($"  ...({members.Length - k_MaxMembersListed} more, use get_member for details)");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult HandleSearchType(JObject arguments)
        {
            var query = arguments?["search_query"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return McpToolResult.Error("'search_query' is required for search_type");
            }

            var matches = new List<string>();
            var lowerQuery = query.ToLowerInvariant();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t.FullName == null) continue;
                    if (t.FullName.ToLowerInvariant().Contains(lowerQuery))
                    {
                        matches.Add(t.FullName);
                        if (matches.Count >= k_SearchResultLimit) break;
                    }
                }

                if (matches.Count >= k_SearchResultLimit) break;
            }

            if (matches.Count == 0)
            {
                return McpToolResult.Success($"No types found matching '{query}'");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {matches.Count} type(s) matching '{query}':");
            foreach (var m in matches.OrderBy(x => x))
            {
                sb.AppendLine($"  {m}");
            }

            if (matches.Count >= k_SearchResultLimit)
            {
                sb.AppendLine($"  ...(results capped at {k_SearchResultLimit})");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult HandleGetMember(JObject arguments)
        {
            var typeName = arguments?["type_name"]?.Value<string>();
            var memberName = arguments?["member_name"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return McpToolResult.Error("'type_name' is required for get_member");
            }
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return McpToolResult.Error("'member_name' is required for get_member");
            }

            var type = ResolveType(typeName);
            if (type == null)
            {
                return McpToolResult.Error($"Type '{typeName}' not found");
            }

            var members = type.GetMember(memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (members.Length == 0)
            {
                return McpToolResult.Error($"Member '{memberName}' not found on type '{typeName}'");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Member '{memberName}' on {type.FullName}:");

            foreach (var member in members)
            {
                sb.AppendLine($"\n  Kind: {member.MemberType}");
                sb.AppendLine($"  DeclaringType: {member.DeclaringType?.FullName}");

                switch (member)
                {
                    case MethodInfo method:
                        sb.AppendLine($"  ReturnType: {method.ReturnType.FullName}");
                        sb.AppendLine($"  IsStatic: {method.IsStatic}");
                        sb.AppendLine($"  IsVirtual: {method.IsVirtual}");
                        var parameters = method.GetParameters();
                        if (parameters.Length > 0)
                        {
                            sb.AppendLine($"  Parameters ({parameters.Length}):");
                            foreach (var p in parameters)
                            {
                                var defaultStr = p.HasDefaultValue ? $" = {p.DefaultValue ?? "null"}" : "";
                                sb.AppendLine($"    {p.ParameterType.Name} {p.Name}{defaultStr}");
                            }
                        }
                        else
                        {
                            sb.AppendLine("  Parameters: (none)");
                        }
                        break;

                    case PropertyInfo prop:
                        sb.AppendLine($"  PropertyType: {prop.PropertyType.FullName}");
                        sb.AppendLine($"  CanRead: {prop.CanRead}");
                        sb.AppendLine($"  CanWrite: {prop.CanWrite}");
                        sb.AppendLine($"  IsStatic: {prop.GetGetMethod()?.IsStatic ?? prop.GetSetMethod()?.IsStatic ?? false}");
                        break;

                    case FieldInfo field:
                        sb.AppendLine($"  FieldType: {field.FieldType.FullName}");
                        sb.AppendLine($"  IsStatic: {field.IsStatic}");
                        sb.AppendLine($"  IsReadonly: {field.IsInitOnly}");
                        sb.AppendLine($"  IsLiteral: {field.IsLiteral}");
                        if (field.IsLiteral)
                        {
                            try { sb.AppendLine($"  Value: {field.GetRawConstantValue()}"); }
                            catch { /* ignore */ }
                        }
                        break;

                    case EventInfo evt:
                        sb.AppendLine($"  EventHandlerType: {evt.EventHandlerType?.FullName}");
                        break;
                }

                var attrs = member.GetCustomAttributes(false);
                if (attrs.Length > 0)
                {
                    sb.AppendLine($"  Attributes: {string.Join(", ", attrs.Select(a => a.GetType().Name))}");
                }
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult HandleInvokeMethod(JObject arguments)
        {
            var typeName = arguments?["type_name"]?.Value<string>();
            var memberName = arguments?["member_name"]?.Value<string>();
            var argsArray = arguments?["method_args"] as JArray;

            if (string.IsNullOrWhiteSpace(typeName))
            {
                return McpToolResult.Error("'type_name' is required for invoke_method");
            }
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return McpToolResult.Error("'member_name' is required for invoke_method");
            }

            var type = ResolveType(typeName);
            if (type == null)
            {
                return McpToolResult.Error($"Type '{typeName}' not found");
            }

            // Find static methods with matching name
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == memberName)
                .ToArray();

            if (methods.Length == 0)
            {
                return McpToolResult.Error(
                    $"No public static method '{memberName}' found on '{typeName}'. " +
                    "Only static methods can be invoked via unity_reflect.");
            }

            var argValues = argsArray?.Select(a => ConvertArgument(a))?.ToArray() ?? Array.Empty<object>();

            // Try to find matching overload
            MethodInfo bestMatch = null;
            foreach (var method in methods)
            {
                var parms = method.GetParameters();
                if (parms.Length == argValues.Length)
                {
                    bestMatch = method;
                    break;
                }
                // Check for optional parameters
                int requiredCount = parms.Count(p => !p.HasDefaultValue);
                if (argValues.Length >= requiredCount && argValues.Length <= parms.Length)
                {
                    bestMatch = method;
                }
            }

            if (bestMatch == null)
            {
                var overloads = string.Join("\n  ",
                    methods.Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})"));
                return McpToolResult.Error(
                    $"No overload of '{memberName}' matches {argValues.Length} argument(s). Available:\n  {overloads}");
            }

            try
            {
                // Convert arguments to correct types
                var parms = bestMatch.GetParameters();
                var convertedArgs = new object[parms.Length];
                for (int i = 0; i < parms.Length; i++)
                {
                    if (i < argValues.Length)
                    {
                        convertedArgs[i] = Convert.ChangeType(argValues[i], parms[i].ParameterType);
                    }
                    else
                    {
                        convertedArgs[i] = parms[i].DefaultValue;
                    }
                }

                var result = bestMatch.Invoke(null, convertedArgs);

                return McpToolResult.Success(
                    $"Invoked {typeName}.{memberName}() successfully.\nReturn value: {result ?? "(null)"}");
            }
            catch (TargetInvocationException ex)
            {
                return McpToolResult.Error($"Method threw exception: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Failed to invoke method: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Resolves a type name by trying Type.GetType first, then searching all assemblies.
        /// </summary>
        internal static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            // Try direct resolution first
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        private static string FormatMemberBrief(MemberInfo member)
        {
            switch (member)
            {
                case MethodInfo m:
                    var parms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    var staticPrefix = m.IsStatic ? "static " : "";
                    return $"{staticPrefix}{m.ReturnType.Name} {m.Name}({parms})";

                case PropertyInfo p:
                    var accessors = new List<string>();
                    if (p.CanRead) accessors.Add("get");
                    if (p.CanWrite) accessors.Add("set");
                    return $"{p.PropertyType.Name} {p.Name} {{ {string.Join("; ", accessors)} }}";

                case FieldInfo f:
                    var fieldStatic = f.IsStatic ? "static " : "";
                    return $"{fieldStatic}{f.FieldType.Name} {f.Name}";

                case EventInfo e:
                    return $"event {e.EventHandlerType?.Name} {e.Name}";

                case ConstructorInfo c:
                    var cParms = string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    return $".ctor({cParms})";

                default:
                    return member.Name;
            }
        }

        private static object ConvertArgument(JToken token)
        {
            if (token == null) return null;

            switch (token.Type)
            {
                case JTokenType.Integer: return token.Value<long>();
                case JTokenType.Float: return token.Value<double>();
                case JTokenType.String: return token.Value<string>();
                case JTokenType.Boolean: return token.Value<bool>();
                case JTokenType.Null: return null;
                default: return token.ToString();
            }
        }

        #endregion
    }
}
