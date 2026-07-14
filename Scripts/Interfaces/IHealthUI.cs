using UnityEngine;

namespace OutGame
{
    public interface IHealthUI
    {
        UnityEngine.UI.Image healthFrame { get; set; }
        UnityEngine.UI.Image healthFill { get; set; }


        public void ReduceHealth(float amount);
        public void RegenerateHealth();
    }
}
