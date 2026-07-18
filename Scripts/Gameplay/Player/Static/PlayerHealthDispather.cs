using System;

namespace OutGame
{
    /// <summary>
    /// Global messaging hub for player health events and execution requests.
    /// </summary>
    public static class OutPlayerHealthDispatcher
    {
        public static event Action<float, EDamageType> OnDamageRequested;
        public static event Action<EDamageType> OnKillRequested;

        /// <summary>
        /// Request a deduction in player health from any script safely.
        /// </summary>
        public static void RequestDamage(float rawAmount, EDamageType damageType = EDamageType.Generic)
        {
            OnDamageRequested?.Invoke(rawAmount, damageType);
        }

        /// <summary>
        /// Instantly request the player to enter a specific death execution pipeline.
        /// </summary>
        public static void RequestKill(EDamageType deathReason)
        {
            OnKillRequested?.Invoke(deathReason);
        }
    }
}