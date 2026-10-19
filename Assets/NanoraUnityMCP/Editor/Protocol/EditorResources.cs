using System;
using System.Linq;
using System.Threading.Tasks;
using NanoraUnityMCP.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NanoraUnityMCP.Protocol
{
    /// <summary>Small read-only snapshots, available without executing arbitrary C#.</summary>
    internal static class EditorResources
    {
        public static object List() => new { resources = new[]
        {
            new { uri = "unity://project/info", name = "project_info", mimeType = "application/json",
                description = "Project identity, Unity/MCP version and available tool names." },
            new { uri = "unity://editor/state", name = "editor_state", mimeType = "application/json",
                description = "Live compile/play state and loaded scenes, including unsaved changes." }
        }};

        public static Task<JsonRpcResponse> ReadAsync(JsonRpcRequest request)
        {
            var uri = (string)request.Params?["uri"];
            if (uri != "unity://project/info" && uri != "unity://editor/state")
                return Task.FromResult(JsonRpcResponse.CreateError(request.Id, -32002, "Resource not found"));

            return MainThreadDispatcher.RunAsync(() =>
            {
                object snapshot;
                if (uri == "unity://project/info")
                    snapshot = new { project = McpServer.ProjectName, projectPath = McpServer.ProjectPath,
                        unityVersion = Application.unityVersion, serverVersion = McpServer.Version,
                        tools = McpServer.GetRegisteredTools() };
                else
                    snapshot = new { projectPath = McpServer.ProjectPath, timestamp = DateTime.UtcNow,
                        isCompiling = EditorApplication.isCompiling, isUpdating = EditorApplication.isUpdating,
                        compilationFailed = EditorUtility.scriptCompilationFailed,
                        isPlaying = EditorApplication.isPlaying, isPaused = EditorApplication.isPaused,
                        scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i =>
                        {
                            var scene = SceneManager.GetSceneAt(i);
                            return new { scene.name, scene.path, scene.isLoaded, scene.isDirty };
                        }).ToArray() };
                return JsonRpcResponse.Success(request.Id, new { contents = new[] {
                    new { uri, mimeType = "application/json", text = JsonConvert.SerializeObject(snapshot) }
                }});
            });
        }
    }
}
