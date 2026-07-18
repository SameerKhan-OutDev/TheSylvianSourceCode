using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace OutGame
{
    /// <summary>
    /// Handles the presentation, sequenced animation, and interaction of the Sylvian Failed screen.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OutFailurePanel : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Main Panel")]
        [SerializeField] private CanvasGroup m_mainCanvasGroup;
        [SerializeField] private float m_bgFadeDuration = 0.5f;

        [Header("Title Container")]
        [SerializeField] private CanvasGroup m_titleContainerCG;
        [SerializeField] private RectTransform m_titleContainerRect;
        [SerializeField] private Vector2 m_titleStartPosition;
        [SerializeField] private Vector2 m_titleTargetPosition;
        [SerializeField] private float m_titleAnimDuration = 0.5f;

        [Header("Reason Text")]
        [SerializeField] private TMP_Text m_failureReasonText;
        [SerializeField] private float m_reasonFadeDuration = 0.5f;

        [Header("Buttons Container")]
        [SerializeField] private RectTransform m_buttonsContainerRect;
        [SerializeField] private Vector2 m_buttonsStartPosition;
        [SerializeField] private Vector2 m_buttonsTargetPosition;
        [SerializeField] private float m_buttonsSlideDuration = 0.5f;

        [Header("Controls")]
        [SerializeField] private Button m_restartButton;
        [SerializeField] private Button m_restartCheckpointButton;
        [SerializeField] private Button m_quitMenuButton;
        #endregion

        #region Internal State
        private Sequence m_animationSequence;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (m_mainCanvasGroup == null) m_mainCanvasGroup = GetComponent<CanvasGroup>();

            ResetUIStates();
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_restartButton.onClick.AddListener(OnRestartClicked);
            if (m_restartCheckpointButton != null) m_restartCheckpointButton.onClick.AddListener(OnRestartCheckpointClicked);
            m_quitMenuButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnDisable()
        {
            m_restartButton.onClick.RemoveAllListeners();
            if (m_restartCheckpointButton != null) m_restartCheckpointButton.onClick.RemoveAllListeners();
            m_quitMenuButton.onClick.RemoveAllListeners();

            m_animationSequence?.Kill();
            m_mainCanvasGroup.DOKill();
        }
        #endregion

        #region Core Logic
        public async void ShowPanel(string reason)
        {
            gameObject.SetActive(true);
            ResetUIStates();

            if (m_failureReasonText != null)
            {
                m_failureReasonText.text = string.IsNullOrEmpty(reason) ? "UNKNOWN FATAL ERROR" : reason.ToUpper();
            }

            if (OutSoundManager.Instance != null)
            {
                OutSoundManager.Instance.PlayUISound(SoundType.MissionFailed, true);
            }

            await AnimateInAsync();
        }

        /// <summary>
        /// Resets all UI elements to their invisible/starting positions before the animation begins.
        /// </summary>
        private void ResetUIStates()
        {
            m_mainCanvasGroup.alpha = 0f;
            m_mainCanvasGroup.interactable = false;
            m_mainCanvasGroup.blocksRaycasts = false;

            if (m_titleContainerCG != null) m_titleContainerCG.alpha = 0f;
            if (m_titleContainerRect != null) m_titleContainerRect.anchoredPosition = m_titleStartPosition;

            if (m_failureReasonText != null)
            {
                Color c = m_failureReasonText.color;
                c.a = 0f;
                m_failureReasonText.color = c;
            }

            if (m_buttonsContainerRect != null) m_buttonsContainerRect.anchoredPosition = m_buttonsStartPosition;
        }

        private async Awaitable AnimateInAsync()
        {
            m_mainCanvasGroup.blocksRaycasts = true;

            // 1. Fade in the dark background immediately
            m_mainCanvasGroup.DOFade(1f, m_bgFadeDuration).SetUpdate(true);

            // Create the Sequence and force it to use unscaled time
            m_animationSequence = DOTween.Sequence().SetUpdate(true);

            // Step 1: Title Container fades in
            m_animationSequence.Append(m_titleContainerCG.DOFade(1f, m_titleAnimDuration));

            // Step 2: Wait 1 second
            m_animationSequence.AppendInterval(1f);

            // Step 3: Title Container moves to assigned position
            m_animationSequence.Append(m_titleContainerRect.DOAnchorPos(m_titleTargetPosition, m_titleAnimDuration).SetEase(Ease.OutCubic));

            // Step 4: Reason text fades in AFTER title container finishes moving
            m_animationSequence.Append(m_failureReasonText.DOFade(1f, m_reasonFadeDuration));

            // Step 5: Wait another 1 second
            m_animationSequence.AppendInterval(1f);

            // Step 6: Buttons slide from position A to position B
            m_animationSequence.Append(m_buttonsContainerRect.DOAnchorPos(m_buttonsTargetPosition, m_buttonsSlideDuration).SetEase(Ease.OutExpo));

            // Wait for the entire sequence to finish
            await m_animationSequence.AsyncWaitForCompletion();

            // Allow user interaction only after animation is completely done
            m_mainCanvasGroup.interactable = true;
        }
        #endregion

        #region Callbacks
        private void OnRestartClicked()
        {
            // Restarts the entire mission from scratch
            if (OutSoundManager.Instance != null) OutSoundManager.Instance.PlayOnClickSound();

            if (OutGameManager.Instance != null)
            {
                gameObject.SetActive(false);
                OutGameManager.Instance.IsNewGameSession = true; // Forcing normal mission restart logic
                OutGameManager.Instance.RestartCurrentLevel();
            }
        }

        private void OnRestartCheckpointClicked()
        {
            // Reloads the exact scene but forces a save state integration
            if (OutSoundManager.Instance != null) OutSoundManager.Instance.PlayOnClickSound();

            if (OutGameManager.Instance != null)
            {
                gameObject.SetActive(false);
                OutGameManager.Instance.ContinueGame(true);
            }
        }

        private void OnQuitClicked()
        {
            if (OutSoundManager.Instance != null) OutSoundManager.Instance.PlayOnClickSound();

            if (OutGameManager.Instance != null)
            {
                gameObject.SetActive(false);
                OutGameManager.Instance.ChangeState(OutGameState.MainMenu);
                OutGameManager.Instance.StartLoadingScene(OutStringConstants.Scenes.MainMenu);
            }
        }
        #endregion
    }
}