using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Runs Unity Test Framework tests programmatically.
    /// Integrates with TestRunner API for EditMode and PlayMode tests.
    /// </summary>
    [McpTool("run_tests")]
    public class RunTestsTool : IMcpTool, ICallbacks
    {
        private const int k_DefaultTimeoutSeconds = 300;

        public string Name => "run_tests";
        public string Description => "Run Unity Test Framework tests. " +
            "Supports EditMode and PlayMode tests with filtering options.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""test_mode"": {
                    ""type"": ""string"",
                    ""description"": ""Test mode to run"",
                    ""enum"": [""EditMode"", ""PlayMode"", ""Both""],
                    ""default"": ""Both""
                },
                ""filter"": {
                    ""type"": ""string"",
                    ""description"": ""Filter tests by name (supports wildcards)"",
                    ""default"": """"
                },
                ""category"": {
                    ""type"": ""string"",
                    ""description"": ""Filter tests by category"",
                    ""default"": """"
                },
                ""assembly_filter"": {
                    ""type"": ""string"",
                    ""description"": ""Filter by assembly name"",
                    ""default"": """"
                },
                ""run_synchronously"": {
                    ""type"": ""boolean"",
                    ""description"": ""Wait for tests to complete before returning"",
                    ""default"": true
                },
                ""timeout_seconds"": {
                    ""type"": ""integer"",
                    ""description"": ""Maximum time to wait for tests (sync mode only)"",
                    ""default"": 300
                }
            }
        }");

        // Test execution state
        private TaskCompletionSource<TestRunResult> m_completionSource;
        private TestRunResult m_currentResult;
        private bool m_isRunning;

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(async () =>
            {
                string testModeStr = arguments?["test_mode"]?.Value<string>() ?? "Both";
                string filter = arguments?["filter"]?.Value<string>() ?? "";
                string category = arguments?["category"]?.Value<string>() ?? "";
                string assemblyFilter = arguments?["assembly_filter"]?.Value<string>() ?? "";
                bool runSync = arguments?["run_synchronously"]?.Value<bool>() ?? true;
                int timeoutSeconds = arguments?["timeout_seconds"]?.Value<int>() ?? k_DefaultTimeoutSeconds;

                if (m_isRunning)
                {
                    return McpToolResult.Error("Tests are already running. Wait for completion or restart Unity.");
                }

                try
                {
                    var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                    api.RegisterCallbacks(this);

                    // Build filter
                    var testFilter = new Filter();

                    // Set test mode
                    switch (testModeStr.ToLower())
                    {
                        case "editmode":
                            testFilter.testMode = TestMode.EditMode;
                            break;
                        case "playmode":
                            testFilter.testMode = TestMode.PlayMode;
                            break;
                        default:
                            testFilter.testMode = TestMode.EditMode | TestMode.PlayMode;
                            break;
                    }

                    // Apply name filter
                    if (!string.IsNullOrEmpty(filter))
                    {
                        testFilter.testNames = new[] { filter };
                    }

                    // Apply category filter
                    if (!string.IsNullOrEmpty(category))
                    {
                        testFilter.categoryNames = new[] { category };
                    }

                    // Apply assembly filter
                    if (!string.IsNullOrEmpty(assemblyFilter))
                    {
                        testFilter.assemblyNames = new[] { assemblyFilter };
                    }

                    // Initialize result tracking
                    m_currentResult = new TestRunResult();
                    m_isRunning = true;

                    if (runSync)
                    {
                        m_completionSource = new TaskCompletionSource<TestRunResult>();

                        // Execute tests
                        api.Execute(new ExecutionSettings(testFilter));

                        // Wait for completion with timeout
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                        var completedTask = await Task.WhenAny(m_completionSource.Task, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            m_isRunning = false;
                            return McpToolResult.Error($"Test execution timed out after {timeoutSeconds} seconds.");
                        }

                        var result = await m_completionSource.Task;
                        return McpToolResult.Success(FormatTestResult(result));
                    }
                    else
                    {
                        // Async mode - start tests and return immediately
                        api.Execute(new ExecutionSettings(testFilter));
                        return McpToolResult.Success("Tests started. Use get_test_results to check progress.");
                    }
                }
                catch (Exception ex)
                {
                    m_isRunning = false;
                    return McpToolResult.Error($"Failed to run tests: {ex.Message}");
                }
            });
        }

        #region ICallbacks Implementation

        public void RunStarted(ITestAdaptor testsToRun)
        {
            m_currentResult.TotalTests = CountTests(testsToRun);
            m_currentResult.StartTime = DateTime.Now;
            Debug.Log($"[UnityMCP] Test run started: {m_currentResult.TotalTests} tests");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            m_currentResult.EndTime = DateTime.Now;
            m_currentResult.OverallResult = result.TestStatus.ToString();
            m_currentResult.Duration = result.Duration;
            m_isRunning = false;

            Debug.Log($"[UnityMCP] Test run finished: {m_currentResult.PassedTests} passed, " +
                $"{m_currentResult.FailedTests} failed, {m_currentResult.SkippedTests} skipped");

            m_completionSource?.TrySetResult(m_currentResult);
        }

        public void TestStarted(ITestAdaptor test)
        {
            // Individual test started
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                var testResult = new SingleTestResult
                {
                    Name = result.Name,
                    FullName = result.FullName,
                    Status = result.TestStatus.ToString(),
                    Duration = result.Duration,
                    Message = result.Message,
                    StackTrace = result.StackTrace
                };

                m_currentResult.TestResults.Add(testResult);

                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        m_currentResult.PassedTests++;
                        break;
                    case TestStatus.Failed:
                        m_currentResult.FailedTests++;
                        break;
                    case TestStatus.Skipped:
                        m_currentResult.SkippedTests++;
                        break;
                    case TestStatus.Inconclusive:
                        m_currentResult.InconclusiveTests++;
                        break;
                }
            }
        }

        #endregion

        private int CountTests(ITestAdaptor test)
        {
            if (!test.HasChildren)
            {
                return test.IsSuite ? 0 : 1;
            }

            int count = 0;
            foreach (var child in test.Children)
            {
                count += CountTests(child);
            }
            return count;
        }

        private string FormatTestResult(TestRunResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Test Run Report");
            sb.AppendLine();

            // Summary
            sb.AppendLine("## Summary");
            sb.AppendLine($"- Overall Result: **{result.OverallResult}**");
            sb.AppendLine($"- Total Tests: {result.TotalTests}");
            sb.AppendLine($"- Passed: {result.PassedTests}");
            sb.AppendLine($"- Failed: {result.FailedTests}");
            sb.AppendLine($"- Skipped: {result.SkippedTests}");
            sb.AppendLine($"- Inconclusive: {result.InconclusiveTests}");
            sb.AppendLine($"- Duration: {result.Duration:F2}s");
            sb.AppendLine();

            // Failed tests details
            var failedTests = result.TestResults.Where(t => t.Status == "Failed").ToList();
            if (failedTests.Count > 0)
            {
                sb.AppendLine("## Failed Tests");
                foreach (var test in failedTests)
                {
                    sb.AppendLine($"### {test.Name}");
                    sb.AppendLine($"- Full Name: `{test.FullName}`");
                    sb.AppendLine($"- Duration: {test.Duration:F3}s");
                    if (!string.IsNullOrEmpty(test.Message))
                    {
                        sb.AppendLine($"- Message: {test.Message}");
                    }
                    if (!string.IsNullOrEmpty(test.StackTrace))
                    {
                        sb.AppendLine("- Stack Trace:");
                        sb.AppendLine("```");
                        sb.AppendLine(test.StackTrace);
                        sb.AppendLine("```");
                    }
                    sb.AppendLine();
                }
            }

            // Passed tests (abbreviated)
            var passedTests = result.TestResults.Where(t => t.Status == "Passed").ToList();
            if (passedTests.Count > 0)
            {
                sb.AppendLine("## Passed Tests");
                foreach (var test in passedTests.Take(20))
                {
                    sb.AppendLine($"- {test.Name} ({test.Duration:F3}s)");
                }
                if (passedTests.Count > 20)
                {
                    sb.AppendLine($"- ... and {passedTests.Count - 20} more");
                }
                sb.AppendLine();
            }

            // Skipped tests
            var skippedTests = result.TestResults.Where(t => t.Status == "Skipped").ToList();
            if (skippedTests.Count > 0)
            {
                sb.AppendLine("## Skipped Tests");
                foreach (var test in skippedTests)
                {
                    sb.AppendLine($"- {test.Name}: {test.Message ?? "(no reason)"}");
                }
            }

            return sb.ToString();
        }

        private class TestRunResult
        {
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public int SkippedTests { get; set; }
            public int InconclusiveTests { get; set; }
            public string OverallResult { get; set; }
            public double Duration { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public List<SingleTestResult> TestResults { get; } = new List<SingleTestResult>();
        }

        private class SingleTestResult
        {
            public string Name { get; set; }
            public string FullName { get; set; }
            public string Status { get; set; }
            public double Duration { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
        }
    }

    /// <summary>
    /// Gets test results from a running or completed test run.
    /// </summary>
    [McpTool("get_test_results")]
    public class GetTestResultsTool : IMcpTool
    {
        public string Name => "get_test_results";
        public string Description => "Get available test results from Unity Test Framework.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""include_all_tests"": {
                    ""type"": ""boolean"",
                    ""description"": ""Include all test definitions, not just results"",
                    ""default"": false
                }
            }
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                bool includeAll = arguments?["include_all_tests"]?.Value<bool>() ?? false;

                try
                {
                    var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                    var sb = new StringBuilder();
                    sb.AppendLine("# Available Tests");
                    sb.AppendLine();

                    // Get EditMode tests
                    sb.AppendLine("## EditMode Tests");
                    api.RetrieveTestList(TestMode.EditMode, (testRoot) =>
                    {
                        ListTests(sb, testRoot, 0);
                    });

                    sb.AppendLine();
                    sb.AppendLine("## PlayMode Tests");
                    api.RetrieveTestList(TestMode.PlayMode, (testRoot) =>
                    {
                        ListTests(sb, testRoot, 0);
                    });

                    return McpToolResult.Success(sb.ToString());
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Failed to get test results: {ex.Message}");
                }
            });
        }

        private void ListTests(StringBuilder sb, ITestAdaptor test, int depth)
        {
            if (test == null) return;

            string indent = new string(' ', depth * 2);

            if (!test.IsSuite)
            {
                sb.AppendLine($"{indent}- {test.Name}");
            }
            else if (test.HasChildren)
            {
                if (depth > 0)
                {
                    sb.AppendLine($"{indent}**{test.Name}**");
                }

                foreach (var child in test.Children)
                {
                    ListTests(sb, child, depth + 1);
                }
            }
        }
    }
}
