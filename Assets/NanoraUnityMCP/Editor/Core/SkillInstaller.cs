using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NanoraUnityMCP.Core
{
    public enum SkillInstallResult
    {
        Installed,
        Updated,
        Unchanged,
        SkippedUserModified,
        Failed
    }

    public struct SkillStatus
    {
        public string Name;
        public string SourcePath;
        public string TargetPath;
        public bool IsInstalled;
        public bool IsUpToDate;
        public bool IsUserModified;
        public string Message;
    }

    public struct SkillInstallReport
    {
        public string Name;
        public SkillInstallResult Result;
        public string Message;
    }

    /// <summary>
    /// Installs bundled Claude Code skills into &lt;Project&gt;/.claude/skills/.
    /// Tracks installed fingerprints to detect user modifications and avoid clobbering.
    /// </summary>
    public static class SkillInstaller
    {
        private const string k_SkillsFolderName = "Skills";
        private const string k_ManifestFileName = ".nanora-unity-skills.json";
        private const string k_SkillEntryFile = "SKILL.md";

        // --- Path Detection ---

        public static string GetBundledSkillsDir()
        {
            // UPM packages can live in Library/PackageCache or outside the project
            // (local/Git dependencies). Resolve the assembly's actual package root.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SkillInstaller).Assembly);
            if (package != null)
            {
                var packagePath = Path.Combine(package.resolvedPath, k_SkillsFolderName);
                if (Directory.Exists(packagePath)) return packagePath;
            }

            // Primary: Assets/NanoraUnityMCP/Skills (dev layout)
            var assetsPath = Path.Combine(Application.dataPath, "NanoraUnityMCP", k_SkillsFolderName);
            if (Directory.Exists(assetsPath)) return assetsPath;

            // Fallback for an embedded package before PackageInfo is available.
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var upmPath = Path.Combine(projectRoot, "Packages", "com.nanora.nanora-unity-mcp", k_SkillsFolderName);
            if (Directory.Exists(upmPath)) return upmPath;

            return assetsPath;
        }

        public static string GetTargetSkillsDir()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, ".claude", "skills");
        }

        public static string GetManifestPath()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, ".claude", k_ManifestFileName);
        }

        // --- Discovery ---

        public static string[] GetBundledSkillNames()
        {
            return GetBundledSkillNamesIn(GetBundledSkillsDir());
        }

        public static string[] GetBundledSkillNamesIn(string sourceDir)
        {
            if (!Directory.Exists(sourceDir)) return Array.Empty<string>();

            return Directory.GetDirectories(sourceDir)
                .Where(d => File.Exists(Path.Combine(d, k_SkillEntryFile)))
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
        }

        public static SkillStatus[] GetAllSkillStatuses()
        {
            var names = GetBundledSkillNames();
            var manifest = LoadManifest();
            var statuses = new SkillStatus[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                statuses[i] = GetSkillStatus(names[i], manifest);
            }
            return statuses;
        }

        public static SkillStatus GetSkillStatus(string skillName, JObject manifest = null)
        {
            manifest ??= LoadManifest();

            var sourcePath = Path.Combine(GetBundledSkillsDir(), skillName, k_SkillEntryFile);
            var targetPath = Path.Combine(GetTargetSkillsDir(), skillName, k_SkillEntryFile);

            var status = new SkillStatus
            {
                Name = skillName,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                IsInstalled = File.Exists(targetPath)
            };

            if (!File.Exists(sourcePath))
            {
                status.Message = "Bundled skill missing";
                return status;
            }

            if (!status.IsInstalled)
            {
                status.Message = "Not installed";
                return status;
            }

            var sourceHash = ComputeFileHash(sourcePath);
            var targetHash = ComputeFileHash(targetPath);
            var recordedHash = manifest?["skills"]?[skillName]?["installedHash"]?.Value<string>();

            status.IsUpToDate = sourceHash == targetHash;
            status.IsUserModified = recordedHash != null && recordedHash != targetHash && targetHash != sourceHash;

            if (status.IsUpToDate)
                status.Message = "Up to date";
            else if (status.IsUserModified)
                status.Message = "User modified (skip without --force)";
            else
                status.Message = "Update available";

            return status;
        }

        // --- Install ---

        public static SkillInstallReport[] InstallAll(bool dryRun = false, bool force = false)
        {
            return InstallAllAt(GetBundledSkillsDir(), GetTargetSkillsDir(), GetManifestPath(), dryRun, force);
        }

        public static SkillInstallReport[] InstallAllAt(string bundledSkillsDir, string targetSkillsDir, string manifestPath, bool dryRun, bool force)
        {
            var names = GetBundledSkillNamesIn(bundledSkillsDir);
            var reports = new SkillInstallReport[names.Length];
            var manifest = LoadManifestFrom(manifestPath);

            for (int i = 0; i < names.Length; i++)
            {
                reports[i] = InstallSkillAt(names[i], bundledSkillsDir, targetSkillsDir, manifest, dryRun, force);
            }

            if (!dryRun)
            {
                SaveManifestTo(manifest, manifestPath);
            }

            return reports;
        }

        public static SkillInstallReport InstallSkill(string skillName, JObject manifest, bool dryRun, bool force)
        {
            return InstallSkillAt(skillName, GetBundledSkillsDir(), GetTargetSkillsDir(), manifest, dryRun, force);
        }

        public static SkillInstallReport InstallSkillAt(string skillName, string bundledSkillsDir, string targetSkillsDir, JObject manifest, bool dryRun, bool force)
        {
            var report = new SkillInstallReport { Name = skillName };

            try
            {
                var sourceDir = Path.Combine(bundledSkillsDir, skillName);
                var sourceFile = Path.Combine(sourceDir, k_SkillEntryFile);

                if (!File.Exists(sourceFile))
                {
                    report.Result = SkillInstallResult.Failed;
                    report.Message = "Bundled skill file missing";
                    return report;
                }

                var targetDir = Path.Combine(targetSkillsDir, skillName);
                var targetFile = Path.Combine(targetDir, k_SkillEntryFile);

                var sourceHash = ComputeFileHash(sourceFile);

                if (File.Exists(targetFile))
                {
                    var targetHash = ComputeFileHash(targetFile);

                    if (targetHash == sourceHash)
                    {
                        UpdateManifest(manifest, skillName, sourceHash);
                        report.Result = SkillInstallResult.Unchanged;
                        report.Message = "Already up to date";
                        return report;
                    }

                    var recordedHash = manifest?["skills"]?[skillName]?["installedHash"]?.Value<string>();

                    // No manifest entry: either this is a pre-existing user file, or a prior install
                    // before manifest tracking existed. Either way, treat as user-owned to be safe.
                    // Manifest entry present: user-modified iff the tracked hash no longer matches.
                    bool userModified = recordedHash == null
                        ? true
                        : recordedHash != targetHash;

                    if (userModified && !force)
                    {
                        report.Result = SkillInstallResult.SkippedUserModified;
                        report.Message = recordedHash == null
                            ? "Pre-existing file, skipped (use force to replace)"
                            : "User-modified, skipped (use force to overwrite)";
                        return report;
                    }

                    if (!dryRun)
                    {
                        if (File.Exists(targetFile))
                        {
                            File.Copy(targetFile, targetFile + ".bak", overwrite: true);
                        }
                        CopySkill(sourceDir, targetDir);
                        UpdateManifest(manifest, skillName, sourceHash);
                    }
                    report.Result = SkillInstallResult.Updated;
                    report.Message = force && userModified ? "Overwrote user-modified (backup: .bak)" : "Updated";
                    return report;
                }

                if (!dryRun)
                {
                    Directory.CreateDirectory(targetDir);
                    CopySkill(sourceDir, targetDir);
                    UpdateManifest(manifest, skillName, sourceHash);
                }
                report.Result = SkillInstallResult.Installed;
                report.Message = "Installed";
                return report;
            }
            catch (Exception ex)
            {
                report.Result = SkillInstallResult.Failed;
                report.Message = ex.Message;
                Debug.LogWarning($"[SkillInstaller] Failed to install {skillName}: {ex.Message}");
                return report;
            }
        }

        private static void CopySkill(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                var relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destPath = Path.Combine(targetDir, relative);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(file, destPath, overwrite: true);
            }
        }

        // --- Manifest ---

        public static JObject LoadManifest()
        {
            return LoadManifestFrom(GetManifestPath());
        }

        public static JObject LoadManifestFrom(string path)
        {
            if (!File.Exists(path))
            {
                return new JObject
                {
                    ["version"] = 1,
                    ["skills"] = new JObject()
                };
            }

            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch
            {
                return new JObject
                {
                    ["version"] = 1,
                    ["skills"] = new JObject()
                };
            }
        }

        public static void SaveManifest(JObject manifest)
        {
            SaveManifestTo(manifest, GetManifestPath());
        }

        public static void SaveManifestTo(JObject manifest, string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, manifest.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SkillInstaller] Failed to save manifest: {ex.Message}");
            }
        }

        private static void UpdateManifest(JObject manifest, string skillName, string hash)
        {
            if (manifest["skills"] == null || manifest["skills"].Type != JTokenType.Object)
            {
                manifest["skills"] = new JObject();
            }
            var skills = (JObject)manifest["skills"];
            skills[skillName] = new JObject
            {
                ["installedHash"] = hash,
                ["installedAt"] = DateTime.UtcNow.ToString("o")
            };
        }

        // --- Hashing ---

        public static string ComputeFileHash(string path)
        {
            if (!File.Exists(path)) return null;
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var bytes = sha.ComputeHash(stream);
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // --- Report ---

        public static string GetInstallSummary(SkillInstallReport[] reports)
        {
            var sb = new StringBuilder();
            int installed = 0, updated = 0, unchanged = 0, skipped = 0, failed = 0;
            foreach (var r in reports)
            {
                switch (r.Result)
                {
                    case SkillInstallResult.Installed: installed++; break;
                    case SkillInstallResult.Updated: updated++; break;
                    case SkillInstallResult.Unchanged: unchanged++; break;
                    case SkillInstallResult.SkippedUserModified: skipped++; break;
                    case SkillInstallResult.Failed: failed++; break;
                }
            }
            sb.Append($"{installed} installed, {updated} updated, {unchanged} unchanged");
            if (skipped > 0) sb.Append($", {skipped} skipped");
            if (failed > 0) sb.Append($", {failed} failed");
            return sb.ToString();
        }
    }
}
