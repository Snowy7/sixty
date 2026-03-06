using System;
using UnityEngine;
using UnityEngine.Events;

namespace Sixty.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onDeath;

        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;
        public event Action<Health> OnDied;

        private void Awake()
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
