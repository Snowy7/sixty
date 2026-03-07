using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sixty.UI
{
    public class SceneTransitionOverlay : MonoBehaviour
    {
        private static SceneTransitionOverlay instance;

        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float fadeInDuration = 0.6f;
        [SerializeField] private float holdBlackDuration = 0.15f;

        private Image fadeImage;
        private Canvas canvas;
        private Coroutine activeRoutine;

        public static SceneTransitionOverlay Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateOverlay();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void CreateOverlay()
        {
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject imageGo = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(transform, false);
            RectTransform rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            fadeImage = imageGo.GetComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;
        }

        /// <summary>
        /// Fade to black, load scene, fade from black.
        /// </summary>
        public void TransitionToScene(int buildIndex)
        {
            if (activeRoutine != null)
                return;
            activeRoutine = StartCoroutine(TransitionRoutine(buildIndex));
        }

        /// <summary>
        /// Fade to black, load scene by path, fade from black.
        /// </summary>
        public void TransitionToScene(string scenePath)
        {
            int index = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if (index >= 0)
                TransitionToScene(index);
            else
                Debug.LogWarning($"SceneTransitionOverlay: scene not in build settings: {scenePath}");
        }

        /// <summary>
        /// Start fully black and fade in. Call at scene start.
        /// </summary>
        public void FadeIn(Action onComplete = null)
        {
            if (activeRoutine != null)
                StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(FadeInRoutine(onComplete));
        }

        /// <summary>
        /// Fade to black only (no scene load). Useful before restart.
        /// </summary>
        public void FadeOut(Action onComplete = null)
        {
            if (activeRoutine != null)
                StopCoroutine(activeRoutine);
            activeRoutine = StartCoroutine(FadeOutRoutine(onComplete));
        }

        /// <summary>
        /// Ensure an instance exists. Creates one if needed.
        /// </summary>
        public static SceneTransitionOverlay EnsureInstance()
        {
            if (instance != null)
                return instance;

            GameObject go = new GameObject("SceneTransitionOverlay");
            return go.AddComponent<SceneTransitionOverlay>();
        }

        private IEnumerator TransitionRoutine(int buildIndex)
        {
            // Fade to black
            yield return FadeAlpha(0f, 1f, fadeOutDuration);

            // Hold
            yield return new WaitForSecondsRealtime(holdBlackDuration);

            // Load scene
            AsyncOperation op = SceneManager.LoadSceneAsync(buildIndex);
            if (op != null)
            {
                while (!op.isDone)
                    yield return null;
            }

            // Wait a frame for scene to initialize
            yield return null;

            // Fade from black
            yield return FadeAlpha(1f, 0f, fadeInDuration);
            activeRoutine = null;
        }

        private IEnumerator FadeInRoutine(Action onComplete)
        {
            SetAlpha(1f);
            yield return null; // wait one frame
            yield return FadeAlpha(1f, 0f, fadeInDuration);
            activeRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator FadeOutRoutine(Action onComplete)
        {
            yield return FadeAlpha(0f, 1f, fadeOutDuration);
            activeRoutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator FadeAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Smooth ease
                t = t * t * (3f - 2f * t);
                SetAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            if (fadeImage != null)
                fadeImage.color = new Color(0f, 0f, 0f, a);
        }
    }
}
