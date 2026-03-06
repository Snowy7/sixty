using System;
using System.Collections;
using System.Collections.Generic;
using Sixty.Combat;
using Sixty.Core;
using Sixty.World;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class RunDirector : MonoBehaviour
    {
        [Serializable]
        public class EnemySpawnEntry
        {
            public string label = "Enemy";
            public GameObject prefab;
            public int unlockRoom = 1;
            public float weight = 1f;
        }

        [Header("Run Structure")]
        [SerializeField] private int totalRooms = 10;
        [SerializeField] private int minEnemiesPerRoom = 3;
        [SerializeField] private int maxEnemiesPerRoom = 6;
        [SerializeField] private float delayBetweenRooms = 1.1f;
        [SerializeField] private float spawnRadiusJitter = 1.8f;

        [Header("Spawns")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private List<EnemySpawnEntry> enemyPrefabs = new List<EnemySpawnEntry>();
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private ClockPickup clockPickupPrefab;
        [SerializeField] [Range(0f, 1f)] private float interRoomClockPickupChance = 0.75f;

        public int CurrentRoom { get; private set; }
        public int EnemiesAlive => aliveEnemies.Count;

        public event Action<int, int> OnRoomChanged;
        public event Action<int, int> OnRoomCleared;
        public event Action<int> OnEnemiesRemainingChanged;
        public event Action OnRunWon;

        private readonly List<Health> aliveEnemies = new List<Health>();
        private Coroutine runRoutine;

        private void OnEnable()
        {
            if (runRoutine == null)
            {
                runRoutine = StartCoroutine(RunLoop());
            }
        }

        private void OnDisable()
        {
            if (runRoutine != null)
            {
                StopCoroutine(runRoutine);
                runRoutine = null;
            }

            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                Health health = aliveEnemies[i];
                if (health != null)
                {
                    health.OnDied -= OnEnemyDied;
                }
            }

            aliveEnemies.Clear();
        }

        private IEnumerator RunLoop()
        {
            yield return null;

            for (int room = 1; room <= totalRooms; room++)
            {
                if (TimeManager.Instance != null && TimeManager.Instance.IsOutOfTime)
                {
                    yield break;
                }

                CurrentRoom = room;
                OnRoomChanged?.Invoke(CurrentRoom, totalRooms);

                SpawnRoom(room);
                OnEnemiesRemainingChanged?.Invoke(aliveEnemies.Count);

                while (aliveEnemies.Count > 0)
                {
                    aliveEnemies.RemoveAll(enemy => enemy == null || enemy.IsDead);
                    OnEnemiesRemainingChanged?.Invoke(aliveEnemies.Count);
                    yield return null;
                }

                OnRoomCleared?.Invoke(CurrentRoom, totalRooms);

                if (room < totalRooms)
                {
                    TrySpawnClockPickup();
                    yield return new WaitForSeconds(delayBetweenRooms);
                }
            }

            OnRunWon?.Invoke();
        }

        private void SpawnRoom(int roomNumber)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("RunDirector has no spawn points assigned.", this);
                return;
            }

            if (roomNumber == totalRooms && bossPrefab != null)
            {
                SpawnEnemy(bossPrefab, GetSpawnPosition());

                int bonusAdds = Mathf.Clamp(roomNumber / 3, 1, 3);
                for (int i = 0; i < bonusAdds; i++)
                {
                    GameObject addPrefab = PickEnemyPrefab(roomNumber - 1);
                    if (addPrefab != null)
                    {
                        SpawnEnemy(addPrefab, GetSpawnPosition());
                    }
                }

                return;
            }

            int baseCount = UnityEngine.Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
            int scaledCount = baseCount + Mathf.FloorToInt((roomNumber - 1) * 0.45f);
            int enemiesToSpawn = Mathf.Clamp(scaledCount, minEnemiesPerRoom, maxEnemiesPerRoom + 4);

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                GameObject enemyPrefab = PickEnemyPrefab(roomNumber);
                if (enemyPrefab == null)
                {
                    continue;
                }

                SpawnEnemy(enemyPrefab, GetSpawnPosition());
            }
        }

        private void SpawnEnemy(GameObject prefab, Vector3 position)
        {
            GameObject enemy = Instantiate(prefab, position, Quaternion.identity);
            Health health = enemy.GetComponentInChildren<Health>();
            if (health == null)
            {
                return;
            }

            health.OnDied += OnEnemyDied;
            aliveEnemies.Add(health);
        }

        private GameObject PickEnemyPrefab(int roomNumber)
        {
            float totalWeight = 0f;
            for (int i = 0; i < enemyPrefabs.Count; i++)
            {
                EnemySpawnEntry entry = enemyPrefabs[i];
                if (entry == null || entry.prefab == null || roomNumber < entry.unlockRoom || entry.weight <= 0f)
                {
                    continue;
                }

                totalWeight += entry.weight;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < enemyPrefabs.Count; i++)
            {
                EnemySpawnEntry entry = enemyPrefabs[i];
                if (entry == null || entry.prefab == null || roomNumber < entry.unlockRoom || entry.weight <= 0f)
                {
                    continue;
                }

                cumulative += entry.weight;
                if (roll <= cumulative)
                {
                    return entry.prefab;
                }
            }

            return null;
        }

        private Vector3 GetSpawnPosition()
        {
            Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * spawnRadiusJitter;
            return spawnPoint.position + new Vector3(jitter.x, 0f, jitter.y);
        }

        private void OnEnemyDied(Health health)
        {
            health.OnDied -= OnEnemyDied;
            aliveEnemies.Remove(health);
            OnEnemiesRemainingChanged?.Invoke(aliveEnemies.Count);
        }

        private void TrySpawnClockPickup()
        {
            if (clockPickupPrefab == null || UnityEngine.Random.value > interRoomClockPickupChance)
            {
                return;
            }

            Instantiate(clockPickupPrefab, GetSpawnPosition(), Quaternion.identity);
        }
    }
}
