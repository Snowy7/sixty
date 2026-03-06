using System.Collections.Generic;
using Sixty.Gameplay;
using UnityEngine;

namespace Sixty.World
{
    public class RuntimeArenaBuilder : MonoBehaviour
    {
        [Header("Room Chain")]
        [SerializeField] private int roomCount = 10;
        [SerializeField] private float roomSpacing = 58f;
        [SerializeField] private float wallHalfExtent = 24f;
        [SerializeField] private float wallHeight = 3.2f;
        [SerializeField] private float wallThickness = 1.2f;
        [SerializeField] private float doorOpeningWidth = 6.8f;
        [SerializeField] private float corridorWidth = 9f;
        [SerializeField] private int layoutSeed = 42;

        [Header("Materials")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material wallFaceMaterial;
        [SerializeField] private Material trimMaterial;
        [SerializeField] private Material accentMaterial;
        [SerializeField] private Material guideMaterial;

        [Header("Wall Detail")]
        [SerializeField] private int wallDetailDensity = 8;
        [SerializeField] private float wallDetailMinScale = 0.4f;
        [SerializeField] private float wallDetailMaxScale = 1.8f;

        private Transform roomRoot;
        private Transform corridorRoot;
        private Transform gateRoot;
        private Transform detailRoot;

        private List<RunExitDoor> generatedDoors = new List<RunExitDoor>();
        private List<Renderer> generatedGroundRenderers = new List<Renderer>();
        private Transform[] generatedRoomAnchors;

        private Vector2Int[] gridPositions;
        private int[] connectionDirs; // 0=N, 1=S, 2=E, 3=W

        public Transform[] RoomAnchors => generatedRoomAnchors;
        public RunExitDoor[] ExitDoors => generatedDoors.ToArray();
        public Renderer[] GroundRenderers => generatedGroundRenderers.ToArray();
        public Vector2Int[] GridPositions => gridPositions;
        public int GridRoomCount => roomCount;
        public float GridRoomSpacing => roomSpacing;
        public float GridWallHalfExtent => wallHalfExtent;

        private static readonly Vector3[] SpawnTemplatePositions =
        {
            new Vector3(0f, 1.4f, 20f),
            new Vector3(0f, 1.4f, -20f),
            new Vector3(20f, 1.4f, 0f),
            new Vector3(-20f, 1.4f, 0f),
            new Vector3(15f, 1.4f, 15f),
            new Vector3(-15f, 1.4f, 15f),
            new Vector3(15f, 1.4f, -15f),
            new Vector3(-15f, 1.4f, -15f),
            new Vector3(9f, 1.4f, 19f),
            new Vector3(-9f, 1.4f, 19f),
            new Vector3(9f, 1.4f, -19f),
            new Vector3(-9f, 1.4f, -19f),
            new Vector3(19f, 1.4f, 9f),
            new Vector3(19f, 1.4f, -9f),
            new Vector3(-19f, 1.4f, 9f),
            new Vector3(-19f, 1.4f, -9f)
        };

        public void BuildArena()
        {
            ClearExisting();

            roomRoot = CreateChild("Rooms");
            corridorRoot = CreateChild("Corridors");
            gateRoot = CreateChild("Gates");
            detailRoot = CreateChild("Details");

            generatedRoomAnchors = new Transform[roomCount];
            generatedDoors.Clear();
            generatedGroundRenderers.Clear();

            GenerateLayout();

            System.Random detailRng = new System.Random(48271);

            for (int i = 0; i < roomCount; i++)
            {
                Vector3 center = GridToWorld(gridPositions[i]);

                GameObject anchor = new GameObject($"RoomAnchor_{i + 1:00}");
                anchor.transform.SetParent(transform, false);
                anchor.transform.position = center;
                generatedRoomAnchors[i] = anchor.transform;

                bool openN = false, openS = false, openE = false, openW = false;
                if (i < roomCount - 1) SetOpenDir(connectionDirs[i], ref openN, ref openS, ref openE, ref openW);
                if (i > 0) SetOpenDir(OppositeDir(connectionDirs[i - 1]), ref openN, ref openS, ref openE, ref openW);

                BuildRoomFloor(center, i);
                BuildRoomWalls(center, i, openN, openS, openE, openW, detailRng);
                BuildRoomGuideLines(center, i);

                if (i < roomCount - 1)
                {
                    Vector3 nextCenter = GridToWorld(gridPositions[i + 1]);
                    BuildCorridor(center, nextCenter, connectionDirs[i]);
                    BuildExitGate(center, i, connectionDirs[i]);
                }
            }
        }

        // --- Layout Generation ---

        private void GenerateLayout()
        {
            gridPositions = new Vector2Int[roomCount];
            connectionDirs = new int[roomCount - 1];

            gridPositions[0] = Vector2Int.zero;
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int> { Vector2Int.zero };
            System.Random rng = new System.Random(layoutSeed);

            for (int i = 1; i < roomCount; i++)
            {
                Vector2Int prev = gridPositions[i - 1];
                int[] dirs = { 0, 1, 2, 3 };
                ShuffleArray(rng, dirs);

                bool placed = false;
                foreach (int dir in dirs)
                {
                    Vector2Int candidate = prev + DirOffset(dir);
                    if (!occupied.Contains(candidate))
                    {
                        gridPositions[i] = candidate;
                        occupied.Add(candidate);
                        connectionDirs[i - 1] = dir;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    // Fallback: find nearest unoccupied cell
                    for (int r = 2; !placed; r++)
                    {
                        for (int dx = -r; dx <= r && !placed; dx++)
                        {
                            for (int dz = -r; dz <= r && !placed; dz++)
                            {
                                if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                                Vector2Int candidate = prev + new Vector2Int(dx, dz);
                                if (!occupied.Contains(candidate))
                                {
                                    gridPositions[i] = candidate;
                                    occupied.Add(candidate);
                                    connectionDirs[i - 1] = DirFromDelta(candidate - prev);
                                    placed = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        private Vector3 GridToWorld(Vector2Int grid)
        {
            return new Vector3(grid.x * roomSpacing, 0f, grid.y * roomSpacing);
        }

        private static Vector2Int DirOffset(int dir)
        {
            return dir switch
            {
                0 => new Vector2Int(0, 1),
                1 => new Vector2Int(0, -1),
                2 => new Vector2Int(1, 0),
                _ => new Vector2Int(-1, 0)
            };
        }

        private static int OppositeDir(int dir)
        {
            return dir switch { 0 => 1, 1 => 0, 2 => 3, _ => 2 };
        }

        private static int DirFromDelta(Vector2Int delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x > 0 ? 2 : 3;
            return delta.y > 0 ? 0 : 1;
        }

        private static void SetOpenDir(int dir, ref bool n, ref bool s, ref bool e, ref bool w)
        {
            switch (dir) { case 0: n = true; break; case 1: s = true; break; case 2: e = true; break; case 3: w = true; break; }
        }

        private static void ShuffleArray(System.Random rng, int[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }

        // --- Room Floor ---

        private void BuildRoomFloor(Vector3 center, int roomIndex)
        {
            float floorSpan = wallHalfExtent * 2f;
            GameObject floor = CreateBlock(roomRoot, floorMaterial,
                center + new Vector3(0f, 0.055f, 0f),
                new Vector3(floorSpan, 0.1f, floorSpan),
                $"RoomFloor_{roomIndex + 1:00}", true, true);

            generatedGroundRenderers.Add(floor.GetComponent<Renderer>());

            // Trim border
            CreateBlock(roomRoot, trimMaterial,
                center + new Vector3(0f, 0.04f, 0f),
                new Vector3(floorSpan + 2f, 0.06f, floorSpan + 2f),
                $"RoomFloorTrim_{roomIndex + 1:00}", false, true);
        }

        // --- Walls ---

        private void BuildRoomWalls(Vector3 center, int roomIndex, bool openN, bool openS, bool openE, bool openW, System.Random rng)
        {
            float he = wallHalfExtent;
            float h = wallHeight;
            float t = wallThickness;
            float span = he * 2f;
            Material baseMat = wallMaterial;
            Material faceMat = wallFaceMaterial != null ? wallFaceMaterial : trimMaterial;

            // North wall
            if (openN)
                BuildOpeningWall(center, 0, roomIndex, baseMat, faceMat, rng);
            else
                BuildSolidWall(center, 0, roomIndex, baseMat, faceMat, rng);

            // South wall
            if (openS)
                BuildOpeningWall(center, 1, roomIndex, baseMat, faceMat, rng);
            else
                BuildSolidWall(center, 1, roomIndex, baseMat, faceMat, rng);

            // East wall
            if (openE)
                BuildOpeningWall(center, 2, roomIndex, baseMat, faceMat, rng);
            else
                BuildSolidWall(center, 2, roomIndex, baseMat, faceMat, rng);

            // West wall
            if (openW)
                BuildOpeningWall(center, 3, roomIndex, baseMat, faceMat, rng);
            else
                BuildSolidWall(center, 3, roomIndex, baseMat, faceMat, rng);

            // Corner pillars
            float cp = he - 1.5f;
            BuildCornerPillar(center, cp, cp, roomIndex, "NE", faceMat, baseMat, rng);
            BuildCornerPillar(center, -cp, cp, roomIndex, "NW", faceMat, baseMat, rng);
            BuildCornerPillar(center, cp, -cp, roomIndex, "SE", faceMat, baseMat, rng);
            BuildCornerPillar(center, -cp, -cp, roomIndex, "SW", faceMat, baseMat, rng);
        }

        private void BuildSolidWall(Vector3 center, int side, int roomIndex, Material baseMat, Material faceMat, System.Random rng)
        {
            float he = wallHalfExtent;
            float h = wallHeight;
            float t = wallThickness;
            float span = he * 2f;
            string prefix = $"Wall_{SideName(side)}_{roomIndex}";

            GetWallTransform(center, side, he, out Vector3 basePos, out Vector3 baseScale, out Vector3 faceOffset, out bool isHorizontal);

            // Dark base slab (full thickness)
            basePos.y = h * 0.5f;
            baseScale.y = h;
            CreateBlock(roomRoot, baseMat, basePos, baseScale, $"{prefix}_Base", true, false);

            // Lighter face overlay (thinner, on interior side)
            Vector3 facePos = basePos + faceOffset;
            Vector3 faceScale = baseScale;
            if (isHorizontal)
            {
                faceScale.z = t * 0.4f;
                faceScale.x -= 0.4f;
            }
            else
            {
                faceScale.x = t * 0.4f;
                faceScale.z -= 0.4f;
            }
            faceScale.y = h * 0.88f;
            facePos.y = faceScale.y * 0.5f;
            CreateBlock(detailRoot, faceMat, facePos, faceScale, $"{prefix}_Face", false, false);

            // Stepped base ledge
            BuildWallBaseLedge(center, side, roomIndex, span, faceMat, baseMat);

            // Detail blocks
            BuildWallDetails(center, side, roomIndex, span, h, t, he, faceMat, baseMat, rng);
        }

        private void BuildOpeningWall(Vector3 center, int side, int roomIndex, Material baseMat, Material faceMat, System.Random rng)
        {
            float he = wallHalfExtent;
            float h = wallHeight;
            float t = wallThickness;
            float span = he * 2f;
            float opening = doorOpeningWidth;
            float segLen = Mathf.Max(2f, (span - opening) * 0.5f);
            float segOff = opening * 0.5f + segLen * 0.5f;
            string prefix = $"Wall_{SideName(side)}_{roomIndex}";

            GetWallEdgePosition(center, side, he, out float wallX, out float wallZ, out bool isHorizontal);

            if (isHorizontal) // N or S
            {
                float inward = (side == 0) ? -t * 0.2f : t * 0.2f;
                // Left segment
                CreateBlock(roomRoot, baseMat, new Vector3(center.x - segOff, h * 0.5f, wallZ), new Vector3(segLen, h, t), $"{prefix}_L_Base", true, false);
                CreateBlock(detailRoot, faceMat, new Vector3(center.x - segOff, h * 0.44f, wallZ + inward), new Vector3(segLen - 0.3f, h * 0.88f, t * 0.4f), $"{prefix}_L_Face", false, false);
                // Right segment
                CreateBlock(roomRoot, baseMat, new Vector3(center.x + segOff, h * 0.5f, wallZ), new Vector3(segLen, h, t), $"{prefix}_R_Base", true, false);
                CreateBlock(detailRoot, faceMat, new Vector3(center.x + segOff, h * 0.44f, wallZ + inward), new Vector3(segLen - 0.3f, h * 0.88f, t * 0.4f), $"{prefix}_R_Face", false, false);
                // Header
                CreateBlock(roomRoot, baseMat, new Vector3(center.x, h - 0.3f, wallZ), new Vector3(opening + 1.2f, 0.6f, t + 0.2f), $"{prefix}_Header", true, false);
            }
            else // E or W
            {
                float inward = (side == 2) ? -t * 0.2f : t * 0.2f;
                CreateBlock(roomRoot, baseMat, new Vector3(wallX, h * 0.5f, center.z + segOff), new Vector3(t, h, segLen), $"{prefix}_L_Base", true, false);
                CreateBlock(detailRoot, faceMat, new Vector3(wallX + inward, h * 0.44f, center.z + segOff), new Vector3(t * 0.4f, h * 0.88f, segLen - 0.3f), $"{prefix}_L_Face", false, false);
                CreateBlock(roomRoot, baseMat, new Vector3(wallX, h * 0.5f, center.z - segOff), new Vector3(t, h, segLen), $"{prefix}_R_Base", true, false);
                CreateBlock(detailRoot, faceMat, new Vector3(wallX + inward, h * 0.44f, center.z - segOff), new Vector3(t * 0.4f, h * 0.88f, segLen - 0.3f), $"{prefix}_R_Face", false, false);
                CreateBlock(roomRoot, baseMat, new Vector3(wallX, h - 0.3f, center.z), new Vector3(t + 0.2f, 0.6f, opening + 1.2f), $"{prefix}_Header", true, false);
            }

            // Partial ledge and details for opening walls
            BuildWallBaseLedge(center, side, roomIndex, span, faceMat, baseMat);
            BuildWallDetails(center, side, roomIndex, span, h, t, he, faceMat, baseMat, rng);
        }

        private void BuildWallBaseLedge(Vector3 center, int side, int roomIndex, float span, Material faceMat, Material baseMat)
        {
            float he = wallHalfExtent;
            GetWallEdgePosition(center, side, he, out float wallX, out float wallZ, out bool isHorizontal);
            float inward = GetInwardSign(side);

            for (int step = 0; step < 3; step++)
            {
                float inset = (0.5f + step * 0.6f) * inward;
                float stepH = 0.15f + step * 0.08f;
                float stepLen = span - step * 4f;
                Material mat = step == 0 ? faceMat : baseMat;

                if (isHorizontal)
                {
                    CreateBlock(detailRoot, mat,
                        new Vector3(center.x, stepH * 0.5f, wallZ + inset),
                        new Vector3(stepLen, stepH, 0.6f),
                        $"WallStep_{SideName(side)}_{roomIndex}_{step}", false, false);
                }
                else
                {
                    CreateBlock(detailRoot, mat,
                        new Vector3(wallX + inset, stepH * 0.5f, center.z),
                        new Vector3(0.6f, stepH, stepLen),
                        $"WallStep_{SideName(side)}_{roomIndex}_{step}", false, false);
                }
            }
        }

        private void BuildWallDetails(Vector3 center, int side, int roomIndex, float span, float h, float t, float he, Material faceMat, Material baseMat, System.Random rng)
        {
            int count = wallDetailDensity;
            GetWallEdgePosition(center, side, he, out float wallX, out float wallZ, out bool isHorizontal);
            float inwardSign = GetInwardSign(side);

            for (int d = 0; d < count; d++)
            {
                float along = ((float)rng.NextDouble() - 0.5f) * (span - 4f);
                float w = wallDetailMinScale + (float)rng.NextDouble() * (wallDetailMaxScale - wallDetailMinScale);
                float detailH = 0.6f + (float)rng.NextDouble() * (h - 0.4f);
                float depth = 0.3f + (float)rng.NextDouble() * 1.2f;

                bool outside = rng.NextDouble() > 0.6;
                float depthOffset = (outside ? -depth * 0.4f : depth * 0.4f) * inwardSign;

                Material mat = rng.NextDouble() > 0.65 ? faceMat : baseMat;

                if (isHorizontal)
                {
                    Vector3 pos = new Vector3(center.x + along, detailH * 0.5f, wallZ + depthOffset);
                    CreateBlock(detailRoot, mat, pos, new Vector3(w, detailH, depth), $"WD_{roomIndex}_{side}_{d}", false, false);
                }
                else
                {
                    Vector3 pos = new Vector3(wallX + depthOffset, detailH * 0.5f, center.z + along);
                    CreateBlock(detailRoot, mat, pos, new Vector3(depth, detailH, w), $"WD_{roomIndex}_{side}_{d}", false, false);
                }
            }
        }

        private void BuildCornerPillar(Vector3 center, float offsetX, float offsetZ, int roomIndex, string label, Material faceMat, Material baseMat, System.Random rng)
        {
            float h = wallHeight;
            float baseH = 0.8f + (float)rng.NextDouble() * 1.8f;
            float pillarW = 1.2f + (float)rng.NextDouble() * 0.6f;

            // Dark base
            CreateBlock(detailRoot, baseMat,
                center + new Vector3(offsetX, baseH * 0.5f, offsetZ),
                new Vector3(pillarW + 0.3f, baseH, pillarW + 0.3f),
                $"Corner_{roomIndex}_{label}_Base", false, false);

            // Lighter cap
            float capH = 0.4f + (float)rng.NextDouble() * 0.6f;
            CreateBlock(detailRoot, faceMat,
                center + new Vector3(offsetX, baseH + capH * 0.5f, offsetZ),
                new Vector3(pillarW, capH, pillarW),
                $"Corner_{roomIndex}_{label}_Cap", false, false);
        }

        // --- Guide Lines ---

        private void BuildRoomGuideLines(Vector3 center, int roomIndex)
        {
            float guidLen = wallHalfExtent * 2f - 6f;
            CreateBlock(roomRoot, guideMaterial, center + new Vector3(0f, 0.108f, 0f), new Vector3(2f, 0.02f, guidLen), $"Guide_NS_{roomIndex}", false, false);
            CreateBlock(roomRoot, guideMaterial, center + new Vector3(0f, 0.108f, 0f), new Vector3(guidLen, 0.02f, 2f), $"Guide_EW_{roomIndex}", false, false);
        }

        // --- Corridors ---

        private void BuildCorridor(Vector3 from, Vector3 to, int dir)
        {
            Vector3 center = (from + to) * 0.5f;
            float dist = Vector3.Distance(from, to);
            float length = Mathf.Max(4f, dist - wallHalfExtent * 2f);
            bool isHorizontal = (dir == 2 || dir == 3); // E or W
            string label = $"Corridor_{from.x:0}_{from.z:0}";

            Vector3 floorScale, wallNPos, wallSPos, wallScale, guideScale;
            if (isHorizontal)
            {
                floorScale = new Vector3(length, 0.08f, corridorWidth);
                wallNPos = center + new Vector3(0f, 1.4f, corridorWidth * 0.5f + 0.4f);
                wallSPos = center + new Vector3(0f, 1.4f, -(corridorWidth * 0.5f + 0.4f));
                wallScale = new Vector3(length, 2.8f, 0.8f);
                guideScale = new Vector3(Mathf.Max(1f, length - 0.8f), 0.02f, 0.34f);
            }
            else
            {
                floorScale = new Vector3(corridorWidth, 0.08f, length);
                wallNPos = center + new Vector3(corridorWidth * 0.5f + 0.4f, 1.4f, 0f);
                wallSPos = center + new Vector3(-(corridorWidth * 0.5f + 0.4f), 1.4f, 0f);
                wallScale = new Vector3(0.8f, 2.8f, length);
                guideScale = new Vector3(0.34f, 0.02f, Mathf.Max(1f, length - 0.8f));
            }

            GameObject floor = CreateBlock(corridorRoot, floorMaterial,
                center + new Vector3(0f, 0.05f, 0f), floorScale,
                $"{label}_Floor", true, true);
            generatedGroundRenderers.Add(floor.GetComponent<Renderer>());

            CreateBlock(corridorRoot, guideMaterial,
                center + new Vector3(0f, 0.096f, 0f), guideScale,
                $"{label}_Guide", false, false);

            CreateBlock(corridorRoot, wallMaterial, wallNPos, wallScale, $"{label}_WallA", true, false);
            CreateBlock(corridorRoot, wallMaterial, wallSPos, wallScale, $"{label}_WallB", true, false);
        }

        // --- Exit Gates ---

        private void BuildExitGate(Vector3 roomCenter, int roomIndex, int dir)
        {
            float he = wallHalfExtent;
            float t = wallThickness;
            float h = wallHeight;

            Vector3 gatePos = roomCenter;
            Quaternion gateRot;

            switch (dir)
            {
                case 0: // North
                    gatePos += new Vector3(0f, 0f, he - t * 0.48f);
                    gateRot = Quaternion.identity;
                    break;
                case 1: // South
                    gatePos += new Vector3(0f, 0f, -(he - t * 0.48f));
                    gateRot = Quaternion.Euler(0f, 180f, 0f);
                    break;
                case 2: // East
                    gatePos += new Vector3(he - t * 0.48f, 0f, 0f);
                    gateRot = Quaternion.Euler(0f, 90f, 0f);
                    break;
                default: // West
                    gatePos += new Vector3(-(he - t * 0.48f), 0f, 0f);
                    gateRot = Quaternion.Euler(0f, -90f, 0f);
                    break;
            }

            RunExitDoorDirection doorDir = (RunExitDoorDirection)dir;

            GameObject root = new GameObject($"Gate_{roomIndex + 1:00}_To_{roomIndex + 2:00}");
            root.transform.SetParent(gateRoot, false);
            root.transform.position = gatePos;
            root.transform.rotation = gateRot;

            GameObject slabRoot = new GameObject("GateWall");
            slabRoot.transform.SetParent(root.transform, false);
            slabRoot.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);

            GameObject wall = CreateBlockLocal(slabRoot.transform, wallMaterial, Vector3.zero,
                new Vector3(doorOpeningWidth + 0.3f, h, t + 0.12f), "WallSegment");

            GameObject line = CreateBlockLocal(slabRoot.transform, accentMaterial,
                new Vector3(0f, -h * 0.5f + 0.07f, -0.52f),
                new Vector3(doorOpeningWidth, 0.04f, 0.1f), "GroundLine", false);

            GameObject trigger = new GameObject("EntryTrigger");
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 1.2f, -1.25f);
            BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(doorOpeningWidth + 1.2f, 2.5f, 2.7f);

            RunExitDoor door = trigger.AddComponent<RunExitDoor>();
            door.SetRuntimeRefs(
                doorDir,
                slabRoot.transform,
                triggerCollider,
                new Renderer[] { wall.GetComponent<Renderer>(), line.GetComponent<Renderer>() },
                4.5f, 8.2f,
                new Color(1f, 0.22f, 0.62f, 1f),
                new Color(0.24f, 1f, 0.92f, 1f));

            generatedDoors.Add(door);
        }

        // --- Spawn Points ---

        public Transform[] BuildSpawnPoints()
        {
            GameObject spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(transform, false);

            Transform[] points = new Transform[SpawnTemplatePositions.Length];
            for (int i = 0; i < SpawnTemplatePositions.Length; i++)
            {
                GameObject point = new GameObject($"SpawnPoint_{i + 1:00}");
                point.transform.SetParent(spawnRoot.transform);
                point.transform.localPosition = SpawnTemplatePositions[i];
                points[i] = point.transform;
            }

            return points;
        }

        // --- Helpers ---

        private void ClearExisting()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private Transform CreateChild(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private static string SideName(int side)
        {
            return side switch { 0 => "N", 1 => "S", 2 => "E", _ => "W" };
        }

        private void GetWallTransform(Vector3 center, int side, float he, out Vector3 pos, out Vector3 scale, out Vector3 faceOffset, out bool isHorizontal)
        {
            float t = wallThickness;
            float span = he * 2f;
            isHorizontal = (side == 0 || side == 1);

            switch (side)
            {
                case 0: // North
                    pos = new Vector3(center.x, 0f, center.z + he);
                    scale = new Vector3(span, 0f, t);
                    faceOffset = new Vector3(0f, 0f, -t * 0.2f);
                    break;
                case 1: // South
                    pos = new Vector3(center.x, 0f, center.z - he);
                    scale = new Vector3(span, 0f, t);
                    faceOffset = new Vector3(0f, 0f, t * 0.2f);
                    break;
                case 2: // East
                    pos = new Vector3(center.x + he, 0f, center.z);
                    scale = new Vector3(t, 0f, span);
                    faceOffset = new Vector3(-t * 0.2f, 0f, 0f);
                    break;
                default: // West
                    pos = new Vector3(center.x - he, 0f, center.z);
                    scale = new Vector3(t, 0f, span);
                    faceOffset = new Vector3(t * 0.2f, 0f, 0f);
                    break;
            }
        }

        private void GetWallEdgePosition(Vector3 center, int side, float he, out float wallX, out float wallZ, out bool isHorizontal)
        {
            wallX = center.x;
            wallZ = center.z;
            isHorizontal = (side == 0 || side == 1);

            switch (side)
            {
                case 0: wallZ = center.z + he; break;
                case 1: wallZ = center.z - he; break;
                case 2: wallX = center.x + he; break;
                case 3: wallX = center.x - he; break;
            }
        }

        private static float GetInwardSign(int side)
        {
            return side switch { 0 => -1f, 1 => 1f, 2 => -1f, _ => 1f };
        }

        private GameObject CreateBlock(Transform parent, Material material, Vector3 position, Vector3 scale, string name, bool withCollider, bool isGroundSurface)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;

            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (!withCollider)
            {
                Collider c = block.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }

            return block;
        }

        private GameObject CreateBlockLocal(Transform parent, Material material, Vector3 localPos, Vector3 localScale, string name, bool withCollider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPos;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = localScale;

            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (!withCollider)
            {
                Collider c = block.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }

            return block;
        }
    }
}
