#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using TMPro;
using OutGame;
using System;

[CustomEditor(typeof(OutTextManipulator))]
public class OutTextManipulatorEditor : Editor
{
    // 1. Targeting
    SerializedProperty targetMode;
    SerializedProperty singleText;
    SerializedProperty manipulations;
    SerializedProperty textComponents;

    // 2. Sequence
    SerializedProperty onStart;
    SerializedProperty manipulationMode;

    // 3. Effects
    SerializedProperty effectMode;
    SerializedProperty delayBetweenCharacters;
    SerializedProperty delayBetweenWords;

    // 4. Visual Style
    SerializedProperty visualStyle;
    SerializedProperty loopVisualStyle;
    SerializedProperty glitchIntensity;

    // 5. Instability
    SerializedProperty useUnstableDelay;
    SerializedProperty switchDelayRangeMs;

    // --- PREVIEW TRACKING ---
    private CancellationTokenSource previewCts;
    private string cachedSingleText;
    private List<string> cachedMultiTexts = new List<string>();

    private void OnEnable()
    {
        targetMode = serializedObject.FindProperty("targetMode");
        singleText = serializedObject.FindProperty("text");
        manipulations = serializedObject.FindProperty("manipulations");
        textComponents = serializedObject.FindProperty("textComponents");

        onStart = serializedObject.FindProperty("onStart");
        manipulationMode = serializedObject.FindProperty("manipulationMode");

        effectMode = serializedObject.FindProperty("effectMode");
        delayBetweenCharacters = serializedObject.FindProperty("delayBetweenCharacters");
        delayBetweenWords = serializedObject.FindProperty("delayBetweenWords");

        visualStyle = serializedObject.FindProperty("visualStyle");
        loopVisualStyle = serializedObject.FindProperty("loopVisualStyle");
        glitchIntensity = serializedObject.FindProperty("glitchIntensity");

        useUnstableDelay = serializedObject.FindProperty("useUnstableDelay");
        switchDelayRangeMs = serializedObject.FindProperty("switchDelayRangeMs");
    }

    private void OnDisable()
    {
        if (previewCts != null)
        {
            StopPreview((OutTextManipulator)target);
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(0, 0, 10, 10)
        };

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(5, 5, 5, 5)
        };

        EditorGUILayout.LabelField("OUT TEXT MANIPULATOR", headerStyle);
        EditorGUILayout.Space();

        // Section 1: Target Setup
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("1. Target Setup", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetMode);
        EditorGUILayout.Space();

        if (targetMode.enumValueIndex == (int)ManipulationTarget.StringList)
        {
            EditorGUILayout.PropertyField(singleText, new GUIContent("TMP Text Component"));
            EditorGUILayout.PropertyField(manipulations, new GUIContent("Text Manipulations"), true);
        }
        else
        {
            EditorGUILayout.PropertyField(textComponents, new GUIContent("TMP Text Components"), true);
        }
        EditorGUILayout.EndVertical();

        // Section 2: Sequence Rules
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("2. Sequence Rules", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(onStart, new GUIContent("Play On Start?"));
        EditorGUILayout.PropertyField(manipulationMode, new GUIContent("Manipulation Loop"));
        EditorGUILayout.EndVertical();

        // Section 3: Effect Settings
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("3. Effect Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(effectMode, new GUIContent("Print Style"));

        if (effectMode.enumValueIndex == (int)TextEffectMode.Typewriter)
        {
            EditorGUILayout.PropertyField(delayBetweenCharacters, new GUIContent("Character Delay (s)"));
        }
        else if (effectMode.enumValueIndex == (int)TextEffectMode.WordByWord)
        {
            EditorGUILayout.PropertyField(delayBetweenWords, new GUIContent("Word Delay (s)"));
        }
        EditorGUILayout.EndVertical();

        // Section 4: Visual Style (NEW)
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("4. Visual Style", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(visualStyle);

        if (visualStyle.enumValueIndex != (int)TextVisualStyle.Constant)
        {
            EditorGUILayout.PropertyField(loopVisualStyle, new GUIContent("Loop Visual Effect?"));

            if (visualStyle.enumValueIndex == (int)TextVisualStyle.Glitch)
            {
                EditorGUILayout.PropertyField(glitchIntensity, new GUIContent("Glitch Displacement"));
            }
        }
        EditorGUILayout.EndVertical();

        // Section 5: Instability Settings
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("5. Instability & Timing", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These delays dictate both the text switching speed AND the visual style frequency.", MessageType.None);
        EditorGUILayout.PropertyField(useUnstableDelay, new GUIContent("Use Unstable Delay?"));

        if (useUnstableDelay.boolValue)
        {
            EditorGUILayout.PropertyField(switchDelayRangeMs, new GUIContent("Min/Max Delay (ms)"));
        }
        else
        {
            int currentDelay = switchDelayRangeMs.vector2IntValue.x;
            int newDelay = EditorGUILayout.IntField("Constant Switch Delay (ms)", currentDelay);
            if (newDelay != currentDelay)
            {
                switchDelayRangeMs.vector2IntValue = new Vector2Int(newDelay, switchDelayRangeMs.vector2IntValue.y);
            }
        }
        EditorGUILayout.EndVertical();

        // Section 6: Editor Preview
        OutTextManipulator script = (OutTextManipulator)target;
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("6. Editor Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = Color.green;
        GUI.enabled = previewCts == null && !Application.isPlaying;
        if (GUILayout.Button("Play Preview", GUILayout.Height(30)))
        {
            StartPreview(script);
        }

        GUI.backgroundColor = Color.red;
        GUI.enabled = previewCts != null && !Application.isPlaying;
        if (GUILayout.Button("Stop Preview", GUILayout.Height(30)))
        {
            StopPreview(script);
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Editor preview is disabled in Play Mode. Check your Game window!", MessageType.Info);
        }

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }

    // -------------------------------------------------------------------
    // PREVIEW ENGINE LOGIC
    // -------------------------------------------------------------------
    private async void StartPreview(OutTextManipulator script)
    {
        if (previewCts != null) return;

        previewCts = new CancellationTokenSource();
        CancellationToken token = previewCts.Token;

        cachedMultiTexts.Clear();
        if (script.targetMode == ManipulationTarget.StringList)
        {
            TMP_Text singleTextComp = (TMP_Text)singleText.objectReferenceValue;
            cachedSingleText = singleTextComp != null ? singleTextComp.text : "";
        }
        else
        {
            for (int i = 0; i < textComponents.arraySize; i++)
            {
                TMP_Text t = (TMP_Text)textComponents.GetArrayElementAtIndex(i).objectReferenceValue;
                if (t != null)
                {
                    cachedMultiTexts.Add(t.text);
                    t.text = "";
                }
                else cachedMultiTexts.Add("");
            }
        }

        int currentIndex = 0;
        int direction = 1;
        int maxCount = script.targetMode == ManipulationTarget.StringList ? script.manipulations.Count : script.textComponents.Count;

        if (maxCount == 0)
        {
            StopPreview(script);
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                string statement = "";
                TMP_Text activeTextComponent = null;

                if (script.targetMode == ManipulationTarget.StringList)
                {
                    statement = script.manipulations[currentIndex];
                    activeTextComponent = (TMP_Text)singleText.objectReferenceValue;
                    if (activeTextComponent != null) activeTextComponent.text = "";
                }
                else
                {
                    statement = cachedMultiTexts[currentIndex];
                    activeTextComponent = (TMP_Text)textComponents.GetArrayElementAtIndex(currentIndex).objectReferenceValue;

                    for (int i = 0; i < textComponents.arraySize; i++)
                    {
                        TMP_Text t = (TMP_Text)textComponents.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (t != null) t.text = "";
                    }
                }

                await EditorApplyTextEffect(statement, activeTextComponent, script, token);
                if (token.IsCancellationRequested) break;

                int delayMs = script.useUnstableDelay
                    ? UnityEngine.Random.Range(script.switchDelayRangeMs.x, script.switchDelayRangeMs.y)
                    : script.switchDelayRangeMs.x;

                await Task.Delay(delayMs, token);

                if (script.manipulationMode == TextManipulationMode.Loop)
                {
                    currentIndex = (currentIndex + 1) % maxCount;
                }
                else if (script.manipulationMode == TextManipulationMode.PingPong)
                {
                    currentIndex += direction;
                    if (currentIndex >= maxCount || currentIndex < 0)
                    {
                        direction *= -1;
                        currentIndex += direction * 2;
                        currentIndex = Mathf.Clamp(currentIndex, 0, maxCount - 1);
                    }
                }
            }
        }
        catch (TaskCanceledException) { /* Task killed safely */ }
        catch (OperationCanceledException) { /* Task killed safely */ }
        finally
        {
            RestoreTextStates(script);
        }
    }

    private async Task EditorApplyTextEffect(string statement, TMP_Text targetComponent, OutTextManipulator script, CancellationToken token)
    {
        if (targetComponent == null || string.IsNullOrEmpty(statement)) return;

        void UpdateText(string t)
        {
            targetComponent.text = t;
            EditorUtility.SetDirty(targetComponent);
        }

        switch (script.effectMode)
        {
            case TextEffectMode.Instant:
                UpdateText(statement);
                break;

            case TextEffectMode.Typewriter:
                string currentText = "";
                foreach (char c in statement)
                {
                    token.ThrowIfCancellationRequested();
                    currentText += c;
                    UpdateText(currentText);
                    await Task.Delay(Mathf.RoundToInt(script.delayBetweenCharacters * 1000), token);
                }
                break;

            case TextEffectMode.WordByWord:
                string[] words = statement.Split(' ');
                string currentWordText = "";
                for (int i = 0; i < words.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    currentWordText += words[i] + " ";
                    UpdateText(currentWordText);
                    await Task.Delay(Mathf.RoundToInt(script.delayBetweenWords * 1000), token);
                }
                break;
        }
    }

    private void StopPreview(OutTextManipulator script)
    {
        if (previewCts != null)
        {
            previewCts.Cancel();
            previewCts.Dispose();
            previewCts = null;
        }

        RestoreTextStates(script);
        Repaint();
    }

    private void RestoreTextStates(OutTextManipulator script)
    {
        if (script.targetMode == ManipulationTarget.StringList)
        {
            TMP_Text singleTextComp = (TMP_Text)singleText.objectReferenceValue;
            if (singleTextComp != null && cachedSingleText != null)
            {
                singleTextComp.text = cachedSingleText;
                singleTextComp.color = new Color(singleTextComp.color.r, singleTextComp.color.g, singleTextComp.color.b, 1f);
                singleTextComp.rectTransform.anchoredPosition = Vector2.zero; // Reset Glitch offset
                EditorUtility.SetDirty(singleTextComp);
            }
        }
        else
        {
            for (int i = 0; i < textComponents.arraySize; i++)
            {
                TMP_Text t = (TMP_Text)textComponents.GetArrayElementAtIndex(i).objectReferenceValue;
                if (t != null && i < cachedMultiTexts.Count)
                {
                    t.text = cachedMultiTexts[i];
                    t.color = new Color(t.color.r, t.color.g, t.color.b, 1f);
                    t.rectTransform.anchoredPosition = Vector2.zero; // Reset Glitch offset
                    EditorUtility.SetDirty(t);
                }
            }
        }
    }
}
#endif