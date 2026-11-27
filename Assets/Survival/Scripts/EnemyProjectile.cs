using UnityEngine;

namespace Survival
{
    public class EnemyProjectile : MonoBehaviour
    {
        #region 변수

        public float radius = 0.25f;
        public float lifetime = 5f;

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        SimplePool<EnemyProjectile> pool;
        Vector3 direction;
        float speed;
        float age;
        int damage;

        #endregion

        #region 유니티 이벤트 메서드

        void Update()
        {
            float step = speed * Time.deltaTime;
            int n = Physics.SphereCastNonAlloc(transform.position, radius, direction, Hits, step, ~0, QueryTriggerInteraction.Ignore);
            HitSort.Sort(Hits, n);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col.GetComponentInParent<Enemy>() != null) continue;

                var ph = col.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    if (ph.IsInvulnerable) continue; // dashing through bullets is allowed
                    ph.TakeDamage(damage);
                }
                Despawn();
                return;
            }

            transform.position += direction * step;
            age += Time.deltaTime;
            if (age >= lifetime) Despawn();
        }

        #endregion

        #region 커스텀 메서드

        public void Launch(SimplePool<EnemyProjectile> owner, Vector3 dir, float projectileSpeed, int projectileDamage)
        {
            pool = owner;
            direction = dir;
            speed = projectileSpeed;
            damage = projectileDamage;
            age = 0f;
        }

        void Despawn() => pool.Release(this);

        #endregion
    }
}
