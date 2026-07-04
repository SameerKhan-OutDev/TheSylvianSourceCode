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

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource1;
        [SerializeField] private AudioSource musicSource2;
        [SerializeField] private AudioSource uiSoundSource;
        [SerializeField] private List<AudioSource> sfxSources;

        [Header("Settings")]
        [Tooltip("Time in seconds for sounds to fade in/out when snap is false.")]
        [SerializeField] private float fadeDuration = 1.0f;

        private AudioSource _activeMusicSource;
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
                _ = FadeInAndPlay(uiSoundSource, clip);
            }
        }

        /// <summary>
        /// Finds an available SFX source and plays the clip.
        /// </summary>
        public void PlaySFX(SoundType type, bool snap)
        {
            AudioClip clip = soundData.GetClip(type);
            if (clip == null) return;

            AudioSource availableSource = GetAvailableSFXSource();

            if (availableSource != null)
            {
                if (snap)
                {
                    availableSource.volume = 1f;
                    availableSource.PlayOneShot(clip);
                }
                else
                {
                    _ = FadeInAndPlay(availableSource, clip);
                }
            }
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
                // 1. Position and Spatial Blend Handling
                if (position.HasValue)
                {
                    availableSource.transform.position = position.Value;
                    availableSource.spatialBlend = 1.0f; // Force 3D sound
                }
                else
                {
                    availableSource.spatialBlend = 0.0f; // Force 2D sound
                }

                // 2. Playback Logic
                if (snap)
                {
                    availableSource.volume = 1f;
                    // Note: PlayOneShot is great for 2D, but for 3D positioning 
                    // during a fade, we use .Play() so the source 'owns' the clip.
                    availableSource.clip = clip;
                    availableSource.Play();
                }
                else
                {
                    _ = FadeInAndPlay(availableSource, clip);
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

            foreach (var source in sfxSources)
            {
                if (source.isPlaying && source.clip == clip)
                {
                    if (snap) source.Stop();
                    else _ = FadeOutAndStop(source);
                }
            }

            if (uiSoundSource.isPlaying && uiSoundSource.clip == clip)
            {
                if (snap) uiSoundSource.Stop();
                else _ = FadeOutAndStop(uiSoundSource);
            }
        }

        /// <summary>
        /// Stops the currently active music. Snap false will fade the music out.
        /// </summary>
        public void StopMusic(bool snap)
        {
            if (_activeMusicSource == null || !_activeMusicSource.isPlaying) return;

            if (snap)
            {
                _activeMusicSource.Stop();
            }
            else
            {
                // Reuses your existing private Awaitable logic
                _ = FadeOutAndStop(_activeMusicSource);
            }
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

        private async Awaitable FadeInAndPlay(AudioSource source, AudioClip clip)
        {
            source.clip = clip;
            source.volume = 0;
            source.Play();

            float currentTime = 0;
            while (currentTime < fadeDuration)
            {
                currentTime += Time.deltaTime;
                source.volume = Mathf.Lerp(0, 1, currentTime / fadeDuration);
                await Awaitable.NextFrameAsync();
            }
            source.volume = 1f;
        }

        private async Awaitable FadeOutAndStop(AudioSource source)
        {
            float startVolume = source.volume;
            float currentTime = 0;

            while (currentTime < fadeDuration)
            {
                currentTime += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0, currentTime / fadeDuration);
                await Awaitable.NextFrameAsync();
            }

            source.Stop();
            source.volume = startVolume;
        }

        private async Awaitable FadeOutAndInMusic(AudioClip newClip)
        {
            // Simple fade out current music, then fade in new music
            await FadeOutAndStop(_activeMusicSource);
            _activeMusicSource.clip = newClip;
            await FadeInAndPlay(_activeMusicSource, newClip);
        }
        #endregion
    }
}