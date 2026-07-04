using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;

namespace OutGame.Editor
{
    public class OutSaveManagerWindow : EditorWindow
    {
        private const string RelativeSavePath = "OutGame/SIAV/SavedSessions";

        // State variables for the GUI
        private string targetMissionToDelete = "Mission_01";
        private Vector2 scrollPosition;

        [MenuItem("Tools/SIAV/Save Data Manager", false, 0)]
        public static void ShowWindow()
        {
            OutSaveManagerWindow window = GetWindow<OutSaveManagerWindow>("Out Save Manager");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            string fullPath = GetSaveFolderPath();
            bool directoryExists = Directory.Exists(fullPath);

            GUILayout.Label("Core Save Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Save Folder"))
            {
                if (!directoryExists) Directory.CreateDirectory(fullPath);
                EditorUtility.RevealInFinder(fullPath);
            }
            if (GUILayout.Button("Backup All Saves"))
            {
                BackupSaves(fullPath);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);

            // --- MISSION SPECIFIC DELETION ---
            GUILayout.Label("Targeted Deletion", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Deletes any .json file where 'currentMission' matches the text below.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            targetMissionToDelete = EditorGUILayout.TextField("Mission ID / Name:", targetMissionToDelete);

            GUI.color = new Color(1f, 0.4f, 0.4f); // Light Red
            if (GUILayout.Button("Delete By Mission", GUILayout.Width(130)))
            {
                DeleteSavesByMission(fullPath, targetMissionToDelete);
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);

            // --- DANGER ZONE ---
            GUILayout.Label("Danger Zone", EditorStyles.boldLabel);
            GUI.color = Color.red;
            if (GUILayout.Button("NUKE ALL SAVE DATA", GUILayout.Height(30)))
            {
                DeleteAllSaves(fullPath);
            }
            GUI.color = Color.white;

            GUILayout.Space(15);

            // --- FILE DASHBOARD ---
            GUILayout.Label("Current Save Files", EditorStyles.boldLabel);

            if (!directoryExists)
            {
                EditorGUILayout.HelpBox("Save directory does not exist yet. Play the game to generate saves.", MessageType.Warning);
                return;
            }

            string[] saveFiles = Directory.GetFiles(fullPath, "*.json");

            if (saveFiles.Length == 0)
            {
                EditorGUILayout.HelpBox("No .json save files found.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, "box");
            foreach (string file in saveFiles)
            {
                DrawFileEntry(file);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawFileEntry(string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(fileName, GUILayout.Width(200));

            if (GUILayout.Button("Delete", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Delete File", $"Delete {fileName}?", "Yes", "Cancel"))
                {
                    File.Delete(filePath);
                    AssetDatabase.Refresh();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DeleteSavesByMission(string folderPath, string missionId)
        {
            if (!Directory.Exists(folderPath)) return;
            if (string.IsNullOrWhiteSpace(missionId))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a valid mission ID.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirm Deletion", $"Delete ALL saves where the mission is '{missionId}'?", "Yes", "Cancel"))
                return;

            string[] files = Directory.GetFiles(folderPath, "*.json");
            int deletedCount = 0;

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    SaveData data = JsonUtility.FromJson<SaveData>(json);

                    if (data != null && data.currentMission == missionId)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[OutSaveManager] Skipped unreadable file {Path.GetFileName(file)}: {e.Message}");
                }
            }

            Debug.Log($"<color=green>[OutSaveManager]</color> Deleted {deletedCount} save file(s) for mission '{missionId}'.");
        }

        private void DeleteAllSaves(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            if (EditorUtility.DisplayDialog("Nuke Saves", "Are you absolutely sure you want to delete ALL save files? This cannot be undone.", "DO IT", "Cancel"))
            {
                Directory.Delete(folderPath, true);
                Debug.Log("<color=red>[OutSaveManager]</color> All save data deleted.");
            }
        }

        private void BackupSaves(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            string backupFolder = folderPath + "_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backupFolder);

            string[] files = Directory.GetFiles(folderPath, "*.json");
            foreach (string file in files)
            {
                string destFile = Path.Combine(backupFolder, Path.GetFileName(file));
                File.Copy(file, destFile);
            }

            Debug.Log($"<color=cyan>[OutSaveManager]</color> Backed up {files.Length} files to: {backupFolder}");
            EditorUtility.RevealInFinder(backupFolder);
        }

        private string GetSaveFolderPath()
        {
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docPath, RelativeSavePath);
        }
    }
}