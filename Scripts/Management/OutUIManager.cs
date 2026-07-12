using DG.Tweening;
using OutGame;
using System;
using UnityEngine;

namespace OutGame
{
    public class OutUIManager : MonoBehaviour
    {
        // Singleton Instance
        public static OutUIManager Instance { get; private set; }

        [Header("Core References")]
        [SerializeField] private GameObject mainMenuUI;
        [SerializeField] private GameplayUI gameplayUI;

        [Header("End Screens")]
        [SerializeField] private OutFailurePanel failurePanel;
        private OutFailurePanel FailurePanel
        {
            get
            {
                if (failurePanel == null)
                {
                    failurePanel = FindAnyObjectByType<OutFailurePanel>(FindObjectsInactive.Include);
                    if (failurePanel == null)
                    {
                        OutLogger.Error("OutUIManager: Failure panel reference is missing! Please assign it in the inspector.");
                    }
                }
                return failurePanel;
            }
        }

        [SerializeField] private DOTweenAnimation blackLoadingPanel;

        #region UnityLifecycle
        private void Awake()
        {
            // Ensure only one instance exists
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject);
                InitializeUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            OutGameManager.Instance.StateChanged += OnGameStateChanged;
            OutGameManager.Instance.SceneLoadingStarted += OnSceneLoadingStarted;
            OutGameManager.Instance.SceneLoadingCompleted += OnSceneLoadingCompleted;
        }

        private void OnDisable()
        {
            if (OutGameManager.Instance != null)
                OutGameManager.Instance.StateChanged -= OnGameStateChanged;
            OutGameManager.Instance.SceneLoadingStarted -= OnSceneLoadingStarted;
            OutGameManager.Instance.SceneLoadingCompleted -= OnSceneLoadingCompleted;
        }
        #endregion

        #region Loading & Fading Logic
        private void OnSceneLoadingStarted(string sceneName)
        {
            ShowFadePanel();
        }

        private void OnSceneLoadingCompleted(string sceneName)
        {
            _ = HideFadePanelAsync(1f);
        }

        public void ShowFadePanel()
        {
            if (blackLoadingPanel != null)
                blackLoadingPanel.gameObject.SetActive(true);
        }

        public async Awaitable HideFadePanelAsync(float delay = 1f)
        {
            await Awaitable.WaitForSecondsAsync(delay);
            if (blackLoadingPanel != null)
                blackLoadingPanel.gameObject.SetActive(false);
        }
        #endregion

        #region State Management
        private void OnGameStateChanged(OutGameState newState)
        {
            if (newState == OutGameState.Gameplay)
            {
                EnableGameplayUI();

                // Find FailurePanel if it's not already assigned
                if (FailurePanel != null)
                    FailurePanel.gameObject.SetActive(false);
                else
                {
                    failurePanel = FindAnyObjectByType<OutFailurePanel>(FindObjectsInactive.Include);
                    if (failurePanel != null)
                    {
                        failurePanel.gameObject.SetActive(false);
                    }
                }
            }
            else if (newState == OutGameState.MainMenu)
            {
                EnableMainMenu();
            }
        }

        private void InitializeUI()
        {
            // Set initial UI state
            EnableMainMenu();
        }

        public void EnableMainMenu()
        {
            mainMenuUI.SetActive(true);
            if (gameplayUI != null)
                gameplayUI.gameObject.SetActive(false);
        }

        public void EnableGameplayUI()
        {
            mainMenuUI.SetActive(false);

            // Find and enable the GameplayUI if it's not already assigned
            if (gameplayUI != null)
                gameplayUI.gameObject.SetActive(true);
            else
            {
                gameplayUI = FindAnyObjectByType<GameplayUI>();
                if (gameplayUI != null)
                {
                    gameplayUI.gameObject.SetActive(true);
                }
            }
        }

        public void ShowGameplayHint(string message)
        {
            if (gameplayUI != null && gameplayUI.gameplayHintsPanel != null)
            {
                gameplayUI.gameplayHintsPanel.DisplayHint(message);
            }
        }

        public void HideGameplayHint()
        {
            if (gameplayUI != null && gameplayUI.gameplayHintsPanel != null)
            {
                gameplayUI.gameplayHintsPanel.HideHint();
            }
        }

        public void ShowFailurePanel(string reason)
        {
            if (FailurePanel != null)
            {
                FailurePanel.gameObject.SetActive(true);
                FailurePanel.ShowPanel(reason);
            }
            else
            {

                OutLogger.Error("OutUIManager: Failure panel reference is missing! Cannot display death screen.");
            }
        }
        #endregion
    }
}