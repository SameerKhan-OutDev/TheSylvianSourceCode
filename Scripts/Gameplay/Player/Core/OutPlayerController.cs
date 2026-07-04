using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace OutGame
{

    public class OutPlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private OutThirdPersonCamera tpCamera;

        [Header("State")]
        public MovementState currentState = MovementState.Idle;

        // C# ACTION: Other scripts will listen to this!
        public event Action<MovementState> OnStateChanged;

        [Header("Input Data (Read-Only)")]
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsSprintHeld { get; private set; }

        #region Events
        // Add this alongside your OnStateChanged event
        public event Action OnWalkTurn180;
        #endregion

        private void Update()
        {
            if (OutInputManager.Instance == null) return;

            // 1. Read Inputs
            MoveInput = OutInputManager.Instance.InputActions.Player.Move.ReadValue<Vector2>();
            LookInput = OutInputManager.Instance.InputActions.Player.Look.ReadValue<Vector2>();
            IsSprintHeld = OutInputManager.Instance.InputActions.Player.Sprint.IsPressed();

            // 2. Route Camera Input
            if (tpCamera != null)
            {
                tpCamera.LookInput = LookInput;
            }
        }


        #region Public Methods
        // Add this method to allow other scripts to trigger the event
        public void TriggerWalkTurn180()
        {
            OnWalkTurn180?.Invoke();
        }

        // The Movement script will call this when it calculates actual speed vs obstacles
        public void ChangeState(MovementState newState)
        {
            if (currentState == newState) return;

            currentState = newState;

            // Broadcast the change to Animator, Audio, etc.
            OnStateChanged?.Invoke(currentState);
        }
        #endregion
    }
}