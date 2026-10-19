using System;
using System.Threading.Tasks;

namespace NanoraUnityMCP.Transport
{
    /// <summary>
    /// Interface for MCP transport implementations.
    /// </summary>
    public interface ITransportServer : IDisposable
    {
        /// <summary>
        /// Event fired when a message is received from a client.
        /// </summary>
        event Action<string, Action<string>> OnMessageReceived;

        /// <summary>
        /// Whether the server is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// The port the server is listening on.
        /// </summary>
        int Port { get; }

        /// <summary>
        /// Starts the transport server.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the transport server.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Sends a message to all connected clients via SSE.
        /// </summary>
        void BroadcastMessage(string message);
    }
}
