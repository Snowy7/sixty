using Ia.Core.Update;
using Ia.Gameplay.Actors;
using UnityEngine;

namespace Ia.Gameplay.AI
{
    public abstract class IaEnemyBrainBase : IaBehaviour
    {
        [Header("References")]
        [SerializeField] protected IaActor selfActor;
        [SerializeField] protected IaTargetingService targeting;
        [SerializeField] protected IaNavAgentMotor motor;
        [SerializeField] protected IaEnemyMeleeAttack melee;

        [Header("Aggro")]
        [SerializeField] protected float aggroRange = 25f;
        [SerializeField] protected float stoppingDistance = 1.4f;

        protected IaActor target;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void OnIaAwake()
        {
            if (selfActor == null)
                selfActor = GetComponent<IaActor>();

            if (targeting == null)
                targeting = GetComponent<IaTargetingService>();

            if (motor == null)
                motor = GetComponent<IaNavAgentMotor>();

            if (melee == null)
                melee = GetComponent<IaEnemyMeleeAttack>();
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (selfActor != null && !selfActor.IsAlive)
            {
                motor?.Stop();
                return;
            }

            AcquireTarget();
            if (target == null)
            {
                motor?.Stop();
                return;
            }

            TickBrain(deltaTime);
        }

        void AcquireTarget()
        {
            IaActor player = targeting != null ? targeting.GetPlayer() : null;
            if (player == null || !player.IsAlive)
            {
                target = null;
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > aggroRange)
            {
                target = null;
                return;
            }

            target = player;
        }

        protected abstract void TickBrain(float deltaTime);
    }
}