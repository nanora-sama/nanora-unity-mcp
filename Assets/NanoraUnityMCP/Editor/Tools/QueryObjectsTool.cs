using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// U-Selector: Playwright-like query engine for Unity GameObjects and UI Toolkit elements.
    /// Supports ID (#Name), Type (.Component), Path (/Parent/Child), Attribute ([property=value]),
    /// and UI Toolkit (ui:#name, ui:.class) selectors.
    /// </summary>
    [McpTool("query_objects")]
    public class QueryObjectsTool : IMcpTool
    {
        public string Name => "query_objects";
        public string Description => "Query GameObjects using U-Selector syntax (similar to CSS selectors). " +
            "Supports: #Name (exact name), .Component (has component), /Path (hierarchy path), [prop=value] (attribute), " +
            "ui:#name or ui:.class (UI Toolkit elements)";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector query string. Examples: '#Player', '.Camera', '/Canvas/Menu', '.Enemy[Health>50]'""
                },
                ""include_inactive"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include inactive GameObjects in search"",
                    ""default"": false
                },
                ""max_results"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum number of results to return (0 = unlimited)"",
                    ""default"": 100
                },
                ""include_details"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include component and transform details in results"",
                    ""default"": false
                }
            },
            ""required"": [""selector""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string selector = arguments?["selector"]?.Value<string>();
                bool includeInactive = arguments?["include_inactive"]?.Value<bool>() ?? false;
                int maxResults = arguments?["max_results"]?.Value<int>() ?? 100;
                bool includeDetails = arguments?["include_details"]?.Value<bool>() ?? false;

                if (string.IsNullOrWhiteSpace(selector))
                {
                    return McpToolResult.Error("Selector is required");
                }

                try
                {
                    var results = SelectorEngine.Query(selector, includeInactive);

                    if (maxResults > 0 && results.Count > maxResults)
                    {
                        results = results.Take(maxResults).ToList();
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"Query: {selector}");
                    sb.AppendLine($"Found: {results.Count} object(s)");
                    sb.AppendLine();

                    foreach (var obj in results)
                    {
                        if (obj == null) continue;

                        var path = GetGameObjectPath(obj);
                        var activeMarker = obj.activeInHierarchy ? "" : " [inactive]";

                        if (includeDetails)
                        {
                            sb.AppendLine($"- {path}{activeMarker}");
                            sb.AppendLine($"  InstanceID: {obj.GetInstanceID()}");
                            sb.AppendLine($"  Position: {obj.transform.position}");
                            sb.AppendLine($"  Rotation: {obj.transform.eulerAngles}");
                            sb.AppendLine($"  Scale: {obj.transform.localScale}");
                            sb.AppendLine($"  Layer: {LayerMask.LayerToName(obj.layer)}");
                            sb.AppendLine($"  Tag: {obj.tag}");

                            var components = obj.GetComponents<Component>();
                            var componentNames = string.Join(", ", components
                                .Where(c => c != null)
                                .Select(c => c.GetType().Name));
                            sb.AppendLine($"  Components: {componentNames}");
                            sb.AppendLine();
                        }
                        else
                        {
                            sb.AppendLine($"- {path}{activeMarker}");
                        }
                    }

                    return McpToolResult.Success(sb.ToString());
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Query failed: {ex.Message}");
                }
            });
        }

        private string GetGameObjectPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return "/" + path;
        }
    }

    /// <summary>
    /// U-Selector Engine: Query GameObjects using CSS-like selectors.
    /// Supports wildcard patterns: #Name* (prefix), #*Name (suffix), #*Name* (contains)
    /// </summary>
    public static class SelectorEngine
    {
        // Matches #Name, #Name*, #*Name, #Name_*, etc.
        private static readonly Regex s_IdSelectorRegex = new Regex(@"^#([^\s\[\]\.]+)$", RegexOptions.Compiled);
        private static readonly Regex s_TypeSelectorRegex = new Regex(@"^\.([^\s\[\]#]+)$", RegexOptions.Compiled);
        private static readonly Regex s_PathSelectorRegex = new Regex(@"^/(.+)$", RegexOptions.Compiled);
        private static readonly Regex s_AttributeSelectorRegex = new Regex(@"^\[([^\]]+)\]$", RegexOptions.Compiled);
        private static readonly Regex s_CombinedSelectorRegex = new Regex(
            @"(#[^\s\[\]\.]+|\.([^\s\[\]#]+)|\[[^\]]+\]|/[^\s\[\]#\.]+)",
            RegexOptions.Compiled);
        // Wildcard pattern detection
        private static readonly Regex s_WildcardPattern = new Regex(@"[\*\?]", RegexOptions.Compiled);

        /// <summary>
        /// Query GameObjects using U-Selector syntax.
        /// For UI Toolkit elements, use QueryUIToolkit instead.
        /// </summary>
        public static List<GameObject> Query(string selector, bool includeInactive = false)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return new List<GameObject>();
            }

            selector = selector.Trim();

            // UI Toolkit Selector: ui:#name or ui:.class
            if (selector.StartsWith("ui:", StringComparison.OrdinalIgnoreCase))
            {
                // For UI Toolkit, return the GameObjects containing matching UIDocuments
                var uiSelector = selector.Substring(3);
                return QueryUIToolkitGameObjects(uiSelector, includeInactive);
            }

            // ID Selector: #Name
            var idMatch = s_IdSelectorRegex.Match(selector);
            if (idMatch.Success)
            {
                return QueryByName(idMatch.Groups[1].Value, includeInactive);
            }

            // Type Selector: .ComponentType
            var typeMatch = s_TypeSelectorRegex.Match(selector);
            if (typeMatch.Success)
            {
                return QueryByComponent(typeMatch.Groups[1].Value, includeInactive);
            }

            // Path Selector: /Parent/Child
            var pathMatch = s_PathSelectorRegex.Match(selector);
            if (pathMatch.Success)
            {
                return QueryByPath(pathMatch.Groups[1].Value, includeInactive);
            }

            // Attribute Selector: [property=value] or .Type[property=value]
            if (selector.Contains("["))
            {
                return QueryByAttribute(selector, includeInactive);
            }

            // Fallback: treat as name search
            return QueryByName(selector, includeInactive);
        }

        /// <summary>
        /// Query UI Toolkit VisualElements using USS-like selectors.
        /// Returns matching VisualElements from all UIDocuments in the scene.
        /// </summary>
        public static List<VisualElement> QueryUIToolkit(string selector, bool includeInactive = false)
        {
            var results = new List<VisualElement>();

            if (string.IsNullOrWhiteSpace(selector))
            {
                return results;
            }

            selector = selector.Trim();

            // Remove ui: prefix if present
            if (selector.StartsWith("ui:", StringComparison.OrdinalIgnoreCase))
            {
                selector = selector.Substring(3);
            }

            // Find all UIDocuments in the scene
#if UNITY_2022_1_OR_NEWER
            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var uiDocuments = UnityEngine.Object.FindObjectsOfType<UIDocument>(includeInactive);
#endif

            foreach (var uiDoc in uiDocuments)
            {
                if (uiDoc.rootVisualElement == null)
                {
                    continue;
                }

                var elements = QueryVisualElementTree(uiDoc.rootVisualElement, selector);
                results.AddRange(elements);
            }

            return results;
        }

        /// <summary>
        /// Get GameObjects containing UIDocuments with matching VisualElements.
        /// </summary>
        private static List<GameObject> QueryUIToolkitGameObjects(string selector, bool includeInactive)
        {
            var results = new List<GameObject>();

#if UNITY_2022_1_OR_NEWER
            var uiDocuments = UnityEngine.Object.FindObjectsByType<UIDocument>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var uiDocuments = UnityEngine.Object.FindObjectsOfType<UIDocument>(includeInactive);
#endif

            foreach (var uiDoc in uiDocuments)
            {
                if (uiDoc.rootVisualElement == null)
                {
                    continue;
                }

                var elements = QueryVisualElementTree(uiDoc.rootVisualElement, selector);
                if (elements.Count > 0)
                {
                    results.Add(uiDoc.gameObject);
                }
            }

            return results;
        }

        /// <summary>
        /// Query a VisualElement tree using USS-like selectors.
        /// </summary>
        private static List<VisualElement> QueryVisualElementTree(VisualElement root, string selector)
        {
            var results = new List<VisualElement>();

            if (root == null || string.IsNullOrWhiteSpace(selector))
            {
                return results;
            }

            // Name selector: #name
            if (selector.StartsWith("#"))
            {
                string name = selector.Substring(1);
                var element = root.Q(name);
                if (element != null)
                {
                    results.Add(element);
                }
                return results;
            }

            // Class selector: .classname
            if (selector.StartsWith("."))
            {
                string className = selector.Substring(1);
                var elements = root.Query(className: className).ToList();
                results.AddRange(elements);
                return results;
            }

            // Type selector: Button, Label, etc.
            if (char.IsUpper(selector[0]))
            {
                // Try to find elements by type name
                var elements = QueryByTypeName(root, selector);
                results.AddRange(elements);
                return results;
            }

            // Fallback: try as name
            var fallbackElement = root.Q(selector);
            if (fallbackElement != null)
            {
                results.Add(fallbackElement);
            }

            return results;
        }

        /// <summary>
        /// Query VisualElements by their type name.
        /// </summary>
        private static List<VisualElement> QueryByTypeName(VisualElement root, string typeName)
        {
            var results = new List<VisualElement>();
            CollectElementsByTypeName(root, typeName, results);
            return results;
        }

        private static void CollectElementsByTypeName(VisualElement element, string typeName, List<VisualElement> results)
        {
            if (element.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(element);
            }

            foreach (var child in element.Children())
            {
                CollectElementsByTypeName(child, typeName, results);
            }
        }

        /// <summary>
        /// Find first matching object, throw if not found or ambiguous.
        /// </summary>
        public static GameObject QueryOne(string selector, bool includeInactive = false)
        {
            var results = Query(selector, includeInactive);

            if (results.Count == 0)
            {
                throw new Exception($"No object found for selector: {selector}");
            }

            if (results.Count > 1)
            {
                throw new AmbiguousSelectorException(selector, results.Count);
            }

            return results[0];
        }

        private static List<GameObject> QueryByName(string name, bool includeInactive)
        {
            var results = new List<GameObject>();
            var allObjects = GetAllGameObjects(includeInactive);

            // Check if name contains wildcard characters
            bool hasWildcard = s_WildcardPattern.IsMatch(name);

            if (hasWildcard)
            {
                // Convert glob pattern to regex
                // * matches any sequence of characters
                // ? matches any single character
                var regexPattern = WildcardToRegex(name);
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

                foreach (var obj in allObjects)
                {
                    if (regex.IsMatch(obj.name))
                    {
                        results.Add(obj);
                    }
                }
            }
            else
            {
                // Exact match
                foreach (var obj in allObjects)
                {
                    if (obj.name == name)
                    {
                        results.Add(obj);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Convert a glob-style wildcard pattern to a regex pattern.
        /// Supports: * (match any sequence), ? (match single char)
        /// </summary>
        private static string WildcardToRegex(string pattern)
        {
            // Escape regex special characters except * and ?
            var escaped = Regex.Escape(pattern);
            // Replace escaped \* with .* and escaped \? with .
            escaped = escaped.Replace("\\*", ".*").Replace("\\?", ".");
            // Anchor the pattern
            return "^" + escaped + "$";
        }

        private static List<GameObject> QueryByComponent(string typeName, bool includeInactive)
        {
            var results = new List<GameObject>();

            // Try to find the type
            Type componentType = null;

            // Common Unity types shorthand
            var typeMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Camera", "UnityEngine.Camera" },
                { "Light", "UnityEngine.Light" },
                { "MeshRenderer", "UnityEngine.MeshRenderer" },
                { "MeshFilter", "UnityEngine.MeshFilter" },
                { "BoxCollider", "UnityEngine.BoxCollider" },
                { "SphereCollider", "UnityEngine.SphereCollider" },
                { "CapsuleCollider", "UnityEngine.CapsuleCollider" },
                { "Rigidbody", "UnityEngine.Rigidbody" },
                { "AudioSource", "UnityEngine.AudioSource" },
                { "Canvas", "UnityEngine.Canvas" },
                { "Text", "UnityEngine.UI.Text" },
                { "Image", "UnityEngine.UI.Image" },
                { "Button", "UnityEngine.UI.Button" },
                { "Animator", "UnityEngine.Animator" },
                { "ParticleSystem", "UnityEngine.ParticleSystem" },
            };

            if (typeMapping.TryGetValue(typeName, out string fullTypeName))
            {
                componentType = Type.GetType(fullTypeName + ", UnityEngine") ??
                               Type.GetType(fullTypeName + ", UnityEngine.UI");
            }

            // Try direct type lookup
            if (componentType == null)
            {
                componentType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t.Name == typeName && typeof(Component).IsAssignableFrom(t));
            }

            if (componentType == null)
            {
                return results;
            }

            // Use FindObjectsOfType
            var findMethod = typeof(UnityEngine.Object)
                .GetMethod("FindObjectsOfType", new Type[] { typeof(bool) })
                ?.MakeGenericMethod(componentType);

            if (findMethod != null)
            {
                var components = findMethod.Invoke(null, new object[] { includeInactive }) as UnityEngine.Object[];
                if (components != null)
                {
                    foreach (var comp in components)
                    {
                        if (comp is Component c && c.gameObject != null)
                        {
                            results.Add(c.gameObject);
                        }
                    }
                }
            }

            return results.Distinct().ToList();
        }

        private static List<GameObject> QueryByPath(string path, bool includeInactive)
        {
            var results = new List<GameObject>();
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return results;
            }

            // Find root objects matching first part
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var candidates = new List<Transform>();

            foreach (var root in rootObjects)
            {
                if (!includeInactive && !root.activeInHierarchy) continue;

                if (root.name == parts[0])
                {
                    candidates.Add(root.transform);
                }
            }

            // Traverse path
            for (int i = 1; i < parts.Length; i++)
            {
                var nextCandidates = new List<Transform>();
                string partName = parts[i];

                foreach (var candidate in candidates)
                {
                    foreach (Transform child in candidate)
                    {
                        if (!includeInactive && !child.gameObject.activeInHierarchy) continue;

                        if (child.name == partName)
                        {
                            nextCandidates.Add(child);
                        }
                    }
                }

                candidates = nextCandidates;

                if (candidates.Count == 0)
                {
                    break;
                }
            }

            foreach (var t in candidates)
            {
                results.Add(t.gameObject);
            }

            return results;
        }

        private static List<GameObject> QueryByAttribute(string selector, bool includeInactive)
        {
            var results = new List<GameObject>();

            // Parse selector: .Type[prop=value] or [prop=value]
            var attrMatch = Regex.Match(selector, @"(\.([^\[]+))?\[([^\]]+)\]");
            if (!attrMatch.Success)
            {
                return results;
            }

            string typeName = attrMatch.Groups[2].Value;
            string attributeExpr = attrMatch.Groups[3].Value;

            // Parse attribute expression: prop=value, prop>value, prop<value
            var exprMatch = Regex.Match(attributeExpr, @"(\w+)\s*(=|>|<|>=|<=|!=)\s*(.+)");
            if (!exprMatch.Success)
            {
                return results;
            }

            string propertyName = exprMatch.Groups[1].Value;
            string operatorStr = exprMatch.Groups[2].Value;
            string valueStr = exprMatch.Groups[3].Value.Trim('"', '\'');

            // Get candidate objects
            List<GameObject> candidates;
            if (!string.IsNullOrEmpty(typeName))
            {
                candidates = QueryByComponent(typeName, includeInactive);
            }
            else
            {
                candidates = GetAllGameObjects(includeInactive).ToList();
            }

            // Filter by attribute
            foreach (var obj in candidates)
            {
                if (EvaluateAttribute(obj, propertyName, operatorStr, valueStr))
                {
                    results.Add(obj);
                }
            }

            return results;
        }

        private static bool EvaluateAttribute(GameObject obj, string propertyName, string op, string valueStr)
        {
            // Check common properties first
            object actualValue = null;

            switch (propertyName.ToLower())
            {
                case "name":
                    actualValue = obj.name;
                    break;
                case "tag":
                    actualValue = obj.tag;
                    break;
                case "layer":
                    actualValue = obj.layer;
                    break;
                case "active":
                case "activeinHierarchy":
                    actualValue = obj.activeInHierarchy;
                    break;
                case "activeself":
                    actualValue = obj.activeSelf;
                    break;
                default:
                    // Try to find property in components
                    foreach (var comp in obj.GetComponents<Component>())
                    {
                        if (comp == null) continue;

                        var prop = comp.GetType().GetProperty(propertyName);
                        if (prop != null)
                        {
                            try
                            {
                                actualValue = prop.GetValue(comp);
                                break;
                            }
                            catch { }
                        }

                        var field = comp.GetType().GetField(propertyName);
                        if (field != null)
                        {
                            try
                            {
                                actualValue = field.GetValue(comp);
                                break;
                            }
                            catch { }
                        }
                    }
                    break;
            }

            if (actualValue == null)
            {
                return false;
            }

            // Compare values
            return CompareValues(actualValue, op, valueStr);
        }

        private static bool CompareValues(object actual, string op, string expected)
        {
            if (actual == null)
            {
                return op == "!=" && expected != "null";
            }

            // String comparison
            if (actual is string actualStr)
            {
                return op switch
                {
                    "=" => actualStr.Equals(expected, StringComparison.OrdinalIgnoreCase),
                    "!=" => !actualStr.Equals(expected, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }

            // Boolean comparison
            if (actual is bool actualBool)
            {
                if (bool.TryParse(expected, out bool expectedBool))
                {
                    return op switch
                    {
                        "=" => actualBool == expectedBool,
                        "!=" => actualBool != expectedBool,
                        _ => false
                    };
                }
            }

            // Numeric comparison
            if (double.TryParse(expected, out double expectedNum) &&
                double.TryParse(actual.ToString(), out double actualNum))
            {
                return op switch
                {
                    "=" => Math.Abs(actualNum - expectedNum) < 0.0001,
                    "!=" => Math.Abs(actualNum - expectedNum) >= 0.0001,
                    ">" => actualNum > expectedNum,
                    "<" => actualNum < expectedNum,
                    ">=" => actualNum >= expectedNum,
                    "<=" => actualNum <= expectedNum,
                    _ => false
                };
            }

            // Default string comparison
            return op == "=" && actual.ToString().Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<GameObject> GetAllGameObjects(bool includeInactive)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                if (!includeInactive && !root.activeInHierarchy) continue;

                yield return root;

                foreach (var child in GetAllChildren(root.transform, includeInactive))
                {
                    yield return child;
                }
            }
        }

        private static IEnumerable<GameObject> GetAllChildren(Transform parent, bool includeInactive)
        {
            foreach (Transform child in parent)
            {
                if (!includeInactive && !child.gameObject.activeInHierarchy) continue;

                yield return child.gameObject;

                foreach (var grandchild in GetAllChildren(child, includeInactive))
                {
                    yield return grandchild;
                }
            }
        }
    }

    /// <summary>
    /// Exception thrown when a selector matches multiple objects but only one was expected.
    /// </summary>
    public class AmbiguousSelectorException : Exception
    {
        public string Selector { get; }
        public int MatchCount { get; }

        public AmbiguousSelectorException(string selector, int matchCount)
            : base($"Selector '{selector}' matched {matchCount} objects, expected exactly one. " +
                   "Please use a more specific selector.")
        {
            Selector = selector;
            MatchCount = matchCount;
        }
    }
}
