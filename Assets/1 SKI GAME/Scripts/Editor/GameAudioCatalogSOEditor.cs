#if UNITY_EDITOR
using System;
using PungentFunk.Utilities.Audio;
using PungentFunk.Utilities.Editor.Audio;
using PungentFunk.Utilities.Editor.Theme;
using SkiGame.Audio;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameAudioCatalogSO))]
public sealed class GameAudioCatalogSOEditor : Editor
{
    private const string LegacySelectedCueSessionKey = "SkiGame.Audio.SelectedCueId";
    private const string SelectedCueSessionKey = "GenericUtility.AudioCoverage.SelectedCueId";
    private const string ActiveProfileGuidSessionKey = "GenericUtility.AudioCoverage.ActiveProfileGuid";

    private SerializedProperty _cuesProperty;
    private Vector2 _scroll;
    private AudioCoverageProfileSO _profile;

    private void OnEnable()
    {
        _cuesProperty = serializedObject.FindProperty("cues");
        ResolveProfile();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader();
        EditorGUILayout.Space(8f);
        DrawCueList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
        {
            UtilityWindowTheme.SectionTitle("Game Audio Catalog", UtilityWindowTheme.Blue, _cuesProperty != null && _cuesProperty.isArray ? _cuesProperty.arraySize.ToString() : null);
            EditorGUILayout.LabelField(
                "Use Audio Catalog Coverage to validate cue quality and define project-specific component/event bindings through an AudioCoverageProfileSO.",
                UtilityWindowTheme.MutedMiniLabelStyle);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (UtilityWindowTheme.TintedButton("Open Coverage", UtilityWindowTheme.Blue, GUILayout.Width(120f)))
                    AudioCoverageWindow.Open();

                if (UtilityWindowTheme.TintedButton("Sort", UtilityWindowTheme.Teal, GUILayout.Width(72f)))
                    MutateCatalog("Sort Audio Cues", catalog => catalog.SortByEnumOrder());

                if (UtilityWindowTheme.TintedButton("Add Missing", UtilityWindowTheme.Green, GUILayout.Width(104f)))
                    MutateCatalog("Add Missing Audio Cues", catalog => catalog.AddAllMissingCues());

                if (UtilityWindowTheme.TintedButton("Deduplicate", UtilityWindowTheme.Amber, GUILayout.Width(104f)))
                    MutateCatalog("Remove Duplicate Audio Cues", catalog => catalog.RemoveDuplicateIds(keepFirst: true));

                if (UtilityWindowTheme.TintedButton("Normalize", UtilityWindowTheme.Purple, GUILayout.Width(96f)))
                    MutateCatalog("Normalize Audio Cue Defaults", catalog => catalog.NormalizeAllDefaults());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _profile = (AudioCoverageProfileSO)EditorGUILayout.ObjectField(
                    new GUIContent("Coverage Profile", "Optional profile used to show binding counts beside cue entries."),
                    _profile,
                    typeof(AudioCoverageProfileSO),
                    false);
                if (EditorGUI.EndChangeCheck() && _profile != null)
                    SessionState.SetString(ActiveProfileGuidSessionKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_profile)));

                if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                    ResolveProfile();
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

        int selectedCueValue = SessionState.GetInt(SelectedCueSessionKey, SessionState.GetInt(LegacySelectedCueSessionKey, -1));
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
            Color tint = isHighlighted ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, isHighlighted ? 0.24f : 0.12f, isHighlighted ? 0.14f : 0.06f, 7, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"[{i}] {cueId}", UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();

                    if (_profile != null)
                    {
                        int bindingCount = _profile.CountBindingsForCue(cueId.ToString(), includeIgnored: false);
                        bool ignored = _profile.IsCueIgnored(cueId.ToString());
                        UtilityWindowTheme.CountPill(ignored ? "Ignored" : $"Bindings {bindingCount}", bindingCount > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 104f);
                    }

                    if (GUILayout.Button("Focus", GUILayout.Width(58f)))
                    {
                        SessionState.SetInt(SelectedCueSessionKey, (int)cueId);
                        SessionState.SetInt(LegacySelectedCueSessionKey, (int)cueId);
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                    {
                        _cuesProperty.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                EditorGUILayout.PropertyField(idProperty);
                DrawIfExists(cueProperty, "clips", true);
                DrawIfExists(cueProperty, "volume", false);
                DrawIfExists(cueProperty, "pitchRange", false);
                DrawIfExists(cueProperty, "spatialBlend", false);
                DrawIfExists(cueProperty, "minDistance", false);
                DrawIfExists(cueProperty, "maxDistance", false);
                DrawIfExists(cueProperty, "cooldownSeconds", false);
                DrawIfExists(cueProperty, "mixerGroup", false);

                if (_profile != null)
                    DrawCueBindingSummary(cueId);
            }

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

    private void DrawCueBindingSummary(GameAudioCueId cueId)
    {
        if (_profile == null || cueId == GameAudioCueId.None)
            return;

        int bindingCount = _profile.CountBindingsForCue(cueId.ToString(), includeIgnored: false);
        bool ignored = _profile.IsCueIgnored(cueId.ToString());

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                ignored
                    ? "Coverage: ignored by profile."
                    : bindingCount > 0
                        ? $"Coverage: {bindingCount} configured binding(s)."
                        : "Coverage: no profile binding configured.",
                UtilityWindowTheme.MutedMiniLabelStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Edit Bindings", GUILayout.Width(100f)))
            {
                SessionState.SetInt(SelectedCueSessionKey, (int)cueId);
                SessionState.SetInt(LegacySelectedCueSessionKey, (int)cueId);
                AudioCoverageWindow.Open();
            }
        }
    }

    private void DrawIfExists(SerializedProperty parent, string propertyName, bool includeChildren)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, includeChildren);
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

    private void MutateCatalog(string undoName, Action<GameAudioCatalogSO> action)
    {
        serializedObject.ApplyModifiedProperties();
        GameAudioCatalogSO catalog = (GameAudioCatalogSO)target;
        Undo.RecordObject(catalog, undoName);
        action?.Invoke(catalog);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        serializedObject.Update();
    }

    private void ResolveProfile()
    {
        string activeGuid = SessionState.GetString(ActiveProfileGuidSessionKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(activeGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(activeGuid);
            if (!string.IsNullOrWhiteSpace(path))
                _profile = AssetDatabase.LoadAssetAtPath<AudioCoverageProfileSO>(path);
        }

        if (_profile != null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:AudioCoverageProfileSO");
        if (guids != null && guids.Length > 0)
            _profile = AssetDatabase.LoadAssetAtPath<AudioCoverageProfileSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
#endif
