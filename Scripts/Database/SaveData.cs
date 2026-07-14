using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    [Header("Session Metadata")]
    public string saveName;
    public string lastPlayedTime;
    public string sceneName;

    [Header("Game Progression")]
    public string currentMission;
    public string currentObjective;

    [Header("Character State")]
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public string rememberedLocationName;

    public int saveSlotIndex;
    public float completionPercentage;

    [Header("World State Data")]
    public List<string> completedSubObjectives = new List<string>();
    public List<string> triggeredTrapIDs = new List<string>();
    public List<string> deadEnemyIDs = new List<string>();
    public List<string> unlockedTerminalIDs = new List<string>();

    [Header("Tutorial States")]
    public bool hasSeenHealthTutorial; // NEW: Tracks the health tutorial panel state
}