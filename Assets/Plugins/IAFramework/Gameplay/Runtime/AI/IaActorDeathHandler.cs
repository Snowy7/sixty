using Ia.Gameplay.Actors;
using UnityEngine;

namespace Ia.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class IaActorDeathHandler : MonoBehaviour
    {
        [SerializeField] IaActor actor;
        [SerializeField] Behaviour[] disableOnDeath;
        [SerializeField] bool destroyOnDeath = true;
        [SerializeField] float destroyDelay = 2f;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<IaActor>();
        }

        void OnEnable()
        {
            if (actor != null)
                actor.Died += OnDied;
        }

        void OnDisable()
        {
            if (actor != null)
                actor.Died -= OnDied;
        }

        void OnDied(IaActor a)
        {
            if (disableOnDeath != null)
            {
                foreach (var b in disableOnDeath)
                {
                    if (b != null)
                        b.enabled = false;
                }
            }

            if (destroyOnDeath)
                Destroy(gameObject, destroyDelay);
        }
    }
}