using Ia.Core.Events;
using UnityEngine;
using Ia.Core.Motion;
using Ia.Gameplay.Characters;

namespace Ia.Systems.Motion
{
    public class IaJumpLandMotionLayer : IaMotionLayer
    {
        [Header("Spring Settings")]
        [SerializeField] IaSpringVector3 positionSpring = new IaSpringVector3
        {
            stiffness = 50f,
            damping = 10f
        };

        [Header("Impulses")]
        [SerializeField] Vector3 jumpImpulse = new Vector3(0f, -0.04f, 0.02f);
        [SerializeField] Vector3 landImpulse = new Vector3(0f, 0.08f, -0.03f);

        protected override void OnIaAwake()
        {
            base.OnIaAwake();

            if (motionTarget == null)
                motionTarget = GetComponent<IaMotionTarget>();

            positionSpring.Reset(Vector3.zero);

            IaEventBus.Subscribe<OnPlayerJumped>(OnPlayerJumped);
            IaEventBus.Subscribe<OnPlayerLanded>(OnPlayerLanded);
        }

        protected override void OnIaDestroy()
        {
            IaEventBus.Unsubscribe<OnPlayerJumped>(OnPlayerJumped);
            IaEventBus.Unsubscribe<OnPlayerLanded>(OnPlayerLanded);
        }

        void OnPlayerJumped(OnPlayerJumped evt)
        {
            positionSpring.AddImpulse(jumpImpulse);
        }

        void OnPlayerLanded(OnPlayerLanded evt)
        {
            positionSpring.AddImpulse(landImpulse);
        }

        protected override void UpdateLayer(float deltaTime)
        {
            if (motionTarget == null)
                return;

            // Always spring back to zero
            positionSpring.target = Vector3.zero;
            positionSpring.Update(deltaTime);
            
            // rotate the offset based on the motion target's rotation so we get consistent world space motion downwards

            motionTarget.AddPositionOffset(motionTarget.GetWorldSpaceOffset(positionSpring.value * weight));
        }
    }
}