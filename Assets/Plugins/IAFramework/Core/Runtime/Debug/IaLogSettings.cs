using UnityEngine;

namespace Ia.Core.Debugging
{
    [CreateAssetMenu(
        fileName = "IaLogSettings",
        menuName = "I.A Framework/Core/Log Settings"
    )]
    public class IaLogSettings : ScriptableObject
    {
        [Header("Global")]
        public bool enableLogging = true;

        [Tooltip("Minimum level to log at all (inclusive).")]
        public IaLogLevel minimumLogLevel = IaLogLevel.Info;

        [Header("Category Filters")]
        public bool coreEnabled = true;
        public bool updateEnabled = true;
        public bool gameplayEnabled = true;
        public bool aiEnabled = true;
        public bool combatEnabled = true;
        public bool uiEnabled = true;
        public bool worldEnabled = true;
        public bool audioEnabled = true;
        public bool networkEnabled = true;
        public bool custom1Enabled = true;
        public bool custom2Enabled = true;

        public bool IsCategoryEnabled(IaLogCategory category)
        {
            return category switch
            {
                IaLogCategory.Core => coreEnabled,
                IaLogCategory.Update => updateEnabled,
                IaLogCategory.Gameplay => gameplayEnabled,
                IaLogCategory.AI => aiEnabled,
                IaLogCategory.Combat => combatEnabled,
                IaLogCategory.UI => uiEnabled,
                IaLogCategory.World => worldEnabled,
                IaLogCategory.Audio => audioEnabled,
                IaLogCategory.Network => networkEnabled,
                IaLogCategory.Custom1 => custom1Enabled,
                IaLogCategory.Custom2 => custom2Enabled,
                _ => true
            };
        }
    }
}