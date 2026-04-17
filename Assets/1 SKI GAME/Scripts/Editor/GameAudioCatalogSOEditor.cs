#if UNITY_EDITOR
using System;
using SkiGame.Audio;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameAudioCatalogSO))]
public sealed class GameAudioCatalogSOEditor : Editor
{
    private const string SelectedCueSessionKey = "SkiGame.Audio.SelectedCueId";

    private SerializedProperty _cuesProperty;
    private Vector2 _scroll;
    private GUIStyle _highlightStyle;

    private void OnEnable()
    {
        _cuesProperty = serializedObject.FindProperty("cues");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureStyles();

        DrawHeader();
        EditorGUILayout.Space(8f);
        DrawCueList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Game Audio Catalog", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This inspector supports focus/highlight from the Audio Coverage window. Use the coverage window to add missing cues, validate entries, and jump directly to a cue row here.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sort by Enum"))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(target, "Sort Audio Cues");
                ((GameAudioCatalogSO)target).SortByEnumOrder();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }

            if (GUILayout.Button("Add All Missing"))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(target, "Add Missing Audio Cues");
                ((GameAudioCatalogSO)target).AddAllMissingCues();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }

            if (GUILayout.Button("Remove Duplicates"))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(target, "Remove Duplicate Audio Cues");
                ((GameAudioCatalogSO)target).RemoveDuplicateIds(keepFirst: true);
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }

            if (GUILayout.Button("Normalize Defaults"))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(target, "Normalize Audio Cue Defaults");
                ((GameAudioCatalogSO)target).NormalizeAllDefaults();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }
        }
    }

    private void DrawCueList()
    {
        if (_cuesProperty == null || !_cuesProperty.isArray)
        {
            EditorGUILayout.HelpBox("Cue list property not found.", MessageType.Error);
            return;
        }

        int selectedCueValue = SessionState.GetInt(SelectedCueSessionKey, -1);
        int selectedIndex = FindIndexForCueValue(selectedCueValue);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        for (int i = 0; i < _cuesProperty.arraySize; i++)
        {
            SerializedProperty cueProperty = _cuesProperty.GetArrayElementAtIndex(i);
            if (cueProperty == null)
                continue;

            SerializedProperty idProperty = cueProperty.FindPropertyRelative("id");
            GameAudioCueId cueId = idProperty != null
                ? (GameAudioCueId)idProperty.enumValueIndex
                : GameAudioCueId.None;

            bool isHighlighted = i == selectedIndex;

            Color previousColor = GUI.backgroundColor;
            if (isHighlighted)
                GUI.backgroundColor = new Color(1f, 0.92f, 0.55f, 1f);

            EditorGUILayout.BeginVertical(isHighlighted ? _highlightStyle : EditorStyles.helpBox);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"[{i}] {cueId}", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Ping", GUILayout.Width(50f)))
                {
                    SessionState.SetInt(SelectedCueSessionKey, (int)cueId);
                }

                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    _cuesProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    GUI.backgroundColor = previousColor;
                    break;
                }
            }

            EditorGUILayout.PropertyField(idProperty);
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("clips"), true);
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("volume"));
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("pitchRange"));
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("spatialBlend"));
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("minDistance"));
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("maxDistance"));
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("cooldownSeconds"));
            EditorGUILayout.PropertyField(cueProperty.FindPropertyRelative("mixerGroup"));

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = previousColor;

            if (isHighlighted)
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint)
                {
                    float targetY = Mathf.Max(0f, lastRect.y - 24f);
                    _scroll.y = Mathf.Lerp(_scroll.y, targetY, 0.85f);
                }
            }

            EditorGUILayout.Space(4f);
        }

        EditorGUILayout.EndScrollView();
    }

    private int FindIndexForCueValue(int selectedCueValue)
    {
        if (_cuesProperty == null || !_cuesProperty.isArray || selectedCueValue < 0)
            return -1;

        for (int i = 0; i < _cuesProperty.arraySize; i++)
        {
            SerializedProperty cueProperty = _cuesProperty.GetArrayElementAtIndex(i);
            SerializedProperty idProperty = cueProperty.FindPropertyRelative("id");
            if (idProperty != null && idProperty.enumValueIndex == selectedCueValue)
                return i;
        }

        return -1;
    }

    private void EnsureStyles()
    {
        if (_highlightStyle != null)
            return;

        _highlightStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 8, 8)
        };
    }
}
#endif