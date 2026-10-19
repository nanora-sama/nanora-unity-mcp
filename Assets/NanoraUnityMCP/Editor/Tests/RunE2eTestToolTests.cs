using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for RunE2eTestTool.
    /// Focuses on input validation, step parsing, and report formatting.
    /// Actual Play Mode execution is not tested in EditMode.
    /// </summary>
    [TestFixture]
    public class RunE2eTestToolTests
    {
        private RunE2eTestTool m_tool;

        [SetUp]
        public void SetUp()
        {
            m_tool = new RunE2eTestTool();
        }

        #region Input Validation

        [Test]
        public async Task MissingName_ReturnsError()
        {
            var args = new JObject
            {
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait" }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("name"));
        }

        [Test]
        public async Task EmptyName_ReturnsError()
        {
            var args = new JObject
            {
                ["name"] = "",
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait" }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("name"));
        }

        [Test]
        public async Task NullSteps_ReturnsError()
        {
            var args = new JObject
            {
                ["name"] = "test_null_steps"
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("step"));
        }

        [Test]
        public async Task EmptySteps_ReturnsError()
        {
            var args = new JObject
            {
                ["name"] = "test_empty_steps",
                ["steps"] = new JArray()
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("step"));
        }

        [Test]
        public async Task StepMissingAction_ReturnsError()
        {
            var args = new JObject
            {
                ["name"] = "test_missing_action",
                ["steps"] = new JArray
                {
                    new JObject { ["selector"] = "#Player" }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("action"));
        }

        [Test]
        public async Task NoRegistry_ReturnsError()
        {
            // Tool without registry should fail when trying to delegate
            var args = new JObject
            {
                ["name"] = "test_no_registry",
                ["auto_enter_playmode"] = true,
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait", ["ms"] = 10 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            // Should fail because manage_playmode delegation requires registry
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("ToolRegistry"));
        }

        [Test]
        public async Task NoRegistry_SkipPlayMode_WaitStepSucceeds()
        {
            // With auto_enter/exit disabled and a wait step, no registry needed
            var args = new JObject
            {
                ["name"] = "test_wait_only",
                ["auto_enter_playmode"] = false,
                ["auto_exit_playmode"] = false,
                ["screenshot_on_failure"] = false,
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait", ["ms"] = 10 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("PASSED"));
            Assert.IsTrue(text.Contains("test_wait_only"));
        }

        [Test]
        public async Task MultipleWaitSteps_AllPass()
        {
            var args = new JObject
            {
                ["name"] = "test_multi_wait",
                ["auto_enter_playmode"] = false,
                ["auto_exit_playmode"] = false,
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait", ["ms"] = 5 },
                    new JObject { ["action"] = "wait", ["ms"] = 5 },
                    new JObject { ["action"] = "wait", ["ms"] = 5 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            var text = result.Content[0].Text;
            Assert.IsTrue(text.Contains("PASSED"));
            Assert.IsTrue(text.Contains("Steps: 3/3"));
        }

        #endregion

        #region Step Description Formatting

        [Test]
        public void FormatStep_WaitForSelector()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 1,
                Action = "wait_for_selector",
                Arguments = new JObject { ["selector"] = "#Player" }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("wait_for_selector"));
            Assert.IsTrue(desc.Contains("#Player"));
        }

        [Test]
        public void FormatStep_SimulateInput_KeyTap()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 2,
                Action = "simulate_input",
                Arguments = new JObject
                {
                    ["input_action"] = "key_tap",
                    ["key"] = "Space"
                }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("simulate_input"));
            Assert.IsTrue(desc.Contains("key_tap"));
            Assert.IsTrue(desc.Contains("Space"));
        }

        [Test]
        public void FormatStep_SimulateInput_UiClick()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 2,
                Action = "simulate_input",
                Arguments = new JObject
                {
                    ["input_action"] = "ui_click",
                    ["selector"] = "#StartButton"
                }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("ui_click"));
            Assert.IsTrue(desc.Contains("#StartButton"));
        }

        [Test]
        public void FormatStep_Assert()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 4,
                Action = "assert",
                Arguments = new JObject
                {
                    ["selector"] = "#Player",
                    ["property"] = "active",
                    ["expected"] = true,
                    ["comparison"] = "equals"
                }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("assert"));
            Assert.IsTrue(desc.Contains("#Player"));
            Assert.IsTrue(desc.Contains("active"));
        }

        [Test]
        public void FormatStep_Screenshot()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 5,
                Action = "screenshot",
                Arguments = new JObject { ["label"] = "after_jump" }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("screenshot"));
            Assert.IsTrue(desc.Contains("after_jump"));
        }

        [Test]
        public void FormatStep_Wait()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 3,
                Action = "wait",
                Arguments = new JObject { ["ms"] = 500 }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("wait"));
            Assert.IsTrue(desc.Contains("500ms"));
        }

        [Test]
        public void FormatStep_ExecuteCSharp()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 6,
                Action = "execute_csharp",
                Arguments = new JObject { ["code"] = "Debug.Log(\"hello\")" }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("execute_csharp"));
            Assert.IsTrue(desc.Contains("Debug.Log"));
        }

        [Test]
        public void FormatStep_ExecuteCSharp_LongCode_Truncated()
        {
            var step = new RunE2eTestTool.E2eTestStep
            {
                Index = 1,
                Action = "execute_csharp",
                Arguments = new JObject
                {
                    ["code"] = "var obj = GameObject.Find(\"Player\"); obj.transform.position = new Vector3(0, 10, 0);"
                }
            };

            var desc = RunE2eTestTool.FormatStepDescription(step);

            Assert.IsTrue(desc.Contains("..."));
        }

        [Test]
        public void FormatStep_NullStep_ReturnsUnknown()
        {
            var desc = RunE2eTestTool.FormatStepDescription(null);
            Assert.AreEqual("(unknown step)", desc);
        }

        #endregion

        #region Report Structure

        [Test]
        public async Task Report_ContainsTestName()
        {
            var args = new JObject
            {
                ["name"] = "MySpecialTest",
                ["auto_enter_playmode"] = false,
                ["auto_exit_playmode"] = false,
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait", ["ms"] = 1 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("MySpecialTest"));
            Assert.IsTrue(text.Contains("E2E Test Result"));
        }

        [Test]
        public async Task Report_ContainsDuration()
        {
            var args = new JObject
            {
                ["name"] = "DurationTest",
                ["auto_enter_playmode"] = false,
                ["auto_exit_playmode"] = false,
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait", ["ms"] = 1 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("Duration:"));
            Assert.IsTrue(text.Contains("ms"));
        }

        [Test]
        public async Task Report_ContainsStepCount()
        {
            var args = new JObject
            {
                ["name"] = "StepCountTest",
                ["auto_enter_playmode"] = false,
                ["auto_exit_playmode"] = false,
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait", ["ms"] = 1 },
                    new JObject { ["action"] = "wait", ["ms"] = 1 }
                }
            };

            var result = await m_tool.ExecuteAsync(args);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("Steps: 2/2"));
        }

        [Test]
        public async Task DefaultWaitMs_UsesOneSecond()
        {
            // When ms is not specified, should default to 1000
            var args = new JObject
            {
                ["name"] = "default_wait",
                ["auto_enter_playmode"] = false,
                ["auto_exit_playmode"] = false,
                ["timeout"] = 3,
                ["steps"] = new JArray
                {
                    new JObject { ["action"] = "wait" }
                }
            };

            var result = await m_tool.ExecuteAsync(args);
            var text = result.Content[0].Text;

            Assert.IsTrue(text.Contains("wait(1000ms)"));
        }

        #endregion

        #region Tool Properties

        [Test]
        public void Name_IsCorrect()
        {
            Assert.AreEqual("run_e2e_test", m_tool.Name);
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
            Assert.IsTrue(required.Count == 2);
        }

        [Test]
        public void InputSchema_HasStepsProperty()
        {
            var schema = m_tool.InputSchema;
            var props = schema["properties"] as JObject;
            Assert.IsNotNull(props["steps"]);
            Assert.AreEqual("array", props["steps"]["type"]?.Value<string>());
        }

        #endregion
    }
}
