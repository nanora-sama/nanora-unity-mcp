using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Analyzes frame rendering using Frame Debugger internal APIs.
    /// Provides draw call analysis and optimization recommendations.
    /// </summary>
    [McpTool("analyze_frame")]
    public class AnalyzeFrameTool : IMcpTool
    {
        public string Name => "analyze_frame";
        public string Description => "Analyze the current frame's rendering performance. " +
            "Provides draw call count, shader usage, and optimization recommendations.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""enable_frame_debugger"": {
                    ""type"": ""boolean"",
                    ""description"": ""Enable Frame Debugger to capture frame data"",
                    ""default"": true
                },
                ""include_shader_details"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include detailed shader information"",
                    ""default"": false
                },
                ""include_recommendations"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include optimization recommendations"",
                    ""default"": true
                }
            }
        }");

        // Cached reflection types
        private static Type s_FrameDebuggerUtilityType;
        private static Type s_FrameDebuggerEventType;
        private static bool s_ReflectionInitialized = false;

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                bool enableFrameDebugger = arguments?["enable_frame_debugger"]?.Value<bool>() ?? true;
                bool includeShaderDetails = arguments?["include_shader_details"]?.Value<bool>() ?? false;
                bool includeRecommendations = arguments?["include_recommendations"]?.Value<bool>() ?? true;

                try
                {
                    var analysis = AnalyzeCurrentFrame(enableFrameDebugger, includeShaderDetails);

                    var sb = new StringBuilder();
                    sb.AppendLine("# Frame Analysis Report");
                    sb.AppendLine();

                    // Basic stats
                    sb.AppendLine("## Rendering Statistics");
                    sb.AppendLine($"- Total Draw Calls: {analysis.TotalDrawCalls}");
                    sb.AppendLine($"- Batched Draw Calls: {analysis.BatchedDrawCalls}");
                    sb.AppendLine($"- Set Pass Calls: {analysis.SetPassCalls}");
                    sb.AppendLine($"- Total Triangles: {analysis.TotalTriangles:N0}");
                    sb.AppendLine($"- Total Vertices: {analysis.TotalVertices:N0}");
                    sb.AppendLine();

                    // Render pipeline info
                    sb.AppendLine("## Render Pipeline");
                    sb.AppendLine($"- Current Pipeline: {analysis.RenderPipeline}");
                    sb.AppendLine($"- Color Space: {PlayerSettings.colorSpace}");
                    sb.AppendLine();

                    // Camera info
                    sb.AppendLine("## Active Cameras");
                    foreach (var cam in analysis.Cameras)
                    {
                        sb.AppendLine($"- {cam.Name}: {cam.ClearFlags}, Depth={cam.Depth}, Culling={cam.CullingMask}");
                    }
                    sb.AppendLine();

                    // Shader breakdown
                    if (analysis.ShaderUsage.Count > 0)
                    {
                        sb.AppendLine("## Shader Usage");
                        foreach (var shader in analysis.ShaderUsage.OrderByDescending(s => s.Value))
                        {
                            sb.AppendLine($"- {shader.Key}: {shader.Value} draw calls");
                        }
                        sb.AppendLine();
                    }

                    // Render passes
                    if (analysis.RenderPasses.Count > 0)
                    {
                        sb.AppendLine("## Render Passes");
                        foreach (var pass in analysis.RenderPasses)
                        {
                            sb.AppendLine($"- {pass.Name}: {pass.DrawCalls} draw calls");
                        }
                        sb.AppendLine();
                    }

                    // Optimization recommendations
                    if (includeRecommendations)
                    {
                        sb.AppendLine("## Optimization Recommendations");
                        var recommendations = GenerateRecommendations(analysis);
                        if (recommendations.Count == 0)
                        {
                            sb.AppendLine("- No immediate issues detected.");
                        }
                        else
                        {
                            foreach (var rec in recommendations)
                            {
                                sb.AppendLine($"- [{rec.Priority}] {rec.Message}");
                            }
                        }
                    }

                    return McpToolResult.Success(sb.ToString());
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Frame analysis failed: {ex.Message}");
                }
            });
        }

        private FrameAnalysis AnalyzeCurrentFrame(bool enableFrameDebugger, bool includeShaderDetails)
        {
            var analysis = new FrameAnalysis();

            // Get render pipeline
            analysis.RenderPipeline = GetCurrentRenderPipeline();

            // Analyze cameras
            var cameras = Camera.allCameras;
            foreach (var cam in cameras)
            {
                analysis.Cameras.Add(new CameraInfo
                {
                    Name = cam.name,
                    ClearFlags = cam.clearFlags.ToString(),
                    Depth = cam.depth,
                    CullingMask = cam.cullingMask.ToString()
                });
            }

            // Try to get Frame Debugger data via reflection
            if (enableFrameDebugger)
            {
                TryAnalyzeFrameDebugger(analysis);
            }

            // Analyze scene renderers for additional stats
            AnalyzeSceneRenderers(analysis, includeShaderDetails);

            return analysis;
        }

        private string GetCurrentRenderPipeline()
        {
            var currentPipeline = GraphicsSettings.currentRenderPipeline;
            if (currentPipeline != null)
            {
                var pipelineName = currentPipeline.GetType().Name;
                if (pipelineName.Contains("Universal"))
                {
                    return "Universal Render Pipeline (URP)";
                }
                if (pipelineName.Contains("HighDefinition") || pipelineName.Contains("HDRP"))
                {
                    return "High Definition Render Pipeline (HDRP)";
                }
                return pipelineName;
            }
            return "Built-in Render Pipeline";
        }

        private void TryAnalyzeFrameDebugger(FrameAnalysis analysis)
        {
            try
            {
                InitializeReflection();

                if (s_FrameDebuggerUtilityType == null)
                {
                    return;
                }

                // Try to enable Frame Debugger
                var enabledProperty = s_FrameDebuggerUtilityType.GetProperty("enabled",
                    BindingFlags.Public | BindingFlags.Static);

                if (enabledProperty != null)
                {
                    enabledProperty.SetValue(null, true);
                }

                // Get frame events count
                var countProperty = s_FrameDebuggerUtilityType.GetProperty("count",
                    BindingFlags.Public | BindingFlags.Static);

                if (countProperty != null)
                {
                    var count = (int)countProperty.GetValue(null);
                    analysis.TotalDrawCalls = count;
                }

                // Try to get detailed events
                var getEventsMethod = s_FrameDebuggerUtilityType.GetMethod("GetFrameEvents",
                    BindingFlags.Public | BindingFlags.Static);

                if (getEventsMethod != null)
                {
                    var events = getEventsMethod.Invoke(null, null);
                    if (events is Array eventArray)
                    {
                        AnalyzeFrameEvents(analysis, eventArray);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Frame Debugger reflection failed: {ex.Message}");
            }
        }

        private void InitializeReflection()
        {
            if (s_ReflectionInitialized)
            {
                return;
            }

            s_ReflectionInitialized = true;

            // Try to find FrameDebuggerUtility
            var unityEditorAssembly = typeof(Editor).Assembly;
            s_FrameDebuggerUtilityType = unityEditorAssembly.GetType("UnityEditorInternal.FrameDebuggerUtility");
            s_FrameDebuggerEventType = unityEditorAssembly.GetType("UnityEditorInternal.FrameDebuggerEvent");
        }

        private void AnalyzeFrameEvents(FrameAnalysis analysis, Array events)
        {
            if (s_FrameDebuggerEventType == null || events == null)
            {
                return;
            }

            var shaderNameField = s_FrameDebuggerEventType.GetField("shaderName");
            var passNameField = s_FrameDebuggerEventType.GetField("passName");
            var vertexCountField = s_FrameDebuggerEventType.GetField("vertexCount");
            var indexCountField = s_FrameDebuggerEventType.GetField("indexCount");

            foreach (var evt in events)
            {
                if (evt == null) continue;

                // Extract shader name
                if (shaderNameField != null)
                {
                    var shaderName = shaderNameField.GetValue(evt) as string;
                    if (!string.IsNullOrEmpty(shaderName))
                    {
                        if (!analysis.ShaderUsage.ContainsKey(shaderName))
                        {
                            analysis.ShaderUsage[shaderName] = 0;
                        }
                        analysis.ShaderUsage[shaderName]++;
                    }
                }

                // Extract vertex/triangle counts
                if (vertexCountField != null)
                {
                    var vertexCount = (int)vertexCountField.GetValue(evt);
                    analysis.TotalVertices += vertexCount;
                }

                if (indexCountField != null)
                {
                    var indexCount = (int)indexCountField.GetValue(evt);
                    analysis.TotalTriangles += indexCount / 3;
                }
            }
        }

        private void AnalyzeSceneRenderers(FrameAnalysis analysis, bool includeShaderDetails)
        {
            var renderers = UnityCompatibility.FindObjectsByType<Renderer>();
            var materials = new HashSet<Material>();

            int visibleRenderers = 0;

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                visibleRenderers++;

                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        materials.Add(mat);

                        if (mat.shader != null)
                        {
                            var shaderName = mat.shader.name;
                            if (!analysis.ShaderUsage.ContainsKey(shaderName))
                            {
                                analysis.ShaderUsage[shaderName] = 0;
                            }
                            analysis.ShaderUsage[shaderName]++;
                        }
                    }
                }

                // Get mesh stats
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    analysis.TotalVertices += meshFilter.sharedMesh.vertexCount;
                    analysis.TotalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
                }

                var skinnedMesh = renderer as SkinnedMeshRenderer;
                if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
                {
                    analysis.TotalVertices += skinnedMesh.sharedMesh.vertexCount;
                    analysis.TotalTriangles += skinnedMesh.sharedMesh.triangles.Length / 3;
                }
            }

            // Estimate draw calls if Frame Debugger data wasn't available
            if (analysis.TotalDrawCalls == 0)
            {
                analysis.TotalDrawCalls = visibleRenderers;
            }

            // Count unique materials
            analysis.SetPassCalls = materials.Count;
        }

        private List<Recommendation> GenerateRecommendations(FrameAnalysis analysis)
        {
            var recommendations = new List<Recommendation>();

            // High draw call count
            if (analysis.TotalDrawCalls > 1000)
            {
                recommendations.Add(new Recommendation
                {
                    Priority = "HIGH",
                    Message = $"High draw call count ({analysis.TotalDrawCalls}). Consider using GPU instancing, static batching, or reducing object count."
                });
            }
            else if (analysis.TotalDrawCalls > 500)
            {
                recommendations.Add(new Recommendation
                {
                    Priority = "MEDIUM",
                    Message = $"Moderate draw call count ({analysis.TotalDrawCalls}). Consider enabling batching or using fewer materials."
                });
            }

            // High triangle count
            if (analysis.TotalTriangles > 1000000)
            {
                recommendations.Add(new Recommendation
                {
                    Priority = "HIGH",
                    Message = $"Very high triangle count ({analysis.TotalTriangles:N0}). Consider using LOD groups or reducing mesh complexity."
                });
            }

            // Many unique shaders
            if (analysis.ShaderUsage.Count > 20)
            {
                recommendations.Add(new Recommendation
                {
                    Priority = "MEDIUM",
                    Message = $"Many unique shaders ({analysis.ShaderUsage.Count}). Consider consolidating materials to improve batching."
                });
            }

            // Check for Standard shader in URP
            if (analysis.RenderPipeline.Contains("URP") && analysis.ShaderUsage.ContainsKey("Standard"))
            {
                recommendations.Add(new Recommendation
                {
                    Priority = "HIGH",
                    Message = "Using Built-in 'Standard' shader in URP project. Upgrade to URP/Lit for better performance."
                });
            }

            // Multiple cameras
            if (analysis.Cameras.Count > 2)
            {
                recommendations.Add(new Recommendation
                {
                    Priority = "LOW",
                    Message = $"Multiple cameras active ({analysis.Cameras.Count}). Ensure only necessary cameras are rendering."
                });
            }

            // Check for batching disabled (BatchedDrawCalls = 0 when there are draw calls)
            if (analysis.BatchedDrawCalls == 0 && analysis.TotalDrawCalls > 10)
            {
                recommendations.Add(new Recommendation
                {
                    Priority = "MEDIUM",
                    Message = $"No batched draw calls detected ({analysis.TotalDrawCalls} total). " +
                        "Static Batching or GPU Instancing may not be configured. " +
                        "Check: Edit > Project Settings > Player > Other Settings > Static Batching / Dynamic Batching."
                });
            }

            return recommendations;
        }

        private class FrameAnalysis
        {
            public int TotalDrawCalls { get; set; }
            public int BatchedDrawCalls { get; set; }
            public int SetPassCalls { get; set; }
            public int TotalTriangles { get; set; }
            public int TotalVertices { get; set; }
            public string RenderPipeline { get; set; }
            public List<CameraInfo> Cameras { get; } = new List<CameraInfo>();
            public Dictionary<string, int> ShaderUsage { get; } = new Dictionary<string, int>();
            public List<RenderPassInfo> RenderPasses { get; } = new List<RenderPassInfo>();
        }

        private class CameraInfo
        {
            public string Name { get; set; }
            public string ClearFlags { get; set; }
            public float Depth { get; set; }
            public string CullingMask { get; set; }
        }

        private class RenderPassInfo
        {
            public string Name { get; set; }
            public int DrawCalls { get; set; }
        }

        private class Recommendation
        {
            public string Priority { get; set; }
            public string Message { get; set; }
        }
    }
}
