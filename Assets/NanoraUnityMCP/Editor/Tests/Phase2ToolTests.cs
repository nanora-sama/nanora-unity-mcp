using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using NanoraUnityMCP.Core;
using NanoraUnityMCP.Tools;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// EditMode tests for Phase 2 tools (Animation, UI, Audio, Lighting, Camera).
    /// </summary>
    [TestFixture]
    public class Phase2ToolTests
    {
        #region Animation Tools Tests

        [Test]
        public async Task ControlAnimator_GetInfo_ReturnsInfo()
        {
            var go = new GameObject("AnimatorTest");
            var animator = go.AddComponent<Animator>();

            try
            {
                var tool = new ControlAnimatorTool();
                var args = new JObject
                {
                    ["object_name"] = "AnimatorTest",
                    ["action"] = "get_info"
                };

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("Animator Info"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task ControlAnimator_NoAnimator_ReturnsError()
        {
            var go = new GameObject("NoAnimatorTest");

            try
            {
                var tool = new ControlAnimatorTool();
                var args = new JObject
                {
                    ["object_name"] = "NoAnimatorTest",
                    ["action"] = "get_info"
                };

                var result = await tool.ExecuteAsync(args);

                Assert.IsTrue(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("No Animator"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task ManageAnimationClip_List_ReturnsList()
        {
            var tool = new ManageAnimationClipTool();
            var args = new JObject
            {
                ["action"] = "list",
                ["folder_path"] = "Assets"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Animation Clips"));
        }

        #endregion

        #region UI Tools Tests

        [Test]
        public async Task CreateUIElement_Button_CreatesButton()
        {
            var tool = new CreateUIElementTool();
            var args = new JObject
            {
                ["element_type"] = "button",
                ["name"] = "TestButton"
            };

            var result = await tool.ExecuteAsync(args);
            var button = GameObject.Find("TestButton");

            try
            {
                Assert.IsFalse(result.IsError);
                Assert.IsNotNull(button);
                Assert.IsNotNull(button.GetComponent<Button>());
            }
            finally
            {
                // Clean up Canvas and EventSystem created
                var canvas = UnityCompatibility.FindFirstObjectByType<Canvas>();
                if (canvas != null)
                    Object.DestroyImmediate(canvas.gameObject);

                var eventSystem = UnityCompatibility.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                if (eventSystem != null)
                    Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        [Test]
        public async Task ManageCanvas_List_ReturnsCanvasList()
        {
            var canvasGO = new GameObject("TestCanvas");
            canvasGO.AddComponent<Canvas>();

            try
            {
                var tool = new ManageCanvasTool();
                var args = new JObject
                {
                    ["action"] = "list"
                };

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("TestCanvas"));
            }
            finally
            {
                Object.DestroyImmediate(canvasGO);
            }
        }

        #endregion

        #region Audio Tools Tests

        [Test]
        public async Task ManageAudioSource_List_ReturnsAudioSources()
        {
            var go = new GameObject("AudioTest");
            go.AddComponent<AudioSource>();

            try
            {
                var tool = new ManageAudioSourceTool();
                var args = new JObject
                {
                    ["action"] = "list"
                };

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("AudioTest"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task ManageAudioSource_Create_CreatesAudioSource()
        {
            var tool = new ManageAudioSourceTool();
            var args = new JObject
            {
                ["action"] = "create",
                ["object_name"] = "NewAudioSource",
                ["volume"] = 0.5f
            };

            var result = await tool.ExecuteAsync(args);
            var created = GameObject.Find("NewAudioSource");

            try
            {
                Assert.IsFalse(result.IsError);
                Assert.IsNotNull(created);
                var source = created.GetComponent<AudioSource>();
                Assert.IsNotNull(source);
                Assert.AreEqual(0.5f, source.volume, 0.01f);
            }
            finally
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
        }

        [Test]
        public async Task PlayAudio_SearchClips_ReturnsResults()
        {
            var tool = new PlayAudioTool();
            var args = new JObject
            {
                ["action"] = "search_clips",
                ["search_query"] = "default"
            };

            var result = await tool.ExecuteAsync(args);

            // Should not error even if no clips found
            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("AudioClips"));
        }

        #endregion

        #region Lighting Tools Tests

        [Test]
        public async Task ManageLight_List_ReturnsLights()
        {
            var go = new GameObject("TestLight");
            go.AddComponent<Light>();

            try
            {
                var tool = new ManageLightTool();
                var args = new JObject
                {
                    ["action"] = "list"
                };

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("TestLight"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task ManageLight_Create_CreatesLight()
        {
            var tool = new ManageLightTool();
            var args = new JObject
            {
                ["action"] = "create",
                ["light_name"] = "NewPointLight",
                ["light_type"] = "point",
                ["color"] = "#FF0000",
                ["intensity"] = 2.0f
            };

            var result = await tool.ExecuteAsync(args);
            var created = GameObject.Find("NewPointLight");

            try
            {
                Assert.IsFalse(result.IsError);
                Assert.IsNotNull(created);
                var light = created.GetComponent<Light>();
                Assert.IsNotNull(light);
                Assert.AreEqual(LightType.Point, light.type);
                Assert.AreEqual(2.0f, light.intensity, 0.01f);
            }
            finally
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
        }

        [Test]
        public async Task ConfigureLighting_GetInfo_ReturnsSettings()
        {
            var tool = new ConfigureLightingTool();
            var args = new JObject
            {
                ["action"] = "get_info"
            };

            var result = await tool.ExecuteAsync(args);

            Assert.IsFalse(result.IsError);
            Assert.IsTrue(result.Content[0].Text.Contains("Ambient"));
            Assert.IsTrue(result.Content[0].Text.Contains("Fog"));
        }

        #endregion

        #region Camera Tools Tests

        [Test]
        public async Task ControlCamera_List_ReturnsCameras()
        {
            var go = new GameObject("TestCam");
            go.AddComponent<Camera>();

            try
            {
                var tool = new ControlCameraTool();
                var args = new JObject
                {
                    ["action"] = "list"
                };

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                Assert.IsTrue(result.Content[0].Text.Contains("TestCam"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task ControlCamera_Create_CreatesCamera()
        {
            var tool = new ControlCameraTool();
            var args = new JObject
            {
                ["action"] = "create",
                ["camera_name"] = "NewCamera",
                ["field_of_view"] = 75.0f
            };

            var result = await tool.ExecuteAsync(args);
            var created = GameObject.Find("NewCamera");

            try
            {
                Assert.IsFalse(result.IsError);
                Assert.IsNotNull(created);
                var camera = created.GetComponent<Camera>();
                Assert.IsNotNull(camera);
                Assert.AreEqual(75.0f, camera.fieldOfView, 0.01f);
            }
            finally
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
        }

        [Test]
        public async Task ControlCamera_LookAt_RotatesCamera()
        {
            var cameraGO = new GameObject("LookAtCamera");
            cameraGO.AddComponent<Camera>();
            cameraGO.transform.position = Vector3.zero;

            var targetGO = new GameObject("LookAtTarget");
            targetGO.transform.position = new Vector3(0, 0, 10);

            try
            {
                var tool = new ControlCameraTool();
                var args = new JObject
                {
                    ["action"] = "look_at",
                    ["camera_name"] = "LookAtCamera",
                    ["target_name"] = "LookAtTarget"
                };

                var result = await tool.ExecuteAsync(args);

                Assert.IsFalse(result.IsError);
                // Camera should now be looking toward +Z
                Assert.IsTrue(cameraGO.transform.forward.z > 0.9f);
            }
            finally
            {
                Object.DestroyImmediate(cameraGO);
                Object.DestroyImmediate(targetGO);
            }
        }

        [Test]
        public async Task SetupCinemachine_CheckInstalled_ReturnsStatus()
        {
            var tool = new SetupCinemachineTool();
            var args = new JObject
            {
                ["action"] = "check_installed"
            };

            var result = await tool.ExecuteAsync(args);

            // Should not error regardless of whether Cinemachine is installed
            Assert.IsFalse(result.IsError);
            // Should mention installation status
            Assert.IsTrue(result.Content[0].Text.Contains("Cinemachine"));
        }

        #endregion

        #region ToolCategories Phase 2 Tests

        [Test]
        public void ToolCategories_ContainsPhase2Categories()
        {
            Assert.IsTrue(Core.ToolCategories.Categories.ContainsKey("animation"));
            Assert.IsTrue(Core.ToolCategories.Categories.ContainsKey("ui"));
            Assert.IsTrue(Core.ToolCategories.Categories.ContainsKey("audio"));
            Assert.IsTrue(Core.ToolCategories.Categories.ContainsKey("lighting"));
            Assert.IsTrue(Core.ToolCategories.Categories.ContainsKey("camera"));
        }

        [Test]
        public void ToolCategories_Phase2ToolsHaveBriefs()
        {
            var phase2Tools = new[]
            {
                "control_animator", "manage_animation_clip",
                "create_ui_element", "manage_canvas",
                "manage_audio_source", "play_audio",
                "manage_light", "configure_lighting",
                "control_camera", "setup_cinemachine"
            };

            foreach (var tool in phase2Tools)
            {
                Assert.IsTrue(Core.ToolCategories.ToolBriefs.ContainsKey(tool), $"Missing brief for {tool}");
            }
        }

        #endregion
    }
}
