using UnityEngine;

namespace OutGame
{
    public interface IDamagable
    {
        void TakeDamage(float amount);
        void TakeDamagePercentage(float percentage);
    }
}
