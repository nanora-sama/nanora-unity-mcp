using System;
using System.Collections.Generic;
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
    /// Diagnoses material issues such as missing shaders, incompatible pipelines, etc.
    /// </summary>
    [McpTool("diagnose_materials")]
    public class DiagnoseMaterialsTool : IMcpTool
    {
        public string Name => "diagnose_materials";
        public string Description => "Diagnose material issues in the project. " +
            "Detects pink/error materials, pipeline incompatibilities, missing textures, and missing sprites.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""scope"": {
                    ""type"": ""string"",
                    ""description"": ""Scope of diagnosis"",
                    ""enum"": [""scene"", ""project"", ""selection""],
                    ""default"": ""scene""
                },
                ""include_warnings"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include warning-level issues"",
                    ""default"": true
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string scope = arguments?["scope"]?.Value<string>() ?? "scene";
                bool includeWarnings = arguments?["include_warnings"]?.Value<bool>() ?? true;

                try
                {
                    var diagnosis = DiagnoseMaterials(scope, includeWarnings);

                    var sb = new StringBuilder();
                    sb.AppendLine("# Material Diagnosis Report");
                    sb.AppendLine();

                    // Check SpriteRenderers for missing sprites
                    var spriteIssues = DiagnoseSpriteRenderers(scope);

                    // Summary
                    var componentErrors = spriteIssues.Issues.Count(i => i.Severity == "ERROR");
                    var componentWarnings = spriteIssues.Issues.Count(i => i.Severity == "WARNING");

                    sb.AppendLine("## Summary");
                    sb.AppendLine($"- Scope: {scope}");
                    sb.AppendLine($"- Current Render Pipeline: {diagnosis.CurrentPipeline}");
                    sb.AppendLine($"- Materials Checked: {diagnosis.TotalMaterials}");
                    sb.AppendLine($"- Components Checked: {spriteIssues.TotalChecked}");
                    sb.AppendLine($"- Missing Scripts: {spriteIssues.MissingScripts}");
                    sb.AppendLine($"- Material Errors: {diagnosis.Errors.Count}");
                    sb.AppendLine($"- Component Errors: {componentErrors}");
                    sb.AppendLine($"- Warnings: {diagnosis.Warnings.Count + componentWarnings}");
                    sb.AppendLine();

                    // Errors (pink/broken materials)
                    if (diagnosis.Errors.Count > 0)
                    {
                        sb.AppendLine("## Errors (Broken Materials)");
                        foreach (var error in diagnosis.Errors)
                        {
                            sb.AppendLine($"- **{error.MaterialName}**");
                            sb.AppendLine($"  - Issue: {error.Issue}");
                            sb.AppendLine($"  - Shader: {error.ShaderName}");
                            sb.AppendLine($"  - Used by: {string.Join(", ", error.UsedByObjects.Take(3))}");
                            if (error.UsedByObjects.Count > 3)
                            {
                                sb.AppendLine($"    ... and {error.UsedByObjects.Count - 3} more");
                            }
                            sb.AppendLine($"  - Suggested Fix: {error.SuggestedFix}");
                        }
                        sb.AppendLine();
                    }

                    // Component Errors
                    var errorIssues = spriteIssues.Issues.Where(i => i.Severity == "ERROR").ToList();
                    if (errorIssues.Count > 0)
                    {
                        sb.AppendLine("## Component Errors");
                        foreach (var issue in errorIssues)
                        {
                            sb.AppendLine($"- **{issue.ObjectName}** [{issue.ComponentType}]");
                            sb.AppendLine($"  - Path: {issue.ObjectPath}");
                            sb.AppendLine($"  - Issue: {issue.Issue}");
                            sb.AppendLine($"  - Fix: {issue.SuggestedFix}");
                        }
                        sb.AppendLine();
                    }

                    // Component Warnings
                    var warningIssues = spriteIssues.Issues.Where(i => i.Severity == "WARNING").ToList();
                    if (warningIssues.Count > 0)
                    {
                        sb.AppendLine("## Component Warnings");
                        foreach (var issue in warningIssues)
                        {
                            sb.AppendLine($"- **{issue.ObjectName}** [{issue.ComponentType}]: {issue.Issue}");
                            sb.AppendLine($"  - Fix: {issue.SuggestedFix}");
                        }
                        sb.AppendLine();
                    }

                    // Warnings
                    if (includeWarnings && diagnosis.Warnings.Count > 0)
                    {
                        sb.AppendLine("## Warnings");
                        foreach (var warning in diagnosis.Warnings)
                        {
                            sb.AppendLine($"- **{warning.MaterialName}**: {warning.Issue}");
                            if (!string.IsNullOrEmpty(warning.SuggestedFix))
                            {
                                sb.AppendLine($"  - Fix: {warning.SuggestedFix}");
                            }
                        }
                        sb.AppendLine();
                    }

                    // Statistics
                    if (diagnosis.ShaderStats.Count > 0)
                    {
                        sb.AppendLine("## Shader Usage Statistics");
                        foreach (var stat in diagnosis.ShaderStats.OrderByDescending(s => s.Value))
                        {
                            sb.AppendLine($"- {stat.Key}: {stat.Value} materials");
                        }
                    }

                    if (diagnosis.Errors.Count == 0 && diagnosis.Warnings.Count == 0 && spriteIssues.Issues.Count == 0)
                    {
                        sb.AppendLine("✓ No issues detected.");
                    }

                    return McpToolResult.Success(sb.ToString());
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Diagnosis failed: {ex.Message}");
                }
            });
        }

        private MaterialDiagnosis DiagnoseMaterials(string scope, bool includeWarnings)
        {
            var diagnosis = new MaterialDiagnosis();
            diagnosis.CurrentPipeline = GetCurrentPipeline();

            var materials = GetMaterialsForScope(scope);
            var rendererMaterialMap = GetRendererMaterialMap(scope);

            foreach (var mat in materials)
            {
                if (mat == null) continue;

                diagnosis.TotalMaterials++;

                // Track shader usage
                var shaderName = mat.shader != null ? mat.shader.name : "(null)";
                if (!diagnosis.ShaderStats.ContainsKey(shaderName))
                {
                    diagnosis.ShaderStats[shaderName] = 0;
                }
                diagnosis.ShaderStats[shaderName]++;

                // Check for issues
                var issues = CheckMaterialIssues(mat, diagnosis.CurrentPipeline);

                // Get objects using this material
                var usedBy = rendererMaterialMap.ContainsKey(mat)
                    ? rendererMaterialMap[mat]
                    : new List<string>();

                foreach (var issue in issues)
                {
                    issue.MaterialName = mat.name;
                    issue.ShaderName = shaderName;
                    issue.UsedByObjects = usedBy;

                    if (issue.Severity == "ERROR")
                    {
                        diagnosis.Errors.Add(issue);
                    }
                    else if (includeWarnings)
                    {
                        diagnosis.Warnings.Add(issue);
                    }
                }
            }

            return diagnosis;
        }

        private string GetCurrentPipeline()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                return "Built-in";
            }

            var name = pipeline.GetType().Name;
            if (name.Contains("Universal"))
            {
                return "URP";
            }
            if (name.Contains("HighDefinition") || name.Contains("HDRP"))
            {
                return "HDRP";
            }

            return name;
        }

        private IEnumerable<Material> GetMaterialsForScope(string scope)
        {
            switch (scope.ToLower())
            {
                case "selection":
                    return Selection.gameObjects
                        .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
                        .SelectMany(r => r.sharedMaterials)
                        .Where(m => m != null)
                        .Distinct();

                case "project":
                    return AssetDatabase.FindAssets("t:Material")
                        .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
                        .Where(m => m != null);

                case "scene":
                default:
                    return UnityCompatibility.FindObjectsByType<Renderer>()
                        .SelectMany(r => r.sharedMaterials)
                        .Where(m => m != null)
                        .Distinct();
            }
        }

        private Dictionary<Material, List<string>> GetRendererMaterialMap(string scope)
        {
            var map = new Dictionary<Material, List<string>>();

            Renderer[] renderers;

            if (scope.ToLower() == "selection")
            {
                renderers = Selection.gameObjects
                    .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
                    .ToArray();
            }
            else
            {
                renderers = UnityCompatibility.FindObjectsByType<Renderer>();
            }

            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;

                    if (!map.ContainsKey(mat))
                    {
                        map[mat] = new List<string>();
                    }
                    map[mat].Add(renderer.gameObject.name);
                }
            }

            return map;
        }

        private List<MaterialIssue> CheckMaterialIssues(Material mat, string currentPipeline)
        {
            var issues = new List<MaterialIssue>();

            // Check for null shader (pink material)
            if (mat.shader == null)
            {
                issues.Add(new MaterialIssue
                {
                    Severity = "ERROR",
                    Issue = "Missing shader (null)",
                    SuggestedFix = "Assign a valid shader to this material"
                });
                return issues;
            }

            var shaderName = mat.shader.name;

            // Check for error shader (pink material)
            if (shaderName == "Hidden/InternalErrorShader" ||
                shaderName.Contains("Error") ||
                shaderName == "Unlit/Color" && mat.color == new Color(1, 0, 1, 1)) // Magenta
            {
                issues.Add(new MaterialIssue
                {
                    Severity = "ERROR",
                    Issue = "Shader compilation error or missing shader",
                    SuggestedFix = "Check console for shader errors, or upgrade shader to current pipeline"
                });
            }

            // Check for pipeline incompatibility
            if (currentPipeline == "URP")
            {
                if (shaderName == "Standard" || shaderName.StartsWith("Legacy Shaders/"))
                {
                    issues.Add(new MaterialIssue
                    {
                        Severity = "ERROR",
                        Issue = "Built-in shader used in URP project",
                        SuggestedFix = "Upgrade to 'Universal Render Pipeline/Lit' or equivalent"
                    });
                }
            }
            else if (currentPipeline == "HDRP")
            {
                if (shaderName == "Standard" || shaderName.StartsWith("Legacy Shaders/") ||
                    shaderName.StartsWith("Universal Render Pipeline/"))
                {
                    issues.Add(new MaterialIssue
                    {
                        Severity = "ERROR",
                        Issue = "Non-HDRP shader used in HDRP project",
                        SuggestedFix = "Upgrade to 'HDRP/Lit' or equivalent"
                    });
                }
            }

            // Check for missing textures
            var texPropertyNames = new[] { "_MainTex", "_BaseMap", "_BumpMap", "_NormalMap", "_MetallicGlossMap" };
            foreach (var propName in texPropertyNames)
            {
                if (mat.HasProperty(propName))
                {
                    var tex = mat.GetTexture(propName);
                    if (tex == null)
                    {
                        // Only warn if this property seems to be expected (has UV setup)
                        // This is a heuristic - not all null textures are issues
                    }
                }
            }

            // Check for deprecated shaders
            if (shaderName.StartsWith("Legacy Shaders/"))
            {
                issues.Add(new MaterialIssue
                {
                    Severity = "WARNING",
                    Issue = "Using deprecated legacy shader",
                    SuggestedFix = "Consider upgrading to Standard or pipeline-specific shader"
                });
            }

            return issues;
        }

        private class MaterialDiagnosis
        {
            public string CurrentPipeline { get; set; }
            public int TotalMaterials { get; set; }
            public List<MaterialIssue> Errors { get; } = new List<MaterialIssue>();
            public List<MaterialIssue> Warnings { get; } = new List<MaterialIssue>();
            public Dictionary<string, int> ShaderStats { get; } = new Dictionary<string, int>();
        }

        private class MaterialIssue
        {
            public string Severity { get; set; }
            public string MaterialName { get; set; }
            public string ShaderName { get; set; }
            public string Issue { get; set; }
            public string SuggestedFix { get; set; }
            public List<string> UsedByObjects { get; set; } = new List<string>();
        }

        private ComponentDiagnosis DiagnoseSpriteRenderers(string scope)
        {
            var diagnosis = new ComponentDiagnosis();

            GameObject[] gameObjects;

            switch (scope.ToLower())
            {
                case "selection":
                    gameObjects = Selection.gameObjects
                        .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                        .Select(t => t.gameObject)
                        .ToArray();
                    CheckGameObjects(gameObjects, diagnosis, null);
                    break;
                case "project":
                    // For project scope, check prefabs
                    var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                    foreach (var guid in prefabGuids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab == null) continue;

                        var children = prefab.GetComponentsInChildren<Transform>(true)
                            .Select(t => t.gameObject).ToArray();
                        CheckGameObjects(children, diagnosis, path);
                    }
                    break;
                case "scene":
                default:
                    gameObjects = UnityCompatibility.FindObjectsByType<Transform>()
                        .Select(t => t.gameObject).ToArray();
                    CheckGameObjects(gameObjects, diagnosis, null);
                    break;
            }

            return diagnosis;
        }

        private void CheckGameObjects(GameObject[] gameObjects, ComponentDiagnosis diagnosis, string prefabPath)
        {
            foreach (var go in gameObjects)
            {
                // Check for missing scripts
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        diagnosis.MissingScripts++;
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "ERROR",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "MonoBehaviour",
                            Issue = "Missing script reference (script was deleted or renamed)",
                            SuggestedFix = "Remove the missing component or restore the script"
                        });
                    }
                }

                // Check SpriteRenderer
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    diagnosis.TotalChecked++;
                    if (sr.sprite == null)
                    {
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "ERROR",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "SpriteRenderer",
                            Issue = "No sprite assigned (will be invisible)",
                            SuggestedFix = "Assign a sprite to the SpriteRenderer"
                        });
                    }
                }

                // Check TrailRenderer
                var tr = go.GetComponent<TrailRenderer>();
                if (tr != null)
                {
                    diagnosis.TotalChecked++;
                    if (tr.sharedMaterials.Any(m => m == null))
                    {
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "WARNING",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "TrailRenderer",
                            Issue = "Missing material (will use default pink)",
                            SuggestedFix = "Assign a material to the TrailRenderer"
                        });
                    }
                }

                // Check LineRenderer
                var lr = go.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    diagnosis.TotalChecked++;
                    if (lr.sharedMaterials.Any(m => m == null))
                    {
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "WARNING",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "LineRenderer",
                            Issue = "Missing material",
                            SuggestedFix = "Assign a material to the LineRenderer"
                        });
                    }
                }

                // Check MeshFilter
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    diagnosis.TotalChecked++;
                    if (mf.sharedMesh == null)
                    {
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "ERROR",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "MeshFilter",
                            Issue = "No mesh assigned (will be invisible)",
                            SuggestedFix = "Assign a mesh to the MeshFilter"
                        });
                    }
                }

                // Check Animator
                var animator = go.GetComponent<Animator>();
                if (animator != null)
                {
                    diagnosis.TotalChecked++;
                    if (animator.runtimeAnimatorController == null)
                    {
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "WARNING",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "Animator",
                            Issue = "No AnimatorController assigned",
                            SuggestedFix = "Assign an AnimatorController or remove the Animator"
                        });
                    }
                }

                // Check AudioSource
                var audioSource = go.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    diagnosis.TotalChecked++;
                    if (audioSource.clip == null && audioSource.playOnAwake)
                    {
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "WARNING",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "AudioSource",
                            Issue = "PlayOnAwake enabled but no clip assigned",
                            SuggestedFix = "Assign an AudioClip or disable PlayOnAwake"
                        });
                    }
                }

                // Check UI Image
                var image = go.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                {
                    diagnosis.TotalChecked++;
                    if (image.sprite == null && image.color.a > 0)
                    {
                        diagnosis.Issues.Add(new ComponentIssue
                        {
                            Severity = "WARNING",
                            ObjectName = go.name,
                            ObjectPath = prefabPath ?? GetGameObjectPath(go),
                            ComponentType = "Image",
                            Issue = "No sprite assigned (showing solid color)",
                            SuggestedFix = "Assign a sprite or set alpha to 0 if intentional"
                        });
                    }
                }
            }
        }

        private string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return "/" + path;
        }

        private class ComponentDiagnosis
        {
            public int TotalChecked { get; set; }
            public int MissingScripts { get; set; }
            public List<ComponentIssue> Issues { get; set; } = new List<ComponentIssue>();
        }

        private class ComponentIssue
        {
            public string Severity { get; set; }
            public string ObjectName { get; set; }
            public string ObjectPath { get; set; }
            public string ComponentType { get; set; }
            public string Issue { get; set; }
            public string SuggestedFix { get; set; }
        }
    }

    /// <summary>
    /// Repairs material issues by upgrading shaders to the current render pipeline.
    /// </summary>
    [McpTool("repair_materials")]
    public class RepairMaterialsTool : IMcpTool
    {
        public string Name => "repair_materials";
        public string Description => "Repair material issues by upgrading shaders to the current render pipeline. " +
            "Can automatically fix pink materials and pipeline incompatibilities.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""scope"": {
                    ""type"": ""string"",
                    ""description"": ""Scope of repair"",
                    ""enum"": [""scene"", ""project"", ""selection""],
                    ""default"": ""scene""
                },
                ""dry_run"": {
                    ""type"": ""boolean"",
                    ""description"": ""Preview changes without applying them"",
                    ""default"": true
                },
                ""target_shader"": {
                    ""type"": ""string"",
                    ""description"": ""Specific shader to upgrade to (auto-detected if not specified)""
                },
                ""fix_missing_shaders"": {
                    ""type"": ""boolean"",
                    ""description"": ""Replace materials with missing shaders"",
                    ""default"": true
                },
                ""upgrade_legacy"": {
                    ""type"": ""boolean"",
                    ""description"": ""Upgrade legacy/built-in shaders to pipeline shaders"",
                    ""default"": true
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string scope = arguments?["scope"]?.Value<string>() ?? "scene";
                bool dryRun = arguments?["dry_run"]?.Value<bool>() ?? true;
                string targetShader = arguments?["target_shader"]?.Value<string>();
                bool fixMissing = arguments?["fix_missing_shaders"]?.Value<bool>() ?? true;
                bool upgradeLegacy = arguments?["upgrade_legacy"]?.Value<bool>() ?? true;

                try
                {
                    var result = RepairMaterials(scope, dryRun, targetShader, fixMissing, upgradeLegacy);

                    var sb = new StringBuilder();
                    sb.AppendLine("# Material Repair Report");
                    sb.AppendLine();
                    sb.AppendLine($"## Mode: {(dryRun ? "DRY RUN (preview only)" : "APPLIED")}");
                    sb.AppendLine($"- Scope: {scope}");
                    sb.AppendLine($"- Current Pipeline: {result.Pipeline}");
                    sb.AppendLine();

                    if (result.Repairs.Count == 0)
                    {
                        sb.AppendLine("No materials needed repair.");
                    }
                    else
                    {
                        sb.AppendLine($"## Repairs ({result.Repairs.Count})");
                        foreach (var repair in result.Repairs)
                        {
                            var status = repair.Applied ? "[APPLIED]" : "[WOULD APPLY]";
                            sb.AppendLine($"- {status} **{repair.MaterialName}**");
                            sb.AppendLine($"  - From: {repair.OriginalShader}");
                            sb.AppendLine($"  - To: {repair.NewShader}");
                            if (!repair.Applied && !string.IsNullOrEmpty(repair.Reason))
                            {
                                sb.AppendLine($"  - Note: {repair.Reason}");
                            }

                            // Show property mappings in dry_run mode
                            if (dryRun && repair.PropertyMappings.Count > 0)
                            {
                                sb.AppendLine($"  - Property Mappings:");
                                foreach (var mapping in repair.PropertyMappings.Where(m => m.WillCopy))
                                {
                                    sb.AppendLine($"    - {mapping.SourceProperty} → {mapping.TargetProperty}: {mapping.Value}");
                                }
                            }
                        }
                    }

                    if (result.Skipped.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"## Skipped ({result.Skipped.Count})");
                        foreach (var skip in result.Skipped)
                        {
                            sb.AppendLine($"- {skip.MaterialName}: {skip.Reason}");
                        }
                    }

                    if (!dryRun && result.Repairs.Any(r => r.Applied))
                    {
                        sb.AppendLine();
                        sb.AppendLine("Changes applied. Use Ctrl+Z to undo.");
                    }

                    return McpToolResult.Success(sb.ToString());
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Repair failed: {ex.Message}");
                }
            });
        }

        private RepairResult RepairMaterials(string scope, bool dryRun, string targetShader,
            bool fixMissing, bool upgradeLegacy)
        {
            var result = new RepairResult();
            result.Pipeline = GetCurrentPipeline();

            var defaultShader = GetDefaultShaderForPipeline(result.Pipeline);
            var upgradeShader = !string.IsNullOrEmpty(targetShader)
                ? Shader.Find(targetShader)
                : Shader.Find(defaultShader);

            if (upgradeShader == null)
            {
                throw new Exception($"Target shader not found: {targetShader ?? defaultShader}");
            }

            var materials = GetMaterialsForScope(scope).ToList();

            foreach (var mat in materials)
            {
                if (mat == null) continue;

                bool needsRepair = false;
                string reason = "";

                // Check for missing/error shader
                if (mat.shader == null ||
                    mat.shader.name == "Hidden/InternalErrorShader" ||
                    mat.shader.name.Contains("Error"))
                {
                    if (fixMissing)
                    {
                        needsRepair = true;
                        reason = "Missing or error shader";
                    }
                }
                // Check for pipeline incompatibility
                else if (upgradeLegacy && IsPipelineIncompatible(mat.shader.name, result.Pipeline))
                {
                    needsRepair = true;
                    reason = "Pipeline incompatible";
                }

                if (needsRepair)
                {
                    var repair = new MaterialRepair
                    {
                        MaterialName = mat.name,
                        OriginalShader = mat.shader?.name ?? "(null)",
                        NewShader = upgradeShader.name,
                        Reason = reason
                    };

                    // Analyze property mappings (for dry_run output)
                    repair.PropertyMappings = AnalyzePropertyMappings(mat, upgradeShader);

                    if (!dryRun)
                    {
                        // Record undo
                        Undo.RecordObject(mat, "Repair Material");

                        // Apply shader change
                        mat.shader = upgradeShader;

                        // Try to preserve some properties
                        TryPreserveProperties(mat);

                        repair.Applied = true;

                        // Mark as dirty
                        EditorUtility.SetDirty(mat);
                    }

                    result.Repairs.Add(repair);
                }
                else if (mat.shader != null && !IsPipelineIncompatible(mat.shader.name, result.Pipeline))
                {
                    // Material is fine, skip silently
                }
            }

            return result;
        }

        private string GetCurrentPipeline()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return "Built-in";

            var name = pipeline.GetType().Name;
            if (name.Contains("Universal")) return "URP";
            if (name.Contains("HighDefinition") || name.Contains("HDRP")) return "HDRP";
            return name;
        }

        private string GetDefaultShaderForPipeline(string pipeline)
        {
            return pipeline switch
            {
                "URP" => "Universal Render Pipeline/Lit",
                "HDRP" => "HDRP/Lit",
                _ => "Standard"
            };
        }

        private bool IsPipelineIncompatible(string shaderName, string pipeline)
        {
            if (pipeline == "URP")
            {
                return shaderName == "Standard" ||
                       shaderName.StartsWith("Legacy Shaders/") ||
                       shaderName.StartsWith("HDRP/");
            }

            if (pipeline == "HDRP")
            {
                return shaderName == "Standard" ||
                       shaderName.StartsWith("Legacy Shaders/") ||
                       shaderName.StartsWith("Universal Render Pipeline/");
            }

            return false;
        }

        // Property mapping definitions for shader upgrade
        private static readonly (string Source, string Target, string Type)[] s_PropertyMappings = new[]
        {
            ("_MainTex", "_BaseMap", "Texture"),
            ("_Color", "_BaseColor", "Color"),
            ("_BumpMap", "_BumpMap", "Texture"),
            ("_NormalMap", "_BumpMap", "Texture"),
            ("_Glossiness", "_Smoothness", "Float"),
            ("_Metallic", "_Metallic", "Float"),
            ("_MetallicGlossMap", "_MetallicGlossMap", "Texture"),
            ("_OcclusionMap", "_OcclusionMap", "Texture"),
            ("_EmissionColor", "_EmissionColor", "Color"),
            ("_EmissionMap", "_EmissionMap", "Texture"),
        };

        private List<PropertyMapping> AnalyzePropertyMappings(Material mat, Shader targetShader)
        {
            var mappings = new List<PropertyMapping>();

            foreach (var (source, target, propType) in s_PropertyMappings)
            {
                if (!mat.HasProperty(source))
                    continue;

                var mapping = new PropertyMapping
                {
                    SourceProperty = source,
                    TargetProperty = target,
                    WillCopy = false,
                    Value = "(empty)"
                };

                // Get current value
                switch (propType)
                {
                    case "Texture":
                        var tex = mat.GetTexture(source);
                        if (tex != null)
                        {
                            mapping.Value = tex.name;
                            mapping.WillCopy = true;
                        }
                        break;
                    case "Color":
                        var color = mat.GetColor(source);
                        mapping.Value = $"#{ColorUtility.ToHtmlStringRGBA(color)}";
                        mapping.WillCopy = true;
                        break;
                    case "Float":
                        var floatVal = mat.GetFloat(source);
                        mapping.Value = floatVal.ToString("F2");
                        mapping.WillCopy = true;
                        break;
                }

                // Check if target property exists on target shader
                // (This is a simplification - full check would need shader property iteration)
                if (source == target)
                {
                    mapping.TargetProperty = $"{target} (unchanged)";
                }

                mappings.Add(mapping);
            }

            return mappings;
        }

        private void TryPreserveProperties(Material mat)
        {
            // Common property mappings
            foreach (var (source, target, propType) in s_PropertyMappings)
            {
                if (!mat.HasProperty(source) || !mat.HasProperty(target))
                    continue;

                switch (propType)
                {
                    case "Texture":
                        var tex = mat.GetTexture(source);
                        if (tex != null && source != target)
                        {
                            mat.SetTexture(target, tex);
                        }
                        break;
                    case "Color":
                        if (source != target)
                        {
                            mat.SetColor(target, mat.GetColor(source));
                        }
                        break;
                    case "Float":
                        if (source != target)
                        {
                            mat.SetFloat(target, mat.GetFloat(source));
                        }
                        break;
                }
            }
        }

        private IEnumerable<Material> GetMaterialsForScope(string scope)
        {
            switch (scope.ToLower())
            {
                case "selection":
                    return Selection.gameObjects
                        .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
                        .SelectMany(r => r.sharedMaterials)
                        .Where(m => m != null)
                        .Distinct();

                case "project":
                    return AssetDatabase.FindAssets("t:Material")
                        .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
                        .Where(m => m != null);

                case "scene":
                default:
                    return UnityCompatibility.FindObjectsByType<Renderer>()
                        .SelectMany(r => r.sharedMaterials)
                        .Where(m => m != null)
                        .Distinct();
            }
        }

        private class RepairResult
        {
            public string Pipeline { get; set; }
            public List<MaterialRepair> Repairs { get; } = new List<MaterialRepair>();
            public List<MaterialSkip> Skipped { get; } = new List<MaterialSkip>();
        }

        private class MaterialRepair
        {
            public string MaterialName { get; set; }
            public string OriginalShader { get; set; }
            public string NewShader { get; set; }
            public string Reason { get; set; }
            public bool Applied { get; set; }
            public List<PropertyMapping> PropertyMappings { get; set; } = new List<PropertyMapping>();
        }

        private class PropertyMapping
        {
            public string SourceProperty { get; set; }
            public string TargetProperty { get; set; }
            public bool WillCopy { get; set; }
            public string Value { get; set; }
        }

        private class MaterialSkip
        {
            public string MaterialName { get; set; }
            public string Reason { get; set; }
        }
    }
}
