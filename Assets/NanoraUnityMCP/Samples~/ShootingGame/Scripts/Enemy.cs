using UnityEngine;

namespace NanoraUnityMCP.Samples.ShootingGame
{
    /// <summary>Falls straight down. Reaching the bottom of the screen ends the game.</summary>
    public class Enemy : MonoBehaviour
    {
        private float _speed;

        public static Enemy Spawn(Vector3 position, float speed)
        {
            var go = SampleSprite.CreateSquare("Enemy", position, new Vector2(0.8f, 0.8f), new Color(0.9f, 0.25f, 0.3f));
            var body = go.AddComponent<Rigidbody2D>();
            // Dynamic without gravity: trigger contacts with the kinematic bullets and the static player are reported.
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            var enemy = go.AddComponent<Enemy>();
            enemy._speed = speed;
            return enemy;
        }

        private void Update()
        {
            transform.position += Vector3.down * _speed * Time.deltaTime;
            var bottom = Camera.main != null ? -Camera.main.orthographicSize - 0.5f : -5.5f;
            if (transform.position.y >= bottom) return;
            if (ShootingGameManager.Instance != null) ShootingGameManager.Instance.EndGame();
            Destroy(gameObject);
        }
    }
}
