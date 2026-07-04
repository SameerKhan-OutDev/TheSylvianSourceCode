namespace OutGame
{
    /// <summary>
    /// Centralized repository for all string constants used across The Sylvian.
    /// Access these via OutStringConstants.Category.ConstantName (e.g., OutStringConstants.Tags.Player)
    /// </summary>
    public static class OutStringConstants
    {
        // --------------------------------------------------------
        // PUZZLE OBJECTIVES (IDs used in OutPuzzleZone & SaveData)
        // --------------------------------------------------------
        public static class PuzzleObjectives
        {
            public const string RestorePower = "OBJ_Restore_Main_Power";
            public const string OverrideServer = "OBJ_Override_Server_Terminal";
            public const string HackElevator = "OBJ_Hack_Elevator_Panel";
            public const string ClearAfflicted = "OBJ_Eliminate_Afflicted";
            public const string TrapVesper = "OBJ_Trap_Vesper_Agent";
            public const string TrapAfflicted = "OBJ_Trap_Afflicted";
            public const string UnlockDoors = "OBJ_Unlock_Doors";
            public const string SolveTerminalPuzzle = "OBJ_Terminal_Puzzle";
        }

        // --------------------------------------------------------
        // NARRATIVE & MISSIONS (Used in Saves and UI)
        // --------------------------------------------------------
        public static class Narrative
        {
            public const string DefaultMission = "Mission_01";
            public const string DefaultObjective = "Survive.";
            public const string DefaultLocation = "The Server Room";

            // Story Locations
            public const string LocationLattice = "The Lattice";
            public const string LocationSector4 = "Sector 4";

            // Story Objectives
            public const string ObjFindVesper = "Find the Vesper Agent";
            public const string ObjEscapeLattice = "Escape the Quarantine Zone";
        }

        // --------------------------------------------------------
        // SAVE & LOAD SYSTEM
        // --------------------------------------------------------
        public static class SaveSystem
        {
            public const string LastSaveSlotKey = "LastSaveSlot";
            public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
            public const string JsonExtension = "*.json";
            public const string QuickSaveFileName = "quick_save.json";
            public const string SlotSavePrefix = "save_";
        }

        // --------------------------------------------------------
        // UNITY TAGS & LAYERS
        // --------------------------------------------------------
        public static class Tags
        {
            public const string Player = "Player";
            public const string Enemy = "Enemy";
            public const string MainCamera = "MainCamera";
        }

        public static class Layers
        {
            public const string Interactable = "Interactable";
            public const string Enemy = "Enemy";
            public const string Environment = "Environment";
        }

        // --------------------------------------------------------
        // ANIMATOR PARAMETERS
        // --------------------------------------------------------
        public static class AnimatorParams
        {
            public const string Speed = "Speed";
            public const string IsMoving = "IsMoving";
            public const string DoVault = "Vault";
            public const string DoInteract = "Interact";
            public const string TriggerDeath = "Die";
        }

        // --------------------------------------------------------
        // SCENE NAMES (Used by OutGameManager)
        // --------------------------------------------------------
        public static class Scenes
        {
            public const string MainMenu = "MainMenu";
            public const string Prologue = "Level_00_Prologue";
            public const string LatticeFacility = "Level_01_Lattice";
        }
    }
}