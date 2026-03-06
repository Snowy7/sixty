using Ia.Core.Events;
using Ia.Core.Update;
using Sixty.Core;
using System;
using UnityEngine;

namespace Sixty.Gameplay
{
    public class RoomLayoutDirector : IaBehaviour
    {
        [SerializeField] private RunDirector runDirector;
        [Header("Procedural")]
        [SerializeField] private bool useProceduralGeneration = true;
        [SerializeField] private Transform proceduralRoot;
        [SerializeField] private Material coverMaterial;
        [SerializeField] private Material accentMaterial;
        [SerializeField] private float arenaHalfExtent = 20f;
        [SerializeField] private float roomEdgePadding = 3.2f;
        [SerializeField] private int combatObstacleMin = 7;
        [SerializeField] private int combatObstacleMax = 12;
        [SerializeField] private int riskObstacleMin = 11;
        [SerializeField] private int riskObstacleMax = 16;
        [SerializeField] private int seedSalt = 7331;

        [Header("Fallback Static Layouts")]
        [SerializeField] private GameObject[] combatLayouts;
        [SerializeField] private GameObject[] rewardLayouts;
        [SerializeField] private GameObject[] riskLayouts;
        [SerializeField] private GameObject[] bossLayouts;
        [SerializeField] private bool deterministicSelectionByRoom = true;

        private int currentRoom;
        private RunDirector.RoomType currentRoomType = RunDirector.RoomType.Combat;
        private Vector3 currentRoomOrigin;

        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.World;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.None;
        protected override bool UseOrderedLifecycle => false;

        protected override void OnIaEnable()
        {
            if (runDirector == null)
            {
                runDirector = FindFirstObjectByType<RunDirector>();
            }

            currentRoom = runDirector != null ? Mathf.Max(1, runDirector.CurrentRoom) : 1;
            currentRoomType = runDirector != null ? runDirector.CurrentRoomType : RunDirector.RoomType.Combat;
            currentRoomOrigin = runDirector != null ? runDirector.CurrentRoomOrigin : Vector3.zero;

            IaEventBus.Subscribe<RoomChangedEvent>(OnRoomChangedEvent);
            IaEventBus.Subscribe<RoomTypeChangedEvent>(OnRoomTypeChangedEvent);
            ActivateCurrentLayout();
        }

        protected override void OnIaDisable()
        {
            IaEventBus.Unsubscribe<RoomChangedEvent>(OnRoomChangedEvent);
            IaEventBus.Unsubscribe<RoomTypeChangedEvent>(OnRoomTypeChangedEvent);
        }

        private void OnRoomChangedEvent(RoomChangedEvent evt)
        {
            currentRoom = Mathf.Max(1, evt.Room);
            if (runDirector != null)
            {
                currentRoomOrigin = runDirector.CurrentRoomOrigin;
            }
            ActivateCurrentLayout();
        }

        private void OnRoomTypeChangedEvent(RoomTypeChangedEvent evt)
        {
            currentRoom = Mathf.Max(1, evt.Room);
            currentRoomType = (RunDirector.RoomType)evt.RoomType;
            if (runDirector != null)
            {
                currentRoomOrigin = runDirector.CurrentRoomOrigin;
            }
            ActivateCurrentLayout();
        }

        private void ActivateCurrentLayout()
        {
            if (useProceduralGeneration)
            {
                GenerateProceduralLayout();
                SetActiveLayoutGroup(combatLayouts, null);
                SetActiveLayoutGroup(rewardLayouts, null);
                SetActiveLayoutGroup(riskLayouts, null);
                SetActiveLayoutGroup(bossLayouts, null);
                return;
            }

            GameObject[] candidates = GetLayoutsForRoomType(currentRoomType);
            GameObject selected = SelectLayout(candidates, currentRoom, currentRoomType);

            SetActiveLayoutGroup(combatLayouts, selected);
            SetActiveLayoutGroup(rewardLayouts, selected);
            SetActiveLayoutGroup(riskLayouts, selected);
            SetActiveLayoutGroup(bossLayouts, selected);
        }

        private void GenerateProceduralLayout()
        {
            EnsureProceduralRoot();
            ClearProceduralRoot();

            int seed = (currentRoom * 73856093) ^ (((int)currentRoomType + 1) * 19349663) ^ seedSalt;
            if (seed == int.MinValue)
            {
                seed = int.MaxValue;
            }

            System.Random rng = new System.Random(Math.Abs(seed));
            switch (currentRoomType)
            {
                case RunDirector.RoomType.Reward:
                    GenerateRewardLayout(rng);
                    break;
                case RunDirector.RoomType.Risk:
                    GenerateRiskLayout(rng);
                    break;
                case RunDirector.RoomType.Boss:
                    GenerateBossLayout();
                    break;
                default:
                    GenerateCombatLayout(rng);
                    break;
            }
        }

        private void GenerateCombatLayout(System.Random rng)
        {
            int count = NextInt(rng, Mathf.Max(1, combatObstacleMin), Mathf.Max(combatObstacleMin, combatObstacleMax) + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 scale = new Vector3(
                    NextFloat(rng, 1.2f, 4.2f),
                    NextFloat(rng, 1.4f, 2.3f),
                    NextFloat(rng, 1.2f, 4.2f));
                Vector3 position = GetSafePosition(rng, scale);
                position.y = scale.y * 0.5f;
                CreateProceduralBlock($"Combat_{i:00}", position + currentRoomOrigin, scale, coverMaterial, true);
            }
        }

        private void GenerateRiskLayout(System.Random rng)
        {
            CreateProceduralBlock("RiskLane_North", new Vector3(0f, 0.95f, 12f) + currentRoomOrigin, new Vector3(10.5f, 1.9f, 1.2f), coverMaterial, true);
            CreateProceduralBlock("RiskLane_South", new Vector3(0f, 0.95f, -12f) + currentRoomOrigin, new Vector3(10.5f, 1.9f, 1.2f), coverMaterial, true);
            CreateProceduralBlock("RiskLane_East", new Vector3(12f, 0.95f, 0f) + currentRoomOrigin, new Vector3(1.2f, 1.9f, 10.5f), coverMaterial, true);
            CreateProceduralBlock("RiskLane_West", new Vector3(-12f, 0.95f, 0f) + currentRoomOrigin, new Vector3(1.2f, 1.9f, 10.5f), coverMaterial, true);

            int count = NextInt(rng, Mathf.Max(1, riskObstacleMin), Mathf.Max(riskObstacleMin, riskObstacleMax) + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 scale = new Vector3(
                    NextFloat(rng, 1.1f, 3f),
                    NextFloat(rng, 1.2f, 2f),
                    NextFloat(rng, 1.1f, 3f));
                Vector3 position = GetSafePosition(rng, scale);
                position.y = scale.y * 0.5f;
                CreateProceduralBlock($"Risk_{i:00}", position + currentRoomOrigin, scale, coverMaterial, true);
            }
        }

        private void GenerateRewardLayout(System.Random rng)
        {
            CreateProceduralBlock("RewardPad", new Vector3(0f, 0.08f, 0f) + currentRoomOrigin, new Vector3(11f, 0.12f, 11f), accentMaterial, false);

            float radius = 7.5f;
            for (int i = 0; i < 3; i++)
            {
                float angle = (Mathf.PI * 2f * i / 3f) + NextFloat(rng, -0.12f, 0.12f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0.65f, Mathf.Sin(angle) * radius);
                CreateProceduralBlock($"RewardPedestal_{i + 1}", pos + currentRoomOrigin, new Vector3(1.8f, 1.3f, 1.8f), accentMaterial, true);
            }
        }

        private void GenerateBossLayout()
        {
            CreateProceduralBlock("BossPillar_NE", new Vector3(14f, 0.95f, 14f) + currentRoomOrigin, new Vector3(3f, 1.9f, 3f), accentMaterial, true);
            CreateProceduralBlock("BossPillar_NW", new Vector3(-14f, 0.95f, 14f) + currentRoomOrigin, new Vector3(3f, 1.9f, 3f), accentMaterial, true);
            CreateProceduralBlock("BossPillar_SE", new Vector3(14f, 0.95f, -14f) + currentRoomOrigin, new Vector3(3f, 1.9f, 3f), accentMaterial, true);
            CreateProceduralBlock("BossPillar_SW", new Vector3(-14f, 0.95f, -14f) + currentRoomOrigin, new Vector3(3f, 1.9f, 3f), accentMaterial, true);
            CreateProceduralBlock("BossCenterMark", new Vector3(0f, 0.06f, 0f) + currentRoomOrigin, new Vector3(7.8f, 0.08f, 7.8f), accentMaterial, false);
        }

        private void EnsureProceduralRoot()
        {
            if (proceduralRoot != null)
            {
                return;
            }

            GameObject root = new GameObject("ProceduralRoomGeometry");
            root.transform.SetParent(transform, false);
            proceduralRoot = root.transform;
        }

        private void ClearProceduralRoot()
        {
            if (proceduralRoot == null)
            {
                return;
            }

            for (int i = proceduralRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = proceduralRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void CreateProceduralBlock(string name, Vector3 position, Vector3 scale, Material material, bool withCollider)
        {
            if (proceduralRoot == null)
            {
                return;
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(proceduralRoot, false);
            root.transform.localPosition = position;

            // Main collider block
            GameObject mainBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mainBlock.name = "Main";
            mainBlock.transform.SetParent(root.transform, false);
            mainBlock.transform.localScale = scale;
            ConfigureBlockRenderer(mainBlock, material);

            if (!withCollider)
            {
                Collider collider = mainBlock.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }

            // Add voxel detail cubes for visual complexity
            if (scale.x > 1f && scale.z > 1f)
            {
                int detailSeed = name.GetHashCode();
                System.Random detailRng = new System.Random(detailSeed);

                int detailCount = 1 + (int)(Mathf.Min(scale.x, scale.z) * 0.6f);
                for (int i = 0; i < detailCount; i++)
                {
                    float dx = (float)(detailRng.NextDouble() - 0.5) * scale.x * 0.8f;
                    float dz = (float)(detailRng.NextDouble() - 0.5) * scale.z * 0.8f;
                    float dh = scale.y * (0.3f + (float)detailRng.NextDouble() * 0.5f);
                    float dw = 0.3f + (float)detailRng.NextDouble() * 0.6f;

                    GameObject detail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    detail.name = $"Detail_{i}";
                    detail.transform.SetParent(root.transform, false);
                    detail.transform.localPosition = new Vector3(dx, (dh - scale.y) * 0.5f + scale.y * 0.5f, dz);
                    detail.transform.localScale = new Vector3(dw, dh, dw);
                    ConfigureBlockRenderer(detail, material);

                    Collider detailCol = detail.GetComponent<Collider>();
                    if (detailCol != null) detailCol.enabled = false;
                }

                // Top step/ledge
                if (scale.y > 1.2f)
                {
                    GameObject topStep = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    topStep.name = "TopStep";
                    topStep.transform.SetParent(root.transform, false);
                    topStep.transform.localPosition = new Vector3(0f, scale.y * 0.5f + 0.06f, 0f);
                    topStep.transform.localScale = new Vector3(scale.x * 0.85f, 0.12f, scale.z * 0.85f);
                    ConfigureBlockRenderer(topStep, accentMaterial != null ? accentMaterial : material);

                    Collider stepCol = topStep.GetComponent<Collider>();
                    if (stepCol != null) stepCol.enabled = false;
                }
            }
        }

        private static void ConfigureBlockRenderer(GameObject block, Material material)
        {
            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private Vector3 GetSafePosition(System.Random rng, Vector3 scale)
        {
            float max = Mathf.Max(3f, arenaHalfExtent - roomEdgePadding);
            float x = NextFloat(rng, -max, max);
            float z = NextFloat(rng, -max, max);

            float safeCenter = 4.8f;
            if (Mathf.Abs(x) < safeCenter && Mathf.Abs(z) < safeCenter)
            {
                if (Mathf.Abs(x) > Mathf.Abs(z))
                {
                    x = Mathf.Sign(x == 0f ? NextFloat(rng, -1f, 1f) : x) * safeCenter;
                }
                else
                {
                    z = Mathf.Sign(z == 0f ? NextFloat(rng, -1f, 1f) : z) * safeCenter;
                }
            }

            float halfX = scale.x * 0.5f;
            float halfZ = scale.z * 0.5f;
            x = Mathf.Clamp(x, -max + halfX, max - halfX);
            z = Mathf.Clamp(z, -max + halfZ, max - halfZ);
            return new Vector3(x, 0f, z);
        }

        private static int NextInt(System.Random rng, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return rng.Next(minInclusive, maxExclusive);
        }

        private static float NextFloat(System.Random rng, float minInclusive, float maxInclusive)
        {
            double t = rng.NextDouble();
            return minInclusive + ((maxInclusive - minInclusive) * (float)t);
        }

        private static void SetActiveLayoutGroup(GameObject[] layouts, GameObject selected)
        {
            if (layouts == null)
            {
                return;
            }

            for (int i = 0; i < layouts.Length; i++)
            {
                GameObject layout = layouts[i];
                if (layout != null)
                {
                    layout.SetActive(layout == selected);
                }
            }
        }

        private GameObject[] GetLayoutsForRoomType(RunDirector.RoomType roomType)
        {
            return roomType switch
            {
                RunDirector.RoomType.Reward => rewardLayouts,
                RunDirector.RoomType.Risk => riskLayouts,
                RunDirector.RoomType.Boss => bossLayouts,
                _ => combatLayouts
            };
        }

        private GameObject SelectLayout(GameObject[] layouts, int room, RunDirector.RoomType roomType)
        {
            if (layouts == null || layouts.Length == 0)
            {
                return null;
            }

            int validCount = 0;
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return null;
            }

            int index;
            if (deterministicSelectionByRoom)
            {
                int hash = (room * 73856093) ^ (((int)roomType + 1) * 19349663);
                hash = Mathf.Abs(hash);
                index = hash % validCount;
            }
            else
            {
                index = UnityEngine.Random.Range(0, validCount);
            }

            int cursor = 0;
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] == null)
                {
                    continue;
                }

                if (cursor == index)
                {
                    return layouts[i];
                }

                cursor++;
            }

            return layouts[0];
        }
    }
}
