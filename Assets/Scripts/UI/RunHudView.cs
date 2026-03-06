using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Core;
using Sixty.Gameplay;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sixty.UI
{
    public class RunHudView : IaBehaviour
    {
        [SerializeField] private RunDirector runDirector;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text deathText;
        [SerializeField] private TMP_Text roomText;
        [SerializeField] private TMP_Text enemiesText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text weaponText;
        [SerializeField] private TMP_Text rewardText;
        [Header("Reward Choices")]
        [SerializeField] private WeaponDefinition shotgunWeapon;
        [SerializeField] private WeaponDefinition chargeBeamWeapon;
        [SerializeField] private float fireRateRewardMultiplier = 1.15f;
        [SerializeField] private float damageRewardMultiplier = 1.2f;
        [SerializeField] private float projectileSpeedRewardMultiplier = 1.16f;
        [SerializeField] private Color normalTimeColor = Color.white;
        [SerializeField] private Color lowTimeColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color timeGainColor = new Color(0.45f, 1f, 0.6f, 1f);
        [SerializeField] private Color timeLossColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float pulseDuration = 0.2f;
        [SerializeField] private float pulseScaleMultiplier = 1.12f;

        private TimeManager timeManager;
        private float gainPulseTimer;
        private float lossPulseTimer;
        private Vector3 timeLabelBaseScale = Vector3.one;
        private float lastDisplayedTime = float.NaN;
        private bool terminalStatusLocked;
        private WeaponController weaponController;
        private bool rewardSelectionActive;
        private readonly string[] rewardLabels = new string[3];
        private readonly Action[] rewardActions = new Action[3];
        private float rewardInputUnlockAt;

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

            if (timeText != null)
            {
                timeLabelBaseScale = timeText.rectTransform.localScale;
            }

            terminalStatusLocked = false;
            IaEventBus.Subscribe<TimeChangedEvent>(HandleTimeChangedEvent);
            IaEventBus.Subscribe<DeathCountChangedEvent>(HandleDeathCountChangedEvent);
            IaEventBus.Subscribe<TimeOutEvent>(HandleTimeOutEvent);
            IaEventBus.Subscribe<RoomChangedEvent>(HandleRoomChangedEvent);
            IaEventBus.Subscribe<RoomClearedEvent>(HandleRoomClearedEvent);
            IaEventBus.Subscribe<RoomTypeChangedEvent>(HandleRoomTypeChangedEvent);
            IaEventBus.Subscribe<EnemiesRemainingChangedEvent>(HandleEnemiesRemainingChangedEvent);
            IaEventBus.Subscribe<RunWonEvent>(HandleRunWonEvent);

            RefreshAllText();
        }

        protected override void OnIaDisable()
        {
            IaEventBus.Unsubscribe<TimeChangedEvent>(HandleTimeChangedEvent);
            IaEventBus.Unsubscribe<DeathCountChangedEvent>(HandleDeathCountChangedEvent);
            IaEventBus.Unsubscribe<TimeOutEvent>(HandleTimeOutEvent);
            IaEventBus.Unsubscribe<RoomChangedEvent>(HandleRoomChangedEvent);
            IaEventBus.Unsubscribe<RoomClearedEvent>(HandleRoomClearedEvent);
            IaEventBus.Unsubscribe<RoomTypeChangedEvent>(HandleRoomTypeChangedEvent);
            IaEventBus.Unsubscribe<EnemiesRemainingChangedEvent>(HandleEnemiesRemainingChangedEvent);
            IaEventBus.Unsubscribe<RunWonEvent>(HandleRunWonEvent);
            if (rewardSelectionActive)
            {
                rewardSelectionActive = false;
                Time.timeScale = 1f;
            }
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

            UpdateWeaponLabel();
            HandleRewardSelectionInput();
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

        private void HandleTimeChangedEvent(TimeChangedEvent evt)
        {
            HandleTimeChanged(evt.Remaining, evt.Delta);
        }

        private void HandleDeathCountChangedEvent(DeathCountChangedEvent evt)
        {
            HandleDeathCountChanged(evt.DeathCount);
        }

        private void HandleTimeOutEvent(TimeOutEvent evt)
        {
            HandleTimeOut();
        }

        private void HandleRoomChangedEvent(RoomChangedEvent evt)
        {
            HandleRoomChanged(evt.Room, evt.TotalRooms);
        }

        private void HandleRoomClearedEvent(RoomClearedEvent evt)
        {
            HandleRoomCleared(evt.Room, evt.TotalRooms);
        }

        private void HandleEnemiesRemainingChangedEvent(EnemiesRemainingChangedEvent evt)
        {
            HandleEnemiesRemainingChanged(evt.Remaining);
        }

        private void HandleRoomTypeChangedEvent(RoomTypeChangedEvent evt)
        {
            HandleRoomTypeChanged((RunDirector.RoomType)evt.RoomType);
        }

        private void HandleRunWonEvent(RunWonEvent evt)
        {
            HandleRunWon();
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
            terminalStatusLocked = true;
            CloseRewardSelection(true);
            SetText(statusText, "Run Complete");
        }

        private void HandleTimeOut()
        {
            terminalStatusLocked = true;
            CloseRewardSelection(true);
            SetText(statusText, "Time Out");
        }

        private void HandleRoomTypeChanged(RunDirector.RoomType roomType)
        {
            if (terminalStatusLocked || rewardSelectionActive)
            {
                return;
            }

            string label = roomType switch
            {
                RunDirector.RoomType.Reward => "Reward Room",
                RunDirector.RoomType.Risk => "Risk Room",
                RunDirector.RoomType.Boss => "Boss Arena",
                _ => "Combat Room"
            };

            SetText(statusText, label);
        }

        private void HandleRoomCleared(int room, int totalRooms)
        {
            if (room <= 0 || room >= totalRooms || terminalStatusLocked)
            {
                return;
            }

            OpenRewardSelection(room, totalRooms);
        }

        private void OpenRewardSelection(int room, int totalRooms)
        {
            if (rewardSelectionActive)
            {
                return;
            }

            if (weaponController == null)
            {
                weaponController = FindFirstObjectByType<WeaponController>();
            }

            if (weaponController == null)
            {
                return;
            }

            BuildRewardChoices();
            rewardSelectionActive = true;
            rewardInputUnlockAt = Time.unscaledTime + 0.08f;
            Time.timeScale = 0f;

            SetText(statusText, $"Room Clear {room}/{totalRooms} - Choose Reward");
            SetText(
                rewardText,
                $"[1] {rewardLabels[0]}\n[2] {rewardLabels[1]}\n[3] {rewardLabels[2]}");
        }

        private void BuildRewardChoices()
        {
            rewardLabels[0] = "+15% Fire Rate";
            rewardActions[0] = () => weaponController.ApplyFireRateMultiplier(fireRateRewardMultiplier);

            rewardLabels[1] = "+20% Damage";
            rewardActions[1] = () => weaponController.ApplyDamageMultiplier(damageRewardMultiplier);

            WeaponDefinition current = weaponController.CurrentWeapon;
            if (shotgunWeapon != null && current != shotgunWeapon)
            {
                rewardLabels[2] = "Equip Shotgun";
                rewardActions[2] = () => weaponController.SetWeapon(shotgunWeapon);
                return;
            }

            if (chargeBeamWeapon != null && current != chargeBeamWeapon)
            {
                rewardLabels[2] = "Equip Charge Beam";
                rewardActions[2] = () => weaponController.SetWeapon(chargeBeamWeapon);
                return;
            }

            rewardLabels[2] = "+16% Projectile Speed";
            rewardActions[2] = () => weaponController.ApplyProjectileSpeedMultiplier(projectileSpeedRewardMultiplier);
        }

        private void HandleRewardSelectionInput()
        {
            if (!rewardSelectionActive || Time.unscaledTime < rewardInputUnlockAt)
            {
                return;
            }

            int selectedIndex = -1;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    selectedIndex = 0;
                }
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    selectedIndex = 1;
                }
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                {
                    selectedIndex = 2;
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (selectedIndex < 0 && gamepad != null)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame)
                {
                    selectedIndex = 0;
                }
                else if (gamepad.buttonEast.wasPressedThisFrame)
                {
                    selectedIndex = 1;
                }
                else if (gamepad.buttonWest.wasPressedThisFrame)
                {
                    selectedIndex = 2;
                }
            }

            if (selectedIndex < 0)
            {
                return;
            }

            SelectReward(selectedIndex);
        }

        private void SelectReward(int index)
        {
            if (index < 0 || index >= rewardActions.Length || rewardActions[index] == null)
            {
                return;
            }

            string label = rewardLabels[index];
            rewardActions[index].Invoke();
            CloseRewardSelection(true);
            SetText(statusText, $"Reward: {label}");
            UpdateWeaponLabel();
        }

        private void CloseRewardSelection(bool resumeTime)
        {
            if (!rewardSelectionActive)
            {
                return;
            }

            rewardSelectionActive = false;
            SetText(rewardText, string.Empty);
            if (resumeTime)
            {
                Time.timeScale = 1f;
            }
        }

        private void UpdateWeaponLabel()
        {
            if (weaponText == null)
            {
                return;
            }

            string weaponName = weaponController != null ? weaponController.CurrentWeaponName : "--";
            SetText(weaponText, $"Weapon: {weaponName}");
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
    }
}
