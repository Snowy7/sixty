using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Core;
using Sixty.Gameplay;
using Sixty.Player;
using TMPro;
using UnityEngine;

namespace Sixty.UI
{
    public class RunHudView : IaBehaviour
    {
        private const int ShotgunUnlockDeaths = 5;
        private const int ChargeBeamUnlockDeaths = 10;
        private const int StartingBonusUnlockDeaths = 25;

        [SerializeField] private RunDirector runDirector;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text deathText;
        [SerializeField] private TMP_Text roomText;
        [SerializeField] private TMP_Text enemiesText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text weaponText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Color normalTimeColor = Color.white;
        [SerializeField] private Color lowTimeColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color timeGainColor = new Color(0.45f, 1f, 0.6f, 1f);
        [SerializeField] private Color timeLossColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color combatStatusColor = new Color(0.66f, 0.9f, 1f, 1f);
        [SerializeField] private Color rewardStatusColor = new Color(1f, 0.85f, 0.38f, 1f);
        [SerializeField] private Color riskStatusColor = new Color(1f, 0.38f, 0.82f, 1f);
        [SerializeField] private Color bossStatusColor = new Color(1f, 0.28f, 0.62f, 1f);
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float pulseDuration = 0.2f;
        [SerializeField] private float pulseScaleMultiplier = 1.12f;

        private TimeManager timeManager;
        private WeaponController weaponController;
        private RunPassiveController passiveController;
        private float gainPulseTimer;
        private float lossPulseTimer;
        private Vector3 timeLabelBaseScale = Vector3.one;
        private float lastDisplayedTime = float.NaN;
        private bool terminalStatusLocked;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.UI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaEnable()
        {
            if (runDirector == null)
            {
                runDirector = FindFirstObjectByType<RunDirector>();
            }

            timeManager = TimeManager.Instance;
            weaponController = FindFirstObjectByType<WeaponController>();
            passiveController = FindFirstObjectByType<RunPassiveController>();

            if (timeText != null)
            {
                timeLabelBaseScale = timeText.rectTransform.localScale;
            }

            terminalStatusLocked = false;
            IaEventBus.Subscribe<TimeChangedEvent>(HandleTimeChangedEvent);
            IaEventBus.Subscribe<DeathCountChangedEvent>(HandleDeathCountChangedEvent);
            IaEventBus.Subscribe<TimeOutEvent>(HandleTimeOutEvent);
            IaEventBus.Subscribe<RoomChangedEvent>(HandleRoomChangedEvent);
            IaEventBus.Subscribe<RoomTypeChangedEvent>(HandleRoomTypeChangedEvent);
            IaEventBus.Subscribe<RoomClearedEvent>(HandleRoomClearedEvent);
            IaEventBus.Subscribe<EnemiesRemainingChangedEvent>(HandleEnemiesRemainingChangedEvent);
            IaEventBus.Subscribe<RunWonEvent>(HandleRunWonEvent);
            IaEventBus.Subscribe<RewardSelectionStartedEvent>(HandleRewardSelectionStartedEvent);
            IaEventBus.Subscribe<RewardSelectedEvent>(HandleRewardSelectedEvent);
            IaEventBus.Subscribe<PassiveSelectedEvent>(HandlePassiveSelectedEvent);

            RefreshAllText();
        }

        protected override void OnIaDisable()
        {
            IaEventBus.Unsubscribe<TimeChangedEvent>(HandleTimeChangedEvent);
            IaEventBus.Unsubscribe<DeathCountChangedEvent>(HandleDeathCountChangedEvent);
            IaEventBus.Unsubscribe<TimeOutEvent>(HandleTimeOutEvent);
            IaEventBus.Unsubscribe<RoomChangedEvent>(HandleRoomChangedEvent);
            IaEventBus.Unsubscribe<RoomTypeChangedEvent>(HandleRoomTypeChangedEvent);
            IaEventBus.Unsubscribe<RoomClearedEvent>(HandleRoomClearedEvent);
            IaEventBus.Unsubscribe<EnemiesRemainingChangedEvent>(HandleEnemiesRemainingChangedEvent);
            IaEventBus.Unsubscribe<RunWonEvent>(HandleRunWonEvent);
            IaEventBus.Unsubscribe<RewardSelectionStartedEvent>(HandleRewardSelectionStartedEvent);
            IaEventBus.Unsubscribe<RewardSelectedEvent>(HandleRewardSelectedEvent);
            IaEventBus.Unsubscribe<PassiveSelectedEvent>(HandlePassiveSelectedEvent);
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (timeManager == null)
            {
                timeManager = TimeManager.Instance;
            }

            if (weaponController == null)
            {
                weaponController = FindFirstObjectByType<WeaponController>();
            }

            if (passiveController == null)
            {
                passiveController = FindFirstObjectByType<RunPassiveController>();
            }

            UpdateWeaponLabel();
            UpdateTimePulseVisuals(deltaTime);
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
            SetText(rewardText, string.Empty);
            UpdateWeaponLabel();
        }

        private void HandleTimeChangedEvent(TimeChangedEvent evt) => HandleTimeChanged(evt.Remaining, evt.Delta);
        private void HandleDeathCountChangedEvent(DeathCountChangedEvent evt) => HandleDeathCountChanged(evt.DeathCount);
        private void HandleTimeOutEvent(TimeOutEvent evt) => HandleTimeOut();
        private void HandleRoomChangedEvent(RoomChangedEvent evt) => HandleRoomChanged(evt.Room, evt.TotalRooms);
        private void HandleRoomTypeChangedEvent(RoomTypeChangedEvent evt) => HandleRoomTypeChanged((RunDirector.RoomType)evt.RoomType);
        private void HandleRoomClearedEvent(RoomClearedEvent evt) => HandleRoomCleared(evt.Room, evt.TotalRooms);
        private void HandleEnemiesRemainingChangedEvent(EnemiesRemainingChangedEvent evt) => HandleEnemiesRemainingChanged(evt.Remaining);
        private void HandleRunWonEvent(RunWonEvent evt) => HandleRunWon();

        private void HandleRewardSelectionStartedEvent(RewardSelectionStartedEvent evt)
        {
            if (terminalStatusLocked)
            {
                return;
            }

            SetText(statusText, "Reward Room (Timer Paused) - Choose 1");
            SetText(rewardText, $"- {evt.Option1}\n- {evt.Option2}\n- {evt.Option3}");
        }

        private void HandleRewardSelectedEvent(RewardSelectedEvent evt)
        {
            if (terminalStatusLocked)
            {
                return;
            }

            SetText(statusText, $"Reward: {evt.Label}");
            SetText(rewardText, string.Empty);
        }

        private void HandlePassiveSelectedEvent(PassiveSelectedEvent evt)
        {
            if (terminalStatusLocked)
            {
                return;
            }

            SetText(statusText, $"Passive: {evt.Label}");
            SetText(rewardText, string.Empty);
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
            MetaProgressionSnapshot snapshot = SixtyMetaProgression.GetSnapshot(deaths);
            string nextUnlock = GetNextUnlockLabel(snapshot);
            SetText(deathText, $"Deaths: {deaths} | Boss Clears: {snapshot.BossClears} | {nextUnlock}");
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

        private void HandleRoomTypeChanged(RunDirector.RoomType roomType)
        {
            if (terminalStatusLocked)
            {
                return;
            }

            string label = roomType switch
            {
                RunDirector.RoomType.Reward => "Reward Room (Timer Paused)",
                RunDirector.RoomType.Risk => "Risk Room",
                RunDirector.RoomType.Boss => "Boss Arena",
                _ => "Combat Room"
            };

            SetText(statusText, label);
            if (statusText != null)
            {
                statusText.color = roomType switch
                {
                    RunDirector.RoomType.Reward => rewardStatusColor,
                    RunDirector.RoomType.Risk => riskStatusColor,
                    RunDirector.RoomType.Boss => bossStatusColor,
                    _ => combatStatusColor
                };
            }
        }

        private void HandleRoomCleared(int room, int totalRooms)
        {
            if (terminalStatusLocked || room <= 0 || room >= totalRooms)
            {
                return;
            }

            SetText(statusText, "Room Clear - Move Through The Open Gate");
            if (statusText != null)
            {
                statusText.color = new Color(0.4f, 1f, 0.92f, 1f);
            }
        }

        private void HandleEnemiesRemainingChanged(int remaining)
        {
            SetText(enemiesText, $"Enemies: {Mathf.Max(remaining, 0)}");
        }

        private void HandleRunWon()
        {
            terminalStatusLocked = true;
            SetText(statusText, "Run Complete");
            SetText(rewardText, string.Empty);
        }

        private void HandleTimeOut()
        {
            terminalStatusLocked = true;
            SetText(statusText, "Time Out");
            SetText(rewardText, string.Empty);
        }

        private void UpdateWeaponLabel()
        {
            if (weaponText == null)
            {
                return;
            }

            string weaponName = weaponController != null ? weaponController.CurrentWeaponName : "--";
            string passiveName = passiveController != null ? passiveController.ActivePassiveLabel : "None";
            if (weaponController == null)
            {
                SetText(weaponText, $"Weapon: {weaponName} | Passive: {passiveName}");
                return;
            }

            SetText(
                weaponText,
                $"Weapon: {weaponName} | Passive: {passiveName}\n" +
                $"DMG {weaponController.EffectiveDamage:0.0} x{weaponController.EffectiveProjectileCount}  ROF {weaponController.EffectiveFireRate:0.0}/s  SPD {weaponController.EffectiveProjectileSpeed:0.0}");
        }

        private void UpdateTimeLabel(float seconds)
        {
            float roundedSeconds = Mathf.Max(0f, Mathf.Round(seconds * 10f) * 0.1f);
            if (!Mathf.Approximately(roundedSeconds, lastDisplayedTime))
            {
                lastDisplayedTime = roundedSeconds;
                SetText(timeText, $"Time: {roundedSeconds:0.0}s");
            }

            if (timeText == null)
            {
                return;
            }

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

        private void UpdateTimePulseVisuals(float deltaTime)
        {
            if (gainPulseTimer > 0f)
            {
                gainPulseTimer = Mathf.Max(0f, gainPulseTimer - deltaTime);
            }

            if (lossPulseTimer > 0f)
            {
                lossPulseTimer = Mathf.Max(0f, lossPulseTimer - deltaTime);
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

        private static string GetNextUnlockLabel(MetaProgressionSnapshot snapshot)
        {
            if (!snapshot.ShotgunUnlocked)
            {
                int needed = Mathf.Max(0, ShotgunUnlockDeaths - snapshot.DeathCount);
                return $"Shotgun in {needed}";
            }

            if (!snapshot.ChargeBeamUnlocked)
            {
                int needed = Mathf.Max(0, ChargeBeamUnlockDeaths - snapshot.DeathCount);
                return $"Charge Beam in {needed}";
            }

            if (!snapshot.StartingBonusUnlocked)
            {
                int needed = Mathf.Max(0, StartingBonusUnlockDeaths - snapshot.DeathCount);
                return $"+5s start in {needed}";
            }

            return "All meta unlocks active";
        }
    }
}
