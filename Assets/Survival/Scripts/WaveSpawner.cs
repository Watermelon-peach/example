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
        public float timeBetweenWaves = 3f;
        public int maxAlive = 60;

        public int Wave { get; private set; }
        public int Remaining => toSpawn + alive.Count;
        public float CurrentSpeed { get; private set; }
        public event Action<int> OnWaveStarted;

        readonly List<Enemy> alive = new List<Enemy>();
        SimplePool<Enemy> pool;
        PlayerHealth playerHealth;
        int toSpawn;

        void Awake()
        {
            pool = new SimplePool<Enemy>(enemyPrefab, 30, "EnemyPool");
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        IEnumerator Start()
        {
            yield return null; // let the HUD subscribe and the NavMesh finish baking

            while (true)
            {
                Wave++;
                int count = baseCount + countPerWave * (Wave - 1);
                CurrentSpeed = Mathf.Min(maxSpeed, baseSpeed + speedPerWave * (Wave - 1));
                int health = 1 + Wave / 3;
                float interval = Mathf.Max(0.15f, baseSpawnInterval * Mathf.Pow(0.9f, Wave - 1));

                toSpawn = count;
                OnWaveStarted?.Invoke(Wave);

                while (toSpawn > 0)
                {
                    if (alive.Count < maxAlive && TrySpawn(CurrentSpeed, health)) toSpawn--;
                    yield return new WaitForSeconds(interval);
                }

                while (alive.Count > 0) yield return null;
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        bool TrySpawn(float speed, int health)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector3 candidate = RandomEdgePoint();
                if ((candidate - player.position).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer) continue;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;

                Enemy enemy = pool.Get(hit.position, Quaternion.identity);
                enemy.Spawn(this, hit.position, speed, health, player, playerHealth);
                alive.Add(enemy);
                return true;
            }
            return false;
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
            alive.Remove(enemy);
            pool.Release(enemy);
        }
    }
}
