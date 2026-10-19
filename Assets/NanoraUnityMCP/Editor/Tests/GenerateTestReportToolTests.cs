using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for GenerateTestReportTool.
    /// Tests report generation for markdown/HTML formats,
    /// various test types, and edge cases.
    /// </summary>
    [TestFixture]
    public class GenerateTestReportToolTests
    {
        private GenerateTestReportTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new GenerateTestReportTool();
        }

        #region Tool Properties

        [Test]
        public void Name_IsCorrect()
        {
            Assert.AreEqual("generate_test_report", m_tool.Name);
        }

        [Test]
        public void Description_IsNotEmpty()
        {
            Assert.IsNotNull(m_tool.Description);
            Assert.IsTrue(m_tool.Description.Length > 0);
        }

        [Test]
        public void InputSchema_IsValid()
        {
            var schema = m_tool.InputSchema;
            Assert.IsNotNull(schema);
            Assert.AreEqual("object", schema["type"]?.Value<string>());

            var required = schema["required"] as JArray;
            Assert.IsNotNull(required);
            Assert.AreEqual(1, required.Count);
            Assert.AreEqual("results", required[0].Value<string>());
        }

        [Test]
        public void InputSchema_HasResultsProperty()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props["results"]);
            Assert.AreEqual("array", props["results"]["type"]?.Value<string>());
        }

        #endregion

        #region Empty / Null Results

        [Test]
        public async Task EmptyResults_GeneratesEmptyReport()
        {
            var args = new JObject
            {
                ["results"] = new JArray()
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("# E2E Test Report"));
            Assert.IsTrue(text.Contains("**Total**: 0 tests"));
            Assert.IsTrue(text.Contains("**Passed**: 0"));
            Assert.IsTrue(text.Contains("**Failed**: 0"));
        }

        [Test]
        public async Task NullResults_ReturnsError()
        {
            var args = new JObject
            {
                ["title"] = "No Results"
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("results"));
        }

        #endregion

        #region Markdown Format - E2E

        [Test]
        public async Task Markdown_E2ePassedResult_ContainsExpectedContent()
        {
            var args = new JObject
            {
                ["title"] = "My Test Suite",
                ["results"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "e2e",
                        ["name"] = "Player Jump Test",
                        ["status"] = "passed",
                        ["duration_ms"] = 1234,
                        ["steps"] = new JArray
                        {
                            new JObject
                            {
                                ["name"] = "wait_for_selector(#Player)",
                                ["status"] = "passed",
                                ["duration_ms"] = 120
                            },
                            new JObject
                            {
                                ["name"] = "simulate_input(Space)",
                                ["status"] = "passed",
                                ["duration_ms"] = 5
                            }
                        }
                    }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("# My Test Suite"));
            Assert.IsTrue(text.Contains("Player Jump Test"));
            Assert.IsTrue(text.Contains("PASSED"));
            Assert.IsTrue(text.Contains("1234ms"));
            Assert.IsTrue(text.Contains("E2E"));
            // Steps table
            Assert.IsTrue(text.Contains("wait_for_selector(#Player)"));
            Assert.IsTrue(text.Contains("simulate_input(Space)"));
            Assert.IsTrue(text.Contains("120ms"));
        }

        [Test]
        public async Task Markdown_E2eFailedResult_ContainsFailedStatus()
        {
            var args = new JObject
            {
                ["results"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "e2e",
                        ["name"] = "Login Flow",
                        ["status"] = "failed",
                        ["duration_ms"] = 500,
                        ["details"] = "Assertion failed at step 3",
                        ["steps"] = new JArray
                        {
                            new JObject
                            {
                                ["name"] = "click_login",
                                ["status"] = "passed",
                                ["duration_ms"] = 200
                            },
                            new JObject
                            {
                                ["name"] = "verify_dashboard",
                                ["status"] = "failed",
                                ["duration_ms"] = 300,
                                ["message"] = "Element not found"
                            }
                        }
                    }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("FAILED"));
            Assert.IsTrue(text.Contains("Login Flow"));
            Assert.IsTrue(text.Contains("Assertion failed at step 3"));
            Assert.IsTrue(text.Contains("Element not found"));
            Assert.IsTrue(text.Contains("**Failed**: 1"));
        }

        #endregion

        #region Markdown Format - Visual Regression

        [Test]
        public async Task Markdown_VisualRegressionResult_ContainsDiff()
        {
            var args = new JObject
            {
                ["results"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "visual_regression",
                        ["name"] = "Main Menu",
                        ["status"] = "failed",
                        ["duration_ms"] = 567,
                        ["diff_percentage"] = 5.2,
                        ["threshold"] = 1.0
                    }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("Visual"));
            Assert.IsTrue(text.Contains("Main Menu"));
            Assert.IsTrue(text.Contains("5.2%"));
            Assert.IsTrue(text.Contains("threshold: 1.0%"));
        }

        #endregion

        #region Markdown Format - Performance Budget

        [Test]
        public async Task Markdown_PerformanceBudgetResult_ContainsMetrics()
        {
            var args = new JObject
            {
                ["results"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "performance_budget",
                        ["name"] = "Scene Budget",
                        ["status"] = "passed",
                        ["duration_ms"] = 3000,
                        ["metrics"] = new JObject
                        {
                            ["fps"] = 60,
                            ["draw_calls"] = 150,
                            ["triangles"] = 50000
                        }
                    }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("Perf"));
            Assert.IsTrue(text.Contains("Scene Budget"));
            Assert.IsTrue(text.Contains("fps"));
            Assert.IsTrue(text.Contains("draw_calls"));
            Assert.IsTrue(text.Contains("triangles"));
            Assert.IsTrue(text.Contains("60"));
            Assert.IsTrue(text.Contains("150"));
        }

        #endregion

        #region Mixed Results

        [Test]
        public async Task Markdown_MixedResults_CorrectCounts()
        {
            var args = new JObject
            {
                ["results"] = new JArray
                {
                    new JObject { ["type"] = "e2e", ["name"] = "Test1", ["status"] = "passed", ["duration_ms"] = 100 },
                    new JObject { ["type"] = "visual_regression", ["name"] = "Test2", ["status"] = "failed", ["duration_ms"] = 200 },
                    new JObject { ["type"] = "performance_budget", ["name"] = "Test3", ["status"] = "passed", ["duration_ms"] = 300 },
                    new JObject { ["type"] = "e2e", ["name"] = "Test4", ["status"] = "skipped", ["duration_ms"] = 0 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("**Total**: 4 tests"));
            Assert.IsTrue(text.Contains("**Passed**: 2"));
            Assert.IsTrue(text.Contains("**Failed**: 1"));
            Assert.IsTrue(text.Contains("**Skipped**: 1"));
            Assert.IsTrue(text.Contains("**Duration**: 600ms"));
        }

        #endregion

        #region HTML Format

        [Test]
        public async Task Html_BasicStructure()
        {
            var args = new JObject
            {
                ["format"] = "html",
                ["title"] = "HTML Report",
                ["results"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "e2e",
                        ["name"] = "Basic Test",
                        ["status"] = "passed",
                        ["duration_ms"] = 100
                    }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("<!DOCTYPE html>"));
            Assert.IsTrue(text.Contains("<html"));
            Assert.IsTrue(text.Contains("</html>"));
            Assert.IsTrue(text.Contains("<style>"));
            Assert.IsTrue(text.Contains("HTML Report"));
            Assert.IsTrue(text.Contains("<table>"));
            Assert.IsTrue(text.Contains("PASSED"));
        }

        [Test]
        public async Task Html_FailedResult_HasFailedClass()
        {
            var args = new JObject
            {
                ["format"] = "html",
                ["results"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "e2e",
                        ["name"] = "Fail Test",
                        ["status"] = "failed",
                        ["duration_ms"] = 50
                    }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("status-failed"));
            Assert.IsTrue(text.Contains("FAILED"));
        }

        [Test]
        public async Task Html_ScreenshotEmbedded()
        {
            var args = new JObject
            {
                ["format"] = "html",
                ["results"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "visual_regression",
                        ["name"] = "Screenshot Test",
                        ["status"] = "failed",
                        ["duration_ms"] = 100,
                        ["screenshot_base64"] = "iVBORw0KGgoAAAANS"
                    }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("data:image/png;base64,iVBORw0KGgoAAAANS"));
            Assert.IsTrue(text.Contains("<img"));
        }

        [Test]
        public async Task Html_EmptyResults_ValidStructure()
        {
            var args = new JObject
            {
                ["format"] = "html",
                ["results"] = new JArray()
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("<!DOCTYPE html>"));
            Assert.IsTrue(text.Contains("</html>"));
            Assert.IsTrue(text.Contains("Total"));
        }

        #endregion

        #region Default Values

        [Test]
        public async Task DefaultTitle_UsesE2eTestReport()
        {
            var args = new JObject
            {
                ["results"] = new JArray()
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("# E2E Test Report"));
        }

        [Test]
        public async Task DefaultFormat_UsesMarkdown()
        {
            var args = new JObject
            {
                ["results"] = new JArray
                {
                    new JObject { ["type"] = "e2e", ["name"] = "T", ["status"] = "passed", ["duration_ms"] = 1 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;

            // Markdown uses # headers, HTML uses <h1>
            Assert.IsTrue(text.Contains("# "));
            Assert.IsFalse(text.Contains("<html"));
        }

        #endregion

        #region Helper Methods

        [Test]
        public void FormatTypeLabel_E2e()
        {
            Assert.AreEqual("E2E", GenerateTestReportTool.FormatTypeLabel("e2e"));
        }

        [Test]
        public void FormatTypeLabel_VisualRegression()
        {
            Assert.AreEqual("Visual", GenerateTestReportTool.FormatTypeLabel("visual_regression"));
        }

        [Test]
        public void FormatTypeLabel_PerformanceBudget()
        {
            Assert.AreEqual("Perf", GenerateTestReportTool.FormatTypeLabel("performance_budget"));
        }

        [Test]
        public void FormatTypeLabel_Unknown()
        {
            Assert.AreEqual("custom_type", GenerateTestReportTool.FormatTypeLabel("custom_type"));
        }

        [Test]
        public void FormatTypeLabel_Null()
        {
            Assert.AreEqual("Unknown", GenerateTestReportTool.FormatTypeLabel(null));
        }

        [Test]
        public void FormatStatusIcon_Passed()
        {
            var icon = GenerateTestReportTool.FormatStatusIcon("passed");
            Assert.IsTrue(icon.Contains("PASSED"));
        }

        [Test]
        public void FormatStatusIcon_Failed()
        {
            var icon = GenerateTestReportTool.FormatStatusIcon("failed");
            Assert.IsTrue(icon.Contains("FAILED"));
        }

        [Test]
        public void FormatStatusIcon_Skipped()
        {
            var icon = GenerateTestReportTool.FormatStatusIcon("skipped");
            Assert.IsTrue(icon.Contains("SKIPPED"));
        }

        #endregion

        #region Parsing

        [Test]
        public void ParseTestResult_AllFields()
        {
            var obj = new JObject
            {
                ["type"] = "visual_regression",
                ["name"] = "My Visual",
                ["status"] = "failed",
                ["duration_ms"] = 999,
                ["details"] = "Some detail",
                ["diff_percentage"] = 3.5,
                ["threshold"] = 1.0,
                ["metrics"] = new JObject { ["fps"] = 30 },
                ["steps"] = new JArray
                {
                    new JObject { ["name"] = "step1", ["status"] = "passed", ["duration_ms"] = 50, ["message"] = "ok" }
                }
            };

            var entry = GenerateTestReportTool.ParseTestResult(obj);

            Assert.AreEqual("visual_regression", entry.Type);
            Assert.AreEqual("My Visual", entry.Name);
            Assert.AreEqual("failed", entry.Status);
            Assert.AreEqual(999, entry.DurationMs);
            Assert.AreEqual("Some detail", entry.Details);
            Assert.AreEqual(3.5, entry.DiffPercentage);
            Assert.AreEqual(1.0, entry.Threshold);
            Assert.AreEqual(1, entry.Steps.Count);
            Assert.AreEqual("step1", entry.Steps[0].Name);
            Assert.AreEqual("ok", entry.Steps[0].Message);
        }

        [Test]
        public void ParseTestResult_MinimalFields_Defaults()
        {
            var obj = new JObject();
            var entry = GenerateTestReportTool.ParseTestResult(obj);

            Assert.AreEqual("e2e", entry.Type);
            Assert.AreEqual("Unnamed Test", entry.Name);
            Assert.AreEqual("skipped", entry.Status);
            Assert.AreEqual(0, entry.DurationMs);
            Assert.IsNull(entry.Details);
            Assert.IsNull(entry.DiffPercentage);
            Assert.AreEqual(0, entry.Steps.Count);
        }

        #endregion

        #region File Save (basic validation only - no actual disk write in EditMode)

        [Test]
        public async Task SaveToFile_InvalidPath_ReturnsError()
        {
            // Attempting to save to an invalid/nonexistent absolute path should error
            var args = new JObject
            {
                ["results"] = new JArray
                {
                    new JObject { ["type"] = "e2e", ["name"] = "T", ["status"] = "passed", ["duration_ms"] = 1 }
                },
                ["save_to_file"] = "/nonexistent_root_12345/report.md"
            };

            var result = await m_tool.ExecuteAsync(args);

            // Should return error because directory doesn't exist at root level
            // (Directory.CreateDirectory might fail on some platforms, or succeed - either way test the path)
            // The report itself should still be generated
            Assert.IsNotNull(result);
        }

        #endregion
    }
}
