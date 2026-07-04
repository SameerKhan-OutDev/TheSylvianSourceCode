using TMPro;
using UnityEngine;

namespace OutGame
{
    public class SaveGameSlot : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text completion;
        public TMP_Text locationName;
        public TMP_Text howOld;

        [HideInInspector]
        public bool isLoaded;
    }
}
