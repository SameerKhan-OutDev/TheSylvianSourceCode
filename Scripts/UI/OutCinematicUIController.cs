using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace OutGame
{
    /// <summary>
    /// Manages the cinematic UI, handling playback controls, bookmark navigation, 
    /// and progress tracking with DOTween animations.
    /// </summary>
    public class OutCinematicUIController : MonoBehaviour
    {
        #region UI Elements
        [Header("UI Elements")]
        [SerializeField] private GameObject controlsContainer;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform progressBarContainer; // The parent container for spawning bookmarks

        [SerializeField] private Button skipButton;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button nextBookmarkButton;
        [SerializeField] private Button prevBookmarkButton;
        #endregion

        #region Sprites & Prefabs
        [Header("Sprites & Prefabs")]
        [SerializeField] private Image playPauseImage; // The Image component on the Play/Pause button
        [SerializeField] private Sprite playSprite;
        [SerializeField] private Sprite pauseSprite;
        [SerializeField] private GameObject bookmarkPrefab; // Assign your bookmark UI prefab here
        #endregion

        #region Private Variables
        private int _currentBookmarkIndex = 0;
        private bool _bookmarksSpawned = false;
        private List<GameObject> _spawnedBookmarks = new List<GameObject>();
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            _currentBookmarkIndex = 0;
            _bookmarksSpawned = false;
            var currentData = OutCinematicManager.Instance.CurrentCinematic;

            if (currentData == null) return;

            // Toggle visibility based on the CinematicData rules
            controlsContainer.SetActive(currentData.showUIControls);
            skipButton.gameObject.SetActive(currentData.isSkippable);

            UpdatePlayPauseSprite();

            skipButton.onClick.AddListener(OnSkipClicked);
            playPauseButton.onClick.AddListener(OnPlayPauseClicked);
            nextBookmarkButton.onClick.AddListener(OnNextBookmarkClicked);
            prevBookmarkButton.onClick.AddListener(OnPrevBookmarkClicked);
        }

        private void OnDisable()
        {
            skipButton.onClick.RemoveAllListeners();
            playPauseButton.onClick.RemoveAllListeners();
            nextBookmarkButton.onClick.RemoveAllListeners();
            prevBookmarkButton.onClick.RemoveAllListeners();

            // Cleanup spawned bookmarks for the next sequence
            foreach (var bookmark in _spawnedBookmarks)
            {
                if (bookmark != null) Destroy(bookmark);
            }
            _spawnedBookmarks.Clear();
        }

        private void Update()
        {
            var vp = OutCinematicManager.Instance.videoPlayer;

            if (vp.isPrepared && vp.length > 0)
            {
                progressFill.fillAmount = (float)(vp.time / vp.length);

                // Wait until the video is prepared to know its length before spawning
                if (!_bookmarksSpawned)
                {
                    SpawnBookmarks((float)vp.length);
                    _bookmarksSpawned = true;
                }
            }
        }
        #endregion

        #region Bookmark Spawning
        /// <summary>
        /// Spawns the bookmark prefabs along the progress bar relative to their timestamps.
        /// </summary>
        /// <param name="videoLength">Total length of the current cinematic in seconds.</param>
        private void SpawnBookmarks(float videoLength)
        {
            var data = OutCinematicManager.Instance.CurrentCinematic;
            if (data == null || data.bookmarks == null || data.bookmarks.Count == 0) return;

            foreach (var bookmarkTime in data.bookmarks)
            {
                // Calculate percentage position
                float normalizedPosition = Mathf.Clamp01(bookmarkTime / videoLength);

                GameObject bookmark = Instantiate(bookmarkPrefab, progressBarContainer);
                RectTransform rt = bookmark.GetComponent<RectTransform>();

                // Uses responsive UI anchoring to place the bookmark accurately regardless of resolution
                rt.anchorMin = new Vector2(normalizedPosition, 0.5f);
                rt.anchorMax = new Vector2(normalizedPosition, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                _spawnedBookmarks.Add(bookmark);
            }
        }
        #endregion

        #region Interaction & Animations
        /// <summary>
        /// Triggers the skip functionality and pushes the button inward.
        /// </summary>
        private void OnSkipClicked()
        {
            skipButton.transform.DOKill(true);
            skipButton.transform.DOPunchScale(Vector3.one * -0.1f, 0.2f, 1, 0.1f);

            OutCinematicManager.Instance.SkipCinematic();
        }

        /// <summary>
        /// Toggles play/pause state, swaps the sprite, and punches the button inward.
        /// </summary>
        private void OnPlayPauseClicked()
        {
            playPauseButton.transform.DOKill(true);
            playPauseButton.transform.DOPunchScale(Vector3.one * -0.1f, 0.2f, 1, 0.1f);

            OutCinematicManager.Instance.TogglePausePlay();
            UpdatePlayPauseSprite();
        }

        private void UpdatePlayPauseSprite()
        {
            var vp = OutCinematicManager.Instance.videoPlayer;
            if (playPauseImage != null)
            {
                playPauseImage.sprite = vp.isPaused ? playSprite : pauseSprite;
            }
        }

        /// <summary>
        /// Jumps to the next bookmark and slightly pushes the button to the right.
        /// </summary>
        private void OnNextBookmarkClicked()
        {
            nextBookmarkButton.transform.DOKill(true);
            nextBookmarkButton.transform.DOPunchPosition(Vector3.right * 10f, 0.2f, 1, 0.1f);

            var data = OutCinematicManager.Instance.CurrentCinematic;
            if (data == null || data.bookmarks.Count == 0) return;

            _currentBookmarkIndex = Mathf.Min(_currentBookmarkIndex + 1, data.bookmarks.Count - 1);
            OutCinematicManager.Instance.JumpToTime(data.bookmarks[_currentBookmarkIndex]);
        }

        /// <summary>
        /// Jumps to the previous bookmark and slightly pushes the button to the left.
        /// </summary>
        private void OnPrevBookmarkClicked()
        {
            prevBookmarkButton.transform.DOKill(true);
            prevBookmarkButton.transform.DOPunchPosition(Vector3.left * 10f, 0.2f, 1, 0.1f);

            var data = OutCinematicManager.Instance.CurrentCinematic;
            if (data == null || data.bookmarks.Count == 0) return;

            _currentBookmarkIndex = Mathf.Max(_currentBookmarkIndex - 1, 0);
            OutCinematicManager.Instance.JumpToTime(data.bookmarks[_currentBookmarkIndex]);
        }
        #endregion
    }
}