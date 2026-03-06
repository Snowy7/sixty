using UnityEngine;
using Ia.Core.Motion;
using Ia.Gameplay.Characters;
using Ia.Systems.Camera;

namespace Ia.Systems.Motion
{
    public class IaFovMotionLayer : IaMotionLayer
    {
        [Header("References")]
        [SerializeField] FovController targetCamera;
        [SerializeField] IaCharacterState state;

        [Header("FOV")]
        [SerializeField] float baseFov = 60f;
        [SerializeField] float sprintFov = 72f;

        [Header("Spring")]
        [SerializeField] IaSpringFloat fovSpring = new IaSpringFloat
        {
            stiffness = 40f,
            damping = 10f
        };

        protected override void Awake()
        {
            base.Awake();

            
            if (state == null)
                state = FindAnyObjectByType<IaCharacterState>();

            if (targetCamera != null)
            {
                if (baseFov <= 0f)
                    baseFov = targetCamera.fieldOfView;

                fovSpring.Reset(baseFov);
            }
        }

        protected override void UpdateLayer(float deltaTime)
        {
            if (targetCamera == null)
                return;

            float target = baseFov;
            if (state != null && state.IsSprinting)
                target = sprintFov;

            fovSpring.target = target;
            fovSpring.Update(deltaTime);

            targetCamera.fieldOfView = fovSpring.value;
        }
    }
}