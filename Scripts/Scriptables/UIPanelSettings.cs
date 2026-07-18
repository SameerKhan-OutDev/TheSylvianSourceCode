using UnityEngine;

namespace OutGame
{
    [CreateAssetMenu(fileName = "NewUIPanelSettings", menuName = "Audio/UI Panel Settings")]
    public class UIPanelSettings : ScriptableObject
    {
        [Header("Pause Menu Audio")]
        public SoundType pauseMenuTheme = SoundType.pauseMenuTheme1;
        [Tooltip("How long it takes to crossfade game audio and the pause theme.")]
        public float pauseCrossfadeDuration = 1.0f;

        [Header("Failure Panel Audio")]
        public SoundType failureTheme = SoundType.MissionFailed;
        public float failureCrossfadeDuration = 1.5f;
    }
}