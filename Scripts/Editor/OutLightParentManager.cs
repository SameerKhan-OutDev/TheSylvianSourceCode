using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window to manage the parent GameObjects of non-directional lights.
/// Allows filtering by keyword and toggling static flags for entire hierarchies.
/// </summary>
public class OutLightParentManager : EditorWindow
{
    #region Variables
    private string _exclusionKeywords = "";
    private List<GameObject> _lightParents = new List<GameObject>();
    private bool _targetStaticState = true;
    private Vector2 _scrollPosition;
    #endregion

    #region Window Setup
    [MenuItem("Tools/Light Parent Manager")]
    public static void ShowWindow()
    {
        GetWindow<OutLightParentManager>("Light Parents");
    }
    #endregion

    #region GUI
    private void OnGUI()
    {
        GUILayout.Label("Filters", EditorStyles.boldLabel);
        _exclusionKeywords = EditorGUILayout.TextField(
            new GUIContent("Exclusion Keywords", "Comma-separated keywords. Parents containing any of these will be ignored."),
            _exclusionKeywords
        );

        EditorGUILayout.Space();

        if (GUILayout.Button("List Light Parents"))
        {
            FetchLightParents();
        }

        EditorGUILayout.Space();

        DrawLightParentsList();

        EditorGUILayout.Space();

        GUILayout.Label("Modify State", EditorStyles.boldLabel);
        _targetStaticState = EditorGUILayout.Toggle("Target Static State", _targetStaticState);

        EditorGUI.BeginDisabledGroup(_lightParents.Count == 0);
        if (GUILayout.Button("Apply Static State to Listed Hierarchies"))
        {
            ApplyStaticState();
        }
        EditorGUI.EndDisabledGroup();
    }

    private void DrawLightParentsList()
    {
        GUILayout.Label($"Listed Parents ({_lightParents.Count})", EditorStyles.boldLabel);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, "box", GUILayout.ExpandHeight(true));

        foreach (var go in _lightParents)
        {
            if (go != null)
            {
                GUI.enabled = false;
                EditorGUILayout.ObjectField(go, typeof(GameObject), true);
                GUI.enabled = true;
            }
        }

        EditorGUILayout.EndScrollView();
    }
    #endregion

    #region Core Logic
    /// <summary>
    /// Finds all non-directional lights, retrieves their parents, 
    /// and filters out those matching the exclusion keywords.
    /// </summary>
    private void FetchLightParents()
    {
        _lightParents.Clear();

        Light[] allLights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        HashSet<GameObject> uniqueParents = new HashSet<GameObject>();

        string[] keywords = _exclusionKeywords
            .Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim().ToLower())
            .ToArray();

        foreach (Light light in allLights)
        {
            if (light.type == LightType.Directional) continue;
            if (light.transform.parent == null) continue;

            GameObject parentGO = light.transform.parent.gameObject;
            string parentName = parentGO.name.ToLower();

            bool shouldExclude = keywords.Any(keyword => parentName.Contains(keyword));

            if (!shouldExclude)
            {
                uniqueParents.Add(parentGO);
            }
        }

        _lightParents = uniqueParents.ToList();
    }

    /// <summary>
    /// Applies the chosen static state to the listed parent objects and their entire child hierarchies.
    /// </summary>
    private void ApplyStaticState()
    {
        List<GameObject> allObjectsToModify = new List<GameObject>();

        foreach (GameObject parent in _lightParents)
        {
            if (parent != null)
            {
                Transform[] children = parent.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in children)
                {
                    allObjectsToModify.Add(t.gameObject);
                }
            }
        }

        if (allObjectsToModify.Count == 0) return;

        Undo.RecordObjects(allObjectsToModify.ToArray(), "Change Static State of Hierarchies");

        foreach (GameObject go in allObjectsToModify)
        {
            go.isStatic = _targetStaticState;
            EditorUtility.SetDirty(go);
        }

        Debug.Log($"[OutLightParentManager] Updated static state of {allObjectsToModify.Count} objects (across {_lightParents.Count} hierarchies) to {_targetStaticState}.");
    }
    #endregion
}