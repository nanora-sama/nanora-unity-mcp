using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using NanoraUnityMCP.Core;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// A main-thread timeout must say whether the operation ran: one that never started is dropped and
    /// reported as not started; one that started (a long menu item such as a player build) keeps running
    /// and is reported as still running, so the caller does not replay a mutation that is about to finish.
    /// Calls come from the thread pool, as they do from the HTTP transport.
    /// </summary>
    [TestFixture]
    public class MainThreadDispatcherTimeoutTests
    {
        static IEnumerator WaitFor(Func<bool> done, int timeoutMs)
        {
            var clock = Stopwatch.StartNew();
            while (!done() && clock.ElapsedMilliseconds < timeoutMs)
                yield return null;
        }

        static Exception Unwrap(Task task)
        {
            var ex = task.Exception?.GetBaseException();
            Assert.IsNotNull(ex, "the dispatch should have failed");
            return ex;
        }

        [UnityTest]
        public IEnumerator NotStartedBeforeTimeout_IsReportedAsNotStarted_AndNeverRuns()
        {
            int ran = 0;
            var task = Task.Run(() => MainThreadDispatcher.RunAsync(() => { Interlocked.Exchange(ref ran, 1); }, 300));

            // Hold the main thread so the queue is not processed before the timeout.
            Thread.Sleep(1000);
            try { task.Wait(3000); } catch (AggregateException) { }
            Assert.IsTrue(task.IsCompleted, "the dispatch should give up while the main thread is busy");
            var ex = Unwrap(task);
            Assert.IsInstanceOf<TimeoutException>(ex);
            StringAssert.Contains("did not start", ex.Message);
            StringAssert.Contains("will not run", ex.Message);

            // Once the main thread is free again, the abandoned operation must not run late.
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(0, ran, "an operation reported as not started must not run afterwards");
        }

        [UnityTest]
        public IEnumerator StartedButStillRunning_IsReportedAsStillRunning_AndCompletes()
        {
            int finished = 0;
            var task = Task.Run(() => MainThreadDispatcher.RunAsync(() =>
            {
                Thread.Sleep(1500);
                Interlocked.Exchange(ref finished, 1);
                return 1;
            }, 300));

            yield return WaitFor(() => task.IsCompleted && finished == 1, 5000);
            Assert.AreEqual(1, finished, "a started operation is not cancelled by the timeout");
            var ex = Unwrap(task);
            Assert.IsInstanceOf<TimeoutException>(ex);
            StringAssert.Contains("still running", ex.Message);
            StringAssert.Contains("may still complete", ex.Message);
            StringAssert.DoesNotContain("Try focusing", ex.Message);
        }

        [UnityTest]
        public IEnumerator AsyncOperationStillRunning_IsReportedAsStillRunning()
        {
            var task = Task.Run(() => MainThreadDispatcher.RunAsync<int>(async () =>
            {
                await Task.Delay(1500);
                return 1;
            }, 300));

            yield return WaitFor(() => task.IsCompleted, 5000);
            var ex = Unwrap(task);
            Assert.IsInstanceOf<TimeoutException>(ex);
            StringAssert.Contains("still running", ex.Message);
        }

        [UnityTest]
        public IEnumerator CompletedWithinTimeout_ReturnsTheResult()
        {
            var task = Task.Run(() => MainThreadDispatcher.RunAsync(() => 42, 5000));
            yield return WaitFor(() => task.IsCompleted, 5000);
            Assert.IsTrue(task.Status == TaskStatus.RanToCompletion, task.Exception?.GetBaseException().Message);
            Assert.AreEqual(42, task.Result);
        }
    }
}
