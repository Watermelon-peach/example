using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Survival
{
    /// <summary>
    /// Central place for "juice": spark particles, floating damage numbers, camera shake, hit-stop,
    /// plus the pools for enemy projectiles and pickups.
    /// </summary>
    public class Effects : MonoBehaviour
    {
        class FloatingText
        {
            #region 변수

            public Text text;
            public Vector3 world;
            public Color color;
            public float age;

            #endregion
        }

        #region 변수

        public ParticleSystem sparks;
        public EnemyProjectile enemyProjectilePrefab;
        public Pickup pickupPrefab;
        public Canvas damageCanvas;
        public CameraFollow cameraFollow;
        public Transform player;
        public PlayerHealth playerHealth;
        [Range(0f, 1f)] public float pickupChance = 0.07f;

        const float NumberLifetime = 0.6f;

        SimplePool<EnemyProjectile> projectiles;
        SimplePool<Pickup> pickups;
        SimplePool<Text> numbers;
        readonly List<FloatingText> floating = new List<FloatingText>();
        Camera cam;
        float lastKillStop;

        #endregion

        #region 프로퍼티

        public static Effects Instance { get; private set; }

        #endregion

        #region 유니티 이벤트 메서드

        void Awake()
        {
            Instance = this;
            projectiles = new SimplePool<EnemyProjectile>(enemyProjectilePrefab, 40, "EnemyProjectilePool");
            pickups = new SimplePool<Pickup>(pickupPrefab, 6, "PickupPool");
            numbers = new SimplePool<Text>(CreateNumberTemplate(), 30, "DamageNumbers", damageCanvas.transform);
            numbers.Root.SetSiblingIndex(1); // above the damage overlay, below panels
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            for (int i = floating.Count - 1; i >= 0; i--)
            {
                var f = floating[i];
                f.age += Time.unscaledDeltaTime;
                if (f.age >= NumberLifetime)
                {
                    numbers.Release(f.text);
                    floating.RemoveAt(i);
                    continue;
                }
                f.text.transform.position = cam.WorldToScreenPoint(f.world + Vector3.up * (f.age * 1.5f));
                float k = f.age / NumberLifetime;
                Color c = f.color;
                c.a = 1f - k * k;
                f.text.color = c;
                f.text.transform.localScale = Vector3.one * (1f + 0.5f * Mathf.Max(0f, 1f - f.age * 8f)); // pop
            }
        }

        #endregion

        #region 커스텀 메서드

        Text CreateNumberTemplate()
        {
            var go = new GameObject("NumberTemplate", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(damageCanvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(200, 60);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
            return t;
        }

        // ---------- Particles ----------

        public void Burst(Vector3 position, Color color, int count, float speed, float size)
        {
            if (sparks == null) return;
            var ep = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.velocity = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * speed * Random.Range(0.4f, 1f);
                ep.startColor = color;
                ep.startSize = size * Random.Range(0.12f, 0.3f);
                ep.startLifetime = Random.Range(0.25f, 0.6f);
                sparks.Emit(ep, 1);
            }
        }

        // ---------- Floating numbers ----------

        public void DamageNumber(Vector3 world, int amount) => FloatText(world, amount.ToString(), Color.white, 30);

        public void FloatText(Vector3 world, string text, Color color, int size)
        {
            var t = numbers.Get(Vector3.zero, Quaternion.identity);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            floating.Add(new FloatingText { text = t, world = world + Random.insideUnitSphere * 0.3f, color = color });
        }

        // ---------- Camera / time ----------

        public void Shake(float amount)
        {
            if (cameraFollow != null) cameraFollow.Shake(amount);
        }

        public void KillFeedback(bool big)
        {
            Shake(big ? 1f : 0.12f);
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (big) gm.HitStop(0.35f);
            else if (Time.unscaledTime - lastKillStop > 0.12f)
            {
                gm.HitStop(0.025f);
                lastKillStop = Time.unscaledTime;
            }
        }

        // ---------- Spawning ----------

        public void SpawnEnemyProjectile(Vector3 origin, Vector3 direction, float speed, int damage)
        {
            direction.y = 0f;
            projectiles.Get(origin, Quaternion.LookRotation(direction)).Launch(projectiles, direction.normalized, speed, damage);
        }

        public void TryDropPickup(Vector3 position, int guaranteed)
        {
            int count = guaranteed > 0 ? guaranteed : (Random.value < pickupChance ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                Vector2 r = count > 1 ? Random.insideUnitCircle * 1.5f : Vector2.zero;
                var p = pickups.Get(new Vector3(position.x + r.x, 0.6f, position.z + r.y), Quaternion.identity);
                p.Init(pickups, player, playerHealth);
            }
        }

        #endregion
    }
}
