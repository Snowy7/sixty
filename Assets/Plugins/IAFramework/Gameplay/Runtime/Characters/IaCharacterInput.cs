using UnityEngine;

namespace Ia.Gameplay.Characters
{
    /// <summary>
    /// Movement input data structure.
    /// Can be produced by player or AI.
    /// </summary>
    public struct IaCharacterInput
    {
        public Vector2 Move;      // x = strafe, y = forward
        public bool Jump;
        public bool Sprint;
        public bool Crouch;

        public static readonly IaCharacterInput None = new IaCharacterInput
        {
            Move = Vector2.zero,
            Jump = false,
            Sprint = false,
            Crouch = false
        };
    }

    /// <summary>
    /// Combat input data structure for weapons and combat systems.
    /// </summary>
    public struct IaCombatInput
    {
        public bool PrimaryFire;
        public bool SecondaryFire;
        public bool Reload;

        public static readonly IaCombatInput None = new IaCombatInput
        {
            PrimaryFire = false,
            SecondaryFire = false,
            Reload = false
        };
    }

    /// <summary>
    /// Interaction input data structure for interaction systems.
    /// </summary>
    public struct IaInteractionInput
    {
        public bool Interact;

        public static readonly IaInteractionInput None = new IaInteractionInput
        {
            Interact = false
        };
    }

    /// <summary>
    /// Aim state input data structure.
    /// </summary>
    public struct IaAimInput
    {
        public bool Aim;

        public static readonly IaAimInput None = new IaAimInput
        {
            Aim = false
        };
    }

    /// <summary>
    /// Inventory input data structure for scrolling and switching items.
    /// </summary>
    public struct IaInventoryInput
    {
        public bool Previous;
        public bool Next;
        public Vector2 ScrollDelta;

        public static readonly IaInventoryInput None = new IaInventoryInput
        {
            Previous = false,
            Next = false,
            ScrollDelta = Vector2.zero
        };
    }
}