using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using NanoraUnityMCP.Protocol;
using NanoraUnityMCP.Tools;
using NanoraUnityMCP.Tools.Meta;
using System.Reflection;

namespace NanoraUnityMCP.Core
{
    /// <summary>
    /// Registry for MCP tools. Automatically discovers and registers tools via reflection.
    /// Supports SuperSave mode for 96%+ token reduction.
    /// </summary>
    public class ToolRegistry
    {
        private readonly Dictionary<string, IMcpTool> m_tools = new Dictionary<string, IMcpTool>();

        /// <summary>
        /// Meta-tool names for SuperSave mode.
        /// </summary>
        private static readonly HashSet<string> s_metaToolNames = new HashSet<string>
        {
            "list_categories",
            "list_tools",
            "execute"
        };

        /// <summary>
        /// Whether SuperSave mode is enabled.
        /// Set via NANORA_MCP_SUPERSAVE environment variable.
        /// </summary>
        public bool IsSuperSaveMode { get; private set; }

        public ToolRegistry()
        {
            CheckSuperSaveMode();
            DiscoverTools();
        }

        /// <summary>
        /// Checks if SuperSave mode is enabled via environment variable.
        /// </summary>
        private void CheckSuperSaveMode()
        {
            var envValue = Environment.GetEnvironmentVariable("NANORA_MCP_SUPERSAVE");
            IsSuperSaveMode = !string.IsNullOrEmpty(envValue) &&
                (envValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                 envValue == "1");

            if (IsSuperSaveMode)
            {
                Debug.Log("[UnityMCP] SuperSave mode enabled - only meta-tools will be exposed");
            }
        }

        /// <summary>
        /// Discovers all tools marked with [McpTool] attribute.
        /// </summary>
        private void DiscoverTools()
        {
            var toolInterface = typeof(IMcpTool);

            var toolTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => !t.IsAbstract && !t.IsInterface && toolInterface.IsAssignableFrom(t));

            foreach (var type in toolTypes)
            {
                try
                {
                    var tool = (IMcpTool)Activator.CreateInstance(type);
                    RegisterTool(tool);

                    // Inject registry into tools that need it
                    if (tool is ExecuteTool executeTool)
                    {
                        executeTool.SetRegistry(this);
                    }

                    // Inject registry via reflection for tools defined in other assemblies
                    var setRegistryMethod = type.GetMethod("SetRegistry", new[] { typeof(ToolRegistry) });
                    if (setRegistryMethod != null && !(tool is ExecuteTool))
                    {
                        setRegistryMethod.Invoke(tool, new object[] { this });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityMCP] Failed to instantiate tool {type.Name}: {ex.Message}");
                }
            }

            Debug.Log($"[UnityMCP] Registered {m_tools.Count} tools (SuperSave: {IsSuperSaveMode})");
        }

        /// <summary>
        /// Registers a tool manually.
        /// </summary>
        public void RegisterTool(IMcpTool tool)
        {
            if (tool == null) return;

            if (m_tools.ContainsKey(tool.Name))
            {
                Debug.LogWarning($"[UnityMCP] Tool '{tool.Name}' is already registered, skipping duplicate");
                return;
            }

            m_tools[tool.Name] = tool;
            Debug.Log($"[UnityMCP] Registered tool: {tool.Name}");
        }

        /// <summary>
        /// Gets a tool by name.
        /// </summary>
        public IMcpTool GetTool(string name)
        {
            m_tools.TryGetValue(name, out var tool);
            return tool;
        }

        /// <summary>
        /// Gets all tool definitions for the tools/list response.
        /// In SuperSave mode, only returns meta-tools (list_categories, list_tools, execute).
        /// </summary>
        public List<McpToolDefinition> GetToolDefinitions()
        {
            var tools = IsSuperSaveMode
                ? m_tools.Values.Where(t => s_metaToolNames.Contains(t.Name))
                : m_tools.Values;

            // Stable ordering also improves client prompt-cache reuse.
            return tools.OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => new McpToolDefinition
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema
            }).ToList();
        }

        /// <summary>
        /// Gets all registered tool names.
        /// In SuperSave mode, only returns meta-tool names for external API.
        /// </summary>
        public IEnumerable<string> GetToolNames()
        {
            if (IsSuperSaveMode)
            {
                return m_tools.Keys.Where(k => s_metaToolNames.Contains(k));
            }
            return m_tools.Keys;
        }

        /// <summary>
        /// Gets all registered tool names regardless of SuperSave mode.
        /// For internal use only.
        /// </summary>
        public IEnumerable<string> GetAllToolNames()
        {
            return m_tools.Keys;
        }

        /// <summary>
        /// Checks if a tool is a meta-tool.
        /// </summary>
        public static bool IsMetaTool(string toolName)
        {
            return s_metaToolNames.Contains(toolName);
        }

        /// <summary>
        /// Gets the total count of registered tools.
        /// </summary>
        public int TotalToolCount => m_tools.Count;

        /// <summary>
        /// Gets the count of exposed tools (respects SuperSave mode).
        /// </summary>
        public int ExposedToolCount => IsSuperSaveMode ? s_metaToolNames.Count : m_tools.Count;
    }
}
