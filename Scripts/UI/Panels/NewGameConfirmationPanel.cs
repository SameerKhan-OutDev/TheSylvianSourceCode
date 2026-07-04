using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutGame
{
    public class NewGameConfirmationPanel : MonoBehaviour
    {
        public TMP_Text confirmationText;

        public Button confirmButton;

        public bool forOverwrite = false;

        public void SetConfirmationText(string message)
        {
            confirmationText.text = message;
        }
    }
}
