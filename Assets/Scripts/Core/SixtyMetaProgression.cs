using System;
using System.IO;
using UnityEngine;

namespace Sixty.Core
{
    public readonly struct MetaProgressionSnapshot
    {
        public readonly int DeathCount;
        public readonly bool ShotgunUnlocked;
        public readonly bool ChargeBeamUnlocked;
        public readonly bool PassivesUnlocked;
        public readonly bool StartingBonusUnlocked;
        public readonly float StartingBonusSeconds;
        public readonly int BossClears;
        public readonly float BestBossClearTimeRemaining;

        public MetaProgressionSnapshot(
            int deathCount,
            bool shotgunUnlocked,
            bool chargeBeamUnlocked,
            bool passivesUnlocked,
            bool startingBonusUnlocked,
            float startingBonusSeconds,
            int bossClears,
            float bestBossClearTimeRemaining)
        {
            DeathCount = deathCount;
            ShotgunUnlocked = shotgunUnlocked;
            ChargeBeamUnlocked = chargeBeamUnlocked;
            PassivesUnlocked = passivesUnlocked;
            StartingBonusUnlocked = startingBonusUnlocked;
            StartingBonusSeconds = startingBonusSeconds;
            BossClears = bossClears;
            BestBossClearTimeRemaining = bestBossClearTimeRemaining;
        }
    }

    public static class SixtyMetaProgression
    {
        [Serializable]
        private sealed class MetaData
        {
            public int version = 1;
            public int recordedDeaths;
            public int bossClears;
            public float bestBossClearTimeRemaining;
            public string lastUpdatedUtc = string.Empty;
        }

        private const int ShotgunUnlockDeaths = 5;
        private const int ChargeBeamUnlockDeaths = 10;
        private const int PassiveUnlockDeaths = 10;
        private const int StartingBonusUnlockDeaths = 25;
        private const float StartingBonusSeconds = 5f;
        private const string FileName = "sixty_meta.json";

        private static readonly object Sync = new object();
        private static MetaData cachedData;
        private static bool loaded;

        public static MetaProgressionSnapshot GetSnapshot(int deathCount)
        {
            lock (Sync)
            {
                EnsureLoaded();
                int effectiveDeaths = Mathf.Max(Mathf.Max(0, deathCount), cachedData.recordedDeaths);
                bool shotgunUnlocked = effectiveDeaths >= ShotgunUnlockDeaths;
                bool chargeBeamUnlocked = effectiveDeaths >= ChargeBeamUnlockDeaths;
                bool passivesUnlocked = effectiveDeaths >= PassiveUnlockDeaths;
                bool startingBonusUnlocked = effectiveDeaths >= StartingBonusUnlockDeaths;

                return new MetaProgressionSnapshot(
                    effectiveDeaths,
                    shotgunUnlocked,
                    chargeBeamUnlocked,
                    passivesUnlocked,
                    startingBonusUnlocked,
                    startingBonusUnlocked ? StartingBonusSeconds : 0f,
                    Mathf.Max(0, cachedData.bossClears),
                    Mathf.Max(0f, cachedData.bestBossClearTimeRemaining));
            }
        }

        public static void RecordDeath(int deathCount)
        {
            lock (Sync)
            {
                EnsureLoaded();
                int clamped = Mathf.Max(0, deathCount);
                if (clamped <= cachedData.recordedDeaths)
                {
                    return;
                }

                cachedData.recordedDeaths = clamped;
                cachedData.lastUpdatedUtc = DateTime.UtcNow.ToString("O");
                SaveUnsafe();
            }
        }

        public static void RecordBossClear(float timeRemaining)
        {
            lock (Sync)
            {
                EnsureLoaded();
                cachedData.bossClears = Mathf.Max(0, cachedData.bossClears) + 1;
                cachedData.bestBossClearTimeRemaining = Mathf.Max(
                    Mathf.Max(0f, cachedData.bestBossClearTimeRemaining),
                    Mathf.Max(0f, timeRemaining));
                cachedData.lastUpdatedUtc = DateTime.UtcNow.ToString("O");
                SaveUnsafe();
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            cachedData = new MetaData();

            string path = GetSavePath();
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                MetaData parsed = JsonUtility.FromJson<MetaData>(json);
                if (parsed != null)
                {
                    cachedData = parsed;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load meta progression: {ex.Message}");
                cachedData = new MetaData();
            }
        }

        private static void SaveUnsafe()
        {
            string path = GetSavePath();
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(cachedData, true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save meta progression: {ex.Message}");
            }
        }

        private static string GetSavePath()
        {
            string root = Application.persistentDataPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = ".";
            }

            return Path.Combine(root, FileName);
        }
    }
}
