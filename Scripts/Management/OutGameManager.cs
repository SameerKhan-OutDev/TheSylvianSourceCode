using System;
using System.Threading;
using System.Threading.Tasks;
using TimeGhost;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
// using TimeGhost; // Uncomment if needed

namespace OutGame
{
    public class OutGameManager : MonoBehaviour
    {
        // Singleton Instance
        public static OutGameManager Instance { get; private set; }

        [Header("Core States")]
        public OutGameState currentState; // Assumes OutGameState is defined in EnumsJar

        [Header("Game Configuration")]
        [Tooltip("The name of the scene to load when 'New Game' is clicked.")]
        [SerializeField] private string firstMissionScene = "3_GameScene";

        [Header("Core References")]
        [SerializeField] private OutInputManager inputManager;
        [SerializeField] private OutSoundManager soundManager;
        [SerializeField] private OutUIManager uiManager;

        #region public properties
        [HideInInspector]
        public bool IsNewGameSession { get; internal set; }
        public int CurrentSaveSlot { get; set; }

        #endregion

        #region delegates

        public delegate void OnStateChange(OutGameState newState);
        public delegate void OnSceneLoadingStart(string sceneName);
        public delegate void OnSceneLoadingComplete(string sceneName);


        public delegate void StartGame();

        public delegate void OnPlayerFailed(string reason);

        #endregion

        #region events

        public event OnStateChange StateChanged;
        public event OnSceneLoadingStart SceneLoadingStarted;
        public event OnSceneLoadingComplete SceneLoadingCompleted;

        public event OnPlayerFailed SylvianFailed;

        #endregion


        private void Awake()
        {
            // Ensure only one instance exists
            if (Instance == null)
            {
                Instance = this;

                if (transform.parent != null)
                {
                    transform.parent = null;
                    DontDestroyOnLoad(gameObject);
                }

                InitializeSystems();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async void InitializeSystems()
        {
            OutLogger.Log("<color=cyan>[OutGameManager]</color> Initializing Systems...");

            await Task.Delay(100); // Simulate async initialization
            if (inputManager == null) inputManager = OutInputManager.Instance;
            if (soundManager == null) soundManager = OutSoundManager.Instance;
            if (uiManager == null) uiManager = OutUIManager.Instance;

            // Optional: Check for Save Controller
            if (OutSaveController.Instance == null)
                OutLogger.LogWarning("[OutGameManager] OutSaveController not found. Saving/Loading may fail.");

            OutLogger.Log("<color=green>[OutGameManager]</color> Initialization Complete.");
        }

        public void ChangeState(OutGameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            StateChanged?.Invoke(currentState); // Notify listeners

            OutLogger.Log($"State Changed to {currentState}");

            switch (currentState)
            {
                case OutGameState.MainMenu:
                    Time.timeScale = 1f;
                    break;
                case OutGameState.Gameplay:
                    break;
                case OutGameState.Paused:
                    break;
            }
        }

        public void StartLoadingScene(string sceneName)
        {
            // Start the asynchronous loading process without awaiting it
            _ = LoadGameScene(sceneName);
        }

        public async Awaitable LoadGameScene(string sceneName)
        {
            OutLogger.Log($"[OutGameManager] Loading scene: {sceneName}");
            SceneLoadingStarted?.Invoke(sceneName);

            try
            {
                // 1. Load the scene and await the operation directly
                await SceneManager.LoadSceneAsync(sceneName);

                // 2. Fire the finish event
                SceneLoadingCompleted?.Invoke(sceneName);

            }
            catch (OperationCanceledException)
            {
                OutLogger.Log("Scene load was cancelled because the object was destroyed.");
            }
        }

        // --------------------------------------------------------
        // GAME FLOW METHODS
        // --------------------------------------------------------

        public void StartNewGame()
        {
            OutLogger.Log("[OutGameManager] Starting New Game...");

            // Optional: If you want to wipe previous autosaves on New Game:

            // OutSaveController.Instance.DeleteSave(); 

            OutSoundManager.Instance.StopMusic(false);
            IsNewGameSession = true;
            StartLoadingScene(firstMissionScene);
        }

        public bool ContinueGame()
        {
            OutLogger.Log("[OutGameManager] Attempting to Continue from the latest save...");

            if (OutSaveController.Instance == null)
            {
                OutLogger.LogError("[OutGameManager] OutSaveController is missing!");
                return false;
            }

            try
            {
                // If it crashes, the try-catch will catch it here
                SaveData latestData = OutSaveController.Instance.GetLatestSaveData(out string filePath);

                if (latestData != null && !string.IsNullOrEmpty(latestData.sceneName))
                {
                    OutLogger.Log($"[OutGameManager] Latest save found: {latestData.saveName}.");
                    CurrentSaveSlot = latestData.saveSlotIndex;
                    OutSoundManager.Instance.StopMusic(false);
                    IsNewGameSession = false;
                    StartLoadingScene(latestData.sceneName);
                    return true;
                }
                else
                {
                    OutLogger.LogWarning("[OutGameManager] No valid save file found.");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                // THIS will catch the silent killer and tell us exactly what it is!
                OutLogger.LogError($"[OutGameManager] FATAL CRASH during ContinueGame: {ex.Message}\n{ex.StackTrace}");
                return false; // Still return false so your UI Panel opens!
            }
        }

        public void TriggerSylvianFailed(string failureReason)
        {
            OutLogger.Log($"<color=red>[OutGameManager]</color> Sylvian Failed! Reason: {failureReason}");

            // Optional: Change game state to freeze background elements or pause
            ChangeState(OutGameState.Paused);

            // Broadcast the failure to any listeners (like a Game Over UI Panel)
            SylvianFailed?.Invoke(failureReason);
        }

        public void RestartCurrentLevel()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            OutLogger.Log($"[OutGameManager] Restarting level: {currentScene}");
            OutSoundManager.Instance.StopMusic(true);
            StartLoadingScene(currentScene);
        }

        public void QuitGame()
        {
            OutLogger.Log("[OutGameManager] Quitting Application.");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}