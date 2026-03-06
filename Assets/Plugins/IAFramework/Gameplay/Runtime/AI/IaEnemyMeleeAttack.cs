using Ia.Core.Update;
using Ia.Gameplay.Actors;
using Ia.Gameplay.Combat;
using UnityEngine;

namespace Ia.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class IaEnemyMeleeAttack : IaBehaviour
    {
        [Header("References")]
        [SerializeField] IaActor selfActor;

        [Header("Attack")]
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float attackCooldown = 1.1f;
        [SerializeField] float damage = 10f;
        [SerializeField] IaDamageType damageType = IaDamageType.Physical;

        float m_nextAttackTime;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void OnIaAwake()
        {
            if (selfActor == null)
                selfActor = GetComponent<IaActor>();
        }

        public bool CanAttack(IaActor target)
        {
            if (target == null || !target.IsAlive)
                return false;

            if (Time.time < m_nextAttackTime)
                return false;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            return dist <= attackRange;
        }

        public bool TryAttack(IaActor target)
        {
            if (!CanAttack(target))
                return false;

            m_nextAttackTime = Time.time + attackCooldown;

            Vector3 hitPoint = target.transform.position;
            Vector3 hitNormal = (target.transform.position - transform.position).normalized;

            IaDamageInfo dmg = new IaDamageInfo(
                amount: damage,
                type: damageType,
                source: selfActor,
                hitPoint: hitPoint,
                hitNormal: hitNormal
            );

            target.ApplyDamage(dmg);
            return true;
        }

        public float GetAttackRange() => attackRange;
    }
}