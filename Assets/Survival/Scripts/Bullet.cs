using System.Collections.Generic;
using UnityEngine;

namespace Survival
{
    public class Bullet : MonoBehaviour
    {
        #region 변수

        public float baseSpeed = 32f;
        public float lifetime = 1.2f;
        public float radius = 0.15f;

        static readonly RaycastHit[] Hits = new RaycastHit[16];
        static readonly Color ImpactColor = new Color(1f, 0.85f, 0.4f);

        readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();
        SimplePool<Bullet> pool;
        float age;
        float speed;
        int damage;
        int pierceLeft;

        #endregion

        #region 유니티 이벤트 메서드

        void Update()
        {
            float step = speed * Time.deltaTime;
            // Sweep ahead so fast bullets never tunnel; sorted so piercing hits enemies in order.
            int n = Physics.SphereCastNonAlloc(transform.position, radius, transform.forward, Hits, step, ~0, QueryTriggerInteraction.Ignore);
            HitSort.Sort(Hits, n);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col.GetComponentInParent<PlayerController>() != null) continue;

                var enemy = col.GetComponentInParent<Enemy>();
                if (enemy == null)
                {
                    var fx = Effects.Instance;
                    if (fx != null) fx.Burst(Hits[i].distance > 0f ? Hits[i].point : transform.position, ImpactColor, 4, 3f, 0.5f);
                    Despawn();
                    return;
                }

                if (!enemy.IsAlive || !alreadyHit.Add(enemy)) continue;
                enemy.TakeDamage(damage, transform.forward);
                if (pierceLeft <= 0)
                {
                    Despawn();
                    return;
                }
                pierceLeft--;
            }

            transform.position += transform.forward * step;
            age += Time.deltaTime;
            if (age >= lifetime) Despawn();
        }

        #endregion

        #region 커스텀 메서드

        public void Launch(SimplePool<Bullet> owner, int bulletDamage, int pierce, float speedMultiplier)
        {
            pool = owner;
            damage = bulletDamage;
            pierceLeft = pierce;
            speed = baseSpeed * speedMultiplier;
            age = 0f;
            alreadyHit.Clear();
        }

        void Despawn()
        {
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }

        #endregion
    }
}
