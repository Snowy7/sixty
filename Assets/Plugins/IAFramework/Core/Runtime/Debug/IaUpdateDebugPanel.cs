using UnityEngine;
using UnityEngine.InputSystem;

namespace Ia.Core.Update
{
    /// <summary>
    /// Simple OnGUI-based debug panel for IaUpdateManager.
    /// Toggle with F2 (configurable).
    /// </summary>
    public class IaUpdateDebugPanel : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.F2;
        [SerializeField] private bool startVisibleInEditor = true;

        private bool m_visible;

        private void Start()
        {
#if UNITY_EDITOR
            m_visible = startVisibleInEditor;
#else
            m_visible = false;
#endif
        }

        private void Update()
        {
            if (Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                m_visible = !m_visible;
            }
        }

        private void OnGUI()
        {
            if (!m_visible)
                return;

            var manager = IaUpdateManager.Instance;
            if (manager == null)
            {
                GUILayout.BeginArea(new Rect(10, 10, 300, 60), "I.A Update", GUI.skin.window);
                GUILayout.Label("No IaUpdateManager found.");
                GUILayout.EndArea();
                return;
            }

            const float width = 340f;
            const float height = 260f;
            GUILayout.BeginArea(new Rect(10, 10, width, height), "I.A Update", GUI.skin.window);

            GUILayout.Label($"FPS: {(1f / Time.smoothDeltaTime):0}");

            DrawGroupToggle(manager, IaUpdateGroup.Player);
            DrawGroupToggle(manager, IaUpdateGroup.AI);
            DrawGroupToggle(manager, IaUpdateGroup.World);
            DrawGroupToggle(manager, IaUpdateGroup.UI);
            DrawGroupToggle(manager, IaUpdateGroup.FX);
            DrawGroupToggle(manager, IaUpdateGroup.Custom1);
            DrawGroupToggle(manager, IaUpdateGroup.Custom2);

            GUILayout.Space(8);
            GUILayout.Label("Behaviour Counts (per phase):");

            DrawPhaseCount(manager, IaUpdatePhase.Update);
            DrawPhaseCount(manager, IaUpdatePhase.FixedUpdate);
            DrawPhaseCount(manager, IaUpdatePhase.LateUpdate);

            GUILayout.EndArea();
        }

        private void DrawGroupToggle(IaUpdateManager manager, IaUpdateGroup group)
        {
            bool enabled = manager.IsGroupEnabled(group);
            GUILayout.BeginHorizontal();
            bool newEnabled = GUILayout.Toggle(enabled, group.ToString());
            if (newEnabled != enabled)
            {
                manager.SetGroupEnabled(group, newEnabled);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPhaseCount(IaUpdateManager manager, IaUpdatePhase phase)
        {
            int total = manager.GetBehaviourCount(phase);
            GUILayout.Label($"{phase}: {total} behaviours");
        }
    }
}
