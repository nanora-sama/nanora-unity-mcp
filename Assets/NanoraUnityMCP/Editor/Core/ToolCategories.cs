using System.Collections.Generic;
using System.Linq;

namespace NanoraUnityMCP.Core
{
    /// <summary>
    /// Information about a tool category.
    /// </summary>
    public class ToolCategoryInfo
    {
        public string DisplayName { get; }
        public string Description { get; }
        public string[] ToolNames { get; }

        public ToolCategoryInfo(string displayName, string description, params string[] toolNames)
        {
            DisplayName = displayName;
            Description = description;
            ToolNames = toolNames;
        }
    }

    /// <summary>
    /// Tool brief descriptions for SuperSave mode.
    /// </summary>
    public class ToolBriefInfo
    {
        public string Name { get; }
        public string BriefDescription { get; }
        public string Category { get; }

        public ToolBriefInfo(string name, string briefDescription, string category)
        {
            Name = name;
            BriefDescription = briefDescription;
            Category = category;
        }
    }

    /// <summary>
    /// Registry of tool categories for Token SuperSave mode.
    /// Reduces token consumption by 96%+ when used with meta-tools.
    /// </summary>
    public static class ToolCategories
    {
        /// <summary>
        /// All tool categories with their tools.
        /// </summary>
        public static readonly Dictionary<string, ToolCategoryInfo> Categories = new Dictionary<string, ToolCategoryInfo>
        {
            ["scene"] = new ToolCategoryInfo(
                "Scene",
                "GameObject creation and modification",
                "get_scene_hierarchy", "get_object_details", "create_game_object",
                "modify_game_object", "delete_game_object"),

            ["query"] = new ToolCategoryInfo(
                "Query",
                "Search and find objects/assets",
                "query_objects", "search_assets", "wait_for_selector", "instantiate_asset"),

            ["batch"] = new ToolCategoryInfo(
                "Batch",
                "Bulk operations on multiple objects",
                "batch_modify", "set_active", "destroy_objects"),

            ["code"] = new ToolCategoryInfo(
                "Code",
                "Code execution and compilation",
                "execute_csharp", "execute_menu_item", "compile_project", "get_compilation_errors"),

            ["playmode"] = new ToolCategoryInfo(
                "PlayMode",
                "Play mode control",
                "manage_playmode"),

            ["test"] = new ToolCategoryInfo(
                "Test",
                "Test execution, E2E scenarios, visual regression and budgets",
                "run_tests", "get_test_results", "simulate_input", "assert_ui_state",
                "run_e2e_test", "visual_regression_test", "performance_budget_test", "generate_test_report"),

            ["performance"] = new ToolCategoryInfo(
                "Performance",
                "Profiling and performance analysis",
                "analyze_frame", "analyze_memory", "profile_cpu", "get_profiler_stats"),

            ["materials"] = new ToolCategoryInfo(
                "Materials",
                "Material diagnostics and repair",
                "diagnose_materials", "repair_materials"),

            ["diagnostics"] = new ToolCategoryInfo(
                "Diagnostics",
                "Editor diagnostics and logging",
                "mcp_status", "get_console_logs", "configure_editor", "capture_screenshot",
                "setup_status", "reconfigure_client", "install_skills"),

            ["scene_mgmt"] = new ToolCategoryInfo(
                "SceneMgmt",
                "Scene file management",
                "manage_scene"),

            // Phase 2 categories
            ["animation"] = new ToolCategoryInfo(
                "Animation",
                "Animator and animation clip control",
                "control_animator", "manage_animation_clip"),

            ["ui"] = new ToolCategoryInfo(
                "UI",
                "UI element creation and canvas management",
                "create_ui_element", "manage_canvas"),

            ["audio"] = new ToolCategoryInfo(
                "Audio",
                "Audio source and playback control",
                "manage_audio_source", "play_audio"),

            ["lighting"] = new ToolCategoryInfo(
                "Lighting",
                "Light management and global lighting settings",
                "manage_light", "configure_lighting"),

            ["camera"] = new ToolCategoryInfo(
                "Camera",
                "Camera control and Cinemachine setup",
                "control_camera", "setup_cinemachine"),

            // Editor Operations & Reflection
            ["editor"] = new ToolCategoryInfo(
                "Editor",
                "Editor operations, reflection, and batch execution",
                "editor_operations", "unity_reflect", "batch_execute"),

            // Assets & Prefabs
            ["assets"] = new ToolCategoryInfo(
                "Assets",
                "Asset file operations and prefab management",
                "manage_assets", "manage_prefabs"),

            // Package Manager
            ["packages"] = new ToolCategoryInfo(
                "Packages",
                "Unity Package Manager: search/install packages from Unity Registry (official assets, Cinemachine, Input System, etc.)",
                "manage_packages", "install_package", "uninstall_package")
        };

        /// <summary>
        /// Brief descriptions for all tools.
        /// </summary>
        public static readonly Dictionary<string, ToolBriefInfo> ToolBriefs = new Dictionary<string, ToolBriefInfo>
        {
            // Scene
            ["get_scene_hierarchy"] = new ToolBriefInfo("get_scene_hierarchy", "Get scene hierarchy tree", "scene"),
            ["get_object_details"] = new ToolBriefInfo("get_object_details", "Get GameObject details by name", "scene"),
            ["create_game_object"] = new ToolBriefInfo("create_game_object", "Create new GameObject", "scene"),
            ["modify_game_object"] = new ToolBriefInfo("modify_game_object", "Modify GameObject properties", "scene"),
            ["delete_game_object"] = new ToolBriefInfo("delete_game_object", "Delete GameObject", "scene"),

            // Query
            ["query_objects"] = new ToolBriefInfo("query_objects", "Find objects by U-Selector", "query"),
            ["search_assets"] = new ToolBriefInfo("search_assets", "Search assets in project", "query"),
            ["wait_for_selector"] = new ToolBriefInfo("wait_for_selector", "Wait for object existence", "query"),
            ["instantiate_asset"] = new ToolBriefInfo("instantiate_asset", "Instantiate asset in scene", "query"),

            // Batch
            ["batch_modify"] = new ToolBriefInfo("batch_modify", "Batch modify objects by selector", "batch"),
            ["set_active"] = new ToolBriefInfo("set_active", "Set active state of objects", "batch"),
            ["destroy_objects"] = new ToolBriefInfo("destroy_objects", "Destroy objects by selector", "batch"),

            // Code
            ["execute_csharp"] = new ToolBriefInfo("execute_csharp", "Execute C# code snippet", "code"),
            ["execute_menu_item"] = new ToolBriefInfo("execute_menu_item", "Execute Unity menu item", "code"),
            ["compile_project"] = new ToolBriefInfo("compile_project", "Compile Unity project", "code"),
            ["get_compilation_errors"] = new ToolBriefInfo("get_compilation_errors", "Get compilation errors", "code"),

            // PlayMode
            ["manage_playmode"] = new ToolBriefInfo("manage_playmode", "Control play mode state", "playmode"),

            // Test
            ["run_tests"] = new ToolBriefInfo("run_tests", "Run unit tests", "test"),
            ["get_test_results"] = new ToolBriefInfo("get_test_results", "Get test results", "test"),
            ["simulate_input"] = new ToolBriefInfo("simulate_input", "Simulate user input", "test"),
            ["assert_ui_state"] = new ToolBriefInfo("assert_ui_state", "Wait until a UI element property matches an expected value", "test"),
            ["run_e2e_test"] = new ToolBriefInfo("run_e2e_test", "Run a Play Mode E2E scenario of steps and assertions", "test"),
            ["visual_regression_test"] = new ToolBriefInfo("visual_regression_test", "Compare a screenshot against a stored baseline", "test"),
            ["performance_budget_test"] = new ToolBriefInfo("performance_budget_test", "Check FPS, frame time and draw calls against a budget", "test"),
            ["generate_test_report"] = new ToolBriefInfo("generate_test_report", "Write a Markdown or HTML report of test results", "test"),

            // Performance
            ["analyze_frame"] = new ToolBriefInfo("analyze_frame", "Analyze frame performance", "performance"),
            ["analyze_memory"] = new ToolBriefInfo("analyze_memory", "Analyze memory usage", "performance"),
            ["profile_cpu"] = new ToolBriefInfo("profile_cpu", "Profile CPU performance", "performance"),
            ["get_profiler_stats"] = new ToolBriefInfo("get_profiler_stats", "Get profiler statistics", "performance"),

            // Materials
            ["diagnose_materials"] = new ToolBriefInfo("diagnose_materials", "Diagnose material issues", "materials"),
            ["repair_materials"] = new ToolBriefInfo("repair_materials", "Repair broken materials", "materials"),

            // Diagnostics
            ["mcp_status"] = new ToolBriefInfo("mcp_status", "Get MCP server status", "diagnostics"),
            ["get_console_logs"] = new ToolBriefInfo("get_console_logs", "Get Unity console logs", "diagnostics"),
            ["configure_editor"] = new ToolBriefInfo("configure_editor", "Configure editor settings", "diagnostics"),
            ["capture_screenshot"] = new ToolBriefInfo("capture_screenshot", "Capture editor screenshot", "diagnostics"),
            ["setup_status"] = new ToolBriefInfo("setup_status", "Check all MCP client config health", "diagnostics"),
            ["reconfigure_client"] = new ToolBriefInfo("reconfigure_client", "Fix/update client configurations", "diagnostics"),
            ["install_skills"] = new ToolBriefInfo("install_skills", "Install or update bundled workflow skills into .claude/skills", "diagnostics"),

            // Scene Management
            ["manage_scene"] = new ToolBriefInfo("manage_scene", "Manage scene files", "scene_mgmt"),

            // Phase 2: Animation
            ["control_animator"] = new ToolBriefInfo("control_animator", "Control Animator states and parameters", "animation"),
            ["manage_animation_clip"] = new ToolBriefInfo("manage_animation_clip", "List, info, create animation clips", "animation"),

            // Phase 2: UI
            ["create_ui_element"] = new ToolBriefInfo("create_ui_element", "Create UI elements (Button, Text, etc.)", "ui"),
            ["manage_canvas"] = new ToolBriefInfo("manage_canvas", "Configure Canvas and get UI hierarchy", "ui"),

            // Phase 2: Audio
            ["manage_audio_source"] = new ToolBriefInfo("manage_audio_source", "Create and configure AudioSource", "audio"),
            ["play_audio"] = new ToolBriefInfo("play_audio", "Control audio playback", "audio"),

            // Phase 2: Lighting
            ["manage_light"] = new ToolBriefInfo("manage_light", "Create and configure lights", "lighting"),
            ["configure_lighting"] = new ToolBriefInfo("configure_lighting", "Configure global lighting settings", "lighting"),

            // Phase 2: Camera
            ["control_camera"] = new ToolBriefInfo("control_camera", "Control cameras and align to view", "camera"),
            ["setup_cinemachine"] = new ToolBriefInfo("setup_cinemachine", "Setup Cinemachine virtual cameras", "camera"),

            // Editor Operations & Reflection
            ["editor_operations"] = new ToolBriefInfo("editor_operations", "Undo, redo, get/set selection, undo history", "editor"),
            ["unity_reflect"] = new ToolBriefInfo("unity_reflect", "Inspect types via reflection, search types, invoke static methods", "editor"),
            ["batch_execute"] = new ToolBriefInfo("batch_execute", "Execute multiple tool calls in one request with atomic undo", "editor"),

            // Assets & Prefabs
            ["manage_assets"] = new ToolBriefInfo("manage_assets", "Asset info, create folder, delete, move, rename, duplicate, import", "assets"),
            ["manage_prefabs"] = new ToolBriefInfo("manage_prefabs", "Create, apply/revert, unpack, save, variant, info for prefabs", "assets"),

            // Package Manager
            ["manage_packages"] = new ToolBriefInfo("manage_packages", "Search Unity Registry for packages/assets (Cinemachine, URP, etc.)", "packages"),
            ["install_package"] = new ToolBriefInfo("install_package", "Install package from Unity Registry/Git/local", "packages"),
            ["uninstall_package"] = new ToolBriefInfo("uninstall_package", "Remove package from project", "packages")
        };

        /// <summary>
        /// Gets the category for a tool name.
        /// </summary>
        public static string GetCategoryForTool(string toolName)
        {
            if (ToolBriefs.TryGetValue(toolName, out var brief))
            {
                return brief.Category;
            }
            return null;
        }

        /// <summary>
        /// Gets all tool names in a category.
        /// </summary>
        public static IEnumerable<string> GetToolsInCategory(string category)
        {
            if (Categories.TryGetValue(category.ToLowerInvariant(), out var info))
            {
                return info.ToolNames;
            }
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets a formatted summary of all categories.
        /// </summary>
        public static string GetCategorySummary()
        {
            var parts = Categories.Select(kvp => $"{kvp.Key} ({kvp.Value.ToolNames.Length})");
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Gets a formatted list of tools in a category.
        /// </summary>
        public static string GetCategoryToolList(string category)
        {
            var key = category.ToLowerInvariant();
            if (!Categories.ContainsKey(key))
            {
                return null;
            }

            var lines = new List<string>();
            foreach (var toolName in Categories[key].ToolNames)
            {
                if (ToolBriefs.TryGetValue(toolName, out var brief))
                {
                    lines.Add($"- {toolName}: {brief.BriefDescription}");
                }
            }
            return string.Join("\n", lines);
        }
    }
}
