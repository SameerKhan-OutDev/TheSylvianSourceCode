using UnityEngine;
using DG.Tweening;

namespace OutGame
{
    /// <summary>
    /// Handles the automated activation, time-scaling, and visual fading of trigger-based panels.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class TutorialPanel : MonoBehaviour
    {
        #region Fields

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CanvasGroup canvasGroup;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            Time.timeScale = 0f;

            canvasGroup.alpha = 0f;
            // .SetUpdate(true) is required so DOTween ignores the 0 timescale and still animates
            canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;

            canvasGroup.DOKill();
        }

        #endregion
    }
}