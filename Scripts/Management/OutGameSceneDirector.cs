using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Transactions;
using TimeGhost;
using UnityEngine;
using UnityEngine.Video;

namespace OutGame
{
    public class OutGameSceneDirector : MonoBehaviour
    {
        [Header("Scene References")]
        public Transform playerTransform;
        public GameObject GameplayElements;

        [SerializeField] private Transform defaultSpawnPoint;
        [SerializeField] private List<GameObject> environment;

        public GameObject Cinematics;

        [Header("Narrative Sequences")]
        [SerializeField] private OutCinematicData openingPrologue;


        public static OutGameSceneDirector Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance.StateChanged += OnGameStateChanged;
            }
            else OutLogger.LogError("Game Manager Instance doesn't exist, skipping Game State Change.");
        }
        private void OnDisable()
        {
            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance.StateChanged -= OnGameStateChanged;
            }
            else OutLogger.LogError("Game Manager Instance doesn't exist, skipping Game State Change.");
        }
        private void OnDestroy()
        {
            Instance = null;
            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance.StateChanged -= OnGameStateChanged;
            }
            else OutLogger.LogError("Game Manager Instance doesn't exist, skipping Game State Change.");
        }

        private void Start()
        {
            // Ensure UI and Time are reset
            Time.timeScale = 1f;
            OutUIManager.Instance?.EnableGameplayUI();

            // Fire and forget the asynchronous initialization
            _ = InitializeGameSessionAsync();
        }

        private void OnGameStateChanged(OutGameState newState)
        {
            switch (newState)
            {
                case OutGameState.Gameplay:
                    StartGameplay();
                    break;
                case OutGameState.Paused:
                    PauseGame();
                    break;
                case OutGameState.Cinematic:
                    // Handle cinematic state if needed
                    break;
                default:
                    break;
            }
        }

        private async Awaitable InitializeGameSessionAsync()
        {
            // Fallback for testing directly in the Editor without Main Menu
            bool isNewGame = OutGameManager.Instance == null || OutGameManager.Instance.IsNewGameSession;

            if (isNewGame)
            {
                await SetupNewGameAsync();
            }
            else
            {
                ResumeSavedGame();
            }
        }

        private async Awaitable SetupNewGameAsync()
        {
            OutLogger.Log("<color=cyan>[Gameplay]</color> Initializing Fresh Game.");

            // 1. Place player at start
            if (playerTransform != null && defaultSpawnPoint != null)
            {
                playerTransform.position = defaultSpawnPoint.position;
                playerTransform.rotation = defaultSpawnPoint.rotation;
            }

            if (OutGameManager.Instance != null)
            {
                if (environment.Count > 0)
                {
                    foreach (var env in environment) env.SetActive(false);
                }
                GameplayElements.SetActive(false);

                // 2. Lock state and trigger Cinematic
                if (openingPrologue != null && OutCinematicManager.Instance != null)
                {
                    OutGameManager.Instance.ChangeState(OutGameState.Cinematic);
                    OutInputManager.Instance.SetGameplayInput(false);

                    await OutCinematicManager.Instance.PlayCinematicAsync(openingPrologue);
                }
            }

            // 3. Smooth transition to gameplay
            await ResumeGameplayWithTransitionAsync();
        }

        private void ResumeSavedGame()
        {
            int activeSlot = OutGameManager.Instance != null ? OutGameManager.Instance.CurrentSaveSlot : 0;
            SaveData data = OutSaveController.Instance?.LoadGame(activeSlot);

            if (data != null)
            {
                // 1. Restore Player
                if (playerTransform != null)
                {
                    playerTransform.position = data.playerPosition;
                    playerTransform.rotation = data.playerRotation;
                }

                // 2. Restore World State
                var allSaveables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>();
                foreach (var saveable in allSaveables)
                {
                    saveable.RestoreFromSaveData(data);
                }
            }

            StartGameplay();
        }

        public async Awaitable StartCutscene(OutCinematicData cutsceneAsset, Transform playerPos = null)
        {
            OutLogger.Log("<color=yellow>[Cutscene]</color> Starting Cutscene.");

            if (playerTransform != null && playerPos != null)
            {
                playerTransform.position = playerPos.position;
                playerTransform.rotation = playerPos.rotation;
            }
            else Debug.Log("Started cutscene, but skipped positioning the player becaues either player itsef or player position was not given.");

            // Lock the state BEFORE the cutscene plays
            OutGameManager.Instance?.ChangeState(OutGameState.Cinematic);
            OutInputManager.Instance.SetGameplayInput(false);

            if (environment.Count > 0)
            {
                foreach (var env in environment)
                {
                    env.SetActive(false);
                }
            }
            GameplayElements.SetActive(false);

            // Play the Cutscene
            if (cutsceneAsset != null && OutCinematicManager.Instance != null)
            {
                await OutCinematicManager.Instance.PlayCinematicAsync(cutsceneAsset);
            }

            // 3. Smooth transition back to gameplay
            await ResumeGameplayWithTransitionAsync();
        }

        private async Awaitable ResumeGameplayWithTransitionAsync()
        {
            // Drop curtain if UI manager exists
            if (OutUIManager.Instance != null)
            {
                OutUIManager.Instance.ShowFadePanel();
                await Awaitable.WaitForSecondsAsync(0.5f); // Wait for DOTween to cover the screen
            }

            // Setup environment & player control behind the scenes
            StartGameplay();

            // Lift curtain
            if (OutUIManager.Instance != null)
            {
                _ = OutUIManager.Instance.HideFadePanelAsync(0.5f);
            }
        }

        public void StartGameplay()
        {
            if (OutGameManager.Instance != null && OutGameManager.Instance.currentState == OutGameState.Gameplay) return;
            OutLogger.Log("<color=green>[Gameplay]</color> Player now has control.");


            OutInputManager.Instance.SetGameplayInput(true);

            Cinematics.SetActive(false);
            if (environment.Count > 0)
            {
                foreach (var env in environment)
                {
                    env.SetActive(true);
                }
            }
            GameplayElements.SetActive(true);


            if (OutGameManager.Instance != null && OutGameManager.Instance.currentState != OutGameState.Gameplay)
            {
                OutGameManager.Instance?.ChangeState(OutGameState.Gameplay);

                OutLogger.Log("Game state changed to Gameplay.");
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // TODO: Start ambient audio via OutSoundManager
        }

        public void PauseGame()
        {
            if (OutGameManager.Instance != null && OutGameManager.Instance.currentState == OutGameState.Paused) return;
            OutInputManager.Instance.SetGameplayInput(false);
            Cinematics.SetActive(false);

            GameplayElements.SetActive(true);

            if (OutGameManager.Instance != null && OutGameManager.Instance.currentState != OutGameState.Paused)
            {
                OutGameManager.Instance?.ChangeState(OutGameState.Paused);
                OutLogger.Log("Game state changed to Paused.");
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}