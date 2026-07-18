using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

namespace OutGame
{
    /// <summary>
    /// Handles the presentation, sequenced animation, and interaction of the Pause screen.
    /// Freezes time, manages audio muting, and handles state restoration upon resuming.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OutPausePanel : MonoBehaviour
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

        [Header("Text Elements")]
        [SerializeField] private TMP_Text m_pauseTitleText;
        [SerializeField] private TMP_Text m_currentObjectiveText;
        [SerializeField] private float m_textFadeDuration = 0.5f;

        [Header("Buttons Container")]
        [SerializeField] private RectTransform m_buttonsContainerRect;
        [SerializeField] private Vector2 m_buttonsStartPosition;
        [SerializeField] private Vector2 m_buttonsTargetPosition;
        [SerializeField] private float m_buttonsSlideDuration = 0.5f;

        [Header("Controls")]
        [SerializeField] private Button m_resumeButton;
        [SerializeField] private Button m_optionsButton;
        [SerializeField] private Button m_restartButton;
        [SerializeField] private Button m_restartCheckpointButton;
        [SerializeField] private Button m_quitMenuButton;

        [Header("Audio")]
        [Tooltip("Ensure the theme exists in your SoundType enum in SoundData.cs")]
        [SerializeField] private SoundType m_pauseMenuTheme = SoundType.ThemeSong;
        #endregion

        #region Internal State
        private Sequence m_animationSequence;
        private List<AudioSource> m_mutedSources = new List<AudioSource>();
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
            m_resumeButton.onClick.AddListener(OnResumeClicked);
            if (m_optionsButton != null) m_optionsButton.onClick.AddListener(OnOptionsClicked);

            m_restartButton.onClick.AddListener(OnRestartClicked);
            if (m_restartCheckpointButton != null) m_restartCheckpointButton.onClick.AddListener(OnRestartCheckpointClicked);
            m_quitMenuButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnDisable()
        {
            m_resumeButton.onClick.RemoveAllListeners();
            if (m_optionsButton != null) m_optionsButton.onClick.RemoveAllListeners();

            m_restartButton.onClick.RemoveAllListeners();
            if (m_restartCheckpointButton != null) m_restartCheckpointButton.onClick.RemoveAllListeners();
            m_quitMenuButton.onClick.RemoveAllListeners();

            m_animationSequence?.Kill();
            m_mainCanvasGroup.DOKill();
        }
        #endregion

        #region Core Logic
        public async void ShowPanel(string title, string currentObjective)
        {
            gameObject.SetActive(true);
            ResetUIStates();

            if (m_pauseTitleText != null)
                m_pauseTitleText.text = string.IsNullOrEmpty(title) ? "PAUSED" : title.ToUpper();

            if (m_currentObjectiveText != null)
                m_currentObjectiveText.text = string.IsNullOrEmpty(currentObjective) ? "NO ACTIVE OBJECTIVE" : currentObjective.ToUpper();

            await AnimateInAsync();
        }

        private void ResetUIStates()
        {
            m_mainCanvasGroup.alpha = 0f;
            m_mainCanvasGroup.interactable = false;
            m_mainCanvasGroup.blocksRaycasts = false;

            if (m_titleContainerCG != null) m_titleContainerCG.alpha = 0f;
            if (m_titleContainerRect != null) m_titleContainerRect.anchoredPosition = m_titleStartPosition;

            if (m_pauseTitleText != null)
            {
                Color c = m_pauseTitleText.color; c.a = 0f; m_pauseTitleText.color = c;
            }
            if (m_currentObjectiveText != null)
            {
                Color c = m_currentObjectiveText.color; c.a = 0f; m_currentObjectiveText.color = c;
            }

            if (m_buttonsContainerRect != null) m_buttonsContainerRect.anchoredPosition = m_buttonsStartPosition;
        }

        private async Awaitable AnimateInAsync()
        {
            m_mainCanvasGroup.blocksRaycasts = true;

            m_mainCanvasGroup.DOFade(1f, m_bgFadeDuration).SetUpdate(true);

            // .SetUpdate(true) is mandatory since Time.timeScale is 0
            m_animationSequence = DOTween.Sequence().SetUpdate(true);

            m_animationSequence.Append(m_titleContainerCG.DOFade(1f, m_titleAnimDuration));
            m_animationSequence.AppendInterval(0.5f);
            m_animationSequence.Append(m_titleContainerRect.DOAnchorPos(m_titleTargetPosition, m_titleAnimDuration).SetEase(Ease.OutCubic));

            m_animationSequence.Append(m_pauseTitleText.DOFade(1f, m_textFadeDuration));
            m_animationSequence.Join(m_currentObjectiveText.DOFade(1f, m_textFadeDuration));

            m_animationSequence.AppendInterval(0.5f);
            m_animationSequence.Append(m_buttonsContainerRect.DOAnchorPos(m_buttonsTargetPosition, m_buttonsSlideDuration).SetEase(Ease.OutExpo));

            await m_animationSequence.AsyncWaitForCompletion();
            m_mainCanvasGroup.interactable = true;
        }
        #endregion

        #region Audio Muting System
        private void MuteGameplayAudio()
        {
            m_mutedSources.Clear();

            // Unity 6 compliant way to find all active audio sources
            AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var source in allSources)
            {
                // Skip the OutSoundManager entirely so our UI clicks and Pause Theme still work
                if (OutSoundManager.Instance != null && source.transform.IsChildOf(OutSoundManager.Instance.transform))
                    continue;

                // Only grab sources that aren't already muted
                if (!source.mute)
                {
                    source.mute = true;
                    m_mutedSources.Add(source);
                }
            }
        }

        private void RestoreGameplayAudio()
        {
            foreach (var source in m_mutedSources)
            {
                if (source != null)
                    source.mute = false;
            }
            m_mutedSources.Clear();
        }
        #endregion

        #region Callbacks
        public void OnResumeClicked()
        {
            // Just tell the Game Manager to go back to Gameplay. 
            // The Game Manager will handle Time.timeScale and input mapping.
            if (OutGameManager.Instance != null)
                OutGameManager.Instance.ChangeState(OutGameState.Gameplay);
        }

        private void OnOptionsClicked()
        {
            if (OutSoundManager.Instance != null) OutSoundManager.Instance.PlayOnClickSound();

            OutLogger.Note("[OutPausePanel] Options menu opened.");
            // Add your options panel logic here
        }

        private void OnRestartClicked()
        {
            Time.timeScale = 1f; // Must reset time scale before loading scene
            if (OutSoundManager.Instance != null) OutSoundManager.Instance.PlayOnClickSound();

            if (OutGameManager.Instance != null)
            {
                gameObject.SetActive(false);
                OutGameManager.Instance.IsNewGameSession = true;
                OutGameManager.Instance.RestartCurrentLevel();
            }
        }

        private void OnRestartCheckpointClicked()
        {
            Time.timeScale = 1f;
            if (OutSoundManager.Instance != null) OutSoundManager.Instance.PlayOnClickSound();

            if (OutGameManager.Instance != null)
            {
                gameObject.SetActive(false);
                OutGameManager.Instance.ContinueGame();
            }
        }

        private void OnQuitClicked()
        {
            Time.timeScale = 1f;
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