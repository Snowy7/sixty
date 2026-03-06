using Sixty.Player;
using UnityEngine;

namespace Sixty.Enemies
{
    [RequireComponent(typeof(Collider))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private bool destroyWhenTouchingEnvironment = true;

        private Vector3 moveDirection = Vector3.forward;
        private float speed = 12f;
        private float timeDamage = 2f;
        private float lifeRemaining = 2f;
        private GameObject owner;

        public void Initialize(Vector3 direction, float projectileSpeed, float damageAsTimeLoss, float lifetime, GameObject sourceOwner = null)
        {
            moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            speed = Mathf.Max(0.01f, projectileSpeed);
            timeDamage = Mathf.Max(0.1f, damageAsTimeLoss);
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
            if (owner != null && other.transform.root == owner.transform.root)
            {
                return;
            }

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.TryTakeTimeDamage(timeDamage);
                Destroy(gameObject);
                return;
            }

            if (destroyWhenTouchingEnvironment && !other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}
