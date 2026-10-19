using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NanoraUnityMCP.Samples.ShootingGame.Editor
{
    /// <summary>
    /// Builds Scenes/ShootingGame.unity inside the imported sample folder. The scene is made in the importing
    /// project's own Unity version and render pipeline, so the sample ships no scene, prefab or image assets.
    /// An AI client can run it with execute_menu_item("Tools/Nanora Unity MCP/Samples/Build Shooting Game Scene").
    /// </summary>
    public static class ShootingGameSceneBuilder
    {
        public const string MenuPath = "Tools/Nanora Unity MCP/Samples/Build Shooting Game Scene";

        [MenuItem(MenuPath)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var path = Build();
            Debug.Log($"[ShootingGame] Scene built: {path}. Press Play, move with Left/Right, fire with Space.");
        }

        public static string Build()
        {
            var scenePath = GetScenePath();
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, -4f, 0f);
            player.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
            player.AddComponent<SpriteRenderer>();
            player.AddComponent<SquareVisual>();
            player.AddComponent<BoxCollider2D>().isTrigger = true;
            player.AddComponent<PlayerController>();

            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            // Screen Space - Camera, so capture_screenshot (which renders the camera) shows the score too.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            CreateText(canvasObject.transform, "ScoreLabel", "SCORE", 40, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -30f));
            var score = CreateText(canvasObject.transform, "ScoreText", "0", 72, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(40f, -80f));
            var gameOver = CreateText(canvasObject.transform, "GameOver", "GAME OVER", 120, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero);
            gameOver.color = new Color(0.95f, 0.3f, 0.35f);
            gameOver.gameObject.SetActive(false);

            var manager = new GameObject("GameManager").AddComponent<ShootingGameManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("scoreText").objectReferenceValue = score;
            serialized.FindProperty("gameOver").objectReferenceValue = gameOver.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            return scenePath;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor, Vector2 pivot, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(900f, size * 1.4f);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            SampleSprite.EnsureFont(text);
            return text;
        }

        // <sample root>/Scenes/ShootingGame.unity, wherever the Package Manager imported the sample.
        private static string GetScenePath()
        {
            var guids = AssetDatabase.FindAssets("ShootingGameSceneBuilder t:MonoScript");
            var builder = guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : "Assets/ShootingGame/Editor/ShootingGameSceneBuilder.cs";
            var root = Path.GetDirectoryName(Path.GetDirectoryName(builder));
            return Path.Combine(root, "Scenes", "ShootingGame.unity").Replace('\\', '/');
        }
    }
}
