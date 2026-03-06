using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Core;
using Sixty.Gameplay;
using Sixty.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sixty.UI
{
    public class GameHudController : IaBehaviour
    {
        private const int ShotgunUnlockDeaths = 5;
        private const int ChargeBeamUnlockDeaths = 10;
        private const int StartingBonusUnlockDeaths = 25;

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private RunDirector runDirector;
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float pulseDuration = 0.2f;

        private TimeManager timeManager;
        private WeaponController weaponController;
        private RunPassiveController passiveController;

        // UI elements
        private Label timerValue;
        private Label roomValue;
        private Label enemiesValue;
        private Label statusText;
        private Label weaponName;
        private Label weaponStats;
        private Label passiveLabel;
        private Label metaText;
        private Label reward1Text;
        private Label reward2Text;
        private Label reward3Text;
        private VisualElement rewardContainer;

        private float gainPulseTimer;
        private float lossPulseTimer;
        private float lastDisplayedTime = float.NaN;
        private bool terminalStatusLocked;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.UI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaEnable()
        {
            if (runDirector == null)
                runDirector = FindFirstObjectByType<RunDirector>();

            timeManager = TimeManager.Instance;
            weaponController = FindFirstObjectByType<WeaponController>();
            passiveController = FindFirstObjectByType<RunPassiveController>();

            BindElements();

            terminalStatusLocked = false;
            IaEventBus.Subscribe<TimeChangedEvent>(OnTimeChanged);
            IaEventBus.Subscribe<DeathCountChangedEvent>(OnDeathCountChanged);
            IaEventBus.Subscribe<TimeOutEvent>(OnTimeOut);
            IaEventBus.Subscribe<RoomChangedEvent>(OnRoomChanged);
            IaEventBus.Subscribe<RoomTypeChangedEvent>(OnRoomTypeChanged);
            IaEventBus.Subscribe<RoomClearedEvent>(OnRoomCleared);
            IaEventBus.Subscribe<EnemiesRemainingChangedEvent>(OnEnemiesRemaining);
            IaEventBus.Subscribe<RunWonEvent>(OnRunWon);
            IaEventBus.Subscribe<RewardSelectionStartedEvent>(OnRewardStarted);
            IaEventBus.Subscribe<RewardSelectedEvent>(OnRewardSelected);
            IaEventBus.Subscribe<PassiveSelectedEvent>(OnPassiveSelected);

            RefreshAll();
        }

        protected override void OnIaDisable()
        {
            IaEventBus.Unsubscribe<TimeChangedEvent>(OnTimeChanged);
            IaEventBus.Unsubscribe<DeathCountChangedEvent>(OnDeathCountChanged);
            IaEventBus.Unsubscribe<TimeOutEvent>(OnTimeOut);
            IaEventBus.Unsubscribe<RoomChangedEvent>(OnRoomChanged);
            IaEventBus.Unsubscribe<RoomTypeChangedEvent>(OnRoomTypeChanged);
            IaEventBus.Unsubscribe<RoomClearedEvent>(OnRoomCleared);
            IaEventBus.Unsubscribe<EnemiesRemainingChangedEvent>(OnEnemiesRemaining);
            IaEventBus.Unsubscribe<RunWonEvent>(OnRunWon);
            IaEventBus.Unsubscribe<RewardSelectionStartedEvent>(OnRewardStarted);
            IaEventBus.Unsubscribe<RewardSelectedEvent>(OnRewardSelected);
            IaEventBus.Unsubscribe<PassiveSelectedEvent>(OnPassiveSelected);
        }

        private void BindElements()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return;

            VisualElement root = uiDocument.rootVisualElement;
            timerValue = root.Q<Label>("timer-value");
            roomValue = root.Q<Label>("room-value");
            enemiesValue = root.Q<Label>("enemies-value");
            statusText = root.Q<Label>("status-text");
            weaponName = root.Q<Label>("weapon-name");
            weaponStats = root.Q<Label>("weapon-stats");
            passiveLabel = root.Q<Label>("passive-label");
            metaText = root.Q<Label>("meta-text");
            reward1Text = root.Q<Label>("reward-1-text");
            reward2Text = root.Q<Label>("reward-2-text");
            reward3Text = root.Q<Label>("reward-3-text");
            rewardContainer = root.Q<VisualElement>("reward-container");
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (timeManager == null) timeManager = TimeManager.Instance;
            if (weaponController == null) weaponController = FindFirstObjectByType<WeaponController>();
            if (passiveController == null) passiveController = FindFirstObjectByType<RunPassiveController>();

            UpdateWeaponDisplay();
            UpdatePulseTimers(deltaTime);
        }

        private void RefreshAll()
        {
            if (timeManager != null)
            {
                UpdateTimerDisplay(timeManager.TimeRemaining);
                UpdateMetaDisplay(timeManager.DeathCount);
            }
            else
            {
                UpdateTimerDisplay(0f);
                UpdateMetaDisplay(0);
            }

            if (runDirector != null)
            {
                UpdateRoomDisplay(runDirector.CurrentRoom, 10);
                UpdateEnemiesDisplay(runDirector.EnemiesAlive);
            }

            SetStatusText("COMBAT", "status-combat");
            HideRewards();
            UpdateWeaponDisplay();
        }

        // --- Event Handlers ---

        private void OnTimeChanged(TimeChangedEvent evt)
        {
            if (evt.Delta > 0f) gainPulseTimer = pulseDuration;
            else if (evt.Delta < 0f) lossPulseTimer = pulseDuration;
            UpdateTimerDisplay(evt.Remaining);
        }

        private void OnDeathCountChanged(DeathCountChangedEvent evt) => UpdateMetaDisplay(evt.DeathCount);

        private void OnTimeOut(TimeOutEvent evt)
        {
            terminalStatusLocked = true;
            SetStatusText("TIME OUT", "status-boss");
            HideRewards();
        }

        private void OnRoomChanged(RoomChangedEvent evt) => UpdateRoomDisplay(evt.Room, evt.TotalRooms);

        private void OnRoomTypeChanged(RoomTypeChangedEvent evt)
        {
            if (terminalStatusLocked) return;

            RunDirector.RoomType rt = (RunDirector.RoomType)evt.RoomType;
            string label = rt switch
            {
                RunDirector.RoomType.Reward => "REWARD ROOM",
                RunDirector.RoomType.Risk => "RISK ROOM",
                RunDirector.RoomType.Boss => "BOSS ARENA",
                _ => "COMBAT"
            };
            string cls = rt switch
            {
                RunDirector.RoomType.Reward => "status-reward",
                RunDirector.RoomType.Risk => "status-risk",
                RunDirector.RoomType.Boss => "status-boss",
                _ => "status-combat"
            };
            SetStatusText(label, cls);
        }

        private void OnRoomCleared(RoomClearedEvent evt)
        {
            if (terminalStatusLocked || evt.Room <= 0 || evt.Room >= evt.TotalRooms) return;
            SetStatusText("ROOM CLEAR", "status-clear");
        }

        private void OnEnemiesRemaining(EnemiesRemainingChangedEvent evt) => UpdateEnemiesDisplay(evt.Remaining);

        private void OnRunWon(RunWonEvent evt)
        {
            terminalStatusLocked = true;
            SetStatusText("RUN COMPLETE", "status-clear");
            HideRewards();
        }

        private void OnRewardStarted(RewardSelectionStartedEvent evt)
        {
            if (terminalStatusLocked) return;
            SetStatusText("CHOOSE REWARD", "status-reward");
            ShowRewards(evt.Option1, evt.Option2, evt.Option3);
        }

        private void OnRewardSelected(RewardSelectedEvent evt)
        {
            if (terminalStatusLocked) return;
            SetStatusText($"SELECTED: {evt.Label.ToUpper()}", "status-reward");
            HideRewards();
        }

        private void OnPassiveSelected(PassiveSelectedEvent evt)
        {
            if (terminalStatusLocked) return;
            SetStatusText($"PASSIVE: {evt.Label.ToUpper()}", "status-reward");
            HideRewards();
        }

        // --- Display Updates ---

        private void UpdateTimerDisplay(float seconds)
        {
            float rounded = Mathf.Max(0f, Mathf.Round(seconds * 10f) * 0.1f);
            if (Mathf.Approximately(rounded, lastDisplayedTime)) return;
            lastDisplayedTime = rounded;

            if (timerValue == null) return;
            timerValue.text = $"{rounded:0.0}";

            // Clear all pulse classes first
            timerValue.RemoveFromClassList("pulse-gain");
            timerValue.RemoveFromClassList("pulse-loss");
            timerValue.RemoveFromClassList("time-critical");

            if (seconds <= lowTimeThreshold)
                timerValue.AddToClassList("time-critical");
        }

        private void UpdatePulseTimers(float dt)
        {
            if (timerValue == null) return;

            if (gainPulseTimer > 0f)
            {
                gainPulseTimer = Mathf.Max(0f, gainPulseTimer - dt);
                if (gainPulseTimer > 0f)
                    timerValue.AddToClassList("pulse-gain");
                else
                    timerValue.RemoveFromClassList("pulse-gain");
            }

            if (lossPulseTimer > 0f)
            {
                lossPulseTimer = Mathf.Max(0f, lossPulseTimer - dt);
                if (lossPulseTimer > 0f)
                    timerValue.AddToClassList("pulse-loss");
                else
                    timerValue.RemoveFromClassList("pulse-loss");
            }
        }

        private void UpdateRoomDisplay(int room, int total)
        {
            if (roomValue == null) return;
            roomValue.text = room <= 0 ? "-- / --" : $"{room} / {total}";
        }

        private void UpdateEnemiesDisplay(int count)
        {
            if (enemiesValue == null) return;
            enemiesValue.text = Mathf.Max(count, 0).ToString();
        }

        private void UpdateWeaponDisplay()
        {
            if (weaponName == null) return;

            string wName = weaponController != null ? weaponController.CurrentWeaponName : "--";
            weaponName.text = wName;

            if (weaponStats != null && weaponController != null)
            {
                weaponStats.text = $"DMG {weaponController.EffectiveDamage:0.0} x{weaponController.EffectiveProjectileCount}  " +
                                   $"ROF {weaponController.EffectiveFireRate:0.0}/s  " +
                                   $"SPD {weaponController.EffectiveProjectileSpeed:0.0}";
            }

            if (passiveLabel != null)
            {
                string pName = passiveController != null ? passiveController.ActivePassiveLabel : "None";
                passiveLabel.text = $"PASSIVE: {pName.ToUpper()}";
            }
        }

        private void UpdateMetaDisplay(int deaths)
        {
            if (metaText == null) return;
            MetaProgressionSnapshot snapshot = SixtyMetaProgression.GetSnapshot(deaths);
            string nextUnlock = GetNextUnlockLabel(snapshot);
            metaText.text = $"DEATHS: {deaths}  |  BOSS CLEARS: {snapshot.BossClears}  |  {nextUnlock.ToUpper()}";
        }

        private void SetStatusText(string text, string styleClass)
        {
            if (statusText == null) return;
            statusText.text = text;

            statusText.RemoveFromClassList("status-combat");
            statusText.RemoveFromClassList("status-reward");
            statusText.RemoveFromClassList("status-risk");
            statusText.RemoveFromClassList("status-boss");
            statusText.RemoveFromClassList("status-clear");
            statusText.AddToClassList(styleClass);
        }

        private void ShowRewards(string opt1, string opt2, string opt3)
        {
            if (rewardContainer == null) return;
            rewardContainer.AddToClassList("visible");
            if (reward1Text != null) reward1Text.text = opt1;
            if (reward2Text != null) reward2Text.text = opt2;
            if (reward3Text != null) reward3Text.text = opt3;
        }

        private void HideRewards()
        {
            if (rewardContainer == null) return;
            rewardContainer.RemoveFromClassList("visible");
        }

        private static string GetNextUnlockLabel(MetaProgressionSnapshot snapshot)
        {
            if (!snapshot.ShotgunUnlocked)
                return $"Shotgun in {Mathf.Max(0, ShotgunUnlockDeaths - snapshot.DeathCount)}";
            if (!snapshot.ChargeBeamUnlocked)
                return $"Charge Beam in {Mathf.Max(0, ChargeBeamUnlockDeaths - snapshot.DeathCount)}";
            if (!snapshot.StartingBonusUnlocked)
                return $"+5s start in {Mathf.Max(0, StartingBonusUnlockDeaths - snapshot.DeathCount)}";
            return "All unlocks active";
        }
    }
}
