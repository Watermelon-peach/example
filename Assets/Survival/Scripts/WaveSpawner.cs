using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Survival
{
    public class WaveSpawner : MonoBehaviour
    {
        [Header("References")]
        public Enemy enemyPrefab;
        public Transform player;

        [Header("Arena")]
        public Vector2 arenaHalfSize = new Vector2(23f, 23f);
        public float minDistanceFromPlayer = 12f;

        [Header("Wave scaling")]
        public int baseCount = 5;
        public int countPerWave = 3;
        public float baseSpeed = 3f;
        public float speedPerWave = 0.5f;
        public float maxSpeed = 9f;
        public float baseSpawnInterval = 0.8f;
        public float timeBetweenWaves = 2f;
        public int maxAlive = 60;
        public int bossEvery = 5;

        public int Wave { get; private set; }
        public int Remaining => toSpawn + alive.Count;
        public float CurrentSpeed { get; private set; }
        public Enemy CurrentBoss { get; private set; }

        /// <summary>(wave number, is boss wave)</summary>
        public event Action<int, bool> OnWaveStarted;

        readonly List<Enemy> alive = new List<Enemy>();
        SimplePool<Enemy> pool;
        PlayerHealth playerHealth;
        int toSpawn;

        void Awake()
        {
            pool = new SimplePool<Enemy>(enemyPrefab, 40, "EnemyPool");
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        IEnumerator Start()
        {
            yield return null; // let the HUD subscribe and the NavMesh finish baking

            while (true)
            {
                Wave++;
                bool bossWave = bossEvery > 0 && Wave % bossEvery == 0;
                int count = baseCount + countPerWave * (Wave - 1);
                if (bossWave) count /= 2;
                CurrentSpeed = Mathf.Min(maxSpeed, baseSpeed + speedPerWave * (Wave - 1));
                int health = 1 + Wave / 3;
                float interval = Mathf.Max(0.15f, baseSpawnInterval * Mathf.Pow(0.9f, Wave - 1));

                toSpawn = count + (bossWave ? 1 : 0);
                OnWaveStarted?.Invoke(Wave, bossWave);

                if (bossWave)
                {
                    yield return new WaitForSeconds(1f);
                    TrySpawn(EnemyKind.Boss, CurrentSpeed, 30 + Wave * 10);
                    toSpawn--; // never stall the wave even if placement failed
                }

                while (toSpawn > 0)
                {
                    if (alive.Count < maxAlive && TrySpawn(PickKind(), CurrentSpeed, health)) toSpawn--;
                    yield return new WaitForSeconds(interval);
                }

                while (alive.Count > 0) yield return null;

                if (GameManager.Instance != null) yield return GameManager.Instance.OfferUpgrades();
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        /// <summary>New enemy types unlock as waves progress.</summary>
        EnemyKind PickKind()
        {
            float r = Random.value;
            float tank = Wave >= 6 ? 0.08f : 0f;
            float splitter = Wave >= 4 ? 0.12f : 0f;
            float shooter = Wave >= 3 ? 0.15f : 0f;
            float charger = Wave >= 2 ? 0.18f : 0f;
            if ((r -= tank) < 0f) return EnemyKind.Tank;
            if ((r -= splitter) < 0f) return EnemyKind.Splitter;
            if ((r -= shooter) < 0f) return EnemyKind.Shooter;
            if ((r -= charger) < 0f) return EnemyKind.Charger;
            return EnemyKind.Chaser;
        }

        bool TrySpawn(EnemyKind kind, float speed, int health)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 candidate = RandomEdgePoint();
                if ((candidate - player.position).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer) continue;
                if (SpawnAt(kind, candidate, speed, health, 3f)) return true;
            }
            return false;
        }

        bool SpawnAt(EnemyKind kind, Vector3 position, float speed, int health, float searchRadius)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas)) return false;

            var stats = EnemyCatalog.Get(kind);
            int finalHealth = kind == EnemyKind.Boss ? health : Mathf.Max(1, Mathf.RoundToInt(health * stats.healthMul));
            Enemy enemy = pool.Get(hit.position, Quaternion.identity);
            enemy.Spawn(this, kind, hit.position, speed * stats.speedMul, finalHealth, player, playerHealth);
            alive.Add(enemy);
            if (kind == EnemyKind.Boss) CurrentBoss = enemy;
            return true;
        }

        /// <summary>Random point on the arena border, so enemies come from every side.</summary>
        Vector3 RandomEdgePoint()
        {
            float t = Random.Range(-1f, 1f);
            float x = arenaHalfSize.x, z = arenaHalfSize.y;
            Vector3 local;
            switch (Random.Range(0, 4))
            {
                case 0: local = new Vector3(t * x, 0f, z); break;
                case 1: local = new Vector3(t * x, 0f, -z); break;
                case 2: local = new Vector3(x, 0f, t * z); break;
                default: local = new Vector3(-x, 0f, t * z); break;
            }
            return transform.position + local;
        }

        public void OnEnemyDied(Enemy enemy)
        {
            Vector3 position = enemy.transform.position;
            EnemyKind kind = enemy.Kind;

            alive.Remove(enemy);
            if (enemy == CurrentBoss) CurrentBoss = null;
            pool.Release(enemy);

            if (kind == EnemyKind.Splitter)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 offset = Random.insideUnitCircle.normalized * 0.8f;
                    SpawnAt(EnemyKind.Mini, position + new Vector3(offset.x, 0f, offset.y), CurrentSpeed, 1, 1.5f);
                }
            }
        }
    }
}
