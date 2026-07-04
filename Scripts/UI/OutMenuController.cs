using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Threading;

namespace OutGame
{
    /// <summary>
    /// Core UI Controller for handling Menu navigation, history tracking, and async transitions.
    /// Fully adapted for Unity 6.5 CoreCLR and Awaitable standards.
    /// </summary>
    public class OutMenuController : MonoBehaviour
    {
        public static OutMenuController MainMenuInstance { get; private set; }
        public static OutMenuController PauseMenuInstance { get; private set; }

        #region Editor Fields
        [Header("Menu Classification")]
        [SerializeField] private MenuType menuType = MenuType.MainMenu;
        [SerializeField] private GameObject startPanel;
        public bool printLogs;

        [Header("UX & Transitions")]
        [SerializeField] private bool autoSelectFirstButton = true;
        [SerializeField, Range(0.05f, 1f)] private float transitionDuration = 0.25f;

        [Header("Menu Tree")]
        [SerializeField] private List<MenuPanel> panels = new();
        #endregion

        #region Internal State
        private readonly Dictionary<GameObject, MenuPanel> _panelMap = new();
        private readonly Stack<GameObject> _history = new();
        private GameObject _currentPanel;

        public bool IsOpen { get; private set; }
        private bool _isTransitioning; // Replaces lockInputDuringTransition
        #endregion

        #region Structs
        [Serializable]
        public class MenuPanel
        {
            public string name;
            public GameObject panel;
            public List<MenuButtonLink> buttons = new();
        }

        [Serializable]
        public class MenuButtonLink
        {
            public string name;
            public Button button;
            public GameObject opensPanel;
            public UnityEvent onClick;

            [Space]
            public bool isStartGameButton;
            [Tooltip("Action to take if isStartGameButton is true.")]
            public StartGameType startGameAction;

            [Tooltip("If starting the game fails (e.g. no saves), trigger this event instead.")]
            public UnityEvent onActionFailed;

            [Space]
            public bool dontPushToHistory;
            public bool isBackButton;
            public bool isHomeButton;
            public bool isExitButton;
            public bool closeMenu;
        }
        #endregion

        #region Events
        public static event Action AButtonClicked;

        /// <summary>
        /// Broadcasts true when the menu opens, false when it closes.
        /// OutGameManager should listen to this to pause time and lock gameplay input.
        /// </summary>
        public static event Action<bool> OnMenuStateChanged;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (menuType == MenuType.MainMenu)
            {
                if (IsDuplicateSingleton(MainMenuInstance)) return;
                MainMenuInstance = this;
            }
            else if (menuType == MenuType.PauseMenu)
            {
                if (IsDuplicateSingleton(PauseMenuInstance)) return;
                PauseMenuInstance = this;
            }

            BuildMap();
            WireButtons();
            HideAllPanelsInstant();
        }

        private void OnEnable()
        {
            // Automatically show if it's the Main Menu.
            if (menuType == MenuType.MainMenu)
            {
                Show(startPanel);
            }
        }

        private void Update()
        {
            if (menuType != MenuType.PauseMenu || _isTransitioning) return;
            if (OutInputManager.Instance == null) return;

            if (OutInputManager.Instance.InputActions.UI.Cancel.WasPressedThisFrame())
            {
                HandlePauseCancel();
            }
        }

        private void SetupSingleton(ref OutMenuController instance)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }
        #endregion

        #region Input & Routing
        private void HandlePauseCancel()
        {
            if (!IsOpen)
            {
                Show(startPanel);
                return;
            }

            if (_currentPanel == startPanel)
            {
                HideMenu();
            }
            else
            {
                Back();
            }
        }
        #endregion

        #region Core Navigation (Awaitable)
        public void Show(GameObject panel)
        {
            if (panel == null || _isTransitioning) return;

            IsOpen = true;
            OnMenuStateChanged?.Invoke(true); // Tell GameManager to pause/lock input

            _history.Clear();
            gameObject.SetActive(true);
            _ = TransitionToPanelAsync(panel, false);
        }

        public void HideMenu()
        {
            if (_isTransitioning) return;

            HideAllPanelsInstant();
            IsOpen = false;
            OnMenuStateChanged?.Invoke(false); // Tell GameManager to unpause/unlock input

            gameObject.SetActive(false);
        }

        public void Open(GameObject panel, bool pushHistory = true)
        {
            if (panel == null || _isTransitioning) return;
            _ = TransitionToPanelAsync(panel, pushHistory);
        }

        public void Back()
        {
            if (_isTransitioning)
            {
                OutLogger.LogError("OutMenuController: Transition in progress. Back action ignored.");
                return;
            }


            if (_currentPanel == startPanel)
            {
                if (menuType == MenuType.MainMenu) return;
                HideMenu();
                return;
            }

            if (_history.Count == 0)
            {
                if (printLogs) OutLogger.LogError("OutMenuController: History stack is empty. Cannot go back.");
                return;
            }

            _ = TransitionToPanelAsync(_history.Pop(), false);
        }

        public void Home()
        {
            if (_isTransitioning) return;
            _history.Clear();
            _ = TransitionToPanelAsync(startPanel, false);
        }
        #endregion

        #region Awaitable Transitions
        /// <summary>
        /// Handles the safe, asynchronous crossfade between UI panels.
        /// Automatically locks inputs to prevent stack corruption.
        /// </summary>
        private async Awaitable TransitionToPanelAsync(GameObject newPanel, bool pushHistory)
        {
            _isTransitioning = true;

            try
            {
                CanvasGroup newCg = GetOrAddCanvasGroup(newPanel);

                // Fade out current panel if it exists
                if (_currentPanel != null)
                {
                    if (pushHistory) _history.Push(_currentPanel);

                    CanvasGroup currentCg = GetOrAddCanvasGroup(_currentPanel);
                    await FadeCanvasGroupAsync(currentCg, 1f, 0f, transitionDuration, destroyCancellationToken);
                    _currentPanel.SetActive(false);
                }

                // Prepare and fade in new panel
                newPanel.SetActive(true);
                newCg.alpha = 0f;
                _currentPanel = newPanel;

                if (autoSelectFirstButton) SelectFirstButton(newPanel);

                await FadeCanvasGroupAsync(newCg, 0f, 1f, transitionDuration, destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful exit if the object is destroyed during a transition (CoreCLR standard)
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async Awaitable FadeCanvasGroupAsync(CanvasGroup cg, float startAlpha, float targetAlpha, float duration, CancellationToken token)
        {
            if (cg == null) return;

            float elapsed = 0f;
            cg.alpha = startAlpha;
            cg.blocksRaycasts = targetAlpha > 0.5f;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime; // Use unscaled time so menus fade even when paused
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                await Awaitable.NextFrameAsync(token);
            }

            cg.alpha = targetAlpha;
        }

        private CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            if (!target.TryGetComponent(out CanvasGroup cg))
            {
                cg = target.AddComponent<CanvasGroup>();
            }
            return cg;
        }
        #endregion

        #region Button Handling
        private void HandleButton(MenuButtonLink link)
        {
            if (_isTransitioning) return; // Prevent double-clicking from corrupting the stack

            AButtonClicked?.Invoke();
            if (printLogs) OutLogger.Log($"Button Clicked: {link.name}");

            link.onClick?.Invoke();

            if (link.isStartGameButton)
            {
                switch (link.startGameAction)
                {
                    case StartGameType.NewGame:
                        OutGameManager.Instance?.StartNewGame();
                        break;
                    case StartGameType.Continue:
                        bool success = OutGameManager.Instance?.ContinueGame() ?? false;
                        if (!success)
                        {
                            OutLogger.LogError("Failed to continue game.");
                            link.onActionFailed?.Invoke();
                        }
                        break;
                }
            }

            if (link.isBackButton)
            {
                Back();
                return;
            }

            if (link.isHomeButton)
            {
                Home();
                return;
            }

            if (link.closeMenu)
            {
                HideMenu();
                return;
            }

            if (link.isExitButton)
            {
                Application.Quit();
            }

            if (link.opensPanel != null)
            {
                Open(link.opensPanel, !link.dontPushToHistory);
            }
        }
        #endregion

        #region Setup Helpers
        private void BuildMap()
        {
            _panelMap.Clear();
            foreach (var p in panels)
            {
                if (p?.panel == null) continue;
                _panelMap[p.panel] = p;
            }

            if (startPanel == null && panels.Count > 0)
                startPanel = panels[0].panel;
        }

        private void WireButtons()
        {
            foreach (var panel in panels)
            {
                foreach (var link in panel.buttons)
                {
                    if (link?.button == null) continue;

                    link.button.onClick.RemoveAllListeners();
                    link.button.onClick.AddListener(() => HandleButton(link));
                }
            }
        }

        /// <summary>
        /// Checks if an instance already exists. If it does, destroys this duplicate and returns true.
        /// </summary>
        private bool IsDuplicateSingleton(OutMenuController currentInstance)
        {
            if (currentInstance != null && currentInstance != this)
            {
                Destroy(gameObject);
                return true;
            }

            DontDestroyOnLoad(gameObject);
            return false;
        }

        public void HideAllPanelsInstant()
        {
            foreach (var p in panels)
            {
                if (p?.panel != null)
                {
                    p.panel.SetActive(false);
                    CanvasGroup cg = GetOrAddCanvasGroup(p.panel);
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                }
            }

            _currentPanel = null;
            _history.Clear();
            _isTransitioning = false;
        }

        private void SelectFirstButton(GameObject panel)
        {
            var btn = panel.GetComponentInChildren<Button>();
            if (btn != null)
                EventSystem.current.SetSelectedGameObject(btn.gameObject);
        }
        #endregion
    }
}