using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("manage_scene")]
    public class SceneManagementTool : IMcpTool
    {
        public string Name => "manage_scene";
        public string Description => "Manage scene files: new, save, save_as, load, or info about the active scene (name, path, dirty state, root objects). " +
            "new and load replace all open scenes (single mode). Before replacing, if the active scene has unsaved changes, " +
            "save_current=true (the default) saves it to its existing file without asking, and save_current=false discards those changes; " +
            "an untitled scene's changes are discarded either way. Check the dirty state with info first when unsaved changes matter.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""new"", ""save"", ""save_as"", ""load"", ""info""],
                    ""description"": ""Action to perform: new (create new scene), save (save current), save_as (save to path), load (open scene), info (get current scene info)""
                },
                ""path"": {
                    ""type"": ""string"",
                    ""description"": ""Scene path (e.g., 'Assets/Scenes/MyScene.unity'). Required for save_as and load actions""
                },
                ""save_current"": {
                    ""type"": ""boolean"",
                    ""description"": ""For new and load: true saves the dirty active scene to its file first; false discards its unsaved changes"",
                    ""default"": true
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
                var saveCurrent = arguments?["save_current"]?.Value<bool>() ?? true;

                if (string.IsNullOrEmpty(action))
                {
                    return McpToolResult.Error("Action is required");
                }

                switch (action.ToLower())
                {
                    case "new":
                        return CreateNewScene(saveCurrent);
                    case "save":
                        return SaveCurrentScene();
                    case "save_as":
                        return SaveSceneAs(path);
                    case "load":
                        return LoadScene(path, saveCurrent);
                    case "info":
                        return GetSceneInfo();
                    default:
                        return McpToolResult.Error($"Unknown action: {action}. Valid actions: new, save, save_as, load, info");
                }
            });
        }

        private McpToolResult CreateNewScene(bool saveCurrent)
        {
            var currentScene = SceneManager.GetActiveScene();

            if (saveCurrent && currentScene.isDirty)
            {
                if (!string.IsNullOrEmpty(currentScene.path))
                {
                    EditorSceneManager.SaveScene(currentScene);
                }
            }

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            return McpToolResult.Success($"Created new scene.\nThe scene is unsaved. Use 'save_as' with a path to save it.\nDefault objects: Main Camera, Directional Light");
        }

        private McpToolResult SaveCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();

            if (string.IsNullOrEmpty(scene.path))
            {
                return McpToolResult.Error("Scene has no path. Use 'save_as' action with a path to save a new scene.");
            }

            bool success = EditorSceneManager.SaveScene(scene);

            if (success)
            {
                return McpToolResult.Success($"Saved scene: {scene.path}");
            }
            else
            {
                return McpToolResult.Error("Failed to save scene");
            }
        }

        private McpToolResult SaveSceneAs(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return McpToolResult.Error("Path is required for save_as action");
            }

            // Ensure path starts with Assets/
            if (!path.StartsWith("Assets/"))
            {
                path = "Assets/" + path;
            }

            // Ensure .unity extension
            if (!path.EndsWith(".unity"))
            {
                path += ".unity";
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var scene = SceneManager.GetActiveScene();
            bool success = EditorSceneManager.SaveScene(scene, path);

            if (success)
            {
                return McpToolResult.Success($"Saved scene to: {path}");
            }
            else
            {
                return McpToolResult.Error($"Failed to save scene to: {path}");
            }
        }

        private McpToolResult LoadScene(string path, bool saveCurrent)
        {
            if (string.IsNullOrEmpty(path))
            {
                return McpToolResult.Error("Path is required for load action");
            }

            // Ensure path starts with Assets/
            if (!path.StartsWith("Assets/"))
            {
                path = "Assets/" + path;
            }

            // Check if scene exists
            if (!File.Exists(path))
            {
                return McpToolResult.Error($"Scene not found: {path}");
            }

            var currentScene = SceneManager.GetActiveScene();

            if (saveCurrent && currentScene.isDirty && !string.IsNullOrEmpty(currentScene.path))
            {
                EditorSceneManager.SaveScene(currentScene);
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            if (scene.IsValid())
            {
                return McpToolResult.Success($"Loaded scene: {scene.name}\nPath: {scene.path}\nRoot objects: {scene.rootCount}");
            }
            else
            {
                return McpToolResult.Error($"Failed to load scene: {path}");
            }
        }

        private McpToolResult GetSceneInfo()
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder();

            sb.AppendLine("=== Current Scene ===");
            sb.AppendLine($"Name: {(string.IsNullOrEmpty(scene.name) ? "(Untitled)" : scene.name)}");
            sb.AppendLine($"Path: {(string.IsNullOrEmpty(scene.path) ? "(Not saved)" : scene.path)}");
            sb.AppendLine($"Is Dirty: {scene.isDirty}");
            sb.AppendLine($"Is Loaded: {scene.isLoaded}");
            sb.AppendLine($"Root Objects: {scene.rootCount}");
            sb.AppendLine($"Build Index: {scene.buildIndex}");

            // List loaded scenes
            var sceneCount = SceneManager.sceneCount;
            if (sceneCount > 1)
            {
                sb.AppendLine();
                sb.AppendLine($"=== All Loaded Scenes ({sceneCount}) ===");
                for (int i = 0; i < sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    var active = s == scene ? " [ACTIVE]" : "";
                    sb.AppendLine($"  - {s.name}{active} ({s.path})");
                }
            }

            // List available scenes in project
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            if (sceneGuids.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== Available Scenes ({sceneGuids.Length}) ===");
                foreach (var guid in sceneGuids)
                {
                    var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    var current = scenePath == scene.path ? " [CURRENT]" : "";
                    sb.AppendLine($"  - {scenePath}{current}");
                }
            }

            return McpToolResult.Success(sb.ToString());
        }
    }
}
