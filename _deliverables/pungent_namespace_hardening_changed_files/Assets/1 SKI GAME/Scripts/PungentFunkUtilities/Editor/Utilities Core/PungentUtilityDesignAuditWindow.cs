using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor-facing report window for validating current utilities against the architecture/design bible.
    /// </summary>
    public sealed class PungentUtilityDesignAuditWindow : EditorWindow
    {
        private const string PrefPrefix = "PungentFunkUtilities.DesignAudit.";
        private const string PrefIncludeInfo = PrefPrefix + "IncludeInfo";
        private const string PrefSeverity = PrefPrefix + "Severity";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefGroupByArea = PrefPrefix + "GroupByArea";

        private Vector2 _scroll;
        private bool _includeInfo;
        private bool _groupByArea;
        private int _severityIndex;
        private string _search = string.Empty;
        private string _status = "Run an audit to validate the package against design-bible rules.";
        private PungentUtilityDesignAudit.Report _report;

        private static readonly string[] SeverityLabels = { "All", "Errors", "Warnings", "Info" };

        [MenuItem("Tools/Utilities/Core/Design Validation Audit", priority = -180)]
        public static void Open()
        {
            PungentUtilityDesignAuditWindow window = GetWindow<PungentUtilityDesignAuditWindow>("Design Validation");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Design Validation");
            _includeInfo = UtilityWindowPrefs.GetBool(PrefIncludeInfo, false);
            _groupByArea = UtilityWindowPrefs.GetBool(PrefGroupByArea, true);
            _severityIndex = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefSeverity, 0), 0, SeverityLabels.Length - 1);
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            RunAudit();
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Design Validation Audit",
                "Validate registered utilities, EditorWindow layout signals, menu taxonomy, CreateAssetMenu roots, namespaces, and asmdef readiness.",
                _status);

            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSummary();
            DrawIssueList();
            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Audit Controls", UtilityWindowTheme.Blue);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Run Audit", UtilityWindowTheme.Green, GUILayout.Width(104f), GUILayout.Height(24f)))
                        RunAudit();

                    if (UtilityWindowTheme.TintedButton("Copy Summary", UtilityWindowTheme.Cyan, GUILayout.Width(118f), GUILayout.Height(24f)))
                        CopySummary();

                    if (GUILayout.Button(new GUIContent("Open Design Bible", "Opens the bundled architecture/design bible if present."), GUILayout.Width(140f), GUILayout.Height(24f)))
                        PungentUtilityDesignAudit.OpenDocumentation("Architecture_Design_Bible");

                    if (GUILayout.Button(new GUIContent("Open Inventory", "Opens the bundled audit and feature inventory if present."), GUILayout.Width(116f), GUILayout.Height(24f)))
                        PungentUtilityDesignAudit.OpenDocumentation("Audit_and_Feature_Inventory");

                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _includeInfo = EditorGUILayout.ToggleLeft(new GUIContent("Include info rows", "Info rows are useful for a full review, but warnings/errors are better for update passes."), _includeInfo, GUILayout.Width(124f));
                    _groupByArea = EditorGUILayout.ToggleLeft(new GUIContent("Group by area", "Group audit rows by architecture area instead of one flat list."), _groupByArea, GUILayout.Width(112f));

                    EditorGUILayout.LabelField("Severity", GUILayout.Width(54f));
                    _severityIndex = EditorGUILayout.Popup(_severityIndex, SeverityLabels, GUILayout.Width(104f));

                    EditorGUILayout.LabelField("Search", GUILayout.Width(44f));
                    _search = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle);
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(52f)))
                        _search = string.Empty;
                }
            }
        }

        private void DrawSummary()
        {
            if (_report == null)
            {
                EditorGUILayout.HelpBox("No audit report is available yet.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(_report.HasBlockingIssues ? UtilityWindowTheme.Red : UtilityWindowTheme.Green)))
            {
                UtilityWindowTheme.SectionTitle("At a Glance", _report.HasBlockingIssues ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, _report.generatedUtc.ToString("u"));
                EditorGUILayout.LabelField("Use this report as a gate before adding new feature-heavy labs. It intentionally prioritizes architecture, layout, and package-readiness signals.", UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_report.ErrorCount + " Errors", UtilityWindowTheme.Red, 88f);
                    UtilityWindowTheme.CountPill(_report.WarningCount + " Warnings", UtilityWindowTheme.Amber, 104f);
                    UtilityWindowTheme.CountPill(_report.InfoCount + " Info", UtilityWindowTheme.Cyan, 82f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_report.scriptCount + " Scripts", UtilityWindowTheme.Neutral, 92f);
                    UtilityWindowTheme.CountPill(_report.editorWindowCount + " Windows", UtilityWindowTheme.Blue, 94f);
                    UtilityWindowTheme.CountPill(_report.descriptorCount + " Registered", UtilityWindowTheme.Teal, 112f);
                    UtilityWindowTheme.CountPill(_report.asmdefCount + " Asmdefs", _report.asmdefCount == 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 96f);
                    UtilityWindowTheme.CountPill(_report.namespaceDeclarationCount + " Namespaced", _report.namespaceDeclarationCount == 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Green, 116f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawIssueList()
        {
            if (_report == null)
                return;

            PungentUtilityDesignAudit.Severity? severity = GetSeverityFilter();
            List<PungentUtilityDesignAudit.Issue> filtered = _report.Filter(severity, _search).ToList();

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Findings", UtilityWindowTheme.Teal, filtered.Count + " shown");

                if (filtered.Count == 0)
                {
                    EditorGUILayout.HelpBox("No findings match the current filter.", MessageType.Info);
                    return;
                }

                if (_groupByArea)
                {
                    foreach (IGrouping<string, PungentUtilityDesignAudit.Issue> group in filtered.GroupBy(i => i.area).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        EditorGUILayout.Space(4f);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                            UtilityWindowTheme.CountPill(group.Count() + " findings", UtilityWindowTheme.Neutral, 92f);
                        }

                        foreach (PungentUtilityDesignAudit.Issue issue in group)
                            DrawIssue(issue);
                    }
                }
                else
                {
                    foreach (PungentUtilityDesignAudit.Issue issue in filtered)
                        DrawIssue(issue);
                }
            }
        }

        private void DrawIssue(PungentUtilityDesignAudit.Issue issue)
        {
            Color tint = GetTint(issue.severity);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.15f, 0.08f, 7, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(issue.severity.ToString(), tint, 78f);
                    EditorGUILayout.LabelField(issue.target, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (!string.IsNullOrWhiteSpace(issue.assetPath) && GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(48f)))
                        PingAsset(issue.assetPath);
                }

                EditorGUILayout.LabelField(issue.message, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrWhiteSpace(issue.recommendation))
                    EditorGUILayout.LabelField("Recommendation: " + issue.recommendation, UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(issue.assetPath))
                    EditorGUILayout.LabelField(issue.assetPath, UtilityWindowTheme.PathLabelStyle);
            }
        }

        private void RunAudit()
        {
            _report = PungentUtilityDesignAudit.Run(_includeInfo);
            _status = _report.ErrorCount + " errors · " + _report.WarningCount + " warnings · " + _report.InfoCount + " info";
            Repaint();
        }

        private void CopySummary()
        {
            if (_report == null)
                RunAudit();

            EditorGUIUtility.systemCopyBuffer = _report != null ? _report.ToMarkdownSummary() : string.Empty;
            _status = "Copied audit summary to clipboard.";
        }

        private PungentUtilityDesignAudit.Severity? GetSeverityFilter()
        {
            switch (_severityIndex)
            {
                case 1: return PungentUtilityDesignAudit.Severity.Error;
                case 2: return PungentUtilityDesignAudit.Severity.Warning;
                case 3: return PungentUtilityDesignAudit.Severity.Info;
                default: return null;
            }
        }

        private static Color GetTint(PungentUtilityDesignAudit.Severity severity)
        {
            switch (severity)
            {
                case PungentUtilityDesignAudit.Severity.Error:
                    return UtilityWindowTheme.Red;
                case PungentUtilityDesignAudit.Severity.Warning:
                    return UtilityWindowTheme.Amber;
                default:
                    return UtilityWindowTheme.Cyan;
            }
        }

        private static void PingAsset(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefIncludeInfo, _includeInfo);
            UtilityWindowPrefs.SetBool(PrefGroupByArea, _groupByArea);
            UtilityWindowPrefs.SetInt(PrefSeverity, _severityIndex);
            UtilityWindowPrefs.SetString(PrefSearch, _search);
        }
    }
    #endif

}