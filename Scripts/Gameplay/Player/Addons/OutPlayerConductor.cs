using UnityEngine;
using System.Threading;

namespace OutGame
{
    /// <summary>
    /// Handles physical/locomotion shutdown, animation sequencing, and ragdoll presentation upon death notifications.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class OutPlayerConductor : MonoBehaviour, IOutConductor
    {
        #region Inspector Fields
        [Header("Component References")]
        [SerializeField] private Animator m_animator;
        [SerializeField] private MxM.MxMAnimator m_mxmAnimator;
        [SerializeField] private OutLocomotionManager m_locomotionManager;

        [Header("MxM Death Visual Profiles")]
        [SerializeField] private MxM.MxMEventDefinition m_electrocutedEvent;
        [SerializeField] private MxM.MxMEventDefinition m_genericDeathEvent;
        #endregion

        #region Internal References
        private CharacterController m_characterController;
        private Rigidbody[] m_boneRigidbodies;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            m_characterController = GetComponent<CharacterController>();

            if (m_locomotionManager == null) m_locomotionManager = GetComponent<OutLocomotionManager>();
            if (m_animator == null) m_animator = GetComponentInChildren<Animator>();
            if (m_mxmAnimator == null) m_mxmAnimator = GetComponentInChildren<MxM.MxMAnimator>();

            InitializeRagdoll();
        }

        private void OnEnable()
        {
            OutPlayerHealthDispatcher.OnKillRequested += ExecuteDeathSequence;
        }

        private void OnDisable()
        {
            OutPlayerHealthDispatcher.OnKillRequested -= ExecuteDeathSequence;
        }
        #endregion

        #region Setup
        private void InitializeRagdoll()
        {
            m_boneRigidbodies = GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in m_boneRigidbodies)
            {
                if (rb.gameObject == this.gameObject) continue;
                rb.isKinematic = true;
            }
        }
        #endregion

        #region Interface Hooks
        public void OnElectrocuted(Transform attachPoint)
        {
            if (attachPoint != null)
            {
                transform.position = attachPoint.position;
                transform.rotation = attachPoint.rotation;
            }

            OutPlayerHealthDispatcher.RequestKill(EDamageType.Electrocution);
        }
        #endregion

        #region Centralized Execution Sequence
        private void ExecuteDeathSequence(EDamageType reason)
        {
            OutLogger.Note($"<color=red>[OutPlayerConductor]</color> Executing player kill profile: {reason}");

            DisablePlayerLocomotion();

            MxM.MxMEventDefinition targetEvent = m_genericDeathEvent;
            string failureString = "SYLVIAN KILLED IN ACTION";

            switch (reason)
            {
                case EDamageType.Electrocution:
                    targetEvent = m_electrocutedEvent;
                    failureString = "Electrocuted by Spark Trap";
                    break;
                case EDamageType.PuzzleFailure:
                    failureString = "Fatal Brain Hemorrhage: Console Firewall Lockdown";
                    break;
                case EDamageType.AfflictedBlast:
                    failureString = "Killed by Neurological Disorient Blast";
                    break;
            }

            if (m_mxmAnimator != null && targetEvent != null)
            {
                m_mxmAnimator.BeginEvent(targetEvent);
            }

            _ = HandleDeathSequenceAsync(failureString);
        }

        private async Awaitable HandleDeathSequenceAsync(string logReason)
        {
            await Awaitable.NextFrameAsync(destroyCancellationToken);

            if (m_mxmAnimator != null)
            {
                while (m_mxmAnimator.IsEventPlaying)
                {
                    if (destroyCancellationToken.IsCancellationRequested) return;
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
            }

            EnableRagdoll();
            await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

            TriggerFailure(logReason);
            DisableEverything();
        }
        #endregion

        #region Physical Mutators
        private void EnableRagdoll()
        {
            if (m_animator != null) m_animator.enabled = false;
            if (m_mxmAnimator != null) m_mxmAnimator.enabled = false;

            foreach (Rigidbody rb in m_boneRigidbodies)
            {
                if (rb.gameObject == this.gameObject) continue;
                rb.isKinematic = false;
                rb.AddTorque(new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), Random.Range(-2f, 2f)), ForceMode.Impulse);
            }
        }

        private void DisablePlayerLocomotion()
        {
            if (OutInputManager.Instance != null) OutInputManager.Instance.SetGameplayInput(false);
            if (m_characterController != null) m_characterController.enabled = false;
            if (m_locomotionManager != null) m_locomotionManager.enabled = false;
        }

        private void TriggerFailure(string logReason)
        {
            if (OutGameManager.Instance != null)
            {
                OutGameManager.Instance.TriggerSylvianFailed(logReason);
            }
        }

        private void DisableEverything()
        {
            foreach (var comp in GetComponents<MonoBehaviour>())
            {
                if (comp != this) comp.enabled = false;
            }
        }
        #endregion
    }
}