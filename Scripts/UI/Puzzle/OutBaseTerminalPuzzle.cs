using UnityEngine;
using System.Threading.Tasks;

namespace OutGame
{
    public abstract class OutBaseTerminalPuzzle : MonoBehaviour
    {
        // This completion source lets us "await" the puzzle in OutTerminal
        protected TaskCompletionSource<bool> puzzleCompletionSource;

        public virtual void InitializePuzzle(OutTerminal terminalSource, TaskCompletionSource<bool> tcs)
        {
            puzzleCompletionSource = tcs;
        }
        public virtual void OnEnable()
        {
            OutInputManager.Instance.SetGameplayInput(false);
            OutPlayerHealthDispatcher.OnKillRequested += HandlePlayerDeath;
        }

        public virtual void OnDisable()
        {
            OutInputManager.Instance.SetGameplayInput(true);
            OutPlayerHealthDispatcher.OnKillRequested -= HandlePlayerDeath;
        }

        private void HandlePlayerDeath(EDamageType deathReason)
        {
            ForceInstantShutdown();
        }

        /// <summary>
        /// Instantly shuts down the puzzle UI without animations to clear the screen for death sequences.
        /// </summary>
        protected virtual void ForceInstantShutdown()
        {
            gameObject.SetActive(false);
        }

        protected void CompletePuzzle(bool success)
        {
            puzzleCompletionSource?.TrySetResult(success);
        }

        public abstract void AnimateIn();
        public abstract Awaitable AnimateOutAsync();
    }
}