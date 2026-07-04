using UnityEngine;
using DG.Tweening;

namespace OutGame
{
    /// <summary>
    /// Fades a World Space Canvas in and out based on distance to the player/camera.
    /// Utilizes DOTween for smooth transitions and hysteresis to prevent flickering.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OutProximityUI : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Proximity Settings")]
        [Tooltip("The distance threshold to trigger the Fade IN.")]
        [SerializeField] private float m_fadeInDistance = 3.0f;

        [Tooltip("The distance threshold to trigger the Fade OUT. Should be slightly larger than Fade In to prevent flickering.")]
        [SerializeField] private float m_fadeOutDistance = 4.0f;

        [Header("Animation Settings")]
        [SerializeField] private float m_fadeDuration = 0.4f;

        [Tooltip("Optional: Assign the Player transform. If left null, it defaults to the Main Camera.")]
        [SerializeField] private Transform m_targetTransform;
        #endregion

        #region Internal State
        private CanvasGroup m_canvasGroup;
        private bool m_isVisible;
        private Tween m_fadeTween;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            m_canvasGroup = GetComponent<CanvasGroup>();

            // Force invisible on start
            m_canvasGroup.alpha = 0f;
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;
            m_isVisible = false;
        }

        private void Start()
        {
            if (m_targetTransform == null && Camera.main != null)
            {
                m_targetTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (m_targetTransform == null) return;

            // Simple distance check (SqrMagnitude is faster, but Distance is perfectly fine here for UI)
            float distance = Vector3.Distance(transform.position, m_targetTransform.position);

            if (distance <= m_fadeInDistance && !m_isVisible)
            {
                ToggleVisibility(true);
            }
            else if (distance > m_fadeOutDistance && m_isVisible)
            {
                ToggleVisibility(false);
            }
        }

        private void OnDestroy()
        {
            m_fadeTween?.Kill();
        }
        #endregion

        #region Core Logic
        private void ToggleVisibility(bool a_show)
        {
            m_isVisible = a_show;
            m_fadeTween?.Kill(); // Interrupt any ongoing fade

            float targetAlpha = a_show ? 1f : 0f;

            m_fadeTween = m_canvasGroup.DOFade(targetAlpha, m_fadeDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() =>
                {
                    // Only allow UI interaction if it is fully visible (optional safety)
                    m_canvasGroup.interactable = a_show;
                });
        }
        #endregion
    }
}