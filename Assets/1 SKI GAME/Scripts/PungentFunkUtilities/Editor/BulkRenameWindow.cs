using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// Standalone generic editor-window utility for bulk-renaming scene objects and optional project assets.
    /// This version intentionally has no dependency on the legacy Rename scene component.
    /// </summary>
    public sealed class BulkRenameWindow : EditorWindow
    {
        private enum RenameMode
        {
            ExactName,
            BaseNameWithIndex,
            AddPrefixSuffix,
            StripPrefixSuffix,
            FindReplace,
            RemoveTrailingNumbering
        }

        private struct RenamePreview
        {
            public UnityObject target;
            public string oldName;
            public string newName;
            public bool asset;
            public bool valid;
            public string note;
        }

        private const string PrefPrefix = "GenericUtility.BulkRename.";
        private const string PrefMode = PrefPrefix + "Mode";
        private const string PrefBaseName = PrefPrefix + "BaseName";
        private const string PrefPrefixText = PrefPrefix + "PrefixText";
        private const string PrefSuffixText = PrefPrefix + "SuffixText";
        private const string PrefFindText = PrefPrefix + "FindText";
        private const string PrefReplaceText = PrefPrefix + "ReplaceText";
        private const string PrefSeparator = PrefPrefix + "Separator";
        private const string PrefStartIndex = PrefPrefix + "StartIndex";
        private const string PrefPadding = PrefPrefix + "Padding";
        private const string PrefUseSelectionLive = PrefPrefix + "UseSelectionLive";
        private const string PrefRenameAssets = PrefPrefix + "RenameAssets";
        private const string PrefKeepAssetExtension = PrefPrefix + "KeepAssetExtension";
        private const string PrefTargetsHeight = PrefPrefix + "TargetsHeight";
        private const string PrefOperationHeight = PrefPrefix + "OperationHeight";
        private const string PrefPreviewHeight = PrefPrefix + "PreviewHeight";
        private const string PrefTargetsFoldout = PrefPrefix + "TargetsFoldout";
        private const string PrefOperationFoldout = PrefPrefix + "OperationFoldout";
        private const string PrefPreviewFoldout = PrefPrefix + "PreviewFoldout";

        private readonly List<UnityObject> _manualTargets = new List<UnityObject>();
        private readonly List<RenamePreview> _preview = new List<RenamePreview>();

        private Vector2 _targetsScroll;
        private Vector2 _operationScroll;
        private Vector2 _previewScroll;
        private string _status = "Ready.";

        private RenameMode _mode = RenameMode.BaseNameWithIndex;
        private string _baseName = "Object";
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private string _find = string.Empty;
        private string _replace = string.Empty;
        private string _separator = "_";
        private int _startIndex = 1;
        private int _indexPadding = 2;
        private bool _useSelectionLive = true;
        private bool _renameAssets;
        private bool _keepAssetExtension = true;

        private float _targetsHeight = 210f;
        private float _operationHeight = 190f;
        private float _previewHeight = 260f;
        private bool _targetsFoldout = true;
        private bool _operationFoldout = true;
        private bool _previewFoldout = true;

        public static void Open()
        {
            BulkRenameWindow window = GetWindow<BulkRenameWindow>("Bulk Rename");
            window.minSize = new Vector2(430f, 420f);
            window.Show();
        }

        //// Legacy compatibility alias. Keep until the final public menu root is locked for release.
        //public static void OpenLegacy()
        //{
        //    Open();
        //}

        public static void OpenWithTargets(IEnumerable<UnityObject> targets)
        {
            BulkRenameWindow window = GetWindow<BulkRenameWindow>("Bulk Rename");
            window.minSize = new Vector2(430f, 420f);
            window._useSelectionLive = false;
            window._manualTargets.Clear();
            window.AddTargets(targets);
            window.SavePrefs();
            window.RebuildPreview();
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Bulk Rename");
            LoadPrefs();
            Selection.selectionChanged += OnSelectionChanged;
            RebuildPreview();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SavePrefs();
        }

        private void OnSelectionChanged()
        {
            if (_useSelectionLive)
            {
                RebuildPreview();
                Repaint();
            }
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Bulk Rename",
                "Generic utility for renaming selected scene objects and, optionally, project assets. Supports preview, Undo, prefix/suffix, indexing, find/replace, and manual target lists.",
                _status);

            DrawToolbar();

            DrawTargetsSection();
            DrawOperationSection();
            DrawPreviewSection();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool previousLive = _useSelectionLive;
                _useSelectionLive = GUILayout.Toggle(_useSelectionLive, "Live Selection", EditorStyles.toolbarButton, GUILayout.Width(105f));
                if (_useSelectionLive != previousLive)
                {
                    SavePrefs();
                    RebuildPreview();
                }

                if (GUILayout.Button("Add Selection", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    AddTargets(Selection.objects);
                    _useSelectionLive = false;
                    RebuildPreview();
                }

                if (GUILayout.Button("Clear Manual", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    _manualTargets.Clear();
                    RebuildPreview();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh Preview", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                    RebuildPreview();
            }
        }

        private void DrawTargetsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _targetsFoldout = EditorGUILayout.Foldout(_targetsFoldout, "Targets", true, UtilityWindowTheme.SectionHeaderStyle);
                if (_targetsFoldout)
                {
                    _targetsScroll = EditorGUILayout.BeginScrollView(_targetsScroll, GUILayout.Height(_targetsHeight));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(_useSelectionLive ? "Using current editor selection." : "Using manual target list.", UtilityWindowTheme.MutedMiniLabelStyle);
                        UtilityWindowTheme.CountPill(GetTargets().Count.ToString(), UtilityWindowTheme.Teal, 42f);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _renameAssets = EditorGUILayout.ToggleLeft(new GUIContent("Allow Project Asset Renames", "Scene objects are always renameable. Project assets are only renamed when this is enabled."), _renameAssets);
                        _keepAssetExtension = EditorGUILayout.ToggleLeft(new GUIContent("Keep Asset Extension", "When renaming assets, strips any typed file extension before AssetDatabase.RenameAsset is called."), _keepAssetExtension);
                    }

                    Rect dropRect = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
                    GUI.Box(dropRect, "Drag objects/assets here, or use Add Selection", EditorStyles.helpBox);
                    HandleDragAndDrop(dropRect);

                    List<UnityObject> targets = GetTargets();
                    if (targets.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No targets. Select objects with Live Selection enabled, or add objects/assets manually.", MessageType.Info);
                    }
                    else
                    {
                        for (int i = 0; i < targets.Count; i++)
                            DrawTargetRow(targets, i);
                    }

                    EditorGUILayout.EndScrollView();
                    UtilityWindowTheme.VerticalResizeHandle(ref _targetsHeight, 120f, Mathf.Max(130f, position.height - 280f), SavePrefs);
                }
            }
        }

        private void DrawTargetRow(List<UnityObject> targets, int index)
        {
            UnityObject target = targets[index];
            if (target == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(target, typeof(UnityObject), true);

                if (!_useSelectionLive)
                {
                    using (new EditorGUI.DisabledScope(index <= 0))
                    {
                        if (GUILayout.Button("▲", GUILayout.Width(28f)))
                        {
                            UnityObject temp = _manualTargets[index - 1];
                            _manualTargets[index - 1] = _manualTargets[index];
                            _manualTargets[index] = temp;
                            RebuildPreview();
                        }
                    }

                    using (new EditorGUI.DisabledScope(index >= _manualTargets.Count - 1))
                    {
                        if (GUILayout.Button("▼", GUILayout.Width(28f)))
                        {
                            UnityObject temp = _manualTargets[index + 1];
                            _manualTargets[index + 1] = _manualTargets[index];
                            _manualTargets[index] = temp;
                            RebuildPreview();
                        }
                    }

                    if (GUILayout.Button("−", GUILayout.Width(28f)))
                    {
                        _manualTargets.RemoveAt(index);
                        RebuildPreview();
                    }
                }
            }
        }

        private void DrawOperationSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                _operationFoldout = EditorGUILayout.Foldout(_operationFoldout, "Rename Operation", true, UtilityWindowTheme.SectionHeaderStyle);
                if (_operationFoldout)
                {
                    _operationScroll = EditorGUILayout.BeginScrollView(_operationScroll, GUILayout.Height(_operationHeight));

                    EditorGUI.BeginChangeCheck();

                    _mode = (RenameMode)EditorGUILayout.EnumPopup("Mode", _mode);

                    switch (_mode)
                    {
                        case RenameMode.ExactName:
                            _baseName = EditorGUILayout.TextField(new GUIContent("New Name", "Every target receives exactly this name."), _baseName);
                            break;
                        case RenameMode.BaseNameWithIndex:
                            _baseName = EditorGUILayout.TextField("Base Name", _baseName);
                            _separator = EditorGUILayout.TextField("Separator", _separator);
                            _startIndex = EditorGUILayout.IntField("Start Index", _startIndex);
                            _indexPadding = Mathf.Clamp(EditorGUILayout.IntField("Index Padding", _indexPadding), 0, 8);
                            break;
                        case RenameMode.AddPrefixSuffix:
                            _prefix = EditorGUILayout.TextField("Prefix", _prefix);
                            _suffix = EditorGUILayout.TextField("Suffix", _suffix);
                            break;
                        case RenameMode.StripPrefixSuffix:
                            _prefix = EditorGUILayout.TextField("Prefix To Strip", _prefix);
                            _suffix = EditorGUILayout.TextField("Suffix To Strip", _suffix);
                            break;
                        case RenameMode.FindReplace:
                            _find = EditorGUILayout.TextField("Find", _find);
                            _replace = EditorGUILayout.TextField("Replace With", _replace);
                            break;
                        case RenameMode.RemoveTrailingNumbering:
                            EditorGUILayout.HelpBox("Removes common trailing numbering patterns such as 'Name 01', 'Name_01', 'Name #1', and 'Name (1)'.", MessageType.None);
                            break;
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        SavePrefs();
                        RebuildPreview();
                    }

                    EditorGUILayout.EndScrollView();
                    UtilityWindowTheme.VerticalResizeHandle(ref _operationHeight, 100f, Mathf.Max(120f, position.height - 280f), SavePrefs);
                }
            }
        }

        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _previewFoldout = EditorGUILayout.Foldout(_previewFoldout, "Preview & Apply", true, UtilityWindowTheme.SectionHeaderStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"{CountChanges()} change(s)", UtilityWindowTheme.Purple, 92f);
                }

                if (_previewFoldout)
                {
                    _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(_previewHeight));

                    if (_preview.Count == 0)
                    {
                        EditorGUILayout.HelpBox("Nothing to preview.", MessageType.Info);
                    }
                    else
                    {
                        foreach (RenamePreview row in _preview)
                            DrawPreviewRow(row);
                    }

                    EditorGUILayout.EndScrollView();
                    UtilityWindowTheme.VerticalResizeHandle(ref _previewHeight, 140f, Mathf.Max(160f, position.height - 260f), SavePrefs);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(CountChanges() == 0))
                        {
                            if (UtilityWindowTheme.TintedButton("Apply Rename", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                                ApplyRename();
                        }

                        if (GUILayout.Button("Select Targets", GUILayout.Height(28f), GUILayout.Width(120f)))
                            Selection.objects = GetTargets().ToArray();
                    }
                }
            }
        }

        private void DrawPreviewRow(RenamePreview row)
        {
            Color tint = row.valid ? (row.oldName == row.newName ? UtilityWindowTheme.Neutral : UtilityWindowTheme.Green) : UtilityWindowTheme.Red;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.14f, 0.08f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(row.target, typeof(UnityObject), true);
                    if (row.asset)
                        UtilityWindowTheme.CountPill("Asset", UtilityWindowTheme.Amber, 46f);
                }

                EditorGUILayout.LabelField(row.oldName + "  →  " + row.newName, UtilityWindowTheme.CardLabelStyle);
                if (!string.IsNullOrEmpty(row.note))
                    EditorGUILayout.LabelField(row.note, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
            Event evt = Event.current;
            if (evt == null || !dropRect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    AddTargets(DragAndDrop.objectReferences);
                    _useSelectionLive = false;
                    RebuildPreview();
                }
                evt.Use();
            }
        }

        private void AddTargets(IEnumerable<UnityObject> targets)
        {
            if (targets == null)
                return;

            foreach (UnityObject target in targets)
            {
                if (target == null || _manualTargets.Contains(target))
                    continue;
                _manualTargets.Add(target);
            }
        }

        private List<UnityObject> GetTargets()
        {
            UnityObject[] raw = _useSelectionLive ? Selection.objects : _manualTargets.ToArray();
            List<UnityObject> results = new List<UnityObject>();
            HashSet<int> seen = new HashSet<int>();

            for (int i = 0; i < raw.Length; i++)
            {
                UnityObject target = raw[i];
                if (target == null)
                    continue;

                int id = target.GetInstanceID();
                if (seen.Add(id))
                    results.Add(target);
            }

            return results;
        }

        private void RebuildPreview()
        {
            _preview.Clear();
            List<UnityObject> targets = GetTargets();

            for (int i = 0; i < targets.Count; i++)
            {
                UnityObject target = targets[i];
                if (target == null)
                    continue;

                bool isAsset = IsProjectAsset(target);
                string oldName = target.name;
                string newName = BuildNewName(oldName, i);
                string note = string.Empty;
                bool valid = true;

                if (string.IsNullOrWhiteSpace(newName))
                {
                    valid = false;
                    note = "Generated name is empty.";
                }
                else if (isAsset && !_renameAssets)
                {
                    valid = false;
                    note = "Project asset rename disabled.";
                }
                else if (isAsset && _keepAssetExtension)
                {
                    newName = StripExtension(newName);
                }

                _preview.Add(new RenamePreview
                {
                    target = target,
                    oldName = oldName,
                    newName = newName,
                    asset = isAsset,
                    valid = valid,
                    note = note
                });
            }

            _status = $"Previewing {_preview.Count} target(s), {CountChanges()} change(s).";
        }

        private string BuildNewName(string oldName, int index)
        {
            switch (_mode)
            {
                case RenameMode.ExactName:
                    return _baseName ?? string.Empty;
                case RenameMode.BaseNameWithIndex:
                    string number = _indexPadding > 0
                        ? (_startIndex + index).ToString().PadLeft(_indexPadding, '0')
                        : (_startIndex + index).ToString();
                    return (_baseName ?? string.Empty) + (_separator ?? string.Empty) + number;
                case RenameMode.AddPrefixSuffix:
                    return (_prefix ?? string.Empty) + oldName + (_suffix ?? string.Empty);
                case RenameMode.StripPrefixSuffix:
                    return StripPrefixSuffix(oldName, _prefix, _suffix);
                case RenameMode.FindReplace:
                    return string.IsNullOrEmpty(_find) ? oldName : oldName.Replace(_find, _replace ?? string.Empty);
                case RenameMode.RemoveTrailingNumbering:
                    return RemoveTrailingNumbering(oldName);
                default:
                    return oldName;
            }
        }

        private static string StripPrefixSuffix(string value, string prefix, string suffix)
        {
            string result = value ?? string.Empty;
            if (!string.IsNullOrEmpty(prefix))
            {
                while (result.StartsWith(prefix, StringComparison.Ordinal))
                    result = result.Substring(prefix.Length);
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                while (result.EndsWith(suffix, StringComparison.Ordinal))
                    result = result.Substring(0, result.Length - suffix.Length);
            }

            return result.Trim();
        }

        private static string RemoveTrailingNumbering(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            string result = Regex.Replace(value, @"\s*\(\d+\)$", string.Empty);
            result = Regex.Replace(result, @"\s*[#_\- ]\d+$", string.Empty);
            return result.TrimEnd();
        }

        private int CountChanges()
        {
            int count = 0;
            for (int i = 0; i < _preview.Count; i++)
            {
                RenamePreview row = _preview[i];
                if (row.valid && row.target != null && row.oldName != row.newName)
                    count++;
            }
            return count;
        }

        private void ApplyRename()
        {
            RebuildPreview();

            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < _preview.Count; i++)
                {
                    RenamePreview row = _preview[i];
                    if (!row.valid || row.target == null || row.oldName == row.newName)
                        continue;

                    if (row.asset)
                    {
                        string path = AssetDatabase.GetAssetPath(row.target);
                        string assetName = _keepAssetExtension ? StripExtension(row.newName) : row.newName;
                        string error = AssetDatabase.RenameAsset(path, assetName);
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogWarning($"[BulkRename] Failed to rename asset '{path}' to '{assetName}': {error}", row.target);
                            continue;
                        }
                    }
                    else
                    {
                        Undo.RecordObject(row.target, "Bulk Rename");
                        row.target.name = row.newName;
                        EditorUtility.SetDirty(row.target);
                    }

                    changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            _status = $"Renamed {changed} object(s).";
            RebuildPreview();
            Repaint();
        }

        private static bool IsProjectAsset(UnityObject target)
        {
            if (target == null)
                return false;

            string path = AssetDatabase.GetAssetPath(target);
            return !string.IsNullOrEmpty(path) && !AssetDatabase.IsSubAsset(target);
        }

        private static string StripExtension(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            int slash = Mathf.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            int dot = name.LastIndexOf('.');
            if (dot > slash)
                return name.Substring(0, dot);
            return name;
        }

        private void LoadPrefs()
        {
            _mode = (RenameMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefMode, (int)_mode), 0, Enum.GetValues(typeof(RenameMode)).Length - 1);
            _baseName = UtilityWindowPrefs.GetString(PrefBaseName, _baseName);
            _prefix = UtilityWindowPrefs.GetString(PrefPrefixText, _prefix);
            _suffix = UtilityWindowPrefs.GetString(PrefSuffixText, _suffix);
            _find = UtilityWindowPrefs.GetString(PrefFindText, _find);
            _replace = UtilityWindowPrefs.GetString(PrefReplaceText, _replace);
            _separator = UtilityWindowPrefs.GetString(PrefSeparator, _separator);
            _startIndex = UtilityWindowPrefs.GetInt(PrefStartIndex, _startIndex);
            _indexPadding = UtilityWindowPrefs.GetInt(PrefPadding, _indexPadding);
            _useSelectionLive = UtilityWindowPrefs.GetBool(PrefUseSelectionLive, _useSelectionLive);
            _renameAssets = UtilityWindowPrefs.GetBool(PrefRenameAssets, _renameAssets);
            _keepAssetExtension = UtilityWindowPrefs.GetBool(PrefKeepAssetExtension, _keepAssetExtension);
            _targetsHeight = UtilityWindowPrefs.GetFloat(PrefTargetsHeight, _targetsHeight);
            _operationHeight = UtilityWindowPrefs.GetFloat(PrefOperationHeight, _operationHeight);
            _previewHeight = UtilityWindowPrefs.GetFloat(PrefPreviewHeight, _previewHeight);
            _targetsFoldout = UtilityWindowPrefs.GetBool(PrefTargetsFoldout, _targetsFoldout);
            _operationFoldout = UtilityWindowPrefs.GetBool(PrefOperationFoldout, _operationFoldout);
            _previewFoldout = UtilityWindowPrefs.GetBool(PrefPreviewFoldout, _previewFoldout);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetInt(PrefMode, (int)_mode);
            UtilityWindowPrefs.SetString(PrefBaseName, _baseName);
            UtilityWindowPrefs.SetString(PrefPrefixText, _prefix);
            UtilityWindowPrefs.SetString(PrefSuffixText, _suffix);
            UtilityWindowPrefs.SetString(PrefFindText, _find);
            UtilityWindowPrefs.SetString(PrefReplaceText, _replace);
            UtilityWindowPrefs.SetString(PrefSeparator, _separator);
            UtilityWindowPrefs.SetInt(PrefStartIndex, _startIndex);
            UtilityWindowPrefs.SetInt(PrefPadding, _indexPadding);
            UtilityWindowPrefs.SetBool(PrefUseSelectionLive, _useSelectionLive);
            UtilityWindowPrefs.SetBool(PrefRenameAssets, _renameAssets);
            UtilityWindowPrefs.SetBool(PrefKeepAssetExtension, _keepAssetExtension);
            UtilityWindowPrefs.SetFloat(PrefTargetsHeight, _targetsHeight);
            UtilityWindowPrefs.SetFloat(PrefOperationHeight, _operationHeight);
            UtilityWindowPrefs.SetFloat(PrefPreviewHeight, _previewHeight);
            UtilityWindowPrefs.SetBool(PrefTargetsFoldout, _targetsFoldout);
            UtilityWindowPrefs.SetBool(PrefOperationFoldout, _operationFoldout);
            UtilityWindowPrefs.SetBool(PrefPreviewFoldout, _previewFoldout);
        }
    }
    #endif

}
