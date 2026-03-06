using UnityEngine;
using Ia.Core.Motion;
using Ia.Gameplay.Characters;

namespace Ia.Systems.Motion
{
    /// <summary>
    /// Headbob based on horizontal velocity, grounded state, sprint/crouch.
    /// Designed for FPS camera motionTarget.
    /// </summary>
    public class IaHeadbobMotionLayer : IaMotionLayer
    {
        [Header("References")]
        [SerializeField] IaCharacterMovementController movement;
        [SerializeField] IaCharacterState state;

        [Header("Bob Settings")]
        [SerializeField] float walkAmplitude = 0.06f;
        [SerializeField] float walkFrequency = 7f;
        [SerializeField] float sprintMultiplier = 1.5f;
        [SerializeField] float crouchMultiplier = 0.5f;
        [SerializeField] float minSpeedForBob = 0.1f;

        [Header("Smoothing")]
        [SerializeField] float positionLerpSpeed = 12f;

        float m_time;
        Vector3 m_currentOffset;

        protected override void Awake()
        {
            base.Awake();

            if (movement == null)
                movement = FindAnyObjectByType<IaCharacterMovementController>();

            if (state == null && movement != null)
                state = movement.GetComponent<IaCharacterState>();
        }

        protected override void UpdateLayer(float deltaTime)
        {
            if (movement == null)
                return;

            Vector3 vel = movement.Velocity;
            Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
            float speed = horizontal.magnitude;

            Vector3 targetOffset = Vector3.zero;

            if (speed >= minSpeedForBob && movement.IsGrounded)
            {
                m_time += deltaTime * walkFrequency;

                float amp = walkAmplitude;
                if (state != null && state.IsSprinting)
                    amp *= sprintMultiplier;
                if (state != null && state.IsCrouching)
                    amp *= crouchMultiplier;

                float bobY = Mathf.Sin(m_time * 2f) * amp;
                float bobX = Mathf.Cos(m_time) * amp * 0.5f;

                targetOffset = new Vector3(bobX, bobY, 0f);
            }
            else
            {
                // reset time slowly so phase doesn't jump hard
                m_time = Mathf.Lerp(m_time, 0f, deltaTime * 2f);
            }

            m_currentOffset = Vector3.Lerp(
                m_currentOffset,
                targetOffset,
                deltaTime * positionLerpSpeed
            );

            motionTarget.AddPositionOffset(m_currentOffset * weight);
        }
    }
}