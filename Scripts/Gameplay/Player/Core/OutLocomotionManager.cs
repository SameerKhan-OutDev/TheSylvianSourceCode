using DG.Tweening;
using MxM;
using MxMGameplay;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

namespace OutGame
{
    /// <summary>
    /// Handles grounded movement states, trajectory biasing, and jump event execution for MxM.
    /// </summary>
    public class OutLocomotionManager : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Component References")]
        [SerializeField] private MxMAnimator m_mxmAnimator;
        [SerializeField] private MxMTrajectoryGenerator m_trajectoryGenerator;
        [SerializeField] private MxMRootMotionApplicator m_rootMotionApplicator;
        [SerializeField] private GenericControllerWrapper m_controllerWrapper;

        [Header("Aiming - Rigging")]
        [Tooltip("The root Rig component containing your Two Bone IK Constraints")]
        [SerializeField] private TwoBoneIKConstraint m_aimRig;
        [SerializeField] private float m_transitionDuration = 0.25f;

        [Header("Speeds & Biases")]
        [SerializeField] private float walkSpeed = 2.0f;
        [SerializeField] private float jogSpeed = 4.3f;
        [SerializeField] private float sprintSpeed = 6.7f;
        [SerializeField] private Vector2 walkBias = new Vector2(10f, 10f);
        [SerializeField] private Vector2 jogBias = new Vector2(10f, 10f);
        [SerializeField] private Vector2 sprintBias = new Vector2(6f, 6f);
        [SerializeField] private float favourMultiplier = 0.6f;

        [Space]

        [SerializeField] private float m_pushPower = 2.0f;
        [SerializeField] private float m_weightLimit = 50f;

        [Header("Jump Settings & Exploits")]
        [Tooltip("Layers considered solid obstacles that should block a jump.")]
        [SerializeField] private LayerMask m_jumpObstacleLayer = 1; // Default to 1 (Default layer)
        [Tooltip("How far ahead to check for walls before allowing a jump.")]
        [SerializeField] private float m_jumpCheckDistance = 1.2f;

        [Tooltip("If true, bypasses the obstacle check, allowing the player to clip through walls and doors.")]
        [SerializeField] private bool m_enableJumpClippingExploit = false;

        [Header("Input Profiles")]
        [SerializeField] private MxMInputProfile generalLocomotionProfile;
        [SerializeField] private MxMInputProfile sprintLocomotionProfile;

        [Header("Aiming - Camera (CM3)")]
        [SerializeField] private CinemachineCamera m_cinemachineCam;
        [SerializeField] private float m_aimFOV = 25f;
        [SerializeField] private float m_normalFOV = 40f;
        [Tooltip("Offset applied to Orbital Follow to push the camera over the shoulder")]
        [SerializeField] private Vector3 m_aimOffset = new Vector3(0.5f, 0f, 0f);
        [SerializeField] private Vector3 m_normalOffset = Vector3.zero;

        [Header("Events & Tags")]
        [SerializeField] private MxMEventDefinition jumpDefinition;

        public static event System.Action<bool> OnAimStateChanged;

        [SerializeField] private string runTagName = "Run";
        [SerializeField] private string sprintTagName = "Sprint";
        #endregion

        #region Private Fields
        private OutLocomotionState currentState = OutLocomotionState.Walk;
        private bool isJogToggled = false;
        private float sprintPressTime = 0f;
        private float holdThreshold = 0.2f;
        private ETags runTagHandle;
        private ETags sprintTagHandle;

        private CinemachineOrbitalFollow m_orbitalFollow;
        private Tween m_rigTween;
        private Tween m_fovTween;
        private Tween m_offsetTween;
        #endregion

        #region Unity Messages

        private void Awake()
        {
            if (m_cinemachineCam != null)
            {
                m_cinemachineCam.TryGetComponent(out m_orbitalFollow);
            }
        }
        private void Start()
        {
            runTagHandle = m_mxmAnimator.CurrentAnimData.FavourTagFromName(runTagName);
            sprintTagHandle = m_mxmAnimator.CurrentAnimData.FavourTagFromName(sprintTagName);

            m_mxmAnimator.SetFavourMultiplier(favourMultiplier);
            SetLocomotionState(OutLocomotionState.Walk);
        }

        private void Update()
        {
            if (currentState == OutLocomotionState.Jump)
            {
                UpdateJumpState();
                return;
            }

            ToggleAiming(OutInputManager.Instance.InputActions.Player.Aim.IsPressed());

            HandleJumpInput();

            // Do not override locomotion trajectories if the agent is falling
            if (m_controllerWrapper != null && !m_controllerWrapper.IsGrounded)
                return;

            HandleLocomotionInput();

            Vector2 rawInput = OutInputManager.Instance.InputActions.Player.Move.ReadValue<Vector2>();
            m_trajectoryGenerator.InputVector = new Vector3(rawInput.x, 0f, rawInput.y);
        }

        /// <summary>
        /// Fires continuously while the CharacterController intersects with another collider during its Move() cycle.
        /// </summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;

            if (rb == null || rb.isKinematic)
                return;

            if (rb.mass > m_weightLimit)
                return;

            if (hit.moveDirection.y < -0.3f)
                return;

            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);

            rb.AddForceAtPosition(pushDir * m_pushPower, hit.point, ForceMode.Impulse);
        }

        private void OnDestroy()
        {
            m_rigTween?.Kill();
            m_fovTween?.Kill();
            m_offsetTween?.Kill();
        }
        #endregion

        #region Movement Logic
        private void HandleLocomotionInput()
        {
            var sprintAction = OutInputManager.Instance.InputActions.Player.Sprint;
            var moveAction = OutInputManager.Instance.InputActions.Player.Move;

            bool isGamepad = moveAction.activeControl != null && moveAction.activeControl.device is Gamepad;

            if (sprintAction.WasPressedThisFrame())
            {
                sprintPressTime = Time.time;
            }

            if (sprintAction.IsPressed() && (Time.time - sprintPressTime) >= holdThreshold)
            {
                if (currentState != OutLocomotionState.Sprint)
                {
                    SetLocomotionState(OutLocomotionState.Sprint);
                }
                return;
            }

            if (isGamepad)
            {
                if (currentState != OutLocomotionState.Sprint)
                {
                    float inputMag = moveAction.ReadValue<Vector2>().sqrMagnitude;
                    if (inputMag >= 0.49f)
                    {
                        if (currentState != OutLocomotionState.Jog) SetLocomotionState(OutLocomotionState.Jog);
                    }
                    else
                    {
                        if (currentState != OutLocomotionState.Walk) SetLocomotionState(OutLocomotionState.Walk);
                    }
                }
                else if (sprintAction.WasReleasedThisFrame())
                {
                    SetLocomotionState(moveAction.ReadValue<Vector2>().sqrMagnitude >= 0.49f ? OutLocomotionState.Jog : OutLocomotionState.Walk);
                }
            }
            else
            {
                if (sprintAction.WasReleasedThisFrame())
                {
                    if (currentState == OutLocomotionState.Sprint)
                    {
                        SetLocomotionState(isJogToggled ? OutLocomotionState.Jog : OutLocomotionState.Walk);
                    }
                    else
                    {
                        isJogToggled = !isJogToggled;
                        SetLocomotionState(isJogToggled ? OutLocomotionState.Jog : OutLocomotionState.Walk);
                    }
                }
            }
        }

        private void SetLocomotionState(OutLocomotionState newState)
        {
            currentState = newState;
            m_mxmAnimator.RemoveFavourTags(runTagHandle);
            m_mxmAnimator.RemoveFavourTags(sprintTagHandle);

            switch (currentState)
            {
                case OutLocomotionState.Walk:
                    m_trajectoryGenerator.MaxSpeed = walkSpeed;
                    m_trajectoryGenerator.PositionBias = walkBias.x;
                    m_trajectoryGenerator.DirectionBias = walkBias.y;
                    m_trajectoryGenerator.InputProfile = generalLocomotionProfile;
                    m_mxmAnimator.SetCalibrationData("General");
                    break;

                case OutLocomotionState.Jog:
                    m_trajectoryGenerator.MaxSpeed = jogSpeed;
                    m_trajectoryGenerator.PositionBias = jogBias.x;
                    m_trajectoryGenerator.DirectionBias = jogBias.y;
                    m_trajectoryGenerator.InputProfile = generalLocomotionProfile;
                    m_mxmAnimator.AddFavourTags(runTagHandle);
                    m_mxmAnimator.SetCalibrationData("General");
                    break;

                case OutLocomotionState.Sprint:
                    m_trajectoryGenerator.MaxSpeed = sprintSpeed;
                    m_trajectoryGenerator.PositionBias = sprintBias.x;
                    m_trajectoryGenerator.DirectionBias = sprintBias.y;
                    m_trajectoryGenerator.InputProfile = sprintLocomotionProfile;
                    m_mxmAnimator.AddFavourTags(sprintTagHandle);
                    m_mxmAnimator.SetCalibrationData("Sprint");
                    break;
            }
        }
        #endregion

        #region Core Logic
        public void ToggleAiming(bool isAiming)
        {
            // 1. Animation Rigging (Smooth Weight Transition)
            if (m_aimRig != null)
            {
                float targetRigWeight = isAiming ? 1f : 0f;
                m_rigTween?.Kill();
                m_rigTween = DOTween.To(() => m_aimRig.weight, x => m_aimRig.weight = x, targetRigWeight, m_transitionDuration).SetEase(Ease.InOutSine).SetUpdate(UpdateType.Late);
            }

            // 2. MxM Trajectory Mode
            if (m_trajectoryGenerator != null)
            {
                m_trajectoryGenerator.TrajectoryMode = isAiming ? ETrajectoryMoveMode.Strafe : ETrajectoryMoveMode.Normal;
            }

            // 3. Camera Shift
            HandleCameraShift(isAiming);

            // 4. Broadcast to UI / Scanner
            OnAimStateChanged?.Invoke(isAiming);
        }

        private void HandleCameraShift(bool isAiming)
        {
            if (m_cinemachineCam == null || m_orbitalFollow == null) return;

            m_fovTween?.Kill();
            m_offsetTween?.Kill();

            float targetFOV = isAiming ? m_aimFOV : m_normalFOV;
            Vector3 targetOffset = isAiming ? m_aimOffset : m_normalOffset;

            // CM3 Lens is a struct, so we use DOTween's getter/setter structure to modify it safely
            m_fovTween = DOTween.To(
                () => m_cinemachineCam.Lens.FieldOfView,
                x =>
                {
                    var lens = m_cinemachineCam.Lens;
                    lens.FieldOfView = x;
                    m_cinemachineCam.Lens = lens;
                },
                targetFOV,
                m_transitionDuration
            ).SetEase(Ease.OutCubic);
        }
        #endregion

        #region Jump Logic
        /// <summary>
        /// Captures jump input, calculates landing trajectory via raycast, and initiates the MxM jump event.
        /// </summary>
        /// <summary>
        /// Captures jump input, checks for obstacles to prevent clipping, calculates landing trajectory, and initiates the MxM jump.
        /// </summary>
        private void HandleJumpInput()
        {
            var jumpAction = OutInputManager.Instance.InputActions.Player.Jump;

            if (jumpAction.WasPressedThisFrame() && jumpDefinition != null && m_controllerWrapper.IsGrounded)
            {
                // --- BUG FIX: Prevent jumping through walls ---
                if (!m_enableJumpClippingExploit)
                {
                    // Create a capsule matching the character's physical bounds
                    Vector3 p1 = transform.position + (Vector3.up * m_controllerWrapper.Radius);
                    Vector3 p2 = transform.position + (Vector3.up * (m_controllerWrapper.Height - m_controllerWrapper.Radius));

                    // Determine jump direction (use momentum direction, or fallback to facing direction)
                    Vector3 jumpDir = m_mxmAnimator.BodyVelocity.sqrMagnitude > 0.1f
                        ? m_mxmAnimator.BodyVelocity.normalized
                        : transform.forward;

                    // Sweep the capsule forward. If it hits an obstacle on the chosen layer, abort the jump.
                    if (Physics.CapsuleCast(p1, p2, m_controllerWrapper.Radius, jumpDir, out _, m_jumpCheckDistance, m_jumpObstacleLayer, QueryTriggerInteraction.Ignore))
                    {
                        return; // Wall detected, jump blocked.
                    }
                }
                // ----------------------------------------------

                jumpDefinition.ClearContacts();
                jumpDefinition.AddDummyContacts(1);
                m_mxmAnimator.BeginEvent(jumpDefinition);

                ref readonly EventContact eventContact = ref m_mxmAnimator.NextEventContactRoot_Actual_World;

                Ray ray = new Ray(eventContact.Position + (Vector3.up * 3.5f), Vector3.down);
                RaycastHit rayHit;

                if (Physics.Raycast(ray, out rayHit, 10f) && rayHit.distance > 1.5f && rayHit.distance < 5f)
                {
                    m_mxmAnimator.ModifyDesiredEventContactPosition(rayHit.point);
                }
                else
                {
                    m_mxmAnimator.ModifyDesiredEventContactPosition(eventContact.Position);
                }

                m_rootMotionApplicator.EnableGravity = false;
                SetLocomotionState(OutLocomotionState.Jump);
            }
        }

        private void UpdateJumpState()
        {
            if (m_mxmAnimator.IsEventComplete) //[cite: 1]
            {
                m_rootMotionApplicator.EnableGravity = true; //[cite: 1]
                SetLocomotionState(OutLocomotionState.Walk);
            }
        }
        #endregion

    }
}