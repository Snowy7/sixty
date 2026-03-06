using UnityEngine;

namespace Sixty.CameraSystem
{
    public class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 22f, -12f);
        [SerializeField] private float followLerpSpeed = 8f;
        [SerializeField] private bool lookAtTarget = true;
        [Header("Shake")]
        [SerializeField] private float shakeDecayPerSecond = 1.2f;
        [SerializeField] private float maxShakeDistance = 1.15f;
        [SerializeField] private float maxShakeRollDegrees = 4f;
        [SerializeField] private float maxShakePitchYawDegrees = 2.2f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void AddShake(float amount)
        {
            shakeTrauma = Mathf.Clamp01(shakeTrauma + Mathf.Max(0f, amount));
        }

        private float shakeTrauma;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerpSpeed * Time.deltaTime);

            if (lookAtTarget)
            {
                Vector3 lookAt = target.position;
                lookAt.y = 0.75f;
                transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            }

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

                shakeTrauma = Mathf.Max(0f, shakeTrauma - (shakeDecayPerSecond * Time.deltaTime));
            }
        }
    }
}
