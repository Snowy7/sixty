using Sixty.Player;
using Sixty.Combat;
using UnityEngine;
using Ia.Core.Update;
using System;

namespace Sixty.Enemies
{
    [RequireComponent(typeof(Collider))]
    public class EnemyProjectile : IaBehaviour
    {
        [SerializeField] private bool destroyWhenTouchingEnvironment = true;

        private Vector3 moveDirection = Vector3.forward;
        private float speed = 12f;
        private float timeDamage = 2f;
        private float lifeRemaining = 2f;
        private GameObject owner;
        private Action<EnemyProjectile> releaseToPool;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        public void Initialize(Vector3 direction, float projectileSpeed, float damageAsTimeLoss, float lifetime, GameObject sourceOwner = null)
        {
            moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            speed = Mathf.Max(0.01f, projectileSpeed);
            timeDamage = Mathf.Max(0.1f, damageAsTimeLoss);
            lifeRemaining = Mathf.Max(0.01f, lifetime);
            owner = sourceOwner;
        }

        public void SetPoolReleaseCallback(Action<EnemyProjectile> releaseCallback)
        {
            releaseToPool = releaseCallback;
        }

        private void Reset()
        {
            Collider projectileCollider = GetComponent<Collider>();
            projectileCollider.isTrigger = true;
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (owner == null)
            {
                Despawn();
                return;
            }

            if (owner != null)
            {
                Health ownerHealth = owner.GetComponent<Health>();
                if (ownerHealth != null && ownerHealth.IsDead)
                {
                    Despawn();
                    return;
                }
            }

            transform.position += moveDirection * (speed * deltaTime);

            lifeRemaining -= deltaTime;
            if (lifeRemaining <= 0f)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null && other.transform.root == owner.transform.root)
            {
                return;
            }

            Transform otherRoot = other.transform.root;
            PlayerController player = otherRoot != null ? otherRoot.GetComponent<PlayerController>() : null;
            if (player != null)
            {
                player.TryTakeTimeDamage(timeDamage);
                Despawn();
                return;
            }

            if (destroyWhenTouchingEnvironment && !other.isTrigger)
            {
                Despawn();
            }
        }

        private void Despawn()
        {
            if (releaseToPool != null)
            {
                object callbackTarget = releaseToPool.Target;
                if (!(callbackTarget is UnityEngine.Object unityTarget) || unityTarget != null)
                {
                    try
                    {
                        releaseToPool(this);
                        return;
                    }
                    catch (MissingReferenceException)
                    {
                        // Shooter/pool owner was destroyed; fallback to regular destroy.
                    }
                }

                releaseToPool = null;
            }

            Destroy(gameObject);
        }
    }
}
