using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Core;
using UnityEngine;

namespace Sixty.Player
{
    public enum RunPassiveType
    {
        None = 0,
        Adrenaline = 1,
        Overclock = 2,
        SecondWind = 3
    }

    public class RunPassiveController : IaBehaviour
    {
        [SerializeField] private float adrenalineThresholdSeconds = 10f;
        [SerializeField] private float adrenalineMoveSpeedMultiplier = 1.2f;
        [SerializeField] private float overclockDamageMultiplier = 1.3f;

        private PlayerController playerController;
        private WeaponController weaponController;
        private TimeManager timeManager;

        public RunPassiveType ActivePassive { get; private set; } = RunPassiveType.None;
        public bool HasPassive => ActivePassive != RunPassiveType.None;
        public string ActivePassiveLabel => GetPassiveLabel(ActivePassive);

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaEnable()
        {
            ResolveReferences();
            IaEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilledEvent);
        }

        protected override void OnIaDisable()
        {
            IaEventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilledEvent);
            if (playerController != null)
            {
                playerController.SetExternalMoveSpeedMultiplier(1f);
            }
        }

        public override void OnIaUpdate(float deltaTime)
        {
            ResolveReferences();

            if (ActivePassive != RunPassiveType.Adrenaline || playerController == null)
            {
                return;
            }

            bool boosted = timeManager != null && timeManager.TimeRemaining <= adrenalineThresholdSeconds;
            playerController.SetExternalMoveSpeedMultiplier(boosted ? adrenalineMoveSpeedMultiplier : 1f);
        }

        public bool TryApplyPassive(RunPassiveType passiveType)
        {
            if (passiveType == RunPassiveType.None || HasPassive)
            {
                return false;
            }

            ResolveReferences();
            ActivePassive = passiveType;

            if (passiveType == RunPassiveType.Overclock && weaponController != null)
            {
                weaponController.ApplyDamageMultiplier(overclockDamageMultiplier);
            }

            IaEventBus.Publish(new PassiveSelectedEvent((int)ActivePassive, ActivePassiveLabel));
            return true;
        }

        public static string GetPassiveLabel(RunPassiveType passiveType)
        {
            return passiveType switch
            {
                RunPassiveType.Adrenaline => "Adrenaline",
                RunPassiveType.Overclock => "Overclock",
                RunPassiveType.SecondWind => "Second Wind",
                _ => "None"
            };
        }

        private void ResolveReferences()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
            }

            if (weaponController == null)
            {
                weaponController = GetComponentInChildren<WeaponController>();
            }

            if (timeManager == null)
            {
                timeManager = TimeManager.Instance;
            }
        }

        private void OnEnemyKilledEvent(EnemyKilledEvent evt)
        {
            if (ActivePassive == RunPassiveType.SecondWind && playerController != null)
            {
                playerController.RefreshDashCooldown();
            }
        }
    }
}
