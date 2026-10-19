using UnityEngine;

namespace NanoraUnityMCP.Samples.ShootingGame
{
    /// <summary>Gives a scene object the sample's square sprite, in Edit Mode too, so screenshots show it.</summary>
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public class SquareVisual : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.25f, 0.85f, 0.95f);

        private void OnEnable() => SampleSprite.Apply(GetComponent<SpriteRenderer>(), color);

        private void OnValidate()
        {
            if (isActiveAndEnabled) SampleSprite.Apply(GetComponent<SpriteRenderer>(), color);
        }
    }
}
