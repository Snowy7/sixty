using System.Collections;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class HitFlashSquash : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Renderer[] targetRenderers;

        [Header("Hit")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashInDuration = 0.03f;
        [SerializeField] private float flashOutDuration = 0.12f;
        [SerializeField] private float hitSqueezeStrength = 0.22f;
        [SerializeField] private float hitRecoverDuration = 0.16f;

        [Header("Shot Recoil")]
        [SerializeField] private float recoilSqueezeStrength = 0.11f;
        [SerializeField] private float recoilDuration = 0.11f;

        private struct MaterialSnapshot
        {
            public Material material;
            public bool hasBaseColor;
            public bool hasColor;
            public Color baseColor;
        }

        private MaterialSnapshot[] snapshots;
        private Vector3 baseScale;
        private Coroutine activeRoutine;

        private void Awake()
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
                Color baseColor = hasBaseColor ? mat.GetColor("_BaseColor") : (hasColor ? mat.GetColor("_Color") : Color.white);

                snapshots[i] = new MaterialSnapshot
                {
                    material = mat,
                    hasBaseColor = hasBaseColor,
                    hasColor = hasColor,
                    baseColor = baseColor
                };
            }
        }

        public void PlayHitReaction(bool heavy)
        {
            float strength = heavy ? hitSqueezeStrength * 1.75f : hitSqueezeStrength;
            StartReaction(strength, hitRecoverDuration, true);
        }

        public void PlayRecoil()
        {
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

                Color color = Color.Lerp(snapshot.baseColor, flashColor, alpha);
                if (snapshot.hasBaseColor)
                {
                    snapshot.material.SetColor("_BaseColor", color);
                }

                if (snapshot.hasColor)
                {
                    snapshot.material.SetColor("_Color", color);
                }
            }
        }
    }
}
