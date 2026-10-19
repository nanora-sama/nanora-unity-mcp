using System;
using System.Collections;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Playwright-like auto-waiting tool.
    /// Waits for a selector to match an "actionable" object (exists, active, visible, interactable).
    /// </summary>
    [McpTool("wait_for_selector")]
    public class WaitForSelectorTool : IMcpTool
    {
        private const float k_DefaultTimeout = 5f;
        private const float k_PollInterval = 0.1f;

        public string Name => "wait_for_selector";
        public string Description => "Poll until a GameObject matching a U-Selector (same syntax as query_objects) reaches a state, or until timeout. " +
            "States: attached (exists, even inactive), detached (no match), visible (active in hierarchy and rendered/shown), " +
            "hidden (no match or not visible), interactable (visible and accepts interaction). " +
            "Use it after actions whose effect appears a few frames later, such as entering Play Mode or clicking UI, instead of sleeping. " +
            "Returns the matched object's path, or an error on timeout.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector query string""
                },
                ""state"": {
                    ""type"": ""string"",
                    ""description"": ""Expected state to wait for"",
                    ""enum"": [""attached"", ""detached"", ""visible"", ""hidden"", ""interactable""],
                    ""default"": ""visible""
                },
                ""timeout"": {
                    ""type"": ""number"",
                    ""description"": ""Maximum time to wait in seconds"",
                    ""default"": 5
                },
                ""poll_interval"": {
                    ""type"": ""number"",
                    ""description"": ""Time between checks in seconds"",
                    ""default"": 0.1
                }
            },
            ""required"": [""selector""]
        }");

        public async Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            string selector = arguments?["selector"]?.Value<string>();
            string state = arguments?["state"]?.Value<string>() ?? "visible";
            float timeout = arguments?["timeout"]?.Value<float>() ?? k_DefaultTimeout;
            float pollInterval = arguments?["poll_interval"]?.Value<float>() ?? k_PollInterval;

            if (string.IsNullOrWhiteSpace(selector))
            {
                return McpToolResult.Error("Selector is required");
            }

            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(timeout);

            while (DateTime.Now < endTime)
            {
                // Check condition on main thread
                var checkResult = await MainThreadDispatcher.RunAsync(() =>
                {
                    return CheckState(selector, state);
                });

                if (checkResult.isSuccess)
                {
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    return McpToolResult.Success(
                        $"Selector '{selector}' matched state '{state}' after {elapsed:F2}s.\n" +
                        $"Object: {checkResult.objectPath}");
                }

                // Wait before next check
                await Task.Delay((int)(pollInterval * 1000));
            }

            // Timeout
            return McpToolResult.Error(
                $"Timeout ({timeout}s) waiting for selector '{selector}' to be '{state}'.");
        }

        private (bool isSuccess, string objectPath) CheckState(string selector, string state)
        {
            try
            {
                var results = SelectorEngine.Query(selector, true);

                switch (state.ToLower())
                {
                    case "attached":
                        // Object exists (even if inactive)
                        if (results.Count > 0)
                        {
                            return (true, GetPath(results[0]));
                        }
                        return (false, null);

                    case "detached":
                        // Object does not exist
                        if (results.Count == 0)
                        {
                            return (true, "(detached)");
                        }
                        return (false, null);

                    case "visible":
                        // Object exists, is active, and in hierarchy
                        foreach (var obj in results)
                        {
                            if (obj.activeInHierarchy && IsVisible(obj))
                            {
                                return (true, GetPath(obj));
                            }
                        }
                        return (false, null);

                    case "hidden":
                        // Object is not visible (either doesn't exist or is inactive)
                        if (results.Count == 0)
                        {
                            return (true, "(not found)");
                        }
                        foreach (var obj in results)
                        {
                            if (!obj.activeInHierarchy || !IsVisible(obj))
                            {
                                return (true, GetPath(obj) + " (hidden)");
                            }
                        }
                        return (false, null);

                    case "interactable":
                        // Object exists, is visible, and can be interacted with
                        foreach (var obj in results)
                        {
                            if (obj.activeInHierarchy && IsVisible(obj) && IsInteractable(obj))
                            {
                                return (true, GetPath(obj));
                            }
                        }
                        return (false, null);

                    default:
                        return (false, null);
                }
            }
            catch
            {
                return (false, null);
            }
        }

        private bool IsVisible(GameObject obj)
        {
            // Check if object is active
            if (!obj.activeInHierarchy)
            {
                return false;
            }

            // For UI elements, check CanvasGroup alpha and visibility
            var canvasGroup = obj.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null && canvasGroup.alpha <= 0)
            {
                return false;
            }

            // Check renderer visibility
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && !renderer.enabled)
            {
                return false;
            }

            // Check UI Graphic visibility
            var graphic = obj.GetComponent<Graphic>();
            if (graphic != null && !graphic.enabled)
            {
                return false;
            }

            // For 3D objects, could check if in camera frustum
            // For now, just check if active
            return true;
        }

        private bool IsInteractable(GameObject obj)
        {
            // Check CanvasGroup.interactable
            var canvasGroup = obj.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null && !canvasGroup.interactable)
            {
                return false;
            }

            // Check Selectable.interactable (Button, Toggle, InputField, etc.)
            var selectable = obj.GetComponent<Selectable>();
            if (selectable != null && !selectable.interactable)
            {
                return false;
            }

            // Check if raycast target
            var graphic = obj.GetComponent<Graphic>();
            if (graphic != null && !graphic.raycastTarget)
            {
                return false;
            }

            return IsVisible(obj);
        }

        private string GetPath(GameObject obj)
        {
            if (obj == null) return "(null)";

            var path = obj.name;
            var parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return "/" + path;
        }
    }

    /// <summary>
    /// Custom yield instruction for coroutine-based waiting.
    /// Can be used in test scripts for auto-waiting.
    /// </summary>
    public class WaitForSelectorYield : CustomYieldInstruction
    {
        private readonly string m_selector;
        private readonly string m_state;
        private readonly float m_timeout;
        private readonly float m_startTime;

        public GameObject Result { get; private set; }
        public bool TimedOut { get; private set; }
        public string Error { get; private set; }

        public WaitForSelectorYield(string selector, string state = "visible", float timeout = 5f)
        {
            m_selector = selector;
            m_state = state;
            m_timeout = timeout;
            m_startTime = Time.unscaledTime;
        }

        public override bool keepWaiting
        {
            get
            {
                // Check timeout
                if (Time.unscaledTime - m_startTime > m_timeout)
                {
                    TimedOut = true;
                    Error = $"Timeout waiting for selector: {m_selector}";
                    return false;
                }

                try
                {
                    var results = SelectorEngine.Query(m_selector, m_state == "attached");

                    switch (m_state.ToLower())
                    {
                        case "attached":
                            if (results.Count > 0)
                            {
                                Result = results[0];
                                return false;
                            }
                            break;

                        case "detached":
                            if (results.Count == 0)
                            {
                                return false;
                            }
                            break;

                        case "visible":
                            foreach (var obj in results)
                            {
                                if (obj.activeInHierarchy)
                                {
                                    Result = obj;
                                    return false;
                                }
                            }
                            break;

                        case "hidden":
                            if (results.Count == 0 || !results[0].activeInHierarchy)
                            {
                                return false;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    return false;
                }

                return true;
            }
        }
    }
}
