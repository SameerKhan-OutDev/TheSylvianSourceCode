using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OutGame
{
    /// <summary>
    /// Handles a multi-floor elevator platform.
    /// Manages connected facility doors, localized audio, and camera swapping during transit.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class OutElevatorPlatform : MonoBehaviour, IOutInteractable
    {
        #region Configuration Variables

        [Header("Floor & Door Setup")]
        [Tooltip("List of transforms representing each floor. Index 0 is the starting floor.")]
        [SerializeField] private List<Transform> floors = new List<Transform>();
        [Tooltip("Facility doors to lock while the elevator is in transit.")]
        [SerializeField] private List<OutFacilityDoor> connectedDoors = new List<OutFacilityDoor>();

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float confirmationDelay = 2.0f;

        [Header("Camera Swapping")]
        [SerializeField] private GameObject thirdPersonCamera;
        [SerializeField] private GameObject elevatorCamera;

        [Header("Audio Settings")]
        [SerializeField] private SoundType elevatorStartSound = SoundType.ElevatorStart;
        [SerializeField] private SoundType elevatorMovingSound = SoundType.ElevatorMoving;
        [SerializeField] private SoundType elevatorArriveSound = SoundType.ElevatorArrive;
        [SerializeField] private SoundType elevatorErrorSound = SoundType.ElevatorError;
        [SerializeField] private SoundType inputTickSound = SoundType.SliderTick;

        [Header("System Integrity")]
        [Tooltip("If true, panel inputs are locked out until telekinetically unjammed.")]
        [SerializeField] private bool _isJammed = false;
        [Tooltip("The floor index to force the elevator to upon being unjammed.")]
        [SerializeField] private int _unjamTargetFloorIndex = 0;

        public List<Renderer> RendererObjects => rendererObjects;
        [SerializeField] List<Renderer> rendererObjects;

        #endregion

        #region Interface Properties
        public EOutInteractableState CurrentState { get; private set; } = EOutInteractableState.Dormant;
        #endregion

        #region Internal State

        private int _currentFloorIndex = 0;
        private int _targetFloorIndex = 0;
        private ElevatorState _currentState = ElevatorState.Idle;

        private bool _isPlayerInside = false;
        private CancellationTokenSource _inputCts;

        #endregion

        #region Interface Implementation

        public void OnAimEnter()
        {
            if (CurrentState == EOutInteractableState.Dormant) CurrentState = EOutInteractableState.Hovered;
        }

        public void OnAimExit()
        {
            if (CurrentState == EOutInteractableState.Hovered || CurrentState == EOutInteractableState.Interacting)
            {
                CurrentState = EOutInteractableState.Dormant;
            }
        }

        public async Awaitable ExecuteInteractionAsync(Transform a_instigator, Action<float> a_onProgress = null)
        {
            if (!_isJammed || _currentState == ElevatorState.Moving) return;

            float hackDuration = 2.0f;
            float elapsedTime = 0f;

            while (elapsedTime < hackDuration)
            {
                elapsedTime += Time.deltaTime;

                // Updates the Reticle UI directly
                a_onProgress?.Invoke(elapsedTime / hackDuration);

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            // Hack complete, reset the UI fill
            a_onProgress?.Invoke(1f);

            CurrentState = EOutInteractableState.Interacting;
            CancelWaitTimer(); // Stop any pending inputs

            // Optional: Play a loud heavy machinery unlocking sound

            _isJammed = false; // Lift the mechanical lockdown
            _targetFloorIndex = Mathf.Clamp(_unjamTargetFloorIndex, 0, floors.Count - 1);

            CurrentState = EOutInteractableState.Cooldown;

            // Force the elevator to move to the emergency target floor immediately
            if (_targetFloorIndex != _currentFloorIndex)
            {
                await MoveToTargetFloorAsync();
            }

            CurrentState = EOutInteractableState.Dormant;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;

            if (floors.Count > 0)
            {
                // Find which floor the elevator was placed closest to in the Editor
                float minDistance = float.MaxValue;
                int startingIndex = 0;

                for (int i = 0; i < floors.Count; i++)
                {
                    if (floors[i] == null) continue;

                    float dist = Vector3.Distance(transform.position, floors[i].position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        startingIndex = i;
                    }
                }

                // Set internal state to match the detected starting floor
                _currentFloorIndex = startingIndex;
                _targetFloorIndex = startingIndex;

                // Snap to perfectly align with the target floor
                transform.position = floors[startingIndex].position;
            }

            ToggleCameras(false);
        }

        private void OnDisable()
        {
            CancelWaitTimer();
            UnsubscribeFromInputs();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !_isPlayerInside)
            {
                _isPlayerInside = true;
                other.transform.SetParent(transform);
                DisplayElevatorHint();
                SubscribeToInputs();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _isPlayerInside = false;
                other.transform.SetParent(null);
                HideElevatorHint();
                UnsubscribeFromInputs();
            }
        }

        #endregion

        #region Public Input Interface

        public void TapLiftUp() => RegisterTap(1);
        public void TapLiftDown() => RegisterTap(-1);

        #endregion

        #region Core Logic

        private void RegisterTap(int directionModifier)
        {
            if (_isJammed)
            {
                PlaySoundSafe(elevatorErrorSound, true);
                return;
            }

            if (_currentState == ElevatorState.Moving || floors.Count == 0) return;

            int nextFloor = Mathf.Clamp(_targetFloorIndex + directionModifier, 0, floors.Count - 1);

            // Reached max/min constraints
            if (nextFloor == _targetFloorIndex && _currentState != ElevatorState.WaitingForConfirmation)
            {
                PlaySoundSafe(elevatorErrorSound, true);
                return;
            }

            _targetFloorIndex = nextFloor;
            _currentState = ElevatorState.WaitingForConfirmation;

            PlaySoundSafe(inputTickSound, true);
            _ = AwaitConfirmationAsync();
        }

        private async Awaitable AwaitConfirmationAsync()
        {
            CancelWaitTimer();
            _inputCts = new CancellationTokenSource();

            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_inputCts.Token, destroyCancellationToken);

            try
            {
                await Awaitable.WaitForSecondsAsync(confirmationDelay, linkedCts.Token);

                if (_targetFloorIndex != _currentFloorIndex)
                {
                    _ = MoveToTargetFloorAsync();
                }
                else
                {
                    _currentState = ElevatorState.Idle;
                }
            }
            catch (System.OperationCanceledException)
            {
                // Cancelled by another input tap or object destruction
            }
        }

        private async Awaitable MoveToTargetFloorAsync()
        {
            _currentState = ElevatorState.Moving;
            Transform targetTransform = floors[_targetFloorIndex];

            SetDoorsLockState(true);
            ToggleCameras(true);

            PlaySoundSafe(elevatorStartSound, true, transform.position);
            PlaySoundSafe(elevatorMovingSound, false, transform.position); // Fades in looping drone

            while (transform.position != targetTransform.position)
            {
                if (destroyCancellationToken.IsCancellationRequested) return;

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetTransform.position,
                    moveSpeed * Time.deltaTime
                );

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            _currentFloorIndex = _targetFloorIndex;
            _currentState = ElevatorState.Idle;

            SetDoorsLockState(false);
            ToggleCameras(false);

            if (OutSoundManager.Instance != null)
            {
                OutSoundManager.Instance.StopSound(elevatorMovingSound, false); // Fades out drone
                OutSoundManager.Instance.PlaySFX(elevatorArriveSound, true, transform.position);
            }
        }

        #endregion

        #region Helpers

        private void SetDoorsLockState(bool isLocked)
        {
            foreach (var door in connectedDoors)
            {
                if (door != null) door.SetLockState(isLocked);
            }
        }

        private void ToggleCameras(bool isElevatorMoving)
        {
            if (thirdPersonCamera != null) thirdPersonCamera.SetActive(!isElevatorMoving);
            if (elevatorCamera != null) elevatorCamera.SetActive(isElevatorMoving);
        }

        private void PlaySoundSafe(SoundType type, bool snap, Vector3? pos = null)
        {
            if (OutSoundManager.Instance == null) return;

            if (pos.HasValue) OutSoundManager.Instance.PlaySFX(type, snap, pos.Value);
            else OutSoundManager.Instance.PlayUISound(type, snap);
        }

        private void CancelWaitTimer()
        {
            if (_inputCts != null)
            {
                _inputCts.Cancel();
                _inputCts.Dispose();
                _inputCts = null;
            }
        }

        private string GetBindingDisplayString(InputAction inputAction)
        {
            if (inputAction == null) return "?";

            System.Collections.Generic.List<string> displayParts = new System.Collections.Generic.List<string>();

            for (int i = 0; i < inputAction.bindings.Count; i++)
            {
                if (inputAction.bindings[i].isComposite) continue;

                string part = inputAction.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);

                if (!string.IsNullOrEmpty(part) && !displayParts.Contains(part))
                {
                    displayParts.Add(part);
                }
            }

            return displayParts.Count > 0 ? string.Join(" / ", displayParts) : "?";
        }

        private void DisplayElevatorHint()
        {
            GameplayUI ui = FindAnyObjectByType<GameplayUI>();
            if (ui != null && ui.gameplayHintsPanel != null)
            {
                var inputActions = OutInputManager.Instance.InputActions.Player;

                string upBinding = GetBindingDisplayString(inputActions.InteractV8);
                string downBinding = GetBindingDisplayString(inputActions.InteractV2);

                string hintText = $"Tap [{upBinding}] to go up\nTap [{downBinding}] to go down\n(Max: {floors.Count}).\nWait {confirmationDelay}s to confirm.";

                ui.gameplayHintsPanel.DisplayHint(hintText);
            }
        }

        private void HideElevatorHint()
        {
            GameplayUI ui = FindAnyObjectByType<GameplayUI>();
            if (ui != null && ui.gameplayHintsPanel != null)
            {
                ui.gameplayHintsPanel.HideHint();
            }
        }

        #endregion

        #region Input Handling

        private void SubscribeToInputs()
        {
            if (OutInputManager.Instance == null) return;

            var playerActions = OutInputManager.Instance.InputActions.Player;
            playerActions.InteractV8.performed += OnLiftUpPressed;
            playerActions.InteractV2.performed += OnLiftDownPressed;
        }

        private void UnsubscribeFromInputs()
        {
            if (OutInputManager.Instance == null) return;

            var playerActions = OutInputManager.Instance.InputActions.Player;
            playerActions.InteractV8.performed -= OnLiftUpPressed;
            playerActions.InteractV2.performed -= OnLiftDownPressed;
        }

        private void OnLiftUpPressed(InputAction.CallbackContext context) => TapLiftUp();
        private void OnLiftDownPressed(InputAction.CallbackContext context) => TapLiftDown();

        #endregion
    }
}