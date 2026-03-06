using Sixty.Player;
using UnityEngine;
using Ia.Core.Update;

namespace Sixty.Enemies
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyChaser : IaBehaviour
    {
        public enum MovementMode
        {
            DirectChase = 0,
            OrbitStrafe = 1,
            ChargeBurst = 2,
            HeavyTank = 3
        }

        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float stoppingDistance = 1.2f;
        [SerializeField] private float rotationLerpSpeed = 18f;
        [SerializeField] private bool lockVerticalPosition = true;
        [SerializeField] private float playerLookupInterval = 0.4f;
        [Header("Behavior")]
        [SerializeField] private MovementMode moveMode = MovementMode.DirectChase;
        [SerializeField] private float orbitPreferredDistance = 6f;
        [SerializeField] private float orbitStrafeWeight = 1f;
        [SerializeField] private float orbitApproachWeight = 0.9f;
        [SerializeField] private float orbitDirectionFlipInterval = 1.8f;
        [SerializeField] private float orbitSpeedMultiplier = 0.95f;
        [SerializeField] private float chargeSpeedMultiplier = 2.7f;
        [SerializeField] private float chargeApproachSpeedMultiplier = 0.85f;
        [SerializeField] private float chargeDuration = 0.42f;
        [SerializeField] private float chargeCooldown = 1.6f;
        [SerializeField] private float chargeMinRange = 2.4f;
        [SerializeField] private float chargeMaxRange = 14f;
        [SerializeField] private float chargeTurnMultiplier = 0.65f;
        [SerializeField] private float heavySpeedMultiplier = 0.72f;
        [SerializeField] private float heavyTurnMultiplier = 0.58f;

        private Rigidbody body;
        private Transform target;
        private float lockedY;
        private float nextPlayerLookupAt;
        private int orbitDirection = 1;
        private float nextOrbitDirectionFlipAt;
        private float chargeTimer;
        private float nextChargeAt;
        private Vector3 chargeDirection = Vector3.forward;
        private float externalMoveSpeedMultiplier = 1f;
        private static PlayerController cachedPlayer;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.FixedUpdate;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation |
                               (lockVerticalPosition ? RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            lockedY = transform.position.y;
            nextOrbitDirectionFlipAt = Time.time + Mathf.Max(0.2f, orbitDirectionFlipInterval);
        }

        public override void OnIaFixedUpdate(float fixedDeltaTime)
        {
            if (target == null && !TryResolveTarget())
            {
                return;
            }

            if (target == null)
            {
                return;
            }

            Vector3 toTarget = target.position - body.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
            {
                return;
            }

            Vector3 toTargetDirection = toTarget / distance;
            ResolveMovement(
                toTargetDirection,
                distance,
                fixedDeltaTime,
                out Vector3 moveDirection,
                out float speedMultiplier,
                out float turnMultiplier);

            if (moveDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float effectiveSpeed = moveSpeed * Mathf.Max(0.1f, speedMultiplier) * Mathf.Max(0.1f, externalMoveSpeedMultiplier);
            Vector3 nextPosition = body.position + (moveDirection * effectiveSpeed * fixedDeltaTime);
            if (lockVerticalPosition)
            {
                nextPosition.y = lockedY;
            }

            body.MovePosition(nextPosition);

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            float effectiveTurn = rotationLerpSpeed * Mathf.Max(0.1f, turnMultiplier);
            Quaternion smoothed = Quaternion.Slerp(transform.rotation, targetRotation, effectiveTurn * fixedDeltaTime);
            body.MoveRotation(smoothed);
        }

        public void SetExternalMoveSpeedMultiplier(float multiplier)
        {
            externalMoveSpeedMultiplier = Mathf.Clamp(multiplier, 0.2f, 4f);
        }

        private void ResolveMovement(
            Vector3 toTargetDirection,
            float distance,
            float fixedDeltaTime,
            out Vector3 moveDirection,
            out float speedMultiplier,
            out float turnMultiplier)
        {
            moveDirection = toTargetDirection;
            speedMultiplier = 1f;
            turnMultiplier = 1f;

            switch (moveMode)
            {
                case MovementMode.DirectChase:
                    if (distance <= stoppingDistance)
                    {
                        moveDirection = Vector3.zero;
                    }

                    break;

                case MovementMode.OrbitStrafe:
                    if (Time.time >= nextOrbitDirectionFlipAt)
                    {
                        orbitDirection *= -1;
                        nextOrbitDirectionFlipAt = Time.time + Mathf.Max(0.2f, orbitDirectionFlipInterval);
                    }

                    float preferredDistance = Mathf.Max(stoppingDistance + 0.5f, orbitPreferredDistance);
                    Vector3 tangent = Vector3.Cross(Vector3.up, toTargetDirection) * orbitDirection;
                    float distanceError = distance - preferredDistance;
                    float radialInfluence = Mathf.Clamp(distanceError / preferredDistance, -1f, 1f) * orbitApproachWeight;
                    Vector3 orbitVector = (tangent * orbitStrafeWeight) + (toTargetDirection * radialInfluence);
                    if (distance < stoppingDistance * 0.75f)
                    {
                        orbitVector += -toTargetDirection * 1.25f;
                    }

                    moveDirection = orbitVector.sqrMagnitude > 0.001f ? orbitVector.normalized : toTargetDirection;
                    speedMultiplier = orbitSpeedMultiplier;
                    break;

                case MovementMode.ChargeBurst:
                    turnMultiplier = chargeTurnMultiplier;
                    if (chargeTimer > 0f)
                    {
                        chargeTimer -= fixedDeltaTime;
                        moveDirection = chargeDirection;
                        speedMultiplier = chargeSpeedMultiplier;
                        break;
                    }

                    bool inChargeRange = distance >= chargeMinRange && distance <= chargeMaxRange;
                    if (inChargeRange && Time.time >= nextChargeAt)
                    {
                        chargeDirection = toTargetDirection;
                        chargeTimer = Mathf.Max(0.05f, chargeDuration);
                        nextChargeAt = Time.time + Mathf.Max(0.1f, chargeCooldown);
                        moveDirection = chargeDirection;
                        speedMultiplier = chargeSpeedMultiplier;
                    }
                    else if (distance <= stoppingDistance)
                    {
                        moveDirection = Vector3.zero;
                    }
                    else
                    {
                        moveDirection = toTargetDirection;
                        speedMultiplier = chargeApproachSpeedMultiplier;
                    }

                    break;

                case MovementMode.HeavyTank:
                    if (distance <= stoppingDistance)
                    {
                        moveDirection = Vector3.zero;
                    }

                    speedMultiplier = heavySpeedMultiplier;
                    turnMultiplier = heavyTurnMultiplier;
                    break;
            }
        }

        private bool TryResolveTarget()
        {
            if (target != null)
            {
                return true;
            }

            if (Time.time < nextPlayerLookupAt)
            {
                return false;
            }

            nextPlayerLookupAt = Time.time + Mathf.Max(0.05f, playerLookupInterval);
            if (cachedPlayer == null)
            {
                cachedPlayer = FindFirstObjectByType<PlayerController>();
            }

            if (cachedPlayer != null)
            {
                target = cachedPlayer.transform;
                return true;
            }

            return false;
        }
    }
}
