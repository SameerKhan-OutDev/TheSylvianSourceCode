using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace OutGame
{
    [RequireComponent(typeof(Button), typeof(CanvasGroup))]
    public class OutLetterSlot : MonoBehaviour, ISelectHandler, IPointerEnterHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI m_letterText;
        [SerializeField] private GameObject m_highlightVisual;

        private char _currentLetter;
        private OutWordPuzzle _puzzleController;
        private Button _button;

        public int CurrentAnchorIndex { get; set; }

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Setup(char a_initialLetter, OutWordPuzzle a_controller)
        {
            _puzzleController = a_controller;
            _button = GetComponent<Button>();

            SetLetter(a_initialLetter);
            SetHighlight(false);

            // THE UNCLE FIX: Ghost Slot visual logic
            Image bgImage = GetComponent<Image>();
            if (a_initialLetter == ' ')
            {
                // Make it invisible, but keep the button active for the EventSystem
                if (bgImage != null) bgImage.color = new Color(1f, 1f, 1f, 0f);
                if (m_letterText != null) m_letterText.enabled = false;
            }
            else
            {
                // Make sure standard letters are visible
                if (bgImage != null) bgImage.color = new Color(1f, 1f, 1f, 1f);
                if (m_letterText != null) m_letterText.enabled = true;
            }

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(ReportSubmit);
        }

        public char GetLetter() => _currentLetter;

        public void SetLetter(char a_newLetter)
        {
            _currentLetter = char.ToUpper(a_newLetter);

            if (m_letterText != null)
            {
                m_letterText.text = _currentLetter.ToString();
            }
        }

        public void SetHighlight(bool a_isHighlighted)
        {
            if (m_highlightVisual != null)
            {
                m_highlightVisual.SetActive(a_isHighlighted);
            }
        }

        #region Navigation & Highlighting

        public void OnSelect(BaseEventData eventData)
        {
            _puzzleController?.OnSlotHighlighted(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        #endregion

        #region Mouse Drag & Drop Logic

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Don't let them drag an empty ghost space
            if (_currentLetter == ' ') return;

            _canvasGroup.blocksRaycasts = false;
            _puzzleController?.OnDragStarted(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_currentLetter == ' ') return;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector3 globalMousePos))
            {
                _rectTransform.position = globalMousePos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_currentLetter == ' ') return;

            _canvasGroup.blocksRaycasts = true;
            _puzzleController?.OnDragEnded(this);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent(out OutLetterSlot droppedSlot))
            {
                _puzzleController?.SwapSlots(droppedSlot, this);
            }
        }

        #endregion

        private void ReportSubmit()
        {
            if (_puzzleController != null)
            {
                _puzzleController.OnSlotSubmit(this);
            }
        }
    }
}