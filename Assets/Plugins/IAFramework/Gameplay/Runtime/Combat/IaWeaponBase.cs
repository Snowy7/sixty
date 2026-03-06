using Ia.Core.Update;
using UnityEngine;
using Ia.Gameplay.Actors;

namespace Ia.Gameplay.Combat
{
    public abstract class IaWeaponBase : IaBehaviour, IIaWeapon
    {
        [SerializeField] protected IaActor owner;
        [SerializeField] protected float damage = 10f;
        [SerializeField] protected IaDamageType damageType =
            IaDamageType.Physical;

        public IaActor Owner => owner;

        public virtual void Initialize(IaActor newOwner)
        {
            owner = newOwner;
        }

        public abstract void PrimaryFire();
        public virtual void SecondaryFire() { }
    }
}