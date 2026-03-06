using System;
using System.Collections.Generic;
using Ia.Core.Config;
using Ia.Core.Debugging;
using UnityEngine;

namespace Ia.Core.Update
{
    public class IaUpdateManager : MonoBehaviour
    {
        private sealed class PhaseBucket
        {
            public readonly List<IaBehaviour> Behaviours;
            public readonly HashSet<IaBehaviour> Membership;
            public readonly List<IaBehaviour> PendingAdd;
            public readonly List<IaBehaviour> PendingRemove;
            public bool IsIterating;
            public bool SortDirty;

            public PhaseBucket(int capacity)
            {
                Behaviours = new List<IaBehaviour>(capacity);
                Membership = new HashSet<IaBehaviour>();
                PendingAdd = new List<IaBehaviour>(Mathf.Max(8, capacity / 8));
                PendingRemove = new List<IaBehaviour>(Mathf.Max(8, capacity / 8));
                SortDirty = false;
            }
        }

        private static IaUpdateManager m_instance;
        private static bool m_isShuttingDown;

        public static IaUpdateManager Instance
        {
            get
            {
                if (m_instance == null && !m_isShuttingDown)
                {
                    m_instance = FindFirstObjectByType<IaUpdateManager>();

                    if (m_instance == null)
                    {
                        var go = new GameObject("[IaUpdateManager]");
                        m_instance = go.AddComponent<IaUpdateManager>();
                        IaLogger.Warning(
                            IaLogCategory.Update,
                            "Auto-created IaUpdateManager on: " + go.name
                        );
#if UNITY_EDITOR
                        go.AddComponent<IaUpdateDebugPanel>();
#endif
                    }
                }

                return m_instance;
            }
        }

        [Header("Group Toggles")]
        [SerializeField] private bool playerEnabled = true;
        [SerializeField] private bool aiEnabled = true;
        [SerializeField] private bool worldEnabled = true;
        [SerializeField] private bool uiEnabled = true;
        [SerializeField] private bool fxEnabled = true;
        [SerializeField] private bool custom1Enabled = true;
        [SerializeField] private bool custom2Enabled = true;

        private const int GroupCount = (int)IaUpdateGroup.Custom2 + 1;
        private readonly bool[] m_groupEnabled = new bool[GroupCount];

        private readonly PhaseBucket[] m_phaseBuckets =
        {
            new PhaseBucket(64), // Update
            new PhaseBucket(32), // FixedUpdate
            new PhaseBucket(32)  // LateUpdate
        };

        private List<IaLifecycleEntry> m_lifecycleQueue = new List<IaLifecycleEntry>(32);
        private List<IaLifecycleEntry> m_lifecycleProcessing = new List<IaLifecycleEntry>(32);
        private bool m_hasLifecycleEvents;

        private static readonly Comparison<IaLifecycleEntry> s_lifecycleComparer = CompareLifecycle;
        private static readonly Comparison<IaBehaviour> s_behaviourComparer = CompareBehaviour;

        private void Awake()
        {
            if (m_instance != null && m_instance != this)
            {
                Debug.LogWarning("[IaUpdateManager] Duplicate instance destroyed: " + gameObject.name);
                Destroy(gameObject);
                return;
            }

            m_instance = this;
            m_isShuttingDown = false;
            DontDestroyOnLoad(gameObject);
            InitializeGroupEnabledState();

            var global = IaGlobal.Settings;
            if (global?.logSettings != null)
            {
                IaLogger.Info(IaLogCategory.Update, "IaUpdateManager initialized.");
            }
        }

        private void OnDestroy()
        {
            if (m_instance == this)
            {
                m_instance = null;
            }
        }

        private void InitializeGroupEnabledState()
        {
            m_groupEnabled[(int)IaUpdateGroup.Player] = playerEnabled;
            m_groupEnabled[(int)IaUpdateGroup.AI] = aiEnabled;
            m_groupEnabled[(int)IaUpdateGroup.World] = worldEnabled;
            m_groupEnabled[(int)IaUpdateGroup.UI] = uiEnabled;
            m_groupEnabled[(int)IaUpdateGroup.FX] = fxEnabled;
            m_groupEnabled[(int)IaUpdateGroup.Custom1] = custom1Enabled;
            m_groupEnabled[(int)IaUpdateGroup.Custom2] = custom2Enabled;
        }

        #region Public API

        public void SetGroupEnabled(IaUpdateGroup group, bool enabled)
        {
            int index = (int)group;
            if (index < 0 || index >= m_groupEnabled.Length)
            {
                return;
            }

            m_groupEnabled[index] = enabled;

            switch (group)
            {
                case IaUpdateGroup.Player: playerEnabled = enabled; break;
                case IaUpdateGroup.AI: aiEnabled = enabled; break;
                case IaUpdateGroup.World: worldEnabled = enabled; break;
                case IaUpdateGroup.UI: uiEnabled = enabled; break;
                case IaUpdateGroup.FX: fxEnabled = enabled; break;
                case IaUpdateGroup.Custom1: custom1Enabled = enabled; break;
                case IaUpdateGroup.Custom2: custom2Enabled = enabled; break;
            }
        }

        public bool IsGroupEnabled(IaUpdateGroup group)
        {
            int index = (int)group;
            return index >= 0 && index < m_groupEnabled.Length && m_groupEnabled[index];
        }

        public void Register(IaBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            IaUpdatePhase phases = behaviour.GetUpdatePhases();

            if ((phases & IaUpdatePhase.Update) != 0)
            {
                QueueAdd(PhaseIndex.Update, behaviour);
            }

            if ((phases & IaUpdatePhase.FixedUpdate) != 0)
            {
                QueueAdd(PhaseIndex.FixedUpdate, behaviour);
            }

            if ((phases & IaUpdatePhase.LateUpdate) != 0)
            {
                QueueAdd(PhaseIndex.LateUpdate, behaviour);
            }
        }

        public void Unregister(IaBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            QueueRemove(PhaseIndex.Update, behaviour);
            QueueRemove(PhaseIndex.FixedUpdate, behaviour);
            QueueRemove(PhaseIndex.LateUpdate, behaviour);
        }

        public int GetBehaviourCount(IaUpdatePhase phase)
        {
            if (!TryGetPhaseIndex(phase, out int index))
            {
                return 0;
            }

            PhaseBucket bucket = m_phaseBuckets[index];
            CompactNulls(bucket);
            return bucket.Behaviours.Count;
        }

        public int GetBehaviourCount(IaUpdatePhase phase, IaUpdateGroup group)
        {
            if (!TryGetPhaseIndex(phase, out int index))
            {
                return 0;
            }

            PhaseBucket bucket = m_phaseBuckets[index];
            CompactNulls(bucket);

            int count = 0;
            for (int i = 0; i < bucket.Behaviours.Count; i++)
            {
                IaBehaviour behaviour = bucket.Behaviours[i];
                if (behaviour != null && behaviour.GetUpdateGroup() == group)
                {
                    count++;
                }
            }

            return count;
        }

        internal void QueueLifecycle(IaBehaviour behaviour, IaLifecycleEvent evt)
        {
            if (behaviour == null)
            {
                return;
            }

            m_lifecycleQueue.Add(new IaLifecycleEntry
            {
                Behaviour = behaviour,
                Event = evt,
                Group = behaviour.GetUpdateGroup(),
                Priority = behaviour.GetUpdatePriority()
            });
            m_hasLifecycleEvents = true;
        }

        #endregion

        #region Phase Management

        private enum PhaseIndex
        {
            Update = 0,
            FixedUpdate = 1,
            LateUpdate = 2
        }

        private static bool TryGetPhaseIndex(IaUpdatePhase phase, out int index)
        {
            switch (phase)
            {
                case IaUpdatePhase.Update:
                    index = (int)PhaseIndex.Update;
                    return true;
                case IaUpdatePhase.FixedUpdate:
                    index = (int)PhaseIndex.FixedUpdate;
                    return true;
                case IaUpdatePhase.LateUpdate:
                    index = (int)PhaseIndex.LateUpdate;
                    return true;
                default:
                    index = -1;
                    return false;
            }
        }

        private void QueueAdd(PhaseIndex phase, IaBehaviour behaviour)
        {
            PhaseBucket bucket = m_phaseBuckets[(int)phase];

            if (bucket.IsIterating)
            {
                if (bucket.PendingRemove.Remove(behaviour))
                {
                    return;
                }

                if (bucket.Membership.Contains(behaviour) || bucket.PendingAdd.Contains(behaviour))
                {
                    return;
                }

                bucket.PendingAdd.Add(behaviour);
                return;
            }

            AddImmediate(bucket, behaviour);
        }

        private void QueueRemove(PhaseIndex phase, IaBehaviour behaviour)
        {
            PhaseBucket bucket = m_phaseBuckets[(int)phase];

            if (bucket.IsIterating)
            {
                if (bucket.PendingAdd.Remove(behaviour))
                {
                    return;
                }

                if (!bucket.Membership.Contains(behaviour) || bucket.PendingRemove.Contains(behaviour))
                {
                    return;
                }

                bucket.PendingRemove.Add(behaviour);
                return;
            }

            RemoveImmediate(bucket, behaviour);
        }

        private static void AddImmediate(PhaseBucket bucket, IaBehaviour behaviour)
        {
            if (behaviour == null || !bucket.Membership.Add(behaviour))
            {
                return;
            }

            bucket.Behaviours.Add(behaviour);
            bucket.SortDirty = true;
        }

        private static void RemoveImmediate(PhaseBucket bucket, IaBehaviour behaviour)
        {
            if (behaviour == null || !bucket.Membership.Remove(behaviour))
            {
                return;
            }

            int index = bucket.Behaviours.IndexOf(behaviour);
            if (index >= 0)
            {
                bucket.Behaviours.RemoveAt(index);
            }
        }

        private static void ApplyPendingMutations(PhaseBucket bucket)
        {
            if (bucket.PendingRemove.Count > 0)
            {
                for (int i = 0; i < bucket.PendingRemove.Count; i++)
                {
                    RemoveImmediate(bucket, bucket.PendingRemove[i]);
                }

                bucket.PendingRemove.Clear();
            }

            if (bucket.PendingAdd.Count > 0)
            {
                for (int i = 0; i < bucket.PendingAdd.Count; i++)
                {
                    AddImmediate(bucket, bucket.PendingAdd[i]);
                }

                bucket.PendingAdd.Clear();
            }
        }

        private static int CompareBehaviour(IaBehaviour a, IaBehaviour b)
        {
            int groupCompare = a.GetUpdateGroup().CompareTo(b.GetUpdateGroup());
            return groupCompare != 0 ? groupCompare : a.GetUpdatePriority().CompareTo(b.GetUpdatePriority());
        }

        private static int CompareLifecycle(IaLifecycleEntry a, IaLifecycleEntry b)
        {
            int groupCompare = a.Group.CompareTo(b.Group);
            return groupCompare != 0 ? groupCompare : a.Priority.CompareTo(b.Priority);
        }

        private static void EnsureSorted(PhaseBucket bucket)
        {
            if (!bucket.SortDirty || bucket.Behaviours.Count <= 1)
            {
                return;
            }

            bucket.Behaviours.Sort(s_behaviourComparer);
            bucket.SortDirty = false;
        }

        private static void CompactNulls(PhaseBucket bucket)
        {
            bool foundNull = false;
            for (int i = bucket.Behaviours.Count - 1; i >= 0; i--)
            {
                if (bucket.Behaviours[i] != null)
                {
                    continue;
                }

                bucket.Behaviours.RemoveAt(i);
                foundNull = true;
            }

            if (!foundNull)
            {
                return;
            }

            bucket.Membership.Clear();
            for (int i = 0; i < bucket.Behaviours.Count; i++)
            {
                IaBehaviour behaviour = bucket.Behaviours[i];
                if (behaviour != null)
                {
                    bucket.Membership.Add(behaviour);
                }
            }
        }

        #endregion

        #region Lifecycle Processing

        private void ProcessLifecycleQueue()
        {
            if (!m_hasLifecycleEvents)
            {
                return;
            }

            (m_lifecycleQueue, m_lifecycleProcessing) = (m_lifecycleProcessing, m_lifecycleQueue);
            m_lifecycleQueue.Clear();
            m_hasLifecycleEvents = false;

            if (m_lifecycleProcessing.Count > 1)
            {
                m_lifecycleProcessing.Sort(s_lifecycleComparer);
            }

            for (int i = 0; i < m_lifecycleProcessing.Count; i++)
            {
                IaLifecycleEntry entry = m_lifecycleProcessing[i];
                if (entry.Behaviour == null)
                {
                    continue;
                }

                if (entry.Event != IaLifecycleEvent.Disable && !IsGroupEnabled(entry.Group))
                {
                    continue;
                }

                try
                {
                    entry.Behaviour.InvokeLifecycle(entry.Event);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, entry.Behaviour);
                }
            }

            m_lifecycleProcessing.Clear();
        }

        #endregion

        #region Unity Update Loops

        private void Update()
        {
            ProcessLifecycleQueue();
            RunPhase(PhaseIndex.Update, Time.deltaTime, static (behaviour, delta) => behaviour.OnIaUpdate(delta));
        }

        private void FixedUpdate()
        {
            RunPhase(PhaseIndex.FixedUpdate, Time.fixedDeltaTime, static (behaviour, delta) => behaviour.OnIaFixedUpdate(delta));
        }

        private void LateUpdate()
        {
            RunPhase(PhaseIndex.LateUpdate, Time.deltaTime, static (behaviour, delta) => behaviour.OnIaLateUpdate(delta));
            ProcessLifecycleQueue();
        }

        private void RunPhase(PhaseIndex phase, float delta, Action<IaBehaviour, float> tick)
        {
            PhaseBucket bucket = m_phaseBuckets[(int)phase];
            ApplyPendingMutations(bucket);
            EnsureSorted(bucket);

            bucket.IsIterating = true;
            for (int i = 0; i < bucket.Behaviours.Count; i++)
            {
                IaBehaviour behaviour = bucket.Behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                if (!IsGroupEnabled(behaviour.GetUpdateGroup()))
                {
                    continue;
                }

                tick(behaviour, delta);
            }

            bucket.IsIterating = false;
            ApplyPendingMutations(bucket);
            CompactNulls(bucket);
        }

        private void OnApplicationQuit()
        {
            m_isShuttingDown = true;
        }

        #endregion
    }
}
