using System;
using Ia.Core.Update;
using UnityEngine;

namespace Ia.Systems.Camera
{
    public class FovController : IaBehaviour
    {
        [SerializeField] private UnityEngine.Camera targetCamera;
        protected override int UpdatePriority => 0;
        
        private bool m_fovChangeLocked = false;
        
        public float fieldOfView
        {
            get
            {
                if (targetCamera != null)
                {
                    return targetCamera.fieldOfView;
                }
                return 60f; // default FOV
            }
            set
            {
                SetFov(value);
            }
        }
        
        # if UNITY_EDITOR
        private void OnValidate()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<UnityEngine.Camera>();
        }
        # endif
        
        protected override void OnIaEnable()
        {
            base.OnIaEnable();
            if (targetCamera == null)
                targetCamera = GetComponent<UnityEngine.Camera>();
        }
        
        // set FOV instantly
        public void SetFov(float fov)
        {
            if (m_fovChangeLocked) return;
            
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = fov;
            }
        }
        
        public void ChangeLockedFov(float fov)
        {
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = fov;
            }
        }
        
        // lock/unlock FOV changes
        public void LockFovChange(bool isLocked)
        {
            m_fovChangeLocked = isLocked;
        }
    }
}