using UnityEngine;

namespace OutGame
{
    public class GameplayUI : MonoBehaviour
    {
        public GameplayHintsPanel gameplayHintsPanel;

        private void Start()
        {
            if (gameplayHintsPanel != null)
            {
                gameplayHintsPanel.gameObject.SetActive(false);
            }
        }
    }
}
