using UnityEngine;
using Ia.Core.Update;
using Ia.Core.Events;
using Ia.Core.Debugging;
using Ia.Gameplay.Actors;
using Ia.Gameplay.Characters;
using Ia.Systems.Interaction;

namespace Ia.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public class IaCharacterInteraction : IaBehaviour
    {
        [Header("Raycast")]
        [SerializeField] float maxDistance = 3f;
        [SerializeField] LayerMask interactionMask = ~0;

        [Header("References")]
        [SerializeField] Camera cameraRef;
        [SerializeField] IaActor actor;
        [SerializeField] IaCharacterState state;
        [SerializeField] IaPlayerInputDriver inputDriver;

        IInteractable m_currentFocus;
        string m_lastFocusPrompt;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void OnIaAwake()
        {
            if (actor == null)
                actor = GetComponent<IaActor>();

            if (state == null)
                state = GetComponent<IaCharacterState>();

            if (inputDriver == null)
                inputDriver = GetComponent<IaPlayerInputDriver>();

            if (cameraRef == null && Camera.main != null)
                cameraRef = Camera.main;
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (actor == null || cameraRef == null)
                return;

            if (state != null && state.IsInMenu)
                return;

            UpdateFocus();
            HandleInteractionInput();
        }

        void UpdateFocus()
        {
            IInteractable newFocus = null;

            Ray ray = cameraRef.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (
                Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    999999f,
                    interactionMask,
                    QueryTriggerInteraction.Collide
                )
            )
            {
                float distance = Vector3.Distance(actor.transform.position, hit.point);
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                
                if (distance > maxDistance + interactable.ExtraDistanceForInteraction)
                    return;
                
                newFocus = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (!ReferenceEquals(newFocus, m_currentFocus))
            {
                if (m_currentFocus != null)
                {
                    m_currentFocus.Unhover(actor);
                }
                
                m_currentFocus = newFocus;
                IaEventBus.Publish(
                    new InteractionFocusChangedEvent(m_currentFocus)
                );
                
                if (m_currentFocus != null)
                {
                    m_currentFocus.Hover(actor);
                }
            }
            
            if (m_currentFocus != null && m_lastFocusPrompt != m_currentFocus.InteractionLabel)
            {
                m_lastFocusPrompt = m_currentFocus.InteractionLabel;
                IaEventBus.Publish(
                    new InteractionPromptUpdatedEvent(m_currentFocus)
                );
            }
        }

        void HandleInteractionInput()
        {
            if (inputDriver == null)
                return;

            // Get interaction input from the centralized input driver
            IaInteractionInput interactionInput = inputDriver.GetInteractionInput();

            if (m_currentFocus == null)
                return;
            
            // Check if interact button was pressed
            if (!interactionInput.Interact)
                return;

            if (!m_currentFocus.CanInteract(actor))
                return;

            m_currentFocus.Interact(actor);
            IaEventBus.Publish(
                new InteractionPerformedEvent(m_currentFocus)
            );

            IaLogger.Info(
                IaLogCategory.Gameplay,
                $"{actor.DisplayName} interacted with {m_currentFocus.Transform.name}",
                this
            );
        }
    }
}