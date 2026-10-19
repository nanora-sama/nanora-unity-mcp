using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("get_scene_hierarchy")]
    public class GetSceneHierarchyTool : IMcpTool
    {
        public string Name => "get_scene_hierarchy";
        public string Description => "Gets the GameObject hierarchy of the active scene as indented text: scene name, path, root count, then one line per object with [inactive]/[prefab] markers and its component type names. " +
            "Includes inactive objects unless include_inactive=false; max_depth limits traversal (0 = unlimited). " +
            "Covers the active scene only. For component details of one object use get_object_details; to find objects by selector use query_objects.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""include_inactive"": {
                    ""type"": ""boolean"",
                    ""description"": ""Whether to include inactive GameObjects"",
                    ""default"": true
                },
                ""max_depth"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum depth to traverse (0 = unlimited)"",
                    ""default"": 0
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                bool includeInactive = arguments?["include_inactive"]?.Value<bool>() ?? true;
                int maxDepth = arguments?["max_depth"]?.Value<int>() ?? 0;

                var sb = new StringBuilder();
                var scene = SceneManager.GetActiveScene();
                sb.AppendLine($"Scene: {scene.name}");
                sb.AppendLine($"Path: {scene.path}");
                sb.AppendLine($"Root GameObjects: {scene.rootCount}");
                sb.AppendLine();

                var rootObjects = scene.GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    if (!includeInactive && !root.activeInHierarchy)
                        continue;

                    BuildHierarchy(sb, root, 0, maxDepth, includeInactive);
                }

                return McpToolResult.Success(sb.ToString());
            });
        }

        private void BuildHierarchy(StringBuilder sb, GameObject obj, int depth, int maxDepth, bool includeInactive)
        {
            if (maxDepth > 0 && depth >= maxDepth)
                return;

            var indent = new string(' ', depth * 2);
            var activeMarker = obj.activeSelf ? "" : " [inactive]";
            var prefabMarker = PrefabUtility.IsPartOfPrefabInstance(obj) ? " [prefab]" : "";
            var components = obj.GetComponents<Component>();
            var componentNames = string.Join(", ", System.Array.ConvertAll(components, c => c?.GetType().Name ?? "null"));

            sb.AppendLine($"{indent}- {obj.name}{activeMarker}{prefabMarker} ({componentNames})");

            foreach (Transform child in obj.transform)
            {
                if (!includeInactive && !child.gameObject.activeInHierarchy)
                    continue;

                BuildHierarchy(sb, child.gameObject, depth + 1, maxDepth, includeInactive);
            }
        }
    }
}
