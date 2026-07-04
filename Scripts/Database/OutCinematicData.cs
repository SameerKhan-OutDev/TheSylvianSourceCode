using UnityEngine;
using UnityEngine.Video;
using System.Collections.Generic;

namespace OutGame
{
    [CreateAssetMenu(fileName = "NewCinematicData", menuName = "OutGame/Cinematic Data")]
    public class OutCinematicData : ScriptableObject
    {
        [Header("Media")]
        public VideoClip videoClip;

        [Header("Rules")]
        public bool isSkippable = true;
        public bool showUIControls = true;

        [Header("Navigation")]
        [Tooltip("Timestamps in seconds where the player can skip to.")]
        public List<float> bookmarks = new List<float>();
    }
}