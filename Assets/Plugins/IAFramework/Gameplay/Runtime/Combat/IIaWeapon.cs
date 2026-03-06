using UnityEngine;
using Ia.Gameplay.Actors;

namespace Ia.Gameplay.Combat
{
    public interface IIaWeapon
    {
        IaActor Owner { get; }

        void Initialize(IaActor owner);
        void PrimaryFire();
        void SecondaryFire();
    }
}