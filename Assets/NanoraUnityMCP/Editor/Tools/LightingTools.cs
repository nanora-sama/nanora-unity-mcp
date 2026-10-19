using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Tool for managing Light components.
    /// </summary>
    [McpTool("manage_light")]
    public class ManageLightTool : IMcpTool
    {
        public string Name => "manage_light";
        public string Description => "Manage lights: create, configure, list, and get info";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""create"", ""configure"", ""info"", ""list"", ""delete""],
                    ""description"": ""Action to perform""
                },
                ""light_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the light GameObject""
                },
                ""light_type"": {
                    ""type"": ""string"",
                    ""enum"": [""directional"", ""point"", ""spot"", ""area""],
                    ""description"": ""Type of light to create""
                },
                ""color"": {
                    ""type"": ""string"",
                    ""description"": ""Light color in hex format (e.g., #FFFFFF)""
                },
                ""intensity"": {
                    ""type"": ""number"",
                    ""description"": ""Light intensity"",
                    ""minimum"": 0
                },
                ""range"": {
                    ""type"": ""number"",
                    ""description"": ""Range for Point/Spot lights"",
                    ""minimum"": 0
                },
                ""spot_angle"": {
                    ""type"": ""number"",
                    ""description"": ""Spot angle for Spot lights (degrees)"",
                    ""minimum"": 1,
                    ""maximum"": 179
                },
                ""inner_spot_angle"": {
                    ""type"": ""number"",
                    ""description"": ""Inner spot angle percentage"",
                    ""minimum"": 0,
                    ""maximum"": 100
                },
                ""shadows"": {
                    ""type"": ""string"",
                    ""enum"": [""none"", ""hard"", ""soft""],
                    ""description"": ""Shadow type""
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
                ""cookie_path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path to cookie texture""
                },
                ""culling_mask"": {
                    ""type"": ""string"",
                    ""description"": ""Layer mask (e.g., 'Everything', 'Default', 'UI')""
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();
                string lightName = arguments?["light_name"]?.Value<string>();
                string lightType = arguments?["light_type"]?.Value<string>();
                string colorHex = arguments?["color"]?.Value<string>();
                float? intensity = arguments?["intensity"]?.Value<float>();
                float? range = arguments?["range"]?.Value<float>();
                float? spotAngle = arguments?["spot_angle"]?.Value<float>();
                float? innerSpotAngle = arguments?["inner_spot_angle"]?.Value<float>();
                string shadows = arguments?["shadows"]?.Value<string>();
                var positionObj = arguments?["position"];
                var rotationObj = arguments?["rotation"];
                string cookiePath = arguments?["cookie_path"]?.Value<string>();
                string cullingMask = arguments?["culling_mask"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "create" => CreateLight(lightName, lightType, colorHex, intensity, range, spotAngle, positionObj, rotationObj, shadows),
                        "configure" => ConfigureLight(lightName, colorHex, intensity, range, spotAngle, innerSpotAngle, shadows, cookiePath, cullingMask),
                        "info" => GetLightInfo(lightName),
                        "list" => ListLights(),
                        "delete" => DeleteLight(lightName),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Light operation failed: {ex.Message}");
                }
            });
        }

        private McpToolResult CreateLight(string name, string lightType, string colorHex, float? intensity, float? range, float? spotAngle, JToken position, JToken rotation, string shadows)
        {
            if (string.IsNullOrWhiteSpace(lightType))
            {
                lightType = "point";
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"{char.ToUpper(lightType[0])}{lightType.Substring(1)} Light";
            }

            var go = new GameObject(name);
            var light = go.AddComponent<Light>();

            // Set light type
            light.type = lightType.ToLowerInvariant() switch
            {
                "directional" => LightType.Directional,
                "spot" => LightType.Spot,
                "area" => LightType.Rectangle,
                _ => LightType.Point
            };

            // Set color
            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color color))
            {
                light.color = color;
            }

            // Set intensity
            if (intensity.HasValue)
            {
                light.intensity = intensity.Value;
            }

            // Set range for point/spot lights
            if (range.HasValue && (light.type == LightType.Point || light.type == LightType.Spot))
            {
                light.range = range.Value;
            }

            // Set spot angle
            if (spotAngle.HasValue && light.type == LightType.Spot)
            {
                light.spotAngle = spotAngle.Value;
            }

            // Set position
            if (position != null)
            {
                float x = position["x"]?.Value<float>() ?? 0;
                float y = position["y"]?.Value<float>() ?? 0;
                float z = position["z"]?.Value<float>() ?? 0;
                go.transform.position = new Vector3(x, y, z);
            }

            // Set rotation
            if (rotation != null)
            {
                float x = rotation["x"]?.Value<float>() ?? 0;
                float y = rotation["y"]?.Value<float>() ?? 0;
                float z = rotation["z"]?.Value<float>() ?? 0;
                go.transform.eulerAngles = new Vector3(x, y, z);
            }
            else if (light.type == LightType.Directional)
            {
                // Default directional light rotation
                go.transform.eulerAngles = new Vector3(50, -30, 0);
            }

            // Set shadows
            if (!string.IsNullOrWhiteSpace(shadows))
            {
                light.shadows = shadows.ToLowerInvariant() switch
                {
                    "hard" => LightShadows.Hard,
                    "soft" => LightShadows.Soft,
                    _ => LightShadows.None
                };
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Light");
            Selection.activeGameObject = go;

            return McpToolResult.Success($"Created {light.type} light: {name}\n" +
                $"- Color: #{ColorUtility.ToHtmlStringRGB(light.color)}\n" +
                $"- Intensity: {light.intensity}\n" +
                $"- Shadows: {light.shadows}");
        }

        private McpToolResult ConfigureLight(string lightName, string colorHex, float? intensity, float? range, float? spotAngle, float? innerSpotAngle, string shadows, string cookiePath, string cullingMask)
        {
            var light = FindLight(lightName);
            if (light == null)
            {
                return McpToolResult.Error(lightName != null ? $"Light not found: {lightName}" : "No light found in scene");
            }

            Undo.RecordObject(light, "Configure Light");

            var changes = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color color))
            {
                light.color = color;
                changes.AppendLine($"- Color: {colorHex}");
            }

            if (intensity.HasValue)
            {
                light.intensity = intensity.Value;
                changes.AppendLine($"- Intensity: {intensity.Value}");
            }

            if (range.HasValue)
            {
                light.range = range.Value;
                changes.AppendLine($"- Range: {range.Value}");
            }

            if (spotAngle.HasValue && light.type == LightType.Spot)
            {
                light.spotAngle = spotAngle.Value;
                changes.AppendLine($"- Spot Angle: {spotAngle.Value}");
            }

            if (innerSpotAngle.HasValue && light.type == LightType.Spot)
            {
                light.innerSpotAngle = innerSpotAngle.Value;
                changes.AppendLine($"- Inner Spot Angle: {innerSpotAngle.Value}%");
            }

            if (!string.IsNullOrWhiteSpace(shadows))
            {
                light.shadows = shadows.ToLowerInvariant() switch
                {
                    "hard" => LightShadows.Hard,
                    "soft" => LightShadows.Soft,
                    _ => LightShadows.None
                };
                changes.AppendLine($"- Shadows: {light.shadows}");
            }

            if (!string.IsNullOrWhiteSpace(cookiePath))
            {
                var cookie = AssetDatabase.LoadAssetAtPath<Texture>(cookiePath);
                if (cookie != null)
                {
                    light.cookie = cookie;
                    changes.AppendLine($"- Cookie: {cookie.name}");
                }
            }

            if (!string.IsNullOrWhiteSpace(cullingMask))
            {
                int mask = LayerMask.GetMask(cullingMask.Split(',').Select(s => s.Trim()).ToArray());
                if (mask != 0 || cullingMask.ToLowerInvariant() == "nothing")
                {
                    light.cullingMask = mask;
                    changes.AppendLine($"- Culling Mask: {cullingMask}");
                }
            }

            EditorUtility.SetDirty(light);

            return McpToolResult.Success($"Configured light: {light.gameObject.name}\n{changes}");
        }

        private McpToolResult GetLightInfo(string lightName)
        {
            var light = FindLight(lightName);
            if (light == null)
            {
                return McpToolResult.Error(lightName != null ? $"Light not found: {lightName}" : "No light found in scene");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Light: {light.gameObject.name}");
            sb.AppendLine();
            sb.AppendLine($"**Type:** {light.type}");
            sb.AppendLine($"**Color:** #{ColorUtility.ToHtmlStringRGB(light.color)}");
            sb.AppendLine($"**Intensity:** {light.intensity}");
            sb.AppendLine($"**Shadows:** {light.shadows}");
            sb.AppendLine($"**Render Mode:** {light.renderMode}");

            if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                sb.AppendLine($"**Range:** {light.range}");
            }

            if (light.type == LightType.Spot)
            {
                sb.AppendLine($"**Spot Angle:** {light.spotAngle}");
                sb.AppendLine($"**Inner Spot Angle:** {light.innerSpotAngle}%");
            }

            if (light.type == LightType.Rectangle)
            {
                sb.AppendLine($"**Area Size:** {light.areaSize}");
            }

            sb.AppendLine();
            sb.AppendLine("## Transform");
            sb.AppendLine($"**Position:** {light.transform.position}");
            sb.AppendLine($"**Rotation:** {light.transform.eulerAngles}");

            if (light.cookie != null)
            {
                sb.AppendLine();
                sb.AppendLine($"**Cookie:** {light.cookie.name}");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult ListLights()
        {
            var lights = UnityCompatibility.FindObjectsByType<Light>();
            var sb = new StringBuilder();
            sb.AppendLine("# Lights in Scene");
            sb.AppendLine();
            sb.AppendLine($"Found {lights.Length} light(s):");
            sb.AppendLine();

            foreach (var light in lights)
            {
                string colorStr = $"#{ColorUtility.ToHtmlStringRGB(light.color)}";
                sb.AppendLine($"- {light.gameObject.name} ({light.type}): {colorStr}, intensity={light.intensity}, shadows={light.shadows}");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult DeleteLight(string lightName)
        {
            if (string.IsNullOrWhiteSpace(lightName))
            {
                return McpToolResult.Error("light_name is required for delete action");
            }

            var light = FindLight(lightName);
            if (light == null)
            {
                return McpToolResult.Error($"Light not found: {lightName}");
            }

            Undo.DestroyObjectImmediate(light.gameObject);

            return McpToolResult.Success($"Deleted light: {lightName}");
        }

        private Light FindLight(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    return go.GetComponent<Light>();
                }
                return null;
            }

            return UnityCompatibility.FindFirstObjectByType<Light>();
        }
    }

    /// <summary>
    /// Tool for configuring global lighting settings.
    /// </summary>
    [McpTool("configure_lighting")]
    public class ConfigureLightingTool : IMcpTool
    {
        public string Name => "configure_lighting";
        public string Description => "Configure global lighting: ambient, fog, skybox, and bake settings";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""get_info"", ""set_ambient"", ""set_fog"", ""set_skybox"", ""bake""],
                    ""description"": ""Action to perform""
                },
                ""ambient_mode"": {
                    ""type"": ""string"",
                    ""enum"": [""skybox"", ""gradient"", ""flat""],
                    ""description"": ""Ambient lighting mode""
                },
                ""ambient_color"": {
                    ""type"": ""string"",
                    ""description"": ""Ambient color in hex format (for flat mode)""
                },
                ""ambient_sky_color"": {
                    ""type"": ""string"",
                    ""description"": ""Sky color for gradient mode""
                },
                ""ambient_equator_color"": {
                    ""type"": ""string"",
                    ""description"": ""Equator color for gradient mode""
                },
                ""ambient_ground_color"": {
                    ""type"": ""string"",
                    ""description"": ""Ground color for gradient mode""
                },
                ""ambient_intensity"": {
                    ""type"": ""number"",
                    ""description"": ""Ambient intensity multiplier"",
                    ""minimum"": 0
                },
                ""fog_enabled"": {
                    ""type"": ""boolean"",
                    ""description"": ""Enable/disable fog""
                },
                ""fog_color"": {
                    ""type"": ""string"",
                    ""description"": ""Fog color in hex format""
                },
                ""fog_mode"": {
                    ""type"": ""string"",
                    ""enum"": [""linear"", ""exponential"", ""exponential_squared""],
                    ""description"": ""Fog mode""
                },
                ""fog_density"": {
                    ""type"": ""number"",
                    ""description"": ""Fog density for exponential modes"",
                    ""minimum"": 0
                },
                ""fog_start"": {
                    ""type"": ""number"",
                    ""description"": ""Fog start distance for linear mode""
                },
                ""fog_end"": {
                    ""type"": ""number"",
                    ""description"": ""Fog end distance for linear mode""
                },
                ""skybox_path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path to skybox material""
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "get_info" => GetLightingInfo(),
                        "set_ambient" => SetAmbient(arguments),
                        "set_fog" => SetFog(arguments),
                        "set_skybox" => SetSkybox(arguments),
                        "bake" => BakeLighting(),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Lighting configuration failed: {ex.Message}");
                }
            });
        }

        private McpToolResult GetLightingInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Global Lighting Settings");
            sb.AppendLine();

            sb.AppendLine("## Ambient");
            sb.AppendLine($"**Mode:** {RenderSettings.ambientMode}");
            sb.AppendLine($"**Intensity:** {RenderSettings.ambientIntensity}");

            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    sb.AppendLine($"**Color:** #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight)}");
                    break;
                case AmbientMode.Trilight:
                    sb.AppendLine($"**Sky Color:** #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor)}");
                    sb.AppendLine($"**Equator Color:** #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientEquatorColor)}");
                    sb.AppendLine($"**Ground Color:** #{ColorUtility.ToHtmlStringRGB(RenderSettings.ambientGroundColor)}");
                    break;
            }

            sb.AppendLine();
            sb.AppendLine("## Fog");
            sb.AppendLine($"**Enabled:** {RenderSettings.fog}");
            if (RenderSettings.fog)
            {
                sb.AppendLine($"**Color:** #{ColorUtility.ToHtmlStringRGB(RenderSettings.fogColor)}");
                sb.AppendLine($"**Mode:** {RenderSettings.fogMode}");
                if (RenderSettings.fogMode == FogMode.Linear)
                {
                    sb.AppendLine($"**Start:** {RenderSettings.fogStartDistance}");
                    sb.AppendLine($"**End:** {RenderSettings.fogEndDistance}");
                }
                else
                {
                    sb.AppendLine($"**Density:** {RenderSettings.fogDensity}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## Skybox");
            sb.AppendLine($"**Material:** {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "(none)")}");

            sb.AppendLine();
            sb.AppendLine("## Other");
            sb.AppendLine($"**Reflection Bounces:** {RenderSettings.reflectionBounces}");
            sb.AppendLine($"**Reflection Intensity:** {RenderSettings.reflectionIntensity}");
            sb.AppendLine($"**Default Reflection Mode:** {RenderSettings.defaultReflectionMode}");

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult SetAmbient(JObject arguments)
        {
            string mode = arguments?["ambient_mode"]?.Value<string>();
            string colorHex = arguments?["ambient_color"]?.Value<string>();
            string skyColorHex = arguments?["ambient_sky_color"]?.Value<string>();
            string equatorColorHex = arguments?["ambient_equator_color"]?.Value<string>();
            string groundColorHex = arguments?["ambient_ground_color"]?.Value<string>();
            float? intensity = arguments?["ambient_intensity"]?.Value<float>();

            // Note: RenderSettings changes are applied immediately, Undo not fully supported

            var changes = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(mode))
            {
                RenderSettings.ambientMode = mode.ToLowerInvariant() switch
                {
                    "gradient" => AmbientMode.Trilight,
                    "flat" => AmbientMode.Flat,
                    _ => AmbientMode.Skybox
                };
                changes.AppendLine($"- Mode: {RenderSettings.ambientMode}");
            }

            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color color))
            {
                RenderSettings.ambientLight = color;
                changes.AppendLine($"- Color: {colorHex}");
            }

            if (!string.IsNullOrWhiteSpace(skyColorHex) && ColorUtility.TryParseHtmlString(skyColorHex, out Color skyColor))
            {
                RenderSettings.ambientSkyColor = skyColor;
                changes.AppendLine($"- Sky Color: {skyColorHex}");
            }

            if (!string.IsNullOrWhiteSpace(equatorColorHex) && ColorUtility.TryParseHtmlString(equatorColorHex, out Color equatorColor))
            {
                RenderSettings.ambientEquatorColor = equatorColor;
                changes.AppendLine($"- Equator Color: {equatorColorHex}");
            }

            if (!string.IsNullOrWhiteSpace(groundColorHex) && ColorUtility.TryParseHtmlString(groundColorHex, out Color groundColor))
            {
                RenderSettings.ambientGroundColor = groundColor;
                changes.AppendLine($"- Ground Color: {groundColorHex}");
            }

            if (intensity.HasValue)
            {
                RenderSettings.ambientIntensity = intensity.Value;
                changes.AppendLine($"- Intensity: {intensity.Value}");
            }

            return McpToolResult.Success($"Updated ambient lighting:\n{changes}");
        }

        private McpToolResult SetFog(JObject arguments)
        {
            bool? enabled = arguments?["fog_enabled"]?.Value<bool>();
            string colorHex = arguments?["fog_color"]?.Value<string>();
            string mode = arguments?["fog_mode"]?.Value<string>();
            float? density = arguments?["fog_density"]?.Value<float>();
            float? start = arguments?["fog_start"]?.Value<float>();
            float? end = arguments?["fog_end"]?.Value<float>();

            // Note: RenderSettings changes are applied immediately, Undo not fully supported

            var changes = new StringBuilder();

            if (enabled.HasValue)
            {
                RenderSettings.fog = enabled.Value;
                changes.AppendLine($"- Enabled: {enabled.Value}");
            }

            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color color))
            {
                RenderSettings.fogColor = color;
                changes.AppendLine($"- Color: {colorHex}");
            }

            if (!string.IsNullOrWhiteSpace(mode))
            {
                RenderSettings.fogMode = mode.ToLowerInvariant() switch
                {
                    "exponential" => FogMode.Exponential,
                    "exponential_squared" => FogMode.ExponentialSquared,
                    _ => FogMode.Linear
                };
                changes.AppendLine($"- Mode: {RenderSettings.fogMode}");
            }

            if (density.HasValue)
            {
                RenderSettings.fogDensity = density.Value;
                changes.AppendLine($"- Density: {density.Value}");
            }

            if (start.HasValue)
            {
                RenderSettings.fogStartDistance = start.Value;
                changes.AppendLine($"- Start: {start.Value}");
            }

            if (end.HasValue)
            {
                RenderSettings.fogEndDistance = end.Value;
                changes.AppendLine($"- End: {end.Value}");
            }

            return McpToolResult.Success($"Updated fog settings:\n{changes}");
        }

        private McpToolResult SetSkybox(JObject arguments)
        {
            string skyboxPath = arguments?["skybox_path"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(skyboxPath))
            {
                return McpToolResult.Error("skybox_path is required for set_skybox action");
            }

            var skybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
            if (skybox == null)
            {
                return McpToolResult.Error($"Skybox material not found: {skyboxPath}");
            }

            // Note: RenderSettings changes are applied immediately, Undo not fully supported
            RenderSettings.skybox = skybox;

            return McpToolResult.Success($"Set skybox: {skybox.name}");
        }

        private McpToolResult BakeLighting()
        {
            if (Lightmapping.isRunning)
            {
                return McpToolResult.Error("Lightmapping is already in progress");
            }

            Lightmapping.BakeAsync();

            return McpToolResult.Success("Started lighting bake. Use Unity Editor to monitor progress.");
        }
    }
}
