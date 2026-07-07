using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

namespace OutGame
{
    /// <summary>
    /// Manages the Load Game UI interface, handling save slot instantiation, detailed data inspection, and scene loading.
    /// Incorporates DOTween for dynamic visual feedback.
    /// </summary>
    public class LoadGamePanel : MonoBehaviour
    {
        #region fields
        [Header("Slots Configuration")]
        [SerializeField] private List<SaveGameSlot> saveSlots;

        [Header("Detail View Components")]
        [SerializeField] private CanvasGroup detailCanvasGroup;
        [SerializeField] private TMP_Text detailTitleText;
        [SerializeField] private TMP_Text detailLocationText;
        [SerializeField] private TMP_Text detailCompletionText;
        [SerializeField] private TMP_Text detailTimeOldText;

        [Header("Controls")]
        [SerializeField] private Button confirmLoadButton;

        private int _selectedSlotIndex = -1;
        private SaveData _selectedData;
        #endregion

        #region unity lifecycle
        private void OnEnable()
        {
            ResetPanelState();
            PopulateSlots();
            OutGameManager.Instance.SceneLoadingStarted += OnSceneLoadingStarted;
        }

        private void OnDisable()
        {
            if (OutGameManager.Instance != null)
                OutGameManager.Instance.SceneLoadingStarted -= OnSceneLoadingStarted;

            // Clean up tweens to prevent memory leaks or null references upon panel deactivation
            detailCanvasGroup.DOKill();
            foreach (var slot in saveSlots)
            {
                slot.transform.DOKill();
            }
        }
        #endregion

        #region initialization
        /// <summary>
        /// Clears the detail view and prepares the interface for a fresh state.
        /// </summary>
        private void ResetPanelState()
        {
            _selectedSlotIndex = -1;
            _selectedData = null;

            confirmLoadButton.interactable = false;
            detailCanvasGroup.alpha = 0f;

            confirmLoadButton.onClick.RemoveAllListeners();
            confirmLoadButton.onClick.AddListener(LoadSelectedGame);
        }

        /// <summary>
        /// Reads save data from the controller and sets up basic identifying UI on child slots.
        /// </summary>
        private void PopulateSlots()
        {
            for (int i = 0; i < saveSlots.Count; i++)
            {
                int slotIndex = i;
                SaveGameSlot slot = saveSlots[i];

                bool fileExists = OutSaveController.Instance.HasSaveFile(slotIndex);
                SaveData data = fileExists ? OutSaveController.Instance.LoadGame(slotIndex) : null;
                bool isLoaded = data != null;

                slot.isLoaded = isLoaded;

                // Display only minimal info on the slot button itself
                slot.title.text = isLoaded ? data.saveName : "Empty Slot";
                slot.locationName.text = isLoaded ? data.rememberedLocationName : "---";

                // Clear the deeper details from the prefab if they exist, forcing reliance on the main Detail View
                if (slot.completion != null) slot.completion.text = "";
                if (slot.howOld != null) slot.howOld.text = "";

                slot.transform.localScale = Vector3.one;

                Button btn = slot.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();

                if (isLoaded)
                {
                    btn.interactable = true;
                    btn.onClick.AddListener(() => OnSlotSelected(slotIndex, data, slot.transform));
                }
                else
                {
                    btn.interactable = false;
                }
            }
        }
        #endregion

        #region ui interaction
        /// <summary>
        /// Updates the primary detail canvas and animates visual elements upon slot selection.
        /// </summary>
        private void OnSlotSelected(int index, SaveData data, Transform slotTransform)
        {
            if (_selectedSlotIndex == index) return;

            _selectedSlotIndex = index;
            _selectedData = data;

            confirmLoadButton.interactable = true;

            AnimateSlotSelection(slotTransform);
            AnimateDetailViewUpdate(data);
        }

        private void AnimateSlotSelection(Transform activeSlot)
        {
            foreach (var slot in saveSlots)
            {
                if (slot.transform == activeSlot)
                {
                    slot.transform.DOScale(1.05f, 0.2f).SetEase(Ease.OutBack);
                }
                else
                {
                    slot.transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
                }
            }
        }

        private void AnimateDetailViewUpdate(SaveData data)
        {
            detailCanvasGroup.DOKill();

            // Dip to zero opacity, update the strings, then fade back up
            detailCanvasGroup.DOFade(0f, 0.15f).OnComplete(() =>
            {
                detailTitleText.text = data.saveName;
                detailLocationText.text = $"Location: {data.rememberedLocationName}";
                detailCompletionText.text = $"Progression: {data.completionPercentage}%";
                detailTimeOldText.text = $"Last Played: {OutTimeUtility.GetTimeAgo(data.lastPlayedTime)}";

                detailCanvasGroup.DOFade(1f, 0.3f).SetEase(Ease.InOutSine);
            });
        }
        #endregion

        #region core execution
        /// <summary>
        /// Finalizes the load operation, updates persistent tracking, and transfers control to the GameManager.
        /// </summary>
        private void LoadSelectedGame()
        {
            if (_selectedData == null) return;

            OutLogger.Log($"[OutLoadGamePanel] Launching Game via Slot {_selectedSlotIndex}");

            PlayerPrefs.SetInt("LastSaveSlot", _selectedSlotIndex);
            PlayerPrefs.Save();

            OutGameManager.Instance.CurrentSaveSlot = _selectedSlotIndex;
            OutGameManager.Instance.IsNewGameSession = false;

            OutSoundManager.Instance.StopMusic(false);

            OutMenuController.MainMenuInstance.HideAllPanelsInstant();
            OutMenuController.MainMenuInstance.gameObject.SetActive(false);

            OutGameManager.Instance.StartLoadingScene(_selectedData.sceneName);
        }

        private void OnSceneLoadingStarted(string sceneName)
        {
            detailCanvasGroup.DOKill();
        }
        #endregion
    }
}