using UnityEngine;
using Ia.Core.Debugging;

namespace Ia.Core.Config
{
    [CreateAssetMenu(
        fileName = "IaGlobalSettings",
        menuName = "I.A Framework/Core/Global Settings"
    )]
    public class IaGlobalSettings : ScriptableObject
    {
        [Header("Debug / Logging")]
        public IaLogSettings logSettings;

        [Header("Update")]
        [Tooltip("Automatically create an IaUpdateManager if none exists in the scene.")]
        public bool autoCreateUpdateManager = true;

        [Header("Misc")]
        [Tooltip("Global time scale multiplier for I.A (applied on top of Unity's Time.timeScale).")]
        public float globalTimeScale = 1f;
    }
}