using Ia.Core.Update;
using Sixty.Combat;
using Sixty.Player;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class GroundGridInfluenceController : IaBehaviour
    {
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private int maxTrackedEnemies = 64;
        [SerializeField] private float refreshInterval = 0.1f;
        [SerializeField] private float influenceRadius = 3.4f;
        [SerializeField] private float falloffExponent = 1.5f;
        [SerializeField] private bool applyPlayerHighlight = true;

        private Texture2D enemyPositionsTexture;
        private Color[] enemyData;
        private MaterialPropertyBlock propertyBlock;
        private float nextRefreshAt;
        private static readonly int EnemyPositionsTexId = Shader.PropertyToID("_EnemyPositionsTex");
        private static readonly int EnemyCountId = Shader.PropertyToID("_EnemyCount");
        private static readonly int InfluenceRadiusId = Shader.PropertyToID("_InfluenceRadius");
        private static readonly int FalloffExponentId = Shader.PropertyToID("_FalloffExponent");
        private static readonly int LightPosId = Shader.PropertyToID("_PlayerPos");

        public void SetRuntimeRenderers(Renderer[] renderers)
        {
            targetRenderers = renderers;
        }

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.FX;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.Update;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaAwake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(true);
            }

            maxTrackedEnemies = Mathf.Clamp(maxTrackedEnemies, 8, 512);
            enemyData = new Color[maxTrackedEnemies];
            enemyPositionsTexture = new Texture2D(maxTrackedEnemies, 1, TextureFormat.RGBAFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            propertyBlock = new MaterialPropertyBlock();
            PushToRenderers(0, Vector3.zero);
        }

        public override void OnIaUpdate(float deltaTime)
        {
            if (Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

            int count = FillEnemyPositions();
            Vector3 playerPos = Vector3.zero;
            if (applyPlayerHighlight)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    playerPos = player.transform.position;
                }
            }

            PushToRenderers(count, playerPos);
        }

        protected override void OnIaDestroy()
        {
            if (enemyPositionsTexture != null)
            {
                Destroy(enemyPositionsTexture);
                enemyPositionsTexture = null;
            }
        }

        private int FillEnemyPositions()
        {
            int count = 0;
            Health[] allHealth = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < allHealth.Length; i++)
            {
                Health health = allHealth[i];
                if (health == null || health.IsDead)
                {
                    continue;
                }

                Transform root = health.transform.root != null ? health.transform.root : health.transform;
                if (root.GetComponent<PlayerController>() != null)
                {
                    continue;
                }

                Vector3 pos = root.position;
                enemyData[count] = new Color(pos.x, pos.z, 0f, 1f);
                count++;
                if (count >= maxTrackedEnemies)
                {
                    break;
                }
            }

            for (int i = count; i < maxTrackedEnemies; i++)
            {
                enemyData[i] = new Color(-100000f, -100000f, 0f, 1f);
            }

            enemyPositionsTexture.SetPixels(enemyData);
            enemyPositionsTexture.Apply(false, false);
            return count;
        }

        private void PushToRenderers(int enemyCount, Vector3 playerPos)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetTexture(EnemyPositionsTexId, enemyPositionsTexture);
                propertyBlock.SetFloat(EnemyCountId, enemyCount);
                propertyBlock.SetFloat(InfluenceRadiusId, influenceRadius);
                propertyBlock.SetFloat(FalloffExponentId, falloffExponent);
                if (applyPlayerHighlight)
                {
                    propertyBlock.SetVector(LightPosId, new Vector4(playerPos.x, 0f, playerPos.z, 0f));
                }

                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
