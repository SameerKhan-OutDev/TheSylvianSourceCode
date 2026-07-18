using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OutGame
{
    // 2. THE CONTROLLER
    public class OutSaveController : MonoBehaviour
    {
        // Singleton pattern for easy access from anywhere (e.g., OutSaveController.Instance.SaveGame())
        public static OutSaveController Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private string folderPath = "OutGame/SIAV/SavedSessions";
        [SerializeField] private string defaultFileName = "quick_save.json";
        [SerializeField] private bool noParentAllowed = false;

        public bool HasSaveFile(int slotIndex)
        {
            string filePath = GetFilePathForSlot(slotIndex);
            return File.Exists(filePath);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (noParentAllowed) transform.parent = null;
            }
            else Destroy(gameObject);
        }

        /// <summary>
        /// Collects data and writes it to the JSON file based on the active save slot.
        /// </summary>
        /// <param name="mission">Current active mission ID or Name</param>
        /// <param name="objective">Current active objective text</param>
        /// <param name="playerParams">The player's Transform</param>
        /// <param name="locationMemory">The narrative name of the current location (e.g., "The Red Hallway")</param>
        public void SaveGame(string mission, string objective, Transform playerParams, string locationMemory)
        {
            // Fetch the active slot from the GameManager
            int activeSlot = 0;
            if (OutGameManager.Instance != null)
            {
                activeSlot = OutGameManager.Instance.CurrentSaveSlot;
            }

            // A. Prepare the Data
            SaveData data = new SaveData
            {
                // Metadata
                saveName = $"Save Slot {activeSlot + 1}", // Give it a dynamic name based on the slot
                saveSlotIndex = activeSlot, // Make sure the data object knows its own slot
                lastPlayedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,

                // Mission Info
                currentMission = mission,
                currentObjective = objective,

                // Position & Rotation
                playerPosition = playerParams.position,
                playerRotation = playerParams.rotation,
                rememberedLocationName = locationMemory
            };

            var allSaveables = FindObjectsByType<MonoBehaviour>().OfType<ISaveable>();
            foreach (var saveable in allSaveables)
            {
                saveable.PopulateSaveData(data);
            }

            // B. Serialize to JSON
            string json = JsonUtility.ToJson(data, true);

            // C. Write to File
            try
            {
                string fullPath = GetSaveFolderPath();

                // Ensure directory exists
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }

                // FIX: Write to the specific slot file path instead of the default hardcoded string!
                string filePath = GetFilePathForSlot(activeSlot);
                File.WriteAllText(filePath, json);

                // Keep the GameManager's active path updated so the game knows exactly where we just saved
                if (OutGameManager.Instance != null)
                {
                    OutGameManager.Instance.ActiveSaveFilePath = filePath;
                }

                OutLogger.Note($"<color=green>[OutSaveController]</color> Game saved successfully at: {filePath}");
            }
            catch (Exception e)
            {
                OutLogger.Error($"[OutSaveController] Failed to save game: {e.Message}");
            }
        }

        /// <summary>
        /// Loads the data from JSON and returns the SaveData object.
        /// You can then use this object to set the player's position, etc.
        /// </summary>
        public SaveData LoadGame()
        {
            string filePath = Path.Combine(GetSaveFolderPath(), defaultFileName);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    SaveData data = JsonUtility.FromJson<SaveData>(json);
                    OutLogger.Note($"<color=cyan>[OutSaveController]</color> Game loaded: {data.lastPlayedTime}");
                    return data;
                }
                catch (Exception e)
                {
                    OutLogger.Error($"[OutSaveController] Save file corrupted: {e.Message}");
                    return null;
                }
            }
            else
            {
                OutLogger.Warn("[OutSaveController] No save file found.");
                return null;
            }
        }

        public SaveData LoadGame(int slotIndex)
        {
            string filePath = GetFilePathForSlot(slotIndex);
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<SaveData>(json);
            }
            return null;
        }

        /// <summary>
        /// Loads save data from a specific absolute file path.
        /// </summary>
        public SaveData LoadGameFromPath(string fullPath)
        {
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
            {
                try
                {
                    string json = File.ReadAllText(fullPath);
                    return JsonUtility.FromJson<SaveData>(json);
                }
                catch (Exception e)
                {
                    OutLogger.Error($"[OutSaveController] Save file corrupted at {fullPath}: {e.Message}");
                }
            }
            return null;
        }

        public void DeleteSave(int slotIndex)
        {
            string filePath = GetFilePathForSlot(slotIndex);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                OutLogger.Note($"<color=red>[OutSaveController]</color> Deleted save slot: {slotIndex}");
            }
        }



        // Helper to get the correct documents path
        private string GetSaveFolderPath()
        {
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docPath, folderPath);
        }
        // Helper to generate unique filenames per slot
        private string GetFilePathForSlot(int index)
        {
            string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), folderPath);
            return Path.Combine(fullPath, $"save_{index}.json");
        }

        public SaveData GetLatestSaveData(out string latestFilePath)
        {
            latestFilePath = string.Empty;
            string fullPath = GetSaveFolderPath();

            if (!Directory.Exists(fullPath)) return null;

            string[] saveFiles = Directory.GetFiles(fullPath, "*.json");
            if (saveFiles.Length == 0) return null;

            SaveData newestData = null;
            DateTime newestTime = DateTime.MinValue;

            foreach (string file in saveFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    SaveData data = JsonUtility.FromJson<SaveData>(json);

                    if (data != null && DateTime.TryParseExact(data.lastPlayedTime, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out DateTime saveTime))
                    {
                        if (saveTime > newestTime)
                        {
                            newestTime = saveTime;
                            newestData = data;
                            latestFilePath = file;
                        }
                    }
                }
                catch (Exception e)
                {
                    OutLogger.Warn($"[OutSaveController] Skipped unreadable save file during latest check: {e.Message}");
                }
            }

            return newestData;
        }
    }
}