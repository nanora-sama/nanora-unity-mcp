using System;
using System.Collections.Generic;
using System.IO;
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
    /// Fuzzy search for assets in the project using AssetDatabase.
    /// Supports natural language-like queries and multiple asset types.
    /// </summary>
    [McpTool("search_assets")]
    public class SearchAssetsTool : IMcpTool
    {
        private const int k_DefaultMaxResults = 20;

        public string Name => "search_assets";
        public string Description => "Search for assets in the local project using fuzzy matching. " +
            "Supports filtering by type (prefab, texture, material, script, etc.) and path patterns. " +
            "Note: For Unity Registry packages (Cinemachine, Input System, etc.), use manage_packages instead.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""query"": {
                    ""type"": ""string"",
                    ""description"": ""Search query (supports multiple words for fuzzy matching)""
                },
                ""type_filter"": {
                    ""type"": ""string"",
                    ""description"": ""Asset type filter"",
                    ""enum"": [""all"", ""prefab"", ""texture"", ""material"", ""script"", ""scene"", ""audio"", ""model"", ""shader"", ""animation""],
                    ""default"": ""all""
                },
                ""path_filter"": {
                    ""type"": ""string"",
                    ""description"": ""Filter by asset path (e.g., 'Assets/Prefabs')""
                },
                ""max_results"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum number of results to return"",
                    ""default"": 20
                },
                ""include_packages"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include assets from Packages folder"",
                    ""default"": false
                }
            },
            ""required"": [""query""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string query = arguments?["query"]?.Value<string>();
                string typeFilter = arguments?["type_filter"]?.Value<string>() ?? "all";
                string pathFilter = arguments?["path_filter"]?.Value<string>() ?? "";
                int maxResults = arguments?["max_results"]?.Value<int>() ?? k_DefaultMaxResults;
                bool includePackages = arguments?["include_packages"]?.Value<bool>() ?? false;

                if (string.IsNullOrWhiteSpace(query))
                {
                    return McpToolResult.Error("Query is required");
                }

                try
                {
                    var results = SearchAssets(query, typeFilter, pathFilter, maxResults, includePackages);
                    return McpToolResult.Success(FormatResults(results, query));
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Asset search failed: {ex.Message}");
                }
            });
        }

        private List<AssetSearchResult> SearchAssets(string query, string typeFilter, string pathFilter, int maxResults, bool includePackages)
        {
            var results = new List<AssetSearchResult>();

            // Build AssetDatabase search filter
            string searchFilter = BuildSearchFilter(query, typeFilter);

            // Search folders
            string[] searchFolders = null;
            if (!string.IsNullOrEmpty(pathFilter))
            {
                searchFolders = new[] { pathFilter };
            }
            else if (!includePackages)
            {
                searchFolders = new[] { "Assets" };
            }

            // Find assets using AssetDatabase
            string[] guids = searchFolders != null
                ? AssetDatabase.FindAssets(searchFilter, searchFolders)
                : AssetDatabase.FindAssets(searchFilter);

            // Parse query words for fuzzy matching
            var queryWords = query.ToLower().Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Skip packages if not included
                if (!includePackages && path.StartsWith("Packages/"))
                {
                    continue;
                }

                // Calculate fuzzy match score
                string fileName = Path.GetFileNameWithoutExtension(path);
                float score = CalculateFuzzyScore(fileName, queryWords);

                if (score > 0)
                {
                    var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

                    results.Add(new AssetSearchResult
                    {
                        Path = path,
                        Name = fileName,
                        Type = assetType?.Name ?? "Unknown",
                        GUID = guid,
                        Score = score,
                        FileSize = GetFileSize(path)
                    });
                }
            }

            // Sort by score (descending) and take top results
            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Name.Length)
                .Take(maxResults)
                .ToList();
        }

        private string BuildSearchFilter(string query, string typeFilter)
        {
            var filterParts = new List<string>();

            // Add type filter
            string typeSpecifier = typeFilter.ToLower() switch
            {
                "prefab" => "t:Prefab",
                "texture" => "t:Texture",
                "material" => "t:Material",
                "script" => "t:Script",
                "scene" => "t:Scene",
                "audio" => "t:AudioClip",
                "model" => "t:Model",
                "shader" => "t:Shader",
                "animation" => "t:AnimationClip",
                _ => ""
            };

            if (!string.IsNullOrEmpty(typeSpecifier))
            {
                filterParts.Add(typeSpecifier);
            }

            // Add query words
            var queryWords = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            filterParts.AddRange(queryWords);

            return string.Join(" ", filterParts);
        }

        private float CalculateFuzzyScore(string text, string[] queryWords)
        {
            if (string.IsNullOrEmpty(text) || queryWords.Length == 0)
            {
                return 0;
            }

            text = text.ToLower();
            float totalScore = 0;
            int matchedWords = 0;

            foreach (var word in queryWords)
            {
                if (text.Contains(word))
                {
                    matchedWords++;

                    // Bonus for exact word boundary match
                    if (text == word || text.StartsWith(word + "_") || text.EndsWith("_" + word) ||
                        text.Contains("_" + word + "_"))
                    {
                        totalScore += 2.0f;
                    }
                    // Bonus for prefix match
                    else if (text.StartsWith(word))
                    {
                        totalScore += 1.5f;
                    }
                    // Regular substring match
                    else
                    {
                        totalScore += 1.0f;
                    }
                }
                else
                {
                    // Try fuzzy character matching
                    float charMatchRatio = CalculateCharacterMatchRatio(text, word);
                    if (charMatchRatio > 0.6f)
                    {
                        matchedWords++;
                        totalScore += charMatchRatio * 0.5f;
                    }
                }
            }

            // All words must match for a valid result
            if (matchedWords < queryWords.Length)
            {
                return 0;
            }

            // Bonus for shorter names (more specific matches)
            float lengthBonus = Math.Max(0, 1.0f - (text.Length / 50.0f));
            totalScore += lengthBonus;

            return totalScore;
        }

        private float CalculateCharacterMatchRatio(string text, string query)
        {
            int matchedChars = 0;
            int queryIndex = 0;

            foreach (char c in text)
            {
                if (queryIndex < query.Length && c == query[queryIndex])
                {
                    matchedChars++;
                    queryIndex++;
                }
            }

            return (float)matchedChars / query.Length;
        }

        private long GetFileSize(string assetPath)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), assetPath);
                if (File.Exists(fullPath))
                {
                    return new FileInfo(fullPath).Length;
                }
            }
            catch
            {
                // Ignore file access errors
            }
            return 0;
        }

        private string FormatResults(List<AssetSearchResult> results, string query)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Asset Search Results for \"{query}\"");
            sb.AppendLine();

            if (results.Count == 0)
            {
                sb.AppendLine("No assets found matching the query.");
                sb.AppendLine();
                sb.AppendLine("**Suggestions:**");
                sb.AppendLine("- Try using different keywords");
                sb.AppendLine("- Check the type_filter parameter");
                sb.AppendLine("- Verify the path_filter is correct");
                return sb.ToString();
            }

            sb.AppendLine($"Found {results.Count} asset(s):");
            sb.AppendLine();

            // Group by type
            var grouped = results.GroupBy(r => r.Type).OrderByDescending(g => g.Count());

            foreach (var group in grouped)
            {
                sb.AppendLine($"## {group.Key} ({group.Count()})");
                foreach (var result in group.OrderByDescending(r => r.Score))
                {
                    string sizeStr = result.FileSize > 0 ? $" ({FormatFileSize(result.FileSize)})" : "";
                    sb.AppendLine($"- **{result.Name}**{sizeStr}");
                    sb.AppendLine($"  - Path: `{result.Path}`");
                    sb.AppendLine($"  - GUID: `{result.GUID}`");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private class AssetSearchResult
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string GUID { get; set; }
            public float Score { get; set; }
            public long FileSize { get; set; }
        }
    }

    /// <summary>
    /// Instantiates a prefab or asset into the scene.
    /// </summary>
    [McpTool("instantiate_asset")]
    public class InstantiateAssetTool : IMcpTool
    {
        public string Name => "instantiate_asset";
        public string Description => "Instantiate a prefab or asset into the current scene. " +
            "Supports positioning via coordinates or raycast to floor.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""asset_path"": {
                    ""type"": ""string"",
                    ""description"": ""Path to the asset (e.g., 'Assets/Prefabs/Enemy.prefab')""
                },
                ""guid"": {
                    ""type"": ""string"",
                    ""description"": ""Asset GUID (alternative to path)""
                },
                ""position"": {
                    ""type"": ""object"",
                    ""description"": ""World position for the instance"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    }
                },
                ""rotation"": {
                    ""type"": ""object"",
                    ""description"": ""Euler rotation for the instance"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    }
                },
                ""parent_selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector for parent object""
                },
                ""place_on_floor"": {
                    ""type"": ""boolean"",
                    ""description"": ""Use raycast to place on floor/terrain"",
                    ""default"": false
                },
                ""instance_name"": {
                    ""type"": ""string"",
                    ""description"": ""Custom name for the instance""
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string assetPath = arguments?["asset_path"]?.Value<string>();
                string guid = arguments?["guid"]?.Value<string>();
                var positionObj = arguments?["position"];
                var rotationObj = arguments?["rotation"];
                string parentSelector = arguments?["parent_selector"]?.Value<string>();
                bool placeOnFloor = arguments?["place_on_floor"]?.Value<bool>() ?? false;
                string instanceName = arguments?["instance_name"]?.Value<string>();

                // Resolve asset path from GUID if provided
                if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
                {
                    assetPath = AssetDatabase.GUIDToAssetPath(guid);
                }

                if (string.IsNullOrEmpty(assetPath))
                {
                    return McpToolResult.Error("Either asset_path or guid is required");
                }

                try
                {
                    // Load the asset
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (asset == null)
                    {
                        return McpToolResult.Error($"Asset not found at path: {assetPath}");
                    }

                    // Parse position
                    Vector3 position = Vector3.zero;
                    if (positionObj != null)
                    {
                        position = new Vector3(
                            positionObj["x"]?.Value<float>() ?? 0,
                            positionObj["y"]?.Value<float>() ?? 0,
                            positionObj["z"]?.Value<float>() ?? 0
                        );
                    }

                    // Parse rotation
                    Quaternion rotation = Quaternion.identity;
                    if (rotationObj != null)
                    {
                        rotation = Quaternion.Euler(
                            rotationObj["x"]?.Value<float>() ?? 0,
                            rotationObj["y"]?.Value<float>() ?? 0,
                            rotationObj["z"]?.Value<float>() ?? 0
                        );
                    }

                    // Place on floor using raycast
                    if (placeOnFloor)
                    {
                        position = FindFloorPosition(position);
                    }

                    // Find parent if specified
                    Transform parent = null;
                    if (!string.IsNullOrEmpty(parentSelector))
                    {
                        var parentObj = SelectorEngine.QueryOne(parentSelector, false);
                        if (parentObj != null)
                        {
                            parent = parentObj.transform;
                        }
                    }

                    // Instantiate
                    GameObject instance;
                    if (asset is GameObject prefab)
                    {
                        instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        instance.transform.position = position;
                        instance.transform.rotation = rotation;
                    }
                    else
                    {
                        instance = new GameObject(asset.name);
                        instance.transform.position = position;
                        instance.transform.rotation = rotation;
                    }

                    if (parent != null)
                    {
                        instance.transform.SetParent(parent, true);
                    }

                    if (!string.IsNullOrEmpty(instanceName))
                    {
                        instance.name = instanceName;
                    }

                    // Register undo
                    Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {asset.name}");

                    // Select the new instance
                    Selection.activeGameObject = instance;

                    return McpToolResult.Success(
                        $"Instantiated '{instance.name}' at position {position}\n" +
                        $"- Path: {GetHierarchyPath(instance)}\n" +
                        $"- Instance ID: {instance.GetInstanceID()}");
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Instantiation failed: {ex.Message}");
                }
            });
        }

        private Vector3 FindFloorPosition(Vector3 startPosition)
        {
            // Start from above and raycast down
            Vector3 rayStart = startPosition + Vector3.up * 100f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f))
            {
                return hit.point;
            }

            // Fallback to original Y=0
            return new Vector3(startPosition.x, 0, startPosition.z);
        }

        private string GetHierarchyPath(GameObject obj)
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
}
