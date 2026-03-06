using Ia.Core.Update;
using Ia.Gameplay.Actors;
using Ia.Systems.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace Plugins.IAFramework.Systems.Runtime.Interaction
{
    public abstract class IaBaseInteractable : IaBehaviour, IInteractable
    {
        public abstract Transform Transform { get; }
        public abstract int ExtraDistanceForInteraction { get; }
        public abstract string InteractionLabel { get; }
        
        [SerializeField] protected UnityEvent onInteracted;
        [SerializeField] protected UnityEvent onHovered;
        [SerializeField] protected UnityEvent onUnhovered;
        
        public abstract bool CanInteract(IaActor actor);

        public abstract void Interact(IaActor actor);
        
        public virtual void Hover(IaActor actor)
        {
            onHovered?.Invoke();
        }

        public virtual void Unhover(IaActor actor)
        {
            onUnhovered?.Invoke();
        }
    }
}