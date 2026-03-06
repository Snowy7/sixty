using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sixty.Core
{
    public class TimeManager : MonoBehaviour
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

        public event Action<float, float> OnTimeChanged;
        public event Action<int> OnDeathCountChanged;
        public event Action OnTimeOut;

        private bool hasTimedOut;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DeathCount = Mathf.Max(0, PlayerPrefs.GetInt(DeathCountPrefsKey, 0));
            ResetRunClock();
        }

        private void Update()
        {
            if (hasTimedOut)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        public float GetStartingTimeForCurrentDeathCount()
        {
            float startTime = baseTimeSeconds + (DeathCount * timePerDeathSeconds);
            return Mathf.Min(startTime, maxStartingTimeSeconds);
        }

        public void ResetRunClock()
        {
            hasTimedOut = false;
            TimeRemaining = GetStartingTimeForCurrentDeathCount();
            OnTimeChanged?.Invoke(TimeRemaining, 0f);
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || hasTimedOut)
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
            DeathCount++;
            PlayerPrefs.SetInt(DeathCountPrefsKey, DeathCount);
            PlayerPrefs.Save();

            OnDeathCountChanged?.Invoke(DeathCount);
            OnTimeOut?.Invoke();

            if (restartSceneOnTimeout)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                SceneManager.LoadScene(activeScene.buildIndex);
            }
        }
    }
}
