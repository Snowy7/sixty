using Ia.Core.Update;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class PostProcessFeedback : IaBehaviour
    {
        [Header("References")]
        [SerializeField] private UnityEngine.Rendering.Volume volume;

        [Header("Low Time")]
        [SerializeField] private float lowTimeRampStart = 45f;
        [SerializeField] private float lowTimeThreshold = 10f;
        [SerializeField] private float lowTimeCurvePower = 2.2f;
        [SerializeField] private float maxBaseVignette = 0.18f;
        [SerializeField] private float lowTimeVignetteBoost = 0.14f;
        [SerializeField] private float lowTimeSaturationPenalty = 18f;
        [SerializeField] private float lowTimeSmoothing = 4.5f;

        [Header("Pulse Decay")]
        [SerializeField] private float chromaticDecayPerSecond = 2.6f;
        [SerializeField] private float lensDecayPerSecond = 3.2f;
        [SerializeField] private float vignetteDecayPerSecond = 3.3f;
        [SerializeField] private float flashDecayPerSecond = 5.2f;

        private UnityEngine.Rendering.Universal.Vignette vignette;
        private UnityEngine.Rendering.Universal.ChromaticAberration chromatic;
        private UnityEngine.Rendering.Universal.LensDistortion lens;
        private UnityEngine.Rendering.Universal.ColorAdjustments colorAdjustments;

        private float baseVignette;
        private float baseSaturation;
        private float baseChromatic;
        private float baseLens;
        private Color baseColorFilter = Color.white;

        private float chromaticPulse;
        private float vignettePulse;
        private float lensPulse;
        private float flashStrength;
        private Color flashColor = Color.white;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.FX;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            ResolveOverrides();
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (volume == null || vignette == null || chromatic == null || lens == null || colorAdjustments == null)
            {
                ResolveOverrides();
            }

            float lowTimeFactor = 0f;
            if (Sixty.Core.TimeManager.Instance != null && lowTimeThreshold > 0.01f)
            {
                float rampStart = Mathf.Max(lowTimeThreshold + 0.01f, lowTimeRampStart);
                float normalized = Mathf.InverseLerp(rampStart, lowTimeThreshold, Sixty.Core.TimeManager.Instance.TimeRemaining);
                lowTimeFactor = Mathf.Pow(1f - normalized, Mathf.Max(0.01f, lowTimeCurvePower));
            }

            if (vignette != null)
            {
                float gameplayBaseVignette = Mathf.Min(baseVignette, maxBaseVignette);
                float targetVignette = gameplayBaseVignette + (lowTimeFactor * lowTimeVignetteBoost) + vignettePulse;
                vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, Mathf.Clamp01(targetVignette), lowTimeSmoothing * deltaTime);
            }

            if (colorAdjustments != null)
            {
                float targetSaturation = baseSaturation - (lowTimeFactor * lowTimeSaturationPenalty);
                colorAdjustments.saturation.value = Mathf.Lerp(colorAdjustments.saturation.value, targetSaturation, lowTimeSmoothing * deltaTime);
                colorAdjustments.colorFilter.value = Color.Lerp(baseColorFilter, flashColor, Mathf.Clamp01(flashStrength));
            }

            if (chromatic != null)
            {
                chromatic.intensity.value = Mathf.Lerp(chromatic.intensity.value, Mathf.Clamp01(baseChromatic + chromaticPulse), lowTimeSmoothing * deltaTime);
            }

            if (lens != null)
            {
                lens.intensity.value = Mathf.Lerp(lens.intensity.value, Mathf.Clamp(baseLens + lensPulse, -1f, 1f), lowTimeSmoothing * deltaTime);
            }

            chromaticPulse = Mathf.MoveTowards(chromaticPulse, 0f, chromaticDecayPerSecond * deltaTime);
            vignettePulse = Mathf.MoveTowards(vignettePulse, 0f, vignetteDecayPerSecond * deltaTime);
            lensPulse = Mathf.MoveTowards(lensPulse, 0f, lensDecayPerSecond * deltaTime);
            flashStrength = Mathf.MoveTowards(flashStrength, 0f, flashDecayPerSecond * deltaTime);
        }

        public void OnShot()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.03f);
        }

        public void OnEnemyHit(bool killed)
        {
            chromaticPulse = Mathf.Max(chromaticPulse, killed ? 0.18f : 0.1f);
            vignettePulse = Mathf.Max(vignettePulse, killed ? 0.08f : 0.04f);
        }

        public void OnPlayerDamaged()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.24f);
            vignettePulse = Mathf.Max(vignettePulse, 0.2f);
            TriggerFlash(new Color(1f, 0.24f, 0.2f, 1f), 0.55f);
        }

        public void OnDash()
        {
            lensPulse = Mathf.Min(lensPulse, -0.2f);
            chromaticPulse = Mathf.Max(chromaticPulse, 0.08f);
        }

        public void OnPickup()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.06f);
            vignettePulse = Mathf.Max(vignettePulse, 0.05f);
            TriggerFlash(new Color(1f, 0.9f, 0.45f, 1f), 0.22f);
        }

        public void OnRoomClear()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.12f);
            vignettePulse = Mathf.Max(vignettePulse, 0.09f);
            TriggerFlash(new Color(0.68f, 1f, 0.74f, 1f), 0.16f);
        }

        public void OnRunWin()
        {
            chromaticPulse = Mathf.Max(chromaticPulse, 0.28f);
            vignettePulse = Mathf.Max(vignettePulse, 0.2f);
            lensPulse = Mathf.Min(lensPulse, -0.18f);
            TriggerFlash(new Color(0.75f, 1f, 0.82f, 1f), 0.26f);
        }

        public void OnBossPhaseShift(int phase)
        {
            float phaseBoost = Mathf.Clamp(phase, 1, 3) * 0.05f;
            chromaticPulse = Mathf.Max(chromaticPulse, 0.18f + phaseBoost);
            vignettePulse = Mathf.Max(vignettePulse, 0.1f + (phaseBoost * 0.75f));
            lensPulse = Mathf.Min(lensPulse, -0.08f - (phaseBoost * 0.3f));
            TriggerFlash(
                phase >= 3 ? new Color(1f, 0.26f, 0.22f, 1f) : new Color(1f, 0.58f, 0.28f, 1f),
                0.24f + (phaseBoost * 0.5f));
        }

        private void TriggerFlash(Color color, float intensity)
        {
            flashColor = color;
            flashStrength = Mathf.Max(flashStrength, intensity);
        }

        private void ResolveOverrides()
        {
            if (volume == null)
            {
                volume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            }

            if (volume == null || volume.profile == null)
            {
                return;
            }

            volume.profile.TryGet(out vignette);
            volume.profile.TryGet(out chromatic);
            volume.profile.TryGet(out lens);
            volume.profile.TryGet(out colorAdjustments);

            if (vignette != null)
            {
                baseVignette = vignette.intensity.value;
            }

            if (chromatic != null)
            {
                baseChromatic = chromatic.intensity.value;
            }

            if (lens != null)
            {
                baseLens = lens.intensity.value;
            }

            if (colorAdjustments != null)
            {
                baseSaturation = colorAdjustments.saturation.value;
                baseColorFilter = colorAdjustments.colorFilter.value;
            }
        }
    }
}
