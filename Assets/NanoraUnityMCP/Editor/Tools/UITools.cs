using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Protocol;
#if TEXTMESHPRO_INSTALLED
using TMPro;
#endif

namespace NanoraUnityMCP.Tools
{
    /// <summary>
    /// Tool for creating UI elements in Unity UI system.
    /// </summary>
    [McpTool("create_ui_element")]
    public class CreateUIElementTool : IMcpTool
    {
        public string Name => "create_ui_element";
        public string Description => "Create UI elements: Button, Text, Image, Panel, InputField, Slider, Toggle, Dropdown";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""element_type"": {
                    ""type"": ""string"",
                    ""enum"": [""button"", ""text"", ""image"", ""panel"", ""input_field"", ""slider"", ""toggle"", ""dropdown"", ""scroll_view"", ""raw_image""],
                    ""description"": ""Type of UI element to create""
                },
                ""name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the UI element""
                },
                ""parent"": {
                    ""type"": ""string"",
                    ""description"": ""Parent object name (must be under a Canvas, or leave empty to create on root Canvas)""
                },
                ""text"": {
                    ""type"": ""string"",
                    ""description"": ""Text content for Button, Text, or InputField placeholder""
                },
                ""position"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" }
                    },
                    ""description"": ""Anchored position (x, y)""
                },
                ""size"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""width"": { ""type"": ""number"" },
                        ""height"": { ""type"": ""number"" }
                    },
                    ""description"": ""Size delta (width, height)""
                },
                ""color"": {
                    ""type"": ""string"",
                    ""description"": ""Color in hex format (e.g., #FF0000)""
                },
                ""font_size"": {
                    ""type"": ""integer"",
                    ""description"": ""Font size for text elements"",
                    ""default"": 14
                },
                ""sprite_path"": {
                    ""type"": ""string"",
                    ""description"": ""Asset path to sprite for Image element""
                },
                ""use_tmp"": {
                    ""type"": ""boolean"",
                    ""description"": ""Use TextMeshPro instead of legacy Text (requires TMP package)"",
                    ""default"": false
                }
            },
            ""required"": [""element_type"", ""name""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string elementType = arguments?["element_type"]?.Value<string>();
                string name = arguments?["name"]?.Value<string>();
                string parent = arguments?["parent"]?.Value<string>();
                string text = arguments?["text"]?.Value<string>();
                var positionObj = arguments?["position"];
                var sizeObj = arguments?["size"];
                string colorHex = arguments?["color"]?.Value<string>();
                int fontSize = arguments?["font_size"]?.Value<int>() ?? 14;
                string spritePath = arguments?["sprite_path"]?.Value<string>();
                bool useTmp = arguments?["use_tmp"]?.Value<bool>() ?? false;

                if (string.IsNullOrWhiteSpace(elementType))
                {
                    return McpToolResult.Error("element_type is required");
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    return McpToolResult.Error("name is required");
                }

                try
                {
                    // Find or create canvas
                    Transform parentTransform = FindOrCreateParent(parent);
                    if (parentTransform == null)
                    {
                        return McpToolResult.Error("Failed to find or create parent Canvas");
                    }

                    // Create the UI element
                    GameObject uiElement = CreateElement(elementType, name, parentTransform, text, fontSize, spritePath, useTmp);
                    if (uiElement == null)
                    {
                        return McpToolResult.Error($"Unknown element_type: {elementType}");
                    }

                    // Apply position and size
                    var rectTransform = uiElement.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        if (positionObj != null)
                        {
                            float x = positionObj["x"]?.Value<float>() ?? 0;
                            float y = positionObj["y"]?.Value<float>() ?? 0;
                            rectTransform.anchoredPosition = new Vector2(x, y);
                        }

                        if (sizeObj != null)
                        {
                            float width = sizeObj["width"]?.Value<float>() ?? rectTransform.sizeDelta.x;
                            float height = sizeObj["height"]?.Value<float>() ?? rectTransform.sizeDelta.y;
                            rectTransform.sizeDelta = new Vector2(width, height);
                        }
                    }

                    // Apply color
                    if (!string.IsNullOrWhiteSpace(colorHex))
                    {
                        if (ColorUtility.TryParseHtmlString(colorHex, out Color color))
                        {
                            var graphic = uiElement.GetComponent<Graphic>();
                            if (graphic != null)
                            {
                                graphic.color = color;
                            }
                        }
                    }

                    Undo.RegisterCreatedObjectUndo(uiElement, $"Create UI {elementType}");
                    Selection.activeGameObject = uiElement;

                    return McpToolResult.Success($"Created UI element: {name} ({elementType})\nParent: {parentTransform.name}");
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Failed to create UI element: {ex.Message}");
                }
            });
        }

        private Transform FindOrCreateParent(string parentName)
        {
            // Find specified parent
            if (!string.IsNullOrWhiteSpace(parentName))
            {
                var parent = GameObject.Find(parentName);
                if (parent != null && parent.GetComponentInParent<Canvas>() != null)
                {
                    return parent.transform;
                }
            }

            // Find existing canvas
            var canvas = UnityCompatibility.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                return canvas.transform;
            }

            // Create new canvas
            var canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (UnityCompatibility.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            return canvas.transform;
        }

        private GameObject CreateElement(string type, string name, Transform parent, string text, int fontSize, string spritePath, bool useTmp)
        {
            GameObject go = null;

            switch (type.ToLowerInvariant())
            {
                case "button":
                    go = CreateButton(name, parent, text ?? "Button", fontSize, useTmp);
                    break;
                case "text":
                    go = CreateText(name, parent, text ?? "Text", fontSize, useTmp);
                    break;
                case "image":
                    go = CreateImage(name, parent, spritePath);
                    break;
                case "raw_image":
                    go = CreateRawImage(name, parent);
                    break;
                case "panel":
                    go = CreatePanel(name, parent);
                    break;
                case "input_field":
                    go = CreateInputField(name, parent, text, fontSize, useTmp);
                    break;
                case "slider":
                    go = CreateSlider(name, parent);
                    break;
                case "toggle":
                    go = CreateToggle(name, parent, text ?? "Toggle", fontSize, useTmp);
                    break;
                case "dropdown":
                    go = CreateDropdown(name, parent, fontSize, useTmp);
                    break;
                case "scroll_view":
                    go = CreateScrollView(name, parent);
                    break;
            }

            return go;
        }

        private GameObject CreateButton(string name, Transform parent, string text, int fontSize, bool useTmp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            // Add text child
            var textGO = CreateTextObject("Text", go.transform, text, fontSize, useTmp);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return go;
        }

        private GameObject CreateText(string name, Transform parent, string text, int fontSize, bool useTmp)
        {
            return CreateTextObject(name, parent, text, fontSize, useTmp);
        }

        private GameObject CreateTextObject(string name, Transform parent, string text, int fontSize, bool useTmp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);

#if TEXTMESHPRO_INSTALLED
            if (useTmp)
            {
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }
            else
#endif
            {
                var textComponent = go.AddComponent<Text>();
                textComponent.text = text;
                textComponent.fontSize = fontSize;
                textComponent.alignment = TextAnchor.MiddleCenter;
                textComponent.color = Color.white;
            }

            return go;
        }

        private GameObject CreateImage(string name, Transform parent, string spritePath)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 100);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

            if (!string.IsNullOrWhiteSpace(spritePath))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                {
                    image.sprite = sprite;
                }
            }

            return go;
        }

        private GameObject CreateRawImage(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 100);

            go.AddComponent<RawImage>();

            return go;
        }

        private GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.5f);

            return go;
        }

        private GameObject CreateInputField(string name, Transform parent, string placeholder, int fontSize, bool useTmp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

#if TEXTMESHPRO_INSTALLED
            if (useTmp)
            {
                var inputField = go.AddComponent<TMP_InputField>();

                var textArea = new GameObject("Text Area");
                textArea.transform.SetParent(go.transform, false);
                var textAreaRect = textArea.AddComponent<RectTransform>();
                textAreaRect.anchorMin = Vector2.zero;
                textAreaRect.anchorMax = Vector2.one;
                textAreaRect.sizeDelta = new Vector2(-10, -6);
                textArea.AddComponent<RectMask2D>();

                var placeholderGO = new GameObject("Placeholder");
                placeholderGO.transform.SetParent(textArea.transform, false);
                var placeholderRect = placeholderGO.AddComponent<RectTransform>();
                placeholderRect.anchorMin = Vector2.zero;
                placeholderRect.anchorMax = Vector2.one;
                placeholderRect.sizeDelta = Vector2.zero;
                var placeholderTmp = placeholderGO.AddComponent<TextMeshProUGUI>();
                placeholderTmp.text = placeholder ?? "Enter text...";
                placeholderTmp.fontSize = fontSize;
                placeholderTmp.fontStyle = FontStyles.Italic;
                placeholderTmp.color = new Color(0.5f, 0.5f, 0.5f);

                var textGO = new GameObject("Text");
                textGO.transform.SetParent(textArea.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                var textTmp = textGO.AddComponent<TextMeshProUGUI>();
                textTmp.fontSize = fontSize;
                textTmp.color = Color.black;

                inputField.textViewport = textAreaRect;
                inputField.placeholder = placeholderTmp;
                inputField.textComponent = textTmp;
            }
            else
#endif
            {
                var inputField = go.AddComponent<InputField>();

                var textGO = new GameObject("Text");
                textGO.transform.SetParent(go.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = new Vector2(-10, -6);
                var textComponent = textGO.AddComponent<Text>();
                textComponent.fontSize = fontSize;
                textComponent.color = Color.black;
                textComponent.supportRichText = false;

                var placeholderGO = new GameObject("Placeholder");
                placeholderGO.transform.SetParent(go.transform, false);
                var placeholderRect = placeholderGO.AddComponent<RectTransform>();
                placeholderRect.anchorMin = Vector2.zero;
                placeholderRect.anchorMax = Vector2.one;
                placeholderRect.sizeDelta = new Vector2(-10, -6);
                var placeholderText = placeholderGO.AddComponent<Text>();
                placeholderText.text = placeholder ?? "Enter text...";
                placeholderText.fontSize = fontSize;
                placeholderText.fontStyle = FontStyle.Italic;
                placeholderText.color = new Color(0.5f, 0.5f, 0.5f);

                inputField.textComponent = textComponent;
                inputField.placeholder = placeholderText;
            }

            return go;
        }

        private GameObject CreateSlider(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 20);

            var slider = go.AddComponent<Slider>();

            // Background
            var background = new GameObject("Background");
            background.transform.SetParent(go.transform, false);
            var bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f);

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.sizeDelta = new Vector2(-20, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.sizeDelta = new Vector2(10, 0);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f);

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-20, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            return go;
        }

        private GameObject CreateToggle(string name, Transform parent, string label, int fontSize, bool useTmp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 20);

            var toggle = go.AddComponent<Toggle>();

            // Background
            var background = new GameObject("Background");
            background.transform.SetParent(go.transform, false);
            var bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 1);
            bgRect.anchorMax = new Vector2(0, 1);
            bgRect.pivot = new Vector2(0, 1);
            bgRect.sizeDelta = new Vector2(20, 20);
            bgRect.anchoredPosition = Vector2.zero;
            var bgImage = background.AddComponent<Image>();
            bgImage.color = Color.white;

            // Checkmark
            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            var checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(20, 20);
            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.6f, 1f);

            // Label
            var labelGO = CreateTextObject("Label", go.transform, label, fontSize, useTmp);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(23, 1);
            labelRect.offsetMax = new Vector2(-5, -2);

            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;

            return go;
        }

        private GameObject CreateDropdown(string name, Transform parent, int fontSize, bool useTmp)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(160, 30);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

#if TEXTMESHPRO_INSTALLED
            if (useTmp)
            {
                var dropdown = go.AddComponent<TMP_Dropdown>();

                var label = new GameObject("Label");
                label.transform.SetParent(go.transform, false);
                var labelRect = label.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = new Vector2(-35, -6);
                var labelTmp = label.AddComponent<TextMeshProUGUI>();
                labelTmp.text = "Option A";
                labelTmp.fontSize = fontSize;
                labelTmp.alignment = TextAlignmentOptions.Left;
                labelTmp.color = Color.black;

                dropdown.captionText = labelTmp;
                dropdown.options.Add(new TMP_Dropdown.OptionData("Option A"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Option B"));
                dropdown.options.Add(new TMP_Dropdown.OptionData("Option C"));
            }
            else
#endif
            {
                var dropdown = go.AddComponent<Dropdown>();

                var label = new GameObject("Label");
                label.transform.SetParent(go.transform, false);
                var labelRect = label.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = new Vector2(-35, -6);
                var labelText = label.AddComponent<Text>();
                labelText.text = "Option A";
                labelText.fontSize = fontSize;
                labelText.alignment = TextAnchor.MiddleLeft;
                labelText.color = Color.black;

                dropdown.captionText = labelText;
                dropdown.options.Add(new Dropdown.OptionData("Option A"));
                dropdown.options.Add(new Dropdown.OptionData("Option B"));
                dropdown.options.Add(new Dropdown.OptionData("Option C"));
            }

            return go;
        }

        private GameObject CreateScrollView(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rectTransform = go.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 200);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f);

            var scrollRect = go.AddComponent<ScrollRect>();

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(go.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0, 1);
            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(0, 300);

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            return go;
        }
    }

    /// <summary>
    /// Tool for managing Canvas and UI layout.
    /// </summary>
    [McpTool("manage_canvas")]
    public class ManageCanvasTool : IMcpTool
    {
        public string Name => "manage_canvas";
        public string Description => "Manage Canvas settings: render mode, sorting, scaler, and get UI hierarchy";

        public JObject InputSchema => JObject.Parse(@"{
            ""type"": ""object"",
            ""properties"": {
                ""action"": {
                    ""type"": ""string"",
                    ""enum"": [""create"", ""configure"", ""info"", ""list"", ""get_hierarchy""],
                    ""description"": ""Action to perform""
                },
                ""canvas_name"": {
                    ""type"": ""string"",
                    ""description"": ""Name of the Canvas""
                },
                ""render_mode"": {
                    ""type"": ""string"",
                    ""enum"": [""screen_space_overlay"", ""screen_space_camera"", ""world_space""],
                    ""description"": ""Canvas render mode""
                },
                ""camera_name"": {
                    ""type"": ""string"",
                    ""description"": ""Camera name for ScreenSpaceCamera mode""
                },
                ""sort_order"": {
                    ""type"": ""integer"",
                    ""description"": ""Sorting order for the canvas""
                },
                ""scale_mode"": {
                    ""type"": ""string"",
                    ""enum"": [""constant_pixel_size"", ""scale_with_screen_size"", ""constant_physical_size""],
                    ""description"": ""Canvas scaler mode""
                },
                ""reference_resolution"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""width"": { ""type"": ""number"" },
                        ""height"": { ""type"": ""number"" }
                    },
                    ""description"": ""Reference resolution for ScaleWithScreenSize mode""
                },
                ""match_width_or_height"": {
                    ""type"": ""number"",
                    ""description"": ""Match width (0) or height (1) for scaling"",
                    ""minimum"": 0,
                    ""maximum"": 1
                }
            },
            ""required"": [""action""]
        }");

        public Task<McpToolResult> ExecuteAsync(JObject arguments)
        {
            return MainThreadDispatcher.RunAsync(() =>
            {
                string action = arguments?["action"]?.Value<string>();
                string canvasName = arguments?["canvas_name"]?.Value<string>();
                string renderMode = arguments?["render_mode"]?.Value<string>();
                string cameraName = arguments?["camera_name"]?.Value<string>();
                int? sortOrder = arguments?["sort_order"]?.Value<int>();
                string scaleMode = arguments?["scale_mode"]?.Value<string>();
                var refResolution = arguments?["reference_resolution"];
                float? matchWidthOrHeight = arguments?["match_width_or_height"]?.Value<float>();

                if (string.IsNullOrWhiteSpace(action))
                {
                    return McpToolResult.Error("action is required");
                }

                try
                {
                    return action.ToLowerInvariant() switch
                    {
                        "create" => CreateCanvas(canvasName, renderMode, scaleMode, refResolution, matchWidthOrHeight),
                        "configure" => ConfigureCanvas(canvasName, renderMode, cameraName, sortOrder, scaleMode, refResolution, matchWidthOrHeight),
                        "info" => GetCanvasInfo(canvasName),
                        "list" => ListCanvases(),
                        "get_hierarchy" => GetUIHierarchy(canvasName),
                        _ => McpToolResult.Error($"Unknown action: {action}")
                    };
                }
                catch (Exception ex)
                {
                    return McpToolResult.Error($"Canvas operation failed: {ex.Message}");
                }
            });
        }

        private McpToolResult CreateCanvas(string name, string renderMode, string scaleMode, JToken refResolution, float? match)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Canvas";
            }

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            var scaler = go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            // Set render mode
            canvas.renderMode = renderMode?.ToLowerInvariant() switch
            {
                "screen_space_camera" => RenderMode.ScreenSpaceCamera,
                "world_space" => RenderMode.WorldSpace,
                _ => RenderMode.ScreenSpaceOverlay
            };

            // Set scaler mode
            if (!string.IsNullOrWhiteSpace(scaleMode))
            {
                scaler.uiScaleMode = scaleMode.ToLowerInvariant() switch
                {
                    "scale_with_screen_size" => CanvasScaler.ScaleMode.ScaleWithScreenSize,
                    "constant_physical_size" => CanvasScaler.ScaleMode.ConstantPhysicalSize,
                    _ => CanvasScaler.ScaleMode.ConstantPixelSize
                };
            }

            if (refResolution != null)
            {
                float width = refResolution["width"]?.Value<float>() ?? 1920;
                float height = refResolution["height"]?.Value<float>() ?? 1080;
                scaler.referenceResolution = new Vector2(width, height);
            }

            if (match.HasValue)
            {
                scaler.matchWidthOrHeight = match.Value;
            }

            // Ensure EventSystem exists
            if (UnityCompatibility.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");

            return McpToolResult.Success($"Created Canvas: {name}\n- Render Mode: {canvas.renderMode}\n- Scale Mode: {scaler.uiScaleMode}");
        }

        private McpToolResult ConfigureCanvas(string canvasName, string renderMode, string cameraName, int? sortOrder, string scaleMode, JToken refResolution, float? match)
        {
            Canvas canvas = FindCanvas(canvasName);
            if (canvas == null)
            {
                return McpToolResult.Error($"Canvas not found: {canvasName ?? "(any)"}");
            }

            Undo.RecordObject(canvas, "Configure Canvas");

            var changes = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(renderMode))
            {
                canvas.renderMode = renderMode.ToLowerInvariant() switch
                {
                    "screen_space_camera" => RenderMode.ScreenSpaceCamera,
                    "world_space" => RenderMode.WorldSpace,
                    _ => RenderMode.ScreenSpaceOverlay
                };
                changes.AppendLine($"- Render Mode: {canvas.renderMode}");
            }

            if (!string.IsNullOrWhiteSpace(cameraName) && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                var cameraObj = GameObject.Find(cameraName);
                if (cameraObj != null)
                {
                    var cam = cameraObj.GetComponent<Camera>();
                    if (cam != null)
                    {
                        canvas.worldCamera = cam;
                        changes.AppendLine($"- Camera: {cameraName}");
                    }
                }
            }

            if (sortOrder.HasValue)
            {
                canvas.sortingOrder = sortOrder.Value;
                changes.AppendLine($"- Sort Order: {sortOrder.Value}");
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Undo.RecordObject(scaler, "Configure Canvas Scaler");

                if (!string.IsNullOrWhiteSpace(scaleMode))
                {
                    scaler.uiScaleMode = scaleMode.ToLowerInvariant() switch
                    {
                        "scale_with_screen_size" => CanvasScaler.ScaleMode.ScaleWithScreenSize,
                        "constant_physical_size" => CanvasScaler.ScaleMode.ConstantPhysicalSize,
                        _ => CanvasScaler.ScaleMode.ConstantPixelSize
                    };
                    changes.AppendLine($"- Scale Mode: {scaler.uiScaleMode}");
                }

                if (refResolution != null)
                {
                    float width = refResolution["width"]?.Value<float>() ?? scaler.referenceResolution.x;
                    float height = refResolution["height"]?.Value<float>() ?? scaler.referenceResolution.y;
                    scaler.referenceResolution = new Vector2(width, height);
                    changes.AppendLine($"- Reference Resolution: {width}x{height}");
                }

                if (match.HasValue)
                {
                    scaler.matchWidthOrHeight = match.Value;
                    changes.AppendLine($"- Match Width/Height: {match.Value}");
                }
            }

            EditorUtility.SetDirty(canvas.gameObject);

            return McpToolResult.Success($"Configured Canvas: {canvas.name}\n{changes}");
        }

        private McpToolResult GetCanvasInfo(string canvasName)
        {
            Canvas canvas = FindCanvas(canvasName);
            if (canvas == null)
            {
                return McpToolResult.Error($"Canvas not found: {canvasName ?? "(any)"}");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Canvas: {canvas.name}");
            sb.AppendLine();
            sb.AppendLine($"**Render Mode:** {canvas.renderMode}");
            sb.AppendLine($"**Pixel Perfect:** {canvas.pixelPerfect}");
            sb.AppendLine($"**Sort Order:** {canvas.sortingOrder}");
            sb.AppendLine($"**Target Display:** {canvas.targetDisplay}");

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                sb.AppendLine($"**Camera:** {(canvas.worldCamera != null ? canvas.worldCamera.name : "(none)")}");
                sb.AppendLine($"**Plane Distance:** {canvas.planeDistance}");
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                sb.AppendLine();
                sb.AppendLine("## Canvas Scaler");
                sb.AppendLine($"**Scale Mode:** {scaler.uiScaleMode}");
                sb.AppendLine($"**Reference Resolution:** {scaler.referenceResolution}");
                sb.AppendLine($"**Match Width/Height:** {scaler.matchWidthOrHeight}");
                sb.AppendLine($"**Scale Factor:** {scaler.scaleFactor}");
            }

            // Count UI elements
            int totalElements = canvas.GetComponentsInChildren<RectTransform>(true).Length - 1; // Exclude canvas itself
            sb.AppendLine();
            sb.AppendLine($"**Total UI Elements:** {totalElements}");

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult ListCanvases()
        {
            var canvases = UnityCompatibility.FindObjectsByType<Canvas>();
            var sb = new StringBuilder();
            sb.AppendLine("# Canvases in Scene");
            sb.AppendLine();
            sb.AppendLine($"Found {canvases.Length} canvas(es):");
            sb.AppendLine();

            foreach (var canvas in canvases)
            {
                int childCount = canvas.GetComponentsInChildren<RectTransform>(true).Length - 1;
                sb.AppendLine($"- {canvas.name} ({canvas.renderMode}, order={canvas.sortingOrder}, {childCount} elements)");
            }

            return McpToolResult.Success(sb.ToString());
        }

        private McpToolResult GetUIHierarchy(string canvasName)
        {
            Canvas canvas = FindCanvas(canvasName);
            if (canvas == null)
            {
                return McpToolResult.Error($"Canvas not found: {canvasName ?? "(any)"}");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# UI Hierarchy: {canvas.name}");
            sb.AppendLine();

            BuildUIHierarchy(sb, canvas.transform, 0);

            return McpToolResult.Success(sb.ToString());
        }

        private void BuildUIHierarchy(StringBuilder sb, Transform parent, int depth)
        {
            string indent = new string(' ', depth * 2);

            foreach (Transform child in parent)
            {
                var components = new System.Collections.Generic.List<string>();

                if (child.GetComponent<Button>()) components.Add("Button");
                if (child.GetComponent<Text>()) components.Add("Text");
                if (child.GetComponent<Image>()) components.Add("Image");
                if (child.GetComponent<InputField>()) components.Add("InputField");
                if (child.GetComponent<Slider>()) components.Add("Slider");
                if (child.GetComponent<Toggle>()) components.Add("Toggle");
                if (child.GetComponent<Dropdown>()) components.Add("Dropdown");
                if (child.GetComponent<ScrollRect>()) components.Add("ScrollRect");
#if TEXTMESHPRO_INSTALLED
                if (child.GetComponent<TextMeshProUGUI>()) components.Add("TMP");
                if (child.GetComponent<TMP_InputField>()) components.Add("TMP_InputField");
                if (child.GetComponent<TMP_Dropdown>()) components.Add("TMP_Dropdown");
#endif

                string componentStr = components.Count > 0 ? $" [{string.Join(", ", components)}]" : "";
                string activeStr = child.gameObject.activeSelf ? "" : " (inactive)";

                sb.AppendLine($"{indent}- {child.name}{componentStr}{activeStr}");

                if (child.childCount > 0 && depth < 5)
                {
                    BuildUIHierarchy(sb, child, depth + 1);
                }
            }
        }

        private Canvas FindCanvas(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    return go.GetComponent<Canvas>();
                }
            }

            return UnityCompatibility.FindFirstObjectByType<Canvas>();
        }
    }
}
