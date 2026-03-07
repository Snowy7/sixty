using System.Collections;
using Ia.Core.Update;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class HitFlashSquash : IaBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Renderer[] targetRenderers;

        [Header("Hit")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashInDuration = 0.03f;
        [SerializeField] private float flashOutDuration = 0.12f;
        [SerializeField] private float hitSqueezeStrength = 0.22f;
        [SerializeField] private float hitRecoverDuration = 0.16f;
        [SerializeField] private float heavyHitSqueezeMultiplier = 2.15f;
        [SerializeField] private float squeezeGlobalMultiplier = 1.45f;
        [SerializeField] private float hitColorIntensity = 1.95f;
        [SerializeField] private float heavyHitColorIntensity = 2.8f;
        [SerializeField] private float hitEmissionIntensity = 2.8f;
        [SerializeField] private float heavyHitEmissionIntensity = 4.8f;

        [Header("Shot Recoil")]
        [SerializeField] private float recoilSqueezeStrength = 0.11f;
        [SerializeField] private float recoilDuration = 0.11f;
        [SerializeField] private float recoilColorIntensity = 1.3f;
        [SerializeField] private float recoilEmissionIntensity = 1.7f;

        private struct MaterialSnapshot
        {
            public Material material;
            public bool hasBaseColor;
            public bool hasColor;
            public bool hasTintColor;
            public bool hasEmissionColor;
            public bool hasCrystalFlash;
            public Color baseColor;
            public Color baseEmissionColor;
        }

        private MaterialSnapshot[] snapshots;
        private Vector3 baseScale;
        private Coroutine activeRoutine;
        private float activeColorIntensity = 1f;
        private float activeEmissionIntensity = 1f;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.FX;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            baseScale = transform.localScale;

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }

            snapshots = new MaterialSnapshot[targetRenderers.Length];
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                Material mat = renderer != null ? renderer.material : null;
                if (mat == null)
                {
                    continue;
                }

                bool hasBaseColor = mat.HasProperty("_BaseColor");
                bool hasColor = mat.HasProperty("_Color");
                bool hasTintColor = mat.HasProperty("_TintColor");
                bool hasEmissionColor = mat.HasProperty("_EmissionColor");
                Color baseColor = hasBaseColor ? mat.GetColor("_BaseColor") : (hasColor ? mat.GetColor("_Color") : Color.white);
                Color baseEmissionColor = hasEmissionColor ? mat.GetColor("_EmissionColor") : Color.black;
                if (hasEmissionColor)
                {
                    mat.EnableKeyword("_EMISSION");
                }

                snapshots[i] = new MaterialSnapshot
                {
                    material = mat,
                    hasBaseColor = hasBaseColor,
                    hasColor = hasColor,
                    hasTintColor = hasTintColor,
                    hasEmissionColor = hasEmissionColor,
                    hasCrystalFlash = mat.HasProperty("_UseFlashColor"),
                    baseColor = baseColor,
                    baseEmissionColor = baseEmissionColor
                };
            }
        }

        public void PlayHitReaction(bool heavy)
        {
            float strength = hitSqueezeStrength * squeezeGlobalMultiplier;
            if (heavy)
            {
                strength *= heavyHitSqueezeMultiplier;
            }

            activeColorIntensity = heavy ? heavyHitColorIntensity : hitColorIntensity;
            activeEmissionIntensity = heavy ? heavyHitEmissionIntensity : hitEmissionIntensity;
            StartReaction(strength, hitRecoverDuration, true);
        }

        public void SetFlashColor(Color color)
        {
            flashColor = color;
        }

        public void PlayRecoil()
        {
            activeColorIntensity = recoilColorIntensity;
            activeEmissionIntensity = recoilEmissionIntensity;
            StartReaction(recoilSqueezeStrength, recoilDuration, false);
        }

        private void StartReaction(float squeezeStrength, float recoverDuration, bool flash)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(ReactionRoutine(squeezeStrength, recoverDuration, flash));
        }

        private IEnumerator ReactionRoutine(float squeezeStrength, float recoverDuration, bool flash)
        {
            Vector3 squeezedScale = new Vector3(
                baseScale.x * (1f + squeezeStrength),
                baseScale.y * (1f - squeezeStrength * 0.82f),
                baseScale.z * (1f + squeezeStrength));

            transform.localScale = squeezedScale;

            if (flash)
            {
                float tIn = 0f;
                while (tIn < flashInDuration)
                {
                    tIn += Time.deltaTime;
                    float lerp = flashInDuration <= 0f ? 1f : Mathf.Clamp01(tIn / flashInDuration);
                    ApplyFlashColor(lerp);
                    yield return null;
                }
            }

            float recoverTimer = 0f;
            while (recoverTimer < recoverDuration)
            {
                recoverTimer += Time.deltaTime;
                float lerp = recoverDuration <= 0f ? 1f : Mathf.Clamp01(recoverTimer / recoverDuration);
                transform.localScale = Vector3.Lerp(squeezedScale, baseScale, lerp);
                yield return null;
            }

            if (flash)
            {
                float tOut = 0f;
                while (tOut < flashOutDuration)
                {
                    tOut += Time.deltaTime;
                    float lerp = flashOutDuration <= 0f ? 1f : 1f - Mathf.Clamp01(tOut / flashOutDuration);
                    ApplyFlashColor(lerp);
                    yield return null;
                }
            }

            transform.localScale = baseScale;
            ApplyFlashColor(0f);
            activeRoutine = null;
        }

        private void ApplyFlashColor(float alpha)
        {
            for (int i = 0; i < snapshots.Length; i++)
            {
                MaterialSnapshot snapshot = snapshots[i];
                if (snapshot.material == null)
                {
                    continue;
                }

                // Crystal shader: use built-in flash support
                if (snapshot.hasCrystalFlash)
                {
                    snapshot.material.SetFloat("_UseFlashColor", alpha > 0.01f ? 1f : 0f);
                    Color intensifiedFlash = flashColor * Mathf.Max(1f, activeColorIntensity);
                    snapshot.material.SetColor("_FlashColor", Color.Lerp(snapshot.baseColor, intensifiedFlash, alpha));
                    continue;
                }

                Color standardFlash = flashColor * Mathf.Max(1f, activeColorIntensity);
                Color color = Color.Lerp(snapshot.baseColor, standardFlash, alpha);
                if (snapshot.hasBaseColor)
                {
                    snapshot.material.SetColor("_BaseColor", color);
                }

                if (snapshot.hasColor)
                {
                    snapshot.material.SetColor("_Color", color);
                }

                if (snapshot.hasTintColor)
                {
                    snapshot.material.SetColor("_TintColor", color);
                }

                if (snapshot.hasEmissionColor)
                {
                    Color emissiveTarget = flashColor * Mathf.Max(0f, activeEmissionIntensity);
                    Color emissive = Color.Lerp(snapshot.baseEmissionColor, emissiveTarget, alpha);
                    snapshot.material.SetColor("_EmissionColor", emissive);
                }
            }
        }
    }
}
