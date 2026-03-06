using Ia.Core.Update;
using UnityEngine;

namespace Ia.Core.Pooling
{
    public abstract class IaPooledObject : IaBehaviour
    {
        internal IaPool Pool { get; set; }

        public virtual void OnSpawned() { }

        public virtual void OnDespawned() { }

        public void Despawn()
        {
            Pool?.Despawn(this);
        }
    }
}