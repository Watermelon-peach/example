using UnityEngine;
using UnityEngine.AI;

namespace Survival
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class Enemy : MonoBehaviour
    {
        public int contactDamage = 10;
        public float attackRange = 1.4f;
        public float attackCooldown = 0.8f;
        public Renderer bodyRenderer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        NavMeshAgent agent;
        MaterialPropertyBlock block;
        Color baseColor = Color.red;
        WaveSpawner spawner;
        Transform target;
        PlayerHealth targetHealth;
        int health;
        float nextAttackTime;
        float repathTimer;
        float flashTimer;

        public bool IsAlive => health > 0;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            block = new MaterialPropertyBlock();
            if (bodyRenderer != null) baseColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        /// <summary>Called every time the enemy is taken from the pool.</summary>
        public void Spawn(WaveSpawner owner, Vector3 position, float speed, int maxHealth, Transform player, PlayerHealth playerHealth)
        {
            spawner = owner;
            target = player;
            targetHealth = playerHealth;
            health = maxHealth;

            agent.Warp(position);
            agent.speed = speed;
            agent.acceleration = speed * 4f;
            agent.isStopped = false;

            repathTimer = 0f;
            flashTimer = 0f;
            nextAttackTime = Time.time + 0.5f;
            // Faster waves get a hotter tint so the escalation is visible.
            baseColor = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.1f), Mathf.InverseLerp(3f, 9f, speed));
            SetColor(baseColor);
        }

        void Update()
        {
            if (target == null || !IsAlive) return;

            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f && agent.isOnNavMesh)
            {
                agent.SetDestination(target.position);
                repathTimer = 0.2f;
            }

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < attackRange * attackRange && Time.time >= nextAttackTime
                && targetHealth != null && !targetHealth.IsDead)
            {
                targetHealth.TakeDamage(contactDamage);
                nextAttackTime = Time.time + attackCooldown;
            }

            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) SetColor(baseColor);
            }
        }

        public void TakeDamage(int amount, Vector3 hitDirection)
        {
            if (!IsAlive) return;
            health -= amount;
            flashTimer = 0.08f;
            SetColor(Color.white);
            if (agent.isOnNavMesh) agent.Move(hitDirection.normalized * 0.3f); // small knockback

            if (!IsAlive)
            {
                if (GameManager.Instance != null) GameManager.Instance.AddKill();
                spawner.OnEnemyDied(this);
            }
        }

        void SetColor(Color c)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(block);
        }
    }
}
