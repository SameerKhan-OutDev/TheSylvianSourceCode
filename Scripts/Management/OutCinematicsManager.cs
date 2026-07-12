using UnityEngine;
using UnityEngine.Video;

namespace OutGame
{
    public class OutCinematicManager : MonoBehaviour
    {
        public static OutCinematicManager Instance { get; private set; }

        [Header("References")]
        public VideoPlayer videoPlayer;
        [SerializeField] private GameObject cinematicContainer;

        // Expose the current data so the UI knows what bookmarks to load
        public OutCinematicData CurrentCinematic { get; private set; }

        private bool _isSkipping;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public async Awaitable PlayCinematicAsync(OutCinematicData cinematicData)
        {
            if (cinematicData == null || cinematicData.videoClip == null) return;

            CurrentCinematic = cinematicData;
            _isSkipping = false;

            cinematicContainer.SetActive(true);
            videoPlayer.clip = cinematicData.videoClip;

            // 1. Force preparation in memory
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            // 2. Command it to play
            videoPlayer.Play();

            // 3. THE FIX: Wait for Unity to actually start playing the video.
            // If we don't wait, the next loop skips instantly.
            while (!videoPlayer.isPlaying && !_isSkipping)
            {
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            // 4. Now that it is definitely playing, wait until it naturally stops or is skipped.
            // We removed the frame math due to the H.264 timestamp skewing.
            while (!_isSkipping && videoPlayer.isPlaying)
            {
                if (cinematicData.isSkippable && OutInputManager.Instance.InputActions.UI.Cancel.WasPressedThisFrame())
                {
                    OutLogger.Note("[OutCinematicManager] Skip input detected.");
                    break;
                }

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            // 5. Cleanup
            videoPlayer.Stop();
            cinematicContainer.SetActive(false);
            CurrentCinematic = null;
        }

        public void SkipCinematic()
        {
            if (CurrentCinematic != null && CurrentCinematic.isSkippable)
                _isSkipping = true;
        }

        public void TogglePausePlay()
        {
            if (videoPlayer.isPaused) videoPlayer.Play();
            else videoPlayer.Pause();
        }

        public void JumpToTime(float timeInSeconds)
        {
            timeInSeconds = Mathf.Clamp(timeInSeconds, 0f, (float)videoPlayer.length);
            videoPlayer.time = timeInSeconds;
        }
    }
}