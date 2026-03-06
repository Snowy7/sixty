using Ia.Core.Events;
using Ia.Core.Motion;
using Ia.Core.Update;
using UnityEngine;

namespace Ia.Gameplay.Combat
{
    public sealed class IaWeaponRecoilMotionLayer : IaMotionLayer
    {
        [Header("Owner Filter (optional)")]
        [Tooltip("If set, only apply recoil when this actor fires.")]
        [SerializeField]
        Ia.Gameplay.Actors.IaActor ownerFilter;

        [Header("Recoil Kick (Hipfire)")] [SerializeField]
        float recoilX = 6f; // pitch up

        [SerializeField] float recoilY = 2f; // yaw randomness
        [SerializeField] float recoilZ = 1f; // roll randomness

        [Header("Recoil Kick (Aiming)")] [SerializeField]
        bool supportAim = false;

        [SerializeField] float aimRecoilX = 3f;
        [SerializeField] float aimRecoilY = 1f;
        [SerializeField] float aimRecoilZ = 0.5f;

        [Header("Settings")] [Tooltip("How fast targetRotation returns to zero.")] [SerializeField]
        float returnSpeed = 18f;

        [Tooltip("How fast currentRotation follows targetRotation.")] [SerializeField]
        float snappiness = 25f;

        [Tooltip("Clamp pitch to avoid flipping into nonsense.")] [SerializeField]
        float maxPitch = 25f;

        Vector3 m_targetRotation;
        Vector3 m_currentRotation;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None; // driven by controller

        protected override void OnIaEnable()
        {
            IaEventBus.Subscribe<WeaponFiredEvent>(OnWeaponFired);
        }

        protected override void OnIaDisable()
        {
            IaEventBus.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
        }

        void OnWeaponFired(WeaponFiredEvent evt)
        {
            if (ownerFilter != null && evt.Shooter != ownerFilter)
                return;

            // If you want aim support, wire in your aiming state here.
            bool isAiming = false;

            float x = isAiming && supportAim ? aimRecoilX : recoilX;
            float y = isAiming && supportAim ? aimRecoilY : recoilY;
            float z = isAiming && supportAim ? aimRecoilZ : recoilZ;

            m_targetRotation += new Vector3(
                x,
                Random.Range(-y, y),
                Random.Range(-z, z)
            );

            // Clamp pitch so recoil doesn't spiral out of control.
            m_targetRotation.x = Mathf.Clamp(m_targetRotation.x, -maxPitch, maxPitch);
        }

        protected override void UpdateLayer(float deltaTime)
        {
            // Return target toward zero (like screenshot)
            m_targetRotation = Vector3.Lerp(
                m_targetRotation,
                Vector3.zero,
                returnSpeed * deltaTime
            );

            // Follow target (snappy)
            m_currentRotation = Vector3.Slerp(
                m_currentRotation,
                m_targetRotation,
                snappiness * deltaTime
            );

            // Apply as rotation offset via motion target
            motionTarget.AddRotationOffset(Quaternion.Euler(m_currentRotation * weight));
        }
    }
}