using UnityEngine;
using UnityEngine.InputSystem;

namespace Survival
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Stats (modified by upgrades)")]
        public float moveSpeed = 7f;
        public float fireInterval = 0.13f;
        public float spreadDegrees = 2.5f;
        public int damage = 1;
        public int pierce = 0;
        public int projectiles = 1;
        public float bulletSpeedMul = 1f;
        public int healOnKill = 0;

        [Header("Dash")]
        public float dashSpeed = 24f;
        public float dashDuration = 0.16f;
        public float dashCooldown = 1.2f;

        [Header("References")]
        public Bullet bulletPrefab;
        public Transform muzzle;

        static readonly Color MuzzleColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color DashColor = new Color(0.3f, 0.7f, 1f);
        const float FanStepDegrees = 7f;

        CharacterController controller;
        PlayerHealth health;
        Camera cam;
        SimplePool<Bullet> bullets;
        float nextFireTime;
        float verticalVelocity;
        float dashEndTime;
        float dashReadyTime;
        Vector3 dashDirection;

        public bool IsDashing => Time.time < dashEndTime;
        public float DashReadiness => dashCooldown <= 0f ? 1f : Mathf.Clamp01(1f - (dashReadyTime - Time.time) / dashCooldown);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<PlayerHealth>();
            bullets = new SimplePool<Bullet>(bulletPrefab, 60, "BulletPool");
        }

        void Start() => cam = Camera.main;

        void Update()
        {
            if (health != null && health.IsDead) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.IsPaused) return;

            Move();
            AimAndShoot();
        }

        void Move()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            Vector2 input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            }
            bool dashPressed = mouse != null && mouse.rightButton.wasPressedThisFrame;

            Vector3 move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (dashPressed && Time.time >= dashReadyTime) StartDash(move);

            Vector3 velocity;
            if (IsDashing)
            {
                velocity = dashDirection * dashSpeed;
                var fx = Effects.Instance;
                if (fx != null) fx.Burst(transform.position + Vector3.up, DashColor, 2, 1.5f, 0.7f);
            }
            else velocity = move * moveSpeed;

            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + Physics.gravity.y * Time.deltaTime;
            controller.Move((velocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        void StartDash(Vector3 move)
        {
            dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : transform.forward;
            dashEndTime = Time.time + dashDuration;
            dashReadyTime = Time.time + dashCooldown;
            if (health != null) health.SetInvulnerable(dashDuration + 0.1f);
            var fx = Effects.Instance;
            if (fx != null)
            {
                fx.Shake(0.15f);
                fx.Burst(transform.position + Vector3.up, DashColor, 14, 4f, 0.8f);
            }
        }

        void AimAndShoot()
        {
            var mouse = Mouse.current;
            if (mouse == null || cam == null) return;

            // Project the cursor onto a horizontal plane at muzzle height and face that point.
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var aimPlane = new Plane(Vector3.up, new Vector3(0f, muzzle.position.y, 0f));
            if (aimPlane.Raycast(ray, out float enter))
            {
                Vector3 look = ray.GetPoint(enter) - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
            }

            // Auto-fire: shoots continuously toward the cursor, no button needed.
            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireInterval;
                Fire();
            }
        }

        public void Fire()
        {
            float start = -(projectiles - 1) * 0.5f * FanStepDegrees;
            for (int i = 0; i < projectiles; i++)
            {
                float yaw = start + i * FanStepDegrees + Random.Range(-spreadDegrees, spreadDegrees);
                Quaternion rot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
                bullets.Get(muzzle.position, rot).Launch(bullets, damage, pierce, bulletSpeedMul);
            }

            var fx = Effects.Instance;
            if (fx != null)
            {
                fx.Burst(muzzle.position, MuzzleColor, 3, 2.5f, 0.5f);
                fx.Shake(0.035f);
            }
        }
    }
}
