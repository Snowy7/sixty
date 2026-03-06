using System;
using Ia.Core.Update;
using UnityEngine;
using UnityEngine.Events;

namespace Sixty.Combat
{
    public class Health : IaBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onDeath;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDead => CurrentHealth <= 0f;
        public event Action<Health> OnDied;

        protected override void OnIaAwake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            onDamaged?.Invoke();

            if (IsDead)
            {
                onDeath?.Invoke();
                OnDied?.Invoke(this);

                if (destroyOnDeath)
                {
                    Destroy(gameObject);
                }
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }
    }
}
