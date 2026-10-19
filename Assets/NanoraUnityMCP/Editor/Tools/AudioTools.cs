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
    /// Tool for managing AudioSource components.
    /// </summary>
    [McpTool("manage_audio_source")]
    public class ManageAudioSourceTool : IMcpTool
    {
        public string Name => "manage_audio_source";
        public string Description => "Manage AudioSource: create, configure, list, and get info";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""create"", ""configure"", ""info"", ""list"", ""add_to_object""],
                    ""description"": ""Action to perform""
                },
                ""object_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the GameObject with AudioSource""
                },
                ""clip_path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path to AudioClip""
                },
                ""volume"": {
                    ""type"": ""number"",
                    ""description"": ""Volume (0.0 to 1.0)"",
                    ""minimum"": 0,
                    ""maximum"": 1
                },
                ""pitch"": {
                    ""type"": ""number"",
                    ""description"": ""Pitch (-3.0 to 3.0)"",
                    ""minimum"": -3,
                    ""maximum"": 3
                },
                ""loop"": {
                    ""type"": ""boolean"",
                    ""description"": ""Whether to loop the audio""
                },
                ""play_on_awake"": {
                    ""type"": ""boolean"",
                    ""description"": ""Play automatically on start""
                },
                ""spatial_blend"": {
                    ""type"": ""number"",
                    ""description"": ""2D (0) to 3D (1) spatial blend"",
                    ""minimum"": 0,
                    ""maximum"": 1
                },
                ""min_distance"": {
                    ""type"": ""number"",
                    ""description"": ""Minimum distance for 3D audio rolloff""
                },
                ""max_distance"": {
                    ""type"": ""number"",
                    ""description"": ""Maximum distance for 3D audio rolloff""
                },
                ""priority"": {
                    ""type"": ""integer"",
                    ""description"": ""Priority (0=highest, 256=lowest)"",
                    ""minimum"": 0,
                    ""maximum"": 256
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();
                string objectName = arguments?["object_name"]?.Value<string>();
                string clipPath = arguments?["clip_path"]?.Value<string>();
                float? volume = arguments?["volume"]?.Value<float>();
                float? pitch = arguments?["pitch"]?.Value<float>();
                bool? loop = arguments?["loop"]?.Value<bool>();
                bool? playOnAwake = arguments?["play_on_awake"]?.Value<bool>();
                float? spatialBlend = arguments?["spatial_blend"]?.Value<float>();
                float? minDistance = arguments?["min_distance"]?.Value<float>();
                float? maxDistance = arguments?["max_distance"]?.Value<float>();
                int? priority = arguments?["priority"]?.Value<int>();

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "create" => CreateAudioSource(objectName, clipPath, volume, pitch, loop, playOnAwake, spatialBlend),
                        "configure" => ConfigureAudioSource(objectName, clipPath, volume, pitch, loop, playOnAwake, spatialBlend, minDistance, maxDistance, priority),
                        "info" => GetAudioSourceInfo(objectName),
                        "list" => ListAudioSources(),
                        "add_to_object" => AddToObject(objectName, clipPath),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"AudioSource operation failed: {ex.Message}");
                }
            });
        }

        private McpToolResult CreateAudioSource(string name, string clipPath, float? volume, float? pitch, bool? loop, bool? playOnAwake, float? spatialBlend)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "AudioSource";
            }

            var go = new GameObject(name);
            var audioSource = go.AddComponent<AudioSource>();

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null)
                {
                    audioSource.clip = clip;
                }
            }

            if (volume.HasValue) audioSource.volume = volume.Value;
            if (pitch.HasValue) audioSource.pitch = pitch.Value;
            if (loop.HasValue) audioSource.loop = loop.Value;
            if (playOnAwake.HasValue) audioSource.playOnAwake = playOnAwake.Value;
            if (spatialBlend.HasValue) audioSource.spatialBlend = spatialBlend.Value;

            Undo.RegisterCreatedObjectUndo(go, "Create AudioSource");
            Selection.activeGameObject = go;

            return McpToolResult.Success($"Created AudioSource: {name}\n" +
                $"- Clip: {(audioSource.clip != null ? audioSource.clip.name : "(none)")}\n" +
                $"- Volume: {audioSource.volume}\n" +
                $"- Loop: {audioSource.loop}");
        }

        private McpToolResult ConfigureAudioSource(string objectName, string clipPath, float? volume, float? pitch, bool? loop, bool? playOnAwake, float? spatialBlend, float? minDistance, float? maxDistance, int? priority)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return McpToolResult.Error("object_name is required for configure action");
            }

            var go = GameObject.Find(objectName);
            if (go == null)
            {
                return McpToolResult.Error($"GameObject not found: {objectName}");
            }

            var audioSource = go.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                return McpToolResult.Error($"No AudioSource on: {objectName}");
            }

            Undo.RecordObject(audioSource, "Configure AudioSource");

            var changes = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null)
                {
                    audioSource.clip = clip;
                    changes.AppendLine($"- Clip: {clip.name}");
                }
            }

            if (volume.HasValue)
            {
                audioSource.volume = volume.Value;
                changes.AppendLine($"- Volume: {volume.Value}");
            }

            if (pitch.HasValue)
            {
                audioSource.pitch = pitch.Value;
                changes.AppendLine($"- Pitch: {pitch.Value}");
            }

            if (loop.HasValue)
            {
                audioSource.loop = loop.Value;
                changes.AppendLine($"- Loop: {loop.Value}");
            }

            if (playOnAwake.HasValue)
            {
                audioSource.playOnAwake = playOnAwake.Value;
                changes.AppendLine($"- Play On Awake: {playOnAwake.Value}");
            }

            if (spatialBlend.HasValue)
            {
                audioSource.spatialBlend = spatialBlend.Value;
                changes.AppendLine($"- Spatial Blend: {spatialBlend.Value}");
            }

            if (minDistance.HasValue)
            {
                audioSource.minDistance = minDistance.Value;
                changes.AppendLine($"- Min Distance: {minDistance.Value}");
            }

            if (maxDistance.HasValue)
            {
                audioSource.maxDistance = maxDistance.Value;
                changes.AppendLine($"- Max Distance: {maxDistance.Value}");
            }

            if (priority.HasValue)
            {
                audioSource.priority = priority.Value;
                changes.AppendLine($"- Priority: {priority.Value}");
            }

            EditorUtility.SetDirty(audioSource);

            return McpToolResult.Success($"Configured AudioSource: {objectName}\n{changes}");
        }

        private McpToolResult GetAudioSourceInfo(string objectName)
        {
            AudioSource audioSource;

            if (!string.IsNullOrWhiteSpace(objectName))
            {
                var go = GameObject.Find(objectName);
                if (go == null)
                {
                    return McpToolResult.Error($"GameObject not found: {objectName}");
                }
                audioSource = go.GetComponent<AudioSource>();
            }
            else
            {
                audioSource = UnityCompatibility.FindFirstObjectByType<AudioSource>();
            }

            if (audioSource == null)
            {
                return McpToolResult.Error("No AudioSource found");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# AudioSource: {audioSource.gameObject.name}");
            sb.AppendLine();
            sb.AppendLine($"**Clip:** {(audioSource.clip != null ? audioSource.clip.name : "(none)")}");

            if (audioSource.clip != null)
            {
                sb.AppendLine($"**Clip Duration:** {audioSource.clip.length:F2}s");
                sb.AppendLine($"**Clip Channels:** {audioSource.clip.channels}");
                sb.AppendLine($"**Clip Frequency:** {audioSource.clip.frequency} Hz");
            }

            sb.AppendLine();
            sb.AppendLine("## Settings");
            sb.AppendLine($"**Volume:** {audioSource.volume}");
            sb.AppendLine($"**Pitch:** {audioSource.pitch}");
            sb.AppendLine($"**Mute:** {audioSource.mute}");
            sb.AppendLine($"**Loop:** {audioSource.loop}");
            sb.AppendLine($"**Play On Awake:** {audioSource.playOnAwake}");
            sb.AppendLine($"**Priority:** {audioSource.priority}");
            sb.AppendLine();
            sb.AppendLine("## 3D Settings");
            sb.AppendLine($"**Spatial Blend:** {audioSource.spatialBlend} ({(audioSource.spatialBlend == 0 ? "2D" : audioSource.spatialBlend == 1 ? "3D" : "Mixed")})");
            sb.AppendLine($"**Doppler Level:** {audioSource.dopplerLevel}");
            sb.AppendLine($"**Spread:** {audioSource.spread}");
            sb.AppendLine($"**Min Distance:** {audioSource.minDistance}");
            sb.AppendLine($"**Max Distance:** {audioSource.maxDistance}");
            sb.AppendLine($"**Rolloff Mode:** {audioSource.rolloffMode}");

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult ListAudioSources()
        {
            var audioSources = UnityCompatibility.FindObjectsByType<AudioSource>();
            var sb = new StringBuilder();
            sb.AppendLine("# AudioSources in Scene");
            sb.AppendLine();
            sb.AppendLine($"Found {audioSources.Length} AudioSource(s):");
            sb.AppendLine();

            foreach (var source in audioSources)
            {
                string clipInfo = source.clip != null ? $"{source.clip.name} ({source.clip.length:F1}s)" : "(no clip)";
                string state = source.isPlaying ? "playing" : "stopped";
                sb.AppendLine($"- {source.gameObject.name}: {clipInfo}, vol={source.volume:F1}, {state}");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult AddToObject(string objectName, string clipPath)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return McpToolResult.Error("object_name is required for add_to_object action");
            }

            var go = GameObject.Find(objectName);
            if (go == null)
            {
                return McpToolResult.Error($"GameObject not found: {objectName}");
            }

            var audioSource = go.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                return McpToolResult.Error($"AudioSource already exists on: {objectName}");
            }

            audioSource = Undo.AddComponent<AudioSource>(go);

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null)
                {
                    audioSource.clip = clip;
                }
            }

            return McpToolResult.Success($"Added AudioSource to: {objectName}");
        }
    }

    /// <summary>
    /// Tool for playing and controlling audio.
    /// </summary>
    [McpTool("play_audio")]
    public class PlayAudioTool : IMcpTool
    {
        public string Name => "play_audio";
        public string Description => "Control audio playback: play, stop, pause, resume, play one-shot";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""play"", ""stop"", ""pause"", ""resume"", ""play_one_shot"", ""stop_all"", ""search_clips""],
                    ""description"": ""Playback action""
                },
                ""object_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the GameObject with AudioSource""
                },
                ""clip_path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path to AudioClip for play_one_shot""
                },
                ""volume_scale"": {
                    ""type"": ""number"",
                    ""description"": ""Volume scale for play_one_shot (0.0 to 1.0)"",
                    ""default"": 1.0
                },
                ""search_query"": {
                    ""type"": ""string"",
                    ""description"": ""Search query for finding audio clips""
                },
                ""delay"": {
                    ""type"": ""number"",
                    ""description"": ""Delay in seconds before playing"",
                    ""default"": 0
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();
                string objectName = arguments?["object_name"]?.Value<string>();
                string clipPath = arguments?["clip_path"]?.Value<string>();
                float volumeScale = arguments?["volume_scale"]?.Value<float>() ?? 1.0f;
                string searchQuery = arguments?["search_query"]?.Value<string>();
                float delay = arguments?["delay"]?.Value<float>() ?? 0;

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "play" => PlayAudio(objectName, delay),
                        "stop" => StopAudio(objectName),
                        "pause" => PauseAudio(objectName),
                        "resume" => ResumeAudio(objectName),
                        "play_one_shot" => PlayOneShot(objectName, clipPath, volumeScale),
                        "stop_all" => StopAllAudio(),
                        "search_clips" => SearchClips(searchQuery),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Audio playback failed: {ex.Message}");
                }
            });
        }

        private McpToolResult PlayAudio(string objectName, float delay)
        {
            var audioSource = FindAudioSource(objectName);
            if (audioSource == null)
            {
                return McpToolResult.Error(objectName != null
                    ? $"No AudioSource on: {objectName}"
                    : "No AudioSource found in scene");
            }

            if (audioSource.clip == null)
            {
                return McpToolResult.Error($"No AudioClip assigned to: {audioSource.gameObject.name}");
            }

            if (delay > 0)
            {
                audioSource.PlayDelayed(delay);
                return McpToolResult.Success($"Playing '{audioSource.clip.name}' on '{audioSource.gameObject.name}' after {delay}s delay");
            }
            else
            {
                audioSource.Play();
                return McpToolResult.Success($"Playing '{audioSource.clip.name}' on '{audioSource.gameObject.name}'");
            }
        }

        private McpToolResult StopAudio(string objectName)
        {
            var audioSource = FindAudioSource(objectName);
            if (audioSource == null)
            {
                return McpToolResult.Error(objectName != null
                    ? $"No AudioSource on: {objectName}"
                    : "No AudioSource found in scene");
            }

            audioSource.Stop();
            return McpToolResult.Success($"Stopped audio on '{audioSource.gameObject.name}'");
        }

        private McpToolResult PauseAudio(string objectName)
        {
            var audioSource = FindAudioSource(objectName);
            if (audioSource == null)
            {
                return McpToolResult.Error(objectName != null
                    ? $"No AudioSource on: {objectName}"
                    : "No AudioSource found in scene");
            }

            audioSource.Pause();
            return McpToolResult.Success($"Paused audio on '{audioSource.gameObject.name}'");
        }

        private McpToolResult ResumeAudio(string objectName)
        {
            var audioSource = FindAudioSource(objectName);
            if (audioSource == null)
            {
                return McpToolResult.Error(objectName != null
                    ? $"No AudioSource on: {objectName}"
                    : "No AudioSource found in scene");
            }

            audioSource.UnPause();
            return McpToolResult.Success($"Resumed audio on '{audioSource.gameObject.name}'");
        }

        private McpToolResult PlayOneShot(string objectName, string clipPath, float volumeScale)
        {
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                return McpToolResult.Error("clip_path is required for play_one_shot action");
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                return McpToolResult.Error($"AudioClip not found: {clipPath}");
            }

            var audioSource = FindAudioSource(objectName);
            if (audioSource == null)
            {
                // Create temporary AudioSource if none specified
                var tempGO = new GameObject("TempAudioSource");
                audioSource = tempGO.AddComponent<AudioSource>();
                audioSource.PlayOneShot(clip, volumeScale);

                // Schedule destruction after clip plays
                UnityEngine.Object.DestroyImmediate(tempGO, true);

                return McpToolResult.Success($"Playing one-shot '{clip.name}' (volume: {volumeScale})");
            }

            audioSource.PlayOneShot(clip, volumeScale);
            return McpToolResult.Success($"Playing one-shot '{clip.name}' on '{audioSource.gameObject.name}' (volume: {volumeScale})");
        }

        private McpToolResult StopAllAudio()
        {
            var audioSources = UnityCompatibility.FindObjectsByType<AudioSource>();
            int count = 0;

            foreach (var source in audioSources)
            {
                if (source.isPlaying)
                {
                    source.Stop();
                    count++;
                }
            }

            return McpToolResult.Success($"Stopped {count} playing AudioSource(s)");
        }

        private McpToolResult SearchClips(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return McpToolResult.Error("search_query is required for search_clips action");
            }

            var guids = AssetDatabase.FindAssets($"t:AudioClip {query}");
            var sb = new StringBuilder();
            sb.AppendLine($"# AudioClips matching '{query}'");
            sb.AppendLine();
            sb.AppendLine($"Found {guids.Length} clips:");
            sb.AppendLine();

            foreach (var guid in guids.Take(30))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    sb.AppendLine($"- {clip.name} ({clip.length:F2}s, {clip.channels}ch) - {path}");
                }
            }

            if (guids.Length > 30)
            {
                sb.AppendLine($"... and {guids.Length - 30} more");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private AudioSource FindAudioSource(string objectName)
        {
            if (!string.IsNullOrWhiteSpace(objectName))
            {
                var go = GameObject.Find(objectName);
                if (go != null)
                {
                    return go.GetComponent<AudioSource>();
                }
                return null;
            }

            return UnityCompatibility.FindFirstObjectByType<AudioSource>();
        }
    }
}
