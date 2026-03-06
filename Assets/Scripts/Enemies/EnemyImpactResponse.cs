using System.Collections;
using Ia.Core.Update;
using Sixty.Combat;
using UnityEngine;

namespace Sixty.Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Health))]
    public class EnemyImpactResponse : IaBehaviour
    {
        [Header("Impact")]
        [SerializeField] private float knockbackStrength = 2.8f;
        [SerializeField] private float killKnockbackStrength = 4.2f;
        [SerializeField] private float velocityDamping = 0.35f;
        [SerializeField] private bool flattenToMovementPlane = true;

        [Header("Stun")]
        [SerializeField] private float stunDuration = 0.09f;

        private Rigidbody body;
        private Health health;
        private EnemyChaser chaser;
        private EnemyShooter shooter;
        private Coroutine stunRoutine;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            body = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
            chaser = GetComponent<EnemyChaser>();
            shooter = GetComponent<EnemyShooter>();
        }

        protected override void OnIaDisable()
        {
            if (stunRoutine != null)
            {
                StopCoroutine(stunRoutine);
                stunRoutine = null;
            }
        }

        public void ApplyImpact(Vector3 hitDirection, bool killed)
        {
            if (body == null)
            {
                return;
            }

            Vector3 direction = hitDirection;
            if (flattenToMovementPlane)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            direction.Normalize();

            Vector3 dampedVelocity = body.linearVelocity * Mathf.Clamp01(velocityDamping);
            if (flattenToMovementPlane)
            {
                dampedVelocity.y = 0f;
            }

            body.linearVelocity = dampedVelocity;
            float force = killed ? killKnockbackStrength : knockbackStrength;
            body.AddForce(direction * Mathf.Max(0f, force), ForceMode.VelocityChange);

            if (flattenToMovementPlane)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.y = 0f;
                body.linearVelocity = velocity;
            }

            if (killed || stunDuration <= 0f)
            {
                return;
            }

            if (stunRoutine != null)
            {
                StopCoroutine(stunRoutine);
            }

            stunRoutine = StartCoroutine(StunRoutine());
        }

        private IEnumerator StunRoutine()
        {
            bool hadChaser = chaser != null && chaser.enabled;
            bool hadShooter = shooter != null && shooter.enabled;

            if (hadChaser)
            {
                chaser.enabled = false;
            }

            if (hadShooter)
            {
                shooter.enabled = false;
            }

            yield return new WaitForSeconds(stunDuration);

            if (this == null || health == null || health.IsDead)
            {
                stunRoutine = null;
                yield break;
            }

            if (hadChaser && chaser != null)
            {
                chaser.enabled = true;
            }

            if (hadShooter && shooter != null)
            {
                shooter.enabled = true;
            }

            stunRoutine = null;
        }
    }
}
