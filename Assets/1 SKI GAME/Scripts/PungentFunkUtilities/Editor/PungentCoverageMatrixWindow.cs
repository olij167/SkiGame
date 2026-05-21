using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;

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
        private readonly PungentScanSession _scanSession = new PungentScanSession("coverage-matrix", "Coverage Matrix");
        private string _resultSourceBanner = string.Empty;

        public static void Open()
        {
            PungentCoverageMatrixWindow window = GetWindow<PungentCoverageMatrixWindow>("Coverage Matrix");
            window.minSize = new Vector2(760f, 460f);
            window.Show();
        }

        public static bool RunCoordinatorScan(out string status)
        {
            status = "Coverage Matrix audit did not run.";
            PungentCoverageMatrixWindow window = null;
            bool destroyWhenDone = false;
            try
            {
                window = GetCoordinatorInstance(out destroyWhenDone);
                window.RunMatrixAudit();
                status = window._status;
                return true;
            }
            catch (Exception ex)
            {
                status = "Coverage Matrix audit failed: " + ex.Message;
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                if (destroyWhenDone && window != null)
                    DestroyImmediate(window);
            }
        }

        public static bool TryGetCoordinatorNotConfiguredReason(out string reason)
        {
            PungentCoverageMatrixWindow window = FindOpenWindow();
            if (window != null && (window._rows.Count == 0 || window._columns.Count == 0))
            {
                reason = "Coverage Matrix needs at least one row and one column.";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public static PungentAuditScanJob CreateAuditJob(PungentAuditScanMode mode)
        {
            PungentCoverageMatrixWindow window = FindOpenWindow();
            List<string> rows = window != null ? new List<string>(window._rows) : new List<string> { "Category A", "Category B", "Fallback" };
            List<string> columns = window != null ? new List<string>(window._columns) : new List<string> { "Asset", "Icon", "Audio", "Notes" };
            List<CoverageEntry> entries = window != null ? new List<CoverageEntry>(window._entries) : new List<CoverageEntry>();
            bool complete = false;

            PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative("coverage-matrix", "Coverage Matrix", context =>
            {
                if (complete)
                    return PungentAuditScanStepResult.Complete("Coverage Matrix audit already completed.");

                if (context.IsCancellationRequested())
                    return PungentAuditScanStepResult.Cancelled("Coverage Matrix audit cancelled before evaluation.");

                PungentScanSession session = new PungentScanSession("coverage-matrix", "Coverage Matrix");
                PungentScanResult result = session.Begin(PungentScanScope.Custom, "Current Matrix");
                context.Report(0.25f, 0, Math.Max(1, rows.Count * columns.Count), "Evaluating current matrix.", false, "Matrix audit");

                if (rows.Count == 0 || columns.Count == 0)
                {
                    result.AddIssue(PungentScanSeverity.Warning, "Not configured", "Coverage Matrix needs at least one row and one column before it can summarize coverage.", null, null, "COVERAGE_MATRIX_NOT_CONFIGURED");
                    PungentScanResult notConfigured = session.Complete(0, 0, 0, 0, "Coverage Matrix is not configured.");
                    complete = true;
                    return PungentAuditScanStepResult.NotConfigured("Coverage Matrix is not configured.", notConfigured);
                }

                int expected = rows.Count * columns.Count;
                int distinctCovered = entries.Select(e => e.row + "|" + e.column).Distinct().Count();
                int missing = Mathf.Max(0, expected - distinctCovered);
                int duplicates = entries.GroupBy(e => e.row + "|" + e.column).Count(g => g.Count() > 1);
                int invalid = entries.Count(e => e.status == CoverageStatus.Invalid);
                int ambiguous = entries.Count(e => e.status == CoverageStatus.Ambiguous);
                int unused = entries.Count(e => e.status == CoverageStatus.Unused);

                if (missing > 0)
                    result.AddIssue(PungentScanSeverity.Info, "Missing matrix cells", missing + " expected matrix cell(s) are still unmarked.", null, null, "COVERAGE_MISSING_CELLS");
                if (duplicates > 0)
                    result.AddIssue(PungentScanSeverity.Warning, "Duplicate coverage cells", duplicates + " matrix cell(s) have duplicate coverage entries.", null, null, "COVERAGE_DUPLICATE_CELL");
                if (invalid > 0)
                    result.AddIssue(PungentScanSeverity.Error, "Invalid coverage cells", invalid + " matrix cell(s) are marked invalid.", null, null, "COVERAGE_INVALID_CELLS");
                if (ambiguous > 0)
                    result.AddIssue(PungentScanSeverity.Warning, "Ambiguous coverage cells", ambiguous + " matrix cell(s) are marked ambiguous.", null, null, "COVERAGE_AMBIGUOUS_CELLS");
                if (unused > 0)
                    result.AddIssue(PungentScanSeverity.Info, "Unused coverage cells", unused + " matrix cell(s) are marked unused.", null, null, "COVERAGE_UNUSED_CELLS");
                if (missing == 0 && duplicates == 0 && invalid == 0 && ambiguous == 0)
                    result.AddIssue(PungentScanSeverity.Success, "Matrix coverage complete", "All expected matrix cells have coverage and no duplicate, invalid, or ambiguous cells are marked.", null, null, "COVERAGE_COMPLETE");

                string status = "Audited current matrix: " + distinctCovered + "/" + expected + " cell(s) covered.";
                PungentScanResult completed = session.Complete(expected, distinctCovered, missing, duplicates, status);
                context.Report(1f, expected, expected, status, false, "Complete");
                complete = true;
                return PungentAuditScanStepResult.Complete(status, completed);
            });
            job.canPause = false;
            job.canCancel = true;
            job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                               PungentAuditScanJobCapabilities.BackgroundSafe |
                               PungentAuditScanJobCapabilities.ScanOnly;
            job.Report(0f, 0, Math.Max(1, rows.Count * columns.Count), "Queued current-matrix audit.", false, "Queued");
            return job;
        }

        private static PungentCoverageMatrixWindow GetCoordinatorInstance(out bool destroyWhenDone)
        {
            PungentCoverageMatrixWindow[] existing = Resources.FindObjectsOfTypeAll<PungentCoverageMatrixWindow>();
            if (existing != null && existing.Length > 0)
            {
                destroyWhenDone = false;
                return existing[0];
            }
            destroyWhenDone = true;
            return CreateInstance<PungentCoverageMatrixWindow>();
        }

        private static PungentCoverageMatrixWindow FindOpenWindow()
        {
            PungentCoverageMatrixWindow[] existing = Resources.FindObjectsOfTypeAll<PungentCoverageMatrixWindow>();
            return existing != null && existing.Length > 0 ? existing[0] : null;
        }

        private void OnEnable()
        {
            if (PungentScanCache.TryHydrateSession(_scanSession, out _resultSourceBanner) && _scanSession.Result != null)
                _status = _scanSession.Result.StatusMessage;
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
                        QueueScanSelection();
                    if (GUILayout.Button("Scan Project", GUILayout.Width(94f)))
                        QueueScanProject();
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
            PungentScanGUI.DrawResultHeader(_scanSession.Result, _resultSourceBanner);
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

        private void QueueScanSelection()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    ScanSelection();
            };
        }

        private void QueueScanProject()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    ScanProject();
            };
        }

        private void ScanSelection()
        {
            PungentScanResult result = _scanSession.Begin(PungentScanScope.Selection, "Selection");
            try
            {
                MarkStats stats = MarkAssets(Selection.objects, result);
                _status = "Scanned " + stats.Scanned + " selected asset(s).";
                CompleteScan(result, stats, _status);
            }
            catch (Exception ex)
            {
                _status = "Selection scan failed: " + ex.Message;
                _scanSession.Fail(ex, _status);
                Debug.LogException(ex);
            }
        }

        private void ScanProject()
        {
            PungentScanResult result = _scanSession.Begin(PungentScanScope.ProjectAssets, string.IsNullOrWhiteSpace(_scanTypeName) ? "Project Assets" : "Project " + _scanTypeName);
            try
            {
                string filter = string.IsNullOrWhiteSpace(_scanTypeName) ? "t:Object" : "t:" + _scanTypeName;
                string[] guids = AssetDatabase.FindAssets(filter);
                List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
                int loadLimit = Mathf.Min(guids.Length, 500);
                for (int i = 0; i < loadLimit; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (obj != null)
                        assets.Add(obj);
                    else
                        result.AddIssue(PungentScanSeverity.Warning, "Asset did not load", "A matching asset GUID did not resolve to a loadable Unity object.", null, path, "COVERAGE_ASSET_LOAD_FAILED");
                }

                if (guids.Length > loadLimit)
                {
                    result.AddIssue(
                        PungentScanSeverity.Warning,
                        "Project scan capped",
                        "The scan matched " + guids.Length + " assets. Only the first " + loadLimit + " were loaded to keep the editor responsive.",
                        null,
                        null,
                        "COVERAGE_SCAN_CAPPED");
                }

                MarkStats stats = MarkAssets(assets.ToArray(), result);
                stats.Scanned = guids.Length;
                stats.Skipped += Mathf.Max(0, guids.Length - assets.Count);
                _status = "Scanned " + assets.Count + " project asset(s).";
                CompleteScan(result, stats, _status);
            }
            catch (Exception ex)
            {
                _status = "Project scan failed: " + ex.Message;
                _scanSession.Fail(ex, _status);
                Debug.LogException(ex);
            }
        }

        private void RunMatrixAudit()
        {
            PungentScanResult result = _scanSession.Begin(PungentScanScope.Custom, "Current Matrix");
            try
            {
                if (_rows.Count == 0 || _columns.Count == 0)
                {
                    result.AddIssue(PungentScanSeverity.Warning, "Not configured", "Coverage Matrix needs at least one row and one column before it can summarize coverage.", null, null, "COVERAGE_MATRIX_NOT_CONFIGURED");
                    _status = "Coverage Matrix is not configured.";
                    _scanSession.Complete(0, 0, 0, 0, _status);
                    return;
                }

                int expected = _rows.Count * _columns.Count;
                int distinctCovered = _entries.Select(e => e.row + "|" + e.column).Distinct().Count();
                int missing = Mathf.Max(0, expected - distinctCovered);
                int duplicates = _entries.GroupBy(e => e.row + "|" + e.column).Count(g => g.Count() > 1);
                int invalid = _entries.Count(e => e.status == CoverageStatus.Invalid);
                int ambiguous = _entries.Count(e => e.status == CoverageStatus.Ambiguous);
                int unused = _entries.Count(e => e.status == CoverageStatus.Unused);

                if (missing > 0)
                    result.AddIssue(PungentScanSeverity.Info, "Missing matrix cells", missing + " expected matrix cell(s) are still unmarked.", null, null, "COVERAGE_MISSING_CELLS");
                if (duplicates > 0)
                    result.AddIssue(PungentScanSeverity.Warning, "Duplicate coverage cells", duplicates + " matrix cell(s) have duplicate coverage entries.", null, null, "COVERAGE_DUPLICATE_CELL");
                if (invalid > 0)
                    result.AddIssue(PungentScanSeverity.Error, "Invalid coverage cells", invalid + " matrix cell(s) are marked invalid.", null, null, "COVERAGE_INVALID_CELLS");
                if (ambiguous > 0)
                    result.AddIssue(PungentScanSeverity.Warning, "Ambiguous coverage cells", ambiguous + " matrix cell(s) are marked ambiguous.", null, null, "COVERAGE_AMBIGUOUS_CELLS");
                if (unused > 0)
                    result.AddIssue(PungentScanSeverity.Info, "Unused coverage cells", unused + " matrix cell(s) are marked unused.", null, null, "COVERAGE_UNUSED_CELLS");
                if (missing == 0 && duplicates == 0 && invalid == 0 && ambiguous == 0)
                    result.AddIssue(PungentScanSeverity.Success, "Matrix coverage complete", "All expected matrix cells have coverage and no duplicate, invalid, or ambiguous cells are marked.", null, null, "COVERAGE_COMPLETE");

                _status = "Audited current matrix: " + distinctCovered + "/" + expected + " cell(s) covered.";
                _scanSession.Complete(expected, distinctCovered, missing, duplicates, _status);
            }
            catch (Exception ex)
            {
                _status = "Coverage Matrix audit failed: " + ex.Message;
                _scanSession.Fail(ex, _status);
                Debug.LogException(ex);
            }
        }

        private MarkStats MarkAssets(UnityEngine.Object[] objects, PungentScanResult result)
        {
            MarkStats stats = new MarkStats();
            if (objects == null || objects.Length == 0)
            {
                result?.AddIssue(PungentScanSeverity.Warning, "No assets supplied", "There were no assets to mark into the coverage matrix.", null, null, "COVERAGE_NO_ASSETS");
                return stats;
            }

            if (_rows.Count == 0 || _columns.Count == 0)
            {
                stats.Skipped = objects.Length;
                result?.AddIssue(PungentScanSeverity.Error, "Matrix axes are empty", "Add at least one row and one column before scanning assets into the matrix.", null, null, "COVERAGE_EMPTY_AXES");
                return stats;
            }

            foreach (UnityEngine.Object obj in objects)
            {
                stats.Scanned++;
                if (obj == null)
                {
                    stats.Skipped++;
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(obj);
                string row = PickAxis(_rows, obj.name, _rowNameContains);
                string column = PickAxis(_columns, path, _columnPathContains);
                CoverageEntry entry = _entries.FirstOrDefault(e => e.row == row && e.column == column);
                if (entry == null)
                {
                    _entries.Add(new CoverageEntry { row = row, column = column, status = CoverageStatus.Covered, note = obj.name, assetPath = path });
                    stats.Created++;
                    stats.Marked++;
                }
                else
                {
                    entry.status = CoverageStatus.Duplicate;
                    entry.note = (entry.note ?? string.Empty) + "\nDuplicate candidate: " + obj.name;
                    stats.Duplicates++;
                    stats.Marked++;
                    result?.AddIssue(PungentScanSeverity.Warning, "Duplicate coverage candidate", obj.name + " maps to an already-covered cell: " + row + " / " + column + ".", obj, path, "COVERAGE_DUPLICATE_CELL");
                }
            }

            if (stats.Marked == 0 && stats.Skipped == 0)
                result?.AddIssue(PungentScanSeverity.Info, "No coverage matches", "No supplied assets produced coverage entries.", null, null, "COVERAGE_NO_MATCHES");

            return stats;
        }

        private void CompleteScan(PungentScanResult result, MarkStats stats, string status)
        {
            int missing = Mathf.Max(0, _rows.Count * _columns.Count - _entries.Select(e => e.row + "|" + e.column).Distinct().Count());
            if (missing > 0)
                result?.AddIssue(PungentScanSeverity.Info, "Missing matrix cells", missing + " expected matrix cell(s) are still unmarked.", null, null, "COVERAGE_MISSING_CELLS");
            else
                result?.AddIssue(PungentScanSeverity.Success, "Matrix coverage complete", "All expected matrix cells have at least one coverage entry.", null, null, "COVERAGE_COMPLETE");

            _scanSession.Complete(stats.Scanned, stats.Marked, stats.Skipped, stats.Created + stats.Duplicates, status);
        }

        private struct MarkStats
        {
            public int Scanned;
            public int Marked;
            public int Created;
            public int Duplicates;
            public int Skipped;
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

    internal sealed class CoverageMatrixAuditProvider : IPungentAuditScanProvider
    {
        public string ProviderId => "coverage-matrix";
        public string DisplayName => "Coverage Matrix";
        public string Description => "Track expected coverage dimensions, gaps, duplicates, and notes.";
        public string OpenButtonLabel => "Open Coverage Matrix";
        public string RunButtonLabel => "Run Coverage Matrix Audit";
        public bool CanRunImmediate => true;
        public bool CanRunBackground => true;
        public bool CanRunFromCoordinator => true;
        public bool CanPause => false;
        public bool CanCancel => true;
        public bool UsesSceneOpening => false;
        public bool UsesAssetDatabase => false;
        public bool UsesModalProgress => false;
        public bool IsCooperative => true;
        public bool IsMonolithic => false;
        public bool IsScanOnly => true;

        public bool TryGetNotConfiguredReason(out string reason)
        {
            return PungentCoverageMatrixWindow.TryGetCoordinatorNotConfiguredReason(out reason);
        }

        public PungentAuditScanJob CreateJob(PungentAuditScanMode mode)
        {
            return PungentCoverageMatrixWindow.CreateAuditJob(mode);
        }

        public void OpenWindow()
        {
            PungentCoverageMatrixWindow.Open();
        }
    }
#endif

}
