using Sixty.Combat;
using Sixty.Core;
using Sixty.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sixty.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
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
        private readonly Collider[] aimAssistBuffer = new Collider[96];

        private bool IsDashing => dashTimer > 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            lockedY = transform.position.y;
        }

        private void OnEnable()
        {
            BindActions();
            playerMap?.Enable();

            if (dashAction != null)
            {
                dashAction.performed += OnDashPerformed;
            }
        }

        private void OnDisable()
        {
            if (dashAction != null)
            {
                dashAction.performed -= OnDashPerformed;
            }

            playerMap?.Disable();
        }

        private void Update()
        {
            moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= Time.deltaTime;
            }

            if (dashTimer > 0f)
            {
                dashTimer -= Time.deltaTime;
            }

            if (invulnerabilityTimer > 0f)
            {
                invulnerabilityTimer -= Time.deltaTime;
            }

            UpdateAimDirection();

            bool isAttacking = attackAction != null && attackAction.IsPressed();
            if (!isAttacking && Gamepad.current != null)
            {
                isAttacking = Gamepad.current.rightTrigger.ReadValue() > 0.35f;
            }

            if (isAttacking && weaponController != null)
            {
                Vector3 fireDirection = ResolveFireDirection(AimDirection, controllerAimActive);
                weaponController.TryFire(fireDirection);
            }
        }

        private void FixedUpdate()
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

            Vector3 nextPosition = body.position + (desiredDirection * desiredSpeed * Time.fixedDeltaTime);
            if (lockVerticalPosition)
            {
                nextPosition.y = lockedY;
            }

            body.MovePosition(nextPosition);
        }

        public bool TryTakeTimeDamage(float seconds = 2f)
        {
            if (IsInvulnerable)
            {
                return false;
            }

            TimeManager.Instance?.TakeDamage(seconds);
            GameFeelController.Instance?.OnPlayerDamaged(transform);
            return true;
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
            attackAction = playerMap.FindAction(attackActionName, true);
            dashAction = playerMap.FindAction(dashActionName, true);
        }

        private void OnDashPerformed(InputAction.CallbackContext context)
        {
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

        private void UpdateAimDirection()
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

            if (!usedControllerAim && !controllerHasRecentPriority && Mouse.current != null && aimCamera != null)
            {
                Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
                Ray mouseRay = aimCamera.ScreenPointToRay(mouseScreenPosition);
                Plane movementPlane = new Plane(Vector3.up, transform.position);

                if (movementPlane.Raycast(mouseRay, out float hitDistance))
                {
                    Vector3 hitPoint = mouseRay.GetPoint(hitDistance);
                    Vector3 toPoint = hitPoint - transform.position;
                    toPoint.y = 0f;

                    if (toPoint.sqrMagnitude > 0.0001f)
                    {
                        resolvedAim = toPoint.normalized;
                        if (Mouse.current.delta.ReadValue().sqrMagnitude > 0.001f)
                        {
                            controllerAimLastUsedAt = -999f;
                        }
                    }
                }
            }

            if (!usedControllerAim && lookAction != null)
            {
                Vector2 lookInput = lookAction.ReadValue<Vector2>();
                Vector3 fallbackAim = new Vector3(lookInput.x, 0f, lookInput.y);
                if (fallbackAim.sqrMagnitude > 0.05f)
                {
                    resolvedAim = fallbackAim.normalized;
                }
            }

            if (resolvedAim.sqrMagnitude > 0.0001f)
            {
                AimDirection = Vector3.Slerp(AimDirection, resolvedAim.normalized, aimSmoothing * Time.deltaTime).normalized;
            }

            Transform targetPivot = aimPivot != null ? aimPivot : transform;
            targetPivot.rotation = Quaternion.LookRotation(AimDirection, Vector3.up);
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
