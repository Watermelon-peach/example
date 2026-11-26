using UnityEngine;
using UnityEngine.InputSystem;

namespace Survival
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 7f;
        public float fireInterval = 0.11f;
        public float spreadDegrees = 2.5f;
        public Bullet bulletPrefab;
        public Transform muzzle;

        CharacterController controller;
        PlayerHealth health;
        Camera cam;
        SimplePool<Bullet> bullets;
        float nextFireTime;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<PlayerHealth>();
            bullets = new SimplePool<Bullet>(bulletPrefab, 40, "BulletPool");
        }

        void Start() => cam = Camera.main;

        void Update()
        {
            if (health != null && health.IsDead) return;

            Move();
            AimAndShoot();
        }

        void Move()
        {
            var kb = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            }

            Vector3 move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f) move.Normalize();

            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + Physics.gravity.y * Time.deltaTime;
            controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
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

            if (mouse.leftButton.isPressed && Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireInterval;
                Fire();
            }
        }

        public void Fire()
        {
            Quaternion rot = transform.rotation * Quaternion.Euler(0f, Random.Range(-spreadDegrees, spreadDegrees), 0f);
            bullets.Get(muzzle.position, rot).Launch(bullets);
        }
    }
}
