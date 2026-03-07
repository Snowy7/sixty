using System;
using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sixty.Core
{
    public class TimeManager : IaBehaviour
    {
        private const string DeathCountPrefsKey = "Sixty.DeathCount";

        [Header("Clock Rules")]
        [SerializeField] private float baseTimeSeconds = 60f;
        [SerializeField] private float timePerDeathSeconds = 10f;
        [SerializeField] private float maxStartingTimeSeconds = 300f;
        [SerializeField] private bool restartSceneOnTimeout = true;

        public static TimeManager Instance { get; private set; }

        public float TimeRemaining { get; private set; }
        public int DeathCount { get; private set; }
        public bool IsOutOfTime => TimeRemaining <= 0f;
        public bool IsClockPaused => isClockPaused;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        public event Action<float, float> OnTimeChanged;
        public event Action<int> OnDeathCountChanged;
        public event Action OnTimeOut;

        private bool hasTimedOut;
        private bool isClockPaused;

        protected override void OnIaAwake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Time.timeScale = 1f;
            DeathCount = Mathf.Max(0, PlayerPrefs.GetInt(DeathCountPrefsKey, 0));
            SixtyMetaProgression.RecordDeath(DeathCount);
            ResetRunClock();
        }

        protected override void OnIaDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (hasTimedOut || isClockPaused)
            {
                return;
            }

            Tick(deltaTime);
        }

        public float GetStartingTimeForCurrentDeathCount()
        {
            float startTime = baseTimeSeconds + (DeathCount * timePerDeathSeconds);
            MetaProgressionSnapshot snapshot = SixtyMetaProgression.GetSnapshot(DeathCount);
            if (snapshot.StartingBonusUnlocked)
            {
                startTime += snapshot.StartingBonusSeconds;
            }

            return Mathf.Min(startTime, maxStartingTimeSeconds);
        }

        public void ResetRunClock()
        {
            hasTimedOut = false;
            isClockPaused = false;
            TimeRemaining = GetStartingTimeForCurrentDeathCount();
            OnTimeChanged?.Invoke(TimeRemaining, 0f);
            IaEventBus.Publish(new TimeChangedEvent(TimeRemaining, 0f));
        }

        public void SetClockPaused(bool paused)
        {
            isClockPaused = paused;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || hasTimedOut || isClockPaused)
            {
                return;
            }

            SetTimeInternal(TimeRemaining - deltaSeconds);
        }

        public void AddTime(float seconds)
        {
            if (seconds <= 0f || hasTimedOut)
            {
                return;
            }

            SetTimeInternal(TimeRemaining + seconds);
        }

        public void TakeDamage(float seconds = 2f)
        {
            if (seconds <= 0f || hasTimedOut)
            {
                return;
            }

            SetTimeInternal(TimeRemaining - seconds);
        }

        private void SetTimeInternal(float targetSeconds)
        {
            float clamped = Mathf.Max(0f, targetSeconds);
            float delta = clamped - TimeRemaining;

            if (Mathf.Approximately(delta, 0f))
            {
                return;
            }

            TimeRemaining = clamped;
            OnTimeChanged?.Invoke(TimeRemaining, delta);
            IaEventBus.Publish(new TimeChangedEvent(TimeRemaining, delta));

            if (TimeRemaining <= 0f)
            {
                HandleTimeOut();
            }
        }

        private void HandleTimeOut()
        {
            if (hasTimedOut)
            {
                return;
            }

            hasTimedOut = true;
            isClockPaused = false;
            DeathCount++;
            PlayerPrefs.SetInt(DeathCountPrefsKey, DeathCount);
            PlayerPrefs.Save();
            SixtyMetaProgression.RecordDeath(DeathCount);

            OnDeathCountChanged?.Invoke(DeathCount);
            OnTimeOut?.Invoke();
            IaEventBus.Publish(new DeathCountChangedEvent(DeathCount));
            IaEventBus.Publish(new TimeOutEvent(DeathCount));

            if (restartSceneOnTimeout)
            {
                SceneTransitionOverlay overlay = SceneTransitionOverlay.EnsureInstance();
                int buildIndex = SceneManager.GetActiveScene().buildIndex;
                overlay.TransitionToScene(buildIndex);
            }
        }
    }

    public struct TimeChangedEvent
    {
        public float Remaining;
        public float Delta;

        public TimeChangedEvent(float remaining, float delta)
        {
            Remaining = remaining;
            Delta = delta;
        }
    }

    public struct DeathCountChangedEvent
    {
        public int DeathCount;

        public DeathCountChangedEvent(int deathCount)
        {
            DeathCount = deathCount;
        }
    }

    public struct TimeOutEvent
    {
        public int DeathCount;

        public TimeOutEvent(int deathCount)
        {
            DeathCount = deathCount;
        }
    }

    public struct RoomChangedEvent
    {
        public int Room;
        public int TotalRooms;

        public RoomChangedEvent(int room, int totalRooms)
        {
            Room = room;
            TotalRooms = totalRooms;
        }
    }

    public struct RoomTypeChangedEvent
    {
        public int Room;
        public int TotalRooms;
        public int RoomType;

        public RoomTypeChangedEvent(int room, int totalRooms, int roomType)
        {
            Room = room;
            TotalRooms = totalRooms;
            RoomType = roomType;
        }
    }

    public struct RoomClearedEvent
    {
        public int Room;
        public int TotalRooms;

        public RoomClearedEvent(int room, int totalRooms)
        {
            Room = room;
            TotalRooms = totalRooms;
        }
    }

    public struct EnemiesRemainingChangedEvent
    {
        public int Remaining;

        public EnemiesRemainingChangedEvent(int remaining)
        {
            Remaining = remaining;
        }
    }

    public struct RunWonEvent
    {
    }

    public struct RewardSelectionStartedEvent
    {
        public string Option1;
        public string Option2;
        public string Option3;

        public RewardSelectionStartedEvent(string option1, string option2, string option3)
        {
            Option1 = option1;
            Option2 = option2;
            Option3 = option3;
        }
    }

    public struct RewardSelectedEvent
    {
        public string Label;

        public RewardSelectedEvent(string label)
        {
            Label = label;
        }
    }

    public struct PassiveSelectedEvent
    {
        public int PassiveType;
        public string Label;

        public PassiveSelectedEvent(int passiveType, string label)
        {
            PassiveType = passiveType;
            Label = label;
        }
    }

    public struct EnemyKilledEvent
    {
        public Transform Victim;

        public EnemyKilledEvent(Transform victim)
        {
            Victim = victim;
        }
    }
}
