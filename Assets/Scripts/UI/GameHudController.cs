using System.Collections;
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

        private const float MaxDmgDisplay = 40f;
        private const float MaxRofDisplay = 20f;
        private const float MaxSpdDisplay = 80f;

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private RunDirector runDirector;
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float pulseDuration = 0.2f;
        [SerializeField] private float notificationDuration = 2.5f;

        private TimeManager timeManager;
        private WeaponController weaponController;
        private RunPassiveController passiveController;
        private PlayerController playerController;

        // UI elements
        private Label timerValue;
        private Label roomValue;
        private Label enemiesValue;
        private Label weaponName;
        private Label projectileCount;
        private Label passiveLabel;
        private Label metaText;
        private Label reward1Text;
        private Label reward2Text;
        private Label reward3Text;
        private VisualElement rewardContainer;

        // Weapon stat bars
        private VisualElement statBarDmg;
        private VisualElement statBarRof;
        private VisualElement statBarSpd;
        private Label statValDmg;
        private Label statValRof;
        private Label statValSpd;

        // Room progress
        private VisualElement roomProgressFill;
        private int totalRooms = 10;

        // Dash
        private VisualElement dashBarFill;
        private Label dashStatus;

        // Notifications
        private VisualElement notificationContainer;
        private Label notificationText;
        private Label notificationSub;
        private Coroutine notifHideRoutine;

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
            playerController = FindFirstObjectByType<PlayerController>();

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
            weaponName = root.Q<Label>("weapon-name");
            projectileCount = root.Q<Label>("projectile-count");
            passiveLabel = root.Q<Label>("passive-label");
            metaText = root.Q<Label>("meta-text");
            reward1Text = root.Q<Label>("reward-1-text");
            reward2Text = root.Q<Label>("reward-2-text");
            reward3Text = root.Q<Label>("reward-3-text");
            rewardContainer = root.Q<VisualElement>("reward-container");

            statBarDmg = root.Q<VisualElement>("stat-bar-dmg");
            statBarRof = root.Q<VisualElement>("stat-bar-rof");
            statBarSpd = root.Q<VisualElement>("stat-bar-spd");
            statValDmg = root.Q<Label>("stat-val-dmg");
            statValRof = root.Q<Label>("stat-val-rof");
            statValSpd = root.Q<Label>("stat-val-spd");

            roomProgressFill = root.Q<VisualElement>("room-progress-fill");

            dashBarFill = root.Q<VisualElement>("dash-bar-fill");
            dashStatus = root.Q<Label>("dash-status");

            notificationContainer = root.Q<VisualElement>("notification-container");
            notificationText = root.Q<Label>("notification-text");
            notificationSub = root.Q<Label>("notification-sub");
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (timeManager == null) timeManager = TimeManager.Instance;
            if (weaponController == null) weaponController = FindFirstObjectByType<WeaponController>();
            if (passiveController == null) passiveController = FindFirstObjectByType<RunPassiveController>();
            if (playerController == null) playerController = FindFirstObjectByType<PlayerController>();

            UpdateWeaponDisplay();
            UpdateDashDisplay();
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

            HideNotification();
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
            ShowNotification("TIME OUT", "", "notif-timeout", -1f);
            HideRewards();
        }

        private void OnRoomChanged(RoomChangedEvent evt)
        {
            totalRooms = evt.TotalRooms;
            UpdateRoomDisplay(evt.Room, evt.TotalRooms);
        }

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
            string sub = rt switch
            {
                RunDirector.RoomType.Reward => "TIMER PAUSED",
                RunDirector.RoomType.Risk => "EXTRA ENEMIES + CLOCK PICKUPS",
                RunDirector.RoomType.Boss => "DEFEAT THE BOSS",
                _ => $"ROOM {evt.Room}"
            };
            string cls = rt switch
            {
                RunDirector.RoomType.Reward => "notif-reward",
                RunDirector.RoomType.Risk => "notif-risk",
                RunDirector.RoomType.Boss => "notif-boss",
                _ => "notif-combat"
            };
            ShowNotification(label, sub, cls, notificationDuration);
        }

        private void OnRoomCleared(RoomClearedEvent evt)
        {
            if (terminalStatusLocked || evt.Room <= 0 || evt.Room >= evt.TotalRooms) return;
            ShowNotification("ROOM CLEAR", "PROCEED TO EXIT", "notif-clear", notificationDuration);
        }

        private void OnEnemiesRemaining(EnemiesRemainingChangedEvent evt) => UpdateEnemiesDisplay(evt.Remaining);

        private void OnRunWon(RunWonEvent evt)
        {
            terminalStatusLocked = true;
            ShowNotification("RUN COMPLETE", "CONGRATULATIONS", "notif-win", -1f);
            HideRewards();
        }

        private void OnRewardStarted(RewardSelectionStartedEvent evt)
        {
            if (terminalStatusLocked) return;
            ShowNotification("CHOOSE REWARD", "", "notif-reward", -1f);
            ShowRewards(evt.Option1, evt.Option2, evt.Option3);
        }

        private void OnRewardSelected(RewardSelectedEvent evt)
        {
            if (terminalStatusLocked) return;
            ShowNotification($"EQUIPPED: {evt.Label.ToUpper()}", "", "notif-reward", notificationDuration);
            HideRewards();
        }

        private void OnPassiveSelected(PassiveSelectedEvent evt)
        {
            if (terminalStatusLocked) return;
            ShowNotification($"PASSIVE: {evt.Label.ToUpper()}", "", "notif-reward", notificationDuration);
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
            if (roomValue != null)
                roomValue.text = room <= 0 ? "-- / --" : $"{room} / {total}";

            if (roomProgressFill != null && total > 0)
            {
                float pct = Mathf.Clamp01((float)room / total) * 100f;
                roomProgressFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            }
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
            weaponName.text = wName.ToUpper();

            if (weaponController != null)
            {
                float dmg = weaponController.EffectiveDamage;
                float rof = weaponController.EffectiveFireRate;
                float spd = weaponController.EffectiveProjectileSpeed;
                int count = weaponController.EffectiveProjectileCount;

                SetStatBar(statBarDmg, statValDmg, dmg, MaxDmgDisplay);
                SetStatBar(statBarRof, statValRof, rof, MaxRofDisplay);
                SetStatBar(statBarSpd, statValSpd, spd, MaxSpdDisplay);

                if (projectileCount != null)
                    projectileCount.text = count > 1 ? $"x{count}" : "x1";
            }

            if (passiveLabel != null)
            {
                string pName = passiveController != null && passiveController.HasPassive
                    ? passiveController.ActivePassiveLabel.ToUpper()
                    : "NO PASSIVE";
                passiveLabel.text = pName;
            }
        }

        private void SetStatBar(VisualElement bar, Label val, float current, float max)
        {
            if (bar != null)
            {
                float pct = Mathf.Clamp01(current / max) * 100f;
                bar.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            }

            if (val != null)
                val.text = $"{current:0.0}";
        }

        private void UpdateDashDisplay()
        {
            if (playerController == null || dashBarFill == null) return;

            // Read dash cooldown state from player via reflection-free approach
            // PlayerController exposes IsInvulnerable but not dash cooldown directly
            // We check if dash is available by observing the player state
            bool dashReady = !playerController.IsInvulnerable;

            if (dashReady)
            {
                dashBarFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
                dashBarFill.RemoveFromClassList("on-cooldown");
                if (dashStatus != null)
                {
                    dashStatus.text = "READY";
                    dashStatus.RemoveFromClassList("on-cooldown");
                }
            }
            else
            {
                dashBarFill.style.width = new StyleLength(new Length(30f, LengthUnit.Percent));
                dashBarFill.AddToClassList("on-cooldown");
                if (dashStatus != null)
                {
                    dashStatus.text = "COOLDOWN";
                    dashStatus.AddToClassList("on-cooldown");
                }
            }
        }

        private void UpdateMetaDisplay(int deaths)
        {
            if (metaText == null) return;
            MetaProgressionSnapshot snapshot = SixtyMetaProgression.GetSnapshot(deaths);
            string nextUnlock = GetNextUnlockLabel(snapshot);
            metaText.text = $"DEATHS: {deaths}  |  BOSS CLEARS: {snapshot.BossClears}  |  {nextUnlock.ToUpper()}";
        }

        // --- Notifications ---

        private static readonly string[] NotifClasses =
        {
            "notif-combat", "notif-reward", "notif-risk", "notif-boss",
            "notif-clear", "notif-timeout", "notif-win"
        };

        private void ShowNotification(string text, string sub, string styleClass, float duration)
        {
            if (notificationContainer == null) return;

            if (notifHideRoutine != null)
            {
                StopCoroutine(notifHideRoutine);
                notifHideRoutine = null;
            }

            foreach (string cls in NotifClasses)
                notificationContainer.RemoveFromClassList(cls);

            notificationContainer.AddToClassList(styleClass);
            notificationContainer.AddToClassList("visible");

            if (notificationText != null) notificationText.text = text;
            if (notificationSub != null) notificationSub.text = sub;

            if (duration > 0f)
                notifHideRoutine = StartCoroutine(AutoHideNotification(duration));
        }

        private IEnumerator AutoHideNotification(float delay)
        {
            yield return new WaitForSeconds(delay);
            HideNotification();
            notifHideRoutine = null;
        }

        private void HideNotification()
        {
            if (notificationContainer == null) return;
            notificationContainer.RemoveFromClassList("visible");
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
