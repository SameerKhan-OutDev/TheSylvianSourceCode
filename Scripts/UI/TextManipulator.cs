using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace OutGame
{
    /// <summary>
    /// Handles asynchronous text manipulations and visual styling on UI elements.
    /// Fully adapted for CoreCLR and Unity 6 Awaitable pattern.
    /// </summary>
    public class OutTextManipulator : MonoBehaviour
    {
        #region Editor Fields
        [Header("Targeting Mode")]
        public ManipulationTarget targetMode;

        [Header("Mode: String List")]
        [SerializeField] private TMP_Text text;
        public List<string> manipulations;

        [Header("Mode: Text Component List")]
        public List<TMP_Text> textComponents;
        private List<string> cachedTextComponentStrings = new List<string>();

        [Header("Sequence Settings")]
        public bool onStart;
        public TextManipulationMode manipulationMode;

        [Header("Effect Settings")]
        public TextEffectMode effectMode;
        public float delayBetweenCharacters = 0.05f;
        public float delayBetweenWords = 0.3f;

        [Header("Instability & Timing")]
        [Tooltip("If true, switch delay is randomized between the min/max range. If false, it uses a constant delay (the Min value).")]
        [SerializeField] public bool useUnstableDelay;
        [Tooltip("X is Minimum delay, Y is Maximum delay (in milliseconds).")]
        public Vector2Int switchDelayRangeMs = new Vector2Int(1000, 3000);

        [Tooltip("If true, all text animations will ignore Time.timeScale and run in real-time.")]
        [SerializeField] public bool useUnscaledTime;

        [Header("Visual Style")]
        public TextVisualStyle visualStyle = TextVisualStyle.Constant;
        public bool loopVisualStyle = true;
        [Range(0.1f, 10f)] public float glitchIntensity = 2f;
        #endregion

        #region Internal State
        private TMP_Text currentActiveText;
        private Awaitable _visualStyleAwaitable;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            if (targetMode == ManipulationTarget.TextComponentList)
            {
                foreach (var t in textComponents)
                {
                    if (t != null)
                    {
                        cachedTextComponentStrings.Add(t.text);
                        t.text = "";
                    }
                }
            }
            else
            {
                if (text == null) text = GetComponent<TMP_Text>();
            }

            if (onStart)
            {
                BeginManipulation();
            }
        }
        #endregion

        #region Core Logic
        /// <summary>
        /// Starts the main text manipulation sequence and the parallel visual styling loop.
        /// </summary>
        public void BeginManipulation()
        {
            _ = ManipulateTextAsync();
            _ = ApplyVisualStyleAsync();
        }

        /// <summary>
        /// Custom wait helper that bridges standard delays and unscaled time delays based on inspector settings.
        /// </summary>
        private async Awaitable WaitAsync(float delaySec, System.Threading.CancellationToken token)
        {
            if (useUnscaledTime)
            {
                float timer = 0f;
                while (timer < delaySec)
                {
                    token.ThrowIfCancellationRequested();
                    timer += Time.unscaledDeltaTime;
                    await Awaitable.NextFrameAsync(token);
                }
            }
            else
            {
                await Awaitable.WaitForSecondsAsync(delaySec, token);
            }
        }

        private async Awaitable ManipulateTextAsync()
        {
            if (targetMode == ManipulationTarget.StringList && (manipulations == null || manipulations.Count == 0 || text == null)) return;
            if (targetMode == ManipulationTarget.TextComponentList && (textComponents == null || textComponents.Count == 0)) return;

            int currentIndex = 0;
            int direction = 1;
            int maxCount = targetMode == ManipulationTarget.StringList ? manipulations.Count : textComponents.Count;

            try
            {
                while (!destroyCancellationToken.IsCancellationRequested)
                {
                    string statement = "";

                    if (targetMode == ManipulationTarget.StringList)
                    {
                        statement = manipulations[currentIndex];
                        currentActiveText = text;
                        currentActiveText.text = "";
                    }
                    else
                    {
                        statement = cachedTextComponentStrings[currentIndex];
                        currentActiveText = textComponents[currentIndex];

                        foreach (var t in textComponents)
                        {
                            if (t != null) t.text = "";
                        }
                    }

                    await ApplyTextEffectAsync(statement, currentActiveText);

                    int delayMs = useUnstableDelay ? Random.Range(switchDelayRangeMs.x, switchDelayRangeMs.y) : switchDelayRangeMs.x;

                    // Replace standard wait with our custom WaitAsync
                    await WaitAsync(delayMs / 1000f, destroyCancellationToken);

                    if (manipulationMode == TextManipulationMode.Loop)
                    {
                        currentIndex = (currentIndex + 1) % maxCount;
                    }
                    else if (manipulationMode == TextManipulationMode.PingPong)
                    {
                        currentIndex += direction;
                        if (currentIndex >= maxCount || currentIndex < 0)
                        {
                            direction *= -1;
                            currentIndex += direction * 2;
                            currentIndex = Mathf.Clamp(currentIndex, 0, maxCount - 1);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* Handled gracefully for CoreCLR */ }
        }

        private async Awaitable ApplyTextEffectAsync(string statement, TMP_Text targetComponent)
        {
            if (targetComponent == null) return;

            switch (effectMode)
            {
                case TextEffectMode.Instant:
                    targetComponent.text = statement;
                    break;
                case TextEffectMode.Typewriter:
                    foreach (char c in statement)
                    {
                        if (destroyCancellationToken.IsCancellationRequested) return;
                        targetComponent.text += c;

                        // Replace standard wait
                        await WaitAsync(delayBetweenCharacters, destroyCancellationToken);
                    }
                    break;
                case TextEffectMode.WordByWord:
                    string[] words = statement.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (destroyCancellationToken.IsCancellationRequested) return;
                        targetComponent.text += words[i] + " ";

                        // Replace standard wait
                        await WaitAsync(delayBetweenWords, destroyCancellationToken);
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles the independent visual styling (opacity, colors, glitches) of the current active text component.
        /// </summary>
        private async Awaitable ApplyVisualStyleAsync()
        {
            bool toggleState = false;

            try
            {
                while (!destroyCancellationToken.IsCancellationRequested)
                {
                    if (currentActiveText == null || visualStyle == TextVisualStyle.Constant)
                    {
                        await Awaitable.NextFrameAsync(destroyCancellationToken);
                        continue;
                    }

                    int delayMs = useUnstableDelay ? Random.Range(switchDelayRangeMs.x, switchDelayRangeMs.y) : switchDelayRangeMs.x;
                    float delaySec = delayMs / 1000f;
                    Color baseColor = currentActiveText.color;

                    switch (visualStyle)
                    {
                        case TextVisualStyle.HighLow:
                            toggleState = !toggleState;
                            SetAlpha(currentActiveText, toggleState ? 1f : 0.5f);
                            await WaitAsync(delaySec, destroyCancellationToken);
                            break;

                        case TextVisualStyle.OnOff:
                            toggleState = !toggleState;
                            SetAlpha(currentActiveText, toggleState ? 1f : 0f);
                            await WaitAsync(delaySec, destroyCancellationToken);
                            break;

                        case TextVisualStyle.FadeInOut:
                            float elapsed = 0f;
                            float startAlpha = currentActiveText.color.a;
                            float targetAlpha = toggleState ? 1f : 0f;
                            toggleState = !toggleState;

                            while (elapsed < delaySec && !destroyCancellationToken.IsCancellationRequested)
                            {
                                // Adjust time scale integration for Fade looping
                                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                                SetAlpha(currentActiveText, Mathf.Lerp(startAlpha, targetAlpha, elapsed / delaySec));
                                await Awaitable.NextFrameAsync(destroyCancellationToken);
                            }
                            break;

                        case TextVisualStyle.RandomColor:
                            currentActiveText.color = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
                            await WaitAsync(delaySec, destroyCancellationToken);
                            break;

                        case TextVisualStyle.Glitch:
                            SetAlpha(currentActiveText, Random.Range(0.2f, 1f));
                            currentActiveText.rectTransform.anchoredPosition = new Vector2(
                                Random.Range(-glitchIntensity, glitchIntensity),
                                Random.Range(-glitchIntensity, glitchIntensity));
                            await WaitAsync(delaySec * 0.2f, destroyCancellationToken);
                            break;
                    }

                    if (!loopVisualStyle) break;
                }
            }
            catch (OperationCanceledException) { /* Handled gracefully */ }
        }

        private void SetAlpha(TMP_Text txt, float alpha)
        {
            if (txt == null) return;
            Color c = txt.color;
            c.a = alpha;
            txt.color = c;
        }
        #endregion
    }
}