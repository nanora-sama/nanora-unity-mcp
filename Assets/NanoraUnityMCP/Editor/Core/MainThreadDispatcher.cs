using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace NanoraUnityMCP.Core
{
    /// <summary>
    /// Dispatches actions to the Unity main thread from background threads.
    /// Essential for calling Unity APIs from HttpListener callbacks.
    /// Uses QueuePlayerLoopUpdate to keep the editor responsive even when
    /// Unity is in the background on macOS.
    /// </summary>
    [InitializeOnLoad]
    public static class MainThreadDispatcher
    {
        private const int k_DefaultTimeoutMs = 30000; // 30 seconds default timeout
        private const int k_KeepAliveIntervalMs = 200; // Background tick interval

        // Whether a dispatched operation reached the main thread. A timeout before the start drops the
        // operation; a timeout after it cannot stop it, so the caller is told it may still complete.
        private sealed class Dispatch
        {
            private const int k_Queued = 0, k_Started = 1, k_Abandoned = 2;
            private readonly System.Diagnostics.Stopwatch m_Clock = System.Diagnostics.Stopwatch.StartNew();
            private int m_State = k_Queued;
            private long m_StartedAfterMs;

            public bool TryStart()
            {
                if (Interlocked.CompareExchange(ref m_State, k_Started, k_Queued) != k_Queued) return false;
                Interlocked.Exchange(ref m_StartedAfterMs, m_Clock.ElapsedMilliseconds);
                return true;
            }

            public TimeoutException Timeout(int timeoutMs)
            {
                if (Interlocked.CompareExchange(ref m_State, k_Abandoned, k_Queued) == k_Queued)
                    return new TimeoutException($"Main thread operation did not start within {timeoutMs}ms: the Unity Editor main thread is busy or throttled (a long operation, a modal dialog, a domain reload, or the Editor in the background). It was cancelled and will not run. Try focusing the Unity Editor window, then retry.");
                return StillRunning(timeoutMs);
            }

            public TimeoutException StillRunning(int timeoutMs) =>
                new TimeoutException($"Main thread operation started after {Interlocked.Read(ref m_StartedAfterMs)}ms and was still running at the {timeoutMs}ms limit. It was not cancelled and may still complete; check the Editor state before retrying a mutation.");
        }

        private static readonly ConcurrentQueue<Action> s_Queue = new ConcurrentQueue<Action>();
        private static volatile bool s_IsMainThread = false;
        private static Timer s_KeepAliveTimer;

        static MainThreadDispatcher()
        {
            s_IsMainThread = true;
            EditorApplication.update += ProcessQueue;
            StartKeepAliveTimer();
        }

        /// <summary>
        /// Enqueues an action to be executed on the main thread.
        /// Also forces the editor to update so requests are processed promptly
        /// even when Unity is in the background.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            s_Queue.Enqueue(action);
            ForceEditorUpdate();
        }

        /// <summary>
        /// Forces the Unity Editor to process a loop update.
        /// This is critical for macOS where the editor throttles updates
        /// when not in focus, causing MCP requests to time out.
        /// </summary>
        private static void ForceEditorUpdate()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch
            {
                // QueuePlayerLoopUpdate may fail during domain reload; ignore
            }
        }

        /// <summary>
        /// Background timer that periodically forces editor updates when
        /// there are pending items in the queue. Prevents MCP timeout
        /// when Unity is in background on macOS.
        /// </summary>
        private static void StartKeepAliveTimer()
        {
            s_KeepAliveTimer?.Dispose();
            s_KeepAliveTimer = new Timer(_ =>
            {
                if (!s_Queue.IsEmpty)
                {
                    ForceEditorUpdate();
                }
            }, null, k_KeepAliveIntervalMs, k_KeepAliveIntervalMs);
        }

        /// <summary>
        /// Runs a function on the main thread and returns the result asynchronously.
        /// Includes timeout handling to prevent indefinite hanging.
        /// </summary>
        public static async Task<T> RunAsync<T>(Func<T> func, int timeoutMs = k_DefaultTimeoutMs)
        {
            var tcs = new TaskCompletionSource<T>();
            var dispatch = new Dispatch();

            Enqueue(() =>
            {
                if (!dispatch.TryStart()) return;
                try
                {
                    var result = func();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));
                if (completedTask == tcs.Task)
                {
                    cts.Cancel(); // Cancel the delay task
                    return await tcs.Task;
                }
                else
                {
                    throw dispatch.Timeout(timeoutMs);
                }
            }
        }

        /// <summary>
        /// Runs an async function on the main thread and returns the result asynchronously.
        /// Includes timeout handling to prevent indefinite hanging.
        /// </summary>
        public static async Task<T> RunAsync<T>(Func<Task<T>> asyncFunc, int timeoutMs = k_DefaultTimeoutMs)
        {
            var tcs = new TaskCompletionSource<Task<T>>();
            var dispatch = new Dispatch();

            Enqueue(() =>
            {
                if (!dispatch.TryStart()) return;
                try
                {
                    var task = asyncFunc();
                    tcs.TrySetResult(task);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));
                if (completedTask == tcs.Task)
                {
                    cts.Cancel();
                    var innerTask = await tcs.Task;
                    // Also apply timeout to inner task
                    var innerCompleted = await Task.WhenAny(innerTask, Task.Delay(timeoutMs, CancellationToken.None));
                    if (innerCompleted == innerTask)
                    {
                        return await innerTask;
                    }
                    throw dispatch.StillRunning(timeoutMs);
                }
                else
                {
                    throw dispatch.Timeout(timeoutMs);
                }
            }
        }

        /// <summary>
        /// Runs an action on the main thread asynchronously.
        /// Includes timeout handling to prevent indefinite hanging.
        /// </summary>
        public static async Task RunAsync(Action action, int timeoutMs = k_DefaultTimeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>();
            var dispatch = new Dispatch();

            Enqueue(() =>
            {
                if (!dispatch.TryStart()) return;
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, cts.Token));
                if (completedTask == tcs.Task)
                {
                    cts.Cancel();
                    await tcs.Task; // Propagate any exceptions
                }
                else
                {
                    throw dispatch.Timeout(timeoutMs);
                }
            }
        }

        private static void ProcessQueue()
        {
            while (s_Queue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }
    }
}
