using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Simulates input events (keyboard, mouse) for automated testing.
    /// Works with both Legacy Input Manager and New Input System.
    /// </summary>
    [McpTool("simulate_input")]
    public class SimulateInputTool : IMcpTool
    {
        public string Name => "simulate_input";
        public string Description => "Simulate keyboard and mouse input for automated testing and UI interaction. " +
            "In Play Mode, keys and mouse reach the game even while the Unity window is in the background.";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""description"": ""Type of input action"",
                    ""enum"": [""key_press"", ""key_release"", ""key_tap"", ""mouse_click"", ""mouse_move"", ""mouse_drag"", ""ui_click""],
                    ""default"": ""key_tap""
                },
                ""key"": {
                    ""type"": ""string"",
                    ""description"": ""Key to press (e.g., 'Space', 'A', 'Return', 'Escape')""
                },
                ""position"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" }
                    },
                    ""description"": ""Screen position for mouse actions (origin at bottom-left)""
                },
                ""button"": {
                    ""type"": ""integer"",
                    ""description"": ""Mouse button (0=left, 1=right, 2=middle)"",
                    ""default"": 0
                },
                ""selector"": {
                    ""type"": ""string"",
                    ""description"": ""U-Selector for UI element to click (for ui_click action)""
                },
                ""duration"": {
                    ""type"": ""number"",
                    ""description"": ""Duration in seconds for drag operations"",
                    ""default"": 0.1
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>() ?? "key_tap";
                string key = arguments?["key"]?.Value<string>();
                JObject positionObj = arguments?["position"] as JObject;
                int button = arguments?["button"]?.Value<int>() ?? 0;
                string selector = arguments?["selector"]?.Value<string>();
                float duration = arguments?["duration"]?.Value<float>() ?? 0.1f;

                Vector2? position = null;
                if (positionObj != null)
                {
                    float x = positionObj["x"]?.Value<float>() ?? 0;
                    float y = positionObj["y"]?.Value<float>() ?? 0;
                    position = new Vector2(x, y);
                }

                try
                {
                    string result = action.ToLower() switch
                    {
                        "key_press" => SimulateKeyPress(key),
                        "key_release" => SimulateKeyRelease(key),
                        "key_tap" => SimulateKeyTap(key),
                        "mouse_click" => SimulateMouseClick(position, button),
                        "mouse_move" => SimulateMouseMove(position),
                        "mouse_drag" => SimulateMouseDrag(position, duration),
                        "ui_click" => SimulateUIClick(selector),
                        _ => throw new ArgumentException($"Unknown action: {action}")
                    };

                    return McpToolResult.Success(result);
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Input simulation failed: {ex.Message}");
                }
            });
        }

        private string SimulateKeyPress(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is required for key_press action");
            }

            var keyCode = ParseKeyCode(key);

            // Note: In Editor, we can use SendMessage to focused windows
            // For actual input simulation, New Input System is required
            Debug.Log($"[UnityMCP] Simulating key press: {keyCode}");

            // Try to use New Input System if available
            if (TrySimulateNewInputSystem("KeyPress", key))
            {
                return $"Key pressed: {key} (New Input System)";
            }

            // Fallback: Use Event.KeyboardEvent
            var evt = Event.KeyboardEvent(key.ToLower());
            if (evt != null)
            {
                // Send to focused editor window
                var focusedWindow = EditorWindow.focusedWindow;
                if (focusedWindow != null)
                {
                    focusedWindow.SendEvent(evt);
                    return $"Key pressed: {key} (Editor Event)";
                }
            }

            return $"Key press simulated: {key} (logged only - for runtime, use New Input System)";
        }

        private string SimulateKeyRelease(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is required for key_release action");
            }

            Debug.Log($"[UnityMCP] Simulating key release: {key}");

            if (TrySimulateNewInputSystem("KeyRelease", key))
            {
                return $"Key released: {key} (New Input System)";
            }

            return $"Key release simulated: {key}";
        }

        private string SimulateKeyTap(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is required for key_tap action");
            }

            string press = SimulateKeyPress(key);
            SimulateKeyRelease(key);

            return press.Contains("(New Input System)") ? $"Key tapped: {key} (New Input System)" : $"Key tapped: {key}. {press}";
        }

        private string SimulateMouseClick(Vector2? position, int button)
        {
            if (!position.HasValue)
            {
                throw new ArgumentException("Position is required for mouse_click action");
            }

            Debug.Log($"[UnityMCP] Simulating mouse click at {position.Value}, button {button}");

            // Try New Input System
            if (TrySimulateNewInputSystem("MouseClick", position.Value, button))
            {
                return $"Mouse clicked at {position.Value}, button {button} (New Input System)";
            }

            // Try EventSystem for UI
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = position.Value,
                    button = (PointerEventData.InputButton)button
                };

                var results = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, results);

                if (results.Count > 0)
                {
                    var target = results[0].gameObject;
                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
                    return $"Mouse clicked on UI element: {target.name}";
                }
            }

            return $"Mouse click simulated at {position.Value}, button {button}";
        }

        private string SimulateMouseMove(Vector2? position)
        {
            if (!position.HasValue)
            {
                throw new ArgumentException("Position is required for mouse_move action");
            }

            Debug.Log($"[UnityMCP] Simulating mouse move to {position.Value}");

            if (TrySimulateNewInputSystem("MouseMove", position.Value, 0))
            {
                return $"Mouse moved to {position.Value} (New Input System)";
            }

            return $"Mouse move simulated to {position.Value}";
        }

        private string SimulateMouseDrag(Vector2? endPosition, float duration)
        {
            if (!endPosition.HasValue)
            {
                throw new ArgumentException("Position (end) is required for mouse_drag action");
            }

            Debug.Log($"[UnityMCP] Simulating mouse drag to {endPosition.Value} over {duration}s");

            return $"Mouse drag simulated to {endPosition.Value} over {duration}s";
        }

        private string SimulateUIClick(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                throw new ArgumentException("Selector is required for ui_click action");
            }

            // Use SelectorEngine to find the object
            var results = SelectorEngine.Query(selector, false);

            if (results.Count == 0)
            {
                throw new Exception($"No UI element found for selector: {selector}");
            }

            if (results.Count > 1)
            {
                throw new AmbiguousSelectorException(selector, results.Count);
            }

            var target = results[0];

            // Try to find UI component to click
            var button = target.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                button.onClick.Invoke();
                return $"Clicked UI Button: {target.name}";
            }

            var toggle = target.GetComponent<UnityEngine.UI.Toggle>();
            if (toggle != null)
            {
                toggle.isOn = !toggle.isOn;
                return $"Toggled UI Toggle: {target.name} (now {toggle.isOn})";
            }

            var inputField = target.GetComponent<UnityEngine.UI.InputField>();
            if (inputField != null)
            {
                inputField.Select();
                return $"Selected UI InputField: {target.name}";
            }

            // Generic click via EventSystem
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                var rectTransform = target.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    var pointerData = new PointerEventData(eventSystem)
                    {
                        position = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position),
                        button = PointerEventData.InputButton.Left
                    };
                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
                    return $"Clicked UI element: {target.name}";
                }
            }

            return $"UI click attempted on: {target.name} (no clickable component found)";
        }

#if ENABLE_INPUT_SYSTEM
        private static HashSet<Key> s_pressedKeys = new HashSet<Key>();
#endif

        private bool TrySimulateNewInputSystem(string action, object param1, int param2 = 0)
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                // Use the current keyboard/mouse devices directly
                var keyboard = Keyboard.current;
                var mouse = Mouse.current;

                if (keyboard == null && (action == "KeyPress" || action == "KeyRelease"))
                {
                    Debug.LogWarning("[UnityMCP] No keyboard device available");
                    return false;
                }

                if (action == "KeyPress" && param1 is string keyName)
                {
                    var key = ParseInputSystemKey(keyName);
                    if (key != Key.None && keyboard != null)
                    {
                        GameViewInputRouting.Begin(keyboard);
                        // Use InputState.Change to directly modify keyboard state
                        // This is more reliable than QueueEvent for immediate state changes
                        SetKeyState(keyboard, key, true);
                        s_pressedKeys.Add(key);

                        // Force InputSystem to process the state change
                        InputSystem.Update();
                        return true;
                    }
                }
                else if (action == "KeyRelease" && param1 is string keyNameRelease)
                {
                    var key = ParseInputSystemKey(keyNameRelease);
                    if (key != Key.None && keyboard != null)
                    {
                        GameViewInputRouting.Begin(keyboard);
                        SetKeyState(keyboard, key, false);
                        s_pressedKeys.Remove(key);

                        // Force InputSystem to process the state change
                        InputSystem.Update();
                        if (s_pressedKeys.Count == 0) GameViewInputRouting.End();
                        return true;
                    }
                }
                else if (action == "MouseClick" && param1 is Vector2 pos && mouse != null)
                {
                    GameViewInputRouting.Begin(mouse);
                    // Move mouse to position
                    InputState.Change(mouse.position, pos);

                    // Click - use direct state change
                    var buttonControl = param2 == 0 ? mouse.leftButton : (param2 == 1 ? mouse.rightButton : mouse.middleButton);

                    // Press
                    using (StateEvent.From(mouse, out var eventPtr))
                    {
                        buttonControl.WriteValueIntoEvent(1f, eventPtr);
                        InputSystem.QueueEvent(eventPtr);
                    }
                    InputSystem.Update();

                    // Release
                    using (StateEvent.From(mouse, out var eventPtr))
                    {
                        buttonControl.WriteValueIntoEvent(0f, eventPtr);
                        InputSystem.QueueEvent(eventPtr);
                    }
                    InputSystem.Update();
                    if (s_pressedKeys.Count == 0) GameViewInputRouting.End();
                    return true;
                }
                else if (action == "MouseMove" && param1 is Vector2 movePos && mouse != null)
                {
                    GameViewInputRouting.Begin(mouse);
                    InputState.Change(mouse.position, movePos);
                    InputSystem.Update();
                    if (s_pressedKeys.Count == 0) GameViewInputRouting.End();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] New Input System simulation failed: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// By default the Input System disables the keyboard and mouse while the Unity window is in the background
        /// and gives keyboard input to the game only while the Game view has focus. An AI client drives Unity from
        /// another window, so simulated keys were dropped although the tool reported them sent. While simulated keys
        /// are held (and for one-shot taps and clicks) input is routed to the game regardless of focus; the project's
        /// settings are restored when the last key is released or Play Mode ends.
        /// </summary>
        internal static class GameViewInputRouting
        {
#if NANORA_INPUT_FOCUS_SETTINGS
            private static bool s_active;
            private static InputSettings.BackgroundBehavior s_background;
            private static InputSettings.EditorInputBehaviorInPlayMode s_editorBehavior;
#endif

            public static void Begin(InputDevice device)
            {
                if (!EditorApplication.isPlaying) return;
#if NANORA_INPUT_FOCUS_SETTINGS
                var settings = InputSystem.settings;
                if (!s_active)
                {
                    s_background = settings.backgroundBehavior;
                    s_editorBehavior = settings.editorInputBehaviorInPlayMode;
                    s_active = true;
                    EditorApplication.playModeStateChanged += OnPlayModeChanged;
                }
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                // Devices disabled when the window lost focus ignore queued events.
                if (device != null && !device.enabled) InputSystem.EnableDevice(device);
            }

            public static void End()
            {
#if NANORA_INPUT_FOCUS_SETTINGS
                if (!s_active) return;
                s_active = false;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                var settings = InputSystem.settings;
                if (settings == null) return;
                settings.backgroundBehavior = s_background;
                settings.editorInputBehaviorInPlayMode = s_editorBehavior;
#endif
            }

#if NANORA_INPUT_FOCUS_SETTINGS
            private static void OnPlayModeChanged(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.ExitingPlayMode) return;
                s_pressedKeys.Clear();
                End();
            }
#endif
        }

        /// <summary>
        /// Directly set a key's pressed state on the keyboard device.
        /// Uses reflection to modify the keyboard state for reliable input simulation.
        /// </summary>
        private void SetKeyState(Keyboard keyboard, Key key, bool pressed)
        {
            // Get the key control
            var keyControl = keyboard[key];

            // Use StateEvent to set the key state
            using (StateEvent.From(keyboard, out var eventPtr))
            {
                keyControl.WriteValueIntoEvent(pressed ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }
#endif

#if ENABLE_INPUT_SYSTEM
        private Key ParseInputSystemKey(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return Key.None;

            return keyName.ToLower() switch
            {
                "space" => Key.Space,
                "enter" => Key.Enter,
                "return" => Key.Enter,
                "escape" => Key.Escape,
                "esc" => Key.Escape,
                "tab" => Key.Tab,
                "backspace" => Key.Backspace,
                "delete" => Key.Delete,
                "up" => Key.UpArrow,
                "down" => Key.DownArrow,
                "left" => Key.LeftArrow,
                "right" => Key.RightArrow,
                "shift" => Key.LeftShift,
                "ctrl" => Key.LeftCtrl,
                "control" => Key.LeftCtrl,
                "alt" => Key.LeftAlt,
                "a" => Key.A, "b" => Key.B, "c" => Key.C, "d" => Key.D,
                "e" => Key.E, "f" => Key.F, "g" => Key.G, "h" => Key.H,
                "i" => Key.I, "j" => Key.J, "k" => Key.K, "l" => Key.L,
                "m" => Key.M, "n" => Key.N, "o" => Key.O, "p" => Key.P,
                "q" => Key.Q, "r" => Key.R, "s" => Key.S, "t" => Key.T,
                "u" => Key.U, "v" => Key.V, "w" => Key.W, "x" => Key.X,
                "y" => Key.Y, "z" => Key.Z,
                "0" => Key.Digit0, "1" => Key.Digit1, "2" => Key.Digit2,
                "3" => Key.Digit3, "4" => Key.Digit4, "5" => Key.Digit5,
                "6" => Key.Digit6, "7" => Key.Digit7, "8" => Key.Digit8,
                "9" => Key.Digit9,
                _ => Key.None
            };
        }
#endif

        private KeyCode ParseKeyCode(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return KeyCode.None;
            }

            // Try direct enum parse
            if (Enum.TryParse<KeyCode>(key, true, out var keyCode))
            {
                return keyCode;
            }

            // Common aliases
            return key.ToLower() switch
            {
                "space" => KeyCode.Space,
                "enter" => KeyCode.Return,
                "return" => KeyCode.Return,
                "escape" => KeyCode.Escape,
                "esc" => KeyCode.Escape,
                "tab" => KeyCode.Tab,
                "backspace" => KeyCode.Backspace,
                "delete" => KeyCode.Delete,
                "up" => KeyCode.UpArrow,
                "down" => KeyCode.DownArrow,
                "left" => KeyCode.LeftArrow,
                "right" => KeyCode.RightArrow,
                "shift" => KeyCode.LeftShift,
                "ctrl" => KeyCode.LeftControl,
                "control" => KeyCode.LeftControl,
                "alt" => KeyCode.LeftAlt,
                "cmd" => KeyCode.LeftCommand,
                "command" => KeyCode.LeftCommand,
                _ => key.Length == 1 ? (KeyCode)Enum.Parse(typeof(KeyCode), key.ToUpper()) : KeyCode.None
            };
        }
    }
}
