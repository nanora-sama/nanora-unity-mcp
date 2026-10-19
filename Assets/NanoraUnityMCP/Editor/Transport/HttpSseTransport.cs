using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;
using Newtonsoft.Json;

namespace NanoraUnityMCP.Transport
{
    /// <summary>
    /// HTTP/SSE transport implementation for MCP.
    /// Provides SSE endpoint for server-to-client messages and HTTP POST for client-to-server messages.
    /// Supports dynamic port allocation when the default port is in use.
    /// </summary>
    public class HttpSseTransport : ITransportServer
    {
        private const int k_DefaultPort = 51726;
        private const int k_MaxPortAttempts = 10;

        private HttpListener m_listener;
        private CancellationTokenSource m_cts;
        private readonly List<SseClient> m_sseClients = new List<SseClient>();
        private readonly object m_clientsLock = new object();

        public event Action<string, Action<string>> OnMessageReceived;
        public bool IsRunning { get; private set; }
        public int Port { get; private set; }
        public int RequestedPort { get; private set; }

        private class SseClient
        {
            public string SessionId { get; } = Guid.NewGuid().ToString("N");
            public StreamWriter Writer { get; set; }
            public bool IsConnected { get; set; }
        }

        public HttpSseTransport(int port = k_DefaultPort)
        {
            RequestedPort = port;
            Port = port;
        }

        public async Task StartAsync()
        {
            if (IsRunning) return;

            m_cts = new CancellationTokenSource();

            // Try to bind to ports starting from RequestedPort
            Exception lastException = null;
            int attempts = 0;
            for (int attempt = 0; attempt < k_MaxPortAttempts; attempt++)
            {
                int tryPort = RequestedPort + attempt;
                if (tryPort > 65535) break;
                attempts++;

                try
                {
                    m_listener = new HttpListener();
                    m_listener.Prefixes.Add($"http://localhost:{tryPort}/");
                    m_listener.Prefixes.Add($"http://127.0.0.1:{tryPort}/");

                    m_listener.Start();
                    Port = tryPort;
                    IsRunning = true;

                    if (attempt > 0)
                    {
                        Debug.Log($"[UnityMCP] HTTP/SSE server started on port {Port} (port {RequestedPort} was in use)");
                    }
                    else
                    {
                        Debug.Log($"[UnityMCP] HTTP/SSE server started on port {Port}");
                    }

                    // Start listening loop
                    _ = ListenLoop(m_cts.Token);

                    await Task.CompletedTask;
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    // Clean up failed listener
                    try { m_listener?.Close(); } catch { }
                    m_listener = null;

                    // Only continue trying if the port is taken; any other failure (access denied, bad prefix) stops here.
                    if (!IsAddressInUse(ex))
                    {
                        break;
                    }
                }
            }

            Debug.LogError($"[UnityMCP] Failed to start server after {attempts} attempt(s) from port {RequestedPort}: {lastException?.Message}");
            throw lastException ?? new Exception("Failed to bind to any port");
        }

        /// <summary>
        /// Whether a listener failed to start because its port is already taken: another process (a second Editor, or a
        /// child process that inherited this Editor's socket) or another listener in this process. Decided from error
        /// codes, not the message text, which the OS localises (on Japanese Windows the WSAEADDRINUSE message contains
        /// none of the English words "address" or "use"). Unity's Mono HttpListener raises a SocketException for a socket bind
        /// conflict and an HttpListenerException ("There's another listener for ...") for a duplicate prefix.
        /// </summary>
        public static bool IsAddressInUse(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is AggregateException aggregate && aggregate.InnerExceptions.Count > 0)
                {
                    foreach (var inner in aggregate.InnerExceptions)
                        if (IsAddressInUse(inner)) return true;
                    return false;
                }
                if (e is SocketException socket && socket.SocketErrorCode == SocketError.AddressAlreadyInUse) return true;
                if (e is HttpListenerException listener)
                {
                    // 10048 WSAEADDRINUSE, 183 ERROR_ALREADY_EXISTS and 32 ERROR_SHARING_VIOLATION (http.sys prefix taken).
                    int code = listener.ErrorCode;
                    if (code == 10048 || code == 183 || code == 32) return true;
                    // Mono's own message for a prefix registered by another listener; it is not localised.
                    if (listener.Message != null && listener.Message.Contains("another listener")) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Notify connected SSE clients that a domain reload is imminent.
        /// Called before Stop() to give clients a chance to prepare for reconnection.
        /// </summary>
        public void NotifyDomainReload()
        {
            if (!IsRunning) return;

            try
            {
                var message = FormatSseEvent("domain_reload", "{\"reason\":\"assembly_reload\"}");

                lock (m_clientsLock)
                {
                    foreach (var client in m_sseClients)
                    {
                        if (!client.IsConnected) continue;
                        try
                        {
                            client.Writer.Write(message);
                            client.Writer.Flush();
                        }
                        catch { }
                    }
                }

                Debug.Log("[UnityMCP] Domain reload notification sent to SSE clients");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Failed to send domain reload notification: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            m_cts?.Cancel();

            lock (m_clientsLock)
            {
                foreach (var client in m_sseClients)
                {
                    client.IsConnected = false;
                    try { client.Writer?.Close(); } catch { }
                }
                m_sseClients.Clear();
            }

            try
            {
                m_listener?.Stop();
                m_listener?.Close();
            }
            catch { }

            IsRunning = false;
            Debug.Log("[UnityMCP] HTTP/SSE server stopped");

            await Task.CompletedTask;
        }

        public void BroadcastMessage(string message)
        {
            var sseMessage = FormatSseMessage(message);

            lock (m_clientsLock)
            {
                var disconnected = new List<SseClient>();

                foreach (var client in m_sseClients)
                {
                    if (!client.IsConnected)
                    {
                        disconnected.Add(client);
                        continue;
                    }

                    try
                    {
                        client.Writer.Write(sseMessage);
                        client.Writer.Flush();
                    }
                    catch
                    {
                        client.IsConnected = false;
                        disconnected.Add(client);
                    }
                }

                foreach (var client in disconnected)
                {
                    m_sseClients.Remove(client);
                }
            }
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsRunning)
            {
                try
                {
                    var context = await m_listener.GetContextAsync().ConfigureAwait(false);
                    _ = HandleRequest(context, ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityMCP] Listen error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequest(HttpListenerContext context, CancellationToken ct)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // Native MCP clients omit Origin. Browser requests must be same-server loopback.
                var origin = request.Headers["Origin"];
                if (origin != null)
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
                        originUri.Scheme != "http" || originUri.Port != Port ||
                        (originUri.Host != "localhost" && originUri.Host != "127.0.0.1"))
                    {
                        response.StatusCode = 403;
                        await WriteResponse(response, "{\"error\":\"Origin not allowed\"}");
                        return;
                    }
                    response.Headers["Access-Control-Allow-Origin"] = origin;
                    response.Headers["Vary"] = "Origin";
                }
                response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                response.Headers["Access-Control-Allow-Headers"] = "Content-Type, MCP-Protocol-Version, MCP-Session-Id";

                // Handle preflight
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                var path = request.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');

                switch (path)
                {
                    case "/mcp":
                        // Stateless Streamable HTTP: JSON replies on POST, no optional GET stream.
                        if (request.HttpMethod != "POST")
                        {
                            response.StatusCode = 405;
                            response.Headers["Allow"] = "POST, OPTIONS";
                            await WriteResponse(response, "{\"error\":\"Use POST for MCP messages\"}");
                            break;
                        }
                        var version = request.Headers["MCP-Protocol-Version"];
                        if (version != null && !McpProtocol.IsSupportedVersion(version))
                        {
                            response.StatusCode = 400;
                            await WriteResponse(response, "{\"error\":\"Unsupported MCP protocol version\"}");
                            break;
                        }
                        await HandleMessage(context, ct);
                        break;

                    case "/sse":
                        if (request.HttpMethod != "GET")
                        {
                            response.StatusCode = 405;
                            await WriteResponse(response, "{\"error\":\"Use GET for SSE\"}");
                            break;
                        }
                        await HandleSseConnection(context, ct);
                        break;

                    case "/message":
                        await HandleMessage(context, ct);
                        break;

                    case "/health":
                        await HandleHealth(context);
                        break;

                    default:
                        // OAuth discovery probes are normal for clients such as Codex.
                        // The HTTP 404 is sufficient; do not flood the Unity Console.
                        response.StatusCode = 404;
                        await WriteResponse(response, "{\"error\":\"Not found\"}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Request error: {ex.Message}");
                try
                {
                    response.StatusCode = 500;
                    await WriteResponse(response, JsonConvert.SerializeObject(new { error = ex.Message }));
                }
                catch { }
            }
        }

        private async Task HandleSseConnection(HttpListenerContext context, CancellationToken ct)
        {
            var response = context.Response;
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.SendChunked = true;

            // Use UTF8 without BOM - BOM causes SSE parsing issues in MCP clients
            var writer = new StreamWriter(response.OutputStream, new UTF8Encoding(false));
            var client = new SseClient { Writer = writer, IsConnected = true };

            lock (m_clientsLock)
            {
                m_sseClients.Add(client);
            }

            Debug.Log("[UnityMCP] SSE client connected");

            // Send endpoint info as first message (just the URL, not JSON)
            // Keep connection alive
            try
            {
                var endpointUrl = $"{context.Request.Url.GetLeftPart(UriPartial.Authority)}/message?sessionId={client.SessionId}";
                lock (m_clientsLock)
                {
                    writer.Write(FormatSseEvent("endpoint", endpointUrl));
                    writer.Flush();
                }
                while (!ct.IsCancellationRequested && client.IsConnected)
                {
                    await Task.Delay(15000, ct); // Heartbeat every 15 seconds
                    if (client.IsConnected)
                    {
                        lock (m_clientsLock)
                        {
                            writer.Write(": heartbeat\n\n");
                            writer.Flush();
                        }
                    }
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception)
            {
                client.IsConnected = false;
            }

            lock (m_clientsLock)
            {
                client.IsConnected = false;
                m_sseClients.Remove(client);
                try { writer.Dispose(); response.Close(); } catch { }
            }

            Debug.Log("[UnityMCP] SSE client disconnected");
        }

        private async Task HandleMessage(HttpListenerContext context, CancellationToken ct)
        {
            var request = context.Request;
            var response = context.Response;

            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                await WriteResponse(response, "{\"error\":\"Method not allowed\"}");
                return;
            }

            SseClient targetClient = null;
            var sessionId = request.QueryString["sessionId"];
            if (sessionId != null)
            {
                lock (m_clientsLock)
                    targetClient = m_sseClients.Find(client => client.SessionId == sessionId && client.IsConnected);
                if (targetClient == null)
                {
                    response.StatusCode = 404;
                    await WriteResponse(response, "{\"error\":\"SSE session expired; reconnect\"}");
                    return;
                }
            }

            const int maxBodyChars = 4 * 1024 * 1024;
            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                var builder = new StringBuilder();
                var buffer = new char[8192];
                int count;
                while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (builder.Length + count > maxBodyChars)
                    {
                        response.StatusCode = 413;
                        await WriteResponse(response, "{\"error\":\"Message too large\"}");
                        return;
                    }
                    builder.Append(buffer, 0, count);
                }
                body = builder.ToString();
            }

            // Process message and send response
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (OnMessageReceived == null)
            {
                response.StatusCode = 503;
                await WriteResponse(response, "{\"error\":\"Server is reloading\"}");
                return;
            }

            OnMessageReceived?.Invoke(body, (responseJson) =>
            {
                tcs.TrySetResult(responseJson);
            });

            // Wait for response with timeout
            var responseTask = tcs.Task;
            int timeoutMs = GetRequestTimeoutMs(body);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var timeoutTask = Task.Delay(timeoutMs, timeoutCts.Token);

            if (await Task.WhenAny(responseTask, timeoutTask) == responseTask)
            {
                timeoutCts.Cancel();
                var result = await responseTask;
                try
                {
                    response.ContentType = "application/json";
                    if (result == null)
                    {
                        response.StatusCode = 202;
                        await WriteResponse(response, "");
                    }
                    else
                    {
                        // A response belongs only to the requesting client, even when IDs overlap.
                        if (targetClient != null)
                        {
                            lock (m_clientsLock)
                            {
                                try
                                {
                                    targetClient.Writer.Write(FormatSseEvent("message", result));
                                    targetClient.Writer.Flush();
                                }
                                catch { targetClient.IsConnected = false; }
                            }
                            response.StatusCode = 202;
                            await WriteResponse(response, "");
                        }
                        else await WriteResponse(response, result);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Server shut down during response (e.g., domain reload after compilation)
                    // Response is lost but client will reconnect and can use get_compilation_errors
                    Debug.LogWarning("[UnityMCP] Response could not be sent - server shutting down (likely domain reload)");
                }
                catch (InvalidOperationException)
                {
                    // HttpListenerResponse was already closed
                    Debug.LogWarning("[UnityMCP] Response could not be sent - connection already closed");
                }
            }
            else
            {
                try
                {
                    // The server stops before a domain reload (compiling, entering Play Mode), which ends the wait early.
                    bool interrupted = ct.IsCancellationRequested;
                    Debug.LogWarning(interrupted
                        ? "[UnityMCP] Server stopping (domain reload); the request may already have completed"
                        : $"[UnityMCP] No response within {timeoutMs / 1000}s; the request may still be running");
                    response.StatusCode = interrupted ? 503 : 504;
                    response.ContentType = "application/json";
                    await WriteResponse(response, interrupted ? BuildInterruptedResponse(body) : BuildTimeoutResponse(body, timeoutMs));
                }
                catch (ObjectDisposedException)
                {
                    Debug.LogWarning("[UnityMCP] Timeout response could not be sent - server shutting down");
                }
                catch (InvalidOperationException)
                {
                    // Response already closed
                }
            }
        }

        // How long /message keeps a request open. Tools wait on their own (compile_project 60 s, manage_playmode up to
        // 120 s, run_tests 300 s by default), so a tools/call request waits for the longer of its timeout argument and
        // the longest tool default, plus a margin for the tool to report its own timeout. Other methods answer at once.
        internal static int NonToolRequestTimeoutMs = 30000;
        internal static int DefaultToolRequestTimeoutMs = 300000;
        internal static int ToolTimeoutGraceMs = 30000;
        private const int k_MaxRequestTimeoutMs = 3600000;

        internal static int GetRequestTimeoutMs(string body)
        {
            try
            {
                var message = JObject.Parse(body);
                if ((string)message["method"] != "tools/call") return NonToolRequestTimeoutMs;

                long requestedMs = 0;
                if (message["params"]?["arguments"] is JObject arguments)
                {
                    var seconds = arguments["timeout_seconds"];
                    var milliseconds = arguments["timeout_ms"];
                    if (seconds != null && (seconds.Type == JTokenType.Integer || seconds.Type == JTokenType.Float))
                        requestedMs = Math.Max(requestedMs, (long)((double)seconds * 1000));
                    if (milliseconds != null && (milliseconds.Type == JTokenType.Integer || milliseconds.Type == JTokenType.Float))
                        requestedMs = Math.Max(requestedMs, (long)(double)milliseconds);
                }
                long timeoutMs = Math.Max(requestedMs, DefaultToolRequestTimeoutMs) + ToolTimeoutGraceMs;
                return (int)Math.Min(timeoutMs, k_MaxRequestTimeoutMs);
            }
            catch (Exception)
            {
                return NonToolRequestTimeoutMs;
            }
        }

        // A JSON-RPC error carrying the request id, so the client can match it. The tool may still finish (or may already
        // have changed the project), so a change must not be retried before the Editor state is read back.
        internal static string BuildTimeoutResponse(string body, int timeoutMs)
        {
            return BuildErrorResponse(body, $"No response from Unity within {timeoutMs / 1000}s. The request may still be running or may " +
                                            "already have completed; check the Editor state before retrying a change.");
        }

        // Entering Play Mode or compiling reloads scripts and restarts the server mid-request. The tool may well have done
        // its work (Play Mode is usually entered by then), so the client should wait for /health and read the state.
        internal static string BuildInterruptedResponse(string body)
        {
            return BuildErrorResponse(body, "Unity is reloading scripts (domain reload), so this reply was cut off. The request may " +
                                            "already have completed: wait until /health responds, then check the Editor state " +
                                            "before retrying a change.");
        }

        private static string BuildErrorResponse(string body, string message)
        {
            JToken id = JValue.CreateNull();
            try
            {
                id = JObject.Parse(body)["id"] ?? JValue.CreateNull();
            }
            catch (Exception)
            {
                // Not JSON: answer without an id.
            }
            var error = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JObject
                {
                    ["code"] = -32001,
                    ["message"] = message
                }
            };
            return error.ToString(Newtonsoft.Json.Formatting.None);
        }

        private async Task HandleHealth(HttpListenerContext context)
        {
            var response = context.Response;
            response.ContentType = "application/json";
            await WriteResponse(response, JsonConvert.SerializeObject(new
            {
                status = "ok", server = McpServer.ServerName, version = McpServer.Version,
                project = McpServer.ProjectName, projectPath = McpServer.ProjectPath.Replace('\\', '/'),
                port = Port, protocolVersion = McpProtocol.ProtocolVersion,
                transports = new[] { "streamable-http", "sse" },
                processId = System.Diagnostics.Process.GetCurrentProcess().Id
            }));
        }

        private async Task WriteResponse(HttpListenerResponse response, string content)
        {
            response.ContentType = "application/json; charset=utf-8";
            var buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private string FormatSseMessage(string data)
        {
            return $"data: {data}\n\n";
        }

        private string FormatSseEvent(string eventType, string data)
        {
            return $"event: {eventType}\ndata: {data}\n\n";
        }

        public void Dispose()
        {
            StopAsync().Wait();
        }
    }
}
