#if UNITY_EDITOR
using Sixty.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Sixty.EditorTools
{
    public static class SixtyMainMenuBuilder
    {
        private const string GeneratedRoot = "Assets/SixtyGenerated";
        private const string ScenesFolder = GeneratedRoot + "/Scenes";
        private const string MenuScenePath = ScenesFolder + "/Sixty_MainMenu.unity";
        private const string PlayScenePath = ScenesFolder + "/Sixty_Playable.unity";

        [MenuItem("Tools/Sixty/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                if (!AssetDatabase.IsValidFolder(GeneratedRoot))
                    AssetDatabase.CreateFolder("Assets", "SixtyGenerated");
                AssetDatabase.CreateFolder(GeneratedRoot, "Scenes");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMenu(scene);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            AddSceneToBuildSettings(MenuScenePath, 0);
            AddSceneToBuildSettings(PlayScenePath, 1);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "SIXTY Main Menu",
                $"Created {MenuScenePath}.\nBuild settings updated: MainMenu at index 0, Playable at index 1.",
                "OK");
        }

        private static void BuildMenu(Scene scene)
        {
            // Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.035f, 0.05f);
            cam.orthographic = false;
            cam.transform.position = new Vector3(0f, 5f, -10f);
            cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            // Optional post-processing
            GameObject ppGo = new GameObject("PostProcess");
            Volume volume = ppGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            VolumeProfile profile = new VolumeProfile();
            Bloom bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(0.9f);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.35f);
            vignette.color.Override(Color.black);
            volume.profile = profile;

            // Light
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.65f, 0.8f);
            light.intensity = 0.3f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightGo.AddComponent<UniversalAdditionalLightData>();

            // Panel settings with transparent background
            PanelSettings panelSettings = CreateOrLoadPanelSettings();

            // UI Document with main menu
            VisualTreeAsset menuAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu.uxml");

            GameObject menuGo = new GameObject("MainMenu");
            UIDocument uiDoc = menuGo.AddComponent<UIDocument>();
            uiDoc.panelSettings = panelSettings;
            if (menuAsset != null)
            {
                uiDoc.visualTreeAsset = menuAsset;
                uiDoc.sortingOrder = 100;
            }

            // Scene transition overlay (persists across scenes)
            GameObject transitionGo = new GameObject("SceneTransitionOverlay");
            transitionGo.AddComponent<Sixty.UI.SceneTransitionOverlay>();

            MainMenuController controller = menuGo.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("uiDocument").objectReferenceValue = uiDoc;
            so.FindProperty("playScenePath").stringValue = PlayScenePath;
            so.ApplyModifiedPropertiesWithoutUndo();

            SceneManager.SetActiveScene(scene);
        }

        private static PanelSettings CreateOrLoadPanelSettings()
        {
            string path = GeneratedRoot + "/UI_PanelSettings.asset";
            PanelSettings existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (existing != null)
                return existing;

            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.clearColor = false;

            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        private static void AddSceneToBuildSettings(string scenePath, int preferredIndex)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            // Check if already present
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == scenePath)
                    return;
            }

            EditorBuildSettingsScene newScene = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[scenes.Length + 1];

            int insertAt = Mathf.Clamp(preferredIndex, 0, scenes.Length);
            for (int i = 0; i < insertAt; i++)
                updated[i] = scenes[i];
            updated[insertAt] = newScene;
            for (int i = insertAt; i < scenes.Length; i++)
                updated[i + 1] = scenes[i];

            EditorBuildSettings.scenes = updated;
        }
    }
}
#endif
