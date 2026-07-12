using UnityEngine;
using System.Threading.Tasks;

namespace OutGame
{
    public class OutTerminalUIController : MonoBehaviour
    {
        public static OutTerminalUIController Instance;

        [Header("Puzzle Panels")]
        [SerializeField] private OutWordPuzzle wordPuzzlePanel;
        // [SerializeField] private OutAnalogPuzzle analogPuzzlePanel; // For future

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public async Awaitable<bool> OpenPuzzleAsync(OutTerminal terminal)
        {
            OutBaseTerminalPuzzle activePuzzle = null;

            // Route to correct puzzle logic
            if (terminal.puzzleType == OutTerminalPuzzleType.WordPuzzle)
                activePuzzle = wordPuzzlePanel;
            // else if (terminal.puzzleType == OutTerminalPuzzleType.AnalogPuzzle)
            //    activePuzzle = analogPuzzlePanel;

            if (activePuzzle == null)
            {
                OutLogger.Error($"No puzzle panel found for terminal type: {terminal.puzzleType}");
                return false;
            }

            // Create a TaskCompletionSource to halt execution until the UI finishes
            var tcs = new TaskCompletionSource<bool>();

            activePuzzle.gameObject.SetActive(true);
            activePuzzle.InitializePuzzle(terminal, tcs);
            activePuzzle.AnimateIn();

            // The code stops here and waits until CompletePuzzle() is called inside OutWordPuzzle
            bool result = await tcs.Task;

            // Fade it out before returning control
            await activePuzzle.AnimateOutAsync();

            return result;
        }
    }
}