using Ia.Core.Motion;
using Ia.Core.Update;
using Ia.Gameplay.Characters;
using UnityEngine;

namespace Ia.Gameplay.Combat
{
    public sealed class IaWeaponSwayMotionLayer : IaMotionLayer
    {
        [Header("References")]
        [SerializeField] IaPlayerInputDriver inputDriver;

        [Header("Sway Settings")]
        [Tooltip("How much rotation sway from mouse look.")]
        [SerializeField] float lookSwayStrength = 1.4f;

        [Tooltip("Clamp for sway rotation in degrees.")]
        [SerializeField] float maxSwayAngle = 4f;
        
        [Tooltip("How much movement sway from player movement.")]
        [SerializeField] float movementSwayStrength = 0f;

        [Tooltip("How fast the sway follows input.")]
        [SerializeField] float snappiness = 14f;

        [Tooltip("How fast the sway returns to center.")]
        [SerializeField] float returnSpeed = 18f;

        Vector3 m_targetEuler;
        Vector3 m_currentEuler;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;

        protected override void OnIaAwake()
        {
            base.OnIaAwake();

            if (inputDriver == null)
                inputDriver = GetComponentInParent<IaPlayerInputDriver>();
        }

        protected override void UpdateLayer(float deltaTime)
        {
            if (inputDriver == null)
                return;

            Vector2 look = inputDriver.GetMouseInput();
            Vector2 movement = inputDriver.GetMovementInput(false).Move;
            
            // Interpret look as "how much to sway opposite direction"
            float yaw = -look.x * lookSwayStrength * 0.02f;
            float pitch = look.y * lookSwayStrength * 0.02f;

            m_targetEuler = new Vector3(pitch, yaw, yaw);
            m_targetEuler.x = Mathf.Clamp(m_targetEuler.x, -maxSwayAngle, maxSwayAngle);
            m_targetEuler.y = Mathf.Clamp(m_targetEuler.y, -maxSwayAngle, maxSwayAngle);
            m_targetEuler.z = Mathf.Clamp(m_targetEuler.z, -maxSwayAngle, maxSwayAngle);
            
            // Add movement sway
            float moveYaw = -movement.x * movementSwayStrength;
            float movePitch = movement.y * movementSwayStrength;
            m_targetEuler += new Vector3(movePitch, moveYaw, moveYaw);
            m_targetEuler.x = Mathf.Clamp(m_targetEuler.x, -maxSwayAngle, maxSwayAngle);
            m_targetEuler.y = Mathf.Clamp(m_targetEuler.y, -maxSwayAngle, maxSwayAngle);
            m_targetEuler.z = Mathf.Clamp(m_targetEuler.z, -maxSwayAngle, maxSwayAngle);

            // Return toward zero smoothly (prevents permanent offset)
            m_targetEuler = Vector3.Lerp(
                m_targetEuler,
                Vector3.zero,
                returnSpeed * deltaTime
            );

            m_currentEuler = Vector3.Slerp(
                m_currentEuler,
                m_targetEuler,
                snappiness * deltaTime
            );

            motionTarget.AddRotationOffset(Quaternion.Euler(m_currentEuler * weight));
        }
    }
}