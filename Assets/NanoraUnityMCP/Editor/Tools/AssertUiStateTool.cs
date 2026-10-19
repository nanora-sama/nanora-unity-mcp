using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;
#if TEXTMESHPRO_INSTALLED
using TMPro;
#endif

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Playwright-style expect/assertion tool for UI state verification.
    /// Waits until the expected condition is met or times out.
    /// </summary>
    [McpTool("assert_ui_state")]
    public class AssertUiStateTool : IMcpTool
    {
        public string Name => "assert_ui_state";
        public string Description => "Assert UI state matches expected value. " +
            "Playwright-style expect/assertion that waits until condition is met or times out.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector to find the target object (e.g., '#ScoreText', '.Button')""
                },
                ""property"": {
                    ""type"": ""string"",
                    ""description"": ""Property to check: text, active, enabled, visible, color, alpha, value, interactable"",
                    ""enum"": [""text"", ""active"", ""enabled"", ""visible"", ""color"", ""alpha"", ""value"", ""interactable""]
                },
                ""expected"": {
                    ""description"": ""Expected value (string, boolean, number depending on property)""
                },
                ""comparison"": {
                    ""type"": ""string"",
                    ""description"": ""Comparison operator"",
                    ""enum"": [""equals"", ""contains"", ""starts_with"", ""ends_with"", ""greater_than"", ""less_than"", ""not_equals""],
                    ""default"": ""equals""
                },
                ""timeout"": {
                    ""type"": ""number"",
                    ""description"": ""Timeout in seconds"",
                    ""default"": 5
                },
                ""poll_interval"": {
                    ""type"": ""number"",
                    ""description"": ""Poll interval in seconds"",
                    ""default"": 0.1
                }
            },
            ""required"": [""selector"", ""property"", ""expected""]
        }");

        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            string selector = arguments?["selector"]?.Value<string>();
            string property = arguments?["property"]?.Value<string>();
            var expected = arguments?["expected"];
            string comparison = arguments?["comparison"]?.Value<string>() ?? "equals";
            float timeout = arguments?["timeout"]?.Value<float>() ?? 5f;
            float pollInterval = arguments?["poll_interval"]?.Value<float>() ?? 0.1f;

            if (string.IsNullOrEmpty(selector))
            {
                return McpToolResult.Error("Selector is required");
            }

            if (string.IsNullOrEmpty(property))
            {
                return McpToolResult.Error("Property is required");
            }

            if (expected == null)
            {
                return McpToolResult.Error("Expected value is required");
            }

            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(timeout);

            string lastActualValue = null;
            string lastError = null;

            while (DateTime.Now < endTime)
            {
                var checkResult = await MainThreadDispatcher.RunAsync(() =>
                    CheckCondition(selector, property, expected, comparison));

                if (checkResult.Success)
                {
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    return McpToolResult.Success(
                        $"Assertion PASSED\n" +
                        $"- Selector: {selector}\n" +
                        $"- Property: {property}\n" +
                        $"- Expected: {expected} ({comparison})\n" +
                        $"- Actual: {checkResult.ActualValue}\n" +
                        $"- Elapsed: {elapsed:F0}ms");
                }

                lastActualValue = checkResult.ActualValue;
                lastError = checkResult.Error;

                await Task.Delay((int)(pollInterval * 1000));
            }

            // Timeout - assertion failed
            var totalElapsed = (DateTime.Now - startTime).TotalMilliseconds;
            return McpToolResult.Error(
                $"Assertion FAILED (timeout after {totalElapsed:F0}ms)\n" +
                $"- Selector: {selector}\n" +
                $"- Property: {property}\n" +
                $"- Expected: {expected} ({comparison})\n" +
                $"- Actual: {lastActualValue ?? "(not found)"}\n" +
                (lastError != null ? $"- Error: {lastError}" : ""));
        }

        private AssertResult CheckCondition(string selector, string property, JToken expected, string comparison)
        {
            try
            {
                // Find objects using SelectorEngine
                var results = SelectorEngine.Query(selector, includeInactive: false);
                if (results.Count == 0)
                {
                    return new AssertResult { Success = false, Error = $"No object found matching '{selector}'" };
                }

                var go = results[0];
                var actualValue = GetPropertyValue(go, property);

                if (actualValue == null)
                {
                    return new AssertResult
                    {
                        Success = false,
                        Error = $"Could not get property '{property}' from object",
                        ActualValue = "(null)"
                    };
                }

                var matches = CompareValues(actualValue, expected, comparison);

                return new AssertResult
                {
                    Success = matches,
                    ActualValue = actualValue.ToString()
                };
            }
            catch (Exception ex)
            {
                return new AssertResult { Success = false, Error = ex.Message };
            }
        }

        private object GetPropertyValue(GameObject go, string property)
        {
            switch (property.ToLower())
            {
                case "text":
#if TEXTMESHPRO_INSTALLED
                    // Try TextMeshProUGUI first
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) return tmp.text;
#endif

                    // Try legacy Text
                    var text = go.GetComponent<Text>();
                    if (text != null) return text.text;

#if TEXTMESHPRO_INSTALLED
                    // Try TextMeshPro (3D)
                    var tmp3d = go.GetComponent<TextMeshPro>();
                    if (tmp3d != null) return tmp3d.text;
#endif

                    return null;

                case "active":
                    return go.activeInHierarchy;

                case "enabled":
                    // Check if main component is enabled
                    var behaviour = go.GetComponent<Behaviour>();
                    return behaviour != null ? behaviour.enabled : go.activeInHierarchy;

                case "visible":
                    // Check CanvasGroup alpha or Renderer visibility
                    var canvasGroup = go.GetComponent<CanvasGroup>();
                    if (canvasGroup != null) return canvasGroup.alpha > 0;

                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null) return renderer.enabled && renderer.isVisible;

                    return go.activeInHierarchy;

                case "color":
                    var image = go.GetComponent<Image>();
                    if (image != null) return ColorUtility.ToHtmlStringRGBA(image.color);

                    var rawImage = go.GetComponent<RawImage>();
                    if (rawImage != null) return ColorUtility.ToHtmlStringRGBA(rawImage.color);

                    var textComp = go.GetComponent<Text>();
                    if (textComp != null) return ColorUtility.ToHtmlStringRGBA(textComp.color);

                    return null;

                case "alpha":
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg != null) return cg.alpha;

                    var img = go.GetComponent<Image>();
                    if (img != null) return img.color.a;

                    return null;

                case "value":
                    var slider = go.GetComponent<Slider>();
                    if (slider != null) return slider.value;

                    var scrollbar = go.GetComponent<Scrollbar>();
                    if (scrollbar != null) return scrollbar.value;

                    var inputField = go.GetComponent<InputField>();
                    if (inputField != null) return inputField.text;

#if TEXTMESHPRO_INSTALLED
                    var tmpInput = go.GetComponent<TMP_InputField>();
                    if (tmpInput != null) return tmpInput.text;
#endif

                    return null;

                case "interactable":
                    var button = go.GetComponent<Button>();
                    if (button != null) return button.interactable;

                    var selectable = go.GetComponent<Selectable>();
                    if (selectable != null) return selectable.interactable;

                    var cgInteract = go.GetComponent<CanvasGroup>();
                    if (cgInteract != null) return cgInteract.interactable;

                    return null;

                default:
                    return null;
            }
        }

        private bool CompareValues(object actual, JToken expected, string comparison)
        {
            var actualStr = actual?.ToString() ?? "";
            var expectedStr = expected?.ToString() ?? "";

            switch (comparison.ToLower())
            {
                case "equals":
                    // Try boolean comparison
                    if (actual is bool actualBool && expected.Type == JTokenType.Boolean)
                    {
                        return actualBool == expected.Value<bool>();
                    }
                    // Try numeric comparison
                    if ((actual is float || actual is double || actual is int) && expected.Type == JTokenType.Float || expected.Type == JTokenType.Integer)
                    {
                        var actualNum = Convert.ToDouble(actual);
                        var expectedNum = expected.Value<double>();
                        return Math.Abs(actualNum - expectedNum) < 0.0001;
                    }
                    // String comparison (case-insensitive)
                    return string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase);

                case "contains":
                    return actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase);

                case "starts_with":
                    return actualStr.StartsWith(expectedStr, StringComparison.OrdinalIgnoreCase);

                case "ends_with":
                    return actualStr.EndsWith(expectedStr, StringComparison.OrdinalIgnoreCase);

                case "greater_than":
                    if (double.TryParse(actualStr, out var actualGt) && double.TryParse(expectedStr, out var expectedGt))
                    {
                        return actualGt > expectedGt;
                    }
                    return false;

                case "less_than":
                    if (double.TryParse(actualStr, out var actualLt) && double.TryParse(expectedStr, out var expectedLt))
                    {
                        return actualLt < expectedLt;
                    }
                    return false;

                case "not_equals":
                    return !string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase);

                default:
                    return string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase);
            }
        }

        private class AssertResult
        {
            public bool Success { get; set; }
            public string ActualValue { get; set; }
            public string Error { get; set; }
        }
    }
}
