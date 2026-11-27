using UnityEngine;

namespace Survival
{
    /// <summary>Health orb: bobs in place, gets pulled toward the player when close.</summary>
    public class Pickup : MonoBehaviour
    {
        #region 변수

        public int healAmount = 20;
        public float magnetRange = 4f;
        public float collectRange = 1.1f;
        public float despawnTime = 15f;

        SimplePool<Pickup> pool;
        Transform player;
        PlayerHealth playerHealth;
        float age;

        #endregion

        #region 유니티 이벤트 메서드

        void Update()
        {
            age += Time.deltaTime;
            transform.Rotate(0f, 140f * Time.deltaTime, 0f, Space.World);

            Vector3 pos = transform.position;
            Vector3 toPlayer = player.position + Vector3.up * 0.8f - pos;
            float dist = toPlayer.magnitude;

            if (dist < collectRange)
            {
                playerHealth.Heal(healAmount);
                var fx = Effects.Instance;
                if (fx != null)
                {
                    fx.Burst(pos, new Color(0.4f, 1f, 0.5f), 16, 4f, 0.6f);
                    fx.FloatText(pos + Vector3.up, $"+{healAmount}", new Color(0.5f, 1f, 0.55f), 34);
                }
                pool.Release(this);
                return;
            }

            if (dist < magnetRange)
                pos += toPlayer.normalized * Mathf.Lerp(16f, 5f, dist / magnetRange) * Time.deltaTime;
            else
                pos.y = 0.6f + Mathf.Sin(age * 3f) * 0.15f;
            transform.position = pos;

            if (age > despawnTime) pool.Release(this);
        }

        #endregion

        #region 커스텀 메서드

        public void Init(SimplePool<Pickup> owner, Transform target, PlayerHealth health)
        {
            pool = owner;
            player = target;
            playerHealth = health;
            age = 0f;
        }

        #endregion
    }
}
