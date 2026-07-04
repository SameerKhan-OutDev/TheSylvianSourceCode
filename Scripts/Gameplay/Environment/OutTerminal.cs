using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.Events;

namespace OutGame // Make sure this matches your project's namespace
{
    public class OutTerminal : MonoBehaviour, IOutInteractable, ISaveable
    {
        [Header("Save & Narrative Settings")]
        public string uniqueID; // Generate a GUID in the inspector
        public string UniqueID => uniqueID;

        [Tooltip("If true, the game autosaves right after this terminal is successfully hacked.")]
        [SerializeField] private bool autosaveOnUnlock = true;
        [SerializeField] private string narrativeMissionName = OutStringConstants.Narrative.DefaultMission;
        [SerializeField] private string narrativeObjective = "Hack Terminal";
        [SerializeField] private string narrativeLocationMemory = "Sector 4";

        [Header("Terminal Settings")]
        public string terminalID;
        public OutTerminalPuzzleType puzzleType = OutTerminalPuzzleType.WordPuzzle;

        [Header("Word Puzzle Settings")]
        public string targetWord = "SYSTEM";
        [Range(1, 3)] public int extraDummyLetters = 2;

        [Header("Rendering & Highlighting")]
        [Tooltip("Assign the mesh renderers of the terminal that should glow when aimed at.")]
        [SerializeField] private List<Renderer> m_rendererObjects = new List<Renderer>();

        [Header("Events")]
        public UnityEvent OnTerminalUnlocked;
        public UnityEvent OnTerminalHackedFailed;

        private bool _isUnlocked = false;

        #region ISaveable Implementation

        public void PopulateSaveData(SaveData data)
        {
            // If this terminal is unlocked, save its ID to the list
            if (_isUnlocked && !data.unlockedTerminalIDs.Contains(uniqueID))
            {
                data.unlockedTerminalIDs.Add(uniqueID);
            }
        }

        public void RestoreFromSaveData(SaveData data)
        {
            // When loading the game, check if this terminal was previously unlocked
            if (data.unlockedTerminalIDs.Contains(uniqueID))
            {
                _isUnlocked = true;
                // Note: Do not invoke OnTerminalUnlocked here to avoid re-triggering doors or cutscenes 
                // during load. The doors should also be ISaveable and remember they are open.
            }
        }

        #endregion

        // ==========================================
        // IOutInteractable Implementation Requirements
        // ==========================================

        public EOutInteractableState CurrentState { get; private set; } // Assuming you have this enum defined elsewhere
        public List<Renderer> RendererObjects => m_rendererObjects;

        public void OnAimEnter()
        {
            if (_isUnlocked) return;
            // You can trigger your custom reticle or emissive highlights here
            OutLogger.Log($"[OutTerminal] Aiming at {gameObject.name}");
        }

        public void OnAimExit()
        {
            // Turn off highlights here
        }

        public async Awaitable ExecuteInteractionAsync(Transform a_instigator, Action<float> a_onProgress = null)
        {
            if (_isUnlocked) return;

            OutLogger.Log($"[OutTerminal] Interacted by {a_instigator.name}");

            // 1. Lock Player Movement/Camera and switch to UI map
            // Using your actual OutInputManager methods
            OutInputManager.Instance.SetGameplayInput(false);;

            // 2. Open the UI and await the result
            bool puzzleSolved = await OutTerminalUIController.Instance.OpenPuzzleAsync(this);

            // 3. Handle Result
            if (puzzleSolved)
            {
                _isUnlocked = true;
                OutLogger.Log($"[OutTerminal] Access Granted.");
                OnTerminalUnlocked?.Invoke();

                if (autosaveOnUnlock && OutSaveController.Instance != null && OutGameSceneDirector.Instance != null)
                {
                    OutLogger.Log($"<color=cyan>[OutTerminal]</color> Autosaving after successful hack...");

                    OutSaveController.Instance.SaveGame(
                        narrativeMissionName,
                        narrativeObjective,
                        OutGameSceneDirector.Instance.playerTransform,
                        narrativeLocationMemory
                    );
                }
            }
            else
            {
                OutLogger.LogWarning($"[OutTerminal] Access Denied.");
                OnTerminalHackedFailed?.Invoke();
            }

            // 4. Return control to player using your actual method
            OutInputManager.Instance.SetGameplayInput(true);
        }
    }

    public enum OutTerminalPuzzleType
    {
        WordPuzzle,
        AnalogPuzzle
    }
}