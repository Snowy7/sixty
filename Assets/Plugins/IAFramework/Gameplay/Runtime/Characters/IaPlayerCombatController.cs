using UnityEngine;
using Ia.Core.Update;
using Ia.Gameplay.Actors;
using Ia.Gameplay.Combat;

namespace Ia.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IaActor))]
    [RequireComponent(typeof(IaCharacterState))]
    public class IaPlayerCombatController : IaBehaviour
    {
        [Header("Weapon")]
        [SerializeField] IaWeaponBase equippedWeapon;

        [Header("References")]
        [SerializeField] IaPlayerInputDriver inputDriver;

        private IaActor m_actor;
        private IaCharacterState m_state;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void OnIaAwake()
        {
            m_actor = GetComponent<IaActor>();
            m_state = GetComponent<IaCharacterState>();

            if (inputDriver == null)
            {
                inputDriver = GetComponent<IaPlayerInputDriver>();
            }

            if (equippedWeapon != null && m_actor != null)
            {
                equippedWeapon.Initialize(m_actor);
            }
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (m_state != null && m_state.IsInMenu)
                return;

            if (equippedWeapon == null || m_actor == null)
                return;

            if (inputDriver == null)
                return;

            // Get combat input from the centralized input driver
            IaCombatInput combatInput = inputDriver.GetCombatInput();

            // Handle combat inputs
            if (combatInput.PrimaryFire)
            {
                equippedWeapon.PrimaryFire();
            }

            if (combatInput.SecondaryFire)
            {
                equippedWeapon.SecondaryFire();
            }
            
            if (combatInput.Reload && equippedWeapon is IIaReloadableWeapon reloadable) 
                reloadable.TryReload();
        }

        public void EquipWeapon(IaWeaponBase weapon)
        {
            equippedWeapon = weapon;

            if (equippedWeapon != null && m_actor != null)
            {
                equippedWeapon.Initialize(m_actor);
            }
        }
    }
}