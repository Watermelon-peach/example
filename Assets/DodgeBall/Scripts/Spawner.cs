using UnityEngine;

namespace DodgeBall
{
    /// <summary>
    /// Drops balls from the sky. The longer the player survives, the more often, the more at once,
    /// and the faster they fall. Some balls aim at where the player will be, and every so often
    /// a ring of balls closes in around the player with a single gap to escape through.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Ball prefab (needs Rigidbody + Ball).")]
        [SerializeField] Ball ballPrefab;
        [Tooltip("Flat marker shown on the ground where a ball will land.")]
        [SerializeField] GameObject landingMarkerPrefab;
        [Tooltip("Used to aim some balls at the player.")]
        [SerializeField] Transform player;

        [Header("Spawn area")]
        [SerializeField] float spawnHeight = 12f;
        [Tooltip("Balls land within ±this distance from the spawner on X and Z.")]
        [SerializeField] Vector2 spawnAreaHalfSize = new Vector2(9f, 9f);
        [SerializeField] float groundHeight = 0f;

        [Header("Difficulty over time")]
        [Tooltip("Delay before the very first ball.")]
        [SerializeField] float firstDelay = 0.5f;
        [Tooltip("Seconds between drops at the start.")]
        [SerializeField] float startInterval = 1.8f;
        [Tooltip("Shortest possible time between drops.")]
        [SerializeField] float minInterval = 0.4f;
        [Tooltip("How much the interval shrinks per second survived.")]
        [SerializeField] float intervalDecreasePerSecond = 0.04f;
        [Tooltip("One extra ball per drop every this many seconds.")]
        [SerializeField] float extraBallEverySeconds = 12f;
        [SerializeField] int maxBallsPerDrop = 6;
        [Tooltip("Downward speed added per second survived (balls fall faster over time).")]
        [SerializeField] float fallSpeedPerSecond = 0.25f;
        [SerializeField] float maxInitialFallSpeed = 12f;

        [Header("Aimed balls")]
        [Tooltip("Chance (0-1) that the first ball of a drop aims at the player.")]
        [Range(0f, 1f)] [SerializeField] float aimedChance = 0.5f;
        [Tooltip("Aimed balls land within this distance of the target point.")]
        [SerializeField] float aimSpread = 0.8f;
        [Tooltip("Aim at where the player will be when the ball lands, not where they are now.")]
        [SerializeField] bool predictiveAim = true;

        [Header("Ring attack")]
        [Tooltip("Every this many seconds, a ring of balls drops around the player (0 = off).")]
        [SerializeField] float ringEverySeconds = 15f;
        [Tooltip("Balls in a full ring; one is left out as the escape gap.")]
        [SerializeField] int ringBalls = 10;
        [SerializeField] float ringRadius = 2.4f;

        float elapsed;
        float timer;
        float ringTimer;
        Vector3 lastPlayerPosition;
        Vector3 playerVelocity;

        public float CurrentInterval => Mathf.Max(minInterval, startInterval - intervalDecreasePerSecond * elapsed);
        public int CurrentBallsPerDrop => Mathf.Min(maxBallsPerDrop, 1 + Mathf.FloorToInt(elapsed / extraBallEverySeconds));
        public float CurrentFallSpeed => Mathf.Min(maxInitialFallSpeed, fallSpeedPerSecond * elapsed);

        void Start()
        {
            timer = CurrentInterval - firstDelay; // first drop after firstDelay seconds
            if (player != null) lastPlayerPosition = player.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // paused / game over

            elapsed += dt;
            TrackPlayerVelocity(dt);

            timer += dt;
            if (timer >= CurrentInterval)
            {
                timer = 0f;
                for (int i = 0; i < CurrentBallsPerDrop; i++)
                {
                    bool aimed = i == 0 && Random.value < aimedChance;
                    SpawnAt(aimed ? AimPoint() : RandomPoint());
                }
            }

            if (ringEverySeconds > 0f)
            {
                ringTimer += dt;
                if (ringTimer >= ringEverySeconds)
                {
                    ringTimer = 0f;
                    SpawnRing();
                }
            }
        }

        void TrackPlayerVelocity(float dt)
        {
            if (player == null) return;
            Vector3 current = (player.position - lastPlayerPosition) / dt;
            current.y = 0f;
            playerVelocity = Vector3.Lerp(playerVelocity, current, 1f - Mathf.Exp(-10f * dt)); // smoothed
            lastPlayerPosition = player.position;
        }

        /// <summary>Seconds a ball takes to fall from spawn height to the ground.</summary>
        float FallTime()
        {
            float g = -Physics.gravity.y;
            float v0 = CurrentFallSpeed;
            float h = spawnHeight - 0.5f; // ball radius
            return (-v0 + Mathf.Sqrt(v0 * v0 + 2f * g * h)) / g;
        }

        Vector2 AimPoint()
        {
            if (player == null) return RandomPoint();
            Vector3 target = player.position;
            if (predictiveAim) target += playerVelocity * FallTime();
            Vector2 jitter = Random.insideUnitCircle * aimSpread;
            return new Vector2(target.x - transform.position.x + jitter.x, target.z - transform.position.z + jitter.y);
        }

        Vector2 RandomPoint() =>
            new Vector2(Random.Range(-spawnAreaHalfSize.x, spawnAreaHalfSize.x), Random.Range(-spawnAreaHalfSize.y, spawnAreaHalfSize.y));

        void SpawnRing()
        {
            if (player == null) return;
            Vector2 center = new Vector2(player.position.x - transform.position.x, player.position.z - transform.position.z);
            int gap = Random.Range(0, ringBalls);
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < ringBalls; i++)
            {
                if (i == gap) continue; // the escape route
                float a = (startAngle + i * 360f / ringBalls) * Mathf.Deg2Rad;
                SpawnAt(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringRadius);
            }
        }

        void SpawnAt(Vector2 offset)
        {
            if (ballPrefab == null) return;

            offset.x = Mathf.Clamp(offset.x, -spawnAreaHalfSize.x, spawnAreaHalfSize.x);
            offset.y = Mathf.Clamp(offset.y, -spawnAreaHalfSize.y, spawnAreaHalfSize.y);
            Vector3 center = transform.position;
            Vector3 landing = new Vector3(center.x + offset.x, groundHeight, center.z + offset.y);

            Transform marker = null;
            if (landingMarkerPrefab != null)
                marker = Instantiate(landingMarkerPrefab, landing + Vector3.up * 0.02f, Quaternion.identity).transform;

            Ball ball = Instantiate(ballPrefab, new Vector3(landing.x, groundHeight + spawnHeight, landing.z), Quaternion.identity);
            ball.Launch(CurrentFallSpeed, marker);
        }
    }
}
