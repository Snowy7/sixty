using System;
using UnityEngine;
using Ia.Core.Update;
using Ia.Core.Debugging;
using Ia.Core.Events;

namespace Ia.Gameplay.Characters
{
    public enum IaCharacterMode
    {
        Normal = 0,
        Aiming = 1,
        Interacting = 2,
        InMenu = 3
    }

    [DisallowMultipleComponent]
    public class IaCharacterState : IaBehaviour
    {
        [SerializeField] private IaCharacterMode mode = IaCharacterMode.Normal;

        public IaCharacterMode Mode => mode;

        public bool IsInNormalMode => mode == IaCharacterMode.Normal;
        public bool CanReceiveGameplayInput =>
            !IsInMenu && !IsInteracting; // nice to centralize this
        
        public bool IsInMenu => mode == IaCharacterMode.InMenu;
        public bool IsInteracting => mode == IaCharacterMode.Interacting;
        public bool IsAiming => mode == IaCharacterMode.Aiming;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }

        public event Action<IaCharacterMode, IaCharacterMode> ModeChanged;
        public event Action<bool> SprintChanged;
        public event Action<bool> CrouchChanged;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        public void SetMode(IaCharacterMode newMode)
        {
            if (mode == newMode)
                return;

            var old = mode;
            mode = newMode;
            ModeChanged?.Invoke(old, mode);

            IaLogger.Info(
                IaLogCategory.Gameplay,
                $"Character mode changed: {old} -> {mode}",
                this
            );
        }

        public void SetSprinting(bool sprinting)
        {
            if (IsSprinting == sprinting)
                return;
            
            // call the event only if there is a change
            if (sprinting)
            {
                IaEventBus.Publish(new OnPlayerStartedSprinting());
            }
            else
            {
                IaEventBus.Publish(new OnPlayerStoppedSprinting());
            }

            IsSprinting = sprinting;
            SprintChanged?.Invoke(IsSprinting);
        }

        public void SetCrouching(bool crouching)
        {
            if (IsCrouching == crouching)
                return;
            
            // call the event only if there is a change
            if (crouching)
            {
                IaEventBus.Publish(new OnPlayerCrouched());
            }
            else
            {
                IaEventBus.Publish(new OnPlayerUncrouched());
            }

            IsCrouching = crouching;
            CrouchChanged?.Invoke(IsCrouching);
        }

        // If you want to manage transitions in Update (e.g., timers), you can later.
        public override void OnIaUpdate(float deltaTime)
        {
            // For now, no automatic transitions here.
        }
    }
}