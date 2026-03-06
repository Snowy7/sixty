using Ia.Core.Events;
using Ia.Core.Motion;
using Ia.Core.Update;
using Ia.Gameplay.Actors;
using UnityEngine;

namespace Ia.Gameplay.Combat
{
    public sealed class IaWeaponFireKickMotionLayer : IaMotionLayer
    {
        [Header("Owner Filter")]
        [SerializeField] IaActor ownerFilter;

        [Header("Kick Settings")]
        [SerializeField] Vector3 kickBack = new(0f, 0f, -0.05f);
        [SerializeField] Vector3 kickUp = new(0f, 0.015f, 0f);

        [Header("Smoothing")]
        [SerializeField] float snappiness = 30f;
        [SerializeField] float returnSpeed = 22f;

        Vector3 m_targetPos;
        Vector3 m_currentPos;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;

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

            m_targetPos += kickBack + kickUp;
        }

        protected override void UpdateLayer(float deltaTime)
        {
            m_targetPos = Vector3.Lerp(m_targetPos, Vector3.zero, returnSpeed * deltaTime);
            m_currentPos = Vector3.Lerp(m_currentPos, m_targetPos, snappiness * deltaTime);

            motionTarget.AddPositionOffset(m_currentPos * weight);
        }
    }
}