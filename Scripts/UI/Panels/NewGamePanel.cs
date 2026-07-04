using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace OutGame
{
    public class NewGamePanel : MonoBehaviour
    {
        public List<SaveGameSlot> saveSlots;
        public NewGameConfirmationPanel confirmationDialog;

        private void OnEnable()
        {
            Invoke(nameof(LoadSlots), 1f);
            OutGameManager.Instance.SceneLoadingStarted += CloseDialog;
        }

        private void OnDisable() => OutGameManager.Instance.SceneLoadingStarted -= CloseDialog;

        private void CloseDialog(string sceneName) => confirmationDialog.gameObject.SetActive(false);

        public void LoadSlots()
        {
            for (int i = 0; i < saveSlots.Count; i++)
            {
                bool fileExists = OutSaveController.Instance.HasSaveFile(i);
                // Safely attempt to load. If corrupted, LoadGame returns null.
                SaveData data = fileExists ? OutSaveController.Instance.LoadGame(i) : null;

                // Only mark as loaded if the data is NOT null
                bool isValidSave = data != null;

                UpdateSlotUI(saveSlots[i], data, isValidSave);
            }
            HandleButtonPressed();
        }

        private void UpdateSlotUI(SaveGameSlot slot, SaveData data, bool isLoaded)
        {
            slot.isLoaded = isLoaded;
            slot.title.text = isLoaded ? data.saveName : "Empty Slot";
            slot.completion.text = isLoaded ? $"Completion: {data.completionPercentage}%" : "";
            slot.howOld.text = isLoaded ? OutTimeUtility.GetTimeAgo(data.lastPlayedTime) : "";
            slot.locationName.text = isLoaded ? $"Location: {data.rememberedLocationName}" : "";
        }

        public void HandleButtonPressed()
        {
            for (int i = 0; i < saveSlots.Count; i++)
            {
                int index = i;
                Button btn = saveSlots[i].GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSlotClicked(index));
            }
        }

        private void OnSlotClicked(int index)
        {
            bool isLoaded = saveSlots[index].isLoaded;

            // Common UI setup
            confirmationDialog.gameObject.SetActive(true);
            confirmationDialog.forOverwrite = isLoaded;
            confirmationDialog.SetConfirmationText(isLoaded
                ? "A save already exists. Overwrite and start new?"
                : "Start a new game in this slot?");

            // Common Listener setup
            confirmationDialog.confirmButton.onClick.RemoveAllListeners();
            confirmationDialog.confirmButton.onClick.AddListener(() =>
            {
                if (isLoaded) OutSaveController.Instance.DeleteSave(index);

                PlayerPrefs.SetInt("LastSaveSlot", index);
                PlayerPrefs.Save();

                OutGameManager.Instance.StartNewGame();
                OutGameManager.Instance.CurrentSaveSlot = index;
                OutLogger.Log($"Slot {index} — Game started.");
                OutMenuController.MainMenuInstance.HideAllPanelsInstant();
                OutMenuController.MainMenuInstance.gameObject.SetActive(false);
            });
        }
    }
}