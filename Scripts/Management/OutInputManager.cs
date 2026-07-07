using UnityEngine;
using UnityEngine.InputSystem;

public class OutInputManager : MonoBehaviour
{
    public static OutInputManager Instance { get; private set; }

    [SerializeField] bool noParentAllowed;

    private static GameObject _holder;
    private InputSystem_Actions inputActions;

    [SerializeField] bool initializeWithGameplayInput = false;

    public InputSystem_Actions InputActions => inputActions;

    #region delegates

    #endregion

    #region events

    #endregion


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        _holder = new GameObject("OutInputManager");
        Instance = _holder.AddComponent<OutInputManager>();
        DontDestroyOnLoad(_holder);
    }

    private void OnEnable() => inputActions?.Enable();
    private void OnDisable() => inputActions?.Disable();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (noParentAllowed) transform.parent = null;

        InitializeInputActions();
    }

    private void InitializeInputActions()
    {
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        inputActions = new InputSystem_Actions();
        inputActions.Enable();

        SetGameplayInput(initializeWithGameplayInput);
    }

    #region ToggleInputActions
    /// <summary>
    /// Updates the hardware cursor lock state and visibility to match the UI or gameplay context.
    /// </summary>
    /// <param name="isUiActive">If true, unlocks and shows the cursor. If false, locks and hides it.</param>
    public void ToggleCursorState(bool isUiActive)
    {
        if (isUiActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void SetGameplayInput(bool enabled)
    {
        if (inputActions == null)
            return;

        if (enabled)
        {
            inputActions.Player.Enable();
            ToggleCursorState(false); // Hide cursor for gameplay
        }
        else
        {
            inputActions.Player.Disable();
            inputActions.UI.Enable();
            ToggleCursorState(true);  // Free cursor for UI interactions
        }
        OutLogger.Log($"Set Gameplay Input: {enabled}");
    }
    #endregion
}
