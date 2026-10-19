using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using NanoraUnityMCP.Transport;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// When the configured port is already taken (another Unity instance, or a socket a child process inherited from
    /// this Editor), the transport must move on to the next port instead of giving up. The check must not depend on the
    /// language of the OS error message (Japanese Windows reports WSAEADDRINUSE without the words "address" or "use").
    /// </summary>
    [TestFixture]
    public class TransportPortFallbackTests
    {
        private TcpListener m_blocker;
        private HttpSseTransport m_transport;

        [TearDown]
        public void TearDown()
        {
            m_transport?.Dispose();
            m_transport = null;
            m_blocker?.Stop();
            m_blocker = null;
        }

        // An ephemeral loopback port, held open for the duration of the test.
        private int HoldFreePort()
        {
            m_blocker = new TcpListener(IPAddress.Loopback, 0);
            m_blocker.Start();
            return ((IPEndPoint)m_blocker.LocalEndpoint).Port;
        }

        [Test]
        public void StartAsync_PortHeldBySocket_FallsBackToNextPort()
        {
            int held = HoldFreePort();
            m_transport = new HttpSseTransport(held);

            Assert.DoesNotThrow(() => m_transport.StartAsync().Wait(), "the transport should fall back instead of failing");

            Assert.IsTrue(m_transport.IsRunning, "transport should be running");
            Assert.AreNotEqual(held, m_transport.Port, "the held port must not be reported as the bound port");
            Assert.Greater(m_transport.Port, held, "fallback ports are tried upward from the requested port");
        }

        // The decision must come from the error code, whatever language the OS message is in.
        [Test]
        public void IsAddressInUse_ClassifiesByErrorCodeNotMessage()
        {
            // "Only one usage of each socket address ..." as Japanese Windows phrases it.
            const string japanese = "通常、各ソケット アドレスに対して";
            Assert.IsTrue(HttpSseTransport.IsAddressInUse(new SocketException(10048)), "SocketException AddressAlreadyInUse");
            Assert.IsTrue(HttpSseTransport.IsAddressInUse(new HttpListenerException(10048, japanese)), "WSAEADDRINUSE with a localised message");
            Assert.IsTrue(HttpSseTransport.IsAddressInUse(new HttpListenerException(183, japanese)), "ERROR_ALREADY_EXISTS (http.sys prefix taken)");
            Assert.IsTrue(HttpSseTransport.IsAddressInUse(new System.AggregateException(new SocketException(10048))), "wrapped by Task.Wait");
            Assert.IsTrue(HttpSseTransport.IsAddressInUse(new HttpListenerException(500, "There's another listener for http://localhost:1/")), "Mono duplicate prefix");

            Assert.IsFalse(HttpSseTransport.IsAddressInUse(new HttpListenerException(5, "Access is denied")), "access denied must not fall back");
            Assert.IsFalse(HttpSseTransport.IsAddressInUse(new SocketException(10013)), "WSAEACCES must not fall back");
            Assert.IsFalse(HttpSseTransport.IsAddressInUse(new System.InvalidOperationException("address in use")), "unrelated exception types are not matched by text");
        }

        [Test]
        public void StartAsync_PortHeldByAnotherTransport_FallsBackToNextPort()
        {
            int port = HoldFreePort();
            m_blocker.Stop();   // free it, then take it with a real transport (the "another Unity instance" case)
            m_blocker = null;
            using (var first = new HttpSseTransport(port))
            {
                first.StartAsync().Wait();
                Assert.AreEqual(port, first.Port);

                m_transport = new HttpSseTransport(port);
                Assert.DoesNotThrow(() => m_transport.StartAsync().Wait());
                Assert.IsTrue(m_transport.IsRunning);
                Assert.Greater(m_transport.Port, port);
            }
        }
    }
}
