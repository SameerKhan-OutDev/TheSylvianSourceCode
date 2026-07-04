using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Handles proximity-based opening and closing of facility doors.
    /// Supports single horizontal, single vertical, and horizontal two-part doors.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(AudioSource))]
    public class OutFacilityDoor : MonoBehaviour, IOutInteractable
    {
        #region Configuration Variables

        [Header("Door Configuration")]
        [SerializeField] private DoorType doorType = DoorType.HorizontalTwoPart;

        [Header("Door Parts")]
        [Tooltip("Used for all door types. Acts as the main door for single Horizontal/Vertical doors.")]
        [SerializeField] private Transform leftDoorPart;
        [Tooltip("Used only for Horizontal Two Part doors.")]
        [SerializeField] private Transform rightDoorPart;

        [Header("Sliding Settings")]
        [SerializeField] private Vector3 leftOpenOffset = new Vector3(-1.5f, 0, 0);
        [SerializeField] private Vector3 rightOpenOffset = new Vector3(1.5f, 0, 0);
        [SerializeField] private float slideDuration = 0.5f;

        [Header("Sensor Settings")]
        [Tooltip("Check this if the door parent is rotated 180 degrees and the left/right detection is flipped.")]
        [SerializeField] private bool invertSensorAxis = false;
        [SerializeField] private float middleZoneThreshold = 0.6f;
        [SerializeField] private float scanDelay = 0.5f;

        [Header("Audio Settings")]
        [SerializeField] private SoundData globalSoundData;
        [SerializeField] private SoundType scanSuccessSound = SoundType.FacilityDoorVerification;
        [SerializeField] private SoundType scanFailedSound = SoundType.ErrorBuzz1;
        [SerializeField] private SoundType doorOpeningSound = SoundType.FacilityTwoStepDoor;

        [Header("System Integrity")]
        [Tooltip("If true, the proximity sensor is dead. Requires telekinetic override.")]
        public bool _isJammed = false;

        [Tooltip("How long (in seconds) the door functions normally before re-jamming. 0 = Permanent.")]
        [SerializeField] private float m_unjammedDuration = 0f;

        [Header("UI & Warnings")]
        [Tooltip("Canvas group containing the LOCKED icons. Blinks when jammed and player is near.")]
        [SerializeField] private CanvasGroup m_lockedWarningGroup;
        [Tooltip("Canvas group containing the UNLOCKED icons. Flashes when successfully unjammed.")]
        [SerializeField] private CanvasGroup m_unlockedWarningGroup;
        [SerializeField] private float m_warningBlinkSpeed = 4.0f;

        [SerializeField] List<Renderer> rendererObjects;

        #region Interface Properties
        public EOutInteractableState CurrentState { get; private set; } = EOutInteractableState.Dormant;

        public List<Renderer> RendererObjects => rendererObjects;
        #endregion

        #endregion

        #region Internal State

        private AudioSource _leftAudioSource;
        private AudioSource _rightAudioSource;
        private AudioSource _maindoorAudioSource;

        private Vector3 _leftClosedPos;
        private Vector3 _rightClosedPos;

        private bool _isPlayerInZone;
        private bool _isScanning;
        private bool _isLocked;

        private Vector3 _leftTargetPos;
        private Vector3 _rightTargetPos;

        private bool _isLeftOpen;
        private bool _isRightOpen;

        private CancellationTokenSource _warningBlinkCts;
        private CancellationTokenSource _unlockedSequenceCts;

        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            _maindoorAudioSource = GetComponent<AudioSource>();

            if (leftDoorPart != null)
            {
                _leftClosedPos = leftDoorPart.localPosition;
                _leftAudioSource = SetupAudioSource(leftDoorPart.gameObject);
            }

            if (rightDoorPart != null)
            {
                _rightClosedPos = rightDoorPart.localPosition;
                _rightAudioSource = SetupAudioSource(rightDoorPart.gameObject);
            }

            _leftTargetPos = _leftClosedPos;
            _rightTargetPos = _rightClosedPos;

            // Ensure the canvases are hidden on startup
            if (m_lockedWarningGroup != null) m_lockedWarningGroup.alpha = 0f;
            if (m_unlockedWarningGroup != null) m_unlockedWarningGroup.alpha = 0f;
        }

        private void Start()
        {
            _ = AnimateDoorsAsync();
        }

        private void OnDisable()
        {
            StopWarningBlink();
            StopUnlockedSequence();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !_isPlayerInZone && !_isLocked)
            {
                _isPlayerInZone = true;

                if (_isJammed)
                {
                    StartWarningBlink();
                }

                _ = PerformScanAndOpen(other.transform);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                StopWarningBlink();
                CloseDoors();
            }
        }

        #endregion

        #region Public Interface (Upgraded)

        public void SetLockState(bool lockState)
        {
            _isLocked = lockState;
            _isJammed = lockState;
            if (_isLocked && _isPlayerInZone)
            {
                CloseDoors();
            }
        }

        public void ChangeLockDuration(float durationInSeconds)
        {
            m_unjammedDuration = durationInSeconds;
        }

        public void OnAimEnter()
        {
            if (CurrentState == EOutInteractableState.Dormant && _isJammed)
            {
                CurrentState = EOutInteractableState.Hovered;
            }
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
            if (!_isJammed || CurrentState == EOutInteractableState.Interacting) return;

            // Lock the state immediately so multiple calls don't stack
            CurrentState = EOutInteractableState.Interacting;

            float hackDuration = 2.0f;
            float elapsedTime = 0f;

            while (elapsedTime < hackDuration)
            {
                if (!OutInputManager.Instance.InputActions.Player.Interact.IsPressed() || CurrentState == EOutInteractableState.Dormant)
                {
                    a_onProgress?.Invoke(0f);

                    if (CurrentState != EOutInteractableState.Dormant)
                    {
                        CurrentState = EOutInteractableState.Hovered;
                    }
                    return;
                }

                elapsedTime += Time.deltaTime;
                a_onProgress?.Invoke(elapsedTime / hackDuration);
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            a_onProgress?.Invoke(1f);

            CurrentState = EOutInteractableState.Interacting;

            await Awaitable.WaitForSecondsAsync(1.5f, destroyCancellationToken);

            _isJammed = false;
            StopWarningBlink();

            // Trigger the unlocked visual sequence
            _ = PlayUnlockedSequenceAsync();

            CurrentState = EOutInteractableState.Cooldown;

            if (m_unjammedDuration > 0f)
            {
                _ = RejamRoutineAsync();
            }

            if (_isPlayerInZone)
            {
                _ = PerformScanAndOpen(a_instigator);
            }
        }
        #endregion

        #region Core Logic

        private void CloseDoors()
        {
            _isPlayerInZone = false;
            _leftTargetPos = _leftClosedPos;
            _rightTargetPos = _rightClosedPos;

            if (globalSoundData != null)
            {
                AudioClip doorClip = globalSoundData.GetClip(doorOpeningSound);
                if (doorClip != null)
                {
                    if (_isLeftOpen && _leftAudioSource != null) _leftAudioSource.PlayOneShot(doorClip);
                    if (_isRightOpen && _rightAudioSource != null) _rightAudioSource.PlayOneShot(doorClip);
                }
            }

            _isLeftOpen = false;
            _isRightOpen = false;
        }

        private async Awaitable PerformScanAndOpen(Transform playerTransform)
        {
            if (_isScanning) return;
            _isScanning = true;

            if (_isJammed)
            {
                _isScanning = false;
                OutSoundManager.Instance.PlaySFX(scanFailedSound, false, transform.position);
                return;
            }

            await Awaitable.WaitForSecondsAsync(scanDelay, destroyCancellationToken);

            if (!_isPlayerInZone || _isLocked)
            {
                _isScanning = false;
                return;
            }

            if (globalSoundData != null && _maindoorAudioSource != null)
            {
                AudioClip scanClip = globalSoundData.GetClip(scanSuccessSound);
                if (scanClip != null) _maindoorAudioSource.PlayOneShot(scanClip);
            }

            while (_isPlayerInZone && !_isLocked && !_isJammed && !destroyCancellationToken.IsCancellationRequested)
            {
                bool openLeft = false;
                bool openRight = false;

                if (doorType == DoorType.HorizontalTwoPart)
                {
                    Vector3 localPlayerPos = transform.InverseTransformPoint(playerTransform.position);
                    float evaluatedX = invertSensorAxis ? -localPlayerPos.x : localPlayerPos.x;

                    if (evaluatedX < -middleZoneThreshold)
                    {
                        openLeft = true;
                        _leftTargetPos = _leftClosedPos + leftOpenOffset;
                        _rightTargetPos = _rightClosedPos;
                    }
                    else if (evaluatedX > middleZoneThreshold)
                    {
                        openRight = true;
                        _leftTargetPos = _leftClosedPos;
                        _rightTargetPos = _rightClosedPos + rightOpenOffset;
                    }
                    else
                    {
                        openLeft = true;
                        openRight = true;
                        _leftTargetPos = _leftClosedPos + leftOpenOffset;
                        _rightTargetPos = _rightClosedPos + rightOpenOffset;
                    }
                }
                else
                {
                    openLeft = true;
                    _leftTargetPos = _leftClosedPos + leftOpenOffset;
                }

                if (globalSoundData != null)
                {
                    AudioClip doorClip = globalSoundData.GetClip(doorOpeningSound);
                    if (doorClip != null)
                    {
                        if (openLeft != _isLeftOpen)
                        {
                            if (_leftAudioSource != null) _leftAudioSource.PlayOneShot(doorClip);
                            _isLeftOpen = openLeft;
                        }

                        if (openRight != _isRightOpen)
                        {
                            if (_rightAudioSource != null) _rightAudioSource.PlayOneShot(doorClip);
                            _isRightOpen = openRight;
                        }
                    }
                }

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            _isScanning = false;
        }

        private async Awaitable RejamRoutineAsync()
        {
            await Awaitable.WaitForSecondsAsync(m_unjammedDuration, destroyCancellationToken);

            _isJammed = true;

            // Ensure the unlocked sequence stops if the duration was extremely short
            StopUnlockedSequence();

            if (_isPlayerInZone)
            {
                StartWarningBlink();
                CloseDoors();
            }
        }

        private AudioSource SetupAudioSource(GameObject targetObj)
        {
            AudioSource source = targetObj.GetComponent<AudioSource>();
            if (source == null) source = targetObj.AddComponent<AudioSource>();

            source.spatialBlend = 1.0f;
            source.playOnAwake = false;
            return source;
        }

        #endregion

        #region Warning UI Logic

        private void StartWarningBlink()
        {
            if (m_lockedWarningGroup == null || _warningBlinkCts != null) return;

            _warningBlinkCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            _ = WarningBlinkRoutineAsync(_warningBlinkCts.Token);
        }

        private void StopWarningBlink()
        {
            if (_warningBlinkCts != null)
            {
                _warningBlinkCts.Cancel();
                _warningBlinkCts.Dispose();
                _warningBlinkCts = null;
            }

            if (m_lockedWarningGroup != null)
            {
                m_lockedWarningGroup.alpha = 0f;
            }
        }

        private async Awaitable WarningBlinkRoutineAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    m_lockedWarningGroup.alpha = (Mathf.Sin(Time.time * m_warningBlinkSpeed) + 1f) / 2f;
                    await Awaitable.NextFrameAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when canceled
            }
        }

        private void StopUnlockedSequence()
        {
            if (_unlockedSequenceCts != null)
            {
                _unlockedSequenceCts.Cancel();
                _unlockedSequenceCts.Dispose();
                _unlockedSequenceCts = null;
            }

            if (m_unlockedWarningGroup != null)
            {
                m_unlockedWarningGroup.alpha = 0f;
            }
        }

        private async Awaitable PlayUnlockedSequenceAsync()
        {
            if (m_unlockedWarningGroup == null) return;

            StopUnlockedSequence();
            _unlockedSequenceCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            try
            {
                // Instantly fade in
                m_unlockedWarningGroup.alpha = 1f;

                // Stay for 1 second
                await Awaitable.WaitForSecondsAsync(1.0f, _unlockedSequenceCts.Token);

                // Fade out smoothly over 0.5s
                float fadeDuration = 0.5f;
                float elapsed = 0f;

                while (elapsed < fadeDuration && !_unlockedSequenceCts.Token.IsCancellationRequested)
                {
                    elapsed += Time.deltaTime;
                    m_unlockedWarningGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    await Awaitable.NextFrameAsync(_unlockedSequenceCts.Token);
                }

                m_unlockedWarningGroup.alpha = 0f;
            }
            catch (OperationCanceledException)
            {
                // Expected when door re-jams quickly or gets disabled
            }
        }

        #endregion

        #region Animation Loops

        private async Awaitable AnimateDoorsAsync()
        {
            Vector3 leftVelocity = Vector3.zero;
            Vector3 rightVelocity = Vector3.zero;

            while (!destroyCancellationToken.IsCancellationRequested)
            {
                if (leftDoorPart != null)
                {
                    leftDoorPart.localPosition = Vector3.SmoothDamp(
                        leftDoorPart.localPosition, _leftTargetPos, ref leftVelocity, slideDuration);
                }

                if (rightDoorPart != null && doorType == DoorType.HorizontalTwoPart)
                {
                    rightDoorPart.localPosition = Vector3.SmoothDamp(
                        rightDoorPart.localPosition, _rightTargetPos, ref rightVelocity, slideDuration);
                }

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
        }

        #endregion
    }
}