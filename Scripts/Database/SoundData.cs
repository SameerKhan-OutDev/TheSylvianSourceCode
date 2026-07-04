using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSoundData", menuName = "Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    public List<SoundEntry> globalSounds;

    /// <summary>
    /// Helper method to find a clip by its type.
    /// </summary>
    public AudioClip GetClip(SoundType type)
    {
        var entry = globalSounds.Find(s => s.soundType == type);
        return entry.clip ? entry.clip : null;
    }
}

public enum SoundType
{
    // --- Original Request ---
    ThemeSong,
    ButtonClick,
    Selection,
    ObjectiveComplete,
    NewObjective,
    MissionComplete,
    MissionFailed,
    Discovery,
    Notification,

    // --- Narrative & Atmosphere (Psychological focus) ---
    ThoughtStart,        // Triggered when a character's thought narration begins
    ThoughtEnd,          // Subtle tail sound when narration ends
    AtmosphericDrone,    // General 2D background tension
    PsychologicalGlitch, // Used during moments of mental instability or "Vant" influence
    MemoryUnlock,        // Specific to the story-driven discovery

    // --- UI & Menu ---
    MenuBack,
    MenuHover,
    SliderTick,
    PauseGame,
    ResumeGame,

    // --- Gameplay Feedback (Non-3D) ---
    PuzzleSolved,
    InvalidAction,      // When a player tries a puzzle logic that doesn't work
    HintRevealed,
    TransitionFade,     // Sound during scene transitions/fades to black
    CollectionItem    ,  // General UI sound for picking up a story item

    // Environment
    FacilityAmbience1,
    FacilityAmbience2,
    ErrorBuzz1,

    // Doors
    FacilityDoorVerification,
    FacilityTwoStepDoor,
    FacilityVerticalDoor,

    // Elevator
    // Elevator Sounds
    ElevatorStart,
    ElevatorMoving,
    ElevatorArrive,
    ElevatorError,

    ShortCircuit,
    ManDie,

    PuzzleZoneMusic1,
    PuzzleZoneMusic2,
    PuzzleZoneMusic3,
    PuzzleZoneMusic4_Loud,
    PuzzleZoneMusic5_Loud2,
}

[Serializable]
public struct SoundEntry
{
    public string name; // Easier to identify in the Inspector
    public SoundType soundType;
    public AudioClip clip;
}