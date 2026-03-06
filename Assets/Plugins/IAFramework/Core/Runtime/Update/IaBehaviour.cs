using Ia.Core.Debugging;
using UnityEngine;

namespace Ia.Core.Update
{
    public class IaBehaviour : MonoBehaviour
    {
        protected virtual IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected virtual IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected virtual int UpdatePriority => 0;

        /// <summary>
        /// If true, lifecycle events (Awake/Enable/Start/Disable) are queued and
        /// executed in priority order. Set to false for legacy immediate behavior.
        /// </summary>
        protected virtual bool UseOrderedLifecycle => true;

        private bool m_isRegistered;
        private bool m_awakeProcessed;
        private bool m_startProcessed;
        private IaUpdateManager m_cachedManager;

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            CacheManager();

            if (UseOrderedLifecycle)
            {
                m_cachedManager?.QueueLifecycle(this, IaLifecycleEvent.Awake);
            }
            else
            {
                OnIaAwake();
            }
        }

        protected virtual void OnEnable()
        {
            RegisterWithUpdateManager();

            if (UseOrderedLifecycle)
            {
                m_cachedManager?.QueueLifecycle(this, IaLifecycleEvent.Enable);
            }
            else
            {
                OnIaEnable();
            }
        }

        protected virtual void Start()
        {
            if (UseOrderedLifecycle)
            {
                m_cachedManager?.QueueLifecycle(this, IaLifecycleEvent.Start);
            }
            else
            {
                OnIaStart();
            }
        }

        protected virtual void OnDisable()
        {
            if (UseOrderedLifecycle && m_cachedManager != null)
            {
                // Disable is processed immediately but in order with other disables this frame
                m_cachedManager.QueueLifecycle(this, IaLifecycleEvent.Disable);
            }
            else
            {
                OnIaDisable();
            }

            UnregisterFromUpdateManager();
        }

        protected virtual void OnDestroy()
        {
            // Destroy is always immediate - object is being removed
            OnIaDestroy();
            UnregisterFromUpdateManager();
        }

        #endregion

        #region Overridable Hooks

        protected virtual void OnIaAwake() { }
        protected virtual void OnIaEnable() { }
        protected virtual void OnIaStart() { }
        protected virtual void OnIaDisable() { }
        protected virtual void OnIaDestroy() { }

        public virtual void OnIaUpdate(float deltaTime) { }
        public virtual void OnIaFixedUpdate(float fixedDeltaTime) { }
        public virtual void OnIaLateUpdate(float deltaTime) { }

        #endregion

        #region Registration

        private void RegisterWithUpdateManager()
        {
            if (m_isRegistered)
                return;

            CacheManager();
            if (m_cachedManager == null)
            {
#if UNITY_EDITOR
                IaLogger.Warning(
                    this,
                    $"[IaBehaviour] No IaUpdateManager instance found for {name}."
                );
#endif
                return;
            }

            m_cachedManager.Register(this);
            m_isRegistered = true;
        }

        private void UnregisterFromUpdateManager()
        {
            if (!m_isRegistered)
                return;

            m_cachedManager?.Unregister(this);
            m_isRegistered = false;
        }

        private void CacheManager()
        {
            if (m_cachedManager == null)
            {
                m_cachedManager = IaUpdateManager.Instance;
            }
        }

        #endregion

        #region Internal Accessors

        internal IaUpdateGroup GetUpdateGroup() => UpdateGroup;
        internal IaUpdatePhase GetUpdatePhases() => UpdatePhases;
        internal int GetUpdatePriority() => UpdatePriority;

        internal void InvokeLifecycle(IaLifecycleEvent evt)
        {
            switch (evt)
            {
                case IaLifecycleEvent.Awake:
                    if (!m_awakeProcessed)
                    {
                        m_awakeProcessed = true;
                        OnIaAwake();
                    }
                    break;
                case IaLifecycleEvent.Enable:
                    OnIaEnable();
                    break;
                case IaLifecycleEvent.Start:
                    if (!m_startProcessed)
                    {
                        m_startProcessed = true;
                        OnIaStart();
                    }
                    break;
                case IaLifecycleEvent.Disable:
                    OnIaDisable();
                    break;
            }
        }

        #endregion
    }
}
