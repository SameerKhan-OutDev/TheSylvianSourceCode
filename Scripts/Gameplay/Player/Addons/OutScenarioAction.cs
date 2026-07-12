using UnityEngine;

namespace OutGame
{
    [RequireComponent(typeof(Collider))] // Ensure you have a trigger collider on this!
    public class OutScenarioAction : MonoBehaviour
    {
        [Header("Scenario Settings")]
        [Tooltip("The exact spot the player needs to stand.")]
        [SerializeField] private Transform alignmentMark;

        [Tooltip("The Animator Trigger hash name for the action.")]
        [SerializeField] private string animationTriggerName = "PlayHackTerminal";

        [Tooltip("The actual EXACT name of the animation state in the Animator window.")]
        [SerializeField] private string animationStateName = "HackTerminalState";

        [Header("UI Hint Settings")]
        [Tooltip("The text to display when the player is near. Supports TMP XML tags.")]
        [TextArea]
        [SerializeField] private string hintMessage = "Press [E] to <color=red>Hack</color> Terminal";
        [SerializeField] private string playerTag = "Player";

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                OutUIManager.Instance?.ShowGameplayHint(hintMessage);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                OutUIManager.Instance?.HideGameplayHint();
            }
        }

        public async void TriggerScenario(OutPlayerMovement playerMovement, Animator playerAnimator)
        {
            // Hide the hint panel once the action actually starts
            OutUIManager.Instance?.HideGameplayHint();

            // 1. Force the player to walk to the mark
            await playerMovement.WalkToScenarioMarkAsync(alignmentMark.position, alignmentMark.rotation);

            // 2. Fire the action animation
            playerAnimator.SetLayerWeight(1, 1);
            playerAnimator.Play(animationStateName);

            // 3. Await the Transition (Crucial Step)
            while (!playerAnimator.GetCurrentAnimatorStateInfo(0).IsName(animationStateName))
            {
                if (playerAnimator.GetNextAnimatorStateInfo(0).IsName(animationStateName))
                {
                    break;
                }
                await Awaitable.NextFrameAsync();
            }

            // 4. Await the Animation Completion
            while (playerAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
            {
                await Awaitable.NextFrameAsync();
            }

            // 5. The tactical delay
            await Awaitable.WaitForSecondsAsync(0.1f);

            // 6. Give control back
            OutPlayerController controller = playerMovement.GetComponent<OutPlayerController>();
            if (controller != null)
            {
                controller.ChangeState(MovementState.Idle);
                playerAnimator.SetLayerWeight(1, 0);
            }
            else
            {
                OutLogger.Error("OutScenarioAction: Could not find OutPlayerController to restore state.");
            }
        }
    }
}