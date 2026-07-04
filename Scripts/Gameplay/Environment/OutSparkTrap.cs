using UnityEngine;
using UnityEngine.Events;

namespace OutGame
{
    [RequireComponent(typeof(BoxCollider))]
    public class OutSparkTrap : MonoBehaviour, ISaveable
    {
        [Header("Trap Settings")]
        [SerializeField] private GameObject _blockingSegmentParticles;
        [SerializeField] private ParticleSystem _attachedVictimParticles;
        [SerializeField] private Transform _attachedTargetPosition;

        [Header("Audio")]
        [SerializeField] private SoundType _trapTriggerSound = SoundType.ShortCircuit; // Add to EnumsJar

        [Header("Events")]
        [Tooltip("Optional UnityEvent to trigger when the trap is activated.")]
        public UnityEvent TriggerTrapActivated;

        private bool _isActive = true;

        #region Interface Implementation for ISaveable
        public string uniqueID; // Assign a unique ID in the Inspector
        public string UniqueID => uniqueID;

        public void PopulateSaveData(SaveData data)
        {
            // If the trap is used (_isActive is false), add its ID to the save data
            if (!_isActive && !data.triggeredTrapIDs.Contains(uniqueID))
            {
                data.triggeredTrapIDs.Add(uniqueID);
            }
        }

        public void RestoreFromSaveData(SaveData data)
        {
            if (data.triggeredTrapIDs.Contains(uniqueID))
            {
                _isActive = false;
                // Visually set it to the "used" state immediately
                if (_blockingSegmentParticles != null) _blockingSegmentParticles.SetActive(false);
                // Optionally spawn a dead enemy corpse here if needed
            }
        }
        #endregion

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;

            // Ensure starting state
            if (_blockingSegmentParticles != null) _blockingSegmentParticles.SetActive(true);
            if (_attachedVictimParticles != null) _attachedVictimParticles.Stop();
            if (_attachedVictimParticles != null) _attachedVictimParticles.gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            OutLogger.Log($"Spark Trap triggered by {other.name}");
            if (!_isActive) return;

            // Check if the object entering has the conductor interface
            IOutConductor conductor = other.GetComponentInParent<IOutConductor>();

            if (conductor != null)
            {
                _isActive = false;

                OutLogger.Log($"Electrocuting {other.name} at position {_attachedTargetPosition.position}");

                // Swap the particle states
                if (_blockingSegmentParticles != null) _blockingSegmentParticles.SetActive(false);

                if (_attachedVictimParticles != null) _attachedVictimParticles.gameObject.SetActive(true);
                if (_attachedVictimParticles != null) _attachedVictimParticles.Play();

                // Play zap sound
                if (OutSoundManager.Instance != null)
                {
                    OutSoundManager.Instance.PlaySFX(_trapTriggerSound, true, transform.position);
                }

                TriggerTrapActivated?.Invoke();

                // Snap and shock the victim
                conductor.OnElectrocuted(_attachedTargetPosition);
            }
        }
    }
}