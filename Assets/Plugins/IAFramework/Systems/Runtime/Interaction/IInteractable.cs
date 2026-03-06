using UnityEngine;
using Ia.Gameplay.Actors;

namespace Ia.Systems.Interaction
{
    public interface IInteractable
    {
        Transform Transform { get; }

        /// <summary>
        /// Extra distance allowed for interaction beyond normal reach.
        /// </summary>
        int ExtraDistanceForInteraction { get; }
        
        /// <summary>
        /// Short description for UI (e.g. "Open", "Talk", "Pick Up").
        /// </summary>
        string InteractionLabel { get; }

        /// <summary>
        /// Can this actor interact right now?
        /// </summary>
        bool CanInteract(IaActor actor);

        /// <summary>
        /// Perform the interaction.
        /// </summary>
        void Interact(IaActor actor);
        
        /// <summary>
        /// Called when the actor hovers over this interactable.
        /// </summary>
        /// <param name="actor"></param>
        void Hover(IaActor actor);
        
        /// <summary>
        /// Called when the actor stops hovering over this interactable.
        /// </summary>
        /// <param name="actor"></param>
        void Unhover(IaActor actor);
    }
}