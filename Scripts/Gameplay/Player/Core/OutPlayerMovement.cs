using UnityEngine;

namespace OutGame
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(OutPlayerController))]
    public class OutPlayerMovement : MonoBehaviour
    {
        private CharacterController _controller;
        private OutPlayerController _hub;

        [Header("References")]
        [Tooltip("Assign your Main Camera here so movement is strictly relative to the lens.")]
        [SerializeField] private Transform cameraTransform;

        #region Configuration Variables
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 2.5f;
        [SerializeField] private float sprintSpeed = 5.5f;
        [SerializeField] private float rotationSmoothTime = 0.1f;
        [SerializeField] private float speedSmoothTime = 0.1f;
        [Tooltip("The angle difference required between current direction and new input to trigger a 180 turn.")]
        [SerializeField] private float turn180AngleThreshold = 150f;
        [Tooltip("How long the turn takes, locking input during this time.")]
        [SerializeField] private float turn180Duration = 1.5f;
        #endregion

        // Add this near your other Obstacle Detection variables at the top:
        [Tooltip("If hitting an obstacle directly (angle less than this), the player stops. If greater, they slide along it.")]
        [SerializeField] private float slideAngleThreshold = 45f;

        [Header("Obstacle Detection")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float obstacleCheckDistance = 0.4f;
        [SerializeField] private float obstacleCheckRadius = 0.3f;
        [SerializeField] private float chestHeightOffset = 1.0f;

        [Header("Physics & Gravity")]
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float gravityMultiplier = 2.0f;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundDistance = 0.2f;
        [SerializeField] private LayerMask groundMask;

        private float _lastTurnTime = -10f;

        public float CurrentSpeed { get; private set; }

        private float _speedSmoothVelocity;
        private float _rotationVelocity;

        // Cache direction so momentum carries forward when keys are released
        private Vector3 _lastMoveDirection;

        // Gravity tracking
        private Vector3 _gravityVelocity;
        private bool _isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _hub = GetComponent<OutPlayerController>();

            // Failsafe if you forget to assign the camera in the inspector
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            HandleGravity(); // Gravity waits for no man

            if (_hub.currentState == MovementState.Automated)
                return; // Block standard input

            HandleMovement(_hub.MoveInput, _hub.IsSprintHeld);
        }

        private void HandleMovement(Vector2 input, bool isSprinting)
        {
            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;
            float targetSpeed = 0f;
            bool isBlocked = false;

            if (direction.magnitude >= 0.1f)
            {
                // 1. Calculate intended movement direction relative to camera
                float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                if (cameraTransform != null) targetAngle += cameraTransform.eulerAngles.y;

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

                // --- 180 TURN DETECTION ---
                if (CurrentSpeed > 0.1f && Time.time >= _lastTurnTime + turn180Duration)
                {
                    float angleDifference = Vector3.Angle(_lastMoveDirection, moveDir);
                    if (angleDifference >= turn180AngleThreshold)
                    {
                        _lastTurnTime = Time.time;
                        _hub.TriggerWalkTurn180();
                        _ = PerformAutomated180TurnAsync();
                        return; // Exit early to yield standard movement control this frame
                    }
                }

                // 2. Obstacle Detection (SphereCast)
                Vector3 rayOrigin = transform.position + (Vector3.up * chestHeightOffset);
                bool hitObstacle = Physics.SphereCast(
                    rayOrigin,
                    obstacleCheckRadius,
                    moveDir,
                    out RaycastHit hit,
                    obstacleCheckDistance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore
                );

                if (hitObstacle)
                {
                    // Calculate how directly we are hitting the wall
                    float hitAngle = Vector3.Angle(moveDir, -hit.normal);

                    if (hitAngle > slideAngleThreshold)
                    {
                        // GLIDING: We hit it at an angle. Project our movement along the wall surface.
                        moveDir = Vector3.ProjectOnPlane(moveDir, hit.normal).normalized;
                        targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
                        // Update target angle so character visually turns into the slide
                        targetAngle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                    }
                    else
                    {
                        // DIRECT HIT: Stop completely
                        isBlocked = true;
                        targetSpeed = 0f;
                    }
                }
                else
                {
                    targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
                }

                // 3. Smooth Rotation (Only turn if not pushing straight into a flat wall)
                if (!isBlocked || CurrentSpeed > 0.1f)
                {
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationVelocity, rotationSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }

                _lastMoveDirection = moveDir.normalized;
            }

            // 4. Smooth Acceleration/Deceleration
            CurrentSpeed = Mathf.SmoothDamp(CurrentSpeed, targetSpeed, ref _speedSmoothVelocity, speedSmoothTime);

            if (CurrentSpeed > 0.01f)
            {
                _controller.Move(_lastMoveDirection * (CurrentSpeed * Time.deltaTime));
            }

            // 5. Update State
            if (CurrentSpeed > 0.1f)
                _hub.ChangeState(isSprinting && !isBlocked ? MovementState.Sprinting : MovementState.Walking);
            else
                _hub.ChangeState(MovementState.Idle);
        }

        private void HandleGravity()
        {
            if (groundCheck == null) return;

            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (_isGrounded && _gravityVelocity.y < 0)
            {
                _gravityVelocity.y = -15f;
            }

            _gravityVelocity.y += gravity * gravityMultiplier * Time.deltaTime;

            _gravityVelocity.y = Mathf.Max(_gravityVelocity.y, -50f);

            _controller.Move(_gravityVelocity * Time.deltaTime);
        }

        public float GetCurrentMaxSpeed()
        {
            return _hub.currentState == MovementState.Sprinting ? sprintSpeed : walkSpeed;
        }

        /// <summary>
        /// Seizes control and physically walks the character to a specific mark.
        /// </summary>
        public async Awaitable WalkToScenarioMarkAsync(Vector3 targetPos, Quaternion targetRot, float stopDistance = 0.05f)
        {
            _hub.ChangeState(MovementState.Automated);

            // Phase 1: Walk to the spot
            while (Vector3.Distance(transform.position, targetPos) > stopDistance)
            {
                Vector3 direction = (targetPos - transform.position).normalized;
                direction.y = 0; // Don't look at the sky/floor

                // Turn towards target
                if (direction.sqrMagnitude > 0.01f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * (rotationSmoothTime * 50f));
                }

                // Move and fake the CurrentSpeed so the Animator keeps playing the walk cycle
                _controller.Move(direction * (walkSpeed * Time.deltaTime));
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, walkSpeed, Time.deltaTime * 5f);

                await Awaitable.NextFrameAsync();
            }

            // Phase 2: Stop walking and align rotation perfectly
            CurrentSpeed = 0f;
            while (Quaternion.Angle(transform.rotation, targetRot) > 0.5f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
                await Awaitable.NextFrameAsync();
            }

            // Snap it perfectly to avoid floating point drift
            transform.rotation = targetRot;

            // Note: We DO NOT change the state back to Idle here. 
            // The Scenario script will do that once the animation finishes.
        }

        #region Automated Routines

        /// <summary>
        /// Locks player input and forcefully rotates the character 180 degrees to the right.
        /// </summary>
        private async Awaitable PerformAutomated180TurnAsync()
        {
            _hub.ChangeState(MovementState.Automated);

            // 1. Capture the exact starting Y rotation
            float startY = transform.eulerAngles.y;
            // 2. Force the target to be exactly +180 degrees (Right Turn)
            float targetY = startY + 180f;

            float elapsedTime = 0f;

            while (elapsedTime < turn180Duration)
            {
                if (destroyCancellationToken.IsCancellationRequested) return;

                elapsedTime += Time.deltaTime;
                float t = elapsedTime / turn180Duration;

                // 3. Smoothly interpolate the raw float instead of the Quaternion
                float currentY = Mathf.Lerp(startY, targetY, t);
                transform.rotation = Quaternion.Euler(0f, currentY, 0f);

                // Maintain physical momentum to match the blend tree animation
                CurrentSpeed = Mathf.Lerp(CurrentSpeed, walkSpeed, Time.deltaTime * 5f);
                _controller.Move(transform.forward * (CurrentSpeed * Time.deltaTime));

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            // Snap cleanly and restore control
            transform.rotation = Quaternion.Euler(0f, targetY, 0f);
            _lastMoveDirection = transform.forward;
            _hub.ChangeState(MovementState.Idle);
        }

        #endregion
    }
}