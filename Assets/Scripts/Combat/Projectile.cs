using UnityEngine;
using Sixty.Gameplay;
using Sixty.Enemies;
using Ia.Core.Update;
using System;

namespace Sixty.Combat
{
    [RequireComponent(typeof(Collider))]
    public class Projectile : IaBehaviour
    {
        [SerializeField] private bool destroyOnHit = true;
        [SerializeField] private bool destroyWhenTouchingNonDamageable = true;

        private Vector3 moveDirection = Vector3.forward;
        private float speed;
        private float damage;
        private float lifeRemaining;
        private GameObject owner;
        private Action<Projectile> releaseToPool;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.FX;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        public void Initialize(Vector3 direction, float projectileSpeed, float projectileDamage, float lifetime, GameObject sourceOwner = null)
        {
            moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            speed = projectileSpeed;
            damage = projectileDamage;
            lifeRemaining = Mathf.Max(0.01f, lifetime);
            owner = sourceOwner;
        }

        public void SetPoolReleaseCallback(Action<Projectile> releaseCallback)
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
            transform.position += moveDirection * (speed * deltaTime);

            lifeRemaining -= deltaTime;
            if (lifeRemaining <= 0f)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null && other.transform.root.gameObject == owner.transform.root.gameObject)
            {
                return;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                Health health = other.GetComponentInParent<Health>();
                bool wasDead = health != null && health.IsDead;
                damageable.TakeDamage(damage);
                bool wasKilled = health != null && !wasDead && health.IsDead;

                EnemyImpactResponse impactResponse = other.GetComponentInParent<EnemyImpactResponse>();
                if (impactResponse != null)
                {
                    impactResponse.ApplyImpact(moveDirection, wasKilled);
                }

                Vector3 hitPoint = other.ClosestPoint(transform.position);
                GameFeelController.Instance?.OnEnemyHit(hitPoint, wasKilled, health != null ? health.transform : other.transform.root);

                if (destroyOnHit)
                {
                    Despawn();
                }

                return;
            }

            if (destroyWhenTouchingNonDamageable && !other.isTrigger)
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
                        // Pool owner was destroyed; fallback to regular destroy.
                    }
                }

                releaseToPool = null;
            }

            Destroy(gameObject);
        }
    }
}
