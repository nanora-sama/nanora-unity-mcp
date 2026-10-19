using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Generates a comprehensive test report from E2E test, visual regression,
    /// and performance budget results. Outputs Markdown or HTML format.
    /// </summary>
    [McpTool("generate_test_report")]
    public class GenerateTestReportTool : IMcpTool
    {
        public string Name => "generate_test_report";
        public string Description => "Generate a comprehensive test report from E2E test, visual regression, " +
            "and performance budget results. Outputs Markdown or HTML format.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""title"": {
                    ""type"": ""string"",
                    ""description"": ""Report title"",
                    ""default"": ""E2E Test Report""
                },
                ""format"": {
                    ""type"": ""string"",
                    ""enum"": [""markdown"", ""html""],
                    ""default"": ""markdown""
                },
                ""results"": {
                    ""type"": ""array"",
                    ""description"": ""Test results to include in the report"",
                    ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""type"": { ""type"": ""string"", ""enum"": [""e2e"", ""visual_regression"", ""performance_budget""] },
                            ""name"": { ""type"": ""string"" },
                            ""status"": { ""type"": ""string"", ""enum"": [""passed"", ""failed"", ""skipped""] },
                            ""duration_ms"": { ""type"": ""number"" },
                            ""details"": { ""type"": ""string"" },
                            ""steps"": {
                                ""type"": ""array"",
                                ""items"": {
                                    ""type"": ""object"",
                                    ""properties"": {
                                        ""name"": { ""type"": ""string"" },
                                        ""status"": { ""type"": ""string"" },
                                        ""duration_ms"": { ""type"": ""number"" },
                                        ""message"": { ""type"": ""string"" }
                                    }
                                }
                            },
                            ""metrics"": {
                                ""type"": ""object"",
                                ""description"": ""For performance_budget: metric values""
                            },
                            ""diff_percentage"": { ""type"": ""number"", ""description"": ""For visual_regression"" },
                            ""threshold"": { ""type"": ""number"", ""description"": ""For visual_regression: threshold percentage"" },
                            ""screenshot_base64"": { ""type"": ""string"", ""description"": ""Base64-encoded screenshot"" }
                        }
                    }
                },
                ""save_to_file"": {
                    ""type"": ""string"",
                    ""description"": ""Optional file path to save the report""
                }
            },
            ""required"": [""results""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            string title = arguments?["title"]?.Value<string>() ?? "E2E Test Report";
            string format = arguments?["format"]?.Value<string>() ?? "markdown";
            var resultsArray = arguments?["results"] as JArray;
            string saveToFile = arguments?["save_to_file"]?.Value<string>();

            if (resultsArray == null)
            {
                return Task.FromResult(McpToolResult.Error("'results' array is required"));
            }

            // Parse results
            var testResults = new List<TestResultEntry>();
            for (int i = 0; i < resultsArray.Count; i++)
            {
                var item = resultsArray[i] as JObject;
                if (item == null) continue;

                testResults.Add(ParseTestResult(item));
            }

            // Generate report
            string report;
            if (format == "html")
            {
                report = GenerateHtmlReport(title, testResults);
            }
            else
            {
                report = GenerateMarkdownReport(title, testResults);
            }

            // Save to file if requested
            string savedPath = null;
            if (!string.IsNullOrWhiteSpace(saveToFile))
            {
                try
                {
                    savedPath = SaveReport(saveToFile, report);
                }
                catch (Exception ex)
                {
                    return Task.FromResult(McpToolResult.Error(
                        $"Report generated but failed to save to '{saveToFile}': {ex.Message}"));
                }
            }

            var sb = new StringBuilder();
            sb.Append(report);

            if (savedPath != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Report saved to: {savedPath}");
            }

            return Task.FromResult(McpToolResult.Success(sb.ToString()));
        }

        #region Parsing

        internal static TestResultEntry ParseTestResult(JObject item)
        {
            var entry = new TestResultEntry
            {
                Type = item["type"]?.Value<string>() ?? "e2e",
                Name = item["name"]?.Value<string>() ?? "Unnamed Test",
                Status = item["status"]?.Value<string>() ?? "skipped",
                DurationMs = item["duration_ms"]?.Value<double>() ?? 0,
                Details = item["details"]?.Value<string>(),
                DiffPercentage = item["diff_percentage"]?.Value<double?>(),
                Threshold = item["threshold"]?.Value<double?>(),
                ScreenshotBase64 = item["screenshot_base64"]?.Value<string>(),
                Metrics = item["metrics"] as JObject
            };

            var stepsArray = item["steps"] as JArray;
            if (stepsArray != null)
            {
                foreach (var stepToken in stepsArray)
                {
                    var stepObj = stepToken as JObject;
                    if (stepObj == null) continue;

                    entry.Steps.Add(new TestStepEntry
                    {
                        Name = stepObj["name"]?.Value<string>() ?? "",
                        Status = stepObj["status"]?.Value<string>() ?? "unknown",
                        DurationMs = stepObj["duration_ms"]?.Value<double>() ?? 0,
                        Message = stepObj["message"]?.Value<string>()
                    });
                }
            }

            return entry;
        }

        #endregion

        #region Markdown Generation

        internal static string GenerateMarkdownReport(string title, List<TestResultEntry> results)
        {
            var sb = new StringBuilder();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            int total = results.Count;
            int passed = results.Count(r => r.Status == "passed");
            int failed = results.Count(r => r.Status == "failed");
            int skipped = results.Count(r => r.Status == "skipped");
            double totalDuration = results.Sum(r => r.DurationMs);

            // Header
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"**Generated**: {timestamp}");
            sb.AppendLine($"**Total**: {total} tests | **Passed**: {passed} | **Failed**: {failed} | **Skipped**: {skipped}");
            sb.AppendLine($"**Duration**: {totalDuration:F0}ms");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Summary table
            if (results.Count > 0)
            {
                sb.AppendLine("## Summary");
                sb.AppendLine();
                sb.AppendLine("| # | Type | Test Name | Status | Duration |");
                sb.AppendLine("|---|------|-----------|--------|----------|");

                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    string typeLabel = FormatTypeLabel(r.Type);
                    string statusIcon = FormatStatusIcon(r.Status);
                    sb.AppendLine($"| {i + 1} | {typeLabel} | {r.Name} | {statusIcon} | {r.DurationMs:F0}ms |");
                }

                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Detailed results
            if (results.Count > 0)
            {
                sb.AppendLine("## Detailed Results");
                sb.AppendLine();

                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    string typeLabel = FormatTypeLabel(r.Type);
                    string statusIcon = FormatStatusIcon(r.Status);

                    sb.AppendLine($"### {i + 1}. [{typeLabel}] {r.Name} -- {statusIcon} ({r.DurationMs:F0}ms)");
                    sb.AppendLine();

                    // Type-specific details
                    switch (r.Type)
                    {
                        case "e2e":
                            AppendE2eDetails(sb, r);
                            break;
                        case "visual_regression":
                            AppendVisualRegressionDetails(sb, r);
                            break;
                        case "performance_budget":
                            AppendPerformanceBudgetDetails(sb, r);
                            break;
                    }

                    // General details
                    if (!string.IsNullOrEmpty(r.Details))
                    {
                        sb.AppendLine(r.Details);
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }

        private static void AppendE2eDetails(StringBuilder sb, TestResultEntry r)
        {
            if (r.Steps.Count == 0) return;

            sb.AppendLine("| Step | Action | Status | Duration |");
            sb.AppendLine("|------|--------|--------|----------|");

            for (int j = 0; j < r.Steps.Count; j++)
            {
                var step = r.Steps[j];
                string stepStatus = FormatStatusIcon(step.Status);
                string msg = !string.IsNullOrEmpty(step.Message) ? $" - {step.Message}" : "";
                sb.AppendLine($"| {j + 1} | {step.Name}{msg} | {stepStatus} | {step.DurationMs:F0}ms |");
            }

            sb.AppendLine();
        }

        private static void AppendVisualRegressionDetails(StringBuilder sb, TestResultEntry r)
        {
            if (r.DiffPercentage.HasValue)
            {
                string thresholdStr = r.Threshold.HasValue
                    ? $" (threshold: {r.Threshold.Value:F1}%)"
                    : "";
                sb.AppendLine($"Diff: {r.DiffPercentage.Value:F1}%{thresholdStr}");
                sb.AppendLine();
            }
        }

        private static void AppendPerformanceBudgetDetails(StringBuilder sb, TestResultEntry r)
        {
            if (r.Metrics == null || !r.Metrics.HasValues) return;

            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");

            foreach (var prop in r.Metrics.Properties())
            {
                sb.AppendLine($"| {prop.Name} | {prop.Value} |");
            }

            sb.AppendLine();
        }

        #endregion

        #region HTML Generation

        internal static string GenerateHtmlReport(string title, List<TestResultEntry> results)
        {
            var sb = new StringBuilder();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            int total = results.Count;
            int passed = results.Count(r => r.Status == "passed");
            int failed = results.Count(r => r.Status == "failed");
            int skipped = results.Count(r => r.Status == "skipped");
            double totalDuration = results.Sum(r => r.DurationMs);

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine($"<title>{EscapeHtml(title)}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(GetCssStyles());
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine($"<h1>{EscapeHtml(title)}</h1>");
            sb.AppendLine("<div class=\"meta\">");
            sb.AppendLine($"<p><strong>Generated</strong>: {timestamp}</p>");
            sb.AppendLine($"<p><strong>Total</strong>: {total} tests | ");
            sb.AppendLine($"<span class=\"passed\">Passed: {passed}</span> | ");
            sb.AppendLine($"<span class=\"failed\">Failed: {failed}</span> | ");
            sb.AppendLine($"<span class=\"skipped\">Skipped: {skipped}</span></p>");
            sb.AppendLine($"<p><strong>Duration</strong>: {totalDuration:F0}ms</p>");
            sb.AppendLine("</div>");

            // Summary table
            if (results.Count > 0)
            {
                sb.AppendLine("<h2>Summary</h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead><tr><th>#</th><th>Type</th><th>Test Name</th><th>Status</th><th>Duration</th></tr></thead>");
                sb.AppendLine("<tbody>");

                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    string statusClass = r.Status;
                    string typeLabel = FormatTypeLabel(r.Type);
                    string statusLabel = r.Status.ToUpperInvariant();

                    sb.AppendLine($"<tr class=\"{statusClass}\">");
                    sb.AppendLine($"<td>{i + 1}</td>");
                    sb.AppendLine($"<td>{EscapeHtml(typeLabel)}</td>");
                    sb.AppendLine($"<td>{EscapeHtml(r.Name)}</td>");
                    sb.AppendLine($"<td class=\"status-{statusClass}\">{statusLabel}</td>");
                    sb.AppendLine($"<td>{r.DurationMs:F0}ms</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");
            }

            // Detailed results
            if (results.Count > 0)
            {
                sb.AppendLine("<h2>Detailed Results</h2>");

                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    string typeLabel = FormatTypeLabel(r.Type);
                    string statusLabel = r.Status.ToUpperInvariant();
                    string statusClass = r.Status;

                    sb.AppendLine($"<div class=\"test-detail {statusClass}\">");
                    sb.AppendLine($"<h3>{i + 1}. [{EscapeHtml(typeLabel)}] {EscapeHtml(r.Name)} -- " +
                        $"<span class=\"status-{statusClass}\">{statusLabel}</span> ({r.DurationMs:F0}ms)</h3>");

                    // Type-specific HTML details
                    switch (r.Type)
                    {
                        case "e2e":
                            AppendE2eDetailsHtml(sb, r);
                            break;
                        case "visual_regression":
                            AppendVisualRegressionDetailsHtml(sb, r);
                            break;
                        case "performance_budget":
                            AppendPerformanceBudgetDetailsHtml(sb, r);
                            break;
                    }

                    if (!string.IsNullOrEmpty(r.Details))
                    {
                        sb.AppendLine($"<p>{EscapeHtml(r.Details)}</p>");
                    }

                    // Embedded screenshot
                    if (!string.IsNullOrEmpty(r.ScreenshotBase64))
                    {
                        sb.AppendLine($"<img src=\"data:image/png;base64,{r.ScreenshotBase64}\" alt=\"Screenshot\" style=\"max-width:100%;\" />");
                    }

                    sb.AppendLine("</div>");
                }
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private static void AppendE2eDetailsHtml(StringBuilder sb, TestResultEntry r)
        {
            if (r.Steps.Count == 0) return;

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Step</th><th>Action</th><th>Status</th><th>Duration</th></tr></thead>");
            sb.AppendLine("<tbody>");

            for (int j = 0; j < r.Steps.Count; j++)
            {
                var step = r.Steps[j];
                string stepStatusClass = step.Status == "passed" ? "passed" : (step.Status == "failed" ? "failed" : "skipped");
                string msg = !string.IsNullOrEmpty(step.Message) ? $" - {EscapeHtml(step.Message)}" : "";

                sb.AppendLine($"<tr class=\"{stepStatusClass}\">");
                sb.AppendLine($"<td>{j + 1}</td>");
                sb.AppendLine($"<td>{EscapeHtml(step.Name)}{msg}</td>");
                sb.AppendLine($"<td class=\"status-{stepStatusClass}\">{step.Status.ToUpperInvariant()}</td>");
                sb.AppendLine($"<td>{step.DurationMs:F0}ms</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }

        private static void AppendVisualRegressionDetailsHtml(StringBuilder sb, TestResultEntry r)
        {
            if (r.DiffPercentage.HasValue)
            {
                string thresholdStr = r.Threshold.HasValue
                    ? $" (threshold: {r.Threshold.Value:F1}%)"
                    : "";
                sb.AppendLine($"<p>Diff: {r.DiffPercentage.Value:F1}%{thresholdStr}</p>");
            }
        }

        private static void AppendPerformanceBudgetDetailsHtml(StringBuilder sb, TestResultEntry r)
        {
            if (r.Metrics == null || !r.Metrics.HasValues) return;

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Metric</th><th>Value</th></tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var prop in r.Metrics.Properties())
            {
                sb.AppendLine($"<tr><td>{EscapeHtml(prop.Name)}</td><td>{EscapeHtml(prop.Value.ToString())}</td></tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }

        private static string GetCssStyles()
        {
            return @"
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 20px; background: #fafafa; color: #333; }
h1 { border-bottom: 2px solid #333; padding-bottom: 8px; }
h2 { margin-top: 24px; }
h3 { margin-bottom: 8px; }
.meta { background: #f0f0f0; padding: 12px; border-radius: 4px; margin-bottom: 16px; }
table { border-collapse: collapse; width: 100%; margin-bottom: 16px; }
th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
th { background: #f5f5f5; font-weight: 600; }
.status-passed { color: #22863a; font-weight: bold; }
.status-failed { color: #cb2431; font-weight: bold; }
.status-skipped { color: #6a737d; font-weight: bold; }
.passed { color: #22863a; }
.failed { color: #cb2431; }
.skipped { color: #6a737d; }
.test-detail { border: 1px solid #e1e4e8; border-radius: 6px; padding: 16px; margin-bottom: 16px; background: #fff; }
.test-detail.passed { border-left: 4px solid #22863a; }
.test-detail.failed { border-left: 4px solid #cb2431; }
.test-detail.skipped { border-left: 4px solid #6a737d; }
img { margin-top: 8px; border: 1px solid #ddd; border-radius: 4px; }";
        }

        #endregion

        #region Helpers

        internal static string FormatTypeLabel(string type)
        {
            switch (type)
            {
                case "e2e": return "E2E";
                case "visual_regression": return "Visual";
                case "performance_budget": return "Perf";
                default: return type ?? "Unknown";
            }
        }

        internal static string FormatStatusIcon(string status)
        {
            switch (status)
            {
                case "passed": return "\u2705 PASSED";
                case "failed": return "\u274c FAILED";
                case "skipped": return "\u23ed\ufe0f SKIPPED";
                default: return status ?? "UNKNOWN";
            }
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string SaveReport(string filePath, string content)
        {
            // If relative path, resolve from project root (two levels up from Assets)
            if (!Path.IsPathRooted(filePath))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(
                    UnityEngine.Application.dataPath, ".."));
                filePath = Path.Combine(projectRoot, filePath);
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
            return filePath;
        }

        #endregion

        #region Data Models

        internal class TestResultEntry
        {
            public string Type { get; set; }
            public string Name { get; set; }
            public string Status { get; set; }
            public double DurationMs { get; set; }
            public string Details { get; set; }
            public List<TestStepEntry> Steps { get; set; } = new List<TestStepEntry>();
            public JObject Metrics { get; set; }
            public double? DiffPercentage { get; set; }
            public double? Threshold { get; set; }
            public string ScreenshotBase64 { get; set; }
        }

        internal class TestStepEntry
        {
            public string Name { get; set; }
            public string Status { get; set; }
            public double DurationMs { get; set; }
            public string Message { get; set; }
        }

        #endregion
    }
}
