using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class PungentCoverageMatrixWindow : EditorWindow
    {
        private enum CoverageStatus { Missing, Covered, Duplicate, Ambiguous, Invalid, Unused }
        private enum Template { Custom, AudioCueCoverage, InputIconCoverage, TokenCoverage, PrefabVariantCoverage, SceneAnchorCoverage }

        [Serializable]
        private sealed class CoverageEntry
        {
            public string row;
            public string column;
            public CoverageStatus status = CoverageStatus.Covered;
            public string note;
            public string assetPath;
        }

        private readonly List<string> _rows = new List<string> { "Category A", "Category B", "Fallback" };
        private readonly List<string> _columns = new List<string> { "Asset", "Icon", "Audio", "Notes" };
        private readonly List<CoverageEntry> _entries = new List<CoverageEntry>();
        private Vector2 _matrixScroll;
        private Vector2 _sideScroll;
        private Template _template = Template.Custom;
        private string _newRow = string.Empty;
        private string _newColumn = string.Empty;
        private string _scanTypeName = "ScriptableObject";
        private string _rowNameContains = string.Empty;
        private string _columnPathContains = string.Empty;
        private CoverageEntry _selected;
        private string _status = "Ready.";

        [MenuItem("Tools/Utilities/Generation/Coverage Matrix", priority = 1320)]
        public static void Open()
        {
            PungentCoverageMatrixWindow window = GetWindow<PungentCoverageMatrixWindow>("Coverage Matrix");
            window.minSize = new Vector2(760f, 460f);
            window.Show();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header("Coverage Matrix", "Define expected authoring coverage, scan selected/project assets into matrix cells, and inspect missing, duplicate, ambiguous, or invalid combinations.", _status);
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMatrix(GUILayout.MinWidth(470f));
                DrawDetails(GUILayout.Width(Mathf.Clamp(position.width * 0.35f, 260f, 380f)));
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _template = (Template)EditorGUILayout.EnumPopup("Template", _template);
                    if (UtilityWindowTheme.TintedButton("Apply Template", UtilityWindowTheme.Teal, GUILayout.Width(112f)))
                        ApplyTemplate(_template);
                    if (GUILayout.Button("Clear Entries", GUILayout.Width(92f)) && EditorUtility.DisplayDialog("Clear Coverage Entries", "Clear all marked coverage cells?", "Clear", "Cancel"))
                        _entries.Clear();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _newRow = EditorGUILayout.TextField("Row", _newRow);
                    if (GUILayout.Button("Add Row", GUILayout.Width(82f)) && !string.IsNullOrWhiteSpace(_newRow))
                        AddUnique(_rows, ref _newRow);
                    _newColumn = EditorGUILayout.TextField("Column", _newColumn);
                    if (GUILayout.Button("Add Column", GUILayout.Width(92f)) && !string.IsNullOrWhiteSpace(_newColumn))
                        AddUnique(_columns, ref _newColumn);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _scanTypeName = EditorGUILayout.TextField("Scan Type", _scanTypeName);
                    _rowNameContains = EditorGUILayout.TextField("Row Name Contains", _rowNameContains, GUILayout.Width(180f));
                    _columnPathContains = EditorGUILayout.TextField("Column Path Contains", _columnPathContains, GUILayout.Width(190f));
                    if (GUILayout.Button("Scan Selection", GUILayout.Width(104f)))
                        ScanSelection();
                    if (GUILayout.Button("Scan Project", GUILayout.Width(94f)))
                        ScanProject();
                }
            }
        }

        private void DrawMatrix(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal), options))
            {
                int expected = _rows.Count * _columns.Count;
                int covered = _entries.Count(e => e.status == CoverageStatus.Covered);
                UtilityWindowTheme.SectionTitle("Matrix", UtilityWindowTheme.Teal, covered + "/" + expected + " covered");

                _matrixScroll = EditorGUILayout.BeginScrollView(_matrixScroll);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(130f);
                    foreach (string col in _columns)
                        GUILayout.Label(col, EditorStyles.boldLabel, GUILayout.Width(116f));
                }

                foreach (string row in _rows)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(row, EditorStyles.boldLabel, GUILayout.Width(130f));
                        foreach (string col in _columns)
                        {
                            CoverageEntry entry = _entries.FirstOrDefault(e => e.row == row && e.column == col);
                            CoverageStatus cellStatus = entry != null ? entry.status : CoverageStatus.Missing;
                            using (UtilityWindowTheme.Background(StatusColor(cellStatus)))
                            {
                                if (GUILayout.Button(StatusLabel(cellStatus), GUILayout.Width(116f)))
                                {
                                    if (entry == null)
                                    {
                                        entry = new CoverageEntry { row = row, column = col, status = CoverageStatus.Covered, note = "Manual" };
                                        _entries.Add(entry);
                                    }
                                    _selected = entry;
                                }
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDetails(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple), options))
            {
                UtilityWindowTheme.SectionTitle("Cell Details", UtilityWindowTheme.Purple, _selected != null ? _selected.row + " / " + _selected.column : "None");
                _sideScroll = EditorGUILayout.BeginScrollView(_sideScroll);
                DrawSummary();
                EditorGUILayout.Space(8f);

                if (_selected == null)
                {
                    EditorGUILayout.HelpBox("Click a matrix cell to inspect or edit coverage metadata. Missing cells become editable entries when clicked.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                _selected.row = EditorGUILayout.TextField("Row", _selected.row);
                _selected.column = EditorGUILayout.TextField("Column", _selected.column);
                _selected.status = (CoverageStatus)EditorGUILayout.EnumPopup("Status", _selected.status);
                _selected.note = EditorGUILayout.TextArea(_selected.note, GUILayout.MinHeight(70f));
                EditorGUILayout.SelectableLabel(_selected.assetPath ?? string.Empty, UtilityWindowTheme.PathLabelStyle, GUILayout.Height(20f));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Ping Asset"))
                        PingSelectedAsset();
                    if (GUILayout.Button("Delete Entry") && EditorUtility.DisplayDialog("Delete Coverage Entry", "Delete this coverage entry?", "Delete", "Cancel"))
                    {
                        _entries.Remove(_selected);
                        _selected = null;
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSummary()
        {
            int missing = _rows.Count * _columns.Count - _entries.Select(e => e.row + "|" + e.column).Distinct().Count();
            UtilityWindowTheme.CountPill("Missing " + Mathf.Max(0, missing), UtilityWindowTheme.Amber, 90f);
            UtilityWindowTheme.CountPill("Duplicates " + _entries.GroupBy(e => e.row + "|" + e.column).Count(g => g.Count() > 1), UtilityWindowTheme.Red, 100f);
            UtilityWindowTheme.CountPill("Invalid " + _entries.Count(e => e.status == CoverageStatus.Invalid), UtilityWindowTheme.Red, 80f);
            EditorGUILayout.Space(4f);
        }

        private void ApplyTemplate(Template template)
        {
            _rows.Clear();
            _columns.Clear();
            switch (template)
            {
                case Template.AudioCueCoverage:
                    _rows.AddRange(new[] { "Impact", "Loop", "Start", "Stop", "UI" });
                    _columns.AddRange(new[] { "Clip", "Mixer", "Fallback", "Volume" });
                    break;
                case Template.InputIconCoverage:
                    _rows.AddRange(new[] { "Keyboard", "Mouse", "Gamepad", "Touch" });
                    _columns.AddRange(new[] { "Jump", "Interact", "Cancel", "Navigate" });
                    break;
                case Template.TokenCoverage:
                    _rows.AddRange(new[] { "Token Defined", "Resolver", "Preview Value", "Usage" });
                    _columns.AddRange(new[] { "UI", "Dialogue", "Tooltip", "Docs" });
                    break;
                case Template.PrefabVariantCoverage:
                    _rows.AddRange(new[] { "Common", "Uncommon", "Rare", "Fallback" });
                    _columns.AddRange(new[] { "Default", "Biome A", "Biome B", "Night" });
                    break;
                case Template.SceneAnchorCoverage:
                    _rows.AddRange(new[] { "Waypoint", "Point Of Interest", "Spawn", "Interactable" });
                    _columns.AddRange(new[] { "Label", "Icon", "Gizmo", "Metadata" });
                    break;
                default:
                    _rows.AddRange(new[] { "Category A", "Category B", "Fallback" });
                    _columns.AddRange(new[] { "Asset", "Icon", "Audio", "Notes" });
                    break;
            }
            _status = "Applied " + template + " template.";
        }

        private void ScanSelection()
        {
            MarkAssets(Selection.objects);
            _status = "Scanned selected assets.";
        }

        private void ScanProject()
        {
            string filter = string.IsNullOrWhiteSpace(_scanTypeName) ? "t:Object" : "t:" + _scanTypeName;
            string[] guids = AssetDatabase.FindAssets(filter);
            List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            foreach (string guid in guids.Take(500))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj != null)
                    assets.Add(obj);
            }
            MarkAssets(assets.ToArray());
            _status = "Scanned " + assets.Count + " project asset(s).";
        }

        private void MarkAssets(UnityEngine.Object[] objects)
        {
            foreach (UnityEngine.Object obj in objects)
            {
                if (obj == null || _rows.Count == 0 || _columns.Count == 0)
                    continue;
                string path = AssetDatabase.GetAssetPath(obj);
                string row = PickAxis(_rows, obj.name, _rowNameContains);
                string column = PickAxis(_columns, path, _columnPathContains);
                CoverageEntry entry = _entries.FirstOrDefault(e => e.row == row && e.column == column);
                if (entry == null)
                {
                    _entries.Add(new CoverageEntry { row = row, column = column, status = CoverageStatus.Covered, note = obj.name, assetPath = path });
                }
                else
                {
                    entry.status = CoverageStatus.Duplicate;
                    entry.note = (entry.note ?? string.Empty) + "\nDuplicate candidate: " + obj.name;
                }
            }
        }

        private static string PickAxis(List<string> axis, string source, string contains)
        {
            if (!string.IsNullOrWhiteSpace(contains))
            {
                string match = axis.FirstOrDefault(a => source.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0 || source.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(match))
                    return match;
            }
            return axis[Mathf.Abs((source ?? string.Empty).GetHashCode()) % axis.Count];
        }

        private void PingSelectedAsset()
        {
            if (_selected == null || string.IsNullOrEmpty(_selected.assetPath))
                return;
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_selected.assetPath);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        private static void AddUnique(List<string> list, ref string value)
        {
            string trimmed = value.Trim();
            if (!list.Contains(trimmed))
                list.Add(trimmed);
            value = string.Empty;
        }

        private static Color StatusColor(CoverageStatus status)
        {
            switch (status)
            {
                case CoverageStatus.Covered: return UtilityWindowTheme.Green;
                case CoverageStatus.Duplicate: return UtilityWindowTheme.Amber;
                case CoverageStatus.Ambiguous: return UtilityWindowTheme.Purple;
                case CoverageStatus.Invalid: return UtilityWindowTheme.Red;
                case CoverageStatus.Unused: return UtilityWindowTheme.Neutral;
                default: return UtilityWindowTheme.Amber;
            }
        }

        private static string StatusLabel(CoverageStatus status)
        {
            switch (status)
            {
                case CoverageStatus.Covered: return "✓ Covered";
                case CoverageStatus.Duplicate: return "Duplicate";
                case CoverageStatus.Ambiguous: return "Ambiguous";
                case CoverageStatus.Invalid: return "Invalid";
                case CoverageStatus.Unused: return "Unused";
                default: return "Missing";
            }
        }
    }
    #endif

}