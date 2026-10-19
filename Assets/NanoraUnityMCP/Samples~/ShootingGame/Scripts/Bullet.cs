using UnityEngine;

namespace NanoraUnityMCP.Samples.ShootingGame
{
    /// <summary>Flies up, destroys the first enemy it touches and scores a point.</summary>
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float speed = 12f;
        [SerializeField] private float lifetime = 1.5f;

        public static Bullet Spawn(Vector3 position)
        {
            var go = SampleSprite.CreateSquare("Bullet", position, new Vector2(0.15f, 0.45f), new Color(1f, 0.85f, 0.2f));
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            return go.AddComponent<Bullet>();
        }

        private void Start() => Destroy(gameObject, lifetime);

        private void Update() => transform.position += Vector3.up * speed * Time.deltaTime;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<Enemy>() == null) return;
            Destroy(other.gameObject);
            Destroy(gameObject);
            if (ShootingGameManager.Instance != null) ShootingGameManager.Instance.AddScore(1);
        }
    }
}
