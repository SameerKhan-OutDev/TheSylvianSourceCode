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
        [Header("Configuration")]
        [SerializeField] private PlayerLocomotionConfig m_config;

        [Header("Component References")]
        [SerializeField] private MxMAnimator m_mxmAnimator;
        [SerializeField] private MxMTrajectoryGenerator m_trajectoryGenerator;
        [SerializeField] private MxMRootMotionApplicator m_rootMotionApplicator;
        [SerializeField] private GenericControllerWrapper m_controllerWrapper;

        [Header("Aiming - Rigging")]
        [Tooltip("The root Rig component containing your Two Bone IK Constraints")]
        [SerializeField] private TwoBoneIKConstraint m_aimRig;

        [Header("Aiming - Camera (CM3)")]
        [SerializeField] private CinemachineCamera m_cinemachineCam;

        public static event System.Action<bool> OnAimStateChanged;

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

            if (m_config == null)
            {
                Debug.LogError("[OutLocomotionManager] Configuration missing! Please assign a PlayerLocomotionConfig.");
            }
        }

        private void Start()
        {
            runTagHandle = m_mxmAnimator.CurrentAnimData.FavourTagFromName(m_config.runTagName);
            sprintTagHandle = m_mxmAnimator.CurrentAnimData.FavourTagFromName(m_config.sprintTagName);

            m_mxmAnimator.SetFavourMultiplier(m_config.favourMultiplier);
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

            if (rb.mass > m_config.weightLimit)
                return;

            if (hit.moveDirection.y < -0.3f)
                return;

            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);

            rb.AddForceAtPosition(pushDir * m_config.pushPower, hit.point, ForceMode.Impulse);
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
                    m_trajectoryGenerator.MaxSpeed = m_config.walkSpeed;
                    m_trajectoryGenerator.PositionBias = m_config.walkBias.x;
                    m_trajectoryGenerator.DirectionBias = m_config.walkBias.y;
                    m_trajectoryGenerator.InputProfile = m_config.generalLocomotionProfile;
                    m_mxmAnimator.SetCalibrationData("General");
                    break;

                case OutLocomotionState.Jog:
                    m_trajectoryGenerator.MaxSpeed = m_config.jogSpeed;
                    m_trajectoryGenerator.PositionBias = m_config.jogBias.x;
                    m_trajectoryGenerator.DirectionBias = m_config.jogBias.y;
                    m_trajectoryGenerator.InputProfile = m_config.generalLocomotionProfile;
                    m_mxmAnimator.AddFavourTags(runTagHandle);
                    m_mxmAnimator.SetCalibrationData("General");
                    break;

                case OutLocomotionState.Sprint:
                    m_trajectoryGenerator.MaxSpeed = m_config.sprintSpeed;
                    m_trajectoryGenerator.PositionBias = m_config.sprintBias.x;
                    m_trajectoryGenerator.DirectionBias = m_config.sprintBias.y;
                    m_trajectoryGenerator.InputProfile = m_config.sprintLocomotionProfile;
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
                m_rigTween = DOTween.To(() => m_aimRig.weight, x => m_aimRig.weight = x, targetRigWeight, m_config.transitionDuration).SetEase(Ease.InOutSine).SetUpdate(UpdateType.Late);
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

            float targetFOV = isAiming ? m_config.aimFOV : m_config.normalFOV;
            Vector3 targetOffset = isAiming ? m_config.aimOffset : m_config.normalOffset;

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
                m_config.transitionDuration
            ).SetEase(Ease.OutCubic);
        }
        #endregion

        #region Jump Logic
        private void HandleJumpInput()
        {
            var jumpAction = OutInputManager.Instance.InputActions.Player.Jump;

            if (jumpAction.WasPressedThisFrame() && m_config.jumpDefinition != null && m_controllerWrapper.IsGrounded)
            {
                // --- BUG FIX: Prevent jumping through walls ---
                if (!m_config.enableJumpClippingExploit)
                {
                    // Create a capsule matching the character's physical bounds
                    Vector3 p1 = transform.position + (Vector3.up * m_controllerWrapper.Radius);
                    Vector3 p2 = transform.position + (Vector3.up * (m_controllerWrapper.Height - m_controllerWrapper.Radius));

                    // Determine jump direction (use momentum direction, or fallback to facing direction)
                    Vector3 jumpDir = m_mxmAnimator.BodyVelocity.sqrMagnitude > 0.1f
                        ? m_mxmAnimator.BodyVelocity.normalized
                        : transform.forward;

                    // Sweep the capsule forward. If it hits an obstacle on the chosen layer, abort the jump.
                    if (Physics.CapsuleCast(p1, p2, m_controllerWrapper.Radius, jumpDir, out _, m_config.jumpCheckDistance, m_config.jumpObstacleLayer, QueryTriggerInteraction.Ignore))
                    {
                        return; // Wall detected, jump blocked.
                    }
                }
                // ----------------------------------------------

                m_config.jumpDefinition.ClearContacts();
                m_config.jumpDefinition.AddDummyContacts(1);
                m_mxmAnimator.BeginEvent(m_config.jumpDefinition);

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
            if (m_mxmAnimator.IsEventComplete)
            {
                m_rootMotionApplicator.EnableGravity = true;
                SetLocomotionState(OutLocomotionState.Walk);
            }
        }
        #endregion

    }
}