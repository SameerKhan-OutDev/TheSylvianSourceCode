using OutGame;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls procedural, low-allocation tactical UI animations for TIA interface elements.
/// </summary>
[RequireComponent(typeof(Image))]
public class OutTacticalButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    #region Serialized Fields
    [Header("Hierarchy Targets")]
    [SerializeField] private RectTransform centerTargetMatrix;
    [SerializeField] private Graphic leftFlankNodes;
    [SerializeField] private Graphic rightFlankAnomaly;

    [Header("Animation Tuning")]
    [SerializeField] private float baseRotationSpeed = 45f;
    [SerializeField] private float hoverRotationSpeedMultiplier = 3f;
    [SerializeField] private float pulseFrequency = 2f;
    #endregion

    #region Private Fields
    private Image _buttonChassis;
    private TacticalButtonState _currentState = TacticalButtonState.Idle;
    private float _currentRotationSpeed;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        _buttonChassis = GetComponent<Image>();
        _currentRotationSpeed = baseRotationSpeed;
    }

    private void Start()
    {
        // Fire long-running asynchronous loops bound to the object's lifetime
        _ = RunTelemetryRotationLoop(destroyCancellationToken);
        _ = RunIdlePulseLoop(destroyCancellationToken);
    }
    #endregion

    #region Animation Loops (Asynchronous)
    /// <summary>
    /// Drives the continuous asymmetric rotation of the center data core.
    /// </summary>
    private async Awaitable RunTelemetryRotationLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (centerTargetMatrix != null)
            {
                centerTargetMatrix.Rotate(Vector3.forward, _currentRotationSpeed * Time.deltaTime);
            }

            await Awaitable.NextFrameAsync(token);
        }
    }

    /// <summary>
    /// Simulates ambient system power states through precise alpha adjustments.
    /// </summary>
    private async Awaitable RunIdlePulseLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_currentState == TacticalButtonState.Idle)
            {
                float pulse = (Mathf.Sin(Time.time * pulseFrequency) + 1f) * 0.5f;

                if (leftFlankNodes != null)
                    leftFlankNodes.color = ModifyAlpha(leftFlankNodes.color, Mathf.Lerp(0.4f, 0.9f, pulse));

                if (rightFlankAnomaly != null)
                    rightFlankAnomaly.color = ModifyAlpha(rightFlankAnomaly.color, Mathf.Lerp(0.2f, 0.6f, pulse));
            }

            await Awaitable.NextFrameAsync(token);
        }
    }

    /// <summary>
    /// Executes a crisp visual flash routine upon user authorization/click.
    /// </summary>
    private async Awaitable ExecuteClickFlashSequence(CancellationToken token)
    {
        if (_buttonChassis == null) return;

        Color originalColor = _buttonChassis.color;
        Color flashColor = new Color(0f, 0.94f, 1f, 1f); // TIA Neon Blue

        // Sudden high-contrast burst
        _buttonChassis.color = flashColor;
        await Awaitable.FixedUpdateAsync(token);

        // Quick calculation blend back to active base
        float elapsed = 0f;
        float duration = 0.15f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _buttonChassis.color = Color.Lerp(flashColor, originalColor, elapsed / duration);
            await Awaitable.NextFrameAsync(token);
        }

        _buttonChassis.color = originalColor;
    }
    #endregion

    #region UI Event Handlers
    public void OnPointerEnter(PointerEventData eventData)
    {
        _currentState = TacticalButtonState.Hover;
        _currentRotationSpeed = baseRotationSpeed * hoverRotationSpeedMultiplier;

        if (leftFlankNodes != null) leftFlankNodes.color = ModifyAlpha(leftFlankNodes.color, 1f);
        if (rightFlankAnomaly != null) rightFlankAnomaly.color = ModifyAlpha(rightFlankAnomaly.color, 0.9f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _currentState = TacticalButtonState.Idle;
        _currentRotationSpeed = baseRotationSpeed;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _currentState = TacticalButtonState.Clicked;
        _ = ExecuteClickFlashSequence(destroyCancellationToken);
    }
    #endregion

    #region Helper Methods
    private Color ModifyAlpha(Color baseColor, float newAlpha)
    {
        baseColor.a = newAlpha;
        return baseColor;
    }
    #endregion
}