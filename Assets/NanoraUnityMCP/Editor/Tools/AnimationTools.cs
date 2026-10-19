using System;
using System.Collections.Generic;
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
    /// Tool for controlling Animator components on GameObjects.
    /// </summary>
    [McpTool("control_animator")]
    public class ControlAnimatorTool : IMcpTool
    {
        public string Name => "control_animator";
        public string Description => "Control Animator component: play/stop states, set parameters, get state info";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""object_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name or selector of the GameObject with Animator""
                },
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""play"", ""crossfade"", ""set_trigger"", ""set_bool"", ""set_int"", ""set_float"", ""get_info"", ""get_parameters"", ""reset""],
                    ""description"": ""Action to perform""
                },
                ""state_name"": {
                    ""type"": ""string"",
                    ""description"": ""State name for play/crossfade actions""
                },
                ""parameter_name"": {
                    ""type"": ""string"",
                    ""description"": ""Parameter name for set_* actions""
                },
                ""value"": {
                    ""description"": ""Value to set (bool, int, or float depending on action)""
                },
                ""layer"": {
                    ""type"": ""integer"",
                    ""description"": ""Animator layer index (default: 0)"",
                    ""default"": 0
                },
                ""transition_duration"": {
                    ""type"": ""number"",
                    ""description"": ""Crossfade transition duration in seconds"",
                    ""default"": 0.25
                }
            },
            ""required"": [""object_name"", ""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string objectName = arguments?["object_name"]?.Value<string>();
                string action = arguments?["action"]?.Value<string>();
                string stateName = arguments?["state_name"]?.Value<string>();
                string paramName = arguments?["parameter_name"]?.Value<string>();
                var valueToken = arguments?["value"];
                int layer = arguments?["layer"]?.Value<int>() ?? 0;
                float transitionDuration = arguments?["transition_duration"]?.Value<float>() ?? 0.25f;

                if (string.IsNullOrWhiteSpace(objectName))
                {
                    return McpToolResult.Error("object_name is required");
                }

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                // Find GameObject
                GameObject target = FindGameObject(objectName);
                if (target == null)
                {
                    return McpToolResult.Error($"GameObject not found: {objectName}");
                }

                // Get Animator component
                var animator = target.GetComponent<Animator>();
                if (animator == null)
                {
                    return McpToolResult.Error($"No Animator component on: {objectName}");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "play" => PlayState(animator, stateName, layer),
                        "crossfade" => CrossfadeState(animator, stateName, layer, transitionDuration),
                        "set_trigger" => SetTrigger(animator, paramName),
                        "set_bool" => SetBool(animator, paramName, valueToken),
                        "set_int" => SetInt(animator, paramName, valueToken),
                        "set_float" => SetFloat(animator, paramName, valueToken),
                        "get_info" => GetAnimatorInfo(animator),
                        "get_parameters" => GetParameters(animator),
                        "reset" => ResetAnimator(animator),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Animator control failed: {ex.Message}");
                }
            });
        }

        private GameObject FindGameObject(string name)
        {
            // Try exact match first
            var obj = GameObject.Find(name);
            if (obj != null) return obj;

            // Try selector if starts with # or .
            if (name.StartsWith("#") || name.StartsWith("."))
            {
                var results = SelectorEngine.Query(name, true);
                return results.FirstOrDefault();
            }

            return null;
        }

        private McpToolResult PlayState(Animator animator, string stateName, int layer)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return McpToolResult.Error("state_name is required for play action");
            }

            animator.Play(stateName, layer);
            return McpToolResult.Success($"Playing state '{stateName}' on layer {layer}");
        }

        private McpToolResult CrossfadeState(Animator animator, string stateName, int layer, float duration)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return McpToolResult.Error("state_name is required for crossfade action");
            }

            animator.CrossFade(stateName, duration, layer);
            return McpToolResult.Success($"Crossfading to state '{stateName}' on layer {layer} (duration: {duration}s)");
        }

        private McpToolResult SetTrigger(Animator animator, string paramName)
        {
            if (string.IsNullOrWhiteSpace(paramName))
            {
                return McpToolResult.Error("parameter_name is required for set_trigger action");
            }

            animator.SetTrigger(paramName);
            return McpToolResult.Success($"Trigger '{paramName}' set");
        }

        private McpToolResult SetBool(Animator animator, string paramName, JToken value)
        {
            if (string.IsNullOrWhiteSpace(paramName))
            {
                return McpToolResult.Error("parameter_name is required for set_bool action");
            }

            bool boolValue = value?.Value<bool>() ?? false;
            animator.SetBool(paramName, boolValue);
            return McpToolResult.Success($"Bool '{paramName}' = {boolValue}");
        }

        private McpToolResult SetInt(Animator animator, string paramName, JToken value)
        {
            if (string.IsNullOrWhiteSpace(paramName))
            {
                return McpToolResult.Error("parameter_name is required for set_int action");
            }

            int intValue = value?.Value<int>() ?? 0;
            animator.SetInteger(paramName, intValue);
            return McpToolResult.Success($"Int '{paramName}' = {intValue}");
        }

        private McpToolResult SetFloat(Animator animator, string paramName, JToken value)
        {
            if (string.IsNullOrWhiteSpace(paramName))
            {
                return McpToolResult.Error("parameter_name is required for set_float action");
            }

            float floatValue = value?.Value<float>() ?? 0f;
            animator.SetFloat(paramName, floatValue);
            return McpToolResult.Success($"Float '{paramName}' = {floatValue}");
        }

        private McpToolResult GetAnimatorInfo(Animator animator)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Animator Info");
            sb.AppendLine();
            sb.AppendLine($"**GameObject:** {animator.gameObject.name}");
            sb.AppendLine($"**Controller:** {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "(none)")}");
            sb.AppendLine($"**Avatar:** {(animator.avatar != null ? animator.avatar.name : "(none)")}");
            sb.AppendLine($"**Apply Root Motion:** {animator.applyRootMotion}");
            sb.AppendLine($"**Update Mode:** {animator.updateMode}");
            sb.AppendLine($"**Culling Mode:** {animator.cullingMode}");
            sb.AppendLine();
            sb.AppendLine($"**Layer Count:** {animator.layerCount}");

            for (int i = 0; i < animator.layerCount; i++)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(i);
                sb.AppendLine($"- Layer {i}: Weight={animator.GetLayerWeight(i):F2}, NormalizedTime={stateInfo.normalizedTime:F2}");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult GetParameters(Animator animator)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Animator Parameters");
            sb.AppendLine();

            foreach (var param in animator.parameters)
            {
                string value = param.type switch
                {
                    AnimatorControllerParameterType.Bool => animator.GetBool(param.name).ToString(),
                    AnimatorControllerParameterType.Int => animator.GetInteger(param.name).ToString(),
                    AnimatorControllerParameterType.Float => animator.GetFloat(param.name).ToString("F2"),
                    AnimatorControllerParameterType.Trigger => "(trigger)",
                    _ => "(unknown)"
                };
                sb.AppendLine($"- {param.name} ({param.type}): {value}");
            }

            if (animator.parameters.Length == 0)
            {
                sb.AppendLine("(no parameters)");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult ResetAnimator(Animator animator)
        {
            animator.Rebind();
            animator.Update(0f);
            return McpToolResult.Success("Animator reset to initial state");
        }
    }

    /// <summary>
    /// Tool for managing Animation Clips in the project.
    /// </summary>
    [McpTool("manage_animation_clip")]
    public class ManageAnimationClipTool : IMcpTool
    {
        public string Name => "manage_animation_clip";
        public string Description => "Manage animation clips: list, info, create simple clips";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""list"", ""info"", ""create"", ""search""],
                    ""description"": ""Action to perform""
                },
                ""clip_path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path of the animation clip (for info action)""
                },
                ""search_query"": {
                    ""type"": ""string"",
                    ""description"": ""Search query for finding animation clips""
                },
                ""folder_path"": {
                    ""type"": ""string"",
                    ""description"": ""Folder path for listing clips (default: Assets)"",
                    ""default"": ""Assets""
                },
                ""clip_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name for new animation clip (create action)""
                },
                ""output_path"": {
                    ""type"": ""string"",
                    ""description"": ""Output path for new clip (create action)""
                },
                ""duration"": {
                    ""type"": ""number"",
                    ""description"": ""Duration in seconds for new clip"",
                    ""default"": 1.0
                },
                ""loop"": {
                    ""type"": ""boolean"",
                    ""description"": ""Whether the clip should loop"",
                    ""default"": false
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();
                string clipPath = arguments?["clip_path"]?.Value<string>();
                string searchQuery = arguments?["search_query"]?.Value<string>();
                string folderPath = arguments?["folder_path"]?.Value<string>() ?? "Assets";
                string clipName = arguments?["clip_name"]?.Value<string>();
                string outputPath = arguments?["output_path"]?.Value<string>();
                float duration = arguments?["duration"]?.Value<float>() ?? 1.0f;
                bool loop = arguments?["loop"]?.Value<bool>() ?? false;

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "list" => ListClips(folderPath),
                        "info" => GetClipInfo(clipPath),
                        "search" => SearchClips(searchQuery),
                        "create" => CreateClip(clipName, outputPath, duration, loop),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Animation clip operation failed: {ex.Message}");
                }
            });
        }

        private McpToolResult ListClips(string folderPath)
        {
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
            var sb = new StringBuilder();
            sb.AppendLine($"# Animation Clips in {folderPath}");
            sb.AppendLine();
            sb.AppendLine($"Found {guids.Length} clips:");
            sb.AppendLine();

            foreach (var guid in guids.Take(50))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    sb.AppendLine($"- {clip.name} ({clip.length:F2}s, {(clip.isLooping ? "loop" : "once")}) - {path}");
                }
            }

            if (guids.Length > 50)
            {
                sb.AppendLine($"... and {guids.Length - 50} more");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult GetClipInfo(string clipPath)
        {
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                return McpToolResult.Error("clip_path is required for info action");
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                return McpToolResult.Error($"Animation clip not found: {clipPath}");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Animation Clip: {clip.name}");
            sb.AppendLine();
            sb.AppendLine($"**Path:** {clipPath}");
            sb.AppendLine($"**Duration:** {clip.length:F3}s");
            sb.AppendLine($"**Frame Rate:** {clip.frameRate} fps");
            sb.AppendLine($"**Frames:** {Mathf.RoundToInt(clip.length * clip.frameRate)}");
            sb.AppendLine($"**Looping:** {clip.isLooping}");
            sb.AppendLine($"**Legacy:** {clip.legacy}");
            sb.AppendLine($"**Wrap Mode:** {clip.wrapMode}");
            sb.AppendLine();

            var bindings = AnimationUtility.GetCurveBindings(clip);
            sb.AppendLine($"**Animated Properties:** {bindings.Length}");

            var groupedBindings = bindings.GroupBy(b => b.type?.Name ?? "Unknown").Take(10);
            foreach (var group in groupedBindings)
            {
                sb.AppendLine($"- {group.Key}: {group.Count()} properties");
            }

            var events = AnimationUtility.GetAnimationEvents(clip);
            if (events.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"**Events:** {events.Length}");
                foreach (var evt in events.Take(5))
                {
                    sb.AppendLine($"- {evt.time:F2}s: {evt.functionName}");
                }
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult SearchClips(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return McpToolResult.Error("search_query is required for search action");
            }

            var guids = AssetDatabase.FindAssets($"t:AnimationClip {query}");
            var sb = new StringBuilder();
            sb.AppendLine($"# Animation Clips matching '{query}'");
            sb.AppendLine();
            sb.AppendLine($"Found {guids.Length} clips:");
            sb.AppendLine();

            foreach (var guid in guids.Take(30))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    sb.AppendLine($"- {clip.name} ({clip.length:F2}s) - {path}");
                }
            }

            if (guids.Length > 30)
            {
                sb.AppendLine($"... and {guids.Length - 30} more");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult CreateClip(string clipName, string outputPath, float duration, bool loop)
        {
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return McpToolResult.Error("clip_name is required for create action");
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = $"Assets/{clipName}.anim";
            }

            if (!outputPath.EndsWith(".anim"))
            {
                outputPath += ".anim";
            }

            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f
            };

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Create a simple placeholder curve
            var curve = AnimationCurve.Linear(0, 0, duration, 0);
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", curve);

            AssetDatabase.CreateAsset(clip, outputPath);
            AssetDatabase.SaveAssets();

            return McpToolResult.Success($"Created animation clip: {outputPath}\n- Duration: {duration}s\n- Loop: {loop}");
        }
    }
}
