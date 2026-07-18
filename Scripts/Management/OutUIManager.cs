using DG.Tweening;
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
        [SerializeField] private OutPausePanel pausePanel;

        private OutPausePanel PausePanel
        {
            get
            {
                if (pausePanel == null)
                {
                    pausePanel = FindAnyObjectByType<OutPausePanel>(FindObjectsInactive.Include);
                    if (pausePanel == null)
                        OutLogger.Error("OutUIManager: Pause panel reference is missing! Please assign it.");
                }
                return pausePanel;
            }
        }

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
            if (Instance == null)
            {
                Instance = this;
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

            OutGameManager.Instance.SylvianFailed += ShowFailurePanel;
        }

        private void OnDisable()
        {
            if (OutGameManager.Instance != null)
                OutGameManager.Instance.StateChanged -= OnGameStateChanged;
            OutGameManager.Instance.SceneLoadingStarted -= OnSceneLoadingStarted;
            OutGameManager.Instance.SceneLoadingCompleted -= OnSceneLoadingCompleted;

            OutGameManager.Instance.SylvianFailed -= ShowFailurePanel;
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
                if (FailurePanel != null) FailurePanel.gameObject.SetActive(false);
                if (PausePanel != null) PausePanel.gameObject.SetActive(false);
            }
            else if (newState == OutGameState.MainMenu)
            {
                EnableMainMenu();
                if (PausePanel != null) PausePanel.gameObject.SetActive(false);
            }
            else if (newState == OutGameState.Paused)
            {
                if (PausePanel != null)
                {
                    string currentObjective = "NO ACTIVE OBJECTIVE";
                    if (OutSaveController.Instance != null)
                    {
                        SaveData latestData = OutSaveController.Instance.GetLatestSaveData(out _);
                        if (latestData != null && !string.IsNullOrEmpty(latestData.currentObjective))
                        {
                            currentObjective = latestData.currentObjective;
                        }
                    }

                    PausePanel.gameObject.SetActive(true);

                    PausePanel.ShowPanel("PAUSED", currentObjective);
                }
            }
            else if (newState == OutGameState.Result)
            {
                // Close the pause panel immediately just in case they died the exact frame they paused
                if (PausePanel != null) PausePanel.gameObject.SetActive(false);

                // Note: The FailurePanel is opened by the ShowFailurePanel() method 
                // which triggers via the SylvianFailed event, so we don't need to open it here!
            }
        }

        private void InitializeUI()
        {
            EnableMainMenu();
        }

        public void EnableMainMenu()
        {
            mainMenuUI.SetActive(true);
            if (gameplayUI != null) gameplayUI.gameObject.SetActive(false);
        }

        public void EnableGameplayUI()
        {
            mainMenuUI.SetActive(false);

            if (gameplayUI != null)
                gameplayUI.gameObject.SetActive(true);
            else
            {
                gameplayUI = FindAnyObjectByType<GameplayUI>();
                if (gameplayUI != null) gameplayUI.gameObject.SetActive(true);
            }
        }

        public void ShowGameplayHint(string message)
        {
            if (gameplayUI != null && gameplayUI.gameplayHintsPanel != null)
                gameplayUI.gameplayHintsPanel.DisplayHint(message);
        }

        public void HideGameplayHint()
        {
            if (gameplayUI != null && gameplayUI.gameplayHintsPanel != null)
                gameplayUI.gameplayHintsPanel.HideHint();
        }

        public void ShowFailurePanel(string reason)
        {
            if (FailurePanel != null)
            {
                FailurePanel.gameObject.SetActive(true);
                FailurePanel.ShowPanel(reason);
            }
        }
        #endregion
    }
}