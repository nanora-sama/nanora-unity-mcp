using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Captures screenshots from Game View or Scene View.
    /// Returns Base64-encoded image data for AI vision analysis.
    /// </summary>
    [McpTool("capture_screenshot")]
    public class CaptureScreenshotTool : IMcpTool
    {
        public string Name => "capture_screenshot";
        public string Description => "Capture a PNG from the Game View or Scene View and return it as an image content block, plus a text summary. " +
            "view=game re-renders a single camera (Camera.main, else the first camera found) into an offscreen texture: " +
            "Screen Space - Overlay canvases, UI Toolkit panels, and other cameras' output are not included, so it is not a copy of what the Game View window shows. " +
            "view=scene renders the Scene View camera; camera_position (the orbit pivot), camera_rotation, and camera_size apply only to this capture and are restored afterwards. " +
            "save_to_file also writes the PNG to disk.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""view"": {
                    ""type"": ""string"",
                    ""description"": ""Which view to capture"",
                    ""enum"": [""game"", ""scene""],
                    ""default"": ""game""
                },
                ""width"": {
                    ""type"": ""integer"",
                    ""description"": ""Screenshot width (0 = use current)"",
                    ""default"": 0
                },
                ""height"": {
                    ""type"": ""integer"",
                    ""description"": ""Screenshot height (0 = use current)"",
                    ""default"": 0
                },
                ""supersampling"": {
                    ""type"": ""integer"",
                    ""description"": ""Supersampling factor for higher quality (1-4)"",
                    ""default"": 1
                },
                ""save_to_file"": {
                    ""type"": ""string"",
                    ""description"": ""Optional file path to save the screenshot (relative to project or absolute)""
                },
                ""camera_position"": {
                    ""type"": ""object"",
                    ""description"": ""Scene View pivot point the camera orbits and looks at (only for scene view)"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    }
                },
                ""camera_rotation"": {
                    ""type"": ""object"",
                    ""description"": ""Scene View camera rotation in euler angles (only for scene view)"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    }
                },
                ""camera_size"": {
                    ""type"": ""number"",
                    ""description"": ""Scene View orthographic size / perspective distance (only for scene view)""
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string view = arguments?["view"]?.Value<string>() ?? "game";
                int width = arguments?["width"]?.Value<int>() ?? 0;
                int height = arguments?["height"]?.Value<int>() ?? 0;
                int supersampling = arguments?["supersampling"]?.Value<int>() ?? 1;
                string saveToFile = arguments?["save_to_file"]?.Value<string>();

                // Camera parameters for scene view
                Vector3? cameraPosition = null;
                Vector3? cameraRotation = null;
                float? cameraSize = null;

                var posObj = arguments?["camera_position"];
                if (posObj != null && posObj.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                {
                    cameraPosition = new Vector3(
                        posObj["x"]?.Value<float>() ?? 0,
                        posObj["y"]?.Value<float>() ?? 0,
                        posObj["z"]?.Value<float>() ?? 0
                    );
                }

                var rotObj = arguments?["camera_rotation"];
                if (rotObj != null && rotObj.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                {
                    cameraRotation = new Vector3(
                        rotObj["x"]?.Value<float>() ?? 0,
                        rotObj["y"]?.Value<float>() ?? 0,
                        rotObj["z"]?.Value<float>() ?? 0
                    );
                }

                if (arguments?["camera_size"] != null)
                {
                    cameraSize = arguments["camera_size"].Value<float>();
                }

                supersampling = Mathf.Clamp(supersampling, 1, 4);

                try
                {
                    Texture2D screenshot;

                    if (view.ToLower() == "scene")
                    {
                        screenshot = CaptureSceneView(width, height, cameraPosition, cameraRotation, cameraSize);
                    }
                    else
                    {
                        screenshot = CaptureGameView(width, height, supersampling);
                    }

                    if (screenshot == null)
                    {
                        return McpToolResult.Error($"Failed to capture {view} view. Make sure the view is open.");
                    }

                    // Encode to PNG
                    byte[] pngData = screenshot.EncodeToPNG();

                    // Save to file if requested
                    string savedPath = null;
                    if (!string.IsNullOrEmpty(saveToFile))
                    {
                        savedPath = SaveScreenshot(pngData, saveToFile);
                    }

                    // Convert to Base64
                    string base64Data = Convert.ToBase64String(pngData);

                    // Clean up
                    UnityEngine.Object.DestroyImmediate(screenshot);

                    // Return as MCP image content
                    var result = new McpToolResult
                    {
                        Content = new System.Collections.Generic.List<McpContent>
                        {
                            new McpContent
                            {
                                Type = "image",
                                Data = base64Data,
                                MimeType = "image/png"
                            },
                            McpContent.TextContent(
                                $"Screenshot captured from {view} view.\n" +
                                $"Size: {width}x{height} (or current if 0)\n" +
                                $"Data size: {pngData.Length / 1024}KB\n" +
                                (savedPath != null ? $"Saved to: {savedPath}" : ""))
                        }
                    };

                    return result;
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Screenshot capture failed: {ex.Message}");
                }
            });
        }

        private Texture2D CaptureGameView(int width, int height, int supersampling)
        {
            // Get Game View
            var gameViewType = Type.GetType("UnityEditor.GameView, UnityEditor");
            if (gameViewType == null)
            {
                return null;
            }

            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gameView == null)
            {
                return null;
            }

            // Try to get render texture from game view
            // Method 1: Use ScreenCapture
            int captureWidth = width > 0 ? width * supersampling : Screen.width * supersampling;
            int captureHeight = height > 0 ? height * supersampling : Screen.height * supersampling;

            // Create render texture
            var rt = new RenderTexture(captureWidth, captureHeight, 24);
            var prevRT = RenderTexture.active;

            // Find main camera
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
                // Fallback: capture current screen
                RenderTexture.active = null;
                var tempTex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tempTex != null)
                {
                    Graphics.Blit(tempTex, rt);
                    UnityEngine.Object.DestroyImmediate(tempTex);
                }
            }

            // Read pixels from render texture
            RenderTexture.active = rt;
            var screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenshot.Apply();

            // Cleanup
            RenderTexture.active = prevRT;
            UnityEngine.Object.DestroyImmediate(rt);

            // Downscale if supersampling was used
            if (supersampling > 1)
            {
                var finalWidth = captureWidth / supersampling;
                var finalHeight = captureHeight / supersampling;
                var downscaled = ScaleTexture(screenshot, finalWidth, finalHeight);
                UnityEngine.Object.DestroyImmediate(screenshot);
                screenshot = downscaled;
            }

            return screenshot;
        }

        private Texture2D CaptureSceneView(int width, int height,
            Vector3? cameraPosition = null, Vector3? cameraRotation = null, float? cameraSize = null)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                sceneView = SceneView.sceneViews.Count > 0 ? (SceneView)SceneView.sceneViews[0] : null;
            }

            if (sceneView == null)
            {
                return null;
            }

            // Store original camera state
            var originalPivot = sceneView.pivot;
            var originalRotation = sceneView.rotation;
            var originalSize = sceneView.size;
            var originalOrthographic = sceneView.orthographic;

            try
            {
                // Apply camera parameters if specified
                if (cameraPosition.HasValue)
                {
                    sceneView.pivot = cameraPosition.Value;
                }

                if (cameraRotation.HasValue)
                {
                    sceneView.rotation = Quaternion.Euler(cameraRotation.Value);
                }

                if (cameraSize.HasValue)
                {
                    sceneView.size = cameraSize.Value;
                }

                // Force scene view to update
                sceneView.Repaint();

                var camera = sceneView.camera;
                if (camera == null)
                {
                    return null;
                }

                // Sync camera transform with SceneView settings
                // Repaint() doesn't immediately update the camera transform, so we set it directly
                if (cameraPosition.HasValue || cameraRotation.HasValue || cameraSize.HasValue)
                {
                    camera.transform.position = sceneView.pivot - sceneView.rotation * Vector3.forward * sceneView.cameraDistance;
                    camera.transform.rotation = sceneView.rotation;
                }

                int captureWidth = width > 0 ? width : (int)sceneView.position.width;
                int captureHeight = height > 0 ? height : (int)sceneView.position.height;

                // Create render texture
                var rt = new RenderTexture(captureWidth, captureHeight, 24);
                var prevRT = RenderTexture.active;
                var prevCameraRT = camera.targetTexture;

                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = prevCameraRT;

                // Read pixels
                RenderTexture.active = rt;
                var screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                screenshot.Apply();

                // Cleanup
                RenderTexture.active = prevRT;
                UnityEngine.Object.DestroyImmediate(rt);

                return screenshot;
            }
            finally
            {
                // Restore original camera state if it was modified
                if (cameraPosition.HasValue || cameraRotation.HasValue || cameraSize.HasValue)
                {
                    sceneView.pivot = originalPivot;
                    sceneView.rotation = originalRotation;
                    sceneView.size = originalSize;
                    sceneView.Repaint();
                }
            }
        }

        private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var result = new Texture2D(targetWidth, targetHeight, source.format, false);

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    float u = (float)x / targetWidth;
                    float v = (float)y / targetHeight;
                    result.SetPixel(x, y, source.GetPixelBilinear(u, v));
                }
            }

            result.Apply();
            return result;
        }

        private string SaveScreenshot(byte[] pngData, string path)
        {
            // Handle relative paths
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Application.dataPath, "..", path);
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Add extension if missing
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                path += ".png";
            }

            File.WriteAllBytes(path, pngData);

            return path;
        }
    }
}
