using UnityEngine;

namespace Sixty.Rendering
{
    public class VoidCityGenerator : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private float cellSize = 2.5f;
        [SerializeField] private float baseY = -0.2f;
        [SerializeField] private int maxInstances = 32768;

        [Header("Bounds")]
        [SerializeField] private float extentX = 300f;
        [SerializeField] private float extentZ = 200f;
        [SerializeField] private Vector3 centerOffset = Vector3.zero;

        public void SetCenter(Vector3 center) { centerOffset = center; }

        [Header("Rendering")]
        [SerializeField] private Material instanceMaterial;

        private ComputeBuffer instanceBuffer;
        private ComputeBuffer counterBuffer;
        private ComputeBuffer argsBuffer;
        private Mesh cubeMesh;
        private int instanceCount;
        private bool isReady;

        // Room data for exclusion zones
        private Vector4[] roomCenters;
        private int roomCount;

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

        public void Generate()
        {
            if (computeShader == null || instanceMaterial == null)
            {
                Debug.LogWarning("VoidCityGenerator: Missing compute shader or material.");
                return;
            }

            CreateCubeMesh();
            AllocateBuffers();
            DispatchCompute();
            ReadBackCount();
            SetupIndirectArgs();
            isReady = true;
        }

        private void CreateCubeMesh()
        {
            if (cubeMesh != null) return;
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
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
