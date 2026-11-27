using UnityEngine;
using UnityEngine.AI;

namespace Survival
{
    /// <summary>
    /// One pooled enemy prefab that becomes any <see cref="EnemyKind"/> when spawned.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Enemy : MonoBehaviour
    {
        enum ChargeState { Chasing, Windup, Dashing, Recover }

        #region 변수

        public float attackRange = 1.4f;
        public float attackCooldown = 1f;
        public Renderer bodyRenderer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color WindupColor = new Color(1f, 0.95f, 0.3f);

        NavMeshAgent agent;
        MaterialPropertyBlock block;
        Color baseColor = Color.red;
        WaveSpawner spawner;
        Transform target;
        PlayerHealth targetHealth;
        int contactDamage;
        float baseSpeed;
        float nextAttackTime;
        float repathTimer;
        float flashTimer;
        float actionTimer;
        int bossPattern;
        ChargeState charge;
        Vector3 dashDirection;

        #endregion

        #region 프로퍼티

        public EnemyKind Kind { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsAlive => Health > 0;

        #endregion

        #region 유니티 이벤트 메서드

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (target == null || !IsAlive) return;
            float dt = Time.deltaTime;

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;

            switch (Kind)
            {
                case EnemyKind.Charger: UpdateCharger(toPlayer, dist, dt); break;
                case EnemyKind.Shooter: UpdateShooter(toPlayer, dist, dt); break;
                case EnemyKind.Boss: Chase(dt); UpdateBoss(toPlayer, dt); break;
                default: Chase(dt); break;
            }

            float reach = attackRange + (transform.localScale.x - 1f) * 0.5f;
            if (dist < reach && Time.time >= nextAttackTime && targetHealth != null && !targetHealth.IsDead)
            {
                targetHealth.TakeDamage(charge == ChargeState.Dashing ? contactDamage * 2 : contactDamage);
                nextAttackTime = Time.time + attackCooldown;
            }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetColor(CurrentColor());
            }
        }

        #endregion

        #region 커스텀 메서드

        /// <summary>Called every time the enemy is taken from the pool.</summary>
        public void Spawn(WaveSpawner owner, EnemyKind kind, Vector3 position, float speed, int health, Transform player, PlayerHealth playerHealth)
        {
            var stats = EnemyCatalog.Get(kind);
            spawner = owner;
            Kind = kind;
            target = player;
            targetHealth = playerHealth;
            MaxHealth = Health = Mathf.Max(1, health);
            contactDamage = stats.damage;
            baseColor = stats.color;
            baseSpeed = speed;

            transform.localScale = Vector3.one * stats.scale;
            agent.Warp(position);
            agent.speed = speed;
            agent.acceleration = speed * 4f;
            agent.isStopped = false;
            agent.stoppingDistance = kind == EnemyKind.Shooter ? 8f : 1.1f * stats.scale;
            agent.avoidancePriority = kind == EnemyKind.Boss ? 10 : 50;

            repathTimer = 0f;
            flashTimer = 0f;
            nextAttackTime = Time.time + 0.5f;
            actionTimer = kind == EnemyKind.Boss ? 2f : Random.Range(1f, 2.5f);
            bossPattern = 0;
            charge = ChargeState.Chasing;
            SetColor(baseColor);
        }

        void Chase(float dt)
        {
            repathTimer -= dt;
            if (repathTimer <= 0f && agent.isOnNavMesh)
            {
                agent.SetDestination(target.position);
                repathTimer = 0.2f;
            }
        }

        // Charger: chase, stop and glow yellow (telegraph), then dash in a straight line.
        void UpdateCharger(Vector3 toPlayer, float dist, float dt)
        {
            actionTimer -= dt;
            switch (charge)
            {
                case ChargeState.Chasing:
                    Chase(dt);
                    if (actionTimer <= 0f && dist < 10f)
                    {
                        charge = ChargeState.Windup;
                        actionTimer = 0.55f;
                        agent.isStopped = true;
                        agent.velocity = Vector3.zero;
                        SetColor(WindupColor);
                    }
                    break;
                case ChargeState.Windup:
                    if (dist > 0.01f) transform.rotation = Quaternion.LookRotation(toPlayer);
                    if (actionTimer <= 0f)
                    {
                        dashDirection = toPlayer.normalized;
                        charge = ChargeState.Dashing;
                        actionTimer = 0.45f;
                        SetColor(baseColor);
                    }
                    break;
                case ChargeState.Dashing:
                    if (agent.isOnNavMesh) agent.Move(dashDirection * baseSpeed * 4.5f * dt);
                    if (actionTimer <= 0f)
                    {
                        charge = ChargeState.Recover;
                        actionTimer = 0.5f;
                    }
                    break;
                case ChargeState.Recover:
                    if (actionTimer <= 0f)
                    {
                        charge = ChargeState.Chasing;
                        agent.isStopped = false;
                        actionTimer = Random.Range(1.8f, 3f);
                    }
                    break;
            }
        }

        // Shooter: keeps its distance and fires single aimed shots.
        void UpdateShooter(Vector3 toPlayer, float dist, float dt)
        {
            Chase(dt);
            if (dist < 6f && agent.isOnNavMesh)
            {
                Vector3 away = transform.position - toPlayer.normalized * 3f;
                if (NavMesh.SamplePosition(away, out NavMeshHit hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                repathTimer = 0.3f;
            }

            actionTimer -= dt;
            if (actionTimer <= 0f && dist < 14f)
            {
                actionTimer = Random.Range(1.6f, 2.4f);
                FireSpread(toPlayer.normalized, 1, 0f, 11f, contactDamage);
            }
        }

        // Boss: alternates a radial burst and an aimed 5-way spread.
        void UpdateBoss(Vector3 toPlayer, float dt)
        {
            actionTimer -= dt;
            if (actionTimer > 0f) return;

            if (bossPattern % 2 == 0)
            {
                FireSpread(Vector3.forward, 18, 20f, 8f, 10);
                actionTimer = 2.6f;
                var fx = Effects.Instance;
                if (fx != null) fx.Shake(0.25f);
            }
            else
            {
                FireSpread(toPlayer.normalized, 5, 12f, 12f, 10);
                actionTimer = 1.4f;
            }
            bossPattern++;
        }

        void FireSpread(Vector3 direction, int count, float stepDegrees, float speed, int damage)
        {
            var fx = Effects.Instance;
            if (fx == null || direction.sqrMagnitude < 0.0001f) return;
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            float start = -(count - 1) * 0.5f * stepDegrees;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, start + i * stepDegrees, 0f) * direction;
                fx.SpawnEnemyProjectile(origin + dir * (0.5f * transform.localScale.x), dir, speed, damage);
            }
        }

        public void TakeDamage(int amount, Vector3 hitDirection)
        {
            if (!IsAlive) return;
            Health -= amount;
            flashTimer = 0.07f;
            SetColor(Color.white);

            var fx = Effects.Instance;
            if (fx != null) fx.DamageNumber(transform.position + Vector3.up * (2f * transform.localScale.x), amount);

            bool heavy = Kind == EnemyKind.Boss || Kind == EnemyKind.Tank;
            if (!heavy && charge != ChargeState.Dashing && agent.isOnNavMesh) agent.Move(hitDirection.normalized * 0.25f);

            if (!IsAlive) Die();
        }

        void Die()
        {
            bool isBoss = Kind == EnemyKind.Boss;
            var fx = Effects.Instance;
            if (fx != null)
            {
                Vector3 center = transform.position + Vector3.up * transform.localScale.x;
                fx.Burst(center, baseColor * 2f, isBoss ? 150 : 18, isBoss ? 14f : 7f, transform.localScale.x);
                fx.KillFeedback(isBoss);
                fx.TryDropPickup(transform.position, isBoss ? 4 : 0);
            }
            if (GameManager.Instance != null) GameManager.Instance.AddKill();

            agent.isStopped = false;
            charge = ChargeState.Chasing;
            spawner.OnEnemyDied(this);
        }

        Color CurrentColor() => charge == ChargeState.Windup ? WindupColor : baseColor;

        void SetColor(Color c)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(block);
        }

        #endregion
    }
}
