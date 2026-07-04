using UnityEngine;

namespace OutGame
{
    public class ObjectInstance : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = false;

        public static ObjectInstance[] Instance { get; private set; }

        [Min(0)] public int instanceID;
        [Tooltip("If true, Instance ID will increment and set up at the end of the registry. \n If false, this object will get destroyed if conflict exists at the curent ID.")]
        public bool incrementIfConflict = true;

        private void Awake()
        {
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            // 1) Ensure the registry exists
            Instance ??= new ObjectInstance[0];

            // 2) Find a valid slot (resolve conflicts)
            int id = Mathf.Max(0, instanceID);

            if (incrementIfConflict)
            {
                // Keep incrementing until we find a free slot
                while (id < Instance.Length && Instance[id] != null && Instance[id] != this)
                {
                    Debug.LogWarning($"ID {id} is taken by {Instance[id].name}. Incrementing for {name}...");
                    id++;
                }
            }
            else
            {
                // If slot exists and taken, kill this object (or choose another policy)
                if (id < Instance.Length && Instance[id] != null && Instance[id] != this)
                {
                    Debug.LogError($"Duplicate ID {id} on {name}. Already used by {Instance[id].name}. Destroying {name}.");
                    Destroy(gameObject);
                    return;
                }
            }

            // 3) Expand the array if needed so [id] exists
            if (id >= Instance.Length)
            {
                int newLen = id + 1;
                var newArr = new ObjectInstance[newLen];
                for (int i = 0; i < Instance.Length; i++)
                    newArr[i] = Instance[i];
                Instance = newArr;
            }

            // 4) Register
            instanceID = id;
            Instance[instanceID] = this;

            // Debug
            Debug.Log($"Registered {name} at Instance[{instanceID}]");
        }

        private void OnDestroy()
        {
            // Clean up registry slot when destroyed
            if (Instance != null &&
                instanceID >= 0 &&
                instanceID < Instance.Length &&
                Instance[instanceID] == this)
            {
                Instance[instanceID] = null;
            }
        }
    }
}
