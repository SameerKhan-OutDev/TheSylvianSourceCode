using System;
using System.Collections.Generic;
using System.Threading.Tasks; // Required for Awaitable/Task based fading
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OutGame
{
    public class OutSoundManager : MonoBehaviour
    {
        #region Fields & Properties
        public static OutSoundManager Instance { get; private set; }

        [Header("Data References")]
        [SerializeField] private SoundData soundData;
        [SerializeField] private UIPanelSettings uiPanelSettings; // The new ScriptableObject

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource1;
        [SerializeField] private AudioSource musicSource2;
        [SerializeField] private AudioSource uiSoundSource;
        [SerializeField] private AudioSource pauseMusicSource; // Dedicated source for pause/fail themes
        [SerializeField] private List<AudioSource> sfxSources;

        [Header("Settings")]
        [Tooltip("Default time in seconds for sounds to fade in/out.")]
        [SerializeField] private float defaultFadeDuration = 1.0f;

        private AudioSource _activeMusicSource;

        // Memory for paused audio states
        private Dictionary<AudioSource, float> _pausedSources = new Dictionary<AudioSource, float>();
        #endregion

        #region Lifecycle
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                _activeMusicSource = musicSource1;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            OutMenuController.AButtonClicked += PlayOnClickSound;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            OutMenuController.AButtonClicked -= PlayOnClickSound;
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                PlayMusic(SoundType.ThemeSong, snap: false);
            }
        }
        #endregion

        #region Scene Logic
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                PlayMusic(SoundType.ThemeSong, snap: false);
            }
        }
        #endregion

        #region Public Play Methods
        /// <summary>
        /// Plays UI sounds. Snap false will fade the UI source volume up.
        /// </summary>
        public void PlayUISound(SoundType type, bool snap)
        {
            AudioClip clip = soundData.GetClip(type);
            if (clip == null) return;

            if (snap)
            {
                uiSoundSource.volume = 1f;
                uiSoundSource.PlayOneShot(clip);
            }
            else
            {
                _ = FadeVolumeAsync(uiSoundSource, 0f, 1f, defaultFadeDuration, clip);
            }
        }

        public void PlaySFX(SoundType type, bool snap)
        {
            InternalPlaySFX(type, snap, null);
        }

        public void PlaySFX(SoundType type, bool snap, Vector3 position)
        {
            InternalPlaySFX(type, snap, position);
        }

        #region Private Implementations
        private void InternalPlaySFX(SoundType type, bool snap, Vector3? position)
        {
            AudioClip clip = soundData.GetClip(type);
            if (clip == null) return;

            AudioSource availableSource = GetAvailableSFXSource();

            if (availableSource != null)
            {
                availableSource.spatialBlend = position.HasValue ? 1.0f : 0.0f;
                if (position.HasValue) availableSource.transform.position = position.Value;

                if (snap)
                {
                    availableSource.volume = 1f;
                    availableSource.clip = clip;
                    availableSource.Play();
                }
                else
                {
                    _ = FadeVolumeAsync(availableSource, 0f, 1f, defaultFadeDuration, clip);
                }
            }
        }
        #endregion

        /// <summary>
        /// Manages background music with optional crossfading.
        /// </summary>
        public void PlayMusic(SoundType type, bool snap)
        {
            AudioClip clip = soundData.GetClip(type);
            if (clip == null) return;

            if (snap)
            {
                _activeMusicSource.Stop();
                _activeMusicSource.clip = clip;
                _activeMusicSource.volume = 1f;
                _activeMusicSource.Play();
            }
            else
            {
                _ = FadeOutAndInMusic(clip);
            }
        }
        #endregion

        #region Public Stop Methods
        public void StopSound(SoundType type, bool snap)
        {
            AudioClip clip = soundData.GetClip(type);
            if (clip == null) return;

            // 1. Check all SFX sources
            foreach (var source in sfxSources)
            {
                if (source.isPlaying && source.clip == clip)
                {
                    if (snap)
                        source.Stop();
                    else
                        _ = FadeVolumeAsync(source, source.volume, 0f, defaultFadeDuration, stopOnComplete: true);
                }
            }

            // 2. Check UI sound source
            if (uiSoundSource != null && uiSoundSource.isPlaying && uiSoundSource.clip == clip)
            {
                if (snap)
                    uiSoundSource.Stop();
                else
                    _ = FadeVolumeAsync(uiSoundSource, uiSoundSource.volume, 0f, defaultFadeDuration, stopOnComplete: true);
            }

            // 3. Check the new Pause Music source
            if (pauseMusicSource != null && pauseMusicSource.isPlaying && pauseMusicSource.clip == clip)
            {
                if (snap)
                    pauseMusicSource.Stop();
                else
                    _ = FadeVolumeAsync(pauseMusicSource, pauseMusicSource.volume, 0f, defaultFadeDuration, stopOnComplete: true);
            }
        }

        /// <summary>
        /// Stops the currently active music. Snap false will fade the music out.
        /// </summary>
        public void StopMusic(bool snap)
        {
            if (_activeMusicSource == null || !_activeMusicSource.isPlaying) return;

            if (snap) _activeMusicSource.Stop();
            else _ = FadeVolumeAsync(_activeMusicSource, _activeMusicSource.volume, 0f, defaultFadeDuration, stopOnComplete: true);
        }
        #endregion

        #region Pause & Resume Audio Logic (Crossfading)

        /// <summary>
        /// Fades out all currently playing game audio, pauses them, and fades in the Pause Theme.
        /// </summary>
        public async void PauseGameplayAudio()
        {
            if (uiPanelSettings == null) return;

            float fadeTime = uiPanelSettings.pauseCrossfadeDuration;
            _pausedSources.Clear();

            // Find EVERY audio source in the scene
            AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var source in allSources)
            {
                // Never pause the UI sound source or our dedicated Pause music source
                if (source == uiSoundSource || source == pauseMusicSource) continue;

                if (source.isPlaying)
                {
                    _pausedSources.Add(source, source.volume);
                    // Fade it to 0 without stopping it
                    _ = FadeVolumeAsync(source, source.volume, 0f, fadeTime);
                }
            }

            // Wait for the fade out to finish
            await Awaitable.WaitForSecondsAsync(fadeTime); // Uses scaled time, but audio fade below uses unscaled

            // Physically pause them so they don't lose their place in the track
            foreach (var kvp in _pausedSources)
            {
                if (kvp.Key != null) kvp.Key.Pause();
            }

            // Play the Pause Theme
            AudioClip pauseClip = soundData.GetClip(uiPanelSettings.pauseMenuTheme);
            if (pauseClip != null)
            {
                pauseMusicSource.clip = pauseClip;
                pauseMusicSource.loop = true;
                _ = FadeVolumeAsync(pauseMusicSource, 0f, 1f, fadeTime, pauseClip);
            }
        }

        public async void FailGameplayAudio()
        {
            if (uiPanelSettings == null) return;

            float fadeTime = uiPanelSettings.failureCrossfadeDuration;

            // Stop the active music
            if (_activeMusicSource != null)
                _ = FadeVolumeAsync(_activeMusicSource, _activeMusicSource.volume, 0f, fadeTime, stopOnComplete: true);

            // Play the Failure Theme
            AudioClip failClip = soundData.GetClip(uiPanelSettings.failureTheme);
            if (failClip != null)
            {
                pauseMusicSource.clip = failClip;
                pauseMusicSource.loop = false; // Usually don't loop fail sounds
                _ = FadeVolumeAsync(pauseMusicSource, 0f, 1f, fadeTime, failClip);
            }
        }

        /// <summary>
        /// Fades out the Pause Theme, unpauses all previously playing game audio, and fades them back to their original volumes.
        /// </summary>
        public void ResumeGameplayAudio()
        {
            if (uiPanelSettings == null) return;
            float fadeTime = uiPanelSettings.pauseCrossfadeDuration;

            // Fade out and stop the pause menu theme
            _ = FadeVolumeAsync(pauseMusicSource, pauseMusicSource.volume, 0f, fadeTime, stopOnComplete: true);

            // Unpause and fade in all saved sources
            foreach (var kvp in _pausedSources)
            {
                AudioSource source = kvp.Key;
                float originalVolume = kvp.Value;

                if (source != null)
                {
                    source.UnPause();
                    _ = FadeVolumeAsync(source, 0f, originalVolume, fadeTime);
                }
            }

            _pausedSources.Clear();
        }

        #endregion

        #region Helpers & Fading logic
        private AudioSource GetAvailableSFXSource()
        {
            foreach (var source in sfxSources)
            {
                if (!source.isPlaying) return source;
            }
            Debug.LogWarning("OutSoundManager: All SFX sources are busy.");
            return null;
        }

        public void PlayOnClickSound()
        {
            PlayUISound(SoundType.ButtonClick, snap: true);
        }

        /// <summary>
        /// Universal fader using unscaledDeltaTime (Works while Time.timeScale = 0).
        /// </summary>
        private async Awaitable FadeVolumeAsync(AudioSource source, float startVol, float endVol, float duration, AudioClip clipToPlay = null, bool stopOnComplete = false)
        {
            if (source == null) return;

            if (clipToPlay != null)
            {
                source.clip = clipToPlay;
                source.Play();
            }

            source.volume = startVol;
            float currentTime = 0;

            while (currentTime < duration)
            {
                if (source == null) break;

                // CRITICAL: UnscaledDeltaTime allows the fade to happen even when the game is paused
                currentTime += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVol, endVol, currentTime / duration);

                await Awaitable.NextFrameAsync();
            }

            if (source != null)
            {
                source.volume = endVol;
                if (stopOnComplete) source.Stop();
            }
        }

        private async Awaitable FadeOutAndInMusic(AudioClip newClip)
        {
            await FadeVolumeAsync(_activeMusicSource, _activeMusicSource.volume, 0f, defaultFadeDuration, stopOnComplete: true);

            _activeMusicSource.clip = newClip;

            await FadeVolumeAsync(_activeMusicSource, 0f, 1f, defaultFadeDuration, newClip);
        }
        #endregion
    }
}