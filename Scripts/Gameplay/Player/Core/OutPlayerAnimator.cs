using UnityEngine;

namespace OutGame
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(OutPlayerController))]
    [RequireComponent(typeof(OutPlayerMovement))]
    public class OutPlayerAnimator : MonoBehaviour
    {
        private Animator _animator;
        private OutPlayerController _hub;
        private OutPlayerMovement _movement;

        [Header("Animation Settings")]
        [SerializeField] private float animationBlendSpeed = 15f;

        [Header("Leaning Settings")]
        [Tooltip("Controls how much the character leans (X value) when the camera/character turns.")]
        [SerializeField] private float leanSensitivity = 0.05f;
        private float _lastRotationY;

        private float _currentAnimX;
        private float _currentAnimY;


        #region Animation Hashes
        // Hashes
        private readonly int _animHashX = Animator.StringToHash("X");
        private readonly int _animHashY = Animator.StringToHash("Y");
        private readonly int _animHashIdle = Animator.StringToHash("Idle");
        private readonly int _animHashIsWalking = Animator.StringToHash("IsWalking");
        private readonly int _animHashIsSprinting = Animator.StringToHash("IsSprinting");
        private readonly int _animHashWalkTurn180 = Animator.StringToHash("WalkTurn180"); 
        #endregion

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _hub = GetComponent<OutPlayerController>();
            _movement = GetComponent<OutPlayerMovement>();
        }

        #region Unity Lifecycle
        private void OnEnable()
        {
            _hub.OnStateChanged += UpdateAnimationState;
            _hub.OnWalkTurn180 += TriggerTurn180Animation;
        }

        private void OnDisable()
        {
            _hub.OnStateChanged -= UpdateAnimationState;
            _hub.OnWalkTurn180 -= TriggerTurn180Animation;
        } 
        #endregion

        private void Update()
        {
            UpdateBlendTree();
        }

        #region Animation Callbacks
        // Add this method to handle the trigger
        private void TriggerTurn180Animation()
        {
            _animator.SetTrigger(_animHashWalkTurn180);
        }
        #endregion

        private void UpdateAnimationState(MovementState newState)
        {
            _animator.SetBool(_animHashIdle, newState == MovementState.Idle);
            _animator.SetBool(_animHashIsWalking, newState == MovementState.Walking);
            _animator.SetBool(_animHashIsSprinting, newState == MovementState.Sprinting);
        }

        private void UpdateBlendTree()
        {
            float currentMaxSpeed = _movement.GetCurrentMaxSpeed();
            if (currentMaxSpeed <= 0f) currentMaxSpeed = 1f;

            float speedRatio = _movement.CurrentSpeed / currentMaxSpeed;

            // Y VALUE (Forward Movement): 
            // Since the character rotates to face the input, all movement is essentially forward.
            // We just use the raw magnitude (0 to 1) multiplied by our physical speed ratio.
            // FIX: If we are automated, ignore thumbstick input and just use our physical speed.
            float targetY;
            if (_hub.currentState == MovementState.Automated)
            {
                targetY = speedRatio;
            }
            else
            {
                targetY = Mathf.Clamp01(_hub.MoveInput.magnitude) * speedRatio;
            }

            // X VALUE (Leaning / Strafing):
            // Calculate how fast the character is rotating to simulate leaning into turns.
            float currentRotationY = transform.eulerAngles.y;
            float rotationDelta = Mathf.DeltaAngle(_lastRotationY, currentRotationY);
            _lastRotationY = currentRotationY;

            float targetX = 0f;
            // Only apply the lean if we are actually moving
            if (speedRatio > 0.01f)
            {
                // Angular velocity (degrees per second)
                float angularVelocity = rotationDelta / Time.deltaTime;

                // Multiply by sensitivity and clamp between -1 (Left) and 1 (Right)
                targetX = Mathf.Clamp(angularVelocity * leanSensitivity, -1f, 1f);
            }

            // Smooth the parameters so the lean eases in and out
            _currentAnimX = Mathf.Lerp(_currentAnimX, targetX, Time.deltaTime * animationBlendSpeed);
            _currentAnimY = Mathf.Lerp(_currentAnimY, targetY, Time.deltaTime * animationBlendSpeed);

            _animator.SetFloat(_animHashX, _currentAnimX);
            _animator.SetFloat(_animHashY, _currentAnimY);
        }
    }
}