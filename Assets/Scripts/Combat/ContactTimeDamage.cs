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
        [SerializeField] private bool triggerDamageFeedback = false;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        private readonly Dictionary<int, float> nextHitAt = new Dictionary<int, float>();

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
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
            Transform otherRoot = other.transform.root;
            if (otherRoot == null)
            {
                return;
            }

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
    }
}
