using System;
using System.Collections;
using System.Collections.Generic;
using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Core;
using Sixty.World;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class RunDirector : IaBehaviour
    {
        public enum RoomType
        {
            Combat = 0,
            Reward = 1,
            Risk = 2,
            Boss = 3
        }

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

        [Header("Room Types")]
        [SerializeField] [Range(0f, 1f)] private float rewardRoomChance = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float riskRoomChance = 0.1f;
        [SerializeField] private int guaranteedCombatRooms = 2;
        [SerializeField] private int rewardClockPickups = 2;
        [SerializeField] private int riskGuaranteedClockPickups = 1;
        [SerializeField] private float riskEnemyMultiplier = 1.35f;

        [Header("Difficulty Scaling")]
        [SerializeField] private float roomEnemyScalePerRoom = 0.45f;
        [SerializeField] private float lowTimePressureThreshold = 20f;
        [SerializeField] private float lowTimeEnemyBonusMultiplier = 0.55f;
        [SerializeField] private int maxAdditionalEnemiesFromPressure = 3;

        [Header("Spawns")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private List<EnemySpawnEntry> enemyPrefabs = new List<EnemySpawnEntry>();
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private ClockPickup clockPickupPrefab;
        [SerializeField] [Range(0f, 1f)] private float interRoomClockPickupChance = 0.75f;

        public int CurrentRoom { get; private set; }
        public RoomType CurrentRoomType { get; private set; } = RoomType.Combat;
        public int EnemiesAlive => aliveEnemies.Count;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        public event Action<int, int> OnRoomChanged;
        public event Action<int, int, RoomType> OnRoomTypeChanged;
        public event Action<int, int> OnRoomCleared;
        public event Action<int> OnEnemiesRemainingChanged;
        public event Action OnRunWon;

        private readonly List<Health> aliveEnemies = new List<Health>();
        private readonly List<Collider> activeEnemyColliders = new List<Collider>(256);
        private readonly Dictionary<Health, Collider[]> enemyCollidersByHealth = new Dictionary<Health, Collider[]>();
        private readonly HashSet<int> warnedInvalidPrefabs = new HashSet<int>();
        private Coroutine runRoutine;
        private float nextEnemyPruneAt;

        protected override void OnIaEnable()
        {
            if (runRoutine == null)
            {
                runRoutine = StartCoroutine(RunLoop());
            }
        }

        protected override void OnIaDisable()
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

            activeEnemyColliders.Clear();
            enemyCollidersByHealth.Clear();
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
                CurrentRoomType = DetermineRoomType(room);
                OnRoomChanged?.Invoke(CurrentRoom, totalRooms);
                IaEventBus.Publish(new RoomChangedEvent(CurrentRoom, totalRooms));
                OnRoomTypeChanged?.Invoke(CurrentRoom, totalRooms, CurrentRoomType);
                IaEventBus.Publish(new RoomTypeChangedEvent(CurrentRoom, totalRooms, (int)CurrentRoomType));

                SpawnRoom(room, CurrentRoomType);
                OnEnemiesRemainingChanged?.Invoke(aliveEnemies.Count);
                IaEventBus.Publish(new EnemiesRemainingChangedEvent(aliveEnemies.Count));
                nextEnemyPruneAt = Time.time + 0.5f;

                while (aliveEnemies.Count > 0)
                {
                    if (Time.time >= nextEnemyPruneAt)
                    {
                        int before = aliveEnemies.Count;
                        aliveEnemies.RemoveAll(enemy => enemy == null || enemy.IsDead);
                        nextEnemyPruneAt = Time.time + 0.5f;

                        if (aliveEnemies.Count != before)
                        {
                            OnEnemiesRemainingChanged?.Invoke(aliveEnemies.Count);
                            IaEventBus.Publish(new EnemiesRemainingChangedEvent(aliveEnemies.Count));
                        }
                    }

                    yield return null;
                }

                OnRoomCleared?.Invoke(CurrentRoom, totalRooms);
                IaEventBus.Publish(new RoomClearedEvent(CurrentRoom, totalRooms));

                if (room < totalRooms)
                {
                    if (CurrentRoomType == RoomType.Combat)
                    {
                        TrySpawnClockPickup();
                    }

                    yield return new WaitForSeconds(delayBetweenRooms);
                }
            }

            OnRunWon?.Invoke();
            IaEventBus.Publish(new RunWonEvent());
        }

        private void SpawnRoom(int roomNumber, RoomType roomType)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("RunDirector has no spawn points assigned.", this);
                return;
            }

            if (roomType == RoomType.Boss && bossPrefab != null)
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

            if (roomType == RoomType.Reward)
            {
                SpawnGuaranteedClockPickups(Mathf.Max(1, rewardClockPickups));
                return;
            }

            int enemiesToSpawn = ComputeEnemiesToSpawn(roomNumber, roomType);
            int effectivePickRoom = roomType == RoomType.Risk ? roomNumber + 1 : roomNumber;

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                GameObject enemyPrefab = PickEnemyPrefab(effectivePickRoom);
                if (enemyPrefab == null)
                {
                    continue;
                }

                SpawnEnemy(enemyPrefab, GetSpawnPosition());
            }

            if (roomType == RoomType.Risk)
            {
                SpawnGuaranteedClockPickups(Mathf.Max(1, riskGuaranteedClockPickups));
            }
        }

        private void SpawnEnemy(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            if (HasMissingScriptReference(prefab))
            {
                int id = prefab.GetInstanceID();
                if (!warnedInvalidPrefabs.Contains(id))
                {
                    warnedInvalidPrefabs.Add(id);
                    Debug.LogError($"RunDirector skipped spawning invalid prefab '{prefab.name}' because it has missing script references. Rebuild generated prefabs.", this);
                }

                return;
            }

            GameObject enemy = Instantiate(prefab, position, Quaternion.identity);
            Health health = enemy.GetComponentInChildren<Health>();
            if (health == null)
            {
                return;
            }

            health.OnDied += OnEnemyDied;
            aliveEnemies.Add(health);
            RegisterEnemyColliders(health, enemy.GetComponentsInChildren<Collider>(true));
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
            UnregisterEnemyColliders(health);
            aliveEnemies.Remove(health);
            OnEnemiesRemainingChanged?.Invoke(aliveEnemies.Count);
            IaEventBus.Publish(new EnemiesRemainingChangedEvent(aliveEnemies.Count));
        }

        private void TrySpawnClockPickup()
        {
            if (clockPickupPrefab == null || UnityEngine.Random.value > interRoomClockPickupChance)
            {
                return;
            }

            Instantiate(clockPickupPrefab, GetSpawnPosition(), Quaternion.identity);
        }

        private RoomType DetermineRoomType(int roomNumber)
        {
            if (roomNumber >= totalRooms)
            {
                return RoomType.Boss;
            }

            if (roomNumber <= Mathf.Max(0, guaranteedCombatRooms))
            {
                return RoomType.Combat;
            }

            float reward = Mathf.Clamp01(rewardRoomChance);
            float risk = Mathf.Clamp01(riskRoomChance);
            float combat = Mathf.Max(0f, 1f - (reward + risk));
            float total = reward + risk + combat;
            if (total <= 0.001f)
            {
                return RoomType.Combat;
            }

            float roll = UnityEngine.Random.value * total;
            if (roll < reward)
            {
                return RoomType.Reward;
            }

            if (roll < reward + risk)
            {
                return RoomType.Risk;
            }

            return RoomType.Combat;
        }

        private int ComputeEnemiesToSpawn(int roomNumber, RoomType roomType)
        {
            int baseCount = UnityEngine.Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
            int roomScaledCount = baseCount + Mathf.FloorToInt((roomNumber - 1) * Mathf.Max(0f, roomEnemyScalePerRoom));

            float pressureMultiplier = 1f;
            TimeManager timeManager = TimeManager.Instance;
            if (timeManager != null && lowTimePressureThreshold > 0.001f)
            {
                float normalizedPressure = 1f - Mathf.Clamp01(timeManager.TimeRemaining / lowTimePressureThreshold);
                pressureMultiplier += normalizedPressure * Mathf.Max(0f, lowTimeEnemyBonusMultiplier);
            }

            if (roomType == RoomType.Risk)
            {
                pressureMultiplier *= Mathf.Max(1f, riskEnemyMultiplier);
            }

            int spawnedCount = Mathf.RoundToInt(roomScaledCount * pressureMultiplier);
            int maxCount = maxEnemiesPerRoom + 4 + Mathf.Max(0, maxAdditionalEnemiesFromPressure);
            return Mathf.Clamp(spawnedCount, minEnemiesPerRoom, maxCount);
        }

        private void SpawnGuaranteedClockPickups(int count)
        {
            if (clockPickupPrefab == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Instantiate(clockPickupPrefab, GetSpawnPosition(), Quaternion.identity);
            }
        }

        private static bool HasMissingScriptReference(GameObject prefab)
        {
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        private void RegisterEnemyColliders(Health health, Collider[] colliders)
        {
            if (health == null || colliders == null || colliders.Length == 0)
            {
                return;
            }

            Transform enemyRoot = health.transform.root != null ? health.transform.root : health.transform;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider newCollider = colliders[i];
                if (newCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < activeEnemyColliders.Count; j++)
                {
                    Collider existing = activeEnemyColliders[j];
                    if (existing == null)
                    {
                        continue;
                    }

                    Transform existingRoot = existing.transform.root != null ? existing.transform.root : existing.transform;
                    if (existingRoot == enemyRoot)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(newCollider, existing, true);
                }

                activeEnemyColliders.Add(newCollider);
            }

            enemyCollidersByHealth[health] = colliders;
        }

        private void UnregisterEnemyColliders(Health health)
        {
            if (health == null)
            {
                return;
            }

            if (!enemyCollidersByHealth.TryGetValue(health, out Collider[] colliders) || colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider toRemove = colliders[i];
                if (toRemove == null)
                {
                    continue;
                }

                activeEnemyColliders.Remove(toRemove);
            }

            enemyCollidersByHealth.Remove(health);
        }
    }
}
