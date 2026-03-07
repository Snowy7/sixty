using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private float notificationDuration = 2.5f;
        [SerializeField] private int maxBulletPips = 8;

        private TimeManager timeManager;
        private WeaponController weaponController;
        private RunPassiveController passiveController;
        private PlayerController playerController;

        // UI elements
        private Label timerValue;
        private Label timerWarning;
        private Label roomValue;
        private Label enemiesValue;
        private Label weaponName;
        private Label weaponRate;
        private VisualElement bulletPips;
        private Label passiveLabel;
        private Label passiveDesc;
        private VisualElement passiveCard;
        private Label reward1Text;
        private Label reward2Text;
        private Label reward3Text;
        private VisualElement rewardContainer;

        // Room segments
        private VisualElement roomSegments;
        private int totalRooms = 10;

        // Target health (boss)
        private VisualElement targetHealthPanel;
        private Label targetName;
        private Label targetHpValue;
        private VisualElement targetBarFill;

        // Notifications
        private VisualElement notificationContainer;
        private Label notificationText;
        private Label notificationSub;
        private Coroutine notifHideRoutine;

        private float gainPulseTimer;
        private float lossPulseTimer;
        private float lastDisplayedTime = float.NaN;
        private bool terminalStatusLocked;
        private Health trackedBossHealth;

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
            timerWarning = root.Q<Label>("timer-warning");
            roomValue = root.Q<Label>("room-value");
            enemiesValue = root.Q<Label>("enemies-value");
            weaponName = root.Q<Label>("weapon-name");
            weaponRate = root.Q<Label>("weapon-rate");
            bulletPips = root.Q<VisualElement>("bullet-pips");
            passiveLabel = root.Q<Label>("passive-label");
            passiveDesc = root.Q<Label>("passive-desc");
            passiveCard = root.Q<VisualElement>("passive-card");
            reward1Text = root.Q<Label>("reward-1-text");
            reward2Text = root.Q<Label>("reward-2-text");
            reward3Text = root.Q<Label>("reward-3-text");
            rewardContainer = root.Q<VisualElement>("reward-container");

            roomSegments = root.Q<VisualElement>("room-segments");

            targetHealthPanel = root.Q<VisualElement>("target-health-panel");
            targetName = root.Q<Label>("target-name");
            targetHpValue = root.Q<Label>("target-hp-value");
            targetBarFill = root.Q<VisualElement>("target-bar-fill");

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
            UpdatePassiveDisplay();
            UpdateBossHealthDisplay();
            UpdatePulseTimers(deltaTime);
        }

        private void RefreshAll()
        {
            if (timeManager != null)
            {
                UpdateTimerDisplay(timeManager.TimeRemaining);
            }
            else
            {
                UpdateTimerDisplay(0f);
            }

            if (runDirector != null)
            {
                UpdateRoomDisplay(runDirector.CurrentRoom, 10);
                UpdateEnemiesDisplay(runDirector.EnemiesAlive);
            }

            HideNotification();
            HideRewards();
            UpdateWeaponDisplay();
            UpdatePassiveDisplay();
        }

        // --- Event Handlers ---

        private void OnTimeChanged(TimeChangedEvent evt)
        {
            if (evt.Delta > 0f) gainPulseTimer = pulseDuration;
            else if (evt.Delta < 0f) lossPulseTimer = pulseDuration;
            UpdateTimerDisplay(evt.Remaining);
        }

        private void OnDeathCountChanged(DeathCountChangedEvent evt) { }

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

            // Track boss health when entering boss room
            if (rt == RunDirector.RoomType.Boss)
                FindBossHealth();
            else
                ClearBossHealth();
        }

        private void OnRoomCleared(RoomClearedEvent evt)
        {
            if (terminalStatusLocked || evt.Room <= 0 || evt.Room >= evt.TotalRooms) return;
            ShowNotification("ROOM CLEAR", "PROCEED TO EXIT", "notif-clear", notificationDuration);
            ClearBossHealth();
        }

        private void OnEnemiesRemaining(EnemiesRemainingChangedEvent evt) => UpdateEnemiesDisplay(evt.Remaining);

        private void OnRunWon(RunWonEvent evt)
        {
            terminalStatusLocked = true;
            ShowNotification("RUN COMPLETE", "CONGRATULATIONS", "notif-win", -1f);
            HideRewards();
            ClearBossHealth();
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

            bool critical = seconds <= lowTimeThreshold;
            if (critical)
                timerValue.AddToClassList("time-critical");

            if (timerWarning != null)
            {
                if (critical)
                {
                    timerWarning.text = $"UNDER {lowTimeThreshold:0}s";
                    timerWarning.AddToClassList("visible");
                }
                else
                {
                    timerWarning.RemoveFromClassList("visible");
                }
            }
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
                roomValue.text = room <= 0 ? "ROOM --/--" : $"ROOM {room}/{total}";

            if (roomSegments != null)
            {
                List<VisualElement> segs = roomSegments.Query(className: "seg").ToList();
                for (int i = 0; i < segs.Count; i++)
                {
                    segs[i].RemoveFromClassList("seg-empty");
                    segs[i].RemoveFromClassList("seg-filled");
                    segs[i].RemoveFromClassList("seg-current");

                    if (i < room - 1)
                        segs[i].AddToClassList("seg-filled");
                    else if (i == room - 1)
                        segs[i].AddToClassList("seg-current");
                    else
                        segs[i].AddToClassList("seg-empty");
                }
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
                float rof = weaponController.EffectiveFireRate;
                int count = weaponController.EffectiveProjectileCount;

                if (weaponRate != null)
                    weaponRate.text = $"{rof:0}/s";

                if (bulletPips != null)
                {
                    bulletPips.Clear();
                    int pipCount = Mathf.Clamp(count, 1, maxBulletPips);
                    for (int i = 0; i < pipCount; i++)
                    {
                        VisualElement pip = new VisualElement();
                        pip.AddToClassList("pip");
                        pip.AddToClassList("pip-filled");
                        bulletPips.Add(pip);
                    }
                }
            }
        }

        private void UpdatePassiveDisplay()
        {
            if (passiveCard == null) return;

            bool hasPassive = passiveController != null && passiveController.HasPassive;
            if (hasPassive)
            {
                passiveCard.AddToClassList("visible");
                if (passiveLabel != null)
                    passiveLabel.text = $"PASSIVE: {passiveController.ActivePassiveLabel.ToUpper()}";
                if (passiveDesc != null)
                    passiveDesc.text = RunPassiveController.GetPassiveDescription(passiveController.ActivePassive);
            }
            else
            {
                passiveCard.RemoveFromClassList("visible");
            }
        }

        private void FindBossHealth()
        {
            // Look for the highest-HP enemy in the scene as the boss
            Health[] allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
            Health best = null;
            float bestMax = 0f;
            foreach (Health h in allHealth)
            {
                if (h == null || h.IsDead) continue;
                if (h.CompareTag("Player")) continue;
                if (h.MaxHealth > bestMax)
                {
                    bestMax = h.MaxHealth;
                    best = h;
                }
            }
            trackedBossHealth = best;
        }

        private void ClearBossHealth()
        {
            trackedBossHealth = null;
            if (targetHealthPanel != null)
                targetHealthPanel.RemoveFromClassList("visible");
        }

        private void UpdateBossHealthDisplay()
        {
            if (targetHealthPanel == null) return;

            if (trackedBossHealth == null || trackedBossHealth.IsDead)
            {
                targetHealthPanel.RemoveFromClassList("visible");
                return;
            }

            targetHealthPanel.AddToClassList("visible");

            if (targetName != null)
                targetName.text = "BOSS";

            float hp = trackedBossHealth.CurrentHealth;
            float maxHp = trackedBossHealth.MaxHealth;

            if (targetHpValue != null)
                targetHpValue.text = $"{Mathf.CeilToInt(hp)}";

            if (targetBarFill != null && maxHp > 0f)
            {
                float pct = Mathf.Clamp01(hp / maxHp) * 100f;
                targetBarFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            }
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
    }
}
