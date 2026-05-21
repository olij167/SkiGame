using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentPropagationPreviewWindow : EditorWindow
    {
        private Vector2 _scroll;
        private PungentRichDocument _document;
        private PungentRichDocumentExtractionResult _result;
        private string _status = "Review proposed outputs before applying anything.";

        public static void Open(PungentRichDocument document, PungentRichDocumentParsedDocument parsed = null)
        {
            PungentRichDocumentPropagationPreviewWindow window = GetWindow<PungentRichDocumentPropagationPreviewWindow>("Propagation Preview");
            window.minSize = new Vector2(620f, 420f);
            window.SetDocument(document, parsed);
            window.Show();
            window.Focus();
        }

        private void SetDocument(PungentRichDocument document, PungentRichDocumentParsedDocument parsed)
        {
            _document = document;
            _result = PungentRichDocumentExtractor.Extract(document, parsed);
            _status = _result.HasEntries ? "Review proposed outputs before applying anything." : "No propagatable text was found in this document.";
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_document == null)
            {
                EditorGUILayout.HelpBox("No rich document is selected.", MessageType.Info);
                return;
            }

            if (_result == null)
                SetDocument(_document, null);

            DrawSummary();
            DrawEntries();
            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(28f)))
            {
                EditorGUILayout.LabelField(_document == null ? "Propagation Preview" : _document.title, EditorStyles.toolbarButton, GUILayout.MinWidth(180f));
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_document == null))
                {
                    if (GUILayout.Button(new GUIContent("Refresh", "Re-read the current document only."), EditorStyles.toolbarButton, GUILayout.Width(64f)))
                        SetDocument(_document, null);
                }

                bool canApply = CanApplySelected();
                using (new EditorGUI.DisabledScope(!canApply))
                {
                    if (GUILayout.Button(new GUIContent("Apply Selected", "Explicitly apply selected token preview values and bound adapter integrations."), EditorStyles.toolbarButton, GUILayout.Width(104f)))
                        ApplySelected();
                }

                using (new EditorGUI.DisabledScope(_result == null || !_result.HasEntries))
                {
                    if (GUILayout.Button(new GUIContent("Copy Summary", "Copy a text summary of the proposed outputs."), EditorStyles.toolbarButton, GUILayout.Width(96f)))
                        CopySummary();
                }
            }
        }

        private void DrawSummary()
        {
            int total = _result == null || _result.entries == null ? 0 : _result.entries.Count;
            int applicable = _result == null || _result.entries == null ? 0 : _result.entries.Count(entry => entry != null && entry.canApply);
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(total + " outputs", UtilityWindowTheme.Cyan, 92f);
                UtilityWindowTheme.CountPill(applicable + " applicable", applicable > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 104f);
                UtilityWindowTheme.CountPill("current document", UtilityWindowTheme.Blue, 126f);
                GUILayout.FlexibleSpace();
            }

            if (_result != null && _result.warnings != null)
            {
                foreach (string warning in _result.warnings)
                    if (!string.IsNullOrWhiteSpace(warning))
                        EditorGUILayout.HelpBox(warning, MessageType.Info);
            }
        }

        private void DrawEntries()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try
            {
                if (_result == null || _result.entries == null || _result.entries.Count == 0)
                {
                    EditorGUILayout.HelpBox("No token, dialogue, quest, tutorial, command, or game-copy outputs were found.", MessageType.Info);
                    return;
                }

                foreach (IGrouping<string, PungentRichDocumentExtractedEntry> group in _result.entries
                             .Where(entry => entry != null)
                             .GroupBy(entry => string.IsNullOrWhiteSpace(entry.category) ? "Other" : entry.category)
                             .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                    foreach (PungentRichDocumentExtractedEntry entry in group)
                        DrawEntry(entry);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawEntry(PungentRichDocumentExtractedEntry entry)
        {
            if (entry == null)
                return;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(TintForCategory(entry.category), 0.08f, 0.04f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!entry.canApply))
                        entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(20f));

                    UtilityWindowTheme.CountPill(entry.category, TintForCategory(entry.category), 88f);
                    EditorGUILayout.LabelField(entry.title, EditorStyles.boldLabel, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("line " + entry.sourceLine, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(58f));
                }

                if (!string.IsNullOrWhiteSpace(entry.key))
                    EditorGUILayout.LabelField("Key", entry.key, UtilityWindowTheme.PathLabelStyle);

                EditorGUILayout.LabelField("Value", string.IsNullOrWhiteSpace(entry.value) ? "(empty)" : entry.value, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrWhiteSpace(entry.sourceText))
                    EditorGUILayout.LabelField("Source", entry.sourceText.Trim(), UtilityWindowTheme.MutedMiniLabelStyle);

                if (!entry.canApply && !string.IsNullOrWhiteSpace(entry.disabledReason))
                    EditorGUILayout.LabelField(entry.disabledReason, UtilityWindowTheme.MutedMiniLabelStyle);

                if (entry.target != null && entry.target.HasTarget)
                {
                    EditorGUILayout.LabelField("Target", entry.target.label, UtilityWindowTheme.PathLabelStyle);
                    if (!string.IsNullOrWhiteSpace(entry.adapterDisplayName))
                        EditorGUILayout.LabelField("Adapter", entry.adapterDisplayName, UtilityWindowTheme.MutedMiniLabelStyle);
                    if (!string.IsNullOrWhiteSpace(entry.currentValue) || string.IsNullOrWhiteSpace(entry.disabledReason))
                        EditorGUILayout.LabelField("Current", string.IsNullOrEmpty(entry.currentValue) ? "(empty)" : entry.currentValue, UtilityWindowTheme.MutedMiniLabelStyle);
                    if (!string.IsNullOrWhiteSpace(entry.currentValueStatus))
                        EditorGUILayout.LabelField(entry.currentValueStatus, UtilityWindowTheme.MutedMiniLabelStyle);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(entry.target == null || !entry.target.HasTarget))
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(54f)))
                            PingTarget(entry);
                    }

                    if (GUILayout.Button("Copy Value", GUILayout.Width(82f)))
                        EditorGUIUtility.systemCopyBuffer = entry.value ?? string.Empty;

                    if (GUILayout.Button("Copy Source", GUILayout.Width(86f)))
                        EditorGUIUtility.systemCopyBuffer = entry.sourceText ?? string.Empty;

                    using (new EditorGUI.DisabledScope(!IsTokenEntry(entry) || PungentUtilityRegistry.Find("token-validator") == null))
                    {
                        if (GUILayout.Button("Open Token", GUILayout.Width(86f)))
                            PungentTokenValidatorWindow.OpenAndSelect(entry.key);
                    }
                }
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                EditorGUILayout.LabelField(_status, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private bool CanApplySelected()
        {
            if (_result == null || _result.entries == null)
                return false;
            bool tokenApplyAvailable = PungentUtilityRegistry.Find("token-validator") != null &&
                                       _result.entries.Any(entry => entry != null && entry.selected && entry.canApply && IsTokenEntry(entry));
            bool boundApplyAvailable = _result.entries.Any(entry => entry != null && entry.selected && entry.canApply && IsBoundSemanticEntry(entry));
            return tokenApplyAvailable || boundApplyAvailable;
        }

        private void ApplySelected()
        {
            if (!CanApplySelected())
            {
                _status = "No applicable selected changes.";
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Rich Document Outputs",
                    "Apply selected outputs from this rich document? This only changes selected token records or explicitly bound adapter targets and does not modify the document body.",
                    "Apply Selected",
                    "Cancel"))
                return;

            int appliedTokens = ApplySelectedTokenEntries();
            int appliedBoundValues = ApplySelectedBoundEntries(out int skippedBoundValues);
            int disabledSelected = _result == null || _result.entries == null
                ? 0
                : _result.entries.Count(entry => entry != null && entry.selected && !entry.canApply);

            string summary = appliedTokens + " token value(s), " + appliedBoundValues + " bound value(s) applied, " + skippedBoundValues + " skipped, " + disabledSelected + " disabled.";
            SetDocument(_document, null);
            _status = summary;
            Repaint();
        }

        private int ApplySelectedTokenEntries()
        {
            if (PungentUtilityRegistry.Find("token-validator") == null)
                return 0;

            PungentTokenStorage.EnsureLoaded();
            int applied = 0;
            foreach (PungentRichDocumentExtractedEntry entry in _result.entries.Where(entry => entry != null && entry.selected && entry.canApply && IsTokenEntry(entry)))
            {
                string key = PungentTokenDatabase.NormalizeKey(entry.key);
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(entry.value))
                    continue;

                PungentTokenDefinition token = PungentTokenStorage.Database.FindToken(key) ?? PungentTokenStorage.Database.AddToken(key);
                token.previewValue = entry.value.Trim();
                if (string.IsNullOrWhiteSpace(token.category))
                    token.category = "Rich Documents";
                if (string.IsNullOrWhiteSpace(token.source))
                    token.source = "rich-document-editor";
                if (token.examples == null)
                    token.examples = new List<string>();
                string example = "{" + key + "}";
                if (!token.examples.Any(item => string.Equals(item, example, StringComparison.OrdinalIgnoreCase)))
                    token.examples.Add(example);
                token.updatedUtc = DateTime.UtcNow.ToString("o");
                applied++;
            }

            if (applied > 0)
                PungentTokenStorage.Save();

            return applied;
        }

        private int ApplySelectedBoundEntries(out int skipped)
        {
            int applied = 0;
            skipped = 0;
            foreach (PungentRichDocumentExtractedEntry entry in _result.entries.Where(entry => entry != null && entry.selected && entry.canApply && IsBoundSemanticEntry(entry)))
            {
                PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.FindBinding(_document, entry.semanticBindingId);
                PungentRichDocumentBindingApplyResult result = PungentRichDocumentBindingApplicationService.Apply(binding, entry.value ?? string.Empty);
                if (result != null && result.applied)
                {
                    applied++;
                    continue;
                }

                skipped++;
                if (result != null && !string.IsNullOrWhiteSpace(result.message))
                    _status = result.message;
            }

            return applied;
        }

        private void CopySummary()
        {
            if (_result == null || _result.entries == null)
                return;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(_result.documentTitle);
            builder.AppendLine("Rich Document Propagation Preview");
            builder.AppendLine();
            foreach (PungentRichDocumentExtractedEntry entry in _result.entries)
            {
                if (entry == null)
                    continue;
                builder.Append(entry.category).Append(" / ").Append(entry.title);
                if (!string.IsNullOrWhiteSpace(entry.key))
                    builder.Append(" [").Append(entry.key).Append("]");
                builder.Append(": ").AppendLine(entry.value ?? string.Empty);
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString();
            _status = "Copied propagation summary.";
        }

        private static bool IsTokenEntry(PungentRichDocumentExtractedEntry entry)
        {
            return entry != null && string.Equals(entry.targetType, PungentRichDocumentParser.TargetTokenPreviewValue, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBoundSemanticEntry(PungentRichDocumentExtractedEntry entry)
        {
            return entry != null &&
                   entry.target != null &&
                   entry.target.HasTarget &&
                   !string.IsNullOrWhiteSpace(entry.semanticBindingId);
        }

        private void PingTarget(PungentRichDocumentExtractedEntry entry)
        {
            if (entry == null || entry.target == null)
                return;

            if (PungentRichDocumentBindingApplicationService.TryResolveUnityTarget(entry.target, out UnityEngine.Object targetObject, out string error) && targetObject != null)
            {
                Selection.activeObject = targetObject;
                EditorGUIUtility.PingObject(targetObject);
                _status = "Pinged " + targetObject.name + ".";
                return;
            }

            _status = string.IsNullOrWhiteSpace(error) ? "Target could not be resolved." : error;
        }

        private static Color TintForCategory(string category)
        {
            if (string.Equals(category, "Token", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Cyan;
            if (string.Equals(category, "Dialogue", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Purple;
            if (string.Equals(category, "Quest", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Green;
            if (string.Equals(category, "Tutorial", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Blue;
            if (string.Equals(category, "Command", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Amber;
            return UtilityWindowTheme.Teal;
        }
    }
#endif
}
