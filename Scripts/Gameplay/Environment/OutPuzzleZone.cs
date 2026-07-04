using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace OutGame
{
    [Serializable]
    public class SubPuzzleObjective
    {
        [Tooltip("Unique ID for this specific objective step (e.g., 'Trap_Electrocuted')")]
        public string objectiveID;

        [TextArea(2, 4)]
        [Tooltip("The hint displayed if the player takes too long on THIS specific step.")]
        public string hintText;

        public bool isCompleted = false;

        [Tooltip("Fired locally when this specific step is completed.")]
        public UnityEvent onObjectiveCompleted;
    }

    [Serializable]
    public class NPCSpawnData
    {
        public string spawnID; // Give each spawn point a unique ID
        public GameObject npcPrefab;
        public Transform spawnPoint;
    }

    /// <summary>
    /// Monitors player presence, spawns dynamic AI, handles BGM, and tracks sequential sub-objectives.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class OutPuzzleZone : MonoBehaviour, ISaveable
    {
        #region Global Events
        /// <summary>
        /// Industry-standard Decoupling: Any interactable, trap, or AI can fire this static event with an ID.
        /// If this Puzzle Zone has a matching sub-objective, it will automatically complete it.
        /// </summary>
        public static event Action<string> OnGlobalSubObjectiveCompleted;

        public static void BroadcastObjectiveComplete(string objectiveID)
        {
            OnGlobalSubObjectiveCompleted?.Invoke(objectiveID);
        }
        #endregion

        #region Inspector Settings
        [Header("Sub-Puzzle Objectives")]
        [Tooltip("List of sequential objectives. The hint timer uses the text of the first uncompleted objective.")]
        [SerializeField] private List<SubPuzzleObjective> m_objectives = new List<SubPuzzleObjective>();

        [Tooltip("How many seconds the player must be in the zone before the current step's hint triggers.")]
        [SerializeField] private float m_hintDelaySeconds = 10f;

        [Header("Final State")]
        [SerializeField] private bool m_isPuzzleSolved = false;
        public UnityEvent onEntirePuzzleSolved;

        [Header("Spawning Settings")]
        [Tooltip("List of NPCs to spawn immediately when the player enters the zone.")]
        [SerializeField] private List<NPCSpawnData> m_npcSpawnList = new List<NPCSpawnData>();
        [SerializeField] private bool m_destroyNPCsPostLeave = true;

        [Header("Sound Settings")]
        [SerializeField] private SoundType m_puzzleZoneSound = SoundType.PuzzleZoneMusic1;
        #endregion

        #region Internal State
        private CancellationTokenSource m_timerCts;
        private bool m_isPlayerInZone = false;
        private List<GameObject> m_spawnedNPCs = new List<GameObject>();
        #endregion

        #region Interface Implementation for ISaveable

        public string uniqueID;
        public string UniqueID => uniqueID;

        public void PopulateSaveData(SaveData data)
        {
            // Save Puzzle Steps
            foreach (var objective in m_objectives)
            {
                if (objective.isCompleted && !data.completedSubObjectives.Contains(objective.objectiveID))
                {
                    data.completedSubObjectives.Add(objective.objectiveID);
                }
            }
        }

        public void RestoreFromSaveData(SaveData data)
        {
            bool allCompleted = true;

            // Restore Puzzle Steps
            foreach (var objective in m_objectives)
            {
                if (data.completedSubObjectives.Contains(objective.objectiveID))
                {
                    objective.isCompleted = true;
                    // Note: Do NOT invoke the UnityEvent here, as it might trigger animations 
                    // or sounds meant for active gameplay. Just set the state visually.
                }
                else
                {
                    allCompleted = false;
                }
            }

            if (allCompleted)
            {
                m_isPuzzleSolved = true;
                if (TryGetComponent(out BoxCollider col)) col.enabled = false;
            }
        }

        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnEnable()
        {
            OnGlobalSubObjectiveCompleted += CompleteSubObjective;
        }

        private void OnDisable()
        {
            OnGlobalSubObjectiveCompleted -= CompleteSubObjective;
            CancelTimer();
            if (m_isPlayerInZone)
            {
                OutUIManager.Instance?.HideGameplayHint();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !m_isPuzzleSolved && !m_isPlayerInZone)
            {
                m_isPlayerInZone = true;
                OutSoundManager.Instance?.PlayMusic(m_puzzleZoneSound, false);

                SpawnNPCs();
                _ = StartHintTimerAsync();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                m_isPlayerInZone = false;
                CancelTimer();
                OutUIManager.Instance?.HideGameplayHint();
                OutSoundManager.Instance?.StopMusic(false);

                CleanupDeadNPCs();
            }
        }
        #endregion

        #region Objective Management
        /// <summary>
        /// Can be called via UnityEvents directly, or caught automatically via the static global event.
        /// </summary>
        public void CompleteSubObjective(string objectiveID)
        {
            if (m_isPuzzleSolved) return;

            bool allCompleted = true;
            bool foundObjective = false;

            OutSaveController.Instance?.SaveGame(
        "Current Mission", // You can pull this from a global narrative manager
        "Current Objective",
        OutGameSceneDirector.Instance.playerTransform,
        gameObject.name // Or a specific narrative room name
    );

            foreach (var objective in m_objectives)
            {
                if (!objective.isCompleted && objective.objectiveID == objectiveID)
                {
                    objective.isCompleted = true;
                    objective.onObjectiveCompleted?.Invoke();
                    foundObjective = true;
                }

                if (!objective.isCompleted)
                {
                    allCompleted = false;
                }
            }

            // If we completed a step, restart the hint timer so it prepares the NEXT objective's hint
            if (foundObjective && !allCompleted)
            {
                _ = StartHintTimerAsync();
            }

            // Auto-complete the main puzzle if all steps are done
            if (allCompleted && m_objectives.Count > 0)
            {
                MarkPuzzleSolved();
            }
        }

        /// <summary>
        /// Call this via the Final Objective Object's UnityEvent to finish the whole zone.
        /// </summary>
        public void MarkPuzzleSolved()
        {
            if (m_isPuzzleSolved) return;

            m_isPuzzleSolved = true;
            CancelTimer();

            OutUIManager.Instance?.HideGameplayHint();
            OutSoundManager.Instance?.StopMusic(false);
            OutSoundManager.Instance?.PlayUISound(SoundType.PuzzleSolved, true);

            onEntirePuzzleSolved?.Invoke();

            // Disable the collider completely so it stops checking
            if (TryGetComponent(out BoxCollider col)) col.enabled = false;
        }
        #endregion

        #region NPC Spawning & Cleanup
        private void SpawnNPCs()
        {
            // Only spawn if we haven't already spawned this session
            if (m_spawnedNPCs.Count > 0) return;

            foreach (var spawnData in m_npcSpawnList)
            {
                if (spawnData.npcPrefab != null && spawnData.spawnPoint != null)
                {
                    GameObject newNPC = Instantiate(spawnData.npcPrefab, spawnData.spawnPoint.position, spawnData.spawnPoint.rotation);
                    m_spawnedNPCs.Add(newNPC);
                }
            }
        }

        private void CleanupDeadNPCs()
        {
            if (!m_destroyNPCsPostLeave || m_spawnedNPCs.Count == 0) return;

            // Loop backwards when removing from a list
            for (int i = m_spawnedNPCs.Count - 1; i >= 0; i--)
            {
                GameObject npc = m_spawnedNPCs[i];

                // If it was destroyed by some other mechanic, just clean the list
                if (npc == null)
                {
                    m_spawnedNPCs.RemoveAt(i);
                    continue;
                }

                // Temporary check for dead AI. 
                // We check if the OutAfflictedAI script is disabled (which happens after the Die animation).
                // We will replace this with IOutCharacter.IsDead in the next step.
                if (npc.TryGetComponent(out OutAfflictedAI ai))
                {
                    if (!ai.enabled)
                    {
                        Destroy(npc);
                        m_spawnedNPCs.RemoveAt(i);
                    }
                }
            }
        }
        #endregion

        #region Hint Timer Logic
        private async Awaitable StartHintTimerAsync()
        {
            CancelTimer();
            m_timerCts = new CancellationTokenSource();
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(m_timerCts.Token, destroyCancellationToken);

            try
            {
                // Find the first uncompleted objective to know which hint to show
                SubPuzzleObjective currentObjective = null;
                foreach (var obj in m_objectives)
                {
                    if (!obj.isCompleted)
                    {
                        currentObjective = obj;
                        break;
                    }
                }

                // If all are completed, we don't need a hint
                if (currentObjective == null) return;

                // Wait in the background while the player figures it out
                await Awaitable.WaitForSecondsAsync(m_hintDelaySeconds, linkedCts.Token);

                // If they are still here and haven't solved it, show the specific objective hint.
                if (m_isPlayerInZone && !m_isPuzzleSolved && OutUIManager.Instance != null)
                {
                    OutUIManager.Instance.ShowGameplayHint(currentObjective.hintText);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Player left the zone, completed the step, or solved the puzzle early.
            }
        }

        private void CancelTimer()
        {
            if (m_timerCts != null)
            {
                m_timerCts.Cancel();
                m_timerCts.Dispose();
                m_timerCts = null;
            }
        }
        #endregion
    }
}