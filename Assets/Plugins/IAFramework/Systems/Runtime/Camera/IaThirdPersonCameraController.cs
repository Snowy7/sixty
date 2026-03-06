using UnityEngine;

namespace Ia.Systems.Camera
{
    public class IaThirdPersonCameraController : IaCameraControllerBase
    {
        [Header("Orbit")]
        [SerializeField] private float _distance = 4f;
        [SerializeField] private float _minDistance = 2f;
        [SerializeField] private float _maxDistance = 8f;
        [SerializeField] private float _zoomSpeed = 5f;

        [SerializeField] private float _yawSpeed = 120f;
        [SerializeField] private float _pitchSpeed = 90f;
        [SerializeField] private float _minPitch = -25f;
        [SerializeField] private float _maxPitch = 70f;

        [Header("Offsets")]
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.7f, 0f);

        [Header("Smoothing")]
        [SerializeField] private float _positionSmoothTime = 0.05f;

        [Header("Collision")]
        [SerializeField] private LayerMask _collisionMask = -1;
        [SerializeField] private float _collisionRadius = 0.2f;

        private float _yaw;
        private float _pitch;
        private float _targetDistance;
        private Vector3 _currentVelocity;

        protected override void OnIaAwake()
        {
            _targetDistance = _distance;
            if (target != null)
            {
                Vector3 euler = target.rotation.eulerAngles;
                _yaw = euler.y;
            }
        }

        public override void OnIaLateUpdate(float deltaTime)
        {
            if (target == null)
                return;

            HandleInput(deltaTime);
            HandleDistance(deltaTime);
            PositionCamera(deltaTime);
        }

        private void HandleInput(float dt)
        {
            if (!m_cameraInputEnabled)
                return;

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            _yaw += mouseX * _yawSpeed * dt;
            _pitch -= mouseY * _pitchSpeed * dt;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            _targetDistance -= scroll * _zoomSpeed;
            _targetDistance = Mathf.Clamp(
                _targetDistance,
                _minDistance,
                _maxDistance
            );
        }

        private void HandleDistance(float dt)
        {
            _distance = Mathf.Lerp(_distance, _targetDistance, 10f * dt);
        }

        private void PositionCamera(float dt)
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 targetPos = target.position + _targetOffset;
            Vector3 desiredOffset = rotation * new Vector3(0f, 0f, -_distance);
            Vector3 desiredPos = targetPos + desiredOffset;

            // Collision
            Vector3 dir = (desiredPos - targetPos).normalized;
            float desiredDist = Vector3.Distance(targetPos, desiredPos);

            if (
                Physics.SphereCast(
                    targetPos,
                    _collisionRadius,
                    dir,
                    out RaycastHit hit,
                    desiredDist,
                    _collisionMask
                )
            )
            {
                desiredPos = targetPos + dir * (hit.distance - 0.1f);
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPos,
                ref _currentVelocity,
                _positionSmoothTime
            );

            transform.LookAt(targetPos);
        }
    }
}