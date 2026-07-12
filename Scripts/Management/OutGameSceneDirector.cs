using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using DG.Tweening;
using Random = UnityEngine.Random;

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

        [Header("Post Processing")]
        [Tooltip("Assign the Global Volume containing ColorAdjustments, SMH, and Bloom overrides.")]
        [SerializeField] private Volume m_globalVolume;

        public static OutGameSceneDirector Instance { get; private set; }

        private ColorAdjustments m_colorAdjustments;
        private ShadowsMidtonesHighlights m_smh;
        private Bloom m_bloom;
        private Tween m_timeTween;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Cache HDRP Post-Processing Overrides
            if (m_globalVolume != null && m_globalVolume.profile != null)
            {
                m_globalVolume.profile.TryGet(out m_colorAdjustments);
                m_globalVolume.profile.TryGet(out m_smh);
                m_globalVolume.profile.TryGet(out m_bloom);
            }
        }

        private void OnEnable()
        {
            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance.StateChanged += OnGameStateChanged;
                OutGameManager.Instance.SylvianFailed += OnSylvianFailed;
            }
            else OutLogger.Error("Game Manager Instance doesn't exist, skipping Game State Change.");
        }

        private void OnDisable()
        {
            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance.StateChanged -= OnGameStateChanged;
                OutGameManager.Instance.SylvianFailed -= OnSylvianFailed;
            }

            m_timeTween?.Kill(); // Prevent memory leaks
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        private void Start()
        {
            Time.timeScale = 1f;
            OutUIManager.Instance?.EnableGameplayUI();
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
                    break;
            }
        }

        private async Awaitable InitializeGameSessionAsync()
        {
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
            OutLogger.Note("<color=cyan>[Gameplay]</color> Initializing Fresh Game.");

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

                if (openingPrologue != null && OutCinematicManager.Instance != null)
                {
                    OutGameManager.Instance.ChangeState(OutGameState.Cinematic);
                    OutInputManager.Instance.SetGameplayInput(false);

                    await OutCinematicManager.Instance.PlayCinematicAsync(openingPrologue);
                }
            }

            await ResumeGameplayWithTransitionAsync();
        }

        private void ResumeSavedGame()
        {
            SaveData data = null;

            // Load directly from the path tracked by the game manager (targets quick saves and slot saves accurately)
            if (OutGameManager.Instance != null && !string.IsNullOrEmpty(OutGameManager.Instance.ActiveSaveFilePath))
            {
                data = OutSaveController.Instance?.LoadGameFromPath(OutGameManager.Instance.ActiveSaveFilePath);
            }
            else
            {
                // Fallback in case of unexpected direct scene loading
                data = OutSaveController.Instance?.GetLatestSaveData(out _);
            }

            if (data != null)
            {
                if (playerTransform != null)
                {
                    playerTransform.position = data.playerPosition;
                    playerTransform.rotation = data.playerRotation;
                }

                // Restore all ISaveable entities (traps, AI, puzzle zones, terminals)
                var allSaveables = FindObjectsByType<MonoBehaviour>().OfType<ISaveable>();
                foreach (var saveable in allSaveables)
                {
                    saveable.RestoreFromSaveData(data);
                }

                OutLogger.Note("<color=cyan>[Gameplay]</color> World state and player position restored successfully.");
            }
            else
            {
                OutLogger.Warn("[Gameplay] Failed to load save data file. Starting with default state.");
            }

            StartGameplay();
        }

        public async Awaitable StartCutscene(OutCinematicData cutsceneAsset, Transform playerPos = null)
        {
            OutLogger.Note("<color=yellow>[Cutscene]</color> Starting Cutscene.");

            if (playerTransform != null && playerPos != null)
            {
                playerTransform.position = playerPos.position;
                playerTransform.rotation = playerPos.rotation;
            }
            else Debug.Log("Started cutscene, but skipped positioning the player because either player itself or player position was not given.");

            OutGameManager.Instance?.ChangeState(OutGameState.Cinematic);
            OutInputManager.Instance.SetGameplayInput(false);

            if (environment.Count > 0)
            {
                foreach (var env in environment) env.SetActive(false);
            }
            GameplayElements.SetActive(false);

            if (cutsceneAsset != null && OutCinematicManager.Instance != null)
            {
                await OutCinematicManager.Instance.PlayCinematicAsync(cutsceneAsset);
            }

            await ResumeGameplayWithTransitionAsync();
        }

        private async Awaitable ResumeGameplayWithTransitionAsync()
        {
            if (OutUIManager.Instance != null)
            {
                OutUIManager.Instance.ShowFadePanel();
                await Awaitable.WaitForSecondsAsync(0.5f);
            }

            StartGameplay();

            if (OutUIManager.Instance != null)
            {
                _ = OutUIManager.Instance.HideFadePanelAsync(0.5f);
            }
        }

        public void StartGameplay()
        {
            if (OutGameManager.Instance != null && OutGameManager.Instance.currentState == OutGameState.Gameplay) return;
            OutLogger.Note("<color=green>[Gameplay]</color> Player now has control.");

            OutInputManager.Instance.SetGameplayInput(true);
            Cinematics.SetActive(false);

            if (environment.Count > 0)
            {
                foreach (var env in environment) env.SetActive(true);
            }
            GameplayElements.SetActive(true);

            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance?.ChangeState(OutGameState.Gameplay);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Transition Time & Visuals back to normal
            float transitionTime = Random.Range(0.2f, 0.5f);

            m_timeTween?.Kill();
            m_timeTween = DOTween.To(() => Time.timeScale, x =>
            {
                Time.timeScale = x;
                Time.fixedDeltaTime = 0.02f * x; // Adjust physics step to avoid stuttering
            }, 1f, transitionTime).SetUpdate(true).SetEase(Ease.InOutSine);

            if (m_colorAdjustments != null)
            {
                DOTween.To(() => m_colorAdjustments.saturation.value, x => m_colorAdjustments.saturation.value = x, 0f, transitionTime).SetUpdate(true);
            }
        }

        public void PauseGame()
        {
            if (OutGameManager.Instance != null && OutGameManager.Instance.currentState == OutGameState.Paused) return;

            OutInputManager.Instance.SetGameplayInput(false);
            Cinematics.SetActive(false);
            GameplayElements.SetActive(true);

            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance?.ChangeState(OutGameState.Paused);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Transition Time to 0 and screen to Greyscale
            float transitionTime = Random.Range(0.2f, 0.5f);

            m_timeTween?.Kill();
            m_timeTween = DOTween.To(() => Time.timeScale, x =>
            {
                Time.timeScale = x;
                Time.fixedDeltaTime = 0.02f * x;
            }, 0f, transitionTime).SetUpdate(true).SetEase(Ease.OutSine);

            if (m_colorAdjustments != null)
            {
                DOTween.To(() => m_colorAdjustments.saturation.value, x => m_colorAdjustments.saturation.value = x, -100f, transitionTime).SetUpdate(true);
            }
        }

        public void OnSylvianFailed(string reason)
        {
            OutLogger.Note($"<color=red>[Gameplay]</color> Sylvian failed: {reason}");

            // Lock Inputs Instantly
            OutInputManager.Instance.SetGameplayInput(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            float transitionTime = Random.Range(0.2f, 0.5f);

            // Crash time to zero
            m_timeTween?.Kill();
            m_timeTween = DOTween.To(() => Time.timeScale, x =>
            {
                Time.timeScale = x;
                Time.fixedDeltaTime = 0.02f * x;
            }, 0f, transitionTime).SetUpdate(true).SetEase(Ease.OutQuad);

            // Spike Bloom Intensity
            if (m_bloom != null)
            {
                float targetBloom = m_bloom.intensity.value + 4f; // Push intensity high
                DOTween.To(() => m_bloom.intensity.value, x => m_bloom.intensity.value = x, targetBloom, transitionTime).SetUpdate(true);
            }

            // Push Shadows into heavy Red trackball mapping
            if (m_smh != null)
            {
                Vector4 targetRedShadows = new Vector4(1.5f, 0f, 0f, 0f);
                DOTween.To(() => m_smh.shadows.value, x => m_smh.shadows.value = x, targetRedShadows, transitionTime).SetUpdate(true);
            }

            // Render the Failure UI Panel
            OutUIManager.Instance?.ShowFailurePanel(reason);
        }
    }
}