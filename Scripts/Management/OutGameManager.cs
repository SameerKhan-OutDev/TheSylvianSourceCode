using System;
using System.Threading;
using System.Threading.Tasks;
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

        public string ActiveSaveFilePath { get; set; }
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
            OutLogger.Note("<color=cyan>[OutGameManager]</color> Initializing Systems...");

            await Task.Delay(100); // Simulate async initialization
            if (inputManager == null) inputManager = OutInputManager.Instance;
            if (soundManager == null) soundManager = OutSoundManager.Instance;
            if (uiManager == null) uiManager = OutUIManager.Instance;

            // Optional: Check for Save Controller
            if (OutSaveController.Instance == null)
                OutLogger.Warn("[OutGameManager] OutSaveController not found. Saving/Loading may fail.");

            OutLogger.Note("<color=green>[OutGameManager]</color> Initialization Complete.");
        }

        public void ChangeState(OutGameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            StateChanged?.Invoke(currentState); // Notify listeners

            OutLogger.Note($"State Changed to {currentState}");

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
            OutLogger.Note($"[OutGameManager] Loading scene: {sceneName}");
            SceneLoadingStarted?.Invoke(sceneName);

            try
            {
                // Capture current scene before doing anything
                Scene oldScene = SceneManager.GetActiveScene();

                // 1. Load the lightweight loading scene additively
                await SceneManager.LoadSceneAsync(OutStringConstants.Scenes.LoadingScreen, LoadSceneMode.Additive);

                // FIX 1: Reset TimeScale so background Awaitables and Animations don't permanently freeze
                Time.timeScale = 1f;

                // FIX 2: Yield back to the Unity main thread for a couple of frames so the Loading UI ACTUALLY renders to the screen
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();

                // FIX 3: Unload the old scene FIRST. 
                // If we load the new scene while the old one is active, all Singletons in the new scene will destroy themselves thinking they are duplicates!
                if (oldScene.IsValid() && oldScene.name != OutStringConstants.Scenes.LoadingScreen)
                {
                    await SceneManager.UnloadSceneAsync(oldScene);
                    await Awaitable.NextFrameAsync(); // Give Unity a frame to clear destroyed objects from memory
                }

                // 2. Load the target scene additively in the background
                await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                // 3. Find the newly loaded scene and set it as Active (vital for lighting/skyboxes)
                Scene newScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                SceneManager.SetActiveScene(newScene);

                // Yield a frame so all the Awake() and Start() methods in the new scene initialize smoothly behind the loading screen
                await Awaitable.NextFrameAsync();

                // 4. Assets are ready. Unload the additive loading screen.
                await SceneManager.UnloadSceneAsync(OutStringConstants.Scenes.LoadingScreen);

                SceneLoadingCompleted?.Invoke(sceneName);
            }
            catch (OperationCanceledException)
            {
                OutLogger.Note("Scene load was cancelled because the object was destroyed.");
            }
        }

        // --------------------------------------------------------
        // GAME FLOW METHODS
        // --------------------------------------------------------

        public void StartNewGame()
        {
            OutLogger.Note("[OutGameManager] Starting New Game...");

            // Optional: If you want to wipe previous autosaves on New Game:

            // OutSaveController.Instance.DeleteSave(); 

            OutSoundManager.Instance.StopMusic(false);
            IsNewGameSession = true;
            ActiveSaveFilePath = string.Empty;
            StartLoadingScene(firstMissionScene);
        }

        public bool ContinueGame()
        {
            OutLogger.Note("[OutGameManager] Attempting to Continue from the latest save...");

            if (OutSaveController.Instance == null)
            {
                OutLogger.Error("[OutGameManager] OutSaveController is missing!");
                return false;
            }

            try
            {
                // If it crashes, the try-catch will catch it here
                SaveData latestData = OutSaveController.Instance.GetLatestSaveData(out string filePath);

                if (latestData != null && !string.IsNullOrEmpty(latestData.sceneName))
                {
                    OutLogger.Note($"[OutGameManager] Latest save found: {latestData.saveName}.");
                    CurrentSaveSlot = latestData.saveSlotIndex;
                    ActiveSaveFilePath = filePath;
                    OutSoundManager.Instance.StopMusic(false);
                    IsNewGameSession = false;
                    StartLoadingScene(latestData.sceneName);
                    return true;
                }
                else
                {
                    OutLogger.Warn("[OutGameManager] No valid save file found.");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                // THIS will catch the silent killer and tell us exactly what it is!
                OutLogger.Error($"[OutGameManager] FATAL CRASH during ContinueGame: {ex.Message}\n{ex.StackTrace}");
                return false; // Still return false so your UI Panel opens!
            }
        }

        public void TriggerSylvianFailed(string failureReason)
        {
            OutLogger.Note($"<color=red>[OutGameManager]</color> Sylvian Failed! Reason: {failureReason}");

            // Optional: Change game state to freeze background elements or pause
            ChangeState(OutGameState.Paused);

            // Broadcast the failure to any listeners (like a Game Over UI Panel)
            SylvianFailed?.Invoke(failureReason);
        }

        public void RestartCurrentLevel()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            OutLogger.Note($"[OutGameManager] Restarting level: {currentScene}");
            OutSoundManager.Instance.StopMusic(true);
            StartLoadingScene(currentScene);
        }

        public void QuitGame()
        {
            OutLogger.Note("[OutGameManager] Quitting Application.");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}