using TMPro;
using UnityEngine;
using System.Threading;

namespace OutGame
{
    public class GameplayHintsPanel : MonoBehaviour
    {
        [Header("References")]
        public TMP_Text text;
        [Tooltip("The background RectTransform that needs to scale.")]
        public RectTransform backgroundRect;

        [Header("Formatting Settings")]
        [Tooltip("How much extra vertical space to add to the panel above/below the text.")]
        public float verticalPadding = 20f;
        [Tooltip("Time between each character typing out.")]
        public float typingSpeed = 0.02f;

        private CancellationTokenSource _typingCts;

        private void OnEnable()
        {
            // Call your Sound Manager (Update the SoundType enum in EnumsJar if needed)
            if (OutSoundManager.Instance != null)
            {
                OutSoundManager.Instance.PlayUISound(SoundType.ButtonClick, true); // Change to your Hint Sound Enum
            }
        }

        private void OnDisable()
        {
            // Clean up the async task if the panel is forcefully deactivated
            CancelTyping();
        }

        public void DisplayHint(string message)
        {
            gameObject.SetActive(true);
            _ = TypeTextAsync(message);
        }

        public void HideHint()
        {
            CancelTyping();
            gameObject.SetActive(false);
        }

        private void CancelTyping()
        {
            if (_typingCts != null)
            {
                _typingCts.Cancel();
                _typingCts.Dispose();
                _typingCts = null;
            }
        }

        private async Awaitable TypeTextAsync(string message)
        {
            CancelTyping();
            _typingCts = new CancellationTokenSource();
            CancellationToken token = _typingCts.Token;

            // 1. Setup the text but make it entirely invisible
            text.text = message;
            text.maxVisibleCharacters = 0;

            // 2. Force TMP to calculate the layout so we can grab the true height
            text.ForceMeshUpdate();

            // 3. Scale the background panel instantly
            if (backgroundRect != null)
            {
                Vector2 currentSize = backgroundRect.sizeDelta;
                backgroundRect.sizeDelta = new Vector2(currentSize.x, text.preferredHeight + verticalPadding);
            }

            try
            {
                // 4. Typewriter effect
                int totalChars = text.textInfo.characterCount;
                int visibleChars = 0;

                while (visibleChars < totalChars)
                {
                    token.ThrowIfCancellationRequested();

                    visibleChars++;
                    text.maxVisibleCharacters = visibleChars;

                    await Awaitable.WaitForSecondsAsync(typingSpeed, token);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Task was cancelled (panel closed early), exit cleanly.
            }
        }
    }
}