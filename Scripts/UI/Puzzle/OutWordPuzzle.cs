using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OutGame
{
    /// <summary>
    /// Handles the interactive word puzzle grid, including drag-drop logic and auto-scrolling.
    /// </summary>
    public class OutWordPuzzle : OutBaseTerminalPuzzle
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup puzzleCanvasGroup;
        [SerializeField] private Transform wordLineContainer;
        [SerializeField] private Transform trashTrayContainer;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button submitButton;

        [Header("Scrolling Configuration")]
        [Tooltip("Assign the ScrollRect that contains the word slots.")]
        [SerializeField] private ScrollRect puzzleScrollRect;
        [SerializeField] private float edgeScrollMargin = 150f;
        [SerializeField] private float edgeScrollSpeed = 1.5f;

        [Header("Custom Grid & Animation")]
        [SerializeField] private List<RectTransform> slotAnchors;
        [SerializeField] private RectTransform highlightCursor;
        [SerializeField] private float swapAnimationDuration = 0.25f;

        [Header("Audio Integration")]
        [SerializeField] private SoundType soundSelect = SoundType.Selection;
        [SerializeField] private SoundType soundSwap = SoundType.MenuHover;
        [SerializeField] private SoundType soundPickUpDrop = SoundType.ButtonClick;
        [SerializeField] private SoundType soundCancel = SoundType.MenuBack;
        [SerializeField] private SoundType soundSuccess = SoundType.PuzzleSolved;
        [SerializeField] private SoundType soundFail = SoundType.InvalidAction;

        private OutLetterSlot[] _gridState;
        [SerializeField] private GameObject letterSlotPrefab;

        private string _targetWord;
        private OutLetterSlot _draggedSlot = null;
        private List<OutLetterSlot> _allSlots = new List<OutLetterSlot>();

        private bool _isSwapping = false;

        private void Update()
        {
            HandleMouseEdgeScrolling();
        }

        public override void InitializePuzzle(OutTerminal terminalSource, TaskCompletionSource<bool> tcs)
        {
            base.InitializePuzzle(terminalSource, tcs);
            _targetWord = terminalSource.targetWord.ToUpper();

            SetupDynamicHint();
            GenerateLetterGrid(terminalSource.targetWord, terminalSource.extraDummyLetters);

            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(CheckSubmission);
        }

        private void SetupDynamicHint()
        {
            hintText.text = $"Use Navigation to move between spaces. Press Submit to pick up a letter.\nNavigate to drag it, press Submit to drop. Verify when done.";
        }

        private void GenerateLetterGrid(string word, int dummyCount)
        {
            _gridState = new OutLetterSlot[slotAnchors.Count];
            _allSlots.Clear();

            List<char> charsToSpawn = word.ToUpper().ToList();
            string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            highlightCursor.SetAsFirstSibling();

            // THE UNCLE FIX: Clamp the dummy count so we never exceed our total anchor count.
            int availableSlotsForDummies = Mathf.Max(0, slotAnchors.Count - charsToSpawn.Count);
            int actualDummyCount = Mathf.Min(dummyCount, availableSlotsForDummies);

            // Add the safe amount of dummy letters
            for (int i = 0; i < actualDummyCount; i++)
            {
                charsToSpawn.Add(alphabet[Random.Range(0, alphabet.Length)]);
            }

            // Fill the remaining empty anchors with blank space characters (' ')
            while (charsToSpawn.Count < slotAnchors.Count)
            {
                charsToSpawn.Add(' ');
            }

            // Shuffle the target locations
            List<int> availableAnchorIndices = Enumerable.Range(0, slotAnchors.Count).OrderBy(x => Random.value).ToList();

            for (int i = 0; i < charsToSpawn.Count; i++)
            {
                int targetAnchor = availableAnchorIndices[i];

                GameObject slotObj = Instantiate(letterSlotPrefab, puzzleScrollRect.content);
                if (slotObj.TryGetComponent(out OutLetterSlot slot))
                {
                    slot.Setup(charsToSpawn[i], this);

                    slot.CurrentAnchorIndex = targetAnchor;
                    _gridState[targetAnchor] = slot;
                    _allSlots.Add(slot);

                    slot.GetComponent<RectTransform>().position = slotAnchors[targetAnchor].position;
                }
            }
        }

        #region Scrolling & Navigation

        /// <summary>
        /// Handles auto-scrolling when the mouse pointer is near the left or right edges of the ScrollRect.
        /// </summary>
        private void HandleMouseEdgeScrolling()
        {
            if (puzzleScrollRect == null || !puzzleScrollRect.gameObject.activeInHierarchy) return;

            Vector2 mousePos = Input.mousePosition;
            RectTransform scrollRectTransform = puzzleScrollRect.GetComponent<RectTransform>();

            // Ensure the pointer is actually inside the UI area before we start moving things around like ghosts.
            if (RectTransformUtility.RectangleContainsScreenPoint(scrollRectTransform, mousePos))
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollRectTransform, mousePos, null, out Vector2 localPoint);

                float width = scrollRectTransform.rect.width;
                float normalizedX = (localPoint.x - scrollRectTransform.rect.xMin) / width;

                float scrollDelta = 0f;

                if (normalizedX < (edgeScrollMargin / width))
                {
                    scrollDelta = -1f; // Scroll Left
                }
                else if (normalizedX > 1f - (edgeScrollMargin / width))
                {
                    scrollDelta = 1f; // Scroll Right
                }

                if (scrollDelta != 0f)
                {
                    float currentPos = puzzleScrollRect.horizontalNormalizedPosition;
                    puzzleScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(currentPos + scrollDelta * Time.deltaTime * edgeScrollSpeed);
                }
            }
        }

        /// <summary>
        /// Snaps the ScrollRect view smoothly so the target slot remains visible when navigating via D-Pad/Keyboard.
        /// </summary>
        private void FocusScrollOnSlot(RectTransform targetSlot)
        {
            if (puzzleScrollRect == null || puzzleScrollRect.content == null) return;

            float contentWidth = puzzleScrollRect.content.rect.width;
            float viewportWidth = puzzleScrollRect.viewport != null ? puzzleScrollRect.viewport.rect.width : puzzleScrollRect.GetComponent<RectTransform>().rect.width;

            // If the grid fits perfectly in the viewport, don't bother doing math.
            if (contentWidth <= viewportWidth) return;

            Vector3 slotLocalPos = puzzleScrollRect.content.InverseTransformPoint(targetSlot.position);

            // Calculate where this slot lives in the grand scheme of the normalized universe (0 to 1)
            float targetX = (slotLocalPos.x - (viewportWidth * 0.5f)) / (contentWidth - viewportWidth);
            float targetNormalized = Mathf.Clamp01(targetX);

            DOTween.To(() => puzzleScrollRect.horizontalNormalizedPosition,
                       x => puzzleScrollRect.horizontalNormalizedPosition = x,
                       targetNormalized,
                       0.2f).SetEase(Ease.OutQuad);
        }

        #endregion

        #region Drag and Drop Logic

        public void OnSlotHighlighted(OutLetterSlot newlyHighlightedSlot)
        {
            if (_isSwapping) return;

            // Make sure the view chases our newly highlighted slot.
            FocusScrollOnSlot(newlyHighlightedSlot.GetComponent<RectTransform>());

            if (_draggedSlot != null && _draggedSlot != newlyHighlightedSlot)
            {
                _isSwapping = true;

                AttemptPlaceSlot(_draggedSlot, newlyHighlightedSlot.CurrentAnchorIndex);

                EventSystem.current.SetSelectedGameObject(_draggedSlot.gameObject);
                DOVirtual.DelayedCall(0.1f, () => _isSwapping = false);
            }
            else
            {
                OutSoundManager.Instance.PlayUISound(soundSelect, true);
                highlightCursor.DOMove(slotAnchors[newlyHighlightedSlot.CurrentAnchorIndex].position, 0.15f).SetEase(Ease.OutBack);
            }
        }

        public void SwapSlots(OutLetterSlot slotA, OutLetterSlot slotB)
        {
            if (slotA == slotB) return;

            OutSoundManager.Instance.PlayUISound(soundSwap, true);

            int indexA = slotA.CurrentAnchorIndex;
            int indexB = slotB.CurrentAnchorIndex;

            _gridState[indexA] = slotB;
            _gridState[indexB] = slotA;

            slotA.CurrentAnchorIndex = indexB;
            slotB.CurrentAnchorIndex = indexA;

            slotA.GetComponent<RectTransform>().DOMove(slotAnchors[indexB].position, swapAnimationDuration).SetEase(Ease.OutQuad);
            slotB.GetComponent<RectTransform>().DOMove(slotAnchors[indexA].position, swapAnimationDuration).SetEase(Ease.OutQuad);

            highlightCursor.DOMove(slotAnchors[indexB].position, swapAnimationDuration).SetEase(Ease.OutQuad);
        }

        public void OnDragStarted(OutLetterSlot slot)
        {
            slot.transform.SetAsLastSibling();

            if (highlightCursor != null)
            {
                highlightCursor.gameObject.SetActive(false);
            }
        }

        public void OnDragEnded(OutLetterSlot draggedSlot)
        {
            if (highlightCursor != null)
            {
                highlightCursor.gameObject.SetActive(true);
            }

            int closestAnchorIndex = GetClosestAnchorIndex(draggedSlot.transform.position);
            AttemptPlaceSlot(draggedSlot, closestAnchorIndex);

            draggedSlot.transform.DOScale(1f, 0.15f);
            if (_draggedSlot == draggedSlot) _draggedSlot = null;
        }

        private int GetClosestAnchorIndex(Vector3 dropPosition)
        {
            int bestIndex = 0;
            float closestDistance = Mathf.Infinity;

            for (int i = 0; i < slotAnchors.Count; i++)
            {
                float dist = Vector3.Distance(dropPosition, slotAnchors[i].position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        public void AttemptPlaceSlot(OutLetterSlot movingSlot, int targetIndex)
        {
            int originalIndex = movingSlot.CurrentAnchorIndex;
            OutLetterSlot occupant = _gridState[targetIndex];

            // Because the board is entirely full of slots (some are just invisible ghosts), 
            // occupant will NEVER be null now. We always swap.
            if (occupant != null && occupant != movingSlot)
            {
                OutSoundManager.Instance.PlayUISound(soundSwap, true);

                _gridState[originalIndex] = occupant;
                _gridState[targetIndex] = movingSlot;

                occupant.CurrentAnchorIndex = originalIndex;
                movingSlot.CurrentAnchorIndex = targetIndex;

                occupant.GetComponent<RectTransform>().DOMove(slotAnchors[originalIndex].position, swapAnimationDuration).SetEase(Ease.OutQuad);
            }

            movingSlot.GetComponent<RectTransform>().DOMove(slotAnchors[targetIndex].position, swapAnimationDuration).SetEase(Ease.OutBounce);
            highlightCursor.DOMove(slotAnchors[targetIndex].position, swapAnimationDuration).SetEase(Ease.OutQuad);
        }

        public void OnSlotSubmit(OutLetterSlot clickedSlot)
        {
            // Don't let them pick up an invisible empty ghost slot
            if (clickedSlot.GetLetter() == ' ' && _draggedSlot == null) return;

            OutSoundManager.Instance.PlayUISound(soundPickUpDrop, true);

            if (_draggedSlot == null)
            {
                _draggedSlot = clickedSlot;
                highlightCursor.DOScale(1.2f, 0.15f);
                clickedSlot.transform.SetAsLastSibling();
            }
            else if (_draggedSlot == clickedSlot)
            {
                _draggedSlot = null;
                highlightCursor.DOScale(1.0f, 0.15f);
            }
        }

        #endregion

        private void CheckSubmission()
        {
            string fullGridString = "";
            for (int i = 0; i < _gridState.Length; i++)
            {
                fullGridString += _gridState[i].GetLetter();
            }

            if (fullGridString.Contains(_targetWord))
            {
                OutSoundManager.Instance.PlayUISound(soundSuccess, true);
                OutLogger.Note("[WordPuzzle] Correct!");
                CompletePuzzle(true);
            }
            else
            {
                OutSoundManager.Instance.PlayUISound(soundFail, true);
                OutLogger.Note("[WordPuzzle] Incorrect Word. Try again.");
                puzzleCanvasGroup.transform.DOShakePosition(0.5f, 5f);

                // ---> NEW DAMAGE LOGIC <---
                PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
                if (playerHealth != null)
                {
                    // Reduces health by exactly 30% of max health
                    playerHealth.TakeDamagePercentage(30f);
                }
            }
        }

        public override void AnimateIn()
        {
            gameObject.SetActive(true);
            puzzleCanvasGroup.alpha = 0f;
            puzzleCanvasGroup.DOFade(1f, 0.4f).SetEase(Ease.OutQuad).OnComplete(() => {
                if (_allSlots.Count > 0)
                {
                    EventSystem.current.SetSelectedGameObject(_allSlots[0].gameObject);
                }
            });
        }

        public override async Awaitable AnimateOutAsync()
        {
            Sequence seq = DOTween.Sequence();

            foreach (var slot in _allSlots)
            {
                seq.Insert(0, slot.GetComponent<CanvasGroup>().DOFade(0f, 0.2f));
            }

            seq.Append(puzzleCanvasGroup.DOFade(0f, 0.3f));

            await seq.AsyncWaitForCompletion();
            gameObject.SetActive(false);
        }

        public void CancelPuzzle()
        {
            OutSoundManager.Instance.PlayUISound(soundCancel, true);
            OutLogger.Note("Puzzle Cancelled by User.");
            CompletePuzzle(false);
        }
    }
}