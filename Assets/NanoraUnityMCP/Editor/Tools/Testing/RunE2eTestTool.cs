using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Runs an E2E test scenario in Play Mode.
    /// Executes a sequence of steps: input simulation, assertions, waits, and screenshots.
    /// Delegates to existing tools via ToolRegistry for each step type.
    /// </summary>
    [McpTool("run_e2e_test")]
    public class RunE2eTestTool : IMcpTool
    {
        private ToolRegistry m_registry;

        public string Name => "run_e2e_test";
        public string Description => "Run an E2E test scenario in Play Mode. " +
            "Executes a sequence of steps: input simulation, assertions, waits, and screenshots.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""name"": {
                    ""type"": ""string"",
                    ""description"": ""Test name for reporting""
                },
                ""steps"": {
                    ""type"": ""array"",
                    ""description"": ""Ordered list of test steps"",
                    ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""action"": {
                                ""type"": ""string"",
                                ""enum"": [""wait_for_selector"", ""simulate_input"", ""assert"", ""screenshot"", ""wait"", ""execute_csharp""],
                                ""description"": ""Step action type""
                            },
                            ""selector"": { ""type"": ""string"" },
                            ""key"": { ""type"": ""string"" },
                            ""input_action"": { ""type"": ""string"", ""description"": ""simulate_input action type (key_tap, ui_click, etc.)"" },
                            ""property"": { ""type"": ""string"" },
                            ""expected"": {},
                            ""comparison"": { ""type"": ""string"", ""default"": ""equals"" },
                            ""label"": { ""type"": ""string"" },
                            ""ms"": { ""type"": ""integer"" },
                            ""code"": { ""type"": ""string"" },
                            ""timeout"": { ""type"": ""number"", ""default"": 5 }
                        },
                        ""required"": [""action""]
                    }
                },
                ""auto_enter_playmode"": {
                    ""type"": ""boolean"",
                    ""description"": ""Automatically enter Play Mode before test (default: true)"",
                    ""default"": true
                },
                ""auto_exit_playmode"": {
                    ""type"": ""boolean"",
                    ""description"": ""Automatically exit Play Mode after test (default: true)"",
                    ""default"": true
                },
                ""screenshot_on_failure"": {
                    ""type"": ""boolean"",
                    ""description"": ""Automatically capture screenshot on test failure (default: true)"",
                    ""default"": true
                },
                ""timeout"": {
                    ""type"": ""number"",
                    ""description"": ""Overall test timeout in seconds (default: 60)"",
                    ""default"": 60
                }
            },
            ""required"": [""name"", ""steps""]
        }");

        /// <summary>
        /// Sets the tool registry for delegating to other tools.
        /// Called by ToolRegistry after instantiation.
        /// </summary>
        public void SetRegistry(ToolRegistry registry)
        {
            m_registry = registry;
        }

        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            string testName = arguments?["name"]?.Value<string>();
            var stepsArray = arguments?["steps"] as JArray;
            bool autoEnter = arguments?["auto_enter_playmode"]?.Value<bool>() ?? true;
            bool autoExit = arguments?["auto_exit_playmode"]?.Value<bool>() ?? true;
            bool screenshotOnFailure = arguments?["screenshot_on_failure"]?.Value<bool>() ?? true;
            double overallTimeout = arguments?["timeout"]?.Value<double>() ?? 60.0;

            if (string.IsNullOrWhiteSpace(testName))
            {
                return McpToolResult.Error("Test name is required");
            }

            if (stepsArray == null || stepsArray.Count == 0)
            {
                return McpToolResult.Error("At least one test step is required");
            }

            // Parse steps
            var steps = new List<E2eTestStep>();
            for (int i = 0; i < stepsArray.Count; i++)
            {
                var stepObj = stepsArray[i] as JObject;
                if (stepObj == null)
                {
                    return McpToolResult.Error($"Step {i + 1} is not a valid object");
                }

                var action = stepObj["action"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error($"Step {i + 1} is missing required 'action' field");
                }

                steps.Add(new E2eTestStep
                {
                    Index = i + 1,
                    Action = action,
                    Arguments = stepObj
                });
            }

            var results = new List<E2eStepResult>();
            var overallStopwatch = Stopwatch.StartNew();
            bool testPassed = true;
            string failureScreenshot = null;

            try
            {
                // Step 1: Enter Play Mode
                if (autoEnter)
                {
                    var playModeResult = await DelegateToTool("manage_playmode", new JObject
                    {
                        ["action"] = "play",
                        ["wait_for_ready"] = true,
                        ["timeout_seconds"] = Math.Min((int)overallTimeout, 30)
                    });

                    if (playModeResult.IsError)
                    {
                        return McpToolResult.Error(
                            $"Failed to enter Play Mode: {GetResultText(playModeResult)}");
                    }
                }

                // Step 2: Execute each step
                for (int i = 0; i < steps.Count; i++)
                {
                    // Check overall timeout
                    if (overallStopwatch.Elapsed.TotalSeconds > overallTimeout)
                    {
                        results.Add(new E2eStepResult
                        {
                            Step = steps[i],
                            Passed = false,
                            ElapsedMs = 0,
                            Message = "Overall test timeout exceeded"
                        });
                        testPassed = false;
                        break;
                    }

                    var stepResult = await ExecuteStep(steps[i]);
                    results.Add(stepResult);

                    if (!stepResult.Passed)
                    {
                        testPassed = false;

                        // Capture failure screenshot
                        if (screenshotOnFailure)
                        {
                            failureScreenshot = await CaptureFailureScreenshot();
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                testPassed = false;
                results.Add(new E2eStepResult
                {
                    Step = results.Count < steps.Count ? steps[results.Count] : null,
                    Passed = false,
                    ElapsedMs = 0,
                    Message = $"Unexpected error: {ex.Message}"
                });
            }
            finally
            {
                // Step 4: Exit Play Mode
                if (autoExit)
                {
                    try
                    {
                        await DelegateToTool("manage_playmode", new JObject
                        {
                            ["action"] = "stop"
                        });
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[E2eTest] Failed to exit Play Mode: {ex.Message}");
                    }
                }
            }

            overallStopwatch.Stop();

            // Step 5: Build result report
            return BuildReport(testName, testPassed, results, steps.Count,
                overallStopwatch.ElapsedMilliseconds, failureScreenshot);
        }

        /// <summary>
        /// Executes a single test step by delegating to the appropriate tool.
        /// </summary>
        private async Task<E2eStepResult> ExecuteStep(E2eTestStep step)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                McpToolResult result;

                switch (step.Action)
                {
                    case "wait_for_selector":
                        result = await ExecuteWaitForSelector(step);
                        break;

                    case "simulate_input":
                        result = await ExecuteSimulateInput(step);
                        break;

                    case "assert":
                        result = await ExecuteAssert(step);
                        break;

                    case "screenshot":
                        result = await ExecuteScreenshot(step);
                        break;

                    case "wait":
                        result = await ExecuteWait(step);
                        break;

                    case "execute_csharp":
                        result = await ExecuteCSharp(step);
                        break;

                    default:
                        result = McpToolResult.Error($"Unknown action: {step.Action}");
                        break;
                }

                sw.Stop();

                return new E2eStepResult
                {
                    Step = step,
                    Passed = !result.IsError,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Message = GetResultText(result),
                    ToolResult = result
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new E2eStepResult
                {
                    Step = step,
                    Passed = false,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Message = $"Exception: {ex.Message}"
                };
            }
        }

        private async Task<McpToolResult> ExecuteWaitForSelector(E2eTestStep step)
        {
            var args = new JObject
            {
                ["selector"] = step.Arguments["selector"]?.Value<string>(),
                ["timeout"] = step.Arguments["timeout"]?.Value<float>() ?? 5f
            };

            var state = step.Arguments["state"]?.Value<string>();
            if (!string.IsNullOrEmpty(state))
            {
                args["state"] = state;
            }

            return await DelegateToTool("wait_for_selector", args);
        }

        private async Task<McpToolResult> ExecuteSimulateInput(E2eTestStep step)
        {
            var args = new JObject
            {
                ["action"] = step.Arguments["input_action"]?.Value<string>() ?? "key_tap"
            };

            var key = step.Arguments["key"]?.Value<string>();
            if (!string.IsNullOrEmpty(key))
            {
                args["key"] = key;
            }

            var selector = step.Arguments["selector"]?.Value<string>();
            if (!string.IsNullOrEmpty(selector))
            {
                args["selector"] = selector;
            }

            var position = step.Arguments["position"];
            if (position != null)
            {
                args["position"] = position;
            }

            var button = step.Arguments["button"];
            if (button != null)
            {
                args["button"] = button;
            }

            return await DelegateToTool("simulate_input", args);
        }

        private async Task<McpToolResult> ExecuteAssert(E2eTestStep step)
        {
            var args = new JObject
            {
                ["selector"] = step.Arguments["selector"]?.Value<string>(),
                ["property"] = step.Arguments["property"]?.Value<string>(),
                ["expected"] = step.Arguments["expected"],
                ["comparison"] = step.Arguments["comparison"]?.Value<string>() ?? "equals",
                ["timeout"] = step.Arguments["timeout"]?.Value<float>() ?? 5f
            };

            return await DelegateToTool("assert_ui_state", args);
        }

        private async Task<McpToolResult> ExecuteScreenshot(E2eTestStep step)
        {
            var args = new JObject();

            var label = step.Arguments["label"]?.Value<string>();
            if (!string.IsNullOrEmpty(label))
            {
                args["save_to_file"] = $"Screenshots/e2e_{label}.png";
            }

            return await DelegateToTool("capture_screenshot", args);
        }

        private async Task<McpToolResult> ExecuteWait(E2eTestStep step)
        {
            int ms = step.Arguments["ms"]?.Value<int>() ?? 1000;
            await Task.Delay(ms);
            return McpToolResult.Success($"Waited {ms}ms");
        }

        private async Task<McpToolResult> ExecuteCSharp(E2eTestStep step)
        {
            var code = step.Arguments["code"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(code))
            {
                return McpToolResult.Error("execute_csharp step requires 'code' field");
            }

            var args = new JObject
            {
                ["code"] = code
            };

            var timeoutToken = step.Arguments["timeout"];
            if (timeoutToken != null)
            {
                args["timeout_ms"] = (int)(timeoutToken.Value<float>() * 1000);
            }

            return await DelegateToTool("execute_csharp", args);
        }

        /// <summary>
        /// Delegates execution to a registered tool by name.
        /// </summary>
        private async Task<McpToolResult> DelegateToTool(string toolName, JObject args)
        {
            if (m_registry == null)
            {
                return McpToolResult.Error(
                    $"ToolRegistry not available. Cannot delegate to '{toolName}'. " +
                    "Ensure RunE2eTestTool is registered via ToolRegistry.");
            }

            var tool = m_registry.GetTool(toolName);
            if (tool == null)
            {
                return McpToolResult.Error($"Tool '{toolName}' not found in registry");
            }

            return await tool.ExecuteAsync(args);
        }

        /// <summary>
        /// Captures a screenshot on test failure.
        /// </summary>
        private async Task<string> CaptureFailureScreenshot()
        {
            try
            {
                var result = await DelegateToTool("capture_screenshot", new JObject
                {
                    ["save_to_file"] = $"Screenshots/e2e_failure_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                });

                if (!result.IsError && result.Content.Count > 0)
                {
                    foreach (var content in result.Content)
                    {
                        if (content.Type == "image" && !string.IsNullOrEmpty(content.Data))
                        {
                            return content.Data;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[E2eTest] Failed to capture failure screenshot: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Builds the structured test result report.
        /// </summary>
        private McpToolResult BuildReport(string testName, bool passed, List<E2eStepResult> results,
            int totalSteps, long elapsedMs, string failureScreenshotBase64)
        {
            var sb = new StringBuilder();
            int passedCount = 0;

            sb.AppendLine($"=== E2E Test Result: {testName} ===");
            sb.AppendLine($"Status: {(passed ? "PASSED" : "FAILED")}");
            sb.AppendLine($"Duration: {elapsedMs}ms");

            foreach (var r in results)
            {
                if (r.Passed) passedCount++;
            }

            sb.AppendLine($"Steps: {passedCount}/{totalSteps}");
            sb.AppendLine();

            // Per-step results
            foreach (var r in results)
            {
                string stepDesc = FormatStepDescription(r.Step);
                string status = r.Passed ? "PASSED" : "FAILED";
                sb.AppendLine($"Step {r.Step.Index}: {stepDesc} -- {status} ({r.ElapsedMs}ms)");
            }

            // Failure details
            foreach (var r in results)
            {
                if (!r.Passed)
                {
                    sb.AppendLine();
                    sb.AppendLine($"[FAILURE] Step {r.Step.Index}: {FormatStepDescription(r.Step)} -- FAILED");

                    if (r.Step.Action == "assert")
                    {
                        var expected = r.Step.Arguments["expected"];
                        var comparison = r.Step.Arguments["comparison"]?.Value<string>() ?? "equals";
                        sb.AppendLine($"  Expected: {comparison} {expected}");
                    }

                    if (!string.IsNullOrEmpty(r.Message))
                    {
                        sb.AppendLine($"  Detail: {r.Message}");
                    }
                }
            }

            // Build content list
            var contentList = new List<McpContent>
            {
                McpContent.TextContent(sb.ToString())
            };

            // Attach failure screenshot if available
            if (!string.IsNullOrEmpty(failureScreenshotBase64))
            {
                contentList.Add(new McpContent
                {
                    Type = "image",
                    Data = failureScreenshotBase64,
                    MimeType = "image/png"
                });
            }

            return new McpToolResult
            {
                Content = contentList,
                IsError = !passed
            };
        }

        /// <summary>
        /// Formats a human-readable step description for the report.
        /// </summary>
        internal static string FormatStepDescription(E2eTestStep step)
        {
            if (step == null) return "(unknown step)";

            switch (step.Action)
            {
                case "wait_for_selector":
                    return $"wait_for_selector(\"{step.Arguments["selector"]}\")";

                case "simulate_input":
                    var inputAction = step.Arguments["input_action"]?.Value<string>() ?? "key_tap";
                    var key = step.Arguments["key"]?.Value<string>();
                    var sel = step.Arguments["selector"]?.Value<string>();
                    if (!string.IsNullOrEmpty(key))
                        return $"simulate_input({inputAction}: {key})";
                    if (!string.IsNullOrEmpty(sel))
                        return $"simulate_input({inputAction}: \"{sel}\")";
                    return $"simulate_input({inputAction})";

                case "assert":
                    var prop = step.Arguments["property"]?.Value<string>();
                    var expected = step.Arguments["expected"];
                    var comparison = step.Arguments["comparison"]?.Value<string>() ?? "equals";
                    return $"assert({step.Arguments["selector"]}, {prop}, {comparison} {expected})";

                case "screenshot":
                    var label = step.Arguments["label"]?.Value<string>() ?? "unnamed";
                    return $"screenshot({label})";

                case "wait":
                    var ms = step.Arguments["ms"]?.Value<int>() ?? 1000;
                    return $"wait({ms}ms)";

                case "execute_csharp":
                    var code = step.Arguments["code"]?.Value<string>() ?? "";
                    var truncated = code.Length > 40 ? code.Substring(0, 40) + "..." : code;
                    return $"execute_csharp(\"{truncated}\")";

                default:
                    return step.Action;
            }
        }

        /// <summary>
        /// Extracts text from an McpToolResult.
        /// </summary>
        private static string GetResultText(McpToolResult result)
        {
            if (result?.Content == null || result.Content.Count == 0) return "";
            foreach (var c in result.Content)
            {
                if (c.Type == "text" && !string.IsNullOrEmpty(c.Text))
                    return c.Text;
            }
            return "";
        }

        /// <summary>
        /// Represents a single test step definition.
        /// </summary>
        internal class E2eTestStep
        {
            public int Index { get; set; }
            public string Action { get; set; }
            public JObject Arguments { get; set; }
        }

        /// <summary>
        /// Represents the result of executing a single test step.
        /// </summary>
        internal class E2eStepResult
        {
            public E2eTestStep Step { get; set; }
            public bool Passed { get; set; }
            public long ElapsedMs { get; set; }
            public string Message { get; set; }
            public McpToolResult ToolResult { get; set; }
        }
    }
}
