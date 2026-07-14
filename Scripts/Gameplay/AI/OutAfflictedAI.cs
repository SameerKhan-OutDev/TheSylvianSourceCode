using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OutGame
{
    public enum EAfflictedBaseDemeanor
    {
        Calm,
        Confused,
        Aggressive
    }

    public enum EAfflictedCurrentState
    {
        Default,
        Commanded_Stop,
        Commanded_TargetingFlee, // NEW: Waiting for player to select a location
        Commanded_Flee,
        Commanded_Engage,
        Dead
    }

    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class OutAfflictedAI : MonoBehaviour, IOutInteractable, IOutConductor // ADDED IOutConductor
    {
        #region Inspector Fields
        [Header("Afflicted Configuration")]
        [SerializeField] private EAfflictedBaseDemeanor _baseDemeanor = EAfflictedBaseDemeanor.Confused;

        private Rigidbody[] _boneRigidbodies;

        [Header("Movement & Speeds")]
        [SerializeField] private float _walkSpeed = 1.5f;
        [SerializeField] private float _sprintSpeed = 4.5f;

        [Header("Animation Settings")]
        [SerializeField] private float _animationBlendSpeed = 10f;
        [SerializeField] private float _leanSensitivity = 0.05f;

        [Header("Command UI (World Space)")]
        [SerializeField] private GameObject _commandCanvas;

        [Header("Command Buttons")]
        [SerializeField] private Button _btnStop;
        [SerializeField] private Button _btnFlee;
        [SerializeField] private Button _btnEngage;
        [SerializeField] private Button _btnDisorient;

        [Header("Base Action Names")]
        [SerializeField] private string _nameStop = "Stop";
        [SerializeField] private string _nameFlee = "Flee";
        [SerializeField] private string _nameEngage = "Engage";
        [SerializeField] private string _nameDisorient = "Disorient";

        [Header("Disorient Damage")]
        [SerializeField] private float _disorientDamageRadius = 4f;
        [SerializeField] private float _disorientDamagePercentage = 20f;

        [Header("Events")]
        public UnityEvent onDie;
        #endregion

        #region Internal State
        private NavMeshAgent _agent;
        private Animator _animator;

        private EAfflictedCurrentState _currentState = EAfflictedCurrentState.Default;
        private Transform _engageTarget;
        private Vector3 _selectedFleePosition;

        private readonly int _animHashX = Animator.StringToHash("X");
        private readonly int _animHashY = Animator.StringToHash("Y");
        private readonly int _animHashIdle = Animator.StringToHash("Idle");
        private readonly int _animHashIsWalking = Animator.StringToHash("IsWalking");
        private readonly int _animHashIsSprinting = Animator.StringToHash("IsSprinting");
        private readonly int _animHashDie = Animator.StringToHash("Die");

        private float _currentAnimX;
        private float _currentAnimY;
        private float _lastRotationY;
        #endregion

        #region IOutInteractable Properties
        public EOutInteractableState CurrentState { get; private set; } = EOutInteractableState.Dormant;

        public List<Renderer> RendererObjects => rendererObjects;
        [SerializeField] List<Renderer> rendererObjects;
        #endregion

        #region private variables
        [HideInInspector]
        public OutAimScanner aimScanner;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();

            InitializeRagdoll();

            ApplyDemeanorSettings();
            SetupCommandUI();
        }

        private void Start()
        {
            aimScanner = FindAnyObjectByType<OutAimScanner>();
        }

        private void Update()
        {
            if (_currentState == EAfflictedCurrentState.Dead) return;

            ProcessAIBehavior();
            UpdateAnimator();

            if (_commandCanvas != null && _commandCanvas.activeSelf)
            {
                ListenForCommandHotkeys();
            }
        }
        #endregion

        #region IOutInteractable Implementation
        public void OnAimEnter()
        {
            if (CurrentState == EOutInteractableState.Dormant && _currentState != EAfflictedCurrentState.Dead)
            {
                CurrentState = EOutInteractableState.Hovered;
                if (rendererObjects != null && rendererObjects.Count > 0)
                    for (int i = 0; i < rendererObjects.Count; i++) rendererObjects[i].material.EnableKeyword("_EMISSION");
            }
        }

        public void OnAimExit()
        {
            if (CurrentState == EOutInteractableState.Hovered || CurrentState == EOutInteractableState.Interacting)
            {
                CurrentState = EOutInteractableState.Dormant;
                if (rendererObjects != null && rendererObjects.Count > 0)
                    for (int i = 0; i < rendererObjects.Count; i++) rendererObjects[i].material.DisableKeyword("_EMISSION");
            }
        }

        public async Awaitable ExecuteInteractionAsync(Transform a_instigator, Action<float> a_onProgress = null)
        {
            if (CurrentState == EOutInteractableState.Interacting || _currentState == EAfflictedCurrentState.Dead) return;

            CurrentState = EOutInteractableState.Interacting;

            float hackDuration = 1.0f;
            float elapsedTime = 0f;

            while (elapsedTime < hackDuration)
            {
                if (!OutInputManager.Instance.InputActions.Player.Interact.IsPressed() || CurrentState == EOutInteractableState.Dormant)
                {
                    a_onProgress?.Invoke(0f);
                    if (CurrentState != EOutInteractableState.Dormant) CurrentState = EOutInteractableState.Hovered;
                    return;
                }

                elapsedTime += Time.deltaTime;
                a_onProgress?.Invoke(elapsedTime / hackDuration);
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            a_onProgress?.Invoke(1f);

            _agent.isStopped = true;
            CurrentState = EOutInteractableState.Cooldown;

            if (_commandCanvasGroup != null)
            {
                _commandCanvasGroup.DOFade(1f, 0.4f);
                _commandCanvasGroup.interactable = true;
                _commandCanvasGroup.blocksRaycasts = true;
            }
        }
        #endregion

        #region IOutConductor Implementation (The Spark Trap)
        public void OnElectrocuted(Transform attachPoint)
        {
            OutLogger.Note("Electrocuted");
            // The AI walked into the trap
            _currentState = EAfflictedCurrentState.Dead;

            // Stop movement immediately
            _agent.isStopped = true;
            _agent.enabled = false;

            // Snap them to the spark source point
            transform.position = attachPoint.position;
            transform.rotation = attachPoint.rotation;

            // Trigger death/disorient animation
            _animator.SetTrigger(_animHashDie);

            OutSoundManager.Instance?.PlaySFX(SoundType.ManDie, true, transform.position);

            if (TryGetComponent(out Collider col)) col.enabled = false;

            // Fire and forget the dynamic disable routine
            _ = DisableAfterDeathAnimationAsync();
        }
        #endregion

        #region Root Node Commands

        public void CommandStop()
        {
            HideCommandUI();
            _currentState = EAfflictedCurrentState.Commanded_Stop;
            _agent.isStopped = true;
        }

        public void CommandFlee()
        {
            HideCommandUI();

            // Tell the Aim Scanner that THIS specific AI is waiting for a coordinate
            if (aimScanner != null)
            {
                aimScanner.EnterCommandTargetingMode(this);
            }
            else Debug.LogError("Aim scanner not found!");
        }

        public void CommandEngage(Transform a_target)
        {
            HideCommandUI();
            _currentState = EAfflictedCurrentState.Commanded_Engage;
            _engageTarget = a_target;
            _agent.isStopped = false;
            _agent.speed = _sprintSpeed;
        }

        public async void CommandDisorient()
        {
            HideCommandUI();
            _currentState = EAfflictedCurrentState.Dead;
            _agent.isStopped = true;

            _animator.SetTrigger(_animHashDie);
            OutSoundManager.Instance?.PlaySFX(SoundType.ShortCircuit, true, transform.position);
            OutSoundManager.Instance?.PlaySFX(SoundType.ManDie, true, transform.position);

            if (TryGetComponent(out Collider col)) col.enabled = false;

            // ---> NEW DAMAGE LOGIC <---
            // Create a blast radius check
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, _disorientDamageRadius);
            foreach (var hit in hitColliders)
            {
                // Check if the object hit has the player health component
                if (hit.TryGetComponent(out IDamagable playerHealth))
                {
                    playerHealth.TakeDamagePercentage(_disorientDamagePercentage);
                }
            }

            _ = DisableAfterDeathAnimationAsync();
        }

        #endregion

        #region Core AI & Animation Logic

        private void InitializeRagdoll()
        {
            // Grab all rigidbodies in the children (the bones generated by the Ragdoll Wizard)
            _boneRigidbodies = GetComponentsInChildren<Rigidbody>();

            foreach (Rigidbody rb in _boneRigidbodies)
            {
                // Skip the main root rigidbody if you happen to have one on the parent object
                if (rb.gameObject == this.gameObject) continue;

                // Force the bones to follow the animation without calculating physics yet
                rb.isKinematic = true;
            }
        }
        private void EnableRagdoll()
        {
            // Turn off the animator so it stops forcing bone positions
            _animator.enabled = false;

            foreach (Rigidbody rb in _boneRigidbodies)
            {
                if (rb.gameObject == this.gameObject) continue;

                // Give control back to the physics engine
                rb.isKinematic = false;

                // Optional: Apply a tiny bit of random torque so they slump naturally instead of stiffly
                rb.AddTorque(new Vector3(
                    UnityEngine.Random.Range(-2f, 2f),
                    UnityEngine.Random.Range(-2f, 2f),
                    UnityEngine.Random.Range(-2f, 2f)
                ), ForceMode.Impulse);
            }
        }

        private void ProcessAIBehavior()
        {
            switch (_currentState)
            {
                case EAfflictedCurrentState.Commanded_Flee:
                    // Handled automatically by NavMeshAgent
                    break;

                case EAfflictedCurrentState.Commanded_Engage:
                    if (_engageTarget != null)
                    {
                        _agent.SetDestination(_engageTarget.position);
                    }
                    break;
            }
        }

        /// <summary>
        /// Called directly by OutAimScanner once the player hits the Attack button.
        /// </summary>
        public void ExecuteFleeMove(Vector3 destination)
        {
            _currentState = EAfflictedCurrentState.Commanded_Flee;

            _agent.isStopped = false;
            _agent.speed = _sprintSpeed;
            _agent.SetDestination(destination);
        }

        private void UpdateAnimator()
        {
            float speedRatio = _agent.velocity.magnitude / _sprintSpeed;
            float currentMaxSpeed = _agent.speed;

            float targetY = _agent.velocity.magnitude > 0.1f ? (_agent.velocity.magnitude / currentMaxSpeed) * (currentMaxSpeed == _sprintSpeed ? 1f : 0.5f) : 0f;

            float currentRotationY = transform.eulerAngles.y;
            float rotationDelta = Mathf.DeltaAngle(_lastRotationY, currentRotationY);
            _lastRotationY = currentRotationY;

            float targetX = 0f;
            if (speedRatio > 0.01f)
            {
                float angularVelocity = rotationDelta / Time.deltaTime;
                targetX = Mathf.Clamp(angularVelocity * _leanSensitivity, -1f, 1f);
            }

            _currentAnimX = Mathf.Lerp(_currentAnimX, targetX, Time.deltaTime * _animationBlendSpeed);
            _currentAnimY = Mathf.Lerp(_currentAnimY, targetY, Time.deltaTime * _animationBlendSpeed);

            _animator.SetFloat(_animHashX, _currentAnimX);
            _animator.SetFloat(_animHashY, _currentAnimY);

            bool isMoving = _agent.velocity.magnitude > 0.1f;
            _animator.SetBool(_animHashIdle, !isMoving);
            _animator.SetBool(_animHashIsWalking, isMoving && _agent.speed <= _walkSpeed);
            _animator.SetBool(_animHashIsSprinting, isMoving && _agent.speed > _walkSpeed);
        }

        private void ApplyDemeanorSettings()
        {
            switch (_baseDemeanor)
            {
                case EAfflictedBaseDemeanor.Calm:
                    _agent.speed = _walkSpeed * 0.7f;
                    break;
                case EAfflictedBaseDemeanor.Confused:
                    _agent.speed = _walkSpeed;
                    break;
                case EAfflictedBaseDemeanor.Aggressive:
                    _agent.speed = _sprintSpeed * 0.8f;
                    break;
            }
        }
        #endregion

        #region UI Setup
        private CanvasGroup _commandCanvasGroup;

        private string GetBindingDisplayString(InputAction inputAction)
        {
            if (inputAction == null) return "?";
            System.Collections.Generic.List<string> displayParts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < inputAction.bindings.Count; i++)
            {
                if (inputAction.bindings[i].isComposite) continue;
                string part = inputAction.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                if (!string.IsNullOrEmpty(part) && !displayParts.Contains(part)) displayParts.Add(part);
            }
            return displayParts.Count > 0 ? string.Join(" / ", displayParts) : "?";
        }

        private void SetButtonLabel(Button btn, string actionName, InputAction inputAction)
        {
            if (btn == null) return;
            string bindingText = GetBindingDisplayString(inputAction);
            string finalText = $"{actionName} [{bindingText}]";

            if (btn.TryGetComponent(out TMP_Text txt)) txt.text = finalText;
            else if (btn.GetComponentInChildren<TMP_Text>() != null) btn.GetComponentInChildren<TMP_Text>().text = finalText;
        }

        private void SetupCommandUI()
        {
            if (_commandCanvas != null)
            {
                _commandCanvasGroup = _commandCanvas.GetComponent<CanvasGroup>();
                if (_commandCanvasGroup != null)
                {
                    _commandCanvasGroup.alpha = 0f;
                    _commandCanvasGroup.interactable = false;
                    _commandCanvasGroup.blocksRaycasts = false;
                }
            }

            var inputActions = OutInputManager.Instance.InputActions.Player;

            if (_btnStop != null)
            {
                _btnStop.onClick.AddListener(CommandStop);
                SetButtonLabel(_btnStop, _nameStop, inputActions.InteractV1);
            }

            if (_btnFlee != null)
            {
                _btnFlee.onClick.AddListener(CommandFlee); // Signature changed
                SetButtonLabel(_btnFlee, _nameFlee, inputActions.InteractV2);
            }

            if (_btnEngage != null)
            {
                _btnEngage.onClick.AddListener(() => CommandEngage(null));
                SetButtonLabel(_btnEngage, _nameEngage, inputActions.InteractV3);
            }

            if (_btnDisorient != null)
            {
                _btnDisorient.onClick.AddListener(CommandDisorient);
                SetButtonLabel(_btnDisorient, _nameDisorient, inputActions.InteractV4);
            }
        }

        private void HideCommandUI()
        {
            if (_commandCanvasGroup != null)
            {
                _commandCanvasGroup.interactable = false;
                _commandCanvasGroup.blocksRaycasts = false;
                _commandCanvasGroup.DOFade(0f, 0.3f);
            }
        }

        private void ListenForCommandHotkeys()
        {
            var inputActions = OutInputManager.Instance.InputActions.Player;

            if (inputActions.InteractV1.WasPressedThisFrame())
            {
                _btnStop?.onClick.Invoke();
            }
            else if (inputActions.InteractV2.WasPressedThisFrame())
            {
                _btnFlee?.onClick.Invoke();
            }
            else if (inputActions.InteractV3.WasPressedThisFrame())
            {
                _btnEngage?.onClick.Invoke();
            }
            else if (inputActions.InteractV4.WasPressedThisFrame())
            {
                _btnDisorient?.onClick.Invoke();
            }
        }

        #endregion

        #region Helpers
        private async Awaitable DisableAfterDeathAnimationAsync()
        {
            await Awaitable.NextFrameAsync(destroyCancellationToken);

            while (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Die"))
            {
                if (destroyCancellationToken.IsCancellationRequested) return;
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            while (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
            {
                if (destroyCancellationToken.IsCancellationRequested) return;
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            onDie?.Invoke();

            // NEW: Safely hand over to the physics engine
            EnableRagdoll();



            // Disable the core AI components
            _agent.enabled = false;
            this.enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _disorientDamageRadius);
        }
        #endregion
    }
}