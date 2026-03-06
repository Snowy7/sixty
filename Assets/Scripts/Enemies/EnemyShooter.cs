using Sixty.Player;
using UnityEngine;
using Ia.Core.Update;
using System.Collections.Generic;

namespace Sixty.Enemies
{
    public class EnemyShooter : IaBehaviour
    {
        public enum FireMode
        {
            Single = 0,
            Burst = 1,
            Spread = 2,
            BurstSpread = 3
        }

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
        [SerializeField] private float minimumRange = 0f;
        [SerializeField] private bool usePredictiveAim = false;
        [SerializeField] private float predictiveLeadSeconds = 0.12f;
        [SerializeField] private float windupSeconds = 0f;
        [SerializeField] private FireMode fireMode = FireMode.Single;
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float burstInterval = 0.08f;
        [SerializeField] private int spreadProjectiles = 3;
        [SerializeField] private float spreadAngle = 12f;
        [SerializeField] private float externalFireRateMultiplier = 1f;

        [Header("Performance")]
        [SerializeField] private float playerLookupInterval = 0.4f;
        [SerializeField] private bool useProjectilePooling = true;
        [SerializeField] private int prewarmCount = 8;
        [SerializeField] private int maxPoolSize = 64;

        private Rigidbody body;
        private Transform target;
        private float nextShotAt;
        private float lockedY;
        private float nextPlayerLookupAt;
        private int burstShotsRemaining;
        private float nextBurstShotAt;
        private float windupTimer;
        private Vector3 queuedAimDirection = Vector3.forward;
        private Rigidbody targetBody;
        private readonly Queue<EnemyProjectile> projectilePool = new Queue<EnemyProjectile>(24);
        private Transform poolRoot;
        private static PlayerController cachedPlayer;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.AI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update | IaUpdatePhase.FixedUpdate;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
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

            if (useProjectilePooling)
            {
                EnsurePoolRoot();
                PrewarmPool();
            }
        }

        public override void OnIaFixedUpdate(float fixedDeltaTime)
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

        public override void OnIaUpdate(float deltaTime)
        {
            if (projectilePrefab == null)
            {
                return;
            }

            if (target == null && !TryResolveTarget())
            {
                return;
            }

            if (target == null)
            {
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float distance = toTarget.magnitude;
            if (distance < minimumRange || distance > range)
            {
                return;
            }

            Vector3 aimDirection = ComputeAimDirection(toTarget, distance);
            Transform pivot = aimPivot != null ? aimPivot : transform;
            pivot.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            if (windupTimer > 0f)
            {
                windupTimer -= deltaTime;
                queuedAimDirection = aimDirection;
                if (windupTimer <= 0f)
                {
                    BeginFireSequence(queuedAimDirection);
                }

                return;
            }

            if (burstShotsRemaining > 0)
            {
                if (Time.time >= nextBurstShotAt)
                {
                    FireBurstShot(queuedAimDirection);
                    burstShotsRemaining--;
                    nextBurstShotAt = Time.time + Mathf.Max(0.01f, burstInterval);
                }

                return;
            }

            if (Time.time < nextShotAt)
            {
                return;
            }

            float clampedRate = Mathf.Max(0.1f, fireRate * Mathf.Max(0.1f, externalFireRateMultiplier));
            nextShotAt = Time.time + (1f / clampedRate);

            if (windupSeconds > 0f)
            {
                windupTimer = windupSeconds;
                queuedAimDirection = aimDirection;
                return;
            }

            BeginFireSequence(aimDirection);
        }

        public void SetExternalFireRateMultiplier(float multiplier)
        {
            externalFireRateMultiplier = Mathf.Clamp(multiplier, 0.2f, 4f);
        }

        public void SetFireMode(FireMode mode)
        {
            fireMode = mode;
        }

        public void SetBurstSettings(int count, float interval)
        {
            burstCount = Mathf.Max(1, count);
            burstInterval = Mathf.Max(0.01f, interval);
        }

        public void SetSpreadSettings(int projectileCount, float angle)
        {
            spreadProjectiles = Mathf.Max(1, projectileCount);
            spreadAngle = Mathf.Max(0f, angle);
        }

        private Vector3 ComputeAimDirection(Vector3 toTarget, float distance)
        {
            Vector3 aimDirection = toTarget.normalized;
            if (!usePredictiveAim || targetBody == null || predictiveLeadSeconds <= 0.001f)
            {
                return aimDirection;
            }

            Vector3 predicted = target.position + (targetBody.linearVelocity * predictiveLeadSeconds);
            predicted.y = transform.position.y;
            Vector3 toPredicted = predicted - transform.position;
            if (toPredicted.sqrMagnitude <= 0.001f)
            {
                return aimDirection;
            }

            return toPredicted.normalized;
        }

        private void BeginFireSequence(Vector3 baseAimDirection)
        {
            switch (fireMode)
            {
                case FireMode.Single:
                    FireSpread(baseAimDirection, 1, 0f);
                    break;

                case FireMode.Burst:
                    burstShotsRemaining = Mathf.Max(1, burstCount);
                    queuedAimDirection = baseAimDirection;
                    nextBurstShotAt = Time.time;
                    break;

                case FireMode.Spread:
                    FireSpread(baseAimDirection, spreadProjectiles, spreadAngle);
                    break;

                case FireMode.BurstSpread:
                    burstShotsRemaining = Mathf.Max(1, burstCount);
                    queuedAimDirection = baseAimDirection;
                    nextBurstShotAt = Time.time;
                    break;
            }
        }

        private void FireBurstShot(Vector3 aimDirection)
        {
            if (fireMode == FireMode.BurstSpread)
            {
                FireSpread(aimDirection, spreadProjectiles, spreadAngle);
                return;
            }

            FireSpread(aimDirection, 1, 0f);
        }

        private void FireSpread(Vector3 baseDirection, int projectileCount, float totalSpreadAngle)
        {
            int count = Mathf.Max(1, projectileCount);
            float halfSpread = totalSpreadAngle * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float angle = count <= 1 ? 0f : Mathf.Lerp(-halfSpread, halfSpread, t);
                Vector3 shotDirection = Quaternion.AngleAxis(angle, Vector3.up) * baseDirection;
                FireProjectile(shotDirection);
            }
        }

        private void FireProjectile(Vector3 aimDirection)
        {
            Transform shotFrom = muzzle != null ? muzzle : transform;
            EnemyProjectile projectile = AcquireProjectile();
            if (projectile == null)
            {
                return;
            }

            projectile.transform.SetParent(null, true);
            projectile.transform.SetPositionAndRotation(
                shotFrom.position,
                Quaternion.LookRotation(aimDirection, Vector3.up));
            if (!projectile.gameObject.activeSelf)
            {
                projectile.gameObject.SetActive(true);
            }

            projectile.Initialize(aimDirection, projectileSpeed, timeDamage, projectileLifetime, gameObject);
        }

        private bool TryResolveTarget()
        {
            if (target != null)
            {
                return true;
            }

            if (Time.time < nextPlayerLookupAt)
            {
                return false;
            }

            nextPlayerLookupAt = Time.time + Mathf.Max(0.05f, playerLookupInterval);
            if (cachedPlayer == null)
            {
                cachedPlayer = FindFirstObjectByType<PlayerController>();
            }

            if (cachedPlayer != null)
            {
                target = cachedPlayer.transform;
                targetBody = cachedPlayer.GetComponent<Rigidbody>();
                return true;
            }

            return false;
        }

        private EnemyProjectile AcquireProjectile()
        {
            if (!useProjectilePooling)
            {
                EnemyProjectile created = Instantiate(projectilePrefab);
                created.SetPoolReleaseCallback(null);
                return created;
            }

            while (projectilePool.Count > 0)
            {
                EnemyProjectile pooled = projectilePool.Dequeue();
                if (pooled != null)
                {
                    pooled.SetPoolReleaseCallback(ReleaseProjectile);
                    return pooled;
                }
            }

            EnemyProjectile instance = Instantiate(projectilePrefab, poolRoot);
            instance.gameObject.SetActive(false);
            instance.SetPoolReleaseCallback(ReleaseProjectile);
            return instance;
        }

        private void ReleaseProjectile(EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            if (this == null)
            {
                Destroy(projectile.gameObject);
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
            if (projectilePrefab == null || prewarmCount <= 0)
            {
                return;
            }

            int clampedPrewarm = Mathf.Clamp(prewarmCount, 0, maxPoolSize);
            while (projectilePool.Count < clampedPrewarm)
            {
                EnemyProjectile projectile = Instantiate(projectilePrefab, poolRoot);
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

            GameObject root = new GameObject($"{name}_EnemyProjectilePool");
            root.transform.SetParent(transform, false);
            poolRoot = root.transform;
        }

        protected override void OnIaDestroy()
        {
            while (projectilePool.Count > 0)
            {
                EnemyProjectile pooled = projectilePool.Dequeue();
                if (pooled != null)
                {
                    pooled.SetPoolReleaseCallback(null);
                    Destroy(pooled.gameObject);
                }
            }
        }
    }
}
