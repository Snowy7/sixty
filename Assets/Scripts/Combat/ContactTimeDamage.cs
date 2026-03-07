using System.Collections.Generic;
using Ia.Core.Update;
using Sixty.Player;
using UnityEngine;

namespace Sixty.Combat
{
    [RequireComponent(typeof(Collider))]
    public class ContactTimeDamage : IaBehaviour
    {
        [SerializeField] private float timeDamage = 2f;
        [SerializeField] private float hitCooldown = 0.35f;
        [SerializeField] private bool triggerDamageFeedback = true;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        private readonly Dictionary<int, float> nextHitAt = new Dictionary<int, float>();
        private Health ownerHealth;
        private Collider triggerCollider;
        private bool damageEnabled = true;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        protected override void OnIaAwake()
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            ownerHealth = GetComponentInParent<Health>();
        }

        protected override void OnIaEnable()
        {
            damageEnabled = true;
            if (ownerHealth != null)
            {
                ownerHealth.OnDied += HandleOwnerDied;
            }
        }

        protected override void OnIaDisable()
        {
            if (ownerHealth != null)
            {
                ownerHealth.OnDied -= HandleOwnerDied;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryDamage(other);
        }

        private void OnTriggerExit(Collider other)
        {
            int id = other.GetInstanceID();
            if (nextHitAt.ContainsKey(id))
            {
                nextHitAt.Remove(id);
            }
        }

        private void TryDamage(Collider other)
        {
            if (!damageEnabled)
            {
                return;
            }

            if (ownerHealth != null && ownerHealth.IsDead)
            {
                DisableDamageZone();
                return;
            }

            // Ignore triggers (projectiles, other damage zones)
            if (other.isTrigger)
            {
                return;
            }

            Transform otherRoot = other.transform.root;
            if (otherRoot == null)
            {
                return;
            }

            // Only damage the player's body collider, not projectiles or other objects
            PlayerController player = otherRoot.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            int targetId = other.GetInstanceID();
            if (nextHitAt.TryGetValue(targetId, out float cooldownUntil) && Time.time < cooldownUntil)
            {
                return;
            }

            bool dealtDamage = player.TryTakeTimeDamage(timeDamage, triggerDamageFeedback);
            if (!dealtDamage)
            {
                return;
            }

            nextHitAt[targetId] = Time.time + hitCooldown;
        }

        private void HandleOwnerDied(Health _)
        {
            DisableDamageZone();
        }

        private void DisableDamageZone()
        {
            damageEnabled = false;
            nextHitAt.Clear();
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }
        }
    }
}
