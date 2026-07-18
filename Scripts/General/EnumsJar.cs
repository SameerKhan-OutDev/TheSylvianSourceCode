using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Contains all globally used enumerations for the project.
    /// </summary>
    public class EnumsJar : MonoBehaviour
    {
    }

    public enum EDamageType
    {
        Generic,
        PuzzleFailure,
        AfflictedBlast,
        Electrocution,
        Hazard
    }

    public enum DoorType
    {
        Horizontal,
        Vertical,
        HorizontalTwoPart
    }

    /// <summary>
    /// Defines the current operational state of the elevator platform.
    /// </summary>
    public enum ElevatorState
    {
        Idle,
        WaitingForConfirmation,
        Moving
    }

    public enum EOutInteractableState
    {
        Dormant,
        Hovered,
        Interacting,
        Jammed,
        Cooldown
    }

    public enum EOutTelekinesisMode
    {
        Translate,
        Rotate,
        TriggerMechanism
    }

    public enum EOutBillboardMode
    {
        CameraAlignment, // Matches camera rotation (Best for flat UI text)
        LookAtPosition   // Looks directly at the camera lens (Best for floating icons)
    }

    /// <summary>
    /// Defines the visual styling and opacity behavior of the manipulated text.
    /// </summary>
    public enum TextVisualStyle
    {
        Constant,
        HighLow,
        OnOff,
        FadeInOut,
        RandomColor,
        Glitch
    }

    public enum TacticalButtonState
    {
        Idle,
        Hover,
        Clicked
    }

    public enum EAfflictedCurrentState
    {
        Default,
        Commanded_Stop,
        Patrolling,
        Commanded_TargetingFlee, // NEW: Waiting for player to select a location
        Commanded_Flee,
        Commanded_Engage,
        Dead
    }

    public enum OutLocomotionState
    {
        Walk,
        Jog,
        Sprint,
        Jump
    }

    public enum StartGameType
    {
        NewGame,
        Continue
    }

    public enum MenuType
    {
        MainMenu,
        PauseMenu
    }

    public enum OutGameState
    {
        Bootup = 0,
        MainMenu = 1,
        Loading = 2,
        Gameplay = 3,
        Paused = 4,
        Cinematic = 5,
        Result = 6,
    }

    public enum MovementState { Idle, Walking, Sprinting, Automated }

    #region Text
    public enum TextManipulationMode
    {
        PingPong,
        Loop,
        None
    }
    public enum TextEffectMode
    {
        Instant,
        Typewriter,
        WordByWord
    }
    public enum ManipulationTarget
    {
        StringList,
        TextComponentList
    }
    #endregion

}
