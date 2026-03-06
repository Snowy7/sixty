using System;
using UnityEngine;
using Ia.Core.Debugging;
using Ia.Core.Events;
using Ia.Core.Update;
using Ia.Gameplay.Combat;

namespace Ia.Gameplay.Actors
{
    public enum IaTeam
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2,
        Ally = 3
    }

    [DisallowMultipleComponent]
    public class IaActor : IaBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _actorId = Guid.NewGuid().ToString();
        [SerializeField] private string _displayName = "Actor";
        [SerializeField] private IaTeam _team = IaTeam.Neutral;

        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;

        public string ActorId => _actorId;
        public string DisplayName => _displayName;
        public IaTeam Team => _team;
        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;

        public bool IsAlive => _currentHealth > 0f;

        // Local events
        public event Action<IaActor, float, float> HealthChanged;
        public event Action<IaActor> Died;

        protected override void Awake()
        {
            base.Awake();
            
            // Ensure current health is valid
            _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        }
        
        public void ApplyDamage(IaDamageInfo damageInfo)
        {
            if (!IsAlive || damageInfo.amount <= 0f)
                return;

            float old = _currentHealth;
            _currentHealth = Mathf.Max(0f, _currentHealth - damageInfo.amount);

            HealthChanged?.Invoke(this, old, _currentHealth);

            IaEventBus.Publish(new ActorDamagedEvent(this, damageInfo));
            
            IaLogger.Info(
                IaLogCategory.Gameplay,
                $"Actor {DisplayName} took {damageInfo.amount} damage (HP: {old} -> {_currentHealth})",
                this
            );

            if (_currentHealth <= 0f)
            {
                HandleDeath(damageInfo.source);
            }
        }

        public void Heal(float amount, object source = null)
        {
            if (amount <= 0f || !IsAlive)
                return;

            float old = _currentHealth;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);

            HealthChanged?.Invoke(this, old, _currentHealth);

            IaLogger.Info(
                IaLogCategory.Gameplay,
                $"Actor {DisplayName} healed {amount} (HP: {old} -> {_currentHealth})",
                this
            );
        }

        private void HandleDeath(object source)
        {
            if (!IsAlive)
            {
                IaLogger.Info(
                    IaLogCategory.Gameplay,
                    $"Actor {DisplayName} died.",
                    this
                );

                Died?.Invoke(this);

                IaEventBus.Publish(new ActorDiedEvent(this));
            }
        }
    }
}