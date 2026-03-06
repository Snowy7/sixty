using Ia.Systems.Interaction;

namespace Ia.Core.Events
{
    public struct InteractionFocusChangedEvent
    {
        public IInteractable Interactable;

        public InteractionFocusChangedEvent(IInteractable interactable)
        {
            Interactable = interactable;
        }
    }
    
    public struct InteractionPromptUpdatedEvent
    {
        public IInteractable Interactable;

        public InteractionPromptUpdatedEvent(IInteractable interactable)
        {
            Interactable = interactable;
        }
    }

    public struct InteractionPerformedEvent
    {
        public IInteractable Interactable;

        public InteractionPerformedEvent(IInteractable interactable)
        {
            Interactable = interactable;
        }
    }
}