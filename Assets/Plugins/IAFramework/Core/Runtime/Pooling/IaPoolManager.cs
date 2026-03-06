using System.Collections.Generic;
using UnityEngine;

namespace Ia.Core.Pooling
{
    [DisallowMultipleComponent]
    public sealed class IaPoolManager : MonoBehaviour
    {
        public static IaPoolManager Instance { get; private set; }

        [SerializeField] Transform poolsRoot;
        [SerializeField] int defaultPrewarm = 16;
        [SerializeField] int defaultMaxSize = 0; // 0 = unlimited

        readonly Dictionary<int, IaPool> m_pools = new();

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (poolsRoot == null)
            {
                var go = new GameObject("PoolsRoot");
                go.transform.SetParent(transform, false);
                poolsRoot = go.transform;
            }
        }

        public IaPool GetOrCreatePool(IaPooledObject prefab, int? prewarm = null, int? maxSize = null)
        {
            int key = prefab.GetInstanceID();
            if (m_pools.TryGetValue(key, out IaPool pool))
                return pool;

            var root = new GameObject($"Pool_{prefab.name}").transform;
            root.SetParent(poolsRoot, false);

            pool = new IaPool(
                prefab,
                root,
                prewarm ?? defaultPrewarm,
                maxSize ?? defaultMaxSize
            );

            m_pools[key] = pool;
            return pool;
        }

        public T Spawn<T>(T prefab, Vector3 pos, Quaternion rot, Transform parent = null)
            where T : IaPooledObject
        {
            IaPool pool = GetOrCreatePool(prefab);
            return pool.Spawn<T>(pos, rot, parent);
        }

        public void Despawn(IaPooledObject obj)
        {
            obj?.Despawn();
        }
    }
}