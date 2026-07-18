using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutGame
{
    public enum EAimScannerMode
    {
        StandardScan,
        CommandTargeting
    }

    public class OutAimScanner : MonoBehaviour
    {
        public static OutAimScanner Instance { get; private set; }

        #region Inspector Fields
        [Header("Scanning Settings")]
        [SerializeField] private float m_scanRange = 25f;
        [Tooltip("The layer containing both interactable and non-interactable scannable props.")]
        [SerializeField] private LayerMask m_scannableLayer;

        [Header("Targeting Mode Settings")]
        [Tooltip("The layer for ground/environment when commanding AI to move.")]
        [SerializeField] private LayerMask m_targetingLayerMask;

        [Space]
        [SerializeField] private Transform m_cameraTransform;
        public Transform targetMarker;

        [Header("Highlight Settings")]
        [ColorUsage(true, true)][SerializeField] private Color m_focusedColor = Color.cyan;
        [ColorUsage(true, true)][SerializeField] private Color m_dormantColor = Color.grey;

        [Header("Debug")]
        [SerializeField] private bool m_showDebugGizmo = false;
        #endregion

        #region Events
        public static event Action<bool> OnAimStateChanged;
        public static event Action<EOutInteractableState> OnTargetStateChanged;
        public static event Action<float> OnInteractionProgressChanged;
        #endregion

        #region Private Variables
        private EAimScannerMode m_currentMode = EAimScannerMode.StandardScan;
        private OutAfflictedAI m_aiPendingCommand;

        private IOutInteractable m_currentHoveredObject;
        private bool m_isAiming;

        // Highlighting
        [Header("Highlight Settings")]
        [Tooltip("A single global material utilizing an Outline/Inverted Hull shader.")]
        [SerializeField] private Material m_globalOutlineMaterial;
        private List<Renderer> m_currentRenderers;
        private MaterialPropertyBlock m_propBlock;
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        #endregion

        #region Unity Messages
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            m_propBlock = new MaterialPropertyBlock();
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            OutLocomotionManager.OnAimStateChanged += ToggleAimState;
        }

        private void OnDisable()
        {
            OutLocomotionManager.OnAimStateChanged -= ToggleAimState;
            ClearCurrentTarget();
        }

        private void Update()
        {
            if (m_currentMode == EAimScannerMode.StandardScan)
            {
                // Only run standard interactions if we are actively aiming
                if (!m_isAiming) return;

                PerformStandardScan();

                if (OutInputManager.Instance.InputActions.Player.Interact.WasPressedThisFrame())
                {
                    _ = TriggerInteractionAsync(m_cameraTransform);
                }
            }
            else if (m_currentMode == EAimScannerMode.CommandTargeting)
            {
                // We are in Flee Command Mode.
                if (m_isAiming)
                {
                    // Show the marker ONLY when holding aim
                    if (targetMarker != null && !targetMarker.gameObject.activeSelf)
                        targetMarker.gameObject.SetActive(true);

                    PerformCommandTargeting();
                }
                else
                {
                    // Player let go of aim. Hide the marker, but DO NOT cancel the command.
                    if (targetMarker != null && targetMarker.gameObject.activeSelf)
                        targetMarker.gameObject.SetActive(false);
                }
            }
        }
        #endregion

        #region Command Targeting Logic
        /// <summary>
        /// Called by the AI's UI Canvas when the player clicks "Flee".
        /// Switches the scanner into targeting mode for this specific AI.
        /// </summary>
        public void EnterCommandTargetingMode(OutAfflictedAI a_afflicted)
        {
            m_currentMode = EAimScannerMode.CommandTargeting;

            OutLogger.Note("Entering command targeting mode.");
            m_aiPendingCommand = a_afflicted;

            ClearCurrentTarget(); // Stop highlighting props

            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
            else OutLogger.Error("Target marker is null. Can't activate it.");

            if (OutUIManager.Instance != null)
            {
                // Note: GetBindingDisplayString logic can be moved to a static helper class later
                OutUIManager.Instance.ShowGameplayHint($"Aim at a location and press Attack to order them to move.");
            }
        }

        private void PerformCommandTargeting()
        {
            Ray ray = new Ray(m_cameraTransform.position, m_cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, 50f, m_targetingLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (targetMarker != null)
                {
                    targetMarker.position = hitInfo.point;
                }
                else OutLogger.Error("Target marker is null. Can't activate it.");

                if (OutInputManager.Instance.InputActions.Player.Attack.WasPressedThisFrame())
                {
                    ExecuteCommand(hitInfo.point);
                }
            }
        }

        private void ExecuteCommand(Vector3 a_destination)
        {
            if (m_aiPendingCommand != null)
            {
                OutLogger.Note("Executing command.");
                m_aiPendingCommand.ExecuteFleeMove(a_destination);
            }
            else OutLogger.Error("No pending command to execute and you tried to execute one.");

            // Reset back to standard scanning
            m_currentMode = EAimScannerMode.StandardScan;
            OutLogger.Note("Exiting command targeting mode.");
            m_aiPendingCommand = null;

            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
            if (OutUIManager.Instance != null) OutUIManager.Instance.HideGameplayHint();
        }
        #endregion

        #region Public Methods
        public void ToggleAimState(bool a_isAiming)
        {
            m_isAiming = a_isAiming;
            OnAimStateChanged?.Invoke(m_isAiming);

            if (!m_isAiming)
            {
                ClearCurrentTarget();

                // Optional: If they stop aiming while targeting, cancel the command
                //if (m_currentMode == EAimScannerMode.CommandTargeting)
                //{
                //    m_currentMode = EAimScannerMode.StandardScan;
                //    if (targetMarker != null) targetMarker.gameObject.SetActive(false);
                //    if (OutUIManager.Instance != null) OutUIManager.Instance.HideGameplayHint();
                //}
            }
        }

        public async Awaitable TriggerInteractionAsync(Transform a_playerTransform)
        {
            if (m_currentHoveredObject != null && m_currentHoveredObject.CurrentState != EOutInteractableState.Interacting)
            {
                await m_currentHoveredObject.ExecuteInteractionAsync(a_playerTransform, OnInteractionProgressChanged);
            }
        }
        #endregion

        #region Standard Scanning Methods
        private void PerformStandardScan()
        {
            Ray ray = new Ray(m_cameraTransform.position, m_cameraTransform.forward);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, m_scanRange, m_scannableLayer, QueryTriggerInteraction.Ignore))
            {
                if (m_showDebugGizmo) Debug.DrawLine(ray.origin, hitInfo.point, Color.green);

                IOutInteractable interactable = hitInfo.collider.GetComponentInParent<IOutInteractable>();
                Renderer singleRenderer = hitInfo.collider.GetComponent<Renderer>();

                if (interactable != null && m_currentHoveredObject == interactable) return;

                if (interactable == null && m_currentHoveredObject == null &&
                    m_currentRenderers != null && m_currentRenderers.Count == 1 && m_currentRenderers[0] == singleRenderer)
                {
                    return;
                }

                ClearCurrentTarget();
                m_currentHoveredObject = interactable;

                if (interactable != null)
                {
                    if (interactable.RendererObjects != null && interactable.RendererObjects.Count > 0)
                    {
                        m_currentRenderers = interactable.RendererObjects;
                    }
                    else if (singleRenderer != null)
                    {
                        m_currentRenderers = new List<Renderer> { singleRenderer };
                    }

                    interactable.OnAimEnter();
                    OnTargetStateChanged?.Invoke(interactable.CurrentState);
                    ApplyHighlight(m_focusedColor);
                }
                else if (singleRenderer != null)
                {
                    m_currentRenderers = new List<Renderer> { singleRenderer };
                    ApplyHighlight(m_dormantColor);
                }
                return;
            }

            if (m_showDebugGizmo) Debug.DrawLine(ray.origin, ray.origin + (ray.direction * m_scanRange), Color.red);
            ClearCurrentTarget();
        }

        private void ClearCurrentTarget()
        {
            if (m_currentHoveredObject != null)
            {
                m_currentHoveredObject.OnAimExit();
                m_currentHoveredObject = null;
                OnTargetStateChanged?.Invoke(EOutInteractableState.Dormant);
            }

            if (m_currentRenderers != null)
            {
                ClearHighlight();
                m_currentRenderers = null;
            }
        }

        private void ApplyHighlight(Color a_color)
        {
            if (m_currentRenderers == null || m_globalOutlineMaterial == null) return;

            for (int i = 0; i < m_currentRenderers.Count; i++)
            {
                Renderer r = m_currentRenderers[i];
                if (r == null) continue;

                Material[] currentMats = r.sharedMaterials;

                // 1. Inject the outline material if it isn't already there
                if (currentMats.Length == 0 || currentMats[currentMats.Length - 1] != m_globalOutlineMaterial)
                {
                    Material[] newMats = new Material[currentMats.Length + 1];
                    Array.Copy(currentMats, newMats, currentMats.Length);
                    newMats[newMats.Length - 1] = m_globalOutlineMaterial;
                    r.sharedMaterials = newMats;
                }

                // 2. Push the specific color to the property block
                r.GetPropertyBlock(m_propBlock);
                m_propBlock.SetColor(OutlineColorId, a_color);
                r.SetPropertyBlock(m_propBlock);
            }
        }

        private void ClearHighlight()
        {
            if (m_currentRenderers == null || m_globalOutlineMaterial == null) return;

            for (int i = 0; i < m_currentRenderers.Count; ++i)
            {
                Renderer r = m_currentRenderers[i];
                if (r == null) continue;

                Material[] currentMats = r.sharedMaterials;

                // 1. Strip the outline material off the end of the array
                if (currentMats.Length > 0 && currentMats[currentMats.Length - 1] == m_globalOutlineMaterial)
                {
                    Material[] newMats = new Material[currentMats.Length - 1];
                    Array.Copy(currentMats, newMats, currentMats.Length - 1);
                    r.sharedMaterials = newMats;
                }

                // 2. Clear the property block to avoid memory leaks or lingering colors
                if (r.HasPropertyBlock())
                {
                    r.GetPropertyBlock(m_propBlock);
                    m_propBlock.Clear();
                    r.SetPropertyBlock(m_propBlock);
                }
            }
        }
        #endregion
    }
}