using Ia.Core.Pooling;
using Ia.Core.Update;
using UnityEngine;

namespace Ia.Gameplay.Combat
{
    public sealed class IaTracer : IaPooledObject
    {
        [SerializeField] float defaultTravelSeconds = 0.06f;
        [SerializeField] TrailRenderer trailRenderer;
        [SerializeField] MeshRenderer meshRenderer;
        [SerializeField] float timeBeforeDespawn = 2f;

        Vector3 m_start;
        Vector3 m_end;
        float m_duration;
        float m_t;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.FX;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;

        public void Play(Vector3 start, Vector3 end, float travelSeconds)
        {
            m_start = start;
            m_end = end;
            m_duration = Mathf.Max(0.01f, travelSeconds > 0 ? travelSeconds : defaultTravelSeconds);
            m_t = 0f;

            transform.position = start;
            transform.LookAt(end);
        }

        public override void OnSpawned()
        {
            // Make sure we start clean each spawn
            m_t = 0f;
            
            // Clear trail renderer history
            if (trailRenderer != null)
                trailRenderer.Clear();
        }

        public override void OnIaUpdate(float deltaTime)
        {
            m_t += deltaTime;
            float a = Mathf.Clamp01(m_t / m_duration);

            transform.position = Vector3.Lerp(m_start, m_end, a);

            if (a >= 1f)
            {
                // Start despawn timer
                Invoke(nameof(Despawn), timeBeforeDespawn);
                
                // Hide mesh
                if (meshRenderer != null)
                    meshRenderer.enabled = false;
            }
        }
    }
}