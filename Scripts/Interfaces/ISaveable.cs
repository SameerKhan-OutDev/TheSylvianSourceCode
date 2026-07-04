namespace OutGame
{
    public interface ISaveable
    {
        string UniqueID { get; }
        void PopulateSaveData(SaveData data); // Write to the save file
        void RestoreFromSaveData(SaveData data); // Read from the save file
    }
}