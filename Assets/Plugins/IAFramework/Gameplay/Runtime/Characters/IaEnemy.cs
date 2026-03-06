using UnityEngine;
using Ia.Gameplay.Actors;

namespace Ia.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IaActor))]
    public class IaEnemy : MonoBehaviour
    {
        [SerializeField] bool destroyOnDeath = true;

        IaActor m_actor;

        void Awake()
        {
            m_actor = GetComponent<IaActor>();
        }

        void OnEnable()
        {
            if (m_actor != null)
                m_actor.Died += OnDied;
        }

        void OnDisable()
        {
            if (m_actor != null)
                m_actor.Died -= OnDied;
        }

        void OnDied(IaActor actor)
        {
            if (destroyOnDeath)
                Destroy(gameObject);
            // Later: play death animation, drop loot, etc.
        }
    }
}