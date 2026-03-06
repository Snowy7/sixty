using UnityEngine;
using Sixty.Gameplay;
using Sixty.Enemies;

namespace Sixty.Combat
{
    [RequireComponent(typeof(Collider))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private bool destroyOnHit = true;
        [SerializeField] private bool destroyWhenTouchingNonDamageable = true;

        private Vector3 moveDirection = Vector3.forward;
        private float speed;
        private float damage;
        private float lifeRemaining;
        private GameObject owner;

        public void Initialize(Vector3 direction, float projectileSpeed, float projectileDamage, float lifetime, GameObject sourceOwner = null)
        {
            moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            speed = projectileSpeed;
            damage = projectileDamage;
            lifeRemaining = Mathf.Max(0.01f, lifetime);
            owner = sourceOwner;
        }

        private void Reset()
        {
            Collider projectileCollider = GetComponent<Collider>();
            projectileCollider.isTrigger = true;
        }

        private void Update()
        {
            transform.position += moveDirection * (speed * Time.deltaTime);

            lifeRemaining -= Time.deltaTime;
            if (lifeRemaining <= 0f)
            {
                Destroy(gameObject);
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
                    Destroy(gameObject);
                }

                return;
            }

            if (destroyWhenTouchingNonDamageable && !other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}
