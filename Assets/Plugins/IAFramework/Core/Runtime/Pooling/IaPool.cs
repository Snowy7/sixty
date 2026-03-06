using System.Collections.Generic;
using UnityEngine;

namespace Ia.Core.Pooling
{
    public sealed class IaPool
    {
        readonly IaPooledObject m_prefab;
        readonly Transform m_root;
        readonly Queue<IaPooledObject> m_inactive = new();
        readonly HashSet<IaPooledObject> m_active = new();

        readonly int m_maxSize;

        public IaPool(IaPooledObject prefab, Transform root, int prewarm, int maxSize)
        {
            m_prefab = prefab;
            m_root = root;
            m_maxSize = maxSize;

            for (int i = 0; i < prewarm; i++)
                CreateAndEnqueue();
        }

        IaPooledObject CreateAndEnqueue()
        {
            IaPooledObject obj = Object.Instantiate(m_prefab, m_root);
            obj.Pool = this;
            obj.gameObject.SetActive(false);
            m_inactive.Enqueue(obj);
            return obj;
        }

        public T Spawn<T>(Vector3 pos, Quaternion rot, Transform parent = null)
            where T : IaPooledObject
        {
            IaPooledObject obj = null;

            if (m_inactive.Count > 0)
            {
                obj = m_inactive.Dequeue();
            }
            else
            {
                if (m_maxSize > 0 && (m_active.Count + m_inactive.Count) >= m_maxSize)
                {
                    // Hard cap: reuse oldest active if you want, or just refuse.
                    // Jam-safe: just create anyway if maxSize==0. If maxSize>0, refuse.
                    return null;
                }

                obj = CreateAndEnqueue();
                obj = m_inactive.Dequeue();
            }

            Transform t = obj.transform;
            t.SetParent(parent != null ? parent : m_root, worldPositionStays: false);
            t.SetPositionAndRotation(pos, rot);

            obj.gameObject.SetActive(true);
            m_active.Add(obj);

            obj.OnSpawned();
            return obj as T;
        }

        public void Despawn(IaPooledObject obj)
        {
            if (obj == null)
                return;

            if (!m_active.Remove(obj))
                return;

            obj.OnDespawned();
            obj.transform.SetParent(m_root, worldPositionStays: false);
            obj.gameObject.SetActive(false);
            m_inactive.Enqueue(obj);
        }
    }
}