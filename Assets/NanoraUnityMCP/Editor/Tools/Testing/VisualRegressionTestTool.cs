using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Visual regression testing via screenshot comparison.
    /// Captures current state and compares against baseline images.
    /// Detects unintended visual changes with configurable threshold.
    /// </summary>
    [McpTool("visual_regression_test")]
    public class VisualRegressionTestTool : IMcpTool
    {
        public string Name => "visual_regression_test";
        public string Description => "Visual regression testing via screenshot comparison. Captures current state and compares against baseline. Detects unintended visual changes.";

        private const int ChannelDiffThreshold = 10;
        private const string BaselineDirectory = "TestBaselines";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""compare"", ""update_baseline"", ""list_baselines"", ""delete_baseline""],
                    ""description"": ""Action: compare (test), update_baseline (save new), list_baselines, delete_baseline"",
                    ""default"": ""compare""
                },
                ""name"": {
                    ""type"": ""string"",
                    ""description"": ""Baseline name (used as filename)""
                },
                ""view"": {
                    ""type"": ""string"",
                    ""enum"": [""game"", ""scene""],
                    ""default"": ""game""
                },
                ""threshold"": {
                    ""type"": ""number"",
                    ""description"": ""Allowed difference percentage (0-100, default: 1.0)"",
                    ""default"": 1.0
                },
                ""width"": { ""type"": ""integer"", ""default"": 0 },
                ""height"": { ""type"": ""integer"", ""default"": 0 }
            },
            ""required"": [""name""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>() ?? "compare";
                string name = arguments?["name"]?.Value<string>();
                string view = arguments?["view"]?.Value<string>() ?? "game";
                float threshold = arguments?["threshold"]?.Value<float>() ?? 1.0f;
                int width = arguments?["width"]?.Value<int>() ?? 0;
                int height = arguments?["height"]?.Value<int>() ?? 0;

                if (string.IsNullOrEmpty(name))
                {
                    return McpToolResult.Error("Parameter 'name' is required.");
                }

                // Sanitize name to prevent path traversal
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(".."))
                {
                    return McpToolResult.Error("Invalid baseline name. Avoid special characters and '..'.");
                }

                try
                {
                    switch (action.ToLower())
                    {
                        case "compare":
                            return ExecuteCompare(name, view, threshold, width, height);
                        case "update_baseline":
                            return ExecuteUpdateBaseline(name, view, width, height);
                        case "list_baselines":
                            return ExecuteListBaselines();
                        case "delete_baseline":
                            return ExecuteDeleteBaseline(name);
                        default:
                            return McpToolResult.Error($"Unknown action: {action}. Use compare, update_baseline, list_baselines, or delete_baseline.");
                    }
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Visual regression test failed: {ex.Message}");
                }
            });
        }

        #region Actions

        private McpToolResult ExecuteCompare(string name, string view, float threshold, int width, int height)
        {
            string baselinePath = GetBaselinePath(name);
            if (!File.Exists(baselinePath))
            {
                return McpToolResult.Error(
                    $"Baseline not found: {baselinePath}\n" +
                    "Use action 'update_baseline' to create a baseline first.");
            }

            // Capture current screenshot
            Texture2D current = CaptureView(view, width, height);
            if (current == null)
            {
                return McpToolResult.Error($"Failed to capture {view} view. Make sure the view is open.");
            }

            try
            {
                // Load baseline
                byte[] baselineBytes = File.ReadAllBytes(baselinePath);
                var baseline = new Texture2D(2, 2);
                if (!baseline.LoadImage(baselineBytes))
                {
                    UnityEngine.Object.DestroyImmediate(baseline);
                    return McpToolResult.Error("Failed to load baseline image.");
                }

                try
                {
                    // Size mismatch check
                    if (current.width != baseline.width || current.height != baseline.height)
                    {
                        return McpToolResult.Error(
                            $"Size mismatch: current={current.width}x{current.height}, " +
                            $"baseline={baseline.width}x{baseline.height}. " +
                            "Update baseline or specify matching width/height.");
                    }

                    // Pixel comparison
                    Color32[] currentPixels = current.GetPixels32();
                    Color32[] baselinePixels = baseline.GetPixels32();
                    int totalPixels = currentPixels.Length;
                    int changedCount = 0;

                    // Track bounding box of changed region
                    int minX = current.width, minY = current.height;
                    int maxX = -1, maxY = -1;

                    // Build diff highlight image
                    Color32[] diffPixels = new Color32[totalPixels];

                    for (int i = 0; i < totalPixels; i++)
                    {
                        Color32 c = currentPixels[i];
                        Color32 b = baselinePixels[i];

                        int dr = Mathf.Abs(c.r - b.r);
                        int dg = Mathf.Abs(c.g - b.g);
                        int db = Mathf.Abs(c.b - b.b);

                        bool isDiff = dr >= ChannelDiffThreshold ||
                                      dg >= ChannelDiffThreshold ||
                                      db >= ChannelDiffThreshold;

                        if (isDiff)
                        {
                            changedCount++;
                            int x = i % current.width;
                            int y = i / current.width;
                            minX = Mathf.Min(minX, x);
                            minY = Mathf.Min(minY, y);
                            maxX = Mathf.Max(maxX, x);
                            maxY = Mathf.Max(maxY, y);

                            // Red highlight for diff pixels
                            diffPixels[i] = new Color32(255, 0, 0, 255);
                        }
                        else
                        {
                            // Darkened grayscale for unchanged pixels
                            byte gray = (byte)(((int)c.r + c.g + c.b) / 6);
                            diffPixels[i] = new Color32(gray, gray, gray, 255);
                        }
                    }

                    float diffPercentage = (float)changedCount / totalPixels * 100f;
                    bool passed = diffPercentage <= threshold;

                    // Build diff image
                    var diffTexture = new Texture2D(current.width, current.height, TextureFormat.RGBA32, false);
                    diffTexture.SetPixels32(diffPixels);
                    diffTexture.Apply();
                    byte[] diffPng = diffTexture.EncodeToPNG();
                    string diffBase64 = Convert.ToBase64String(diffPng);
                    UnityEngine.Object.DestroyImmediate(diffTexture);

                    // Build result text
                    string status = passed ? "PASSED" : "FAILED";
                    string regionInfo = changedCount > 0 && maxX >= 0
                        ? $"Changed region: ({minX},{minY}) - ({maxX},{maxY})"
                        : "No changed regions detected.";

                    string resultText =
                        $"=== Visual Regression Test: {name} ===\n" +
                        $"Status: {status}\n" +
                        $"Diff: {diffPercentage:F2}% (threshold: {threshold}%)\n" +
                        $"Baseline: {BaselineDirectory}/{name}.png\n" +
                        $"Changed pixels: {changedCount} / {totalPixels}\n" +
                        regionInfo;

                    return new McpToolResult
                    {
                        Content = new List<McpContent>
                        {
                            McpContent.TextContent(resultText),
                            McpContent.ImageContent(diffBase64, "image/png")
                        }
                    };
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(baseline);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(current);
            }
        }

        private McpToolResult ExecuteUpdateBaseline(string name, string view, int width, int height)
        {
            Texture2D screenshot = CaptureView(view, width, height);
            if (screenshot == null)
            {
                return McpToolResult.Error($"Failed to capture {view} view. Make sure the view is open.");
            }

            try
            {
                byte[] pngData = screenshot.EncodeToPNG();
                string baselinePath = GetBaselinePath(name);
                string directory = Path.GetDirectoryName(baselinePath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(baselinePath, pngData);

                return McpToolResult.Success(
                    $"Baseline updated: {BaselineDirectory}/{name}.png\n" +
                    $"Size: {screenshot.width}x{screenshot.height}\n" +
                    $"File size: {pngData.Length / 1024}KB");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }

        private McpToolResult ExecuteListBaselines()
        {
            string dir = GetBaselineDirectory();
            if (!Directory.Exists(dir))
            {
                return McpToolResult.Success($"No baselines found. Directory does not exist: {BaselineDirectory}/");
            }

            string[] files = Directory.GetFiles(dir, "*.png");
            if (files.Length == 0)
            {
                return McpToolResult.Success("No baseline images found.");
            }

            var lines = new List<string> { $"=== Baselines ({files.Length}) ===" };
            foreach (string file in files.OrderBy(f => f))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                var info = new FileInfo(file);
                lines.Add($"  - {fileName} ({info.Length / 1024}KB, {info.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
            }

            return McpToolResult.Success(string.Join("\n", lines));
        }

        private McpToolResult ExecuteDeleteBaseline(string name)
        {
            string baselinePath = GetBaselinePath(name);
            if (!File.Exists(baselinePath))
            {
                return McpToolResult.Error($"Baseline not found: {BaselineDirectory}/{name}.png");
            }

            File.Delete(baselinePath);

            // Also delete .meta file if it exists
            string metaPath = baselinePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            return McpToolResult.Success($"Baseline deleted: {BaselineDirectory}/{name}.png");
        }

        #endregion

        #region Screenshot Capture

        private Texture2D CaptureView(string view, int width, int height)
        {
            if (view.ToLower() == "scene")
            {
                return CaptureSceneView(width, height);
            }
            return CaptureGameView(width, height);
        }

        private Texture2D CaptureGameView(int width, int height)
        {
            var gameViewType = Type.GetType("UnityEditor.GameView, UnityEditor");
            if (gameViewType == null)
                return null;

            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gameView == null)
                return null;

            int captureWidth = width > 0 ? width : Screen.width;
            int captureHeight = height > 0 ? height : Screen.height;

            var rt = new RenderTexture(captureWidth, captureHeight, 24);
            var prevRT = RenderTexture.active;

            var camera = Camera.main;
            if (camera == null)
            {
                camera = UnityCompatibility.FindFirstObjectByType<Camera>();
            }

            if (camera != null)
            {
                var prevCameraRT = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = prevCameraRT;
            }
            else
            {
                RenderTexture.active = null;
                var tempTex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tempTex != null)
                {
                    Graphics.Blit(tempTex, rt);
                    UnityEngine.Object.DestroyImmediate(tempTex);
                }
            }

            RenderTexture.active = rt;
            var screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenshot.Apply();

            RenderTexture.active = prevRT;
            UnityEngine.Object.DestroyImmediate(rt);

            return screenshot;
        }

        private Texture2D CaptureSceneView(int width, int height)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                sceneView = SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null;
            }

            if (sceneView == null)
                return null;

            var camera = sceneView.camera;
            if (camera == null)
                return null;

            int captureWidth = width > 0 ? width : (int)sceneView.position.width;
            int captureHeight = height > 0 ? height : (int)sceneView.position.height;

            var rt = new RenderTexture(captureWidth, captureHeight, 24);
            var prevRT = RenderTexture.active;
            var prevCameraRT = camera.targetTexture;

            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = prevCameraRT;

            RenderTexture.active = rt;
            var screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenshot.Apply();

            RenderTexture.active = prevRT;
            UnityEngine.Object.DestroyImmediate(rt);

            return screenshot;
        }

        #endregion

        #region Utilities

        private static string GetBaselineDirectory()
        {
            return Path.Combine(Application.dataPath, "..", BaselineDirectory);
        }

        private static string GetBaselinePath(string name)
        {
            return Path.Combine(GetBaselineDirectory(), $"{name}.png");
        }

        #endregion
    }
}
