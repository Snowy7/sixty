using Sixty.Core;
using Sixty.Gameplay;
using TMPro;
using UnityEngine;

namespace Sixty.UI
{
    public class RunHudView : MonoBehaviour
    {
        [SerializeField] private RunDirector runDirector;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text deathText;
        [SerializeField] private TMP_Text roomText;
        [SerializeField] private TMP_Text enemiesText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Color normalTimeColor = Color.white;
        [SerializeField] private Color lowTimeColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color timeGainColor = new Color(0.45f, 1f, 0.6f, 1f);
        [SerializeField] private Color timeLossColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float pulseDuration = 0.2f;
        [SerializeField] private float pulseScaleMultiplier = 1.12f;

        private TimeManager timeManager;
        private bool boundTimeEvents;
        private bool boundRunEvents;
        private float gainPulseTimer;
        private float lossPulseTimer;
        private Vector3 timeLabelBaseScale = Vector3.one;

        private void OnEnable()
        {
            TryBind();
            if (timeText != null)
            {
                timeLabelBaseScale = timeText.rectTransform.localScale;
            }

            RefreshAllText();
        }

        private void Update()
        {
            if (!boundTimeEvents || !boundRunEvents)
            {
                TryBind();
            }

            if (timeManager != null)
            {
                UpdateTimeLabel(timeManager.TimeRemaining);
            }

            UpdateTimePulseVisuals();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void TryBind()
        {
            if (!boundTimeEvents)
            {
                timeManager = TimeManager.Instance;
                if (timeManager != null)
                {
                    timeManager.OnTimeChanged += HandleTimeChanged;
                    timeManager.OnDeathCountChanged += HandleDeathCountChanged;
                    timeManager.OnTimeOut += HandleTimeOut;
                    boundTimeEvents = true;
                }
            }

            if (!boundRunEvents && runDirector != null)
            {
                runDirector.OnRoomChanged += HandleRoomChanged;
                runDirector.OnEnemiesRemainingChanged += HandleEnemiesRemainingChanged;
                runDirector.OnRunWon += HandleRunWon;
                boundRunEvents = true;
            }
        }

        private void Unbind()
        {
            if (timeManager != null && boundTimeEvents)
            {
                timeManager.OnTimeChanged -= HandleTimeChanged;
                timeManager.OnDeathCountChanged -= HandleDeathCountChanged;
                timeManager.OnTimeOut -= HandleTimeOut;
            }

            if (runDirector != null && boundRunEvents)
            {
                runDirector.OnRoomChanged -= HandleRoomChanged;
                runDirector.OnEnemiesRemainingChanged -= HandleEnemiesRemainingChanged;
                runDirector.OnRunWon -= HandleRunWon;
            }

            boundTimeEvents = false;
            boundRunEvents = false;
        }

        private void RefreshAllText()
        {
            if (timeManager != null)
            {
                UpdateTimeLabel(timeManager.TimeRemaining);
                HandleDeathCountChanged(timeManager.DeathCount);
            }
            else
            {
                UpdateTimeLabel(0f);
                HandleDeathCountChanged(0);
            }

            if (runDirector != null)
            {
                HandleRoomChanged(runDirector.CurrentRoom, 10);
                HandleEnemiesRemainingChanged(runDirector.EnemiesAlive);
            }

            SetText(statusText, string.Empty);
        }

        private void HandleTimeChanged(float remaining, float delta)
        {
            if (delta > 0f)
            {
                gainPulseTimer = pulseDuration;
            }
            else if (delta < 0f)
            {
                lossPulseTimer = pulseDuration;
            }

            UpdateTimeLabel(remaining);
        }

        private void HandleDeathCountChanged(int deaths)
        {
            SetText(deathText, $"Deaths: {deaths}");
        }

        private void HandleRoomChanged(int room, int total)
        {
            if (room <= 0)
            {
                SetText(roomText, "Room: --");
                return;
            }

            SetText(roomText, $"Room: {room}/{total}");
        }

        private void HandleEnemiesRemainingChanged(int remaining)
        {
            SetText(enemiesText, $"Enemies: {Mathf.Max(remaining, 0)}");
        }

        private void HandleRunWon()
        {
            SetText(statusText, "Run Complete");
        }

        private void HandleTimeOut()
        {
            SetText(statusText, "Time Out");
        }

        private void UpdateTimeLabel(float seconds)
        {
            SetText(timeText, $"Time: {seconds:0.0}s");

            if (timeText != null)
            {
                Color baseColor = seconds <= lowTimeThreshold ? lowTimeColor : normalTimeColor;
                if (lossPulseTimer > 0f)
                {
                    float pulse = Mathf.Clamp01(lossPulseTimer / pulseDuration);
                    timeText.color = Color.Lerp(baseColor, timeLossColor, pulse);
                }
                else if (gainPulseTimer > 0f)
                {
                    float pulse = Mathf.Clamp01(gainPulseTimer / pulseDuration);
                    timeText.color = Color.Lerp(baseColor, timeGainColor, pulse);
                }
                else
                {
                    timeText.color = baseColor;
                }
            }
        }

        private void UpdateTimePulseVisuals()
        {
            if (gainPulseTimer > 0f)
            {
                gainPulseTimer = Mathf.Max(0f, gainPulseTimer - Time.deltaTime);
            }

            if (lossPulseTimer > 0f)
            {
                lossPulseTimer = Mathf.Max(0f, lossPulseTimer - Time.deltaTime);
            }

            if (timeText == null)
            {
                return;
            }

            float pulseStrength = Mathf.Max(
                gainPulseTimer > 0f ? gainPulseTimer / pulseDuration : 0f,
                lossPulseTimer > 0f ? lossPulseTimer / pulseDuration : 0f);

            float scale = Mathf.Lerp(1f, pulseScaleMultiplier, pulseStrength);
            timeText.rectTransform.localScale = timeLabelBaseScale * scale;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
