using UnityEngine;
using UnityEngine.UI;

namespace NanoraUnityMCP.Samples.ShootingGame
{
    /// <summary>
    /// Builds the sample's visuals from code (a white square sprite and a built-in font) so the sample has no
    /// image, font or material assets and renders the same in the Built-in, URP and HDRP pipelines.
    /// </summary>
    public static class SampleSprite
    {
        private static Sprite s_square;
        private static Material s_material;

        public static Sprite Square
        {
            get
            {
                if (s_square == null)
                {
                    s_square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    s_square.name = "SampleSquare";
                    s_square.hideFlags = HideFlags.DontSave;
                }
                return s_square;
            }
        }

        // Sprites/Default ships with every pipeline; the pipeline's default sprite material may not.
        public static Material Material
        {
            get
            {
                if (s_material == null)
                {
                    s_material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
                }
                return s_material;
            }
        }

        public static GameObject CreateSquare(string name, Vector3 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            Apply(go.AddComponent<SpriteRenderer>(), color);
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            return go;
        }

        public static void Apply(SpriteRenderer renderer, Color color)
        {
            renderer.sprite = Square;
            renderer.sharedMaterial = Material;
            renderer.color = color;
        }

        // A Text saved in a newer Unity can lose its built-in font in an older one, so pick whichever exists.
        public static void EnsureFont(Text text)
        {
            if (text == null || text.font != null) return;
            text.font = LoadBuiltinFont("LegacyRuntime.ttf") ?? LoadBuiltinFont("Arial.ttf");
        }

        private static Font LoadBuiltinFont(string fileName)
        {
            try { return Resources.GetBuiltinResource<Font>(fileName); }
            catch (System.ArgumentException) { return null; }
        }
    }
}
