using Sixty.Core;
using Sixty.Gameplay;
using Sixty.Player;
using UnityEngine;
using Ia.Core.Update;

namespace Sixty.World
{
    [RequireComponent(typeof(Collider))]
    public class ClockPickup : IaBehaviour
    {
        [SerializeField] private float timeGranted = 5f;
        [SerializeField] private bool destroyOnPickup = true;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return;
            }

            TimeManager.Instance?.AddTime(timeGranted);
            GameFeelController.Instance?.OnClockPickup(transform.position);

            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
        }
    }
}
