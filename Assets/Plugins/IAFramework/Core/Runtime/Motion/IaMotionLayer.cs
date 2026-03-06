using Ia.Core.Update;
using UnityEngine;

namespace Ia.Core.Motion
{
    /// <summary>
    /// Base class for a motion effect layer.
    /// Used by IaMotionController, not updated directly by the update manager.
    /// </summary>
    public abstract class IaMotionLayer : IaBehaviour
    {
        [SerializeField] protected IaMotionTarget motionTarget;
        [SerializeField] protected float weight = 1f;
        [SerializeField] protected bool enabledByDefault = true;

        bool m_enabled;

        protected override void OnIaAwake()
        {
            base.OnIaAwake();
            m_enabled = enabledByDefault;
            if (motionTarget == null)
                motionTarget = GetComponent<IaMotionTarget>();
        }

        public void SetEnabled(bool enabled)
        {
            m_enabled = enabled;
        }

        /// <summary>
        /// Called by IaMotionController once per frame.
        /// </summary>
        public virtual void OnTick(float deltaTime)
        {
            if (!m_enabled || motionTarget == null)
                return;

            UpdateLayer(deltaTime);
        }

        protected abstract void UpdateLayer(float deltaTime);
    }
}