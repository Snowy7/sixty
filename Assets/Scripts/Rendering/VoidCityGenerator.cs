using UnityEngine;

namespace Sixty.Rendering
{
    [ExecuteAlways]
    public class VoidCityGenerator : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private float cellSize = 2.5f;
        [SerializeField] private float baseY = -0.2f;
        [SerializeField] private int maxInstances = 65536;

        [Header("Bounds")]
        [SerializeField] private float extentX = 300f;
        [SerializeField] private float extentZ = 200f;
        [SerializeField] private Vector3 centerOffset = Vector3.zero;

        public void SetCenter(Vector3 center) { centerOffset = center; }

        [Header("Rendering")]
        [SerializeField] private Material instanceMaterial;

        [Header("Editor Preview")]
        [SerializeField] private bool generateInEditor = true;

        private ComputeBuffer instanceBuffer;
        private ComputeBuffer counterBuffer;
        private ComputeBuffer argsBuffer;
        private Mesh cubeMesh;
        private int instanceCount;
        private bool isReady;

        // Room data for exclusion zones
        private Vector4[] roomCenters;
        private int roomCount;

        // Corridor exclusion zones
        private Vector4[] corridorMins;
        private Vector4[] corridorMaxs;
        private int corridorCount;

        public void SetRoomExclusions(Vector3[] centers, float halfExtent)
        {
            roomCount = Mathf.Min(centers.Length, 16);
            roomCenters = new Vector4[16];
            for (int i = 0; i < roomCount; i++)
            {
                roomCenters[i] = new Vector4(centers[i].x, centers[i].y, centers[i].z, halfExtent);
            }
            for (int i = roomCount; i < 16; i++)
            {
                roomCenters[i] = new Vector4(-99999f, 0f, -99999f, 0f);
            }
        }

        public void SetCorridorExclusions(Vector3[] mins, Vector3[] maxs)
        {
            corridorCount = Mathf.Min(mins.Length, 16);
            corridorMins = new Vector4[16];
            corridorMaxs = new Vector4[16];
            for (int i = 0; i < corridorCount; i++)
            {
                corridorMins[i] = new Vector4(mins[i].x, mins[i].y, mins[i].z, 0f);
                corridorMaxs[i] = new Vector4(maxs[i].x, maxs[i].y, maxs[i].z, 0f);
            }
            for (int i = corridorCount; i < 16; i++)
            {
                corridorMins[i] = new Vector4(-99999f, -99999f, -99999f, 0f);
                corridorMaxs[i] = new Vector4(-99999f, -99999f, -99999f, 0f);
            }
        }

        public void Generate()
        {
            if (computeShader == null || instanceMaterial == null)
            {
                Debug.LogWarning("VoidCityGenerator: Missing compute shader or material.");
                return;
            }

            instanceMaterial.enableInstancing = true;

            CreateCubeMesh();
            AllocateBuffers();
            DispatchCompute();
            ReadBackCount();

            if (instanceCount <= 0)
            {
                Debug.LogWarning($"VoidCityGenerator: Compute produced 0 instances.");
                return;
            }

            SetupIndirectArgs();
            isReady = true;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && generateInEditor)
            {
                GenerateEditorPreview();
            }
#endif
        }

        private void OnDisable()
        {
            ReleaseBuffers();
            isReady = false;
        }

#if UNITY_EDITOR
        private void GenerateEditorPreview()
        {
            if (computeShader == null || instanceMaterial == null)
                return;

            // Use default exclusions if none set
            if (roomCenters == null)
            {
                roomCount = 0;
                roomCenters = new Vector4[16];
                for (int i = 0; i < 16; i++)
                    roomCenters[i] = new Vector4(-99999f, 0f, -99999f, 0f);
            }
            if (corridorMins == null)
            {
                corridorCount = 0;
                corridorMins = new Vector4[16];
                corridorMaxs = new Vector4[16];
                for (int i = 0; i < 16; i++)
                {
                    corridorMins[i] = new Vector4(-99999f, -99999f, -99999f, 0f);
                    corridorMaxs[i] = new Vector4(-99999f, -99999f, -99999f, 0f);
                }
            }

            Generate();
        }
#endif

        private void CreateCubeMesh()
        {
            if (cubeMesh != null) return;
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying)
                Destroy(temp);
            else
                DestroyImmediate(temp);
        }

        private void AllocateBuffers()
        {
            ReleaseBuffers();

            int stride = sizeof(float) * 7; // float3 pos + float3 scale + float emissive
            instanceBuffer = new ComputeBuffer(maxInstances, stride);
            counterBuffer = new ComputeBuffer(1, sizeof(uint));
            counterBuffer.SetData(new uint[] { 0 });
        }

        private void DispatchCompute()
        {
            float minX = centerOffset.x - extentX;
            float minZ = centerOffset.z - extentZ;
            float maxX = centerOffset.x + extentX;
            float maxZ = centerOffset.z + extentZ;

            int gridX = Mathf.CeilToInt((maxX - minX) / cellSize);
            int gridZ = Mathf.CeilToInt((maxZ - minZ) / cellSize);

            int kernel = computeShader.FindKernel("CSMain");

            computeShader.SetBuffer(kernel, "_ResultBuffer", instanceBuffer);
            computeShader.SetBuffer(kernel, "_CounterBuffer", counterBuffer);
            computeShader.SetVector("_WorldBounds", new Vector4(minX, minZ, maxX, maxZ));
            computeShader.SetFloat("_CellSize", cellSize);
            computeShader.SetFloat("_BaseY", baseY);
            computeShader.SetInt("_GridCountX", gridX);
            computeShader.SetInt("_GridCountZ", gridZ);
            computeShader.SetInt("_MaxInstances", maxInstances);
            computeShader.SetInt("_RoomCount", roomCount);

            if (roomCenters != null)
            {
                computeShader.SetVectorArray("_RoomCenters", roomCenters);
            }

            computeShader.SetInt("_CorridorCount", corridorCount);
            if (corridorMins != null)
            {
                computeShader.SetVectorArray("_CorridorMins", corridorMins);
                computeShader.SetVectorArray("_CorridorMaxs", corridorMaxs);
            }

            int groupsX = Mathf.CeilToInt(gridX / 8f);
            int groupsZ = Mathf.CeilToInt(gridZ / 8f);
            computeShader.Dispatch(kernel, groupsX, groupsZ, 1);
        }

        private void ReadBackCount()
        {
            uint[] countData = new uint[1];
            counterBuffer.GetData(countData);
            instanceCount = Mathf.Min((int)countData[0], maxInstances);
        }

        private void SetupIndirectArgs()
        {
            if (cubeMesh == null) return;

            uint[] args = new uint[5];
            args[0] = cubeMesh.GetIndexCount(0);
            args[1] = (uint)instanceCount;
            args[2] = cubeMesh.GetIndexStart(0);
            args[3] = cubeMesh.GetBaseVertex(0);
            args[4] = 0;

            argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
        }

        private void Update()
        {
            if (!isReady || instanceMaterial == null || cubeMesh == null || argsBuffer == null)
                return;

            instanceMaterial.SetBuffer("_InstanceBuffer", instanceBuffer);

            Bounds bounds = new Bounds(centerOffset, new Vector3(extentX * 2f, 40f, extentZ * 2f));
            Graphics.DrawMeshInstancedIndirect(cubeMesh, 0, instanceMaterial, bounds, argsBuffer);
        }

        private void ReleaseBuffers()
        {
            instanceBuffer?.Release();
            instanceBuffer = null;
            counterBuffer?.Release();
            counterBuffer = null;
            argsBuffer?.Release();
            argsBuffer = null;
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }
    }
}
