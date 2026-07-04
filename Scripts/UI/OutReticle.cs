using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

namespace OutGame
{
    /// <summary>
    /// Manages the three states of the player reticle via centralized events.
    /// </summary>
    public class OutReticle : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Reticle Components")]
        [Tooltip("The constant dot/crosshair while aiming.")]
        [SerializeField] private GameObject m_reticleEmpty;

        [Tooltip("Image set to 'Filled' (e.g., Radial 360) for hacking progress.")]
        [SerializeField] private Image m_reticleFill;

        [Tooltip("CanvasGroup required to fade the focus graphics using DOTween.")]
        [SerializeField] private CanvasGroup m_reticleFocusGroup;

        [Header("Animation Settings")]
        [SerializeField] private float m_focusFadeDuration = 0.5f;
        #endregion

        #region Internal State
        private Tween m_focusTween;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Set default hidden states
            m_reticleEmpty.SetActive(false);
            m_reticleFill.fillAmount = 0f;
            m_reticleFill.gameObject.SetActive(false);

            m_reticleFocusGroup.alpha = 0f;
            m_reticleFocusGroup.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Subscribe to our centralized state events
            OutLocomotionManager.OnAimStateChanged += HandleAimStateChanged;
            OutAimScanner.OnTargetStateChanged += HandleTargetStateChanged;
            OutAimScanner.OnInteractionProgressChanged += HandleHackingProgress;
        }

        private void OnDisable()
        {
            // Prevent memory leaks
            OutLocomotionManager.OnAimStateChanged -= HandleAimStateChanged;
            OutAimScanner.OnTargetStateChanged -= HandleTargetStateChanged;
            OutAimScanner.OnInteractionProgressChanged -= HandleHackingProgress;

            m_focusTween?.Kill();
        }
        #endregion

        #region Event Handlers
        private void HandleAimStateChanged(bool a_isAiming)
        {
            m_reticleEmpty.SetActive(a_isAiming);

            // If player drops aim, force everything else off immediately
            if (!a_isAiming)
            {
                HandleTargetStateChanged(EOutInteractableState.Dormant);
                HandleHackingProgress(0f);
            }
        }

        private void HandleTargetStateChanged(EOutInteractableState a_state)
        {
            m_focusTween?.Kill(); // Interrupt any ongoing fades

            bool shouldFocus = (a_state == EOutInteractableState.Hovered || a_state == EOutInteractableState.Interacting);

            if (shouldFocus)
            {
                m_reticleFocusGroup.gameObject.SetActive(true);
                m_focusTween = m_reticleFocusGroup.DOFade(1f, m_focusFadeDuration);
            }
            else
            {
                m_focusTween = m_reticleFocusGroup.DOFade(0f, m_focusFadeDuration).OnComplete(() =>
                {
                    m_reticleFocusGroup.gameObject.SetActive(false);
                });
            }
        }

        private void HandleHackingProgress(float a_progress)
        {
            if (a_progress > 0f && a_progress < 1f)
            {
                if (!m_reticleFill.gameObject.activeSelf) m_reticleFill.gameObject.SetActive(true);
                m_reticleFill.fillAmount = a_progress;
            }
            else
            {
                // Reset and hide when hacking is 0% or 100% complete
                m_reticleFill.fillAmount = 0f;
                m_reticleFill.gameObject.SetActive(false);
            }
        }
        #endregion
    }
}