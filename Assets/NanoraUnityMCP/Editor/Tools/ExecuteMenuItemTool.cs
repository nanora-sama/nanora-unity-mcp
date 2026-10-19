using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    [McpTool("execute_menu_item")]
    public class ExecuteMenuItemTool : IMcpTool
    {
        // Menu items that trigger domain reload - execute and return immediately
        private static readonly string[] k_DomainReloadMenus = new[]
        {
            "Assets/Refresh",
            "Assets/Reimport All",
            "File/Build Settings",
            "Edit/Project Settings"
        };

        public string Name => "execute_menu_item";
        public string Description => "Executes a Unity Editor menu item by its path (e.g., 'File/Save', 'Edit/Undo'), including project-defined menus. " +
            "Menu items that can reload the script domain (such as Assets/Refresh) are queued and the response returns before they run, " +
            "so success means the item was dispatched, not that it finished; the connection may drop while the reload runs.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""menu_path"": {
                    ""type"": ""string"",
                    ""description"": ""Full path to the menu item (e.g., 'File/Save Scene', 'GameObject/Create Empty')""
                }
            },
            ""required"": [""menu_path""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            var menuPath = arguments?["menu_path"]?.Value<string>();

            if (string.IsNullOrEmpty(menuPath))
            {
                return Task.FromResult(McpToolResult.Error("menu_path is required"));
            }

            // Check if this menu item triggers domain reload
            bool triggersDomainReload = IsDomainReloadMenu(menuPath);

            if (triggersDomainReload)
            {
                // Execute on main thread but don't wait for completion
                // Return immediately with a warning about domain reload
                MainThreadDispatcher.Enqueue(() =>
                {
                    EditorApplication.ExecuteMenuItem(menuPath);
                });

                return Task.FromResult(McpToolResult.Success(
                    $"Executed menu item: {menuPath}\n\n" +
                    "⚠ Note: This operation triggers domain reload. " +
                    "Server will temporarily stop for 3-10 seconds. " +
                    "Wait before sending next request."));
            }

            // Normal execution with timeout
            return MainThreadDispatcher.RunAsync(() =>
            {
                var result = EditorApplication.ExecuteMenuItem(menuPath);

                if (result)
                {
                    return McpToolResult.Success($"Executed menu item: {menuPath}");
                }
                else
                {
                    return McpToolResult.Error($"Failed to execute menu item: {menuPath}. Make sure the path is correct and the menu item is enabled.");
                }
            });
        }

        private static bool IsDomainReloadMenu(string menuPath)
        {
            foreach (var menu in k_DomainReloadMenus)
            {
                if (menuPath.Equals(menu, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
