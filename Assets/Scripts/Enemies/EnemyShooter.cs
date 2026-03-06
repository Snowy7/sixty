using Sixty.Player;
using UnityEngine;

namespace Sixty.Enemies
{
    public class EnemyShooter : MonoBehaviour
    {
        [SerializeField] private EnemyProjectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform aimPivot;
        [SerializeField] private bool lockVerticalPosition = true;

        [Header("Fire")]
        [SerializeField] private float fireRate = 1.2f;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private float projectileLifetime = 3f;
        [SerializeField] private float timeDamage = 2f;
        [SerializeField] private float range = 18f;

        private Rigidbody body;
        private Transform target;
        private float nextShotAt;
        private float lockedY;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation |
                               (lockVerticalPosition ? RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            lockedY = transform.position.y;
        }

        private void FixedUpdate()
        {
            if (!lockVerticalPosition || body == null)
            {
                return;
            }

            Vector3 position = body.position;
            if (Mathf.Abs(position.y - lockedY) <= 0.0001f)
            {
                return;
            }

            position.y = lockedY;
            body.MovePosition(position);
        }

        private void Update()
        {
            if (projectilePrefab == null)
            {
                return;
            }

            if (target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }

                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector3 aimDirection = toTarget.normalized;
            Transform pivot = aimPivot != null ? aimPivot : transform;
            pivot.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            if (toTarget.magnitude > range || Time.time < nextShotAt)
            {
                return;
            }

            float clampedRate = Mathf.Max(0.1f, fireRate);
            nextShotAt = Time.time + (1f / clampedRate);

            Transform shotFrom = muzzle != null ? muzzle : transform;
            EnemyProjectile projectile = Instantiate(projectilePrefab, shotFrom.position, Quaternion.LookRotation(aimDirection, Vector3.up));
            projectile.Initialize(aimDirection, projectileSpeed, timeDamage, projectileLifetime, gameObject);
        }
    }
}
