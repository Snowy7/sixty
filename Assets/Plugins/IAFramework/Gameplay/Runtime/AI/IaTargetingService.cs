using Ia.Core.Update;
using Ia.Gameplay.Actors;
using UnityEngine;

namespace Ia.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class IaTargetingService : IaBehaviour
    {
        [SerializeField] string playerTag = "Player";
        [SerializeField] float reacquireInterval = 0.5f;

        float m_nextReacquireTime;
        IaActor m_cachedPlayer;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        public IaActor GetPlayer()
        {
            if (m_cachedPlayer != null)
                return m_cachedPlayer;

            TryAcquire();
            return m_cachedPlayer;
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (m_cachedPlayer != null)
                return;

            if (Time.time < m_nextReacquireTime)
                return;

            m_nextReacquireTime = Time.time + reacquireInterval;
            TryAcquire();
        }

        void TryAcquire()
        {
            GameObject go = GameObject.FindGameObjectWithTag(playerTag);
            if (go == null)
                return;

            IaActor actor = go.GetComponentInParent<IaActor>();
            if (actor == null)
                return;

            m_cachedPlayer = actor;
        }
    }
}