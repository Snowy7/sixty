using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sixty.UI
{
    [RequireComponent(typeof(Image))]
    public class ScreenFlashOverlay : MonoBehaviour
    {
        [SerializeField] private Image overlayImage;
        private Coroutine activeFlashRoutine;

        private void Awake()
        {
            if (overlayImage == null)
            {
                overlayImage = GetComponent<Image>();
            }

            overlayImage.raycastTarget = false;
            SetAlphaImmediate(0f);
        }

        public void Flash(Color color, float peakAlpha, float duration)
        {
            if (overlayImage == null)
            {
                return;
            }

            if (activeFlashRoutine != null)
            {
                StopCoroutine(activeFlashRoutine);
            }

            activeFlashRoutine = StartCoroutine(FlashRoutine(color, peakAlpha, duration));
        }

        private IEnumerator FlashRoutine(Color color, float peakAlpha, float duration)
        {
            float half = Mathf.Max(0.01f, duration * 0.5f);
            float timer = 0f;

            while (timer < half)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / half);
                Color c = color;
                c.a = Mathf.Lerp(0f, peakAlpha, t);
                overlayImage.color = c;
                yield return null;
            }

            timer = 0f;
            while (timer < half)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / half);
                Color c = color;
                c.a = Mathf.Lerp(peakAlpha, 0f, t);
                overlayImage.color = c;
                yield return null;
            }

            SetAlphaImmediate(0f);
            activeFlashRoutine = null;
        }

        private void SetAlphaImmediate(float alpha)
        {
            Color c = overlayImage.color;
            c.a = alpha;
            overlayImage.color = c;
        }
    }
}
