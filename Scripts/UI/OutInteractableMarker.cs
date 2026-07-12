using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Manages a world-space UI marker for interactables, handling distance-based alpha fading.
    /// </summary>
    public class OutInteractableMarker : MonoBehaviour
    {
        #region Inspector Fields
        [Header("References")]
        [SerializeField] private CanvasGroup m_canvasGroup;
        [SerializeField] private Canvas m_canvas;

        [Header("Fading Settings")]
        [Tooltip("Distance at which the marker is fully visible.")]
        [SerializeField] private float m_fullAlphaDistance = 10f;
        [Tooltip("Distance at which the marker becomes completely invisible.")]
        [SerializeField] private float m_zeroAlphaDistance = 25f;
        #endregion

        #region Private Variables
        [SerializeField] private Transform m_cameraTransform;
        #endregion

        #region Unity Messages
        private void Start()
        {

            if (m_canvasGroup == null)
            {
                m_canvasGroup = GetComponent<CanvasGroup>();
                if (m_canvasGroup == null)
                {
                    this.Alert("OutInteractableMarker requires a CanvasGroup component.");
                }
            }

            if (m_canvas == null)
            {
                m_canvas = GetComponent<Canvas>();

                if (m_canvas) m_canvas.worldCamera = m_cameraTransform.GetComponent<Camera>();
                if (m_canvas == null)
                {
                    this.Alert("OutInteractableMarker requires a Canvas component.");
                }
            }
        }

        private void Update()
        {
            if (m_cameraTransform == null)
            {
                m_cameraTransform = Camera.main.transform;
                if (m_cameraTransform == null) m_cameraTransform = Camera.current.transform;
                if (m_cameraTransform == null) m_cameraTransform = GameObject.FindWithTag("MainCamera").transform;
            }

            if (m_canvasGroup == null) return;

            ProcessDistanceFade();
        }
        #endregion

        #region Private Methods
        private void ProcessDistanceFade()
        {
            float distance = Vector3.Distance(transform.position, m_cameraTransform.position);

            if (distance <= m_fullAlphaDistance)
            {
                m_canvasGroup.alpha = 1f;
            }
            else if (distance >= m_zeroAlphaDistance)
            {
                m_canvasGroup.alpha = 0f;
            }
            else
            {
                // Remap distance to alpha (1 to 0)
                float range = m_zeroAlphaDistance - m_fullAlphaDistance;
                float currentOffset = distance - m_fullAlphaDistance;
                m_canvasGroup.alpha = 1f - (currentOffset / range);
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, m_fullAlphaDistance);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, m_zeroAlphaDistance);
        }
        #endregion
    }
}