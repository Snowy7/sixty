using UnityEngine;
using Ia.Core.Update;

namespace Sixty.CameraSystem
{
    public class TopDownCameraFollow : IaBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 16f, -9f);
        [SerializeField] private float followLerpSpeed = 8f;
        [SerializeField] private bool lookAtTarget = true;
        [Header("Shake")]
        [SerializeField] private float shakeDecayPerSecond = 1.2f;
        [SerializeField] private float maxShakeDistance = 1.15f;
        [SerializeField] private float maxShakeRollDegrees = 4f;
        [SerializeField] private float maxShakePitchYawDegrees = 2.2f;
        
        protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.UI;
        protected override IaUpdatePhase UpdatePhases => IaUpdatePhase.LateUpdate;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void AddShake(float amount)
        {
            shakeTrauma = Mathf.Clamp01(shakeTrauma + Mathf.Max(0f, amount));
        }

        private float shakeTrauma;

        public bool TryGetPredictedPose(float deltaTime, out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;

            if (target == null)
            {
                return false;
            }

            position = GetFollowPosition(deltaTime, position);
            rotation = GetLookRotation(position, rotation);
            return true;
        }

        public override void OnIaLateUpdate(float deltaTime)
        {
            if (target == null)
            {
                return;
            }

            Vector3 nextPosition = GetFollowPosition(deltaTime, transform.position);
            Quaternion nextRotation = GetLookRotation(nextPosition, transform.rotation);
            transform.SetPositionAndRotation(nextPosition, nextRotation);

            if (shakeTrauma > 0f)
            {
                float shakeStrength = shakeTrauma * shakeTrauma;
                Vector3 positionJitter = Random.insideUnitSphere * (maxShakeDistance * shakeStrength);
                positionJitter.y *= 0.4f;
                transform.position += positionJitter;

                float roll = Random.Range(-1f, 1f) * maxShakeRollDegrees * shakeStrength;
                float pitch = Random.Range(-1f, 1f) * maxShakePitchYawDegrees * shakeStrength;
                float yaw = Random.Range(-1f, 1f) * maxShakePitchYawDegrees * shakeStrength;
                transform.rotation = Quaternion.Euler(transform.eulerAngles.x + pitch, transform.eulerAngles.y + yaw, roll);

                shakeTrauma = Mathf.Max(0f, shakeTrauma - (shakeDecayPerSecond * deltaTime));
            }
        }

        private Vector3 GetFollowPosition(float deltaTime, Vector3 currentPosition)
        {
            Vector3 desiredPosition = target.position + offset;
            return Vector3.Lerp(currentPosition, desiredPosition, followLerpSpeed * deltaTime);
        }

        private Quaternion GetLookRotation(Vector3 cameraPosition, Quaternion fallbackRotation)
        {
            if (!lookAtTarget)
            {
                return fallbackRotation;
            }

            Vector3 lookAt = target.position;
            lookAt.y = 0.75f;
            Vector3 lookDirection = lookAt - cameraPosition;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return fallbackRotation;
            }

            return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }
}
