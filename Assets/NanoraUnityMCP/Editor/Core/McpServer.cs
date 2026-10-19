using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using NanoraUnityMCP.Protocol;
using NanoraUnityMCP.Transport;

namespace NanoraUnityMCP.Core
{
    /// <summary>
    /// Main MCP Server class. Coordinates transport, protocol, and tools.
    /// Enhanced with fast reconnection after domain reload.
    /// </summary>
    [InitializeOnLoad]
    public static class McpServer
    {
        private const int k_DefaultPort = 51726;
        private const int k_PortRange = 100; // Deterministic ports: 51726-51825
        private const int k_UnsetPort = -1; // Sentinel for first-run detection

        /// <summary>
        /// The MCP server name used in client configurations.
        /// Using a unique name to avoid conflicts with official Unity MCP implementations.
        /// </summary>
        public const string ServerName = "nanora-unity-mcp";
        public const string Version = "1.2.1";

        // InitializeOnLoad runs on the main thread; HTTP callbacks must not call Application.dataPath.
        private static readonly string s_ProjectPath = Path.GetDirectoryName(Application.dataPath);

        private static ITransportServer s_Transport;
        private static McpProtocol s_Protocol;
        private static ToolRegistry s_ToolRegistry;

        private static bool s_AutoStart = true;
        private static bool s_AutoGenerateMcpJson = true;
        private static int s_Port = k_DefaultPort;

        // Diagnostics
        private static DateTime s_StartTime;
        private static int s_RestartCount;
        private static bool s_IsCompiling;
        private static DateTime? s_LastCompilationTime;

        public static bool IsRunning => s_Transport?.IsRunning ?? false;
        public static int Port => s_Transport?.Port ?? s_Port;
        public static int RequestedPort => s_Port;
        public static int ConnectedClients { get; private set; }

        // Diagnostic properties
        public static TimeSpan Uptime => IsRunning ? DateTime.Now - s_StartTime : TimeSpan.Zero;
        public static int RestartCount => s_RestartCount;
        public static bool IsCompiling => s_IsCompiling;
        public static DateTime? LastCompilationTime => s_LastCompilationTime;

        /// <summary>
        /// Returns the current Unity project name (directory name).
        /// </summary>
        public static string ProjectName
        {
            get
            {
                return Path.GetFileName(s_ProjectPath);
            }
        }

        /// <summary>
        /// Returns the full path to the current Unity project root directory.
        /// </summary>
        public static string ProjectPath => s_ProjectPath;

        /// <summary>
        /// Returns true if any C# script has been modified since the last compilation.
        /// This helps detect when code changes haven't been compiled yet.
        /// </summary>
        public static bool HasUncompiledChanges
        {
            get
            {
                if (!s_LastCompilationTime.HasValue)
                    return false; // No compilation recorded, assume up to date

                var latestScriptTime = GetLatestScriptModificationTime();
                if (!latestScriptTime.HasValue)
                    return false;

                // Add 1 second tolerance for file system timestamp precision
                return latestScriptTime.Value > s_LastCompilationTime.Value.AddSeconds(1);
            }
        }

        /// <summary>
        /// Gets the most recent modification time of any C# script in the Assets folder.
        /// </summary>
        private static DateTime? GetLatestScriptModificationTime()
        {
            try
            {
                var assetsPath = Path.Combine(ProjectPath, "Assets");
                var scriptFiles = Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories);

                if (scriptFiles.Length == 0)
                    return null;

                return scriptFiles
                    .Select(f => File.GetLastWriteTime(f))
                    .Max();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Failed to check script modification times: {ex.Message}");
                return null;
            }
        }

        public static event Action<bool> OnServerStateChanged;

        static McpServer()
        {
            // Load settings (project-specific via ProjectPrefs)
            s_AutoStart = ProjectPrefs.GetBool("UnityMCP_AutoStart", true);
            s_AutoGenerateMcpJson = ProjectPrefs.GetBool("UnityMCP_AutoGenerateMcpJson", true);

            // Deterministic port: on first run, derive port from project path
            int savedPort = ProjectPrefs.GetInt("UnityMCP_Port", k_UnsetPort);
            if (savedPort == k_UnsetPort)
            {
                s_Port = GetDeterministicPort(Application.dataPath);
                ProjectPrefs.SetInt("UnityMCP_Port", s_Port);
                Debug.Log($"[UnityMCP] Assigned deterministic port {s_Port} for project: {ProjectName}");
            }
            else
            {
                s_Port = savedPort;
            }
            s_RestartCount = SessionState.GetInt("UnityMCP_RestartCount", 0);

            // Restore last compilation time from session state
            var lastCompileStr = SessionState.GetString("UnityMCP_LastCompilationTime", "");
            if (!string.IsNullOrEmpty(lastCompileStr) && long.TryParse(lastCompileStr, out long ticks))
            {
                s_LastCompilationTime = new DateTime(ticks);
            }

            // Subscribe to compilation events for better diagnostics
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // Fast startup: Use update callback for more immediate startup
            // This ensures server restarts as soon as possible after domain reload
            if (s_AutoStart && !Application.isBatchMode)
            {
                // Use update callback which fires sooner than delayCall
                EditorApplication.update += StartOnFirstUpdate;
            }

            // Cleanup on domain unload - store state for fast restart
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (IsRunning)
                {
                    // Increment restart count for diagnostics
                    SessionState.SetInt("UnityMCP_RestartCount", s_RestartCount + 1);

                    // Notify clients about impending domain reload before stopping
                    if (s_Transport is HttpSseTransport httpTransport)
                    {
                        httpTransport.NotifyDomainReload();
                    }

                    Stop();
                }
            };

            EditorApplication.quitting += () =>
            {
                if (IsRunning)
                {
                    Stop();
                }
            };
        }

        private static void StartOnFirstUpdate()
        {
            // Remove this callback immediately
            EditorApplication.update -= StartOnFirstUpdate;

            if (!IsRunning && s_AutoStart && !Application.isBatchMode)
            {
                Start();
            }
        }

        private static void OnCompilationStarted(object context)
        {
            s_IsCompiling = true;
            Debug.Log("[UnityMCP] Compilation started - server will restart after completion");
        }

        private static void OnCompilationFinished(object context)
        {
            s_IsCompiling = false;
            s_LastCompilationTime = DateTime.Now;
            // Persist compilation time across domain reload
            SessionState.SetString("UnityMCP_LastCompilationTime", s_LastCompilationTime.Value.Ticks.ToString());
            Debug.Log("[UnityMCP] Compilation finished");
        }

        public static void Start()
        {
            if (IsRunning)
            {
                Debug.LogWarning("[UnityMCP] Server is already running");
                return;
            }

            try
            {
                // Initialize components
                s_ToolRegistry = new ToolRegistry();
                s_Protocol = new McpProtocol(s_ToolRegistry);
                s_Transport = new HttpSseTransport(s_Port);

                // Wire up message handling
                s_Transport.OnMessageReceived += HandleMessage;

                // Start transport (may use different port if requested port is in use)
                s_Transport.StartAsync().Wait();

                // Track startup time
                s_StartTime = DateTime.Now;
                s_RestartCount = SessionState.GetInt("UnityMCP_RestartCount", 0);

                int actualPort = s_Transport.Port;
                if (actualPort != s_Port)
                {
                    // Port fallback occurred - another Unity instance likely using the configured port
                    Debug.LogWarning($"[UnityMCP] Port {s_Port} is in use by another instance. Using port {actualPort}.");
                }

                // Auto-generate .mcp.json if enabled
                if (s_AutoGenerateMcpJson)
                {
                    EnsureMcpJson(actualPort);
                }

                // Register in global server registry for discovery
                RegisterInGlobalRegistry(actualPort);

                if (s_RestartCount > 0)
                {
                    Debug.Log($"[UnityMCP] Server restarted on port {actualPort} (restart #{s_RestartCount} after domain reload)");
                }
                else
                {
                    Debug.Log($"[UnityMCP] Server started on port {actualPort} (Project: {ProjectName})");
                }
                Debug.Log($"[UnityMCP] Connect via: http://127.0.0.1:{actualPort}/mcp (Codex/HTTP), /sse (legacy)");

                OnServerStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Failed to start server: {ex.Message}");
                Stop();
            }
        }

        public static void Stop()
        {
            // Unregister from global registry before stopping
            UnregisterFromGlobalRegistry();

            if (s_Transport != null)
            {
                s_Transport.OnMessageReceived -= HandleMessage;
                s_Transport.StopAsync().ConfigureAwait(false);
                s_Transport.Dispose();
                s_Transport = null;
            }

            s_Protocol = null;
            s_ToolRegistry = null;

            Debug.Log("[UnityMCP] Server stopped");
            OnServerStateChanged?.Invoke(false);
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        public static void SetAutoStart(bool autoStart)
        {
            s_AutoStart = autoStart;
            ProjectPrefs.SetBool("UnityMCP_AutoStart", autoStart);
        }

        public static bool GetAutoStart()
        {
            return s_AutoStart;
        }

        public static void SetAutoGenerateMcpJson(bool autoGenerate)
        {
            s_AutoGenerateMcpJson = autoGenerate;
            ProjectPrefs.SetBool("UnityMCP_AutoGenerateMcpJson", autoGenerate);
        }

        public static bool GetAutoGenerateMcpJson()
        {
            return s_AutoGenerateMcpJson;
        }

        public static void SetPort(int port)
        {
            if (port < 1024 || port > 65535)
            {
                Debug.LogError("[UnityMCP] Port must be between 1024 and 65535");
                return;
            }

            s_Port = port;
            ProjectPrefs.SetInt("UnityMCP_Port", port);

            if (IsRunning)
            {
                Debug.Log("[UnityMCP] Port changed. Restarting server...");
                Restart();
            }
        }

        public static string[] GetRegisteredTools()
        {
            if (s_ToolRegistry == null)
                return Array.Empty<string>();

            return System.Linq.Enumerable.ToArray(s_ToolRegistry.GetToolNames());
        }

        private static async void HandleMessage(string message, Action<string> respond)
        {
            try
            {
                var response = await s_Protocol.ProcessMessageAsync(message);
                respond(response); // Null completes notifications with HTTP 202.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Message handling error: {ex.Message}");
                respond(Newtonsoft.Json.JsonConvert.SerializeObject(
                    JsonRpcResponse.CreateError(null, JsonRpcErrorCodes.InternalError, ex.Message)));
            }
        }

        /// <summary>
        /// Derives a deterministic port from the project path using FNV-1a hash.
        /// This ensures each project always gets the same port regardless of startup order.
        /// Range: [k_DefaultPort, k_DefaultPort + k_PortRange)
        /// </summary>
        private static int GetDeterministicPort(string projectPath)
        {
            uint hash = StableHash(projectPath.ToLowerInvariant());
            return k_DefaultPort + (int)(hash % k_PortRange);
        }

        /// <summary>
        /// FNV-1a 32-bit hash — stable across .NET runtimes unlike string.GetHashCode().
        /// </summary>
        private static uint StableHash(string input)
        {
            const uint fnvOffsetBasis = 2166136261u;
            const uint fnvPrime = 16777619u;

            uint hash = fnvOffsetBasis;
            foreach (char c in input)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return hash;
        }

        /// <summary>
        /// Ensures .mcp.json exists in the project root with the correct port.
        /// Creates the file if missing, or updates the URL if the port has changed.
        /// </summary>
        private static void EnsureMcpJson(int port)
        {
            try
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var mcpJsonPath = Path.Combine(projectRoot, ".mcp.json");
                var expectedUrl = $"http://localhost:{port}/sse";

                if (File.Exists(mcpJsonPath))
                {
                    // Update existing file if needed
                    var content = File.ReadAllText(mcpJsonPath);
                    Newtonsoft.Json.Linq.JObject json;
                    try
                    {
                        json = Newtonsoft.Json.Linq.JObject.Parse(content);
                    }
                    catch
                    {
                        // Invalid JSON — don't overwrite, user may have custom content
                        return;
                    }

                    if (json["mcpServers"] == null)
                    {
                        json["mcpServers"] = new Newtonsoft.Json.Linq.JObject();
                    }

                    var servers = (Newtonsoft.Json.Linq.JObject)json["mcpServers"];
                    var server = servers[ServerName] as Newtonsoft.Json.Linq.JObject;

                    if (server != null)
                    {
                        var currentUrl = (string)server["url"];
                        var currentType = (string)server["type"];
                        if (currentUrl == expectedUrl && currentType == "sse") return; // Already correct
                        server["type"] = "sse";
                        server["url"] = expectedUrl;
                    }
                    else
                    {
                        servers[ServerName] = new Newtonsoft.Json.Linq.JObject
                        {
                            ["type"] = "sse",
                            ["url"] = expectedUrl
                        };
                    }

                    File.WriteAllText(mcpJsonPath, json.ToString(Newtonsoft.Json.Formatting.Indented));
                    Debug.Log($"[UnityMCP] Updated .mcp.json with port {port}");
                }
                else
                {
                    // Create new .mcp.json
                    var json = new Newtonsoft.Json.Linq.JObject
                    {
                        ["mcpServers"] = new Newtonsoft.Json.Linq.JObject
                        {
                            [ServerName] = new Newtonsoft.Json.Linq.JObject
                            {
                                ["type"] = "sse",
                                ["url"] = expectedUrl
                            }
                        }
                    };

                    File.WriteAllText(mcpJsonPath, json.ToString(Newtonsoft.Json.Formatting.Indented));
                    Debug.Log($"[UnityMCP] Created .mcp.json with port {port}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Failed to ensure .mcp.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Path to the global server registry file shared by all Unity MCP instances.
        /// </summary>
        private static string GlobalRegistryPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".unity-mcp-registry.json");

        /// <summary>
        /// Registers this server instance in the global registry file.
        /// Other tools (e.g., Claude Code) can read this file to discover running servers
        /// even when the MCP connection is unavailable.
        /// </summary>
        private static void RegisterInGlobalRegistry(int port)
        {
            try
            {
                var registryPath = GlobalRegistryPath;
                Newtonsoft.Json.Linq.JArray servers;

                if (File.Exists(registryPath))
                {
                    var content = File.ReadAllText(registryPath);
                    var json = Newtonsoft.Json.Linq.JObject.Parse(content);
                    servers = (Newtonsoft.Json.Linq.JArray)(json["servers"] ?? new Newtonsoft.Json.Linq.JArray());
                    servers = CleanStaleRegistryEntries(servers);
                }
                else
                {
                    servers = new Newtonsoft.Json.Linq.JArray();
                }

                // Remove existing entry for this project (if re-registering)
                var projectPath = ProjectPath;
                for (int i = servers.Count - 1; i >= 0; i--)
                {
                    var entry = servers[i] as Newtonsoft.Json.Linq.JObject;
                    if (entry != null && (string)entry["projectPath"] == projectPath)
                    {
                        servers.RemoveAt(i);
                    }
                }

                servers.Add(new Newtonsoft.Json.Linq.JObject
                {
                    ["project"] = ProjectName,
                    ["projectPath"] = projectPath,
                    ["port"] = port,
                    ["pid"] = System.Diagnostics.Process.GetCurrentProcess().Id,
                    ["startedAt"] = DateTime.Now.ToString("o"),
                    ["unityVersion"] = Application.unityVersion
                });

                var result = new Newtonsoft.Json.Linq.JObject { ["servers"] = servers };
                File.WriteAllText(registryPath, result.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Failed to register in global registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes this server's entry from the global registry on shutdown.
        /// </summary>
        private static void UnregisterFromGlobalRegistry()
        {
            try
            {
                var registryPath = GlobalRegistryPath;
                if (!File.Exists(registryPath)) return;

                var content = File.ReadAllText(registryPath);
                var json = Newtonsoft.Json.Linq.JObject.Parse(content);
                var servers = (Newtonsoft.Json.Linq.JArray)(json["servers"] ?? new Newtonsoft.Json.Linq.JArray());

                var projectPath = ProjectPath;
                for (int i = servers.Count - 1; i >= 0; i--)
                {
                    var entry = servers[i] as Newtonsoft.Json.Linq.JObject;
                    if (entry != null && (string)entry["projectPath"] == projectPath)
                    {
                        servers.RemoveAt(i);
                    }
                }

                servers = CleanStaleRegistryEntries(servers);

                if (servers.Count == 0)
                {
                    File.Delete(registryPath);
                }
                else
                {
                    json["servers"] = servers;
                    File.WriteAllText(registryPath, json.ToString(Newtonsoft.Json.Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Failed to unregister from global registry: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes entries from the registry whose PID is no longer alive.
        /// </summary>
        private static Newtonsoft.Json.Linq.JArray CleanStaleRegistryEntries(Newtonsoft.Json.Linq.JArray servers)
        {
            var cleaned = new Newtonsoft.Json.Linq.JArray();
            foreach (var item in servers)
            {
                var entry = item as Newtonsoft.Json.Linq.JObject;
                if (entry == null) continue;

                var pid = (int?)entry["pid"] ?? 0;
                if (pid <= 0) continue;

                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(pid);
                    if (!process.HasExited)
                    {
                        cleaned.Add(entry);
                    }
                }
                catch (ArgumentException)
                {
                    // Process not found — stale entry, skip it
                }
                catch
                {
                    // Other error checking process — keep the entry to be safe
                    cleaned.Add(entry);
                }
            }
            return cleaned;
        }
    }
}
