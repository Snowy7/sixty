using System;
using UnityEngine;
using Ia.Core.Update;
using Ia.Core.Debugging;
using Ia.Core.Events;

namespace Ia.Gameplay.Characters
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class IaCharacterMovementController : IaBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float crouchSpeed = 2f;

        [Header("Rotation")]
        [SerializeField] float rotationSpeed = 720f;
        [SerializeField] IaCharacterRotationMode rotationMode =
            IaCharacterRotationMode.YawFromCamera;

        [Header("Jumping & Gravity")]
        [SerializeField] float jumpHeight = 2f;
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float groundCheckRadius = 0.3f;
        [SerializeField] LayerMask groundMask = -1;
        [SerializeField] Transform groundCheck;

        [Header("Camera Alignment")]
        [SerializeField] bool useCameraForward = true;
        [SerializeField] Transform cameraTransform;

        [Header("Crouch Settings")]
        [SerializeField] float standingHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.2f;
        [SerializeField] float heightLerpSpeed = 10f;
        [SerializeField] float standUpCheckRadius = 0.4f;
        [SerializeField] float standUpCheckDistance = 0.6f;

        CharacterController m_controller;
        IaCharacterState m_state;

        Vector3 m_velocity; // full velocity we own: x/z = horiz, y = vertical
        bool m_isGrounded;
        bool m_wasGrounded;
        bool m_hasJumpRequested;

        bool m_movementLocked;
        float m_targetHeight;

        public bool IsGrounded => m_isGrounded;
        public Vector3 Velocity => m_velocity;
        public bool IsMoving => m_velocity.x != 0f || m_velocity.z != 0f;

        public float MaxSpeed => sprintSpeed;
        public float SprintSpeed => sprintSpeed;
        public float WalkSpeed => walkSpeed;
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void OnIaAwake()
        {
            m_controller = GetComponent<CharacterController>();
            m_state = GetComponent<IaCharacterState>();

            /*if (groundCheck == null)
            {
                var gc = new GameObject("GroundCheck");
                gc.transform.SetParent(transform);
                gc.transform.localPosition = Vector3.zero;
                groundCheck = gc.transform;
            }*/

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (m_controller != null)
            {
                if (standingHeight <= 0f)
                    standingHeight = m_controller.height;

                m_targetHeight = standingHeight;
            }
        }

        public void LockMovement(bool locked)
        {
            m_movementLocked = locked;
        }

        public void SetCamera(Transform newCameraTransform)
        {
            cameraTransform = newCameraTransform;
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (m_controller == null || !m_controller.enabled)
                return;

            HandleGroundCheck();

            // NOTE: We handle only vertical & crouch each frame here;
            // horizontal component is from ApplyInput
            if (!m_movementLocked && (m_state == null || !m_state.IsInMenu))
            {
                HandleJumpAndGravity(deltaTime);
            }
            else
            {
                HandleJumpAndGravity(deltaTime, applyHorizontal: false);
            }

            UpdateCrouchHeight(deltaTime);
        }

        public void ApplyInput(IaCharacterInput input, float deltaTime)
        {
            if (m_movementLocked)
                return;

            m_hasJumpRequested = input.Jump;

            bool sprintRequested = input.Sprint;
            bool crouchRequested = input.Crouch;

            if (m_state != null)
            {
                m_state.SetSprinting(sprintRequested && !crouchRequested && m_isGrounded && input.Move.sqrMagnitude > 0f);

                if (!crouchRequested && m_state.IsCrouching)
                {
                    if (CanStandUp())
                        m_state.SetCrouching(false);
                    else
                        crouchRequested = true;
                }
                else if (crouchRequested && !m_state.IsCrouching)
                {
                    m_state.SetCrouching(true);
                }
            }

            float speed = walkSpeed;
            bool isCrouching = m_state != null && m_state.IsCrouching;
            bool isSprinting = m_state != null && m_state.IsSprinting;

            if (isSprinting && !isCrouching)
                speed = sprintSpeed;
            if (isCrouching)
                speed = crouchSpeed;

            Vector3 moveDir = GetWorldMoveDirection(input.Move);
            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            // Apply rotation based on mode
            HandleRotation(moveDir, deltaTime);

            // Horizontal velocity
            Vector3 horizontalVelocity = moveDir * speed;

            // Merge with existing vertical velocity
            m_velocity.x = horizontalVelocity.x;
            m_velocity.z = horizontalVelocity.z;

            // Move using our velocity
            Vector3 frameDisplacement = new Vector3(
                m_velocity.x,
                0f, // vertical handled in gravity step
                m_velocity.z
            ) * deltaTime;

            m_controller.Move(frameDisplacement);
        }

        Vector3 GetWorldMoveDirection(Vector2 move)
        {
            Vector3 raw = new Vector3(move.x, 0f, move.y);

            if (!useCameraForward || cameraTransform == null)
                return raw;

            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            return camForward * move.y + camRight * move.x;
        }

        void HandleRotation(Vector3 moveDir, float deltaTime)
        {
            if (rotationMode == IaCharacterRotationMode.OrientToMovement)
            {
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRot,
                        rotationSpeed * deltaTime
                    );
                }
            }
            else if (rotationMode == IaCharacterRotationMode.YawFromCamera)
            {
                if (cameraTransform == null)
                    return;

                Vector3 fwd = cameraTransform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(fwd);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRot,
                        rotationSpeed * deltaTime
                    );
                }
            }
        }

        void HandleGroundCheck()
        {
            if (groundCheck != null)
            {
                m_wasGrounded = m_isGrounded;
                m_isGrounded = Physics.CheckSphere(
                    groundCheck.position,
                    groundCheckRadius,
                    groundMask
                );
            }
            else
            {
                m_wasGrounded = m_isGrounded;
                m_isGrounded = m_controller.isGrounded;
            }

            if (m_isGrounded && m_velocity.y < 0f)
            {
                if (!m_wasGrounded)
                {
                    IaEventBus.Publish<OnPlayerLanded>(new OnPlayerLanded());
                }
                m_velocity.y = -2f;
            }
        }

        void HandleJumpAndGravity(float deltaTime, bool applyHorizontal = true)
        {
            if (m_isGrounded && m_hasJumpRequested)
            {
                m_velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                IaEventBus.Publish<OnPlayerJumped>(new OnPlayerJumped());
            }

            // gravity
            m_velocity.y += gravity * deltaTime;

            // vertical displacement
            Vector3 verticalDisplacement = new Vector3(
                0f,
                m_velocity.y,
                0f
            ) * deltaTime;

            m_controller.Move(verticalDisplacement);
        }

        void UpdateCrouchHeight(float deltaTime)
        {
            if (m_controller == null)
                return;

            bool crouching = m_state != null && m_state.IsCrouching;
            float target = crouching ? crouchHeight : standingHeight;

            if (!crouching && !CanStandUp())
            {
                target = crouchHeight;
                if (m_state != null && !m_state.IsCrouching)
                    m_state.SetCrouching(true);
            }

            m_targetHeight = target;

            float newHeight = Mathf.Lerp(
                m_controller.height,
                m_targetHeight,
                heightLerpSpeed * deltaTime
            );

            float heightDelta = newHeight - m_controller.height;

            m_controller.height = newHeight;
            m_controller.center += new Vector3(0f, heightDelta * 0.5f, 0f);
        }

        bool CanStandUp()
        {
            if (m_controller == null)
                return true;

            Vector3 origin =
                transform.position + Vector3.up * m_controller.height;
            float checkDistance =
                standingHeight - m_controller.height + standUpCheckDistance;
            if (checkDistance <= 0f)
                return true;

            return !Physics.SphereCast(
                origin,
                standUpCheckRadius,
                Vector3.up,
                out _,
                checkDistance,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
        }

        public Vector2 GetVelocity2D()
        {
            return new Vector2(m_velocity.x, m_velocity.z);
        }
    }
}