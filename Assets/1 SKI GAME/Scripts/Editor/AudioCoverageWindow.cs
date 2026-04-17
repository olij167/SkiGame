#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SkiGame.Activities;
using SkiGame.Audio;
using UnityEditor;
using UnityEngine;

public sealed class AudioCoverageWindow : EditorWindow
{
    private const string SelectedCueSessionKey = "SkiGame.Audio.SelectedCueId";

    private enum CueStatus
    {
        Valid,
        Warning,
        Invalid,
        Missing,
        Duplicate
    }

    private sealed class CueAuditRow
    {
        public GameAudioCueId CueId;
        public CueStatus Status;
        public bool HasHookCoverage;
        public List<int> Indices = new List<int>();
        public List<string> Issues = new List<string>();
    }

    private Vector2 _scroll;
    private GameAudioCatalogSO _catalog;
    private GUIStyle _statusStyle;
    private readonly List<CueAuditRow> _rows = new List<CueAuditRow>();

    private GUIStyle _headerLabelStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _metaChipStyle;
    private GUIStyle _issueTextStyle;
    private GUIStyle _subtleLabelStyle;
    [MenuItem("Tools/Ski Game/Audio Coverage")]
    public static void Open()
    {
        GetWindow<AudioCoverageWindow>("Audio Coverage");
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Audio Coverage");
    }

    private void OnGUI()
    {
        EnsureStyles();

        EditorGUILayout.LabelField("Game Audio Coverage", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Audit the cue catalog, validate cue setup quality, add missing entries, deduplicate, normalize defaults, and jump directly to the matching cue row in the catalog inspector.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            _catalog = (GameAudioCatalogSO)EditorGUILayout.ObjectField("Catalog", _catalog, typeof(GameAudioCatalogSO), false);

            if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
            {
                if (Selection.activeObject is GameAudioCatalogSO selectedCatalog)
                    _catalog = selectedCatalog;
            }
        }

        DrawToolbar();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSummary();
        EditorGUILayout.Space(10f);
        DrawCueCoverage();
        EditorGUILayout.Space(12f);
        DrawRuntimeHookCoverage();

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUI.DisabledScope(_catalog == null))
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Add All Missing", EditorStyles.toolbarButton))
            {
                Undo.RecordObject(_catalog, "Add Missing Audio Cues");
                int added = _catalog.AddAllMissingCues();
                EditorUtility.SetDirty(_catalog);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent(added > 0 ? $"Added {added} cue(s)." : "No missing cues."));
            }

            if (GUILayout.Button("Sort by Enum", EditorStyles.toolbarButton))
            {
                Undo.RecordObject(_catalog, "Sort Audio Cues");
                _catalog.SortByEnumOrder();
                EditorUtility.SetDirty(_catalog);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("Catalog sorted."));
            }

            if (GUILayout.Button("Remove Duplicates", EditorStyles.toolbarButton))
            {
                Undo.RecordObject(_catalog, "Remove Duplicate Audio Cues");
                int removed = _catalog.RemoveDuplicateIds(keepFirst: true);
                EditorUtility.SetDirty(_catalog);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent(removed > 0 ? $"Removed {removed} duplicate cue(s)." : "No duplicates found."));
            }

            if (GUILayout.Button("Normalize Defaults", EditorStyles.toolbarButton))
            {
                Undo.RecordObject(_catalog, "Normalize Audio Cue Defaults");
                int changed = _catalog.NormalizeAllDefaults();
                EditorUtility.SetDirty(_catalog);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent(changed > 0 ? $"Normalized {changed} cue(s)." : "Nothing changed."));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Catalog", EditorStyles.toolbarButton))
            {
                OpenCatalogInspector();
            }
        }
    }

    private void DrawSummary()
    {
        BuildAuditRows();

        int valid = 0;
        int warning = 0;
        int invalid = 0;
        int missing = 0;
        int duplicate = 0;

        for (int i = 0; i < _rows.Count; i++)
        {
            switch (_rows[i].Status)
            {
                case CueStatus.Valid: valid++; break;
                case CueStatus.Warning: warning++; break;
                case CueStatus.Invalid: invalid++; break;
                case CueStatus.Missing: missing++; break;
                case CueStatus.Duplicate: duplicate++; break;
            }
        }

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Total Cue IDs", _rows.Count.ToString());
        EditorGUILayout.LabelField("Valid", valid.ToString());
        EditorGUILayout.LabelField("Warnings", warning.ToString());
        EditorGUILayout.LabelField("Invalid", invalid.ToString());
        EditorGUILayout.LabelField("Missing", missing.ToString());
        EditorGUILayout.LabelField("Duplicates", duplicate.ToString());
        EditorGUILayout.EndVertical();
    }

    private void DrawCueCoverage()
    {
        EditorGUILayout.LabelField("Cue Coverage", EditorStyles.boldLabel);

        if (_catalog == null)
        {
            EditorGUILayout.HelpBox("Assign a GameAudioCatalogSO to enable catalog validation and authoring utilities.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            CueAuditRow row = _rows[i];
            DrawCueRow(row);
        }
    }

    private void DrawCueRow(CueAuditRow row)
    {
        Color statusColor = GetStatusColor(row.Status);
        string statusText = GetStatusText(row.Status);

        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.96f, 0.96f, 0.96f, 1f);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = previousBackground;

        DrawCueHeader(row, statusText, statusColor);
        DrawCueMetaRow(row);

        if (row.Issues.Count > 0)
            DrawCueIssues(row, statusColor);

        EditorGUILayout.Space(2f);
        DrawCueActions(row);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawCueHeader(CueAuditRow row, string statusText, Color statusColor)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(row.CueId.ToString(), _headerLabelStyle);

            GUILayout.FlexibleSpace();

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = statusColor;
            GUILayout.Label(statusText, _badgeStyle, GUILayout.Width(88f), GUILayout.Height(20f));
            GUI.backgroundColor = previousColor;
        }
    }

    private void DrawCueMetaRow(CueAuditRow row)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawMetaChip(row.HasHookCoverage ? "Hooked" : "Unhooked");
            DrawMetaChip($"Entries: {row.Indices.Count}");
        }
    }

    private void DrawMetaChip(string text)
    {
        GUILayout.Label(text, _metaChipStyle, GUILayout.Height(18f));
        GUILayout.Space(4f);
    }

    private void DrawCueIssues(CueAuditRow row, Color accentColor)
    {
        Rect rect = EditorGUILayout.BeginVertical();
        EditorGUILayout.Space(2f);

        string heading = row.Issues.Count == 1 ? "Issue" : "Issues";
        EditorGUILayout.LabelField(heading, _subtleLabelStyle);

        for (int i = 0; i < row.Issues.Count; i++)
        {
            EditorGUILayout.LabelField($"• {row.Issues[i]}", _issueTextStyle);
        }

        EditorGUILayout.Space(2f);
        EditorGUILayout.EndVertical();

        Rect lineRect = new Rect(rect.x + 4f, rect.y + 2f, 3f, Mathf.Max(22f, rect.height - 4f));
        EditorGUI.DrawRect(lineRect, accentColor);
    }

    private void DrawCueActions(CueAuditRow row)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (row.Status == CueStatus.Missing)
            {
                if (GUILayout.Button("Add", GUILayout.Height(22f)))
                    AddCue(row.CueId, focusAfterAdd: false);

                if (GUILayout.Button("Add + Focus", GUILayout.Height(22f)))
                    AddCue(row.CueId, focusAfterAdd: true);
            }
            else
            {
                if (GUILayout.Button("Focus", GUILayout.Height(22f)))
                    FocusCue(row.CueId);
            }

            if (row.Status == CueStatus.Warning || row.Status == CueStatus.Invalid)
            {
                if (GUILayout.Button("Normalize", GUILayout.Height(22f)))
                    NormalizeCueEntries(row.CueId);
            }

            if (row.Status == CueStatus.Duplicate)
            {
                if (GUILayout.Button("Delete Extras", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(_catalog, "Remove Duplicate Audio Cues");
                    int removed = RemoveDuplicateEntriesForCue(row.CueId);
                    EditorUtility.SetDirty(_catalog);
                    AssetDatabase.SaveAssets();
                    ShowNotification(new GUIContent(removed > 0 ? $"Removed {removed} duplicate(s)." : "No duplicates removed."));
                }
            }

            GUILayout.FlexibleSpace();
        }
    }

    private void AddCue(GameAudioCueId cueId, bool focusAfterAdd)
    {
        if (_catalog == null)
            return;

        Undo.RecordObject(_catalog, "Add Audio Cue");
        _catalog.AddMissingCue(cueId);
        _catalog.SortByEnumOrder();
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();

        if (focusAfterAdd)
            FocusCue(cueId);

        Repaint();
    }

    private void NormalizeCueEntries(GameAudioCueId cueId)
    {
        if (_catalog == null)
            return;

        SerializedObject serializedCatalog = new SerializedObject(_catalog);
        SerializedProperty cuesProperty = serializedCatalog.FindProperty("cues");
        if (cuesProperty == null || !cuesProperty.isArray)
            return;

        Undo.RecordObject(_catalog, "Normalize Audio Cue Entry");

        for (int i = 0; i < cuesProperty.arraySize; i++)
        {
            SerializedProperty cueProperty = cuesProperty.GetArrayElementAtIndex(i);
            SerializedProperty idProperty = cueProperty.FindPropertyRelative("id");
            if (idProperty == null || idProperty.enumValueIndex != (int)cueId)
                continue;

            ApplySuggestedDefaultsToSerializedCue(cueProperty, cueId);
        }

        serializedCatalog.ApplyModifiedProperties();
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        FocusCue(cueId);
    }

    private static void ApplySuggestedDefaultsToSerializedCue(SerializedProperty cueProperty, GameAudioCueId cueId)
    {
        if (cueProperty == null)
            return;

        SerializedProperty volumeProperty = cueProperty.FindPropertyRelative("volume");
        SerializedProperty pitchRangeProperty = cueProperty.FindPropertyRelative("pitchRange");
        SerializedProperty spatialBlendProperty = cueProperty.FindPropertyRelative("spatialBlend");
        SerializedProperty minDistanceProperty = cueProperty.FindPropertyRelative("minDistance");
        SerializedProperty maxDistanceProperty = cueProperty.FindPropertyRelative("maxDistance");
        SerializedProperty cooldownProperty = cueProperty.FindPropertyRelative("cooldownSeconds");

        bool isUi = GameAudioCatalogSO.IsUiCue(cueId);

        if (volumeProperty != null)
            volumeProperty.floatValue = 1f;

        if (cooldownProperty != null)
            cooldownProperty.floatValue = cueId == GameAudioCueId.RaceCountdownTick ? 0.03f : 0f;

        if (pitchRangeProperty != null)
        {
            if (isUi)
                pitchRangeProperty.vector2Value = Vector2.one;
            else
                pitchRangeProperty.vector2Value = new Vector2(0.98f, 1.02f);
        }

        if (spatialBlendProperty != null)
            spatialBlendProperty.floatValue = isUi ? 0f : 1f;

        if (minDistanceProperty != null)
            minDistanceProperty.floatValue = isUi ? 1f : 5f;

        if (maxDistanceProperty != null)
            maxDistanceProperty.floatValue = isUi ? 20f : 40f;
    }

    private int RemoveDuplicateEntriesForCue(GameAudioCueId cueId)
    {
        SerializedObject serializedCatalog = new SerializedObject(_catalog);
        SerializedProperty cuesProperty = serializedCatalog.FindProperty("cues");
        if (cuesProperty == null || !cuesProperty.isArray)
            return 0;

        List<int> indices = new List<int>();
        for (int i = 0; i < cuesProperty.arraySize; i++)
        {
            SerializedProperty cueProperty = cuesProperty.GetArrayElementAtIndex(i);
            SerializedProperty idProperty = cueProperty.FindPropertyRelative("id");
            if (idProperty != null && idProperty.enumValueIndex == (int)cueId)
                indices.Add(i);
        }

        int removed = 0;
        for (int i = indices.Count - 1; i >= 1; i--)
        {
            cuesProperty.DeleteArrayElementAtIndex(indices[i]);
            removed++;
        }

        serializedCatalog.ApplyModifiedProperties();
        return removed;
    }

    private void FocusCue(GameAudioCueId cueId)
    {
        SessionState.SetInt(SelectedCueSessionKey, (int)cueId);
        OpenCatalogInspector();
        EditorGUIUtility.PingObject(_catalog);
    }

    private void OpenCatalogInspector()
    {
        if (_catalog == null)
            return;

        Selection.activeObject = _catalog;
        EditorUtility.FocusProjectWindow();
    }

    private void BuildAuditRows()
    {
        _rows.Clear();

        Dictionary<GameAudioCueId, List<int>> indicesByCue = BuildIndicesByCue();

        foreach (GameAudioCueId cueId in Enum.GetValues(typeof(GameAudioCueId)))
        {
            if (cueId == GameAudioCueId.None)
                continue;

            CueAuditRow row = new CueAuditRow
            {
                CueId = cueId,
                HasHookCoverage = HasKnownHookCoverage(cueId)
            };

            if (indicesByCue.TryGetValue(cueId, out List<int> indices))
                row.Indices.AddRange(indices);

            ValidateRow(row);
            _rows.Add(row);
        }
    }

    private Dictionary<GameAudioCueId, List<int>> BuildIndicesByCue()
    {
        Dictionary<GameAudioCueId, List<int>> map = new Dictionary<GameAudioCueId, List<int>>();

        if (_catalog == null)
            return map;

        IReadOnlyList<GameAudioCatalogSO.CueDefinition> cues = _catalog.Cues;
        for (int i = 0; i < cues.Count; i++)
        {
            GameAudioCatalogSO.CueDefinition cue = cues[i];
            if (cue == null || cue.id == GameAudioCueId.None)
                continue;

            if (!map.TryGetValue(cue.id, out List<int> indices))
            {
                indices = new List<int>();
                map.Add(cue.id, indices);
            }

            indices.Add(i);
        }

        return map;
    }

    private void ValidateRow(CueAuditRow row)
    {
        if (_catalog == null)
        {
            row.Status = CueStatus.Missing;
            row.Issues.Add("No catalog assigned.");
            return;
        }

        if (row.Indices.Count == 0)
        {
            row.Status = CueStatus.Missing;
            row.Issues.Add("No catalog entry exists for this cue id.");
            if (row.HasHookCoverage)
                row.Issues.Add("This cue has hook coverage and should be added.");
            return;
        }

        if (row.Indices.Count > 1)
        {
            row.Status = CueStatus.Duplicate;
            row.Issues.Add($"Duplicate entries found: {row.Indices.Count}.");
            row.Issues.Add("Runtime lookup will silently overwrite earlier entries with the last duplicate.");
            return;
        }

        GameAudioCatalogSO.CueDefinition cue = _catalog.Cues[row.Indices[0]];
        bool hasWarning = false;
        bool hasInvalid = false;

        if (cue == null)
        {
            row.Status = CueStatus.Invalid;
            row.Issues.Add("Cue entry is null.");
            return;
        }

        int clipCount = cue.clips != null ? cue.clips.Length : 0;
        int nonNullClipCount = 0;

        if (cue.clips != null)
        {
            for (int i = 0; i < cue.clips.Length; i++)
            {
                if (cue.clips[i] != null)
                    nonNullClipCount++;
            }
        }

        if (cue.clips == null || clipCount == 0)
        {
            hasInvalid = true;
            row.Issues.Add("No clips assigned.");
        }
        else if (nonNullClipCount == 0)
        {
            hasInvalid = true;
            row.Issues.Add("All assigned clips are null.");
        }
        else if (nonNullClipCount < clipCount)
        {
            hasWarning = true;
            row.Issues.Add("Some clip slots are null.");
        }

        if (cue.volume <= 0f)
        {
            hasWarning = true;
            row.Issues.Add("Volume is zero or below.");
        }

        if (cue.pitchRange.x > cue.pitchRange.y)
        {
            hasInvalid = true;
            row.Issues.Add("Pitch range is reversed.");
        }
        else if (Mathf.Abs(cue.pitchRange.y - cue.pitchRange.x) > 0.5f)
        {
            hasWarning = true;
            row.Issues.Add("Pitch range is unusually wide.");
        }

        if (cue.maxDistance < cue.minDistance)
        {
            hasInvalid = true;
            row.Issues.Add("Max distance is less than min distance.");
        }

        bool isUi = GameAudioCatalogSO.IsUiCue(row.CueId);
        if (isUi && cue.spatialBlend > 0.01f)
        {
            hasWarning = true;
            row.Issues.Add("UI cue is spatialized.");
        }
        else if (!isUi && cue.spatialBlend < 0.99f)
        {
            hasWarning = true;
            row.Issues.Add("World cue is not fully 3D.");
        }

        if (cue.mixerGroup == null)
        {
            hasWarning = true;
            row.Issues.Add("No mixer group assigned.");
        }

        if (!row.HasHookCoverage)
        {
            hasWarning = true;
            row.Issues.Add("No known hook coverage found for this cue.");
        }

        if (hasInvalid)
            row.Status = CueStatus.Invalid;
        else if (hasWarning)
            row.Status = CueStatus.Warning;
        else
            row.Status = CueStatus.Valid;
    }

    private static bool HasKnownHookCoverage(GameAudioCueId cueId)
    {
        switch (cueId)
        {
            case GameAudioCueId.UiOpen:
            case GameAudioCueId.UiClose:
            case GameAudioCueId.UiClick:
            case GameAudioCueId.UiBack:
            case GameAudioCueId.UiNavigate:
            case GameAudioCueId.UiAdjust:
            case GameAudioCueId.UiConfirm:
            case GameAudioCueId.UiDeny:
            case GameAudioCueId.UiPurchaseSuccess:
            case GameAudioCueId.UiPurchaseFail:
            case GameAudioCueId.InteractionAccept:
            case GameAudioCueId.InteractionCancel:
            case GameAudioCueId.PlayerStack:
            case GameAudioCueId.PlayerRecover:
            case GameAudioCueId.TrickLand:
            case GameAudioCueId.TrickFail:
            case GameAudioCueId.LiftQueueJoin:
            case GameAudioCueId.LiftQueueLeave:
            case GameAudioCueId.LiftAttachChair:
            case GameAudioCueId.LiftAttachTBar:
            case GameAudioCueId.LiftDetach:
            case GameAudioCueId.LiftAccessGranted:
            case GameAudioCueId.LiftAccessDenied:
            case GameAudioCueId.RaceCountdownTick:
            case GameAudioCueId.RaceCountdownGo:
            case GameAudioCueId.RaceCheckpoint:
            case GameAudioCueId.RaceStart:
            case GameAudioCueId.RaceFinish:
            case GameAudioCueId.RaceFail:
            case GameAudioCueId.RescueStart:
            case GameAudioCueId.RescueComplete:
            case GameAudioCueId.RescueFail:
            case GameAudioCueId.SnowmobileMount:
            case GameAudioCueId.SnowmobileDismount:
            case GameAudioCueId.ResortEnter:
            case GameAudioCueId.ResortExit:
            case GameAudioCueId.ShopOpen:
            case GameAudioCueId.ShopClose:
                return true;

            case GameAudioCueId.InteractionDenied:
            default:
                return false;
        }
    }

    private static MessageType GetMessageType(CueStatus status)
    {
        switch (status)
        {
            case CueStatus.Valid: return MessageType.Info;
            case CueStatus.Warning: return MessageType.Warning;
            case CueStatus.Invalid:
            case CueStatus.Missing:
            case CueStatus.Duplicate:
                return MessageType.Error;
            default:
                return MessageType.None;
        }
    }

    private static string GetStatusText(CueStatus status)
    {
        switch (status)
        {
            case CueStatus.Valid: return "Valid";
            case CueStatus.Warning: return "Warning";
            case CueStatus.Invalid: return "Invalid";
            case CueStatus.Missing: return "Missing";
            case CueStatus.Duplicate: return "Duplicate";
            default: return "Unknown";
        }
    }

    private static Color GetStatusColor(CueStatus status)
    {
        switch (status)
        {
            case CueStatus.Valid:
                return new Color(0.38f, 0.70f, 0.43f, 1f);

            case CueStatus.Warning:
                return new Color(0.90f, 0.66f, 0.20f, 1f);

            case CueStatus.Invalid:
                return new Color(0.83f, 0.35f, 0.32f, 1f);

            case CueStatus.Missing:
                return new Color(0.55f, 0.56f, 0.62f, 1f);

            case CueStatus.Duplicate:
                return new Color(0.72f, 0.33f, 0.64f, 1f);

            default:
                return new Color(0.45f, 0.45f, 0.45f, 1f);
        }
    }

    private void EnsureStyles()
    {
        if (_statusStyle != null &&
            _headerLabelStyle != null &&
            _badgeStyle != null &&
            _metaChipStyle != null &&
            _issueTextStyle != null &&
            _subtleLabelStyle != null)
        {
            return;
        }

        _statusStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        _headerLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            wordWrap = false
        };

        _badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            padding = new RectOffset(8, 8, 3, 3),
            margin = new RectOffset(6, 0, 0, 0)
        };

        _metaChipStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(8, 8, 2, 2),
            margin = new RectOffset(0, 4, 0, 0)
        };

        _issueTextStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            richText = false,
            padding = new RectOffset(10, 0, 0, 0)
        };

        _subtleLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.42f, 0.42f, 0.42f, 1f) }
        };
    }

    private static void DrawRuntimeHookCoverage()
    {
        EditorGUILayout.LabelField("Scene Hook Coverage", EditorStyles.boldLabel);

        DrawTypeCount("UIDocument", FindObjectsByTypeSafe<UnityEngine.UIElements.UIDocument>());
        DrawTypeCount("SkiController", FindObjectsByTypeSafe<SkiController>());
        DrawTypeCount("SkierTrickTracker", FindObjectsByTypeSafe<SkierTrickTracker>());
        DrawTypeCount("MountainActivityManager", FindObjectsByTypeSafe<MountainActivityManager>());
        DrawTypeCount("RaceCourseLine", FindObjectsByTypeSafe<RaceCourseLine>());
        DrawTypeCount("LiftRider", FindObjectsByTypeSafe<LiftRider>());
        DrawTypeCount("SnowmobileController", FindObjectsByTypeSafe<SnowmobileController>());
        DrawTypeCount("SkiPassKioskUI", FindObjectsByTypeSafe<SkiPassKioskUI>());
        DrawTypeCount("SkiResortStateController", FindObjectsByTypeSafe<SkiResortStateController>());
        DrawTypeCount("GameAudioDirector", FindObjectsByTypeSafe<GameAudioDirector>());
        DrawTypeCount("GameAudioRuntimeHooks", FindObjectsByTypeSafe<GameAudioRuntimeHooks>());
    }

    private static void DrawTypeCount(string label, int count)
    {
        EditorGUILayout.LabelField(label, count.ToString());
    }

    private static int FindObjectsByTypeSafe<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
#else
        return UnityEngine.Object.FindObjectsOfType<T>().Length;
#endif
    }
}
#endif