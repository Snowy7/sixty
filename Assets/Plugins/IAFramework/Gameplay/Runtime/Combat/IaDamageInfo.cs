using UnityEngine;
using Ia.Gameplay.Actors;

namespace Ia.Gameplay.Combat
{
    public enum IaDamageType
    {
        Physical,
        Fire,
        Ice,
        Electric,
        True
    }

    public struct IaDamageInfo
    {
        public float amount;
        public IaDamageType type;
        public IaActor source;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public bool isCritical;

        public IaDamageInfo(
            float amount,
            IaDamageType type,
            IaActor source,
            Vector3 hitPoint,
            Vector3 hitNormal,
            bool isCritical = false
        )
        {
            this.amount = amount;
            this.type = type;
            this.source = source;
            this.hitPoint = hitPoint;
            this.hitNormal = hitNormal;
            this.isCritical = isCritical;
        }
    }
}