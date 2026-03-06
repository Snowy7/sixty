using Ia.Core.Events;
using Ia.Core.Pooling;
using Ia.Core.Update;
using Ia.Gameplay.Actors;
using UnityEngine;

namespace Ia.Gameplay.Combat
{
    public class IaHitscanWeapon : IaWeaponBase, IIaReloadableWeapon
    {
        [Header("Firing")]
        [SerializeField] float fireRate = 5f;
        [SerializeField] float range = 100f;
        [SerializeField] LayerMask hitMask = ~0;

        [Header("Ammo / Reload")]
        [SerializeField] int magSize = 12;
        [SerializeField] int reserveAmmo = 48;
        [SerializeField] float reloadSeconds = 1.25f;
        [SerializeField] bool autoReload = true;

        [Header("References")]
        [SerializeField] Camera cameraRef;
        [SerializeField] Transform muzzle;

        [Header("Visual Bullet (Tracer)")]
        [SerializeField] IaTracer tracerPrefab;
        [SerializeField] float tracerTravelSeconds = 0.06f;
        [SerializeField] float tracerMaxDistance = 80f;

        int m_ammoInMag;
        float m_nextFireTime;

        bool m_isReloading;
        float m_reloadDoneTime;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        protected override void Awake()
        {
            base.Awake();

            if (cameraRef == null && Camera.main != null)
                cameraRef = Camera.main;

            m_ammoInMag = magSize;
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (!m_isReloading)
                return;

            if (Time.time < m_reloadDoneTime)
                return;

            FinishReload();
        }

        public override void PrimaryFire()
        {
            if (Time.time < m_nextFireTime)
                return;

            if (m_isReloading)
                return;

            if (m_ammoInMag <= 0)
            {
                if (autoReload)
                    TryReload();
                return;
            }

            m_nextFireTime = Time.time + 1f / fireRate;
            m_ammoInMag--;

            if (cameraRef == null)
                return;

            Ray ray = cameraRef.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            bool didHit = Physics.Raycast(
                ray,
                out RaycastHit hit,
                range,
                hitMask,
                QueryTriggerInteraction.Ignore
            );

            Vector3 endPos = ray.origin + ray.direction * Mathf.Min(range, tracerMaxDistance);
            if (didHit)
                endPos = hit.point;

            if (didHit)
            {
                IaActor targetActor = hit.collider.GetComponentInParent<IaActor>();
                if (targetActor != null && targetActor != owner)
                {
                    IaDamageInfo dmg = new IaDamageInfo(
                        amount: damage,
                        type: damageType,
                        source: owner,
                        hitPoint: hit.point,
                        hitNormal: hit.normal
                    );

                    targetActor.ApplyDamage(dmg);
                }

                // TODO: spawn pooled impact FX here
            }

            SpawnTracer(endPos);

            // Publish for recoil/camera shake/muzzle flash/audio/etc.
            Ia.Core.Events.IaEventBus.Publish(
                new WeaponFiredEvent(owner, muzzle != null ? muzzle.position : ray.origin, endPos, didHit)
            );
        }

        void SpawnTracer(Vector3 endPos)
        {
            if (tracerPrefab == null || IaPoolManager.Instance == null)
                return;

            Vector3 start = muzzle != null ? muzzle.position : cameraRef.transform.position;
            Quaternion rot = Quaternion.LookRotation((endPos - start).normalized, Vector3.up);

            IaTracer tracer = IaPoolManager.Instance.Spawn(tracerPrefab, start, rot);
            if (tracer != null)
                tracer.Play(start, endPos, tracerTravelSeconds);
        }

        public void TryReload()
        {
            if (m_isReloading)
                return;

            if (m_ammoInMag >= magSize)
                return;

            if (reserveAmmo <= 0)
                return;

            m_isReloading = true;
            m_reloadDoneTime = Time.time + reloadSeconds;

            Ia.Core.Events.IaEventBus.Publish(new WeaponReloadStartedEvent(owner));
        }

        void FinishReload()
        {
            int needed = magSize - m_ammoInMag;
            int take = Mathf.Min(needed, reserveAmmo);

            reserveAmmo -= take;
            m_ammoInMag += take;

            m_isReloading = false;

            Ia.Core.Events.IaEventBus.Publish(new WeaponReloadFinishedEvent(owner));
        }
    }
}