using UnityEngine;
using UnityEngine.UI;

namespace OutGame
{
    public class OutCinematicUIController : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject controlsContainer; // Parent object for all UI controls
        [SerializeField] private Image progressFill;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button nextBookmarkButton;
        [SerializeField] private Button prevBookmarkButton;

        private int _currentBookmarkIndex = 0;

        private void OnEnable()
        {
            _currentBookmarkIndex = 0;
            var currentData = OutCinematicManager.Instance.CurrentCinematic;

            if (currentData == null) return;

            // Toggle visibility based on the CinematicData rules
            controlsContainer.SetActive(currentData.showUIControls);
            skipButton.gameObject.SetActive(currentData.isSkippable);

            skipButton.onClick.AddListener(OutCinematicManager.Instance.SkipCinematic);
            playPauseButton.onClick.AddListener(OutCinematicManager.Instance.TogglePausePlay);
            nextBookmarkButton.onClick.AddListener(OnNextBookmarkClicked);
            prevBookmarkButton.onClick.AddListener(OnPrevBookmarkClicked);
        }

        private void OnDisable()
        {
            skipButton.onClick.RemoveAllListeners();
            playPauseButton.onClick.RemoveAllListeners();
            nextBookmarkButton.onClick.RemoveAllListeners();
            prevBookmarkButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            var vp = OutCinematicManager.Instance.videoPlayer;

            if (vp.isPrepared && vp.length > 0)
            {
                progressFill.fillAmount = (float)(vp.time / vp.length);
            }
        }

        private void OnNextBookmarkClicked()
        {
            var data = OutCinematicManager.Instance.CurrentCinematic;
            if (data == null || data.bookmarks.Count == 0) return;

            _currentBookmarkIndex = Mathf.Min(_currentBookmarkIndex + 1, data.bookmarks.Count - 1);
            OutCinematicManager.Instance.JumpToTime(data.bookmarks[_currentBookmarkIndex]);
        }

        private void OnPrevBookmarkClicked()
        {
            var data = OutCinematicManager.Instance.CurrentCinematic;
            if (data == null || data.bookmarks.Count == 0) return;

            _currentBookmarkIndex = Mathf.Max(_currentBookmarkIndex - 1, 0);
            OutCinematicManager.Instance.JumpToTime(data.bookmarks[_currentBookmarkIndex]);
        }
    }
}