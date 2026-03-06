using UnityEngine;

namespace Ia.Core.Motion
{
    [DisallowMultipleComponent]
    public class IaMotionTarget : MonoBehaviour
    {
        [SerializeField] bool recordInitialOnAwake = true;

        Transform m_transform;
        Vector3 m_initialLocalPosition = Vector3.zero;
        Quaternion m_initialLocalRotation = Quaternion.identity;
        Vector3 m_initialLocalScale = Vector3.one;

        Vector3 m_accumulatedPositionOffset;
        Quaternion m_accumulatedRotationOffset = Quaternion.identity;
        Vector3 m_accumulatedScaleMultiplier = Vector3.one;

        void Awake()
        {
            m_transform = transform;

            if (recordInitialOnAwake)
            {
                m_initialLocalPosition = m_transform.localPosition;
                m_initialLocalRotation = m_transform.localRotation;
                m_initialLocalScale = m_transform.localScale;
            }
        }

        public void SetInitialPose(
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale
        )
        {
            m_initialLocalPosition = localPosition;
            m_initialLocalRotation = localRotation;
            m_initialLocalScale = localScale;
        }

        public void ResetAccumulated()
        {
            m_accumulatedPositionOffset = Vector3.zero;
            m_accumulatedRotationOffset = Quaternion.identity;
            m_accumulatedScaleMultiplier = Vector3.one;
        }

        public void AddPositionOffset(Vector3 offset)
        {
            m_accumulatedPositionOffset += offset;
        }

        public void AddRotationOffset(Quaternion deltaRotation)
        {
            m_accumulatedRotationOffset = deltaRotation * m_accumulatedRotationOffset;
        }

        public void AddScaleMultiplier(Vector3 scaleMult)
        {
            m_accumulatedScaleMultiplier = new Vector3(
                m_accumulatedScaleMultiplier.x * scaleMult.x,
                m_accumulatedScaleMultiplier.y * scaleMult.y,
                m_accumulatedScaleMultiplier.z * scaleMult.z
            );
        }

        public void ApplyAccumulated()
        {
            if (m_transform == null)
                m_transform = transform;

            m_transform.localPosition = m_initialLocalPosition + m_accumulatedPositionOffset;
            m_transform.localRotation = m_accumulatedRotationOffset * m_initialLocalRotation;
            m_transform.localScale = Vector3.Scale(
                m_initialLocalScale,
                m_accumulatedScaleMultiplier
            );
        }

        /// <summary>
        /// Make sure the offset is pointing in the same direction even if the target is rotated.
        /// </summary>
        public Vector3 GetWorldSpaceOffset(Vector3 localOffset)
        {
            if (m_transform == null)
                m_transform = transform;

            return m_transform.rotation * localOffset;
        }
    }
}