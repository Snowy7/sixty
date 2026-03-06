using System.Collections;
using Sixty.Combat;
using Sixty.Player;
using UnityEngine;

namespace Sixty.Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyChaser : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float stoppingDistance = 1.2f;
        [SerializeField] private float rotationLerpSpeed = 18f;
        [SerializeField] private bool lockVerticalPosition = true;

        private Rigidbody body;
        private Transform target;
        private float lockedY;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation |
                               (lockVerticalPosition ? RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            lockedY = transform.position.y;
        }

        private void FixedUpdate()
        {
            if (target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }

                return;
            }

            Vector3 toTarget = target.position - body.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance <= stoppingDistance || distance <= 0.001f)
            {
                return;
            }

            Vector3 moveDirection = toTarget / distance;
            Vector3 nextPosition = body.position + (moveDirection * moveSpeed * Time.fixedDeltaTime);
            if (lockVerticalPosition)
            {
                nextPosition.y = lockedY;
            }

            body.MovePosition(nextPosition);

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            Quaternion smoothed = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.fixedDeltaTime);
            body.MoveRotation(smoothed);
        }
    }

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Health))]
    public class EnemyImpactResponse : MonoBehaviour
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

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
            chaser = GetComponent<EnemyChaser>();
            shooter = GetComponent<EnemyShooter>();
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
