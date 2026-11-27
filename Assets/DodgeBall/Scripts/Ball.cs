using UnityEngine;

namespace DodgeBall
{
    /// <summary>
    /// A falling ball. It is dangerous only while falling: touching the player ends the game,
    /// touching the ground makes it harmless, after which it shrinks away.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Ball : MonoBehaviour
    {
        [Tooltip("Color once the ball has landed and can no longer hurt the player.")]
        [SerializeField] Color harmlessColor = new Color(0.45f, 0.45f, 0.5f);

        [Tooltip("Seconds it takes a landed ball to shrink and disappear.")]
        [SerializeField] float vanishTime = 0.8f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool IsDangerous { get; private set; } = true;

        Rigidbody body;
        Renderer meshRenderer;
        Transform marker;
        Vector3 markerFullScale;
        Vector3 startScale;
        float startHeight;
        float groundHeight;
        float vanishTimer = -1f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            meshRenderer = GetComponent<Renderer>();
            startScale = transform.localScale;
        }

        /// <summary>Called by the Spawner right after the ball is created.</summary>
        public void Launch(float initialDownSpeed, Transform landingMarker)
        {
            body.linearVelocity = Vector3.down * initialDownSpeed;
            startHeight = transform.position.y;
            marker = landingMarker;
            if (marker != null)
            {
                markerFullScale = marker.localScale;
                groundHeight = marker.position.y;
                UpdateMarker();
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsDangerous) return;

            if (collision.collider.CompareTag("Player"))
            {
                if (GameManager.Instance != null) GameManager.Instance.GameOver();
                return;
            }

            // Hit the ground (or another ball): it can't hurt anyone anymore.
            BecomeHarmless();
        }

        void BecomeHarmless()
        {
            IsDangerous = false;
            vanishTimer = vanishTime;

            var block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, harmlessColor);
            meshRenderer.SetPropertyBlock(block);

            RemoveMarker();
        }

        void Update()
        {
            if (marker != null) UpdateMarker();

            if (vanishTimer >= 0f)
            {
                vanishTimer -= Time.deltaTime;
                transform.localScale = startScale * Mathf.Clamp01(vanishTimer / vanishTime);
                if (vanishTimer <= 0f) Destroy(gameObject);
            }

            // Safety net: anything that falls off the world is cleaned up.
            if (transform.position.y < -10f) Destroy(gameObject);
        }

        /// <summary>The landing marker grows as the ball gets closer to the ground.</summary>
        void UpdateMarker()
        {
            float range = Mathf.Max(0.01f, startHeight - groundHeight);
            float closeness = 1f - Mathf.Clamp01((transform.position.y - groundHeight) / range);
            marker.localScale = new Vector3(markerFullScale.x * Mathf.Lerp(0.35f, 1f, closeness), markerFullScale.y, markerFullScale.z * Mathf.Lerp(0.35f, 1f, closeness));
        }

        void RemoveMarker()
        {
            if (marker != null) Destroy(marker.gameObject);
            marker = null;
        }

        void OnDestroy() => RemoveMarker();
    }
}
