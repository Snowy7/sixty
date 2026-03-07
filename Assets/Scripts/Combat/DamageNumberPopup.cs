using UnityEngine;

namespace Sixty.Combat
{
    public class DamageNumberPopup : MonoBehaviour
    {
        private static readonly Color NormalColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color CritColor = new Color(1f, 0.85f, 0.3f, 1f);
        private const float Duration = 0.7f;
        private const float RiseSpeed = 2.2f;
        private const float FadeStart = 0.35f;
        private const float ScaleStart = 0.6f;
        private const float ScalePeak = 1f;
        private const float ScalePopDuration = 0.1f;
        private const float SpreadRange = 0.4f;

        private TextMesh textMesh;
        private float elapsed;
        private Vector3 velocity;
        private Color baseColor;

        public static void Spawn(Vector3 worldPosition, float damage, bool isKill)
        {
            GameObject go = new GameObject("DmgNum");
            go.transform.position = worldPosition + new Vector3(
                Random.Range(-SpreadRange, SpreadRange),
                0.5f,
                Random.Range(-SpreadRange, SpreadRange));

            DamageNumberPopup popup = go.AddComponent<DamageNumberPopup>();
            popup.Init(damage, isKill);
        }

        private void Init(float damage, bool isKill)
        {
            textMesh = gameObject.AddComponent<TextMesh>();
            textMesh.text = Mathf.CeilToInt(damage).ToString();
            textMesh.characterSize = 0.15f;
            textMesh.fontSize = isKill ? 64 : 48;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontStyle = FontStyle.Bold;

            baseColor = isKill ? CritColor : NormalColor;
            textMesh.color = baseColor;

            velocity = new Vector3(
                Random.Range(-0.3f, 0.3f),
                RiseSpeed,
                Random.Range(-0.3f, 0.3f));
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Duration)
            {
                Destroy(gameObject);
                return;
            }

            // Billboard toward camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    transform.position - cam.transform.position, Vector3.up);
            }

            // Move upward with deceleration
            velocity.y = Mathf.Max(0.3f, velocity.y - 4f * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;

            // Scale pop
            float t = elapsed / Duration;
            float scale;
            if (elapsed < ScalePopDuration)
            {
                scale = Mathf.Lerp(ScaleStart, ScalePeak, elapsed / ScalePopDuration);
            }
            else
            {
                scale = Mathf.Lerp(ScalePeak, ScaleStart * 0.8f, (elapsed - ScalePopDuration) / (Duration - ScalePopDuration));
            }
            transform.localScale = Vector3.one * scale;

            // Fade out
            if (elapsed > FadeStart)
            {
                float fadeT = (elapsed - FadeStart) / (Duration - FadeStart);
                Color c = baseColor;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                textMesh.color = c;
            }
        }
    }
}
