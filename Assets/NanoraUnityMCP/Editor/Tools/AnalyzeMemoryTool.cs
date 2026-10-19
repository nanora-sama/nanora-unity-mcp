using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Analyzes memory usage using Unity's Profiling APIs.
    /// Provides memory breakdown and leak detection hints.
    /// </summary>
    [McpTool("analyze_memory")]
    public class AnalyzeMemoryTool : IMcpTool
    {
        public string Name => "analyze_memory";
        public string Description => "Analyze current memory usage. " +
            "Provides breakdown by category (textures, meshes, audio, etc.) and optimization suggestions.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""include_asset_breakdown"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include detailed asset memory breakdown"",
                    ""default"": true
                },
                ""include_top_consumers"": {
                    ""type"": ""boolean"",
                    ""description"": ""List top memory consuming assets"",
                    ""default"": true
                },
                ""top_count"": {
                    ""type"": ""integer"",
                    ""description"": ""Number of top consumers to list"",
                    ""default"": 10
                },
                ""take_snapshot"": {
                    ""type"": ""boolean"",
                    ""description"": ""Take a memory snapshot (requires Memory Profiler package)"",
                    ""default"": false
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                bool includeAssetBreakdown = arguments?["include_asset_breakdown"]?.Value<bool>() ?? true;
                bool includeTopConsumers = arguments?["include_top_consumers"]?.Value<bool>() ?? true;
                int topCount = arguments?["top_count"]?.Value<int>() ?? 10;
                bool takeSnapshot = arguments?["take_snapshot"]?.Value<bool>() ?? false;

                try
                {
                    var analysis = AnalyzeMemory(includeAssetBreakdown, includeTopConsumers, topCount);

                    var sb = new StringBuilder();
                    sb.AppendLine("# Memory Analysis Report");
                    sb.AppendLine();

                    // Overview
                    sb.AppendLine("## Memory Overview");
                    sb.AppendLine($"- Total Reserved: {FormatBytes(analysis.TotalReservedMemory)}");
                    sb.AppendLine($"- Total Allocated: {FormatBytes(analysis.TotalAllocatedMemory)}");
                    sb.AppendLine($"- Mono Heap: {FormatBytes(analysis.MonoHeapSize)}");
                    sb.AppendLine($"- Mono Used: {FormatBytes(analysis.MonoUsedSize)}");
                    sb.AppendLine($"- GFX Memory: {FormatBytes(analysis.GfxMemory)}");
                    sb.AppendLine();

                    // Asset breakdown
                    if (includeAssetBreakdown)
                    {
                        sb.AppendLine("## Asset Memory Breakdown");
                        sb.AppendLine($"- Textures: {FormatBytes(analysis.TextureMemory)} ({analysis.TextureCount} assets)");
                        sb.AppendLine($"- Meshes: {FormatBytes(analysis.MeshMemory)} ({analysis.MeshCount} assets)");
                        sb.AppendLine($"- Materials: {analysis.MaterialCount} assets");
                        sb.AppendLine($"- Shaders: {analysis.ShaderCount} assets");
                        sb.AppendLine($"- Audio Clips: {FormatBytes(analysis.AudioMemory)} ({analysis.AudioClipCount} assets)");
                        sb.AppendLine($"- Animation Clips: {analysis.AnimationClipCount} assets");
                        sb.AppendLine($"- Render Textures: {FormatBytes(analysis.RenderTextureMemory)} ({analysis.RenderTextureCount} assets)");
                        sb.AppendLine();
                    }

                    // Top memory consumers
                    if (includeTopConsumers && analysis.TopConsumers.Count > 0)
                    {
                        sb.AppendLine($"## Top {topCount} Memory Consumers");
                        int rank = 1;
                        foreach (var consumer in analysis.TopConsumers)
                        {
                            sb.AppendLine($"{rank}. [{consumer.Type}] {consumer.Name}: {FormatBytes(consumer.Size)}");
                            rank++;
                        }
                        sb.AppendLine();
                    }

                    // Potential issues
                    if (analysis.Issues.Count > 0)
                    {
                        sb.AppendLine("## Potential Issues");
                        foreach (var issue in analysis.Issues)
                        {
                            sb.AppendLine($"- [{issue.Severity}] {issue.Message}");
                        }
                        sb.AppendLine();
                    }

                    // Recommendations
                    sb.AppendLine("## Optimization Recommendations");
                    var recommendations = GenerateRecommendations(analysis);
                    if (recommendations.Count == 0)
                    {
                        sb.AppendLine("- Memory usage appears reasonable.");
                    }
                    else
                    {
                        foreach (var rec in recommendations)
                        {
                            sb.AppendLine($"- {rec}");
                        }
                    }

                    // Snapshot info
                    if (takeSnapshot)
                    {
                        sb.AppendLine();
                        sb.AppendLine("## Memory Snapshot");
                        var snapshotResult = TryTakeMemorySnapshot();
                        sb.AppendLine(snapshotResult);
                    }

                    return McpToolResult.Success(sb.ToString());
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Memory analysis failed: {ex.Message}");
                }
            });
        }

        private MemoryAnalysis AnalyzeMemory(bool includeAssetBreakdown, bool includeTopConsumers, int topCount)
        {
            var analysis = new MemoryAnalysis();

            // Basic memory stats from Profiler
            analysis.TotalReservedMemory = Profiler.GetTotalReservedMemoryLong();
            analysis.TotalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            analysis.MonoHeapSize = Profiler.GetMonoHeapSizeLong();
            analysis.MonoUsedSize = Profiler.GetMonoUsedSizeLong();
            analysis.GfxMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();

            if (includeAssetBreakdown || includeTopConsumers)
            {
                AnalyzeLoadedAssets(analysis, includeTopConsumers, topCount);
            }

            // Check for potential issues
            DetectPotentialIssues(analysis);

            return analysis;
        }

        private void AnalyzeLoadedAssets(MemoryAnalysis analysis, bool includeTopConsumers, int topCount)
        {
            var consumers = new List<MemoryConsumer>();

            // Textures
            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            analysis.TextureCount = textures.Length;
            foreach (var tex in textures)
            {
                long size = Profiler.GetRuntimeMemorySizeLong(tex);
                analysis.TextureMemory += size;

                if (includeTopConsumers)
                {
                    consumers.Add(new MemoryConsumer
                    {
                        Name = tex.name,
                        Type = tex.GetType().Name,
                        Size = size
                    });
                }
            }

            // Meshes
            var meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            analysis.MeshCount = meshes.Length;
            foreach (var mesh in meshes)
            {
                long size = Profiler.GetRuntimeMemorySizeLong(mesh);
                analysis.MeshMemory += size;

                if (includeTopConsumers)
                {
                    consumers.Add(new MemoryConsumer
                    {
                        Name = mesh.name,
                        Type = "Mesh",
                        Size = size
                    });
                }
            }

            // Materials
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            analysis.MaterialCount = materials.Length;

            // Shaders
            var shaders = Resources.FindObjectsOfTypeAll<Shader>();
            analysis.ShaderCount = shaders.Length;

            // Audio
            var audioClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            analysis.AudioClipCount = audioClips.Length;
            foreach (var clip in audioClips)
            {
                long size = Profiler.GetRuntimeMemorySizeLong(clip);
                analysis.AudioMemory += size;

                if (includeTopConsumers)
                {
                    consumers.Add(new MemoryConsumer
                    {
                        Name = clip.name,
                        Type = "AudioClip",
                        Size = size
                    });
                }
            }

            // Animation clips
            var animClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
            analysis.AnimationClipCount = animClips.Length;

            // Render Textures
            var renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>();
            analysis.RenderTextureCount = renderTextures.Length;
            foreach (var rt in renderTextures)
            {
                if (rt != null)
                {
                    long size = Profiler.GetRuntimeMemorySizeLong(rt);
                    analysis.RenderTextureMemory += size;

                    if (includeTopConsumers)
                    {
                        consumers.Add(new MemoryConsumer
                        {
                            Name = rt.name,
                            Type = "RenderTexture",
                            Size = size
                        });
                    }
                }
            }

            // Sort and get top consumers
            if (includeTopConsumers)
            {
                analysis.TopConsumers = consumers
                    .Where(c => c.Size > 0 && !string.IsNullOrEmpty(c.Name))
                    .OrderByDescending(c => c.Size)
                    .Take(topCount)
                    .ToList();
            }
        }

        private void DetectPotentialIssues(MemoryAnalysis analysis)
        {
            // Check for high Mono heap fragmentation
            if (analysis.MonoHeapSize > 0)
            {
                double usageRatio = (double)analysis.MonoUsedSize / analysis.MonoHeapSize;
                if (usageRatio < 0.5 && analysis.MonoHeapSize > 100 * 1024 * 1024) // >100MB with <50% usage
                {
                    analysis.Issues.Add(new MemoryIssue
                    {
                        Severity = "WARNING",
                        Message = $"Possible Mono heap fragmentation. Heap: {FormatBytes(analysis.MonoHeapSize)}, Used: {FormatBytes(analysis.MonoUsedSize)} ({usageRatio:P0})"
                    });
                }
            }

            // Check for excessive texture memory
            if (analysis.TextureMemory > 500 * 1024 * 1024) // >500MB
            {
                analysis.Issues.Add(new MemoryIssue
                {
                    Severity = "WARNING",
                    Message = $"High texture memory usage: {FormatBytes(analysis.TextureMemory)}. Consider using texture compression or streaming."
                });
            }

            // Check for many render textures
            if (analysis.RenderTextureCount > 10)
            {
                analysis.Issues.Add(new MemoryIssue
                {
                    Severity = "INFO",
                    Message = $"Many RenderTextures active ({analysis.RenderTextureCount}). Ensure they are properly released."
                });
            }

            // Check for duplicate textures (heuristic: many textures with same memory size)
            // This would require more detailed analysis
        }

        private List<string> GenerateRecommendations(MemoryAnalysis analysis)
        {
            var recommendations = new List<string>();

            // Texture recommendations
            if (analysis.TextureMemory > 200 * 1024 * 1024)
            {
                recommendations.Add("Consider enabling texture compression (e.g., ASTC for mobile, BC7 for desktop).");
                recommendations.Add("Use texture mipmaps to reduce memory for distant objects.");
            }

            // Mesh recommendations
            if (analysis.MeshMemory > 100 * 1024 * 1024)
            {
                recommendations.Add("Consider using mesh compression or LOD groups.");
            }

            // Mono heap recommendations
            if (analysis.MonoUsedSize > 50 * 1024 * 1024)
            {
                recommendations.Add("High managed memory usage. Review object allocations in Update loops.");
                recommendations.Add("Consider using object pooling to reduce GC pressure.");
            }

            // Audio recommendations
            if (analysis.AudioMemory > 50 * 1024 * 1024)
            {
                recommendations.Add("Consider streaming large audio files instead of loading them entirely.");
            }

            // General
            if (analysis.TotalAllocatedMemory > 1024L * 1024 * 1024) // >1GB
            {
                recommendations.Add("Total memory usage is high (>1GB). Profile memory on target platform.");
            }

            return recommendations;
        }

        private string TryTakeMemorySnapshot()
        {
            try
            {
                // Try to use Memory Profiler package via reflection
                var memProfilerType = Type.GetType("Unity.MemoryProfiler.MemoryProfiler, Unity.MemoryProfiler.Editor");

                if (memProfilerType != null)
                {
                    var takeSnapshotMethod = memProfilerType.GetMethod("TakeSnapshot",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (takeSnapshotMethod != null)
                    {
                        takeSnapshotMethod.Invoke(null, null);
                        return "Memory snapshot initiated. Check Window > Analysis > Memory Profiler.";
                    }
                }

                return "Memory Profiler package not found. Install 'com.unity.memoryprofiler' for detailed snapshots.";
            }
            catch (Exception ex)
            {
                return $"Failed to take snapshot: {ex.Message}";
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        private class MemoryAnalysis
        {
            public long TotalReservedMemory { get; set; }
            public long TotalAllocatedMemory { get; set; }
            public long MonoHeapSize { get; set; }
            public long MonoUsedSize { get; set; }
            public long GfxMemory { get; set; }

            public long TextureMemory { get; set; }
            public int TextureCount { get; set; }
            public long MeshMemory { get; set; }
            public int MeshCount { get; set; }
            public int MaterialCount { get; set; }
            public int ShaderCount { get; set; }
            public long AudioMemory { get; set; }
            public int AudioClipCount { get; set; }
            public int AnimationClipCount { get; set; }
            public long RenderTextureMemory { get; set; }
            public int RenderTextureCount { get; set; }

            public List<MemoryConsumer> TopConsumers { get; set; } = new List<MemoryConsumer>();
            public List<MemoryIssue> Issues { get; } = new List<MemoryIssue>();
        }

        private class MemoryConsumer
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public long Size { get; set; }
        }

        private class MemoryIssue
        {
            public string Severity { get; set; }
            public string Message { get; set; }
        }
    }
}
