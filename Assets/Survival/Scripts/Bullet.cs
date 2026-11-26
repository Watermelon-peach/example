using UnityEngine;

namespace Survival
{
    public class Bullet : MonoBehaviour
    {
        public float speed = 32f;
        public float lifetime = 1.2f;
        public int damage = 1;
        public float radius = 0.15f;

        SimplePool<Bullet> pool;
        float age;

        public void Launch(SimplePool<Bullet> owner)
        {
            pool = owner;
            age = 0f;
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            // Sweep ahead so fast bullets never tunnel through enemies.
            if (Physics.SphereCast(transform.position, radius, transform.forward, out RaycastHit hit, step,
                    ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<PlayerController>() == null)
            {
                var enemy = hit.collider.GetComponentInParent<Enemy>();
                if (enemy != null) enemy.TakeDamage(damage, transform.forward);
                Despawn();
                return;
            }

            transform.position += transform.forward * step;
            age += Time.deltaTime;
            if (age >= lifetime) Despawn();
        }

        void Despawn()
        {
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }
    }
}
