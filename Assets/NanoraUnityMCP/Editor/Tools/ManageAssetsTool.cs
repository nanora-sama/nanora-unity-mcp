using System;
using System.IO;
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
    /// MCP tool for managing Unity assets: get info, create folders,
    /// delete, move, rename, duplicate, and import external files.
    /// </summary>
    [McpTool("manage_assets")]
    public class ManageAssetsTool : IMcpTool
    {
        public string Name => "manage_assets";
        public string Description =>
            "Manage Unity project assets: get detailed info, create folders, " +
            "delete, move, rename, duplicate assets, and import external files.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""get_info"", ""create_folder"", ""delete"", ""move"", ""rename"", ""duplicate"", ""import""],
                    ""description"": ""Action to perform on the asset""
                },
                ""path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path (e.g. 'Assets/Materials/MyMat.mat')""
                },
                ""new_path"": {
                    ""type"": ""string"",
                    ""description"": ""Destination path for move, or new name for rename""
                },
                ""parent_folder"": {
                    ""type"": ""string"",
                    ""description"": ""Parent folder path for create_folder (e.g. 'Assets/Textures')""
                },
                ""folder_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the new folder for create_folder""
                },
                ""source_path"": {
                    ""type"": ""string"",
                    ""description"": ""External file path for import action""
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                var action = arguments?["action"]?.Value<string>();
                var path = arguments?["path"]?.Value<string>();
                var newPath = arguments?["new_path"]?.Value<string>();
                var parentFolder = arguments?["parent_folder"]?.Value<string>();
                var folderName = arguments?["folder_name"]?.Value<string>();
                var sourcePath = arguments?["source_path"]?.Value<string>();

                if (string.IsNullOrEmpty(action))
                    return McpToolResult.Error("action is required");

                try
                {
                    switch (action.ToLower())
                    {
                        case "get_info":
                            return GetAssetInfo(path);
                        case "create_folder":
                            return CreateFolder(parentFolder, folderName);
                        case "delete":
                            return DeleteAsset(path);
                        case "move":
                            return MoveAsset(path, newPath);
                        case "rename":
                            return RenameAsset(path, newPath);
                        case "duplicate":
                            return DuplicateAsset(path, newPath);
                        case "import":
                            return ImportAsset(sourcePath, path);
                        default:
                            return McpToolResult.Error(
                                $"Unknown action: {action}. Valid: get_info, create_folder, delete, move, rename, duplicate, import");
                    }
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Asset operation failed: {ex.Message}");
                }
            });
        }

        // ----------------------------------------------------------------
        // Action handlers
        // ----------------------------------------------------------------

        private McpToolResult GetAssetInfo(string path)
        {
            if (string.IsNullOrEmpty(path))
                return McpToolResult.Error("path is required for get_info");

            if (!AssetExists(path))
                return McpToolResult.Error($"Asset not found: {path}");

            var sb = new StringBuilder();
            sb.AppendLine($"=== Asset Info ===");
            sb.AppendLine($"Path: {path}");

            var guid = AssetDatabase.AssetPathToGUID(path);
            sb.AppendLine($"GUID: {guid}");

            var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            sb.AppendLine($"Type: {mainType?.Name ?? "Unknown"}");

            // Labels
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                var labels = AssetDatabase.GetLabels(asset);
                if (labels.Length > 0)
                    sb.AppendLine($"Labels: {string.Join(", ", labels)}");

                sb.AppendLine($"Name: {asset.name}");
                sb.AppendLine($"Hide Flags: {asset.hideFlags}");
            }

            // Is folder?
            bool isFolder = AssetDatabase.IsValidFolder(path);
            sb.AppendLine($"Is Folder: {isFolder}");

            if (isFolder)
            {
                var subAssets = AssetDatabase.FindAssets("", new[] { path });
                // Count only direct children (rough approximation)
                int childCount = 0;
                foreach (var subGuid in subAssets)
                {
                    var subPath = AssetDatabase.GUIDToAssetPath(subGuid);
                    if (subPath != path)
                    {
                        var relative = subPath.Substring(path.Length + 1);
                        if (!relative.Contains("/"))
                            childCount++;
                    }
                }
                sb.AppendLine($"Direct children: {childCount}");
                sb.AppendLine($"Total assets (recursive): {subAssets.Length}");
            }
            else
            {
                // File size
                var fullPath = Path.Combine(
                    Application.dataPath.Replace("/Assets", ""), path);
                if (File.Exists(fullPath))
                {
                    var info = new FileInfo(fullPath);
                    sb.AppendLine($"File Size: {FormatFileSize(info.Length)}");
                    sb.AppendLine($"Last Modified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                }

                // Dependencies
                var deps = AssetDatabase.GetDependencies(path, false);
                if (deps.Length > 1)
                {
                    sb.AppendLine();
                    sb.AppendLine($"--- Direct Dependencies ({deps.Length - 1}) ---");
                    int shown = 0;
                    foreach (var dep in deps)
                    {
                        if (dep != path)
                        {
                            sb.AppendLine($"  - {dep}");
                            if (++shown >= 30) { sb.AppendLine("  ... (truncated)"); break; }
                        }
                    }
                }

                // Sub-assets
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (subAssets != null && subAssets.Length > 1)
                {
                    sb.AppendLine();
                    sb.AppendLine($"--- Sub-Assets ({subAssets.Length - 1}) ---");
                    int shown = 0;
                    foreach (var sub in subAssets)
                    {
                        if (sub != asset && sub != null)
                        {
                            sb.AppendLine($"  - {sub.name} ({sub.GetType().Name})");
                            if (++shown >= 20) { sb.AppendLine("  ... (truncated)"); break; }
                        }
                    }
                }

                // Importer info
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Importer: {importer.GetType().Name}");
                    if (!string.IsNullOrEmpty(importer.userData))
                        sb.AppendLine($"User Data: {importer.userData}");
                    if (!string.IsNullOrEmpty(importer.assetBundleName))
                        sb.AppendLine($"Asset Bundle: {importer.assetBundleName}");
                }
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult CreateFolder(string parentFolder, string folderName)
        {
            if (string.IsNullOrEmpty(parentFolder))
                return McpToolResult.Error("parent_folder is required for create_folder");
            if (string.IsNullOrEmpty(folderName))
                return McpToolResult.Error("folder_name is required for create_folder");

            if (!AssetDatabase.IsValidFolder(parentFolder))
                return McpToolResult.Error($"Parent folder does not exist: {parentFolder}");

            var newFolderPath = parentFolder + "/" + folderName;
            if (AssetDatabase.IsValidFolder(newFolderPath))
                return McpToolResult.Error($"Folder already exists: {newFolderPath}");

            var guid = AssetDatabase.CreateFolder(parentFolder, folderName);
            if (string.IsNullOrEmpty(guid))
                return McpToolResult.Error($"Failed to create folder: {newFolderPath}");

            return McpToolResult.Success(
                $"Created folder\n" +
                $"- Path: {newFolderPath}\n" +
                $"- GUID: {guid}");
        }

        private McpToolResult DeleteAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return McpToolResult.Error("path is required for delete");

            if (!AssetExists(path))
                return McpToolResult.Error($"Asset not found: {path}");

            // Safety: prevent deleting root folders
            if (path == "Assets" || path == "Packages")
                return McpToolResult.Error("Cannot delete root folders");

            bool success = AssetDatabase.DeleteAsset(path);

            if (!success)
                return McpToolResult.Error($"Failed to delete asset: {path}");

            return McpToolResult.Success($"Deleted asset: {path}");
        }

        private McpToolResult MoveAsset(string path, string newPath)
        {
            if (string.IsNullOrEmpty(path))
                return McpToolResult.Error("path is required for move");
            if (string.IsNullOrEmpty(newPath))
                return McpToolResult.Error("new_path is required for move");

            if (!AssetExists(path))
                return McpToolResult.Error($"Asset not found: {path}");

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(destDir) && !AssetDatabase.IsValidFolder(destDir))
                return McpToolResult.Error($"Destination folder does not exist: {destDir}");

            var error = AssetDatabase.MoveAsset(path, newPath);
            if (!string.IsNullOrEmpty(error))
                return McpToolResult.Error($"Move failed: {error}");

            return McpToolResult.Success(
                $"Moved asset\n" +
                $"- From: {path}\n" +
                $"- To: {newPath}");
        }

        private McpToolResult RenameAsset(string path, string newName)
        {
            if (string.IsNullOrEmpty(path))
                return McpToolResult.Error("path is required for rename");
            if (string.IsNullOrEmpty(newName))
                return McpToolResult.Error("new_path (new name) is required for rename");

            if (!AssetExists(path))
                return McpToolResult.Error($"Asset not found: {path}");

            var error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(error))
                return McpToolResult.Error($"Rename failed: {error}");

            // Compute the resulting path
            var dir = Path.GetDirectoryName(path);
            var ext = Path.GetExtension(path);
            var resultPath = string.IsNullOrEmpty(dir)
                ? newName + ext
                : dir + "/" + newName + ext;

            return McpToolResult.Success(
                $"Renamed asset\n" +
                $"- From: {path}\n" +
                $"- To: {resultPath}");
        }

        private McpToolResult DuplicateAsset(string path, string newPath)
        {
            if (string.IsNullOrEmpty(path))
                return McpToolResult.Error("path is required for duplicate");

            if (!AssetExists(path))
                return McpToolResult.Error($"Asset not found: {path}");

            // Auto-generate new_path if not specified
            if (string.IsNullOrEmpty(newPath))
            {
                var dir = Path.GetDirectoryName(path);
                var name = Path.GetFileNameWithoutExtension(path);
                var ext = Path.GetExtension(path);
                newPath = (string.IsNullOrEmpty(dir) ? "" : dir + "/") + name + "_copy" + ext;
            }

            bool success = AssetDatabase.CopyAsset(path, newPath);
            if (!success)
                return McpToolResult.Error($"Failed to duplicate asset to: {newPath}");

            return McpToolResult.Success(
                $"Duplicated asset\n" +
                $"- Source: {path}\n" +
                $"- Copy: {newPath}\n" +
                $"- GUID: {AssetDatabase.AssetPathToGUID(newPath)}");
        }

        private McpToolResult ImportAsset(string sourcePath, string destPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                return McpToolResult.Error("source_path is required for import");
            if (string.IsNullOrEmpty(destPath))
                return McpToolResult.Error("path (destination) is required for import");

            if (!File.Exists(sourcePath))
                return McpToolResult.Error($"Source file not found: {sourcePath}");

            // Ensure destination starts with Assets/
            if (!destPath.StartsWith("Assets/"))
                destPath = "Assets/" + destPath;

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir) && !AssetDatabase.IsValidFolder(destDir))
                return McpToolResult.Error($"Destination folder does not exist: {destDir}");

            // Copy file to project
            var fullDestPath = Path.Combine(
                Application.dataPath.Replace("/Assets", ""), destPath);
            File.Copy(sourcePath, fullDestPath, overwrite: false);

            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

            return McpToolResult.Success(
                $"Imported asset\n" +
                $"- Source: {sourcePath}\n" +
                $"- Destination: {destPath}\n" +
                $"- GUID: {AssetDatabase.AssetPathToGUID(destPath)}");
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static bool AssetExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // Valid folder counts as existing
            if (AssetDatabase.IsValidFolder(path)) return true;
            // Check GUID (works for files)
            var guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid);
        }

        private static string FormatFileSize(long bytes)
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
    }
}
