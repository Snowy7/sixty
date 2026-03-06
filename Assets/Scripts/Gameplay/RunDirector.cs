using System;
using System.Collections;
using System.Collections.Generic;
using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Core;
using Sixty.Player;
using Sixty.World;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class RunDirector : IaBehaviour
    {
        // Temporary hard lock for debugging progression issues.
        private static bool DebugHardLockRoomProgression = false;

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

        private struct RewardOption
        {
            public string label;
            public Color color;
            public bool isWeaponSwap;
            public bool isPassive;
            public Action apply;
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
        [SerializeField] private int minRewardRoomsPerRun = 2;
        [SerializeField] private int maxRoomsWithoutReward = 3;
        [SerializeField] private int riskGuaranteedClockPickups = 1;
        [SerializeField] private float riskEnemyMultiplier = 1.35f;

        [Header("Difficulty Scaling")]
        [SerializeField] private float roomEnemyScalePerRoom = 0.45f;
        [SerializeField] private float lowTimePressureThreshold = 20f;
        [SerializeField] private float lowTimeEnemyBonusMultiplier = 0.55f;
        [SerializeField] private int maxAdditionalEnemiesFromPressure = 3;

        [Header("Spawns")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform[] roomAnchors;
        [SerializeField] private List<EnemySpawnEntry> enemyPrefabs = new List<EnemySpawnEntry>();
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private ClockPickup clockPickupPrefab;
        [SerializeField] private RewardPickup rewardPickupPrefab;
        [SerializeField] [Range(0f, 1f)] private float interRoomClockPickupChance = 0.75f;

        [Header("Room Transition")]
        [SerializeField] private bool requireDoorTransition = true;
        [SerializeField] private RunExitDoor[] exitDoors;
        [SerializeField] private float currentRoomDoorRadius = 40f;
        [SerializeField] private float exitDoorAutoAdvanceTimeout = 18f;
        [SerializeField] private float transitionSpawnOffset = 6.5f;

        [Header("Rewards")]
        [SerializeField] private WeaponDefinition shotgunWeapon;
        [SerializeField] private WeaponDefinition chargeBeamWeapon;
        [SerializeField] private float fireRateRewardMultiplier = 1.16f;
        [SerializeField] private float damageRewardMultiplier = 1.22f;
        [SerializeField] private float projectileSpeedRewardMultiplier = 1.14f;
        [SerializeField] private float timeRewardSeconds = 8f;
        [SerializeField] private float rewardSpawnRadius = 4.5f;
        [SerializeField] private float rewardHoverHeight = 1.15f;

        public int CurrentRoom { get; private set; }
        public RoomType CurrentRoomType { get; private set; } = RoomType.Combat;
        public Vector3 CurrentRoomOrigin => currentRoomOrigin;
        public int EnemiesAlive => aliveEnemies.Count;

        public void SetRuntimeArenaRefs(Transform[] anchors, Transform[] spawns, RunExitDoor[] doors)
        {
            roomAnchors = anchors;
            spawnPoints = spawns;
            exitDoors = doors;
        }

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
        private int rewardRoomsSpawned;
        private int roomsSinceLastReward;
        private Vector3 currentRoomOrigin;

        protected override void OnIaEnable()
        {
            rewardRoomsSpawned = 0;
            roomsSinceLastReward = 0;
            currentRoomOrigin = ResolveRoomOrigin(1);
            LockAllExitDoors(true);
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
            TimeManager.Instance?.SetClockPaused(false);
            LockAllExitDoors(true);
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
                currentRoomOrigin = ResolveRoomOrigin(room);
                CurrentRoomType = DetermineRoomType(room);
                RegisterChosenRoomType(CurrentRoomType);
                OnRoomChanged?.Invoke(CurrentRoom, totalRooms);
                IaEventBus.Publish(new RoomChangedEvent(CurrentRoom, totalRooms));
                OnRoomTypeChanged?.Invoke(CurrentRoom, totalRooms, CurrentRoomType);
                IaEventBus.Publish(new RoomTypeChangedEvent(CurrentRoom, totalRooms, (int)CurrentRoomType));
                LockAllExitDoors(true);

                bool pausedThisRoom = ShouldPauseClockForRoom(CurrentRoomType);
                if (pausedThisRoom)
                {
                    TimeManager.Instance?.SetClockPaused(true);
                }

                try
                {
                    SpawnRoom(room, CurrentRoomType);
                    OnEnemiesRemainingChanged?.Invoke(aliveEnemies.Count);
                    IaEventBus.Publish(new EnemiesRemainingChangedEvent(aliveEnemies.Count));
                    nextEnemyPruneAt = Time.time + 0.5f;

                    if (DebugHardLockRoomProgression)
                    {
                        Debug.LogWarning(
                            $"RunDirector hard lock active at room {CurrentRoom} ({CurrentRoomType}). " +
                            $"Enemies spawned: {aliveEnemies.Count}. Progression is intentionally blocked.",
                            this);

                        while (enabled)
                        {
                            yield return null;
                        }

                        yield break;
                    }

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
                        if (CurrentRoomType == RoomType.Reward)
                        {
                            yield return RunRewardSelection();
                        }

                        if (CurrentRoomType == RoomType.Combat)
                        {
                            TrySpawnClockPickup();
                        }

                        if (requireDoorTransition && HasValidExitDoors())
                        {
                            yield return WaitForExitDoorTransition();
                        }
                        else
                        {
                            yield return new WaitForSeconds(delayBetweenRooms);
                        }
                    }
                }
                finally
                {
                    if (pausedThisRoom)
                    {
                        TimeManager.Instance?.SetClockPaused(false);
                    }
                }
            }

            OnRunWon?.Invoke();
            if (TimeManager.Instance != null)
            {
                SixtyMetaProgression.RecordBossClear(TimeManager.Instance.TimeRemaining);
            }

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
                SpawnEnemy(bossPrefab, GetSpawnPosition(currentRoomOrigin));

                int bonusAdds = Mathf.Clamp(roomNumber / 3, 1, 3);
                for (int i = 0; i < bonusAdds; i++)
                {
                    GameObject addPrefab = PickEnemyPrefab(roomNumber - 1);
                    if (addPrefab != null)
                    {
                        SpawnEnemy(addPrefab, GetSpawnPosition(currentRoomOrigin));
                    }
                }

                return;
            }

            if (roomType == RoomType.Reward)
            {
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

                SpawnEnemy(enemyPrefab, GetSpawnPosition(currentRoomOrigin));
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

                if (HasMissingScriptReference(entry.prefab))
                {
                    int id = entry.prefab.GetInstanceID();
                    if (!warnedInvalidPrefabs.Contains(id))
                    {
                        warnedInvalidPrefabs.Add(id);
                        Debug.LogError($"RunDirector excluded invalid enemy prefab '{entry.prefab.name}' from spawn table (missing script references). Rebuild generated prefabs.", this);
                    }

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

                if (HasMissingScriptReference(entry.prefab))
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

        private Vector3 GetSpawnPosition(Vector3 roomOrigin)
        {
            Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * spawnRadiusJitter;
            Vector3 template = spawnPoint != null ? spawnPoint.localPosition : Vector3.zero;
            return roomOrigin + template + new Vector3(jitter.x, 0f, jitter.y);
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

            Instantiate(clockPickupPrefab, GetSpawnPosition(currentRoomOrigin), Quaternion.identity);
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

            int nonBossTotalRooms = Mathf.Max(1, totalRooms - 1);
            int nonBossRoomsPlayedBeforeThis = Mathf.Clamp(roomNumber - 1, 0, nonBossTotalRooms);
            int nonBossRoomsRemainingIncludingCurrent = nonBossTotalRooms - nonBossRoomsPlayedBeforeThis;
            int rewardsNeeded = Mathf.Max(0, minRewardRoomsPerRun - rewardRoomsSpawned);
            if (rewardsNeeded > 0 && nonBossRoomsRemainingIncludingCurrent <= rewardsNeeded)
            {
                return RoomType.Reward;
            }

            if (roomsSinceLastReward >= Mathf.Max(1, maxRoomsWithoutReward))
            {
                return RoomType.Reward;
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

        private void RegisterChosenRoomType(RoomType roomType)
        {
            if (roomType == RoomType.Boss)
            {
                return;
            }

            if (roomType == RoomType.Reward)
            {
                rewardRoomsSpawned++;
                roomsSinceLastReward = 0;
                return;
            }

            roomsSinceLastReward++;
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
                Instantiate(clockPickupPrefab, GetSpawnPosition(currentRoomOrigin), Quaternion.identity);
            }
        }

        private IEnumerator RunRewardSelection()
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("RunDirector could not find PlayerController for reward selection.", this);
                yield break;
            }

            WeaponController weaponController = player.GetComponentInChildren<WeaponController>();
            RunPassiveController passiveController = player.GetComponent<RunPassiveController>();
            List<RewardOption> options = BuildRewardOptions(weaponController, passiveController);
            if (options.Count < 3)
            {
                Debug.LogError("RunDirector could not build enough reward options. Reward room cannot continue.", this);
                yield break;
            }

            List<RewardOption> selectedOptions = SelectRewardOptions(options);
            if (selectedOptions.Count < 3)
            {
                yield break;
            }

            IaEventBus.Publish(new RewardSelectionStartedEvent(
                selectedOptions[0].label,
                selectedOptions[1].label,
                selectedOptions[2].label));

            player.SetCombatInputLocked(true);
            bool hasSelection = false;
            RewardOption chosen = selectedOptions[0];
            List<RewardPickup> spawned = new List<RewardPickup>(selectedOptions.Count);
            Vector3 center = currentRoomOrigin;
            center.y = rewardHoverHeight;
            float angleStep = 360f / selectedOptions.Count;

            for (int i = 0; i < selectedOptions.Count; i++)
            {
                RewardOption option = selectedOptions[i];
                float angle = i * angleStep;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * rewardSpawnRadius);
                RewardPickup pickup = SpawnRewardPickup(center + offset);
                pickup.Configure($"[{i + 1}] {option.label}", option.color, _ =>
                {
                    if (hasSelection)
                    {
                        return;
                    }

                    hasSelection = true;
                    chosen = option;
                });
                spawned.Add(pickup);
            }

            try
            {
                while (!hasSelection)
                {
                    if (TimeManager.Instance != null && TimeManager.Instance.IsOutOfTime)
                    {
                        break;
                    }

                    yield return null;
                }

                if (hasSelection && chosen.apply != null)
                {
                    chosen.apply.Invoke();
                    IaEventBus.Publish(new RewardSelectedEvent(chosen.label));
                }
            }
            finally
            {
                player.SetCombatInputLocked(false);
                for (int i = 0; i < spawned.Count; i++)
                {
                    if (spawned[i] != null)
                    {
                        Destroy(spawned[i].gameObject);
                    }
                }
            }
        }

        private RewardPickup SpawnRewardPickup(Vector3 position)
        {
            if (rewardPickupPrefab != null)
            {
                return Instantiate(rewardPickupPrefab, position, Quaternion.identity);
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallback.name = "RewardPickup_RuntimeFallback";
            fallback.transform.position = position;
            fallback.transform.localScale = Vector3.one * 0.95f;
            Collider collider = fallback.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            Rigidbody body = fallback.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            RewardPickup pickup = fallback.AddComponent<RewardPickup>();
            Debug.LogWarning("RunDirector used runtime fallback RewardPickup because rewardPickupPrefab was not assigned.", this);
            return pickup;
        }

        private List<RewardOption> BuildRewardOptions(WeaponController weaponController, RunPassiveController passiveController)
        {
            int deathCount = TimeManager.Instance != null ? TimeManager.Instance.DeathCount : 0;
            MetaProgressionSnapshot metaSnapshot = SixtyMetaProgression.GetSnapshot(deathCount);
            List<RewardOption> options = new List<RewardOption>(8)
            {
                new RewardOption
                {
                    label = $"+{Mathf.RoundToInt(timeRewardSeconds)}s Time",
                    color = new Color(1f, 0.9f, 0.35f, 1f),
                    isWeaponSwap = false,
                    isPassive = false,
                    apply = () => TimeManager.Instance?.AddTime(timeRewardSeconds)
                }
            };

            if (weaponController != null)
            {
                options.Add(new RewardOption
                {
                    label = $"+{Mathf.RoundToInt((fireRateRewardMultiplier - 1f) * 100f)}% Fire Rate",
                    color = new Color(0.35f, 0.9f, 1f, 1f),
                    isWeaponSwap = false,
                    isPassive = false,
                    apply = () => weaponController.ApplyFireRateMultiplier(fireRateRewardMultiplier)
                });

                options.Add(new RewardOption
                {
                    label = $"+{Mathf.RoundToInt((damageRewardMultiplier - 1f) * 100f)}% Damage",
                    color = new Color(1f, 0.45f, 0.35f, 1f),
                    isWeaponSwap = false,
                    isPassive = false,
                    apply = () => weaponController.ApplyDamageMultiplier(damageRewardMultiplier)
                });

                options.Add(new RewardOption
                {
                    label = $"+{Mathf.RoundToInt((projectileSpeedRewardMultiplier - 1f) * 100f)}% Projectile Speed",
                    color = new Color(0.55f, 0.7f, 1f, 1f),
                    isWeaponSwap = false,
                    isPassive = false,
                    apply = () => weaponController.ApplyProjectileSpeedMultiplier(projectileSpeedRewardMultiplier)
                });

                WeaponDefinition currentWeapon = weaponController.CurrentWeapon;
                if (metaSnapshot.ShotgunUnlocked && shotgunWeapon != null && currentWeapon != shotgunWeapon)
                {
                    options.Add(new RewardOption
                    {
                        label = "Equip Shotgun",
                        color = new Color(1f, 0.65f, 0.25f, 1f),
                        isWeaponSwap = true,
                        isPassive = false,
                        apply = () => weaponController.SetWeapon(shotgunWeapon)
                    });
                }

                if (metaSnapshot.ChargeBeamUnlocked && chargeBeamWeapon != null && currentWeapon != chargeBeamWeapon)
                {
                    options.Add(new RewardOption
                    {
                        label = "Equip Charge Beam",
                        color = new Color(0.85f, 0.55f, 1f, 1f),
                        isWeaponSwap = true,
                        isPassive = false,
                        apply = () => weaponController.SetWeapon(chargeBeamWeapon)
                    });
                }
            }

            if (metaSnapshot.PassivesUnlocked && passiveController != null && !passiveController.HasPassive)
            {
                options.Add(new RewardOption
                {
                    label = "Passive: Adrenaline",
                    color = new Color(0.65f, 1f, 0.45f, 1f),
                    isWeaponSwap = false,
                    isPassive = true,
                    apply = () => passiveController.TryApplyPassive(RunPassiveType.Adrenaline)
                });

                options.Add(new RewardOption
                {
                    label = "Passive: Overclock",
                    color = new Color(1f, 0.5f, 0.45f, 1f),
                    isWeaponSwap = false,
                    isPassive = true,
                    apply = () => passiveController.TryApplyPassive(RunPassiveType.Overclock)
                });

                options.Add(new RewardOption
                {
                    label = "Passive: Second Wind",
                    color = new Color(0.5f, 0.8f, 1f, 1f),
                    isWeaponSwap = false,
                    isPassive = true,
                    apply = () => passiveController.TryApplyPassive(RunPassiveType.SecondWind)
                });
            }

            while (options.Count < 3)
            {
                options.Add(new RewardOption
                {
                    label = $"+{Mathf.RoundToInt(timeRewardSeconds * 0.5f)}s Time",
                    color = new Color(1f, 0.82f, 0.32f, 1f),
                    isWeaponSwap = false,
                    isPassive = false,
                    apply = () => TimeManager.Instance?.AddTime(timeRewardSeconds * 0.5f)
                });
            }

            return options;
        }

        private static List<RewardOption> SelectRewardOptions(List<RewardOption> options)
        {
            List<RewardOption> pool = new List<RewardOption>(options);
            List<RewardOption> result = new List<RewardOption>(3);

            int guaranteedWeaponIndex = -1;
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].isWeaponSwap)
                {
                    continue;
                }

                guaranteedWeaponIndex = i;
                break;
            }

            if (guaranteedWeaponIndex >= 0)
            {
                result.Add(pool[guaranteedWeaponIndex]);
                pool.RemoveAt(guaranteedWeaponIndex);
            }

            int guaranteedPassiveIndex = -1;
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].isPassive)
                {
                    continue;
                }

                guaranteedPassiveIndex = i;
                break;
            }

            if (guaranteedPassiveIndex >= 0 && result.Count < 3)
            {
                result.Add(pool[guaranteedPassiveIndex]);
                pool.RemoveAt(guaranteedPassiveIndex);
            }

            while (result.Count < 3 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
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

        private static bool ShouldPauseClockForRoom(RoomType roomType)
        {
            return roomType == RoomType.Reward;
        }

        private IEnumerator WaitForExitDoorTransition()
        {
            List<RunExitDoor> validDoors = GetCurrentRoomExitDoors();

            if (validDoors.Count == 0)
            {
                yield return new WaitForSeconds(delayBetweenRooms);
                yield break;
            }

            bool entered = false;
            PlayerController enteringPlayer = null;
            RunExitDoor enteredDoor = null;
            void HandleDoorEntered(RunExitDoor door, PlayerController player)
            {
                if (entered || door == null || player == null)
                {
                    return;
                }

                entered = true;
                enteredDoor = door;
                enteringPlayer = player;
            }

            for (int i = 0; i < validDoors.Count; i++)
            {
                validDoors[i].OnPlayerEntered += HandleDoorEntered;
                validDoors[i].Open(true);
            }

            float timeoutAt = Time.unscaledTime + Mathf.Max(2f, exitDoorAutoAdvanceTimeout);
            while (!entered && enabled)
            {
                if (TimeManager.Instance != null && TimeManager.Instance.IsOutOfTime)
                {
                    break;
                }

                if (Time.unscaledTime >= timeoutAt)
                {
                    break;
                }

                yield return null;
            }

            for (int i = 0; i < validDoors.Count; i++)
            {
                validDoors[i].OnPlayerEntered -= HandleDoorEntered;
            }

            if (enteringPlayer == null)
            {
                enteringPlayer = FindFirstObjectByType<PlayerController>();
            }

            if (enteredDoor == null && validDoors.Count > 0)
            {
                enteredDoor = validDoors[0];
            }

            if (enteringPlayer != null)
            {
                RepositionPlayerForNextRoom(enteringPlayer, enteredDoor);
            }

            for (int i = 0; i < validDoors.Count; i++)
            {
                validDoors[i].Close(true);
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, delayBetweenRooms * 0.25f));
        }

        private void RepositionPlayerForNextRoom(PlayerController player, RunExitDoor sourceDoor)
        {
            if (player == null)
            {
                return;
            }

            float offset = Mathf.Max(2f, transitionSpawnOffset);
            Vector3 nextRoomOrigin = ResolveRoomOrigin(CurrentRoom + 1);
            Vector3 localOffset = sourceDoor != null ? sourceDoor.Direction switch
            {
                RunExitDoorDirection.North => new Vector3(0f, 0f, -offset),
                RunExitDoorDirection.South => new Vector3(0f, 0f, offset),
                RunExitDoorDirection.East => new Vector3(-offset, 0f, 0f),
                RunExitDoorDirection.West => new Vector3(offset, 0f, 0f),
                _ => Vector3.zero
            } : Vector3.zero;

            Vector3 targetPos = nextRoomOrigin + localOffset;
            targetPos.y = player.transform.position.y;
            player.transform.position = targetPos;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private RunExitDoor SelectExitDoor()
        {
            if (exitDoors == null || exitDoors.Length == 0)
            {
                return null;
            }

            List<RunExitDoor> valid = new List<RunExitDoor>(exitDoors.Length);
            for (int i = 0; i < exitDoors.Length; i++)
            {
                RunExitDoor door = exitDoors[i];
                if (door != null)
                {
                    valid.Add(door);
                }
            }

            if (valid.Count == 0)
            {
                return null;
            }

            int hash = (CurrentRoom * 92821) ^ (((int)CurrentRoomType + 11) * 68917);
            if (hash == int.MinValue)
            {
                hash = int.MaxValue;
            }

            int index = Mathf.Abs(hash) % valid.Count;
            return valid[index];
        }

        private bool HasValidExitDoors()
        {
            return GetCurrentRoomExitDoors().Count > 0;
        }

        private void LockAllExitDoors(bool instant)
        {
            if (exitDoors == null)
            {
                return;
            }

            for (int i = 0; i < exitDoors.Length; i++)
            {
                if (exitDoors[i] != null)
                {
                    exitDoors[i].SetLocked(true, instant);
                }
            }
        }

        private List<RunExitDoor> GetCurrentRoomExitDoors()
        {
            List<RunExitDoor> valid = new List<RunExitDoor>(exitDoors != null ? exitDoors.Length : 0);
            if (exitDoors == null || exitDoors.Length == 0)
            {
                return valid;
            }

            float radius = Mathf.Max(4f, currentRoomDoorRadius);
            float radiusSqr = radius * radius;
            for (int i = 0; i < exitDoors.Length; i++)
            {
                RunExitDoor door = exitDoors[i];
                if (door == null)
                {
                    continue;
                }

                Vector3 delta = door.transform.position - currentRoomOrigin;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSqr)
                {
                    valid.Add(door);
                }
            }

            if (valid.Count > 0)
            {
                return valid;
            }

            for (int i = 0; i < exitDoors.Length; i++)
            {
                if (exitDoors[i] != null)
                {
                    valid.Add(exitDoors[i]);
                }
            }

            return valid;
        }

        private Vector3 ResolveRoomOrigin(int roomNumber)
        {
            if (roomAnchors != null && roomAnchors.Length > 0)
            {
                int index = Mathf.Clamp(roomNumber - 1, 0, roomAnchors.Length - 1);
                Transform anchor = roomAnchors[index];
                if (anchor != null)
                {
                    return anchor.position;
                }
            }

            return Vector3.zero;
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
