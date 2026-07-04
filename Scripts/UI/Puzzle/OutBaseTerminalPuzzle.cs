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

        protected void CompletePuzzle(bool success)
        {
            puzzleCompletionSource?.TrySetResult(success);
        }

        public void OnEnable()
        {
            OutInputManager.Instance.SetGameplayInput(false);
        }

        public void OnDisable()
        {
            OutInputManager.Instance.SetGameplayInput(true);
        }

        public abstract void AnimateIn();
        public abstract Awaitable AnimateOutAsync();
    }
}