using Ia.Core.Update;
using UnityEngine;
using UnityEngine.AI;

namespace Ia.Gameplay.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class IaNavAgentMotor : IaBehaviour
    {
        [SerializeField] NavMeshAgent agent;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void OnIaAwake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
        }

        public void SetSpeed(float speed)
        {
            if (agent != null)
                agent.speed = speed;
        }

        public void Stop()
        {
            if (agent == null)
                return;

            agent.isStopped = true;
            agent.ResetPath();
        }

        public void MoveTo(Vector3 position, float stoppingDistance)
        {
            if (agent == null || !agent.isOnNavMesh)
                return;

            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            agent.SetDestination(position);
        }

        public float RemainingDistance()
        {
            if (agent == null || !agent.hasPath)
                return float.PositiveInfinity;

            return agent.remainingDistance;
        }
    }
}