using System.Linq;
using Ia.Core.Debugging;
using UnityEngine;
using Ia.Gameplay.Characters;

namespace Ia.Systems.Camera
{
    public class IaFirstPersonCameraController : IaCameraControllerBase
    {
        [Header("Settings")]
        // Standard sensitivity is usually between 1.0 and 5.0 for raw input
        [SerializeField] private float sensitivityX = 2.0f; 
        [SerializeField] private float sensitivityY = 2.0f;
        [SerializeField, Range(0, 90f)] private float clampAngle = 85f;
        
        [Header("Smoothing")]
        // Set to 0 for "Call of Duty" snappy aim. 
        // Set to 10-20 for "Cinematic" smooth aim.
        [SerializeField] private float smoothTime = 0f;

        [Header("References")]
        [SerializeField] private IaPlayerInputDriver inputDriver;

        private float m_targetYaw;
        private float m_targetPitch;
        
        // We track the current rotation separately for smoothing
        private float m_currentYaw;
        private float m_currentPitch;
        
        private static readonly int MaxRotCache = 3;
        private float[] rotArrayHor = new float[MaxRotCache];
        private float[] rotArrayVert = new float[MaxRotCache];
        private int rotCacheIndex;

        protected override void OnIaAwake()
        {
            if (inputDriver == null)
            {
                inputDriver = GetComponent<IaPlayerInputDriver>();
            }

            if (target != null)
            {
                m_targetYaw = target.eulerAngles.y;
                m_targetPitch = target.eulerAngles.x;
                
                m_currentYaw = m_targetYaw;
                m_currentPitch = m_targetPitch;
            }
        }

        public override void OnIaLateUpdate(float deltaTime)
        {
            if (target == null || !m_cameraInputEnabled) return;

            // Get mouse input from the centralized input driver
            Vector2 mouseInput = GetMouseInput();

            // 1. Get RAW input (No Unity smoothing, No deltaTime)
            float mouseX = mouseInput.x;
            float mouseY = mouseInput.y;

            // 2. Accumulate input into a "Target"
            m_targetYaw += mouseX * sensitivityX;
            m_targetPitch -= mouseY * sensitivityY;
            m_targetPitch = Mathf.Clamp(m_targetPitch, -clampAngle, clampAngle);

            // 3. Smoothly interpolate or snap to target
            if (smoothTime > 0)
            {
                // Smooth camera (Cinematic feel)
                m_currentYaw = Mathf.Lerp(m_currentYaw, m_targetYaw, deltaTime * smoothTime);
                m_currentPitch = Mathf.Lerp(m_currentPitch, m_targetPitch, deltaTime * smoothTime);
            }
            else
            {
                // Instant camera (Competitive FPS feel)
                m_currentYaw = m_targetYaw;
                m_currentPitch = m_targetPitch;
            }

            // 4. Apply Rotation
            ApplyRotation();
        }

        private Vector2 GetMouseInput()
        {
            if (inputDriver != null)
            {
                // Use centralized input system
                var input = inputDriver.GetMouseInput();
                input = new Vector2(GetAverageHorizontal(input.x), GetAverageVertical(input.y));
                IncreaseRotCacheIndex();
                return input;
            }
            else
            {
                // Fallback to legacy input
                var input = new Vector2(GetAverageHorizontal(Input.GetAxisRaw("Mouse X")), GetAverageVertical(Input.GetAxisRaw("Mouse Y")));
                IncreaseRotCacheIndex();
                return input;
            }
        }
        
        private float GetAverageHorizontal(float h)
        {
            rotArrayHor[rotCacheIndex] = h;
            return rotArrayHor.Average();
        }
        
        private float GetAverageVertical(float v)
        {
            rotArrayVert[rotCacheIndex] = v;
            return rotArrayVert.Average();
        }

        private void IncreaseRotCacheIndex()
        {
            rotCacheIndex++;
            rotCacheIndex %= MaxRotCache;
        }
        
        public float GetRotProgress() => m_currentPitch / clampAngle;

        private void ApplyRotation()
        {
            // Rotate Body (Yaw only)
            target.rotation = Quaternion.Euler(0f, m_currentYaw, 0f);

            // Rotate Camera (Pitch + Yaw)
            transform.position = target.position; // Keep camera at head position
            transform.rotation = Quaternion.Euler(m_currentPitch, m_currentYaw, 0f);
        }
    }
}