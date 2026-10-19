using System;
using System.Collections.Generic;
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
    /// MCP tool for managing Unity Prefabs: create, apply, unpack, save,
    /// create variants, revert overrides, and inspect prefab information.
    /// </summary>
    [McpTool("manage_prefabs")]
    public class ManagePrefabsTool : IMcpTool
    {
        public string Name => "manage_prefabs";
        public string Description =>
            "Manage Unity Prefabs: create from GameObject, apply/revert instance overrides, " +
            "unpack prefab instances, save prefab assets, create variants, and get prefab info.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""create"", ""apply"", ""unpack"", ""save"", ""create_variant"", ""revert"", ""get_info""],
                    ""description"": ""Action to perform on the prefab""
                },
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector to identify the target GameObject in the scene""
                },
                ""path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path for saving the prefab (required for create/create_variant, e.g. 'Assets/Prefabs/MyPrefab.prefab')""
                },
                ""unpack_mode"": {
                    ""type"": ""string"",
                    ""enum"": [""completely"", ""root""],
                    ""description"": ""Unpack mode: 'root' unpacks only the outermost prefab, 'completely' unpacks all nested prefabs"",
                    ""default"": ""root""
                }
            },
            ""required"": [""action"", ""selector""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                var action = arguments?["action"]?.Value<string>();
                var selector = arguments?["selector"]?.Value<string>();
                var path = arguments?["path"]?.Value<string>();
                var unpackMode = arguments?["unpack_mode"]?.Value<string>() ?? "root";

                if (string.IsNullOrEmpty(action))
                    return McpToolResult.Error("action is required");

                if (string.IsNullOrEmpty(selector))
                    return McpToolResult.Error("selector is required");

                try
                {
                    // QueryOne throws for missing or ambiguous objects. Report these
                    // failures through the same MCP error boundary as the operation.
                    var target = SelectorEngine.QueryOne(selector, false);
                    switch (action.ToLower())
                    {
                        case "create":
                            return CreatePrefab(target, path);
                        case "apply":
                            return ApplyPrefab(target);
                        case "unpack":
                            return UnpackPrefab(target, unpackMode);
                        case "save":
                            return SavePrefab(target);
                        case "create_variant":
                            return CreateVariant(target, path);
                        case "revert":
                            return RevertPrefab(target);
                        case "get_info":
                            return GetPrefabInfo(target);
                        default:
                            return McpToolResult.Error(
                                $"Unknown action: {action}. Valid: create, apply, unpack, save, create_variant, revert, get_info");
                    }
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Prefab operation failed: {ex.Message}");
                }
            });
        }

        // ----------------------------------------------------------------
        // Action handlers
        // ----------------------------------------------------------------

        private McpToolResult CreatePrefab(GameObject target, string path)
        {
            if (string.IsNullOrEmpty(path))
                return McpToolResult.Error("path is required for the 'create' action");

            path = NormalizePrefabPath(path);

            // Ensure the directory exists
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                CreateFolderRecursive(dir);
            }

            Undo.RecordObject(target, "Create Prefab");

            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAsset(target, path, out success);

            if (!success || prefab == null)
                return McpToolResult.Error($"Failed to create prefab at: {path}");

            return McpToolResult.Success(
                $"Created prefab from '{target.name}'\n" +
                $"- Path: {path}\n" +
                $"- GUID: {AssetDatabase.AssetPathToGUID(path)}");
        }

        private McpToolResult ApplyPrefab(GameObject target)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(target))
                return McpToolResult.Error($"'{target.name}' is not a prefab instance");

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
            if (root == null)
                return McpToolResult.Error($"Could not find prefab instance root for '{target.name}'");

            Undo.RegisterCompleteObjectUndo(root, "Apply Prefab Instance");
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            return McpToolResult.Success(
                $"Applied overrides from '{root.name}' to prefab\n" +
                $"- Prefab path: {assetPath}");
        }

        private McpToolResult UnpackPrefab(GameObject target, string unpackMode)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(target))
                return McpToolResult.Error($"'{target.name}' is not a prefab instance");

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
            if (root == null)
                return McpToolResult.Error($"Could not find prefab instance root for '{target.name}'");

            var mode = unpackMode == "completely"
                ? PrefabUnpackMode.Completely
                : PrefabUnpackMode.OutermostRoot;

            Undo.RegisterCompleteObjectUndo(root, "Unpack Prefab Instance");
            PrefabUtility.UnpackPrefabInstance(root, mode, InteractionMode.AutomatedAction);

            return McpToolResult.Success(
                $"Unpacked prefab instance '{root.name}'\n" +
                $"- Mode: {unpackMode}");
        }

        private McpToolResult SavePrefab(GameObject target)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(target))
                return McpToolResult.Error($"'{target.name}' is not a prefab instance");

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            if (string.IsNullOrEmpty(assetPath))
                return McpToolResult.Error($"Could not determine prefab asset path for '{target.name}'");

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
            Undo.RegisterCompleteObjectUndo(root, "Save Prefab");
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);

            return McpToolResult.Success(
                $"Saved prefab '{root.name}'\n" +
                $"- Path: {assetPath}");
        }

        private McpToolResult CreateVariant(GameObject target, string path)
        {
            if (string.IsNullOrEmpty(path))
                return McpToolResult.Error("path is required for the 'create_variant' action");

            if (!PrefabUtility.IsPartOfPrefabInstance(target))
                return McpToolResult.Error($"'{target.name}' is not a prefab instance; create a base prefab first");

            path = NormalizePrefabPath(path);

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                CreateFolderRecursive(dir);
            }

            Undo.RecordObject(target, "Create Prefab Variant");

            bool success;
            var variant = PrefabUtility.SaveAsPrefabAsset(target, path, out success);

            if (!success || variant == null)
                return McpToolResult.Error($"Failed to create prefab variant at: {path}");

            return McpToolResult.Success(
                $"Created prefab variant from '{target.name}'\n" +
                $"- Path: {path}\n" +
                $"- GUID: {AssetDatabase.AssetPathToGUID(path)}");
        }

        private McpToolResult RevertPrefab(GameObject target)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(target))
                return McpToolResult.Error($"'{target.name}' is not a prefab instance");

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
            if (root == null)
                return McpToolResult.Error($"Could not find prefab instance root for '{target.name}'");

            Undo.RegisterCompleteObjectUndo(root, "Revert Prefab Instance");
            PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);

            return McpToolResult.Success($"Reverted all overrides on '{root.name}'");
        }

        private McpToolResult GetPrefabInfo(GameObject target)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Prefab Info: {target.name} ===");

            bool isPartOfPrefab = PrefabUtility.IsPartOfPrefabInstance(target);
            bool isPartOfAsset = PrefabUtility.IsPartOfPrefabAsset(target);
            var prefabType = PrefabUtility.GetPrefabAssetType(target);
            var instanceStatus = PrefabUtility.GetPrefabInstanceStatus(target);

            sb.AppendLine($"Is Prefab Instance: {isPartOfPrefab}");
            sb.AppendLine($"Is Prefab Asset: {isPartOfAsset}");
            sb.AppendLine($"Prefab Type: {prefabType}");
            sb.AppendLine($"Instance Status: {instanceStatus}");

            if (isPartOfPrefab)
            {
                var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
                sb.AppendLine($"Source Prefab: {assetPath}");

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
                if (root != null)
                    sb.AppendLine($"Outermost Root: {root.name}");

                // List overrides
                var overrides = PrefabUtility.GetObjectOverrides(root ?? target);
                var addedComponents = PrefabUtility.GetAddedComponents(root ?? target);
                var removedComponents = PrefabUtility.GetRemovedComponents(root ?? target);
                var addedObjects = PrefabUtility.GetAddedGameObjects(root ?? target);

                sb.AppendLine();
                sb.AppendLine("--- Overrides ---");
                sb.AppendLine($"Property overrides: {overrides.Count}");
                sb.AppendLine($"Added components: {addedComponents.Count}");
                sb.AppendLine($"Removed components: {removedComponents.Count}");
                sb.AppendLine($"Added GameObjects: {addedObjects.Count}");

                if (overrides.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Property override targets:");
                    int shown = 0;
                    foreach (var ov in overrides)
                    {
                        if (ov.instanceObject != null)
                        {
                            sb.AppendLine($"  - {ov.instanceObject.name} ({ov.instanceObject.GetType().Name})");
                            if (++shown >= 20) { sb.AppendLine("  ... (truncated)"); break; }
                        }
                    }
                }

                if (addedComponents.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Added components:");
                    foreach (var ac in addedComponents)
                    {
                        if (ac.instanceComponent != null)
                            sb.AppendLine($"  - {ac.instanceComponent.GetType().Name} on {ac.instanceComponent.gameObject.name}");
                    }
                }

                if (addedObjects.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Added GameObjects:");
                    foreach (var ao in addedObjects)
                    {
                        if (ao.instanceGameObject != null)
                            sb.AppendLine($"  - {ao.instanceGameObject.name}");
                    }
                }
            }

            return McpToolResult.Success(sb.ToString());
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string NormalizePrefabPath(string path)
        {
            if (!path.StartsWith("Assets/"))
                path = "Assets/" + path;
            if (!path.EndsWith(".prefab"))
                path += ".prefab";
            return path;
        }

        private static void CreateFolderRecursive(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = System.IO.Path.GetDirectoryName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursive(parent);
            }

            var folderName = System.IO.Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
