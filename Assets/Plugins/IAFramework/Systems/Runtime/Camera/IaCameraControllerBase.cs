using UnityEngine;
using Ia.Core.Update;

namespace Ia.Systems.Camera
{
    public abstract class IaCameraControllerBase : IaBehaviour
    {
        [Header("Common")]
        [SerializeField] protected Transform target;
        [SerializeField] protected bool lockCursor = true;

        protected bool m_cameraInputEnabled = true;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.LateUpdate;

        protected override void OnIaEnable()
        {
            if (lockCursor)
            {
                LockCursor(true);
            }
        }

        protected override void OnIaDisable()
        {
            if (lockCursor)
            {
                LockCursor(false);
            }
        }

        public void SetTarget(Transform target)
        {
            this.target = target;
        }

        public void EnableCameraInput(bool enabled)
        {
            m_cameraInputEnabled = enabled;
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}