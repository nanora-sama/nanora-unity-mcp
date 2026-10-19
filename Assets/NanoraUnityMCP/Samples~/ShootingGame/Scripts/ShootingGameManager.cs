using UnityEngine;
using UnityEngine.UI;

namespace NanoraUnityMCP.Samples.ShootingGame
{
    /// <summary>
    /// Spawns enemies, keeps the score and ends the game. The score and game-over objects are plain uGUI
    /// so an AI can check them with selectors: <c>#ScoreText</c> holds only the number, <c>#GameOver</c>
    /// is activated when an enemy reaches the bottom or touches the player.
    /// </summary>
    public class ShootingGameManager : MonoBehaviour
    {
        public static ShootingGameManager Instance { get; private set; }

        [SerializeField] private Text scoreText;
        [SerializeField] private GameObject gameOver;
        [SerializeField, Min(0.1f)] private float spawnInterval = 1.2f;
        [SerializeField, Min(0.1f)] private float enemySpeed = 1.6f;
        [SerializeField] private float spawnHalfWidth = 4f;
        [Tooltip("The first enemies fall straight at the player's start, so a scripted test can score without aiming.")]
        [SerializeField, Min(0)] private int centeredOpeningEnemies = 3;

        public int Score { get; private set; }
        public bool IsGameOver { get; private set; }

        private float _nextSpawn;
        private int _spawned;

        private void Awake()
        {
            Instance = this;
            SampleSprite.EnsureFont(scoreText);
            if (gameOver != null)
            {
                SampleSprite.EnsureFont(gameOver.GetComponentInChildren<Text>(true));
                gameOver.SetActive(false);
            }
            SetScore(0);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (IsGameOver || Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + spawnInterval;
            var x = _spawned++ < centeredOpeningEnemies ? 0f : Random.Range(-spawnHalfWidth, spawnHalfWidth);
            Enemy.Spawn(new Vector3(x, Camera.main != null ? Camera.main.orthographicSize + 1f : 6f, 0f), enemySpeed);
        }

        public void AddScore(int points)
        {
            if (!IsGameOver) SetScore(Score + points);
        }

        public void EndGame()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            if (gameOver != null) gameOver.SetActive(true);
            Debug.Log($"[ShootingGame] Game over. Score: {Score}");
        }

        private void SetScore(int value)
        {
            Score = value;
            if (scoreText != null) scoreText.text = value.ToString();
        }
    }
}
