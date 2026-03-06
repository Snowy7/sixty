using Ia.Core.Events;
using Ia.Core.Motion;
using Ia.Core.Update;
using UnityEngine;
using UnityEngine.Events;

namespace Ia.Gameplay.Combat
{
    public sealed class IaWeaponReloadMotionLayer : IaMotionLayer
    {
        [SerializeField] float reloadSeconds = 1.25f;
        [SerializeField] Vector3 axis = new(0f, 0f, 1f);
        [SerializeField] float oneRevolutionSeconds = 0.5f;
        [SerializeField] UnityEvent onReloadStart;
        [SerializeField] UnityEvent onReloadEnd;
        
        [SerializeField] string reloadAnimationName = "Reload";
        [SerializeField] string idleAnimationName = "Idle";
        [SerializeField] Animator animator;
        [SerializeField] float crossFadeDuration = 0.1f;

        bool m_reloading;
        float m_t;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;

        protected override void OnIaEnable()
        {
            Ia.Core.Events.IaEventBus.Subscribe<WeaponReloadStartedEvent>(OnReloadStart);
            Ia.Core.Events.IaEventBus.Subscribe<WeaponReloadFinishedEvent>(OnReloadEnd);
        }

        protected override void OnIaDisable()
        {
            Ia.Core.Events.IaEventBus.Unsubscribe<WeaponReloadStartedEvent>(OnReloadStart);
            Ia.Core.Events.IaEventBus.Unsubscribe<WeaponReloadFinishedEvent>(OnReloadEnd);
        }

        void OnReloadStart(WeaponReloadStartedEvent evt)
        {
            m_reloading = true;
            m_t = 0f;
            onReloadStart?.Invoke();
            CrossFadeToAnimation(reloadAnimationName);
        }

        void OnReloadEnd(WeaponReloadFinishedEvent evt)
        {
            m_reloading = false;
            m_t = 0f;
            onReloadEnd?.Invoke();
            CrossFadeToAnimation(idleAnimationName);
        }

        protected override void UpdateLayer(float deltaTime)
        {
            if (!m_reloading)
                return;
            
            // rotate around the specified axis over the oneRevolutionSeconds duration for the total reloadSeconds
            m_t += deltaTime;
            float a = Mathf.Clamp01(m_t / reloadSeconds);
            float revolutions = (reloadSeconds / oneRevolutionSeconds);
            float angle = Mathf.Lerp(0f, 360f * revolutions, a);
            Quaternion q = Quaternion.AngleAxis(angle, axis);

            motionTarget.AddRotationOffset(q);
        }

        public void CrossFadeToAnimation(string animationName)
        {
            if (animator == null || string.IsNullOrEmpty(animationName))
                return;

            animator.CrossFadeInFixedTime(animationName, crossFadeDuration);
        }
    }
}