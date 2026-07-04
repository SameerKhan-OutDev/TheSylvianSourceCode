using UnityEngine;

namespace OutGame
{
    [RequireComponent(typeof(BoxCollider))]
    public class OutCinematicTrigger : MonoBehaviour
    {
        [Header("Cinematic Settings")]
        [SerializeField] private OutCinematicData cinematicToPlay;
        [SerializeField] private bool triggerOnlyOnce = true;

        [Tooltip("[Can be left empty] The position where the player will be teleported to for the post-cutscene preparation.")]
        [SerializeField] Transform playerPosition;

        [Header("Autosave Integration")]
        [Tooltip("If true, the game will automatically save the moment the cinematic finishes or is skipped.")]
        [SerializeField] private bool autosaveOnComplete = false;

        [Tooltip("The narrative data to inject into the save file.")]
        [SerializeField] private string narrativeMissionName = "Mission_01";
        [SerializeField] private string narrativeObjective = "Survive.";
        [SerializeField] private string narrativeLocationMemory = "The Server Room";

        private bool _hasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered && triggerOnlyOnce) return;

            if (other.CompareTag("Player"))
            {
                _hasTriggered = true;

                // Fire and forget the async sequence instead of just the cutscene
                _ = PlayCinematicAndSaveAsync();
            }
        }

        private async Awaitable PlayCinematicAndSaveAsync()
        {
            // 1. Await the Director. This naturally pauses here until the video completely finishes or is skipped.
            await OutGameSceneDirector.Instance.StartCutscene(cinematicToPlay, playerPosition != null ? playerPosition : null);

            // 2. The cutscene is over and gameplay has resumed. Check if we need to save.
            if (autosaveOnComplete && OutSaveController.Instance != null)
            {
                // Use the teleport position if provided, otherwise grab the player's current physical transform
                Transform savePosition = playerPosition != null ? playerPosition : OutGameSceneDirector.Instance.playerTransform;

                OutLogger.Log($"<color=cyan>[CinematicTrigger]</color> Cinematic finished. Autosaving at {narrativeLocationMemory}...");

                // 3. Trigger the Autosave!
                OutSaveController.Instance.SaveGame(
                    narrativeMissionName,
                    narrativeObjective,
                    savePosition,
                    narrativeLocationMemory
                );
            }
        }
    }
}