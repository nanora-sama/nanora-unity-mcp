using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NanoraUnityMCP.UI
{
    /// <summary>
    /// Exports a Unity package subtree as a .unitypackage.
    /// </summary>
    public static class PackageExporter
    {
        private const string k_FreeRoot = "Assets/NanoraUnityMCP";
        private const string k_FreeFileStem = "NanoraUnityMCP";
        private const string k_DefaultOutputDir = "Build";

        private static readonly string[] s_FreeExcludePatterns =
        {
            "/Editor/Tests/",
            "/Editor/Tests.meta",
            ".Tests.asmdef",
            ".Tests.asmdef.meta",
            "/.npmignore",
        };

        [MenuItem("Window/Unity MCP Server/Export Package (Silent)")]
        public static void ExportFreeSilent()
        {
            var outputPath = BuildDefaultOutputPath(k_FreeRoot, k_FreeFileStem);
            Export(outputPath, k_FreeRoot, s_FreeExcludePatterns);
        }

        [MenuItem("Window/Unity MCP Server/Export Package")]
        public static void ExportFreeFromMenu()
        {
            var version = GetPackageVersion(k_FreeRoot);
            var defaultName = $"{k_FreeFileStem}-v{version}.unitypackage";
            var outputPath = EditorUtility.SaveFilePanel(
                "Export Nanora Unity MCP Package",
                GetDefaultOutputDir(),
                defaultName,
                "unitypackage");

            if (string.IsNullOrEmpty(outputPath))
                return;

            Export(outputPath, k_FreeRoot, s_FreeExcludePatterns);
        }

        /// <summary>
        /// CLI entry point for batch mode export of the free package.
        /// Usage: Unity -batchmode -executeMethod NanoraUnityMCP.UI.PackageExporter.ExportFromCLI
        ///        -outputPath ./Build/NanoraUnityMCP.unitypackage
        /// </summary>
        public static void ExportFromCLI()
        {
            var outputPath = GetCommandLineArg("-outputPath")
                ?? BuildDefaultOutputPath(k_FreeRoot, k_FreeFileStem);
            Export(outputPath, k_FreeRoot, s_FreeExcludePatterns);
        }

        /// <summary>
        /// Generic package export. Walks <paramref name="packageRoot"/>, skips
        /// folders and any path containing a substring from <paramref name="excludePatterns"/>,
        /// then writes a .unitypackage to <paramref name="outputPath"/>.
        /// </summary>
        public static void Export(string outputPath, string packageRoot, string[] excludePatterns)
        {
            if (string.IsNullOrEmpty(packageRoot))
                throw new ArgumentException("packageRoot is required", nameof(packageRoot));

            excludePatterns ??= Array.Empty<string>();
            var assetPaths = CollectAssetPaths(packageRoot, excludePatterns);

            if (assetPaths.Length == 0)
            {
                Debug.LogError($"[PackageExporter] No assets found to export under {packageRoot}.");
                return;
            }

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            AssetDatabase.ExportPackage(
                assetPaths,
                outputPath,
                ExportPackageOptions.Default);

            Debug.Log($"[PackageExporter] Exported {assetPaths.Length} assets from {packageRoot} to: {outputPath}");
        }

        public static string BuildDefaultOutputPath(string packageRoot, string fileStem)
        {
            var version = GetPackageVersion(packageRoot);
            return Path.Combine(GetDefaultOutputDir(), $"{fileStem}-v{version}.unitypackage");
        }

        public static string GetPackageVersion(string packageRoot)
        {
            var packageJsonPath = Path.Combine(packageRoot, "package.json");
            if (!File.Exists(packageJsonPath))
                return "0.0.0";

            var json = File.ReadAllText(packageJsonPath);
            var match = System.Text.RegularExpressions.Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : "0.0.0";
        }

        public static string GetDefaultOutputDir()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var buildDir = Path.Combine(projectRoot, k_DefaultOutputDir);
            if (!Directory.Exists(buildDir))
                Directory.CreateDirectory(buildDir);
            return buildDir;
        }

        public static string GetCommandLineArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return null;
        }

        private static string[] CollectAssetPaths(string packageRoot, string[] excludePatterns)
        {
            var guids = AssetDatabase.FindAssets("", new[] { packageRoot });
            var paths = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (AssetDatabase.IsValidFolder(path))
                    continue;

                if (ShouldExclude(path, excludePatterns))
                    continue;

                paths.Add(path);
            }

            return paths.ToArray();
        }

        private static bool ShouldExclude(string assetPath, string[] excludePatterns)
        {
            foreach (var pattern in excludePatterns)
            {
                if (assetPath.Contains(pattern))
                    return true;
            }
            return false;
        }
    }
}
