using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Threading.Tasks;

namespace OutGame
{
    public class SylvianHealthUI : MonoBehaviour, IHealthUI, ISaveable
    {
        #region Inspector Fields
        [Header("UI References")]
        [SerializeField] private Image m_healthFrame;
        [SerializeField] private Image m_healthFill;

        [Header("Tutorial")]
        [SerializeField] private TutorialPanel m_healthTutorialPanel;
        #endregion

        #region Internal State
        private bool m_hasSeenTutorial;
        #endregion

        #region Properties
        public Image healthFrame
        {
            get => m_healthFrame;
            set => m_healthFrame = value;
        }

        public Image healthFill
        {
            get => m_healthFill;
            set => m_healthFill = value;
        }

        // ISaveable Requirement 1: Unique string identifier for this specific component
        public string UniqueID => "Sylvian_Health_UI";
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            CheckAndDisplayTutorial();
        }
        #endregion

        #region Core Logic
        private void CheckAndDisplayTutorial()
        {
            // Query the save system directly in case RestoreFromSaveData hasn't fired yet
            if (OutSaveController.Instance != null)
            {
                SaveData latestData = OutSaveController.Instance.GetLatestSaveData(out _);
                if (latestData != null)
                {
                    m_hasSeenTutorial = latestData.hasSeenHealthTutorial;
                }
            }

            if (!m_hasSeenTutorial && m_healthTutorialPanel != null)
            {
                m_healthTutorialPanel.gameObject.SetActive(true);

                // Mark as seen so it doesn't trigger again in the same session before a save occurs
                m_hasSeenTutorial = true;
                _ = WaitNDeactivateTutorial();
            }
        }

        async Awaitable WaitNDeactivateTutorial()
        {
            if (m_healthTutorialPanel != null)
            {
                await Task.Delay(5000); // Wait for 3 seconds
                m_healthTutorialPanel.gameObject.SetActive(false);
            }
        }
        #endregion

        #region IHealthUI Implementation
        public void ReduceHealth(float normalizedHealthAmount)
        {
            if (healthFill != null)
            {
                healthFill.DOFillAmount(normalizedHealthAmount, 0.3f).SetEase(Ease.OutQuad);
            }
        }

        public void RegenerateHealth()
        {
            if (healthFill != null)
            {
                healthFill.DOFillAmount(1f, 0.5f).SetEase(Ease.OutQuad);
            }
        }
        #endregion

        #region ISaveable Implementation
        // ISaveable Requirement 2: Write to the save file
        public void PopulateSaveData(SaveData data)
        {
            if (data != null)
            {
                data.hasSeenHealthTutorial = m_hasSeenTutorial;
            }
        }

        // ISaveable Requirement 3: Read from the save file
        public void RestoreFromSaveData(SaveData data)
        {
            if (data != null)
            {
                m_hasSeenTutorial = data.hasSeenHealthTutorial;
            }
        }
        #endregion
    }
}