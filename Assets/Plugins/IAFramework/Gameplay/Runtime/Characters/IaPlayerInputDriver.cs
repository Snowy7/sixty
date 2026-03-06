using UnityEngine;
using UnityEngine.InputSystem;
using Ia.Core.Update;

namespace Ia.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IaCharacterMovementController))]
    [RequireComponent(typeof(IaCharacterState))]
    public class IaPlayerInputDriver : IaBehaviour, IIaCharacterInputSource
    {
        [Header("Input Actions")]
        [Tooltip("Input Actions Asset (e.g., PlayerInputActions)")]
        [SerializeField] private InputActionAsset inputActionsAsset;

        [Header("Action References")]
        [Tooltip("Move action (Vector2)")]
        [SerializeField] private InputActionReference moveAction;
        [Tooltip("Jump action")]
        [SerializeField] private InputActionReference jumpAction;
        [Tooltip("Sprint action")]
        [SerializeField] private InputActionReference sprintAction;
        [Tooltip("Crouch action")]
        [SerializeField] private InputActionReference crouchAction;
        [Tooltip("Aim action")]
        [SerializeField] private InputActionReference aimAction;
        
        [Tooltip("Primary Fire action")]
        [SerializeField] private InputActionReference primaryFireAction;
        [Tooltip("Secondary Fire action")]
        [SerializeField] private InputActionReference secondaryFireAction;
        [Tooltip("Reload action")]
        [SerializeField] private InputActionReference reloadAction;
        [Tooltip("Interact action")]
        [SerializeField] private InputActionReference interactAction;
        [Tooltip("Look action (Vector2)")]
        [SerializeField] private InputActionReference lookAction;
        
        [Tooltip("Previous item action")]
        [SerializeField] private InputActionReference previousAction;
        [Tooltip("Next item action")]
        [SerializeField] private InputActionReference nextAction;
        [Tooltip("Scroll wheel action (Vector2) - typically from UI map")]
        [SerializeField] private InputActionReference scrollWheelAction;

        [Header("Legacy Input (Fallback)")]
        [Tooltip("Use legacy Input.GetAxis if Input System is not available")]
        [SerializeField] private bool useLegacyInput = false;
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private string jumpButton = "Jump";
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;
        [SerializeField] private KeyCode primaryFireKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode secondaryFireKey = KeyCode.Mouse1;
        [SerializeField] private KeyCode reloadKey = KeyCode.R;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode previousItemKey = KeyCode.Q;
        [SerializeField] private KeyCode nextItemKey = KeyCode.E;

        private IaCharacterMovementController m_movement;
        private IaCharacterState m_state;
        private bool m_locked = false;

        // Input System state variables
        private Vector2 m_moveValue = Vector2.zero;
        private bool m_jumpPressed = false;
        private bool m_aimPressed = false;
        private bool m_primaryFirePressed = false;
        private bool m_secondaryFirePressed = false;
        private bool m_reloadPressed = false;
        private bool m_interactPressed = false;
        private Vector2 m_lookValue = Vector2.zero;
        private bool m_previousPressed = false;
        private bool m_nextPressed = false;
        private Vector2 m_scrollDelta = Vector2.zero;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void OnIaAwake()
        {
            m_movement = GetComponent<IaCharacterMovementController>();
            m_state = GetComponent<IaCharacterState>();
        }

        protected override void OnIaStart()
        {
            SetupInputActions();
        }

        protected override void OnIaEnable()
        {
            if (inputActionsAsset != null)
            {
                inputActionsAsset.Enable();
            }
        }

        protected override void OnIaDisable()
        {
            if (inputActionsAsset != null)
            {
                inputActionsAsset.Disable();
            }
        }

        private void SetupInputActions()
        {
            if (inputActionsAsset == null)
            {
                Debug.LogWarning($"[{name}] No InputActionAsset assigned. Using legacy input system.", this);
                return;
            }

            // Enable the input actions asset
            inputActionsAsset.Enable();

            // Setup move action if available
            if (moveAction != null)
            {
                moveAction.action.performed += ctx => m_moveValue = ctx.ReadValue<Vector2>();
                moveAction.action.canceled += ctx => m_moveValue = Vector2.zero;
            }

            // Setup other actions if available
            if (jumpAction != null)
            {
                jumpAction.action.performed += ctx => m_jumpPressed = true;
            }

            if (sprintAction != null)
            {
                sprintAction.action.Enable();
            }

            if (crouchAction != null)
            {
                crouchAction.action.Enable();
            }

            if (aimAction != null)
            {
                aimAction.action.performed += ctx => m_aimPressed = true;
                aimAction.action.canceled += ctx => m_aimPressed = false;
            }

            // Setup combat actions
            if (primaryFireAction != null)
            {
                primaryFireAction.action.performed += ctx => m_primaryFirePressed = true;
                primaryFireAction.action.canceled += ctx => m_primaryFirePressed = false;
            }

            if (secondaryFireAction != null)
            {
                secondaryFireAction.action.performed += ctx => m_secondaryFirePressed = true;
            }
            
            if (reloadAction != null)
            {
                reloadAction.action.performed += ctx => m_reloadPressed = true;
            }

            // Setup interaction action
            if (interactAction != null)
            {
                interactAction.action.performed += ctx => m_interactPressed = true;
            }

            // Setup look action for camera
            if (lookAction != null)
            {
                lookAction.action.performed += ctx => m_lookValue = ctx.ReadValue<Vector2>();
                lookAction.action.canceled += ctx => m_lookValue = Vector2.zero;
            }

            // Setup inventory actions
            if (previousAction != null)
            {
                previousAction.action.performed += ctx => m_previousPressed = true;
            }

            if (nextAction != null)
            {
                nextAction.action.performed += ctx => m_nextPressed = true;
            }

            if (scrollWheelAction != null)
            {
                scrollWheelAction.action.performed += ctx => m_scrollDelta = ctx.ReadValue<Vector2>();
                scrollWheelAction.action.canceled += ctx => m_scrollDelta = Vector2.zero;
            }
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (m_locked)
                return;
            
            if (m_state != null && m_state.IsInMenu)
                return;

            // Get movement input and apply to movement controller
            IaCharacterInput movementInput = GetMovementInput();
            m_movement.ApplyInput(movementInput, deltaTime);

            // Handle high-level state bits (aim)
            if (m_state != null)
            {
                IaAimInput aimInput = GetAimInput();
                m_state.SetMode(
                    aimInput.Aim ? IaCharacterMode.Aiming : IaCharacterMode.Normal
                );
            }
        }

        // Interface implementation for IIaCharacterInputSource - returns movement input
        public IaCharacterInput GetInput()
        {
            return GetMovementInput();
        }

        public IaCharacterInput GetMovementInput(bool reset = true)
        {
            return ReadMovementInput(reset);
        }

        public IaCombatInput GetCombatInput()
        {
            return ReadCombatInput();
        }

        public IaInteractionInput GetInteractionInput()
        {
            return ReadInteractionInput();
        }

        public IaAimInput GetAimInput()
        {
            return ReadAimInput();
        }

        public IaInventoryInput GetInventoryInput()
        {
            return ReadInventoryInput();
        }

        public Vector2 GetMouseInput()
        {
            // Try Input System first
            if (inputActionsAsset != null && !useLegacyInput)
            {
                // Read look input
                if (lookAction != null)
                {
                    return m_lookValue;
                }
                return Vector2.zero;
            }
            else
            {
                // Fallback to legacy Input system
                return new Vector2(
                    Input.GetAxisRaw("Mouse X"),
                    Input.GetAxisRaw("Mouse Y")
                );
            }
        }
        
        public void LockInput(bool locked)
        {
            m_locked = locked;
        }

        private IaCharacterInput ReadMovementInput(bool reset = true)
        {
            Vector2 move = Vector2.zero;
            bool jump = false;
            bool sprint = false;
            bool crouch = false;

            // Try Input System first
            if (inputActionsAsset != null && !useLegacyInput)
            {
                // Read move input
                if (moveAction != null)
                {
                    move = m_moveValue;
                }

                // Read jump input (performed triggers once)
                if (jumpAction != null)
                {
                    jump = m_jumpPressed;
                    if (reset) m_jumpPressed = false; // Reset for next frame
                }

                // Read sprint input
                if (sprintAction != null)
                {
                    sprint = sprintAction.action.IsPressed();
                }

                // Read crouch input
                if (crouchAction != null)
                {
                    crouch = crouchAction.action.IsPressed();
                }
            }
            else
            {
                // Fallback to legacy Input system
                move.x = Input.GetAxisRaw(horizontalAxis);
                move.y = Input.GetAxisRaw(verticalAxis);
                jump = Input.GetButtonDown(jumpButton);
                sprint = Input.GetKey(sprintKey);
                crouch = Input.GetKey(crouchKey);
            }

            return new IaCharacterInput
            {
                Move = move,
                Jump = jump,
                Sprint = sprint,
                Crouch = crouch
            };
        }

        private IaCombatInput ReadCombatInput()
        {
            bool primaryFire = false;
            bool secondaryFire = false;
            bool reload = false;

            // Try Input System first
            if (inputActionsAsset != null && !useLegacyInput)
            {
                // Read combat inputs
                if (primaryFireAction != null)
                {
                    primaryFire = primaryFireAction.action.IsPressed();
                }
                else
                {
                    primaryFire = m_primaryFirePressed;
                }

                if (secondaryFireAction != null)
                {
                    secondaryFire = m_secondaryFirePressed;
                    m_secondaryFirePressed = false; // Reset for next frame
                }
                
                reload = m_reloadPressed;
                m_reloadPressed = false; // Reset for next frame
            }
            else
            {
                // Fallback to legacy Input system
                primaryFire = Input.GetKey(primaryFireKey);
                secondaryFire = Input.GetKeyDown(secondaryFireKey);
                reload = Input.GetKeyDown(reloadKey);
            }

            return new IaCombatInput
            {
                PrimaryFire = primaryFire,
                SecondaryFire = secondaryFire,
                Reload = reload
            };
        }

        private IaInteractionInput ReadInteractionInput()
        {
            bool interact = false;

            // Try Input System first
            if (inputActionsAsset != null && !useLegacyInput)
            {
                // Read interaction input
                if (interactAction != null)
                {
                    interact = m_interactPressed;
                    m_interactPressed = false; // Reset for next frame
                }
            }
            else
            {
                // Fallback to legacy Input system
                interact = Input.GetKeyDown(interactKey);
            }

            return new IaInteractionInput
            {
                Interact = interact
            };
        }

        private IaAimInput ReadAimInput()
        {
            bool aim = false;

            // Try Input System first
            if (inputActionsAsset != null && !useLegacyInput)
            {
                if (aimAction != null)
                {
                    aim = aimAction.action.IsPressed();
                }
                else
                {
                    aim = m_aimPressed;
                }
            }
            else
            {
                aim = Input.GetKey(aimKey);
            }

            return new IaAimInput
            {
                Aim = aim
            };
        }

        private IaInventoryInput ReadInventoryInput()
        {
            bool previous = false;
            bool next = false;
            Vector2 scrollDelta = Vector2.zero;

            // Try Input System first
            if (inputActionsAsset != null && !useLegacyInput)
            {
                // Read previous input
                if (previousAction != null)
                {
                    previous = m_previousPressed;
                    m_previousPressed = false; // Reset for next frame
                }

                // Read next input
                if (nextAction != null)
                {
                    next = m_nextPressed;
                    m_nextPressed = false; // Reset for next frame
                }

                // Read scroll wheel
                if (scrollWheelAction != null)
                {
                    scrollDelta = m_scrollDelta;
                    // Don't reset scroll delta immediately - let it accumulate or reset in next frame
                    // We'll reset it after reading
                }
            }
            else
            {
                // Fallback to legacy Input system
                previous = Input.GetKeyDown(previousItemKey);
                next = Input.GetKeyDown(nextItemKey);
                scrollDelta = Input.mouseScrollDelta;
            }

            // Reset scroll delta after reading
            Vector2 resultScroll = scrollDelta;
            m_scrollDelta = Vector2.zero;

            return new IaInventoryInput
            {
                Previous = previous,
                Next = next,
                ScrollDelta = resultScroll
            };
        }
    }
}
