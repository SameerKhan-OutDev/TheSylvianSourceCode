using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Core system managing health logic, listening to global requests, and driving UI state.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamagable
    {
        #region Inspector Fields
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        #endregion

        #region Internal State
        private float currentHealth;
        private IHealthUI healthUI;
        private bool isDead;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            currentHealth = maxHealth;
        }

        private void OnEnable()
        {
            OutPlayerHealthDispatcher.OnDamageRequested += HandleGlobalDamageRequest;
            OutPlayerHealthDispatcher.OnKillRequested += HandleGlobalKillRequest;
        }

        private void OnDisable()
        {
            OutPlayerHealthDispatcher.OnDamageRequested -= HandleGlobalDamageRequest;
            OutPlayerHealthDispatcher.OnKillRequested -= HandleGlobalKillRequest;
        }
        #endregion

        #region Core Execution Pipeline
        private void HandleGlobalDamageRequest(float rawAmount, EDamageType type)
        {
            if (isDead) return;

            currentHealth -= rawAmount;
            currentHealth = Mathf.Max(0, currentHealth);

            if (healthUI == null)
            {
                healthUI = FindAnyObjectByType<SylvianHealthUI>(FindObjectsInactive.Include);
            }

            healthUI?.ReduceHealth(currentHealth / maxHealth);

            if (currentHealth <= 0)
            {
                HandleGlobalKillRequest(type);
            }
        }

        private void HandleGlobalKillRequest(EDamageType type)
        {
            if (isDead) return;
            isDead = true;

            // Forward the specialized execution type directly to the conductor listening on the same/sub-systems
            OutPlayerHealthDispatcher.RequestKill(type);
        }

        // Keep explicit interface support intact for manual direct tracing if needed
        public void TakeDamage(float amount) => HandleGlobalDamageRequest(amount, EDamageType.Generic);
        public void TakeDamagePercentage(float percentage) => HandleGlobalDamageRequest(maxHealth * (percentage / 100f), EDamageType.Generic);
        #endregion
    }
}