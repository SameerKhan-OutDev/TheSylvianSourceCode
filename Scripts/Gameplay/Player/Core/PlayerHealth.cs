using UnityEngine;

namespace OutGame
{
    /// <summary>
    /// Manages Sylvian's health, damage intake, and death state.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamagable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;

        private IHealthUI healthUI;

        private void Start()
        {
            currentHealth = maxHealth;
            // Grabs the UI component automatically
            healthUI = FindAnyObjectByType<SylvianHealthUI>();
        }

        /// <summary>
        /// Reduces health by a specific percentage of the max health.
        /// </summary>
        public void TakeDamagePercentage(float percentage)
        {
            float damageAmount = maxHealth * (percentage / 100f);
            TakeDamage(damageAmount);
        }

        public void TakeDamage(float amount)
        {
            currentHealth -= amount;
            currentHealth = Mathf.Max(0, currentHealth);

            // Pass the normalized health value (0.0 to 1.0) to the UI
            healthUI?.ReduceHealth(currentHealth / maxHealth);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (OutGameManager.Instance != null)
            {
                // Triggers the exact failure flow you already established
                OutGameManager.Instance.TriggerSylvianFailed("SYLVIAN KILLED IN ACTION");
            }
        }
    }
}