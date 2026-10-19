using System;
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
    /// Tool for controlling Camera components.
    /// </summary>
    [McpTool("control_camera")]
    public class ControlCameraTool : IMcpTool
    {
        public string Name => "control_camera";
        public string Description => "Control cameras: create, configure, list, look at, and align to view";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""create"", ""configure"", ""info"", ""list"", ""look_at"", ""align_to_view"", ""set_main""],
                    ""description"": ""Action to perform""
                },
                ""camera_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the camera GameObject""
                },
                ""projection"": {
                    ""type"": ""string"",
                    ""enum"": [""perspective"", ""orthographic""],
                    ""description"": ""Camera projection type""
                },
                ""field_of_view"": {
                    ""type"": ""number"",
                    ""description"": ""Field of view in degrees (perspective)"",
                    ""minimum"": 1,
                    ""maximum"": 179
                },
                ""orthographic_size"": {
                    ""type"": ""number"",
                    ""description"": ""Orthographic size (orthographic)"",
                    ""minimum"": 0.01
                },
                ""near_clip"": {
                    ""type"": ""number"",
                    ""description"": ""Near clipping plane"",
                    ""minimum"": 0.01
                },
                ""far_clip"": {
                    ""type"": ""number"",
                    ""description"": ""Far clipping plane""
                },
                ""depth"": {
                    ""type"": ""number"",
                    ""description"": ""Camera depth (render order)""
                },
                ""clear_flags"": {
                    ""type"": ""string"",
                    ""enum"": [""skybox"", ""solid_color"", ""depth"", ""nothing""],
                    ""description"": ""Clear flags""
                },
                ""background_color"": {
                    ""type"": ""string"",
                    ""description"": ""Background color in hex format""
                },
                ""culling_mask"": {
                    ""type"": ""string"",
                    ""description"": ""Layer names to render (comma-separated)""
                },
                ""position"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    },
                    ""description"": ""World position""
                },
                ""rotation"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""z"": { ""type"": ""number"" }
                    },
                    ""description"": ""Euler rotation in degrees""
                },
                ""target_name"": {
                    ""type"": ""string"",
                    ""description"": ""Target object name for look_at action""
                },
                ""viewport_rect"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""width"": { ""type"": ""number"" },
                        ""height"": { ""type"": ""number"" }
                    },
                    ""description"": ""Viewport rectangle (normalized 0-1)""
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();
                string cameraName = arguments?["camera_name"]?.Value<string>();
                string projection = arguments?["projection"]?.Value<string>();
                float? fov = arguments?["field_of_view"]?.Value<float>();
                float? orthoSize = arguments?["orthographic_size"]?.Value<float>();
                float? nearClip = arguments?["near_clip"]?.Value<float>();
                float? farClip = arguments?["far_clip"]?.Value<float>();
                float? depth = arguments?["depth"]?.Value<float>();
                string clearFlags = arguments?["clear_flags"]?.Value<string>();
                string bgColor = arguments?["background_color"]?.Value<string>();
                string cullingMask = arguments?["culling_mask"]?.Value<string>();
                var positionObj = arguments?["position"];
                var rotationObj = arguments?["rotation"];
                string targetName = arguments?["target_name"]?.Value<string>();
                var viewportObj = arguments?["viewport_rect"];

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "create" => CreateCamera(cameraName, projection, fov, positionObj, rotationObj),
                        "configure" => ConfigureCamera(cameraName, projection, fov, orthoSize, nearClip, farClip, depth, clearFlags, bgColor, cullingMask, viewportObj),
                        "info" => GetCameraInfo(cameraName),
                        "list" => ListCameras(),
                        "look_at" => LookAt(cameraName, targetName),
                        "align_to_view" => AlignToSceneView(cameraName),
                        "set_main" => SetMainCamera(cameraName),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Camera operation failed: {ex.Message}");
                }
            });
        }

        private McpToolResult CreateCamera(string name, string projection, float? fov, JToken position, JToken rotation)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Camera";
            }

            var go = new GameObject(name);
            var camera = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();

            // Set projection
            if (!string.IsNullOrWhiteSpace(projection))
            {
                camera.orthographic = projection.ToLowerInvariant() == "orthographic";
            }

            // Set FOV
            if (fov.HasValue && !camera.orthographic)
            {
                camera.fieldOfView = fov.Value;
            }

            // Set position
            if (position != null)
            {
                float x = position["x"]?.Value<float>() ?? 0;
                float y = position["y"]?.Value<float>() ?? 1;
                float z = position["z"]?.Value<float>() ?? -10;
                go.transform.position = new Vector3(x, y, z);
            }
            else
            {
                go.transform.position = new Vector3(0, 1, -10);
            }

            // Set rotation
            if (rotation != null)
            {
                float x = rotation["x"]?.Value<float>() ?? 0;
                float y = rotation["y"]?.Value<float>() ?? 0;
                float z = rotation["z"]?.Value<float>() ?? 0;
                go.transform.eulerAngles = new Vector3(x, y, z);
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Camera");
            Selection.activeGameObject = go;

            return McpToolResult.Success($"Created camera: {name}\n" +
                $"- Projection: {(camera.orthographic ? "Orthographic" : "Perspective")}\n" +
                $"- FOV: {camera.fieldOfView}\n" +
                $"- Position: {go.transform.position}");
        }

        private McpToolResult ConfigureCamera(string cameraName, string projection, float? fov, float? orthoSize, float? nearClip, float? farClip, float? depth, string clearFlags, string bgColor, string cullingMask, JToken viewport)
        {
            var camera = FindCamera(cameraName);
            if (camera == null)
            {
                return McpToolResult.Error(cameraName != null ? $"Camera not found: {cameraName}" : "No camera found in scene");
            }

            Undo.RecordObject(camera, "Configure Camera");

            var changes = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(projection))
            {
                camera.orthographic = projection.ToLowerInvariant() == "orthographic";
                changes.AppendLine($"- Projection: {(camera.orthographic ? "Orthographic" : "Perspective")}");
            }

            if (fov.HasValue)
            {
                camera.fieldOfView = fov.Value;
                changes.AppendLine($"- FOV: {fov.Value}");
            }

            if (orthoSize.HasValue)
            {
                camera.orthographicSize = orthoSize.Value;
                changes.AppendLine($"- Orthographic Size: {orthoSize.Value}");
            }

            if (nearClip.HasValue)
            {
                camera.nearClipPlane = nearClip.Value;
                changes.AppendLine($"- Near Clip: {nearClip.Value}");
            }

            if (farClip.HasValue)
            {
                camera.farClipPlane = farClip.Value;
                changes.AppendLine($"- Far Clip: {farClip.Value}");
            }

            if (depth.HasValue)
            {
                camera.depth = depth.Value;
                changes.AppendLine($"- Depth: {depth.Value}");
            }

            if (!string.IsNullOrWhiteSpace(clearFlags))
            {
                camera.clearFlags = clearFlags.ToLowerInvariant() switch
                {
                    "solid_color" => CameraClearFlags.SolidColor,
                    "depth" => CameraClearFlags.Depth,
                    "nothing" => CameraClearFlags.Nothing,
                    _ => CameraClearFlags.Skybox
                };
                changes.AppendLine($"- Clear Flags: {camera.clearFlags}");
            }

            if (!string.IsNullOrWhiteSpace(bgColor) && ColorUtility.TryParseHtmlString(bgColor, out Color color))
            {
                camera.backgroundColor = color;
                changes.AppendLine($"- Background: {bgColor}");
            }

            if (!string.IsNullOrWhiteSpace(cullingMask))
            {
                var layers = cullingMask.Split(',').Select(s => s.Trim()).ToArray();
                int mask = LayerMask.GetMask(layers);
                camera.cullingMask = mask;
                changes.AppendLine($"- Culling Mask: {cullingMask}");
            }

            if (viewport != null)
            {
                float x = viewport["x"]?.Value<float>() ?? 0;
                float y = viewport["y"]?.Value<float>() ?? 0;
                float w = viewport["width"]?.Value<float>() ?? 1;
                float h = viewport["height"]?.Value<float>() ?? 1;
                camera.rect = new Rect(x, y, w, h);
                changes.AppendLine($"- Viewport: ({x}, {y}, {w}, {h})");
            }

            EditorUtility.SetDirty(camera);

            return McpToolResult.Success($"Configured camera: {camera.gameObject.name}\n{changes}");
        }

        private McpToolResult GetCameraInfo(string cameraName)
        {
            var camera = FindCamera(cameraName);
            if (camera == null)
            {
                return McpToolResult.Error(cameraName != null ? $"Camera not found: {cameraName}" : "No camera found in scene");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Camera: {camera.gameObject.name}");
            sb.AppendLine();
            sb.AppendLine($"**Is Main Camera:** {camera == Camera.main}");
            sb.AppendLine($"**Projection:** {(camera.orthographic ? "Orthographic" : "Perspective")}");

            if (camera.orthographic)
            {
                sb.AppendLine($"**Orthographic Size:** {camera.orthographicSize}");
            }
            else
            {
                sb.AppendLine($"**Field of View:** {camera.fieldOfView}");
            }

            sb.AppendLine($"**Near Clip:** {camera.nearClipPlane}");
            sb.AppendLine($"**Far Clip:** {camera.farClipPlane}");
            sb.AppendLine($"**Depth:** {camera.depth}");
            sb.AppendLine($"**Clear Flags:** {camera.clearFlags}");
            sb.AppendLine($"**Background:** #{ColorUtility.ToHtmlStringRGB(camera.backgroundColor)}");
            sb.AppendLine($"**Viewport:** {camera.rect}");
            sb.AppendLine($"**Target Display:** {camera.targetDisplay}");
            sb.AppendLine();
            sb.AppendLine("## Transform");
            sb.AppendLine($"**Position:** {camera.transform.position}");
            sb.AppendLine($"**Rotation:** {camera.transform.eulerAngles}");
            sb.AppendLine($"**Forward:** {camera.transform.forward}");

            if (camera.targetTexture != null)
            {
                sb.AppendLine();
                sb.AppendLine($"**Render Texture:** {camera.targetTexture.name} ({camera.targetTexture.width}x{camera.targetTexture.height})");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult ListCameras()
        {
            var cameras = UnityCompatibility.FindObjectsByType<Camera>();
            var sb = new StringBuilder();
            sb.AppendLine("# Cameras in Scene");
            sb.AppendLine();
            sb.AppendLine($"Found {cameras.Length} camera(s):");
            sb.AppendLine();

            foreach (var camera in cameras.OrderBy(c => c.depth))
            {
                string mainTag = camera == Camera.main ? " [Main]" : "";
                string projType = camera.orthographic ? "Ortho" : $"FOV={camera.fieldOfView}";
                sb.AppendLine($"- {camera.gameObject.name}{mainTag} (depth={camera.depth}, {projType})");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult LookAt(string cameraName, string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return McpToolResult.Error("target_name is required for look_at action");
            }

            var camera = FindCamera(cameraName);
            if (camera == null)
            {
                return McpToolResult.Error(cameraName != null ? $"Camera not found: {cameraName}" : "No camera found in scene");
            }

            var target = GameObject.Find(targetName);
            if (target == null)
            {
                return McpToolResult.Error($"Target not found: {targetName}");
            }

            Undo.RecordObject(camera.transform, "Camera Look At");
            camera.transform.LookAt(target.transform);

            return McpToolResult.Success($"Camera '{camera.gameObject.name}' now looking at '{targetName}'");
        }

        private McpToolResult AlignToSceneView(string cameraName)
        {
            var camera = FindCamera(cameraName);
            if (camera == null)
            {
                return McpToolResult.Error(cameraName != null ? $"Camera not found: {cameraName}" : "No camera found in scene");
            }

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return McpToolResult.Error("No active Scene View found");
            }

            Undo.RecordObject(camera.transform, "Align Camera to View");
            camera.transform.position = sceneView.camera.transform.position;
            camera.transform.rotation = sceneView.camera.transform.rotation;

            return McpToolResult.Success($"Aligned '{camera.gameObject.name}' to Scene View\n" +
                $"- Position: {camera.transform.position}\n" +
                $"- Rotation: {camera.transform.eulerAngles}");
        }

        private McpToolResult SetMainCamera(string cameraName)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
            {
                return McpToolResult.Error("camera_name is required for set_main action");
            }

            var camera = FindCamera(cameraName);
            if (camera == null)
            {
                return McpToolResult.Error($"Camera not found: {cameraName}");
            }

            // Remove MainCamera tag from current main camera
            if (Camera.main != null && Camera.main != camera)
            {
                Undo.RecordObject(Camera.main.gameObject, "Remove Main Camera Tag");
                Camera.main.gameObject.tag = "Untagged";
            }

            // Set new main camera
            Undo.RecordObject(camera.gameObject, "Set Main Camera");
            camera.gameObject.tag = "MainCamera";

            return McpToolResult.Success($"Set '{cameraName}' as Main Camera");
        }

        private Camera FindCamera(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    return go.GetComponent<Camera>();
                }
                return null;
            }

            return Camera.main ?? UnityCompatibility.FindFirstObjectByType<Camera>();
        }
    }

    /// <summary>
    /// Tool for setting up Cinemachine (if available).
    /// </summary>
    [McpTool("setup_cinemachine")]
    public class SetupCinemachineTool : IMcpTool
    {
        public string Name => "setup_cinemachine";
        public string Description => "Setup Cinemachine virtual cameras and brains (requires Cinemachine package)";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""check_installed"", ""add_brain"", ""create_vcam"", ""list_vcams"", ""info""],
                    ""description"": ""Action to perform""
                },
                ""camera_name"": {
                    ""type"": ""string"",
                    ""description"": ""Camera name for add_brain action""
                },
                ""vcam_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name for the virtual camera""
                },
                ""vcam_type"": {
                    ""type"": ""string"",
                    ""enum"": [""virtual"", ""freelook"", ""state_driven"", ""blend_list""],
                    ""description"": ""Type of virtual camera to create"",
                    ""default"": ""virtual""
                },
                ""follow_target"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the target to follow""
                },
                ""look_at_target"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the target to look at""
                },
                ""priority"": {
                    ""type"": ""integer"",
                    ""description"": ""Virtual camera priority"",
                    ""default"": 10
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();
                string cameraName = arguments?["camera_name"]?.Value<string>();
                string vcamName = arguments?["vcam_name"]?.Value<string>();
                string vcamType = arguments?["vcam_type"]?.Value<string>() ?? "virtual";
                string followTarget = arguments?["follow_target"]?.Value<string>();
                string lookAtTarget = arguments?["look_at_target"]?.Value<string>();
                int priority = arguments?["priority"]?.Value<int>() ?? 10;

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "check_installed" => CheckCinemachineInstalled(),
                        "add_brain" => AddCinemachineBrain(cameraName),
                        "create_vcam" => CreateVirtualCamera(vcamName, vcamType, followTarget, lookAtTarget, priority),
                        "list_vcams" => ListVirtualCameras(),
                        "info" => GetCinemachineInfo(vcamName),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Cinemachine operation failed: {ex.Message}");
                }
            });
        }

        private McpToolResult CheckCinemachineInstalled()
        {
            var cinemachineType = GetCinemachineType("Unity.Cinemachine.CinemachineBrain") ??
                                  GetCinemachineType("Cinemachine.CinemachineBrain");

            if (cinemachineType != null)
            {
                string version = cinemachineType.Assembly.GetName().Version?.ToString() ?? "unknown";
                return McpToolResult.Success($"Cinemachine is installed (version: {version})");
            }

            return McpToolResult.Success("Cinemachine is NOT installed.\n\nTo install:\n1. Window > Package Manager\n2. Search for 'Cinemachine'\n3. Click Install");
        }

        private McpToolResult AddCinemachineBrain(string cameraName)
        {
            var brainType = GetCinemachineType("Unity.Cinemachine.CinemachineBrain") ??
                            GetCinemachineType("Cinemachine.CinemachineBrain");

            if (brainType == null)
            {
                return McpToolResult.Error("Cinemachine is not installed. Use action 'check_installed' for installation instructions.");
            }

            Camera camera = null;
            if (!string.IsNullOrWhiteSpace(cameraName))
            {
                var go = GameObject.Find(cameraName);
                if (go != null)
                {
                    camera = go.GetComponent<Camera>();
                }
            }
            else
            {
                camera = Camera.main ?? UnityCompatibility.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                return McpToolResult.Error("No camera found to add Cinemachine Brain to");
            }

            // Check if already has brain
            var existingBrain = camera.GetComponent(brainType);
            if (existingBrain != null)
            {
                return McpToolResult.Success($"Camera '{camera.gameObject.name}' already has a Cinemachine Brain");
            }

            Undo.AddComponent(camera.gameObject, brainType);

            return McpToolResult.Success($"Added Cinemachine Brain to '{camera.gameObject.name}'");
        }

        private McpToolResult CreateVirtualCamera(string name, string type, string followTarget, string lookAtTarget, int priority)
        {
            // Try new Cinemachine 3.x first, then fall back to 2.x
            var vcamType = GetCinemachineType("Unity.Cinemachine.CinemachineCamera") ??
                           GetCinemachineType("Cinemachine.CinemachineVirtualCamera");

            if (vcamType == null)
            {
                return McpToolResult.Error("Cinemachine is not installed. Use action 'check_installed' for installation instructions.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Virtual Camera";
            }

            var go = new GameObject(name);
            var vcam = go.AddComponent(vcamType);

            // Set priority using reflection
            var priorityProp = vcamType.GetProperty("Priority");
            if (priorityProp != null)
            {
                priorityProp.SetValue(vcam, priority);
            }

            // Set follow target
            if (!string.IsNullOrWhiteSpace(followTarget))
            {
                var followGO = GameObject.Find(followTarget);
                if (followGO != null)
                {
                    var followProp = vcamType.GetProperty("Follow");
                    if (followProp != null)
                    {
                        followProp.SetValue(vcam, followGO.transform);
                    }
                }
            }

            // Set look at target
            if (!string.IsNullOrWhiteSpace(lookAtTarget))
            {
                var lookAtGO = GameObject.Find(lookAtTarget);
                if (lookAtGO != null)
                {
                    var lookAtProp = vcamType.GetProperty("LookAt");
                    if (lookAtProp != null)
                    {
                        lookAtProp.SetValue(vcam, lookAtGO.transform);
                    }
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Virtual Camera");
            Selection.activeGameObject = go;

            var sb = new StringBuilder();
            sb.AppendLine($"Created Virtual Camera: {name}");
            sb.AppendLine($"- Priority: {priority}");
            if (!string.IsNullOrWhiteSpace(followTarget))
                sb.AppendLine($"- Follow: {followTarget}");
            if (!string.IsNullOrWhiteSpace(lookAtTarget))
                sb.AppendLine($"- Look At: {lookAtTarget}");

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult ListVirtualCameras()
        {
            var vcamType = GetCinemachineType("Unity.Cinemachine.CinemachineCamera") ??
                           GetCinemachineType("Cinemachine.CinemachineVirtualCamera");

            if (vcamType == null)
            {
                return McpToolResult.Error("Cinemachine is not installed");
            }

            var vcams = UnityCompatibility.FindObjectsByType(vcamType);
            var sb = new StringBuilder();
            sb.AppendLine("# Virtual Cameras in Scene");
            sb.AppendLine();
            sb.AppendLine($"Found {vcams.Length} virtual camera(s):");
            sb.AppendLine();

            foreach (var vcam in vcams)
            {
                var go = (vcam as Component)?.gameObject;
                if (go == null) continue;

                var priorityProp = vcamType.GetProperty("Priority");
                int priority = priorityProp != null ? (int)priorityProp.GetValue(vcam) : 0;

                var followProp = vcamType.GetProperty("Follow");
                var follow = followProp?.GetValue(vcam) as Transform;

                var lookAtProp = vcamType.GetProperty("LookAt");
                var lookAt = lookAtProp?.GetValue(vcam) as Transform;

                sb.AppendLine($"- {go.name} (priority={priority})");
                if (follow != null)
                    sb.AppendLine($"    Follow: {follow.name}");
                if (lookAt != null)
                    sb.AppendLine($"    Look At: {lookAt.name}");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult GetCinemachineInfo(string vcamName)
        {
            var vcamType = GetCinemachineType("Unity.Cinemachine.CinemachineCamera") ??
                           GetCinemachineType("Cinemachine.CinemachineVirtualCamera");

            if (vcamType == null)
            {
                return McpToolResult.Error("Cinemachine is not installed");
            }

            Component vcam = null;
            if (!string.IsNullOrWhiteSpace(vcamName))
            {
                var go = GameObject.Find(vcamName);
                if (go != null)
                {
                    vcam = go.GetComponent(vcamType);
                }
            }
            else
            {
                vcam = UnityCompatibility.FindFirstObjectByType(vcamType) as Component;
            }

            if (vcam == null)
            {
                return McpToolResult.Error(vcamName != null ? $"Virtual camera not found: {vcamName}" : "No virtual camera found in scene");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Virtual Camera: {vcam.gameObject.name}");
            sb.AppendLine();

            // Get properties using reflection
            var priorityProp = vcamType.GetProperty("Priority");
            if (priorityProp != null)
            {
                sb.AppendLine($"**Priority:** {priorityProp.GetValue(vcam)}");
            }

            var followProp = vcamType.GetProperty("Follow");
            if (followProp != null)
            {
                var follow = followProp.GetValue(vcam) as Transform;
                sb.AppendLine($"**Follow:** {(follow != null ? follow.name : "(none)")}");
            }

            var lookAtProp = vcamType.GetProperty("LookAt");
            if (lookAtProp != null)
            {
                var lookAt = lookAtProp.GetValue(vcam) as Transform;
                sb.AppendLine($"**Look At:** {(lookAt != null ? lookAt.name : "(none)")}");
            }

            sb.AppendLine();
            sb.AppendLine("## Transform");
            sb.AppendLine($"**Position:** {vcam.transform.position}");
            sb.AppendLine($"**Rotation:** {vcam.transform.eulerAngles}");

            return McpToolResult.Success(sb.ToString());
        }

        private Type GetCinemachineType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Ignore assembly loading errors
                }
            }
            return null;
        }
    }
}
