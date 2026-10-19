using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NanoraUnityMCP.Samples.ShootingGame
{
    /// <summary>
    /// Left/Right (or A/D) move, Space fires. With the Input System package the fire action uses a callback,
    /// so a press and release that land between two frames (as simulate_input key_tap sends them) still fires.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 7f;
        [SerializeField] private float halfWidth = 4.5f;
        [SerializeField, Min(0f)] private float fireCooldown = 0.12f;

        public int ShotsFired { get; private set; }

        private float _nextShot;
        private bool _fireQueued;
#if ENABLE_INPUT_SYSTEM
        private InputAction _move;
        private InputAction _fire;

        private void OnEnable()
        {
            _move = new InputAction("Move");
            _move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _fire = new InputAction("Fire", InputActionType.Button, "<Keyboard>/space");
            _fire.performed += OnFire;
            _move.Enable();
            _fire.Enable();
        }

        private void OnDisable()
        {
            _fire.performed -= OnFire;
            _move.Disable();
            _fire.Disable();
            _move.Dispose();
            _fire.Dispose();
        }

        private void OnFire(InputAction.CallbackContext context) => _fireQueued = true;

        private float ReadMove() => _move.ReadValue<float>();
#else
        private float ReadMove() => Input.GetAxisRaw("Horizontal");
#endif

        private void Update()
        {
            var game = ShootingGameManager.Instance;
            if (game != null && game.IsGameOver) return;

#if !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(KeyCode.Space)) _fireQueued = true;
#endif
            var position = transform.position;
            position.x = Mathf.Clamp(position.x + ReadMove() * speed * Time.deltaTime, -halfWidth, halfWidth);
            transform.position = position;

            if (_fireQueued && Time.time >= _nextShot)
            {
                _fireQueued = false;
                _nextShot = Time.time + fireCooldown;
                ShotsFired++;
                Bullet.Spawn(position + Vector3.up * 0.6f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<Enemy>() != null && ShootingGameManager.Instance != null)
                ShootingGameManager.Instance.EndGame();
        }
    }
}
