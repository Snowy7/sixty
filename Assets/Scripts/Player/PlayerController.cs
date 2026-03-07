using Sixty.Combat;
using Sixty.Core;
using Sixty.CameraSystem;
using Sixty.Gameplay;
using Ia.Core.Update;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sixty.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : IaBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float dashSpeed = 25f;
        [SerializeField] private float dashDuration = 0.15f;
        [SerializeField] private float dashCooldown = 2f;
        [SerializeField] private float dashIFrameDuration = 0.12f;
        [SerializeField] private bool lockVerticalPosition = false;
        [SerializeField] private float aimSmoothing = 18f;
        [SerializeField] private float controllerAimDeadzone = 0.2f;
        [SerializeField] private float controllerAimPrioritySeconds = 0.35f;

        [Header("Aim Assist")]
        [SerializeField] private bool enableAimAssist = true;
        [SerializeField] private float aimAssistRadius = 24f;
        [SerializeField] private float mouseAimAssistAngle = 5f;
        [SerializeField] private float controllerAimAssistAngle = 14f;
        [SerializeField] [Range(0f, 1f)] private float mouseAimAssistStrength = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float controllerAimAssistStrength = 0.72f;
        [SerializeField] private float stickyAimBonusAngle = 5f;
        [SerializeField] private float stickyAimDuration = 0.22f;
        [SerializeField] private LayerMask aimAssistLayerMask = ~0;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string pointerActionName = "Point";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string dashActionName = "Jump";

        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private WeaponController weaponController;

        public bool IsInvulnerable => invulnerabilityTimer > 0f;
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        private Rigidbody body;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction pointerAction;
        private InputAction attackAction;
        private InputAction dashAction;

        private Vector2 moveInput;
        private float dashTimer;
        private float dashCooldownTimer;
        private float invulnerabilityTimer;
        private Vector3 dashDirection = Vector3.forward;
        private float controllerAimLastUsedAt = -999f;
        private float lockedY;
        private bool controllerAimActive;
        private Transform stickyTarget;
        private float stickyTargetExpiresAt;
        private float externalMoveSpeedMultiplier = 1f;
        private readonly Collider[] aimAssistBuffer = new Collider[96];
        private bool combatInputLocked;
        private TopDownCameraFollow aimCameraFollow;

        private bool IsDashing => dashTimer > 0f;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update | IaUpdatePhase.FixedUpdate;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            if (body.interpolation == RigidbodyInterpolation.None)
            {
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            lockedY = transform.position.y;
        }

        protected override void OnIaEnable()
        {
            BindActions();
            playerMap?.Enable();

            if (dashAction != null)
            {
                dashAction.performed += OnDashPerformed;
            }
        }

        protected override void OnIaDisable()
        {
            if (dashAction != null)
            {
                dashAction.performed -= OnDashPerformed;
            }

            playerMap?.Disable();
        }

        public override void OnIaUpdate(float deltaTime)
        {
            moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= deltaTime;
            }

            if (dashTimer > 0f)
            {
                dashTimer -= deltaTime;
            }

            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= deltaTime;
            }

            UpdateAimDirection(deltaTime);

            bool isAttacking = attackAction != null && attackAction.IsPressed();
            if (!isAttacking && Gamepad.current != null)
            {
                isAttacking = Gamepad.current.rightTrigger.ReadValue() > 0.35f;
            }

            if (combatInputLocked)
            {
                return;
            }

            if (isAttacking && weaponController != null)
            {
                if (!weaponController.CanFireNow)
                {
                    return;
                }

                Vector3 fireDirection = ResolveFireDirection(AimDirection, controllerAimActive);
                weaponController.TryFire(fireDirection);
            }
        }

        public override void OnIaFixedUpdate(float fixedDeltaTime)
        {
            Vector3 desiredDirection;
            float desiredSpeed;

            if (IsDashing)
            {
                desiredDirection = dashDirection;
                desiredSpeed = dashSpeed;
            }
            else
            {
                desiredDirection = new Vector3(moveInput.x, 0f, moveInput.y);
                if (desiredDirection.sqrMagnitude > 1f)
                {
                    desiredDirection.Normalize();
                }

                desiredSpeed = moveSpeed;
            }

            desiredSpeed *= Mathf.Max(0.1f, externalMoveSpeedMultiplier);
            Vector3 nextPosition = body.position + (desiredDirection * desiredSpeed * fixedDeltaTime);
            if (lockVerticalPosition)
            {
                nextPosition.y = lockedY;
            }

            body.MovePosition(nextPosition);
        }

        public bool TryTakeTimeDamage(float seconds = 2f, bool playDamageFeedback = true)
        {
            if (IsInvulnerable)
            {
                return false;
            }

            TimeManager.Instance?.TakeDamage(seconds);
            if (playDamageFeedback)
            {
                GameFeelController.Instance?.OnPlayerDamaged(transform);
            }

            return true;
        }

        public void SetExternalMoveSpeedMultiplier(float multiplier)
        {
            externalMoveSpeedMultiplier = Mathf.Clamp(multiplier, 0.2f, 4f);
        }

        public void RefreshDashCooldown()
        {
            dashCooldownTimer = 0f;
        }

        public void SetCombatInputLocked(bool locked)
        {
            combatInputLocked = locked;
        }

        private void BindActions()
        {
            if (inputActions == null)
            {
                Debug.LogError("PlayerController requires an InputActionAsset reference.", this);
                return;
            }

            playerMap = inputActions.FindActionMap(actionMapName, true);
            moveAction = playerMap.FindAction(moveActionName, true);
            lookAction = playerMap.FindAction(lookActionName, false);
            pointerAction = inputActions.FindAction($"UI/{pointerActionName}", false);
            attackAction = playerMap.FindAction(attackActionName, true);
            dashAction = playerMap.FindAction(dashActionName, true);
        }

        private void OnDashPerformed(InputAction.CallbackContext context)
        {
            if (combatInputLocked)
            {
                return;
            }

            if (dashCooldownTimer > 0f || IsDashing)
            {
                return;
            }

            Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                dashDirection = moveDirection.normalized;
            }
            else
            {
                dashDirection = AimDirection.sqrMagnitude > 0.0001f ? AimDirection.normalized : transform.forward;
            }

            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            invulnerabilityTimer = Mathf.Max(invulnerabilityTimer, dashIFrameDuration);
            GameFeelController.Instance?.OnPlayerDash(transform.position, dashDirection);
        }

        private void UpdateAimDirection(float deltaTime)
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            Vector3 resolvedAim = AimDirection;
            bool usedControllerAim = false;

            Vector2 gamepadAim = Vector2.zero;
            if (Gamepad.current != null)
            {
                gamepadAim = Gamepad.current.rightStick.ReadValue();
            }

            if (gamepadAim.sqrMagnitude >= controllerAimDeadzone * controllerAimDeadzone)
            {
                resolvedAim = new Vector3(gamepadAim.x, 0f, gamepadAim.y).normalized;
                usedControllerAim = true;
                controllerAimLastUsedAt = Time.unscaledTime;
            }

            bool controllerHasRecentPriority = (Time.unscaledTime - controllerAimLastUsedAt) <= controllerAimPrioritySeconds;
            controllerAimActive = usedControllerAim || controllerHasRecentPriority;

            bool usedMouseAim = false;
            if (!usedControllerAim && !controllerHasRecentPriority && aimCamera != null)
            {
                Vector2 mouseScreenPosition = ReadPointerScreenPosition();
                if (TryResolveMouseAim(mouseScreenPosition, deltaTime, out Vector3 mouseAim))
                {
                    resolvedAim = mouseAim;
                    usedMouseAim = true;
                    controllerAimLastUsedAt = -999f;
                }
            }

            if (!usedControllerAim && !usedMouseAim)
            {
                Vector2 lookInput = ReadNonPointerLookInput();
                if (lookInput.sqrMagnitude > controllerAimDeadzone * controllerAimDeadzone)
                {
                    Vector3 fallbackAim = new Vector3(lookInput.x, 0f, lookInput.y);
                    resolvedAim = fallbackAim.normalized;
                }
            }

            if (resolvedAim.sqrMagnitude > 0.0001f)
            {
                if (controllerAimActive)
                {
                    AimDirection = Vector3.Slerp(AimDirection, resolvedAim.normalized, aimSmoothing * deltaTime).normalized;
                }
                else
                {
                    AimDirection = resolvedAim.normalized;
                }
            }

            Transform targetPivot = aimPivot != null ? aimPivot : transform;
            targetPivot.rotation = Quaternion.LookRotation(AimDirection, Vector3.up);
        }

        private Vector3 GetPredictedAimOrigin(float deltaTime)
        {
            Vector3 origin = body != null ? body.position : transform.position;

            if (deltaTime <= 0f)
            {
                return origin;
            }

            Vector3 moveDirection;
            float desiredSpeed;

            if (IsDashing)
            {
                moveDirection = dashDirection;
                desiredSpeed = dashSpeed;
            }
            else
            {
                moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
                if (moveDirection.sqrMagnitude > 1f)
                {
                    moveDirection.Normalize();
                }

                desiredSpeed = moveSpeed;
            }

            desiredSpeed *= Mathf.Max(0.1f, externalMoveSpeedMultiplier);
            origin += moveDirection * desiredSpeed * deltaTime;
            if (lockVerticalPosition)
            {
                origin.y = lockedY;
            }

            return origin;
        }

        private bool TryResolveMouseAim(Vector2 screenPosition, float deltaTime, out Vector3 aimDirection)
        {
            aimDirection = AimDirection;

            if (aimCamera == null)
            {
                return false;
            }

            Ray mouseRay = BuildMouseAimRay(screenPosition);
            Vector3 aimOrigin = GetPredictedAimOrigin(deltaTime);
            Plane movementPlane = new Plane(Vector3.up, aimOrigin);

            if (!movementPlane.Raycast(mouseRay, out float hitDistance))
            {
                return false;
            }

            Vector3 hitPoint = mouseRay.GetPoint(hitDistance);
            Vector3 toPoint = hitPoint - aimOrigin;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            aimDirection = toPoint.normalized;
            return true;
        }

        private Vector2 ReadPointerScreenPosition()
        {
            if (pointerAction != null)
            {
                Vector2 actionPosition = pointerAction.ReadValue<Vector2>();
                if (actionPosition.sqrMagnitude > 0.001f)
                {
                    return actionPosition;
                }
            }

            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Vector2.zero;
        }

        private Vector2 ReadNonPointerLookInput()
        {
            if (lookAction == null)
            {
                return Vector2.zero;
            }

            InputControl activeControl = lookAction.activeControl;
            if (activeControl != null)
            {
                InputDevice device = activeControl.device;
                if (device is Mouse || device is Pointer)
                {
                    return Vector2.zero;
                }
            }

            Vector2 lookInput = lookAction.ReadValue<Vector2>();
            return lookInput;
        }

        private Ray BuildMouseAimRay(Vector2 screenPosition)
        {
            Vector3 cameraPosition = aimCamera.transform.position;
            Quaternion cameraRotation = aimCamera.transform.rotation;

            if (aimCameraFollow == null || aimCameraFollow.gameObject != aimCamera.gameObject)
            {
                aimCameraFollow = aimCamera.GetComponent<TopDownCameraFollow>();
            }

            if (aimCameraFollow != null &&
                aimCameraFollow.isActiveAndEnabled &&
                aimCameraFollow.TryGetPredictedPose(Time.deltaTime, out Vector3 predictedPosition, out Quaternion predictedRotation))
            {
                cameraPosition = predictedPosition;
                cameraRotation = predictedRotation;
            }

            Rect pixelRect = aimCamera.pixelRect;
            float viewportX = pixelRect.width > 0.001f ? (screenPosition.x - pixelRect.x) / pixelRect.width : 0.5f;
            float viewportY = pixelRect.height > 0.001f ? (screenPosition.y - pixelRect.y) / pixelRect.height : 0.5f;
            viewportX = Mathf.Clamp01(viewportX);
            viewportY = Mathf.Clamp01(viewportY);

            if (aimCamera.orthographic)
            {
                float halfHeight = aimCamera.orthographicSize;
                float halfWidth = halfHeight * aimCamera.aspect;
                Vector3 localOrigin = new Vector3(
                    (viewportX - 0.5f) * 2f * halfWidth,
                    (viewportY - 0.5f) * 2f * halfHeight,
                    aimCamera.nearClipPlane);

                return new Ray(
                    cameraPosition + (cameraRotation * localOrigin),
                    cameraRotation * Vector3.forward);
            }

            float nearClip = Mathf.Max(aimCamera.nearClipPlane, 0.01f);
            float halfNearHeight = Mathf.Tan(aimCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * nearClip;
            float halfNearWidth = halfNearHeight * aimCamera.aspect;
            Vector3 localPoint = new Vector3(
                (viewportX - 0.5f) * 2f * halfNearWidth,
                (viewportY - 0.5f) * 2f * halfNearHeight,
                nearClip);
            Vector3 worldDirection = (cameraRotation * localPoint).normalized;
            Vector3 worldOrigin = cameraPosition + (cameraRotation * localPoint);
            return new Ray(worldOrigin, worldDirection);
        }

        private Vector3 ResolveFireDirection(Vector3 rawDirection, bool usingControllerAim)
        {
            Vector3 flatRaw = rawDirection;
            flatRaw.y = 0f;
            if (flatRaw.sqrMagnitude <= 0.0001f)
            {
                flatRaw = AimDirection;
                flatRaw.y = 0f;
            }

            if (flatRaw.sqrMagnitude <= 0.0001f)
            {
                return transform.forward;
            }

            flatRaw.Normalize();

            if (!enableAimAssist || aimAssistRadius <= 0.1f)
            {
                return flatRaw;
            }

            float allowedAngle = usingControllerAim ? controllerAimAssistAngle : mouseAimAssistAngle;
            float assistStrength = usingControllerAim ? controllerAimAssistStrength : mouseAimAssistStrength;

            Transform bestTarget = null;
            Vector3 bestDirection = flatRaw;
            float bestScore = float.MaxValue;

            int found = Physics.OverlapSphereNonAlloc(transform.position, aimAssistRadius, aimAssistBuffer, aimAssistLayerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                Collider col = aimAssistBuffer[i];
                if (col == null)
                {
                    continue;
                }

                Health health = col.GetComponentInParent<Health>();
                if (health == null || health.IsDead)
                {
                    continue;
                }

                Transform enemyRoot = health.transform.root != null ? health.transform.root : health.transform;
                if (enemyRoot == transform.root)
                {
                    continue;
                }

                Vector3 toTarget = enemyRoot.position - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance <= 0.01f)
                {
                    continue;
                }

                Vector3 toTargetDir = toTarget / distance;
                float angle = Vector3.Angle(flatRaw, toTargetDir);
                float maxAngle = allowedAngle;
                if (enemyRoot == stickyTarget && Time.unscaledTime <= stickyTargetExpiresAt)
                {
                    maxAngle += stickyAimBonusAngle;
                }

                if (angle > maxAngle)
                {
                    continue;
                }

                float score = angle + (distance * 0.085f);
                if (enemyRoot == stickyTarget)
                {
                    score -= 2.2f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = enemyRoot;
                    bestDirection = toTargetDir;
                }
            }

            if (bestTarget != null)
            {
                stickyTarget = bestTarget;
                stickyTargetExpiresAt = Time.unscaledTime + stickyAimDuration;
                return Vector3.Slerp(flatRaw, bestDirection, Mathf.Clamp01(assistStrength)).normalized;
            }

            if (stickyTarget != null && Time.unscaledTime > stickyTargetExpiresAt)
            {
                stickyTarget = null;
            }

            return flatRaw;
        }
    }
}
