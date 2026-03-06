using UnityEngine;
using Sixty.Gameplay;
using Ia.Core.Update;
using System.Collections.Generic;
using System;

namespace Sixty.Combat
{
    public class WeaponController : IaBehaviour
    {
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Projectile fallbackProjectilePrefab;
        [SerializeField] private bool alignProjectileHeightToOwner = true;
        [SerializeField] private float projectileHeightOffsetFromOwner = 0f;
        [Header("Pooling")]
        [SerializeField] private bool useProjectilePooling = true;
        [SerializeField] private int prewarmCount = 24;
        [SerializeField] private int maxPoolSize = 192;
        [Header("Runtime Modifiers")]
        [SerializeField] private float fireRateMultiplier = 1f;
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private float projectileSpeedMultiplier = 1f;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        private float nextShotTime;
        private readonly Queue<Projectile> projectilePool = new Queue<Projectile>(64);
        private Transform poolRoot;

        public WeaponDefinition CurrentWeapon => weapon;
        public string CurrentWeaponName => weapon != null && !string.IsNullOrWhiteSpace(weapon.weaponName) ? weapon.weaponName : "Unknown";
        public float FireRateMultiplier => fireRateMultiplier;
        public float DamageMultiplier => damageMultiplier;
        public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
        public float EffectiveFireRate => weapon != null ? SanitizePositive(weapon.fireRate * fireRateMultiplier, 0f) : 0f;
        public float EffectiveDamage => weapon != null ? SanitizePositive(weapon.damage * damageMultiplier, 0f) : 0f;
        public float EffectiveDamagePerTrigger => EffectiveDamage * Mathf.Max(1, weapon != null ? weapon.projectileCount : 1);
        public float EffectiveProjectileSpeed => weapon != null ? SanitizePositive(weapon.projectileSpeed * projectileSpeedMultiplier, 0f) : 0f;
        public int EffectiveProjectileCount => weapon != null ? Mathf.Max(1, weapon.projectileCount) : 1;

        public bool CanFireNow
        {
            get
            {
                Projectile projectilePrefab = ResolveProjectilePrefab();
                if (weapon == null || projectilePrefab == null)
                {
                    return false;
                }

                if (!float.IsFinite(nextShotTime))
                {
                    nextShotTime = 0f;
                }

                return Time.time >= nextShotTime;
            }
        }

        protected override void OnIaAwake()
        {
            if (useProjectilePooling)
            {
                EnsurePoolRoot();
                PrewarmPool();
            }
        }

        public bool TryFire(Vector3 direction)
        {
            Projectile projectilePrefab = ResolveProjectilePrefab();
            if (weapon == null || projectilePrefab == null)
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

            float clampedRate = SanitizePositive(weapon.fireRate * fireRateMultiplier, 0.01f);
            nextShotTime = Time.time + (1f / clampedRate);

            Transform spawnPoint = muzzle != null ? muzzle : transform;
            Vector3 spawnPosition = spawnPoint.position;
            if (alignProjectileHeightToOwner)
            {
                Transform root = transform.root != null ? transform.root : transform;
                spawnPosition.y = root.position.y + projectileHeightOffsetFromOwner;
            }

            Vector3 baseDirection = direction.normalized;
            int projectileCount = Mathf.Max(1, weapon.projectileCount);
            float spread = Mathf.Max(0f, weapon.spreadAngle);
            bool firedAny = false;
            for (int i = 0; i < projectileCount; i++)
            {
                float t = projectileCount <= 1 ? 0.5f : i / (float)(projectileCount - 1);
                float angle = projectileCount <= 1 ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                Vector3 shotDirection = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;

                Projectile projectile = AcquireProjectile();
                if (projectile == null)
                {
                    continue;
                }

                Transform projectileTransform = projectile.transform;
                projectileTransform.SetParent(null, true);
                projectileTransform.SetPositionAndRotation(
                    spawnPosition,
                    Quaternion.LookRotation(shotDirection, Vector3.up));
                if (!projectile.gameObject.activeSelf)
                {
                    projectile.gameObject.SetActive(true);
                }

                projectile.Initialize(
                    shotDirection,
                    SanitizePositive(weapon.projectileSpeed * projectileSpeedMultiplier, 0.01f),
                    SanitizePositive(weapon.damage * damageMultiplier, 0.01f),
                    weapon.projectileLifetime,
                    gameObject);
                firedAny = true;
            }

            if (!firedAny)
            {
                return false;
            }

            GameFeelController.Instance?.OnPlayerShot(spawnPoint.position, direction, transform.root);

            return true;
        }

        private Projectile AcquireProjectile()
        {
            Projectile projectilePrefab = ResolveProjectilePrefab();
            if (projectilePrefab == null)
            {
                return null;
            }

            if (!useProjectilePooling)
            {
                Projectile created = Instantiate(projectilePrefab);
                created.SetPoolReleaseCallback(null);
                return created;
            }

            while (projectilePool.Count > 0)
            {
                Projectile pooled = projectilePool.Dequeue();
                if (pooled != null)
                {
                    pooled.SetPoolReleaseCallback(ReleaseProjectile);
                    return pooled;
                }
            }

            Projectile instance = Instantiate(projectilePrefab, poolRoot);
            instance.gameObject.SetActive(false);
            instance.SetPoolReleaseCallback(ReleaseProjectile);
            return instance;
        }

        private void ReleaseProjectile(Projectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            if (!useProjectilePooling || projectilePool.Count >= maxPoolSize)
            {
                Destroy(projectile.gameObject);
                return;
            }

            EnsurePoolRoot();
            projectile.transform.SetParent(poolRoot, false);
            projectile.gameObject.SetActive(false);
            projectilePool.Enqueue(projectile);
        }

        private void PrewarmPool()
        {
            Projectile projectilePrefab = ResolveProjectilePrefab();
            if (weapon == null || projectilePrefab == null || prewarmCount <= 0)
            {
                return;
            }

            int clampedPrewarm = Mathf.Clamp(prewarmCount, 0, maxPoolSize);
            while (projectilePool.Count < clampedPrewarm)
            {
                Projectile projectile = Instantiate(projectilePrefab, poolRoot);
                projectile.SetPoolReleaseCallback(ReleaseProjectile);
                projectile.gameObject.SetActive(false);
                projectilePool.Enqueue(projectile);
            }
        }

        private void EnsurePoolRoot()
        {
            if (poolRoot != null)
            {
                return;
            }

            GameObject root = new GameObject($"{name}_ProjectilePool");
            root.transform.SetParent(transform, false);
            poolRoot = root.transform;
        }

        protected override void OnIaDestroy()
        {
            while (projectilePool.Count > 0)
            {
                Projectile pooled = projectilePool.Dequeue();
                if (pooled != null)
                {
                    pooled.SetPoolReleaseCallback(null);
                    Destroy(pooled.gameObject);
                }
            }
        }

        private Projectile ResolveProjectilePrefab()
        {
            if (weapon != null && weapon.projectilePrefab != null)
            {
                return weapon.projectilePrefab;
            }

            return fallbackProjectilePrefab;
        }

        public void SetWeapon(WeaponDefinition newWeapon)
        {
            if (newWeapon == null)
            {
                return;
            }

            weapon = newWeapon;
            nextShotTime = 0f;
            projectilePool.Clear();
            if (useProjectilePooling)
            {
                PrewarmPool();
            }
        }

        public void ApplyFireRateMultiplier(float multiplier)
        {
            fireRateMultiplier = Mathf.Clamp(fireRateMultiplier * Mathf.Max(0.01f, multiplier), 0.1f, 6f);
        }

        public void ApplyDamageMultiplier(float multiplier)
        {
            damageMultiplier = Mathf.Clamp(damageMultiplier * Mathf.Max(0.01f, multiplier), 0.1f, 10f);
        }

        public void ApplyProjectileSpeedMultiplier(float multiplier)
        {
            projectileSpeedMultiplier = Mathf.Clamp(projectileSpeedMultiplier * Mathf.Max(0.01f, multiplier), 0.1f, 6f);
        }

        private static float SanitizePositive(float value, float fallback)
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                return fallback;
            }

            return value;
        }
    }
}
