using Sixty.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Sixty.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string playScenePath = "Assets/SixtyGenerated/Scenes/Sixty_Playable.unity";

        private bool transitioning;

        private void Start()
        {
            SceneTransitionOverlay.EnsureInstance();
            SceneTransitionOverlay.Instance?.FadeIn();
        }

        private void OnEnable()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return;

            VisualElement root = uiDocument.rootVisualElement;

            Button playButton = root.Q<Button>("play-button");
            if (playButton != null)
                playButton.clicked += OnPlayClicked;

            Button quitButton = root.Q<Button>("quit-button");
            if (quitButton != null)
                quitButton.clicked += OnQuitClicked;

            PopulateMetaStats(root);
        }

        private static void PopulateMetaStats(VisualElement root)
        {
            MetaProgressionSnapshot snapshot = SixtyMetaProgression.GetSnapshot(0);

            Label deaths = root.Q<Label>("meta-deaths");
            if (deaths != null)
            {
                deaths.text = snapshot.DeathCount > 0
                    ? $"DEATHS: {snapshot.DeathCount}"
                    : "";
            }

            Label clears = root.Q<Label>("meta-clears");
            if (clears != null)
            {
                clears.text = snapshot.BossClears > 0
                    ? $"BOSS CLEARS: {snapshot.BossClears}"
                    : "";
            }
        }

        private void OnPlayClicked()
        {
            if (transitioning)
                return;
            transitioning = true;

            SceneTransitionOverlay overlay = SceneTransitionOverlay.EnsureInstance();
            int sceneIndex = SceneUtility.GetBuildIndexByScenePath(playScenePath);
            if (sceneIndex < 0 && SceneManager.sceneCountInBuildSettings > 1)
                sceneIndex = 1;

            if (sceneIndex >= 0)
            {
                overlay.TransitionToScene(sceneIndex);
            }
            else
            {
                transitioning = false;
                Debug.LogWarning("MainMenu: Play scene not found in build settings.");
            }
        }

        private static void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
