using UnityEngine;
using Sixty.Gameplay;

namespace Sixty.Combat
{
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private Transform muzzle;
        [SerializeField] private bool alignProjectileHeightToOwner = true;
        [SerializeField] private float projectileHeightOffsetFromOwner = 0f;

        private float nextShotTime;

        public bool TryFire(Vector3 direction)
        {
            if (weapon == null || weapon.projectilePrefab == null)
            {
                return false;
            }

            if (Time.time < nextShotTime)
            {
                return false;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float clampedRate = Mathf.Max(0.01f, weapon.fireRate);
            nextShotTime = Time.time + (1f / clampedRate);

            Transform spawnPoint = muzzle != null ? muzzle : transform;
            Vector3 spawnPosition = spawnPoint.position;
            if (alignProjectileHeightToOwner)
            {
                Transform root = transform.root != null ? transform.root : transform;
                spawnPosition.y = root.position.y + projectileHeightOffsetFromOwner;
            }

            Projectile projectile = Instantiate(
                weapon.projectilePrefab,
                spawnPosition,
                Quaternion.LookRotation(direction.normalized, Vector3.up));

            projectile.Initialize(
                direction.normalized,
                weapon.projectileSpeed,
                weapon.damage,
                weapon.projectileLifetime,
                gameObject);

            GameFeelController.Instance?.OnPlayerShot(spawnPoint.position, direction, transform.root);

            return true;
        }
    }
}
