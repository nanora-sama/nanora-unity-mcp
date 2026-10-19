using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using NanoraUnityMCP.Transport;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// compile_project (60 s by default), manage_playmode wait_for_ready (up to 120 s) and run_tests (300 s by default)
    /// are designed to wait longer than 30 s. The HTTP /message endpoint must keep the request open for as long as the
    /// tool was asked to wait, instead of answering 504 at 30 s and discarding the result the tool produces later.
    /// </summary>
    [TestFixture]
    public class TransportRequestTimeoutTests
    {
        private HttpSseTransport m_transport;
        private int m_nonTool, m_defaultTool, m_grace;

        [SetUp]
        public void SetUp()
        {
            m_nonTool = HttpSseTransport.NonToolRequestTimeoutMs;
            m_defaultTool = HttpSseTransport.DefaultToolRequestTimeoutMs;
            m_grace = HttpSseTransport.ToolTimeoutGraceMs;
        }

        [TearDown]
        public void TearDown()
        {
            m_transport?.Dispose();
            m_transport = null;
            HttpSseTransport.NonToolRequestTimeoutMs = m_nonTool;
            HttpSseTransport.DefaultToolRequestTimeoutMs = m_defaultTool;
            HttpSseTransport.ToolTimeoutGraceMs = m_grace;
        }

        // Shortened limits so the timeout paths run in seconds.
        private static void UseShortLimits()
        {
            HttpSseTransport.NonToolRequestTimeoutMs = 800;
            HttpSseTransport.DefaultToolRequestTimeoutMs = 1000;
            HttpSseTransport.ToolTimeoutGraceMs = 500;
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        // Starts a transport whose handler answers each request after the given delay (never, when null).
        private void StartTransport(int? respondAfterMs)
        {
            m_transport = new HttpSseTransport(FreePort());
            m_transport.OnMessageReceived += (body, respond) =>
            {
                if (respondAfterMs == null) return;
                Task.Run(async () =>
                {
                    await Task.Delay(respondAfterMs.Value);
                    respond("{\"jsonrpc\":\"2.0\",\"id\":7,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"compiled\"}]}}");
                });
            };
            m_transport.StartAsync().Wait();
        }

        private (int status, string body) Post(string json, int clientTimeoutMs)
        {
            var request = (HttpWebRequest)WebRequest.Create($"http://127.0.0.1:{m_transport.Port}/message");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = clientTimeoutMs;
            request.ReadWriteTimeout = clientTimeoutMs;
            var bytes = Encoding.UTF8.GetBytes(json);
            using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    return ((int)response.StatusCode, reader.ReadToEnd());
            }
            catch (WebException e) when (e.Response is HttpWebResponse error)
            {
                using (var reader = new StreamReader(error.GetResponseStream()))
                    return ((int)error.StatusCode, reader.ReadToEnd());
            }
        }

        private const string k_CompileCall =
            "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"tools/call\",\"params\":{\"name\":\"compile_project\",\"arguments\":{\"timeout_seconds\":60}}}";

        // The case that failed in practice: a compile that finishes after 31 s within its own 60 s budget.
        [Test]
        [Timeout(90000)]
        public void ToolCall_AnsweredAfter31Seconds_WithinToolTimeout_ReturnsTheResult()
        {
            StartTransport(respondAfterMs: 31000);

            var (status, body) = Post(k_CompileCall, clientTimeoutMs: 80000);

            Assert.AreEqual(200, status, "the transport must not give up before the tool's own timeout: " + body);
            StringAssert.Contains("compiled", body);
        }

        [Test]
        public void GetRequestTimeoutMs_FollowsTheToolsOwnTimeout()
        {
            string Call(string arguments) =>
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"t\",\"arguments\":" + arguments + "}}";

            Assert.AreEqual(330000, HttpSseTransport.GetRequestTimeoutMs(Call("{}")), "no timeout argument: longest tool default + grace");
            Assert.AreEqual(330000, HttpSseTransport.GetRequestTimeoutMs(Call("{\"timeout_seconds\":120}")), "shorter than the default");
            Assert.AreEqual(630000, HttpSseTransport.GetRequestTimeoutMs(Call("{\"timeout_seconds\":600}")), "timeout_seconds beyond the default");
            Assert.AreEqual(430000, HttpSseTransport.GetRequestTimeoutMs(Call("{\"timeout_ms\":400000}")), "timeout_ms beyond the default");
            Assert.AreEqual(3600000, HttpSseTransport.GetRequestTimeoutMs(Call("{\"timeout_seconds\":999999}")), "capped at one hour");
            Assert.AreEqual(330000, HttpSseTransport.GetRequestTimeoutMs(Call("{\"timeout_seconds\":\"600\"}")), "non-numeric values are ignored");
            Assert.AreEqual(30000, HttpSseTransport.GetRequestTimeoutMs("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"), "other methods");
            Assert.AreEqual(30000, HttpSseTransport.GetRequestTimeoutMs("not json"), "unparsable bodies");
        }

        [Test]
        [Timeout(30000)]
        public void ToolCall_NeverAnswered_Returns504WithJsonRpcErrorForTheRequest()
        {
            UseShortLimits();
            StartTransport(respondAfterMs: null);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var (status, body) = Post(k_CompileCall.Replace("\"timeout_seconds\":60", "\"timeout_seconds\":1"), clientTimeoutMs: 20000);
            watch.Stop();

            Assert.AreEqual(504, status);
            var error = Newtonsoft.Json.Linq.JObject.Parse(body);
            Assert.AreEqual(7, (int)error["id"], "the error must carry the request id");
            Assert.AreEqual(-32001, (int)error["error"]["code"]);
            StringAssert.Contains("before retrying a change", (string)error["error"]["message"]);
            Assert.GreaterOrEqual(watch.ElapsedMilliseconds, 1400, "waits the default tool limit plus the grace (1.5 s here)");
        }

        [Test]
        [Timeout(30000)]
        public void ToolCall_AnsweredWithinItsTimeoutArgument_ReturnsTheResult()
        {
            UseShortLimits();
            StartTransport(respondAfterMs: 2500);

            // 3 s requested + 0.5 s grace: the 2.5 s answer must arrive although the default limit is 1 s.
            var (status, body) = Post(k_CompileCall.Replace("\"timeout_seconds\":60", "\"timeout_seconds\":3"), clientTimeoutMs: 20000);

            Assert.AreEqual(200, status, body);
            StringAssert.Contains("compiled", body);
        }

        [Test]
        [Timeout(30000)]
        public void NonToolRequest_NeverAnswered_TimesOutAtTheShortLimit()
        {
            UseShortLimits();
            StartTransport(respondAfterMs: null);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var (status, _) = Post("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\"}", clientTimeoutMs: 20000);
            watch.Stop();

            Assert.AreEqual(504, status);
            Assert.Less(watch.ElapsedMilliseconds, 1400, "tools/list is not extended to the tool limit");
        }

        // Entering Play Mode stops the server before the domain reload; the cut-off reply must not claim a timeout.
        [Test]
        public void InterruptedResponse_NamesTheDomainReload_AndCarriesTheRequestId()
        {
            var error = Newtonsoft.Json.Linq.JObject.Parse(HttpSseTransport.BuildInterruptedResponse(k_CompileCall));

            Assert.AreEqual(7, (int)error["id"]);
            Assert.AreEqual(-32001, (int)error["error"]["code"]);
            var message = (string)error["error"]["message"];
            StringAssert.Contains("domain reload", message);
            StringAssert.Contains("before retrying a change", message);
            StringAssert.DoesNotContain("within", message);
        }
    }
}
