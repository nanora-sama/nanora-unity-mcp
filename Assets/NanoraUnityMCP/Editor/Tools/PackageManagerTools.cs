using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Tool for managing Unity packages: list, search, and get info.
    /// </summary>
    [McpTool("manage_packages")]
    public class ManagePackagesTool : IMcpTool
    {
        public string Name => "manage_packages";
        public string Description => "Manage Unity packages: list installed, search Unity Registry, check Unity account login status, open My Assets. " +
            "Use 'check_login' first when working with Asset Store assets - if not logged in, user must sign in via Unity Hub.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""list"", ""search"", ""search_all"", ""info"", ""check_login"", ""open_my_assets"", ""open_package_manager""],
                    ""description"": ""Action to perform: list (installed packages), search (by query), search_all, info (package details), check_login (Unity account status), open_my_assets (open Package Manager to My Assets tab), open_package_manager (open Package Manager window)""
                },
                ""package_name"": {
                    ""type"": ""string"",
                    ""description"": ""Package name or search query""
                },
                ""include_indirect"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include indirect dependencies in list"",
                    ""default"": false
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() => Execute(arguments));
        }

        private async Task<McpToolResult> Execute(JObject arguments)
        {
            string action = arguments?["action"]?.Value<string>();
            string packageName = arguments?["package_name"]?.Value<string>();
            bool includeIndirect = arguments?["include_indirect"]?.Value<bool>() ?? false;

            if (string.IsNullOrWhiteSpace(action))
            {
                return McpToolResult.Error("action is required");
            }

            try
            {
                return action.ToLowerInvariant() switch
                {
                    "list" => await ListPackages(includeIndirect),
                    "search" => await SearchPackages(packageName),
                    "search_all" => await SearchAllPackages(),
                    "info" => await GetPackageInfo(packageName),
                    "check_login" => CheckUnityLogin(),
                    "open_my_assets" => OpenMyAssets(),
                    "open_package_manager" => OpenPackageManager(packageName),
                    _ => McpToolResult.Error($"Unknown action: {action}")
                };
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Package operation failed: {ex.Message}");
            }
        }

        private async Task<McpToolResult> ListPackages(bool includeIndirect)
        {
            var request = Client.List(includeIndirect);
            bool completed = await WaitForRequestAsync(request, 10000);

            if (!completed)
            {
                return McpToolResult.Error("Timeout: Package list request took too long. Please try again.");
            }

            if (request.Status == StatusCode.Failure)
            {
                return McpToolResult.Error($"Failed to list packages: {request.Error?.message}");
            }

            var packages = request.Result.ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"# Installed Packages ({packages.Count})");
            sb.AppendLine();

            // Group by source
            var grouped = packages.GroupBy(p => p.source).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                string sourceName = group.Key switch
                {
                    PackageSource.Registry => "Unity Registry",
                    PackageSource.BuiltIn => "Built-in",
                    PackageSource.Embedded => "Embedded",
                    PackageSource.Local => "Local",
                    PackageSource.LocalTarball => "Local Tarball",
                    PackageSource.Git => "Git",
                    _ => group.Key.ToString()
                };

                sb.AppendLine($"## {sourceName}");
                foreach (var pkg in group.OrderBy(p => p.name))
                {
                    string displayName = !string.IsNullOrEmpty(pkg.displayName) ? pkg.displayName : pkg.name;
                    sb.AppendLine($"- {pkg.name} ({pkg.version}) - {displayName}");
                }
                sb.AppendLine();
            }

            return McpToolResult.Success(sb.ToString());
        }

        private async Task<McpToolResult> SearchPackages(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return McpToolResult.Error("package_name is required for search action");
            }

            var request = Client.Search(query);
            bool completed = await WaitForRequestAsync(request, 15000);

            if (!completed)
            {
                return McpToolResult.Error("Timeout: Package search took too long (>15s). Check your network connection and try again.");
            }

            if (request.Status == StatusCode.Failure)
            {
                return McpToolResult.Error($"Search failed: {request.Error?.message}");
            }

            var packages = request.Result;
            var sb = new StringBuilder();
            sb.AppendLine($"# Search Results for \"{query}\"");
            sb.AppendLine();

            if (packages == null || packages.Length == 0)
            {
                sb.AppendLine("No packages found.");
                return McpToolResult.Success(sb.ToString());
            }

            foreach (var pkg in packages.Take(20))
            {
                sb.AppendLine($"**{pkg.name}** ({pkg.version})");
                string displayName = !string.IsNullOrEmpty(pkg.displayName) ? pkg.displayName : pkg.name;
                sb.AppendLine($"  {displayName}");
                if (!string.IsNullOrEmpty(pkg.description))
                {
                    string desc = pkg.description.Length > 100
                        ? pkg.description.Substring(0, 100) + "..."
                        : pkg.description;
                    sb.AppendLine($"  {desc}");
                }
                sb.AppendLine();
            }

            if (packages.Length > 20)
            {
                sb.AppendLine($"... and {packages.Length - 20} more results");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private async Task<McpToolResult> SearchAllPackages()
        {
            var request = Client.SearchAll();
            bool completed = await WaitForRequestAsync(request, 30000);

            if (!completed)
            {
                return McpToolResult.Error("Timeout: Fetching all packages took too long (>30s). Check your network connection and try again.");
            }

            if (request.Status == StatusCode.Failure)
            {
                return McpToolResult.Error($"Search failed: {request.Error?.message}");
            }

            var packages = request.Result;
            var sb = new StringBuilder();
            sb.AppendLine($"# All Available Packages ({packages?.Length ?? 0})");
            sb.AppendLine();

            if (packages == null || packages.Length == 0)
            {
                sb.AppendLine("No packages found.");
                return McpToolResult.Success(sb.ToString());
            }

            // Group by category/keyword if available
            foreach (var pkg in packages.OrderBy(p => p.name).Take(50))
            {
                string displayName = !string.IsNullOrEmpty(pkg.displayName) ? pkg.displayName : pkg.name;
                sb.AppendLine($"- {pkg.name} ({pkg.version}) - {displayName}");
            }

            if (packages.Length > 50)
            {
                sb.AppendLine();
                sb.AppendLine($"... and {packages.Length - 50} more packages");
                sb.AppendLine("Use 'search' action with a specific query to filter.");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private async Task<McpToolResult> GetPackageInfo(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                return McpToolResult.Error("package_name is required for info action");
            }

            // First try to find in installed packages
            var listRequest = Client.List(true);
            bool listCompleted = await WaitForRequestAsync(listRequest, 10000);

            PackageInfo pkg = null;
            if (listCompleted && listRequest.Status == StatusCode.Success)
            {
                pkg = listRequest.Result.FirstOrDefault(p =>
                    p.name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
            }

            // If not found locally, search in registry
            if (pkg == null)
            {
                var searchRequest = Client.Search(packageName);
                bool searchCompleted = await WaitForRequestAsync(searchRequest, 15000);

                if (!searchCompleted)
                {
                    return McpToolResult.Error("Timeout: Package search took too long. Check your network connection and try again.");
                }

                if (searchRequest.Status == StatusCode.Success && searchRequest.Result?.Length > 0)
                {
                    pkg = searchRequest.Result.FirstOrDefault(p =>
                        p.name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (pkg == null)
            {
                return McpToolResult.Error($"Package not found: {packageName}");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# {pkg.displayName ?? pkg.name}");
            sb.AppendLine();
            sb.AppendLine($"**Package ID:** {pkg.name}");
            sb.AppendLine($"**Version:** {pkg.version}");
            sb.AppendLine($"**Source:** {pkg.source}");

            if (!string.IsNullOrEmpty(pkg.description))
            {
                sb.AppendLine();
                sb.AppendLine("## Description");
                sb.AppendLine(pkg.description);
            }

            if (pkg.author != null && !string.IsNullOrEmpty(pkg.author.name))
            {
                sb.AppendLine();
                sb.AppendLine($"**Author:** {pkg.author.name}");
                if (!string.IsNullOrEmpty(pkg.author.url))
                {
                    sb.AppendLine($"**URL:** {pkg.author.url}");
                }
            }

            if (pkg.dependencies != null && pkg.dependencies.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Dependencies");
                foreach (var dep in pkg.dependencies)
                {
                    sb.AppendLine($"- {dep.name} ({dep.version})");
                }
            }

            if (!string.IsNullOrEmpty(pkg.documentationUrl))
            {
                sb.AppendLine();
                sb.AppendLine($"**Documentation:** {pkg.documentationUrl}");
            }

            if (!string.IsNullOrEmpty(pkg.changelogUrl))
            {
                sb.AppendLine($"**Changelog:** {pkg.changelogUrl}");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private async Task<bool> WaitForRequestAsync<T>(Request<T> request, int timeoutMs)
        {
            int elapsed = 0;
            while (!request.IsCompleted && elapsed < timeoutMs)
            {
                await Task.Delay(100);
                elapsed += 100;
            }
            return request.IsCompleted;
        }

        private McpToolResult CheckUnityLogin()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Unity Account Status");
            sb.AppendLine();

            bool isLoggedIn = !string.IsNullOrEmpty(CloudProjectSettings.accessToken);
            string userId = CloudProjectSettings.userId;
            string userName = CloudProjectSettings.userName;
            string organizationName = CloudProjectSettings.organizationName;

            if (isLoggedIn)
            {
                sb.AppendLine("✓ **Logged In**");
                sb.AppendLine();
                if (!string.IsNullOrEmpty(userName))
                    sb.AppendLine($"- User: {userName}");
                if (!string.IsNullOrEmpty(userId))
                    sb.AppendLine($"- User ID: {userId}");
                if (!string.IsNullOrEmpty(organizationName))
                    sb.AppendLine($"- Organization: {organizationName}");
                sb.AppendLine();
                sb.AppendLine("My Assets is available in Package Manager.");
            }
            else
            {
                sb.AppendLine("❌ **Not Logged In**");
                sb.AppendLine();
                sb.AppendLine("My Assets (Asset Store purchases) is NOT available.");
                sb.AppendLine();
                sb.AppendLine("## How to Login:");
                sb.AppendLine("1. Open Unity Hub");
                sb.AppendLine("2. Click on your avatar (top-right corner)");
                sb.AppendLine("3. Sign in with your Unity account");
                sb.AppendLine("4. Restart Unity Editor if needed");
                sb.AppendLine();
                sb.AppendLine("Or use: Edit > Preferences > Unity Hub");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult OpenMyAssets()
        {
            // Check login first
            bool isLoggedIn = !string.IsNullOrEmpty(CloudProjectSettings.accessToken);

            if (!isLoggedIn)
            {
                var sb = new StringBuilder();
                sb.AppendLine("❌ **Cannot Open My Assets - Not Logged In**");
                sb.AppendLine();
                sb.AppendLine("You must be signed in to your Unity account to access My Assets.");
                sb.AppendLine();
                sb.AppendLine("## How to Login:");
                sb.AppendLine("1. Open Unity Hub");
                sb.AppendLine("2. Click on your avatar (top-right corner)");
                sb.AppendLine("3. Sign in with your Unity account");
                sb.AppendLine("4. Restart Unity Editor");
                return McpToolResult.Success(sb.ToString());
            }

            // Open Package Manager window - it will show My Assets tab if logged in
            Window.Open("");
            EditorApplication.ExecuteMenuItem("Window/Package Manager");

            return McpToolResult.Success(
                "✓ Opened Package Manager window.\n\n" +
                "Navigate to **My Assets** tab to see your Asset Store purchases.\n\n" +
                "Note: To import an asset, select it in My Assets and click **Download** then **Import**.\n" +
                "(Programmatic import is not supported - must use Package Manager UI)");
        }

        private McpToolResult OpenPackageManager(string packageName)
        {
            if (!string.IsNullOrEmpty(packageName))
            {
                // Open to specific package
                Window.Open(packageName);
            }
            else
            {
                // Just open Package Manager
                Window.Open("");
            }

            var sb = new StringBuilder();
            sb.AppendLine("✓ Opened Package Manager window.");

            // Check and report login status
            bool isLoggedIn = !string.IsNullOrEmpty(CloudProjectSettings.accessToken);
            sb.AppendLine();
            if (isLoggedIn)
            {
                sb.AppendLine("Unity Account: ✓ Logged in (My Assets available)");
            }
            else
            {
                sb.AppendLine("Unity Account: ❌ Not logged in (My Assets unavailable)");
                sb.AppendLine("→ Sign in via Unity Hub to access Asset Store purchases.");
            }

            return McpToolResult.Success(sb.ToString());
        }
    }

    /// <summary>
    /// Tool for installing Unity packages.
    /// </summary>
    [McpTool("install_package")]
    public class InstallPackageTool : IMcpTool
    {
        public string Name => "install_package";
        public string Description => "Install a Unity package from registry, Git, local path, or .unitypackage file";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""package_id"": {
                    ""type"": ""string"",
                    ""description"": ""Package identifier (e.g., com.unity.inputsystem)""
                },
                ""version"": {
                    ""type"": ""string"",
                    ""description"": ""Specific version (optional, latest if not specified)""
                },
                ""git_url"": {
                    ""type"": ""string"",
                    ""description"": ""Git repository URL for Git packages""
                },
                ""local_path"": {
                    ""type"": ""string"",
                    ""description"": ""Local path for local packages (file:../path)""
                },
                ""unitypackage_path"": {
                    ""type"": ""string"",
                    ""description"": ""Path to .unitypackage file to import""
                },
                ""interactive"": {
                    ""type"": ""boolean"",
                    ""description"": ""Show import dialog for .unitypackage (default: false, imports all)"",
                    ""default"": false
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() => Execute(arguments));
        }

        private async Task<McpToolResult> Execute(JObject arguments)
        {
            string packageId = arguments?["package_id"]?.Value<string>();
            string version = arguments?["version"]?.Value<string>();
            string gitUrl = arguments?["git_url"]?.Value<string>();
            string localPath = arguments?["local_path"]?.Value<string>();
            string unitypackagePath = arguments?["unitypackage_path"]?.Value<string>();
            bool interactive = arguments?["interactive"]?.Value<bool>() ?? false;

            try
            {
                // Handle .unitypackage import
                if (!string.IsNullOrWhiteSpace(unitypackagePath))
                {
                    return ImportUnityPackage(unitypackagePath, interactive);
                }

                string identifier;

                if (!string.IsNullOrWhiteSpace(gitUrl))
                {
                    identifier = gitUrl;
                }
                else if (!string.IsNullOrWhiteSpace(localPath))
                {
                    identifier = localPath.StartsWith("file:") ? localPath : $"file:{localPath}";
                }
                else if (!string.IsNullOrWhiteSpace(packageId))
                {
                    identifier = !string.IsNullOrWhiteSpace(version)
                        ? $"{packageId}@{version}"
                        : packageId;
                }
                else
                {
                    return McpToolResult.Error("Either package_id, git_url, local_path, or unitypackage_path is required");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Installing {identifier}...");

                var request = Client.Add(identifier);
                bool completed = await WaitForRequestAsync(request, 60000);

                if (!completed)
                {
                    return McpToolResult.Error("Timeout: Package installation took too long (>60s). The operation may still be in progress. Check Unity Editor.");
                }

                if (request.Status == StatusCode.Failure)
                {
                    return McpToolResult.Error($"Installation failed: {request.Error?.message}");
                }

                var pkg = request.Result;
                sb.AppendLine();
                sb.AppendLine("✓ Package installed successfully");
                sb.AppendLine();
                sb.AppendLine($"**Package:** {pkg.displayName ?? pkg.name}");
                sb.AppendLine($"**Version:** {pkg.version}");
                sb.AppendLine($"**Source:** {pkg.source}");

                if (pkg.dependencies != null && pkg.dependencies.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Dependencies resolved:**");
                    foreach (var dep in pkg.dependencies)
                    {
                        sb.AppendLine($"- {dep.name} ({dep.version})");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("Note: Domain reload will occur after this operation.");

                return McpToolResult.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Installation failed: {ex.Message}");
            }
        }

        private async Task<bool> WaitForRequestAsync<T>(Request<T> request, int timeoutMs)
        {
            int elapsed = 0;
            while (!request.IsCompleted && elapsed < timeoutMs)
            {
                await Task.Delay(100);
                elapsed += 100;
            }
            return request.IsCompleted;
        }

        private McpToolResult ImportUnityPackage(string packagePath, bool interactive)
        {
            // Validate path
            if (!System.IO.File.Exists(packagePath))
            {
                return McpToolResult.Error($"File not found: {packagePath}");
            }

            if (!packagePath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                return McpToolResult.Error("File must be a .unitypackage file");
            }

            var sb = new StringBuilder();
            string fileName = System.IO.Path.GetFileName(packagePath);
            sb.AppendLine($"Importing {fileName}...");

            try
            {
                AssetDatabase.ImportPackage(packagePath, interactive);

                sb.AppendLine();
                if (interactive)
                {
                    sb.AppendLine("✓ Import dialog opened.");
                    sb.AppendLine();
                    sb.AppendLine("Select the assets you want to import in the Unity Editor.");
                }
                else
                {
                    sb.AppendLine("✓ Package import started (all assets).");
                    sb.AppendLine();
                    sb.AppendLine("Note: Import may take a moment depending on package size.");
                    sb.AppendLine("Domain reload will occur after import completes.");
                }

                return McpToolResult.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Import failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tool for uninstalling Unity packages.
    /// </summary>
    [McpTool("uninstall_package")]
    public class UninstallPackageTool : IMcpTool
    {
        public string Name => "uninstall_package";
        public string Description => "Uninstall a Unity package from the project";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""package_id"": {
                    ""type"": ""string"",
                    ""description"": ""Package identifier to remove (e.g., com.unity.inputsystem)""
                }
            },
            ""required"": [""package_id""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() => Execute(arguments));
        }

        private async Task<McpToolResult> Execute(JObject arguments)
        {
            string packageId = arguments?["package_id"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(packageId))
            {
                return McpToolResult.Error("package_id is required");
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Removing {packageId}...");

                var request = Client.Remove(packageId);
                bool completed = await WaitForRequestAsync(request, 30000);

                if (!completed)
                {
                    return McpToolResult.Error("Timeout: Package removal took too long (>30s). The operation may still be in progress. Check Unity Editor.");
                }

                if (request.Status == StatusCode.Failure)
                {
                    return McpToolResult.Error($"Removal failed: {request.Error?.message}");
                }

                sb.AppendLine();
                sb.AppendLine("✓ Package removed successfully");
                sb.AppendLine();
                sb.AppendLine("Note: Domain reload will occur after this operation.");

                return McpToolResult.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return McpToolResult.Error($"Removal failed: {ex.Message}");
            }
        }

        private async Task<bool> WaitForRequestAsync(RemoveRequest request, int timeoutMs)
        {
            int elapsed = 0;
            while (!request.IsCompleted && elapsed < timeoutMs)
            {
                await Task.Delay(100);
                elapsed += 100;
            }
            return request.IsCompleted;
        }
    }
}
