using UnityEngine;

namespace OutGame
{
    [RequireComponent(typeof(Camera))]
    public class OutThirdPersonCamera : MonoBehaviour
    {
        [Header("Targeting & Offsets")]
        [Tooltip("The main transform the camera will orbit around (e.g., the player's spine or head).")]
        [SerializeField] private Transform followTarget;

        [Tooltip("Offset relative to the camera's rotation. X is shoulder offset, Y is height.")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0.5f, 1.5f, 0f);

        [Tooltip("Default distance from the target.")]
        [SerializeField] private float defaultDistance = 3.0f;

        [Header("Orbit Controls")]
        [SerializeField] private float lookSensitivity = 2.0f;
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private bool invertY = false;

        [Header("Smoothing & Feel")]
        [Tooltip("How quickly the camera follows the target's movement.")]
        [SerializeField] private float positionSmoothTime = 0.1f;
        [Tooltip("How quickly the camera rotation catches up to mouse input.")]
        [SerializeField] private float rotationSmoothTime = 0.05f;

        [Header("Collision & Occlusion")]
        [SerializeField] private LayerMask collisionLayers;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float minCollisionDistance = 0.5f;

        private float _currentYaw;
        private float _currentPitch;
        private float _smoothYaw;
        private float _smoothPitch;
        private float _yawVelocity;
        private float _pitchVelocity;

        private Vector3 _currentFocusPos;
        private Vector3 _focusPosVelocity;
        private float _currentDistance;

        public Vector2 LookInput { get; set; }

        private void Start()
        {
            _currentDistance = defaultDistance;

            if (followTarget != null)
            {
                Vector3 angles = transform.eulerAngles;
                _currentPitch = angles.x;
                _currentYaw = angles.y;

                _smoothPitch = _currentPitch;
                _smoothYaw = _currentYaw;
                _currentFocusPos = followTarget.position;
            }
            else
            {
                OutLogger.LogWarning("OutThirdPersonCamera: No follow target assigned!", "yellow");
            }
        }

        private void LateUpdate()
        {
            if (followTarget == null) return;

            ProcessInput();

            // 1. Smooth the inputs and target position FIRST. No lazy math later.
            _smoothYaw = Mathf.SmoothDampAngle(_smoothYaw, _currentYaw, ref _yawVelocity, rotationSmoothTime);
            _smoothPitch = Mathf.SmoothDampAngle(_smoothPitch, _currentPitch, ref _pitchVelocity, rotationSmoothTime);
            _currentFocusPos = Vector3.SmoothDamp(_currentFocusPos, followTarget.position, ref _focusPosVelocity, positionSmoothTime);

            // 2. Calculate the RIGID target rotation based on smoothed inputs
            Quaternion targetRotation = Quaternion.Euler(_smoothPitch, _smoothYaw, 0);

            // 3. Apply the offset relative to the CAMERA's rotation.
            Vector3 focusPositionWithOffset = _currentFocusPos + (targetRotation * targetOffset);

            // 4. Calculate desired direction
            Vector3 desiredDirection = targetRotation * Vector3.back;

            // 5. Handle Collisions
            float targetDistance = CalculateCollisionDistance(focusPositionWithOffset, desiredDirection);
            _currentDistance = Mathf.Lerp(_currentDistance, targetDistance, Time.deltaTime * 10f);

            // 6. Hard-set the transform. The orbit is perfect, the values driving it are smoothed.
            transform.position = focusPositionWithOffset + (desiredDirection * _currentDistance);
            transform.rotation = targetRotation;
        }

        private void ProcessInput()
        {
            _currentYaw += LookInput.x * lookSensitivity;

            float pitchInput = LookInput.y * lookSensitivity * (invertY ? 1 : -1);
            _currentPitch += pitchInput;
            _currentPitch = ClampAngle(_currentPitch, minPitch, maxPitch);
        }

        private float CalculateCollisionDistance(Vector3 focusPoint, Vector3 direction)
        {
            if (Physics.SphereCast(focusPoint, collisionRadius, direction, out RaycastHit hit, defaultDistance, collisionLayers))
            {
                return Mathf.Clamp(hit.distance, minCollisionDistance, defaultDistance);
            }
            return defaultDistance;
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360F) angle += 360F;
            if (angle > 360F) angle -= 360F;
            return Mathf.Clamp(angle, min, max);
        }

        public void SetTargetOffset(Vector3 newOffset)
        {
            targetOffset = newOffset;
        }
    }
}