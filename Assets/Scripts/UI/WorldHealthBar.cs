using Sixty.Combat;
using Ia.Core.Update;
using UnityEngine;

namespace Sixty.UI
{
    public class WorldHealthBar : IaBehaviour
    {
        [SerializeField] private Health trackedHealth;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private float barWidth = 1.4f;
        [SerializeField] private float barHeight = 0.16f;
        [SerializeField] private Color fullColor = new Color(1f, 0.4f, 0.08f, 1f);
        [SerializeField] private Color lowColor = new Color(1f, 0.15f, 0.08f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);
        [SerializeField] private Color borderColor = new Color(0.3f, 0.3f, 0.35f, 0.9f);
        [SerializeField] private bool hideWhenFull = true;
        [SerializeField] private bool isPlayer;

        private GameObject barRoot;
        private Transform fillTransform;
        private SpriteRenderer fillRenderer;
        private SpriteRenderer bgRenderer;
        private SpriteRenderer borderRenderer;
        private MaterialPropertyBlock fillPropBlock;
        private float displayedRatio = 1f;
        private float smoothSpeed = 8f;
        private bool barCreated;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.UI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.LateUpdate;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            if (trackedHealth == null)
            {
                trackedHealth = GetComponentInParent<Health>();
            }

            if (trackedHealth == null)
            {
                trackedHealth = GetComponent<Health>();
            }
        }

        protected override void OnIaEnable()
        {
            if (!barCreated)
            {
                CreateBar();
                barCreated = true;
            }
        }

        protected override void OnIaDisable()
        {
            if (barRoot != null)
            {
                barRoot.SetActive(false);
            }
        }

        public override void OnIaLateUpdate(float deltaTime)
        {
            if (trackedHealth == null || barRoot == null)
            {
                return;
            }

            if (trackedHealth.IsDead)
            {
                barRoot.SetActive(false);
                return;
            }

            float targetRatio = trackedHealth.CurrentHealth / trackedHealth.MaxHealth;
            displayedRatio = Mathf.Lerp(displayedRatio, targetRatio, smoothSpeed * deltaTime);

            bool shouldShow = !hideWhenFull || displayedRatio < 0.995f;
            barRoot.SetActive(shouldShow);

            if (!shouldShow)
            {
                return;
            }

            // Billboard: face camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                barRoot.transform.position = trackedHealth.transform.position + offset;
                barRoot.transform.rotation = Quaternion.LookRotation(
                    barRoot.transform.position - cam.transform.position, Vector3.up);
            }

            // Update fill — scale width and anchor to left edge
            float clampedRatio = Mathf.Clamp01(displayedRatio);
            float fillWidth = barWidth * clampedRatio;
            fillTransform.localScale = new Vector3(fillWidth, barHeight - 0.03f, 1f);
            fillTransform.localPosition = new Vector3((fillWidth - barWidth) * 0.5f, 0f, -0.001f);

            // Color interpolation
            Color barColor = Color.Lerp(lowColor, fullColor, clampedRatio);
            if (isPlayer)
            {
                barColor = Color.Lerp(lowColor, new Color(0.3f, 0.85f, 1f, 1f), clampedRatio);
            }

            if (fillPropBlock == null)
            {
                fillPropBlock = new MaterialPropertyBlock();
            }

            fillRenderer.GetPropertyBlock(fillPropBlock);
            fillPropBlock.SetColor("_Color", barColor);
            fillRenderer.SetPropertyBlock(fillPropBlock);
        }

        public void SetTrackedHealth(Health health)
        {
            trackedHealth = health;
            displayedRatio = 1f;
        }

        public void SetIsPlayer(bool player)
        {
            isPlayer = player;
            if (isPlayer)
            {
                hideWhenFull = false;
                barWidth = 1.6f;
            }
        }

        private void CreateBar()
        {
            Sprite pixel = CreatePixelSprite();

            barRoot = new GameObject("HealthBar");
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = offset;

            // Border (slightly larger)
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(barRoot.transform, false);
            borderObj.transform.localScale = new Vector3(barWidth + 0.06f, barHeight + 0.06f, 1f);
            borderRenderer = borderObj.AddComponent<SpriteRenderer>();
            borderRenderer.sprite = pixel;
            borderRenderer.color = borderColor;
            borderRenderer.sortingOrder = 100;

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(barRoot.transform, false);
            bgObj.transform.localPosition = new Vector3(0f, 0f, -0.0005f);
            bgObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);
            bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = pixel;
            bgRenderer.color = backgroundColor;
            bgRenderer.sortingOrder = 101;

            // Fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(barRoot.transform, false);
            fillObj.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            fillObj.transform.localScale = new Vector3(barWidth, barHeight - 0.03f, 1f);
            fillTransform = fillObj.transform;
            fillRenderer = fillObj.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = pixel;
            fillRenderer.color = fullColor;
            fillRenderer.sortingOrder = 102;

            fillPropBlock = new MaterialPropertyBlock();
        }

        private static Sprite cachedPixel;

        private static Sprite CreatePixelSprite()
        {
            if (cachedPixel != null)
            {
                return cachedPixel;
            }

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < 16; i++)
            {
                pixels[i] = Color.white;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            cachedPixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return cachedPixel;
        }
    }
}
