using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentInlinePropagationView : VisualElement
    {
        private readonly IMGUIContainer _container;
        private readonly Dictionary<string, bool> _selection = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _scroll;
        private PungentRichDocument _document;
        private PungentRichDocumentPropagationProposal _proposal;
        private Action _prepareDocument;
        private Action _openExpandedPreview;
        private Action<string> _status;

        public PungentRichDocumentInlinePropagationView()
        {
            style.width = 300;
            style.minWidth = 260;
            style.flexShrink = 0;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.borderLeftWidth = 1;
            style.borderLeftColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.28f, 0.29f, 0.31f) : new Color(0.68f, 0.7f, 0.72f));
            style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.13f, 0.138f, 0.145f) : new Color(0.86f, 0.885f, 0.9f));

            _container = new IMGUIContainer(Draw);
            Add(_container);
        }

        public void Bind(PungentRichDocument document, Action prepareDocument, Action openExpandedPreview, Action<string> status)
        {
            _document = document;
            _prepareDocument = prepareDocument;
            _openExpandedPreview = openExpandedPreview;
            _status = status;
            RefreshProposal();
        }

        public void RefreshProposal()
        {
            if (_document == null)
            {
                _proposal = null;
                return;
            }

            _prepareDocument?.Invoke();
            _proposal = PungentRichDocumentExtractor.CreateProposal(_document, PungentRichDocumentParser.Parse(_document));
            foreach (PungentRichDocumentExtractedEntry entry in _proposal.entries ?? new List<PungentRichDocumentExtractedEntry>())
            {
                string key = EntryKey(entry);
                if (!_selection.ContainsKey(key))
                    _selection[key] = entry != null && entry.canApply && HasChanged(entry);
                if (entry != null)
                    entry.selected = _selection[key];
            }
        }

        private void Draw()
        {
            EditorGUILayout.LabelField("Document Outputs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Current-document outputs only. Nothing is applied until Apply Selected.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Refresh", "Re-read this document and update output readiness."), EditorStyles.miniButton))
                    RefreshProposal();
                if (GUILayout.Button(new GUIContent("Open Full", "Open the expanded propagation preview window."), EditorStyles.miniButton, GUILayout.Width(76f)))
                    _openExpandedPreview?.Invoke();
            }

            if (_document == null)
            {
                EditorGUILayout.HelpBox("No rich document is selected.", MessageType.Info);
                return;
            }

            if (_proposal == null)
                RefreshProposal();

            List<PungentRichDocumentExtractedEntry> entries = _proposal == null || _proposal.entries == null
                ? new List<PungentRichDocumentExtractedEntry>()
                : _proposal.entries.Where(entry => entry != null).ToList();
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No token, reference, semantic, or custom insertion outputs were found.", MessageType.Info);
                return;
            }

            int changed = entries.Count(HasChanged);
            int applicable = entries.Count(entry => entry.canApply);
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(entries.Count + " found", UtilityWindowTheme.Cyan, 82f);
                UtilityWindowTheme.CountPill(changed + " changed", changed > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 88f);
                UtilityWindowTheme.CountPill(applicable + " ready", applicable > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 76f);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (IGrouping<string, PungentRichDocumentExtractedEntry> group in entries.GroupBy(entry => string.IsNullOrWhiteSpace(entry.category) ? "Other" : entry.category).OrderBy(group => group.Key))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                foreach (PungentRichDocumentExtractedEntry entry in group)
                    DrawEntry(entry);
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool canApply = PungentRichDocumentPropagationApplyService.CanApplySelected(entries);
                using (new EditorGUI.DisabledScope(!canApply))
                {
                    if (GUILayout.Button(new GUIContent("Apply Selected", "Explicitly apply selected changed outputs."), EditorStyles.toolbarButton))
                        ApplySelected(entries);
                }
            }
        }

        private void DrawEntry(PungentRichDocumentExtractedEntry entry)
        {
            string key = EntryKey(entry);
            entry.selected = _selection.TryGetValue(key, out bool selected) ? selected : entry.selected;
            Color tint = TintForCategory(entry.category);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.08f, 0.035f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!entry.canApply))
                    {
                        bool next = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18f));
                        if (next != entry.selected)
                        {
                            entry.selected = next;
                            _selection[key] = next;
                        }
                    }

                    EditorGUILayout.LabelField(Clamp(entry.title, 34), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(StatusFor(entry), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(72f));
                }

                if (!string.IsNullOrWhiteSpace(entry.value))
                    EditorGUILayout.LabelField("Proposed", Clamp(entry.value, 96), UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(entry.currentValue))
                    EditorGUILayout.LabelField("Current", Clamp(entry.currentValue, 96), UtilityWindowTheme.MutedMiniLabelStyle);
                if (!string.IsNullOrWhiteSpace(entry.disabledReason))
                    EditorGUILayout.LabelField(Clamp(entry.disabledReason, 110), UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(entry.target == null || !entry.target.HasTarget))
                    {
                        if (GUILayout.Button(new GUIContent("Ping", "Ping the target for this output."), EditorStyles.miniButton, GUILayout.Width(48f)))
                            PungentRichDocumentPropagationApplyService.PingTarget(entry, _status);
                    }
                    if (GUILayout.Button(new GUIContent("Copy", "Copy the proposed value."), EditorStyles.miniButton, GUILayout.Width(48f)))
                        EditorGUIUtility.systemCopyBuffer = entry.value ?? string.Empty;
                    if (GUILayout.Button(new GUIContent("Source", "Copy the source text."), EditorStyles.miniButton, GUILayout.Width(56f)))
                        EditorGUIUtility.systemCopyBuffer = entry.sourceText ?? string.Empty;
                }
            }
        }

        private void ApplySelected(List<PungentRichDocumentExtractedEntry> entries)
        {
            if (!EditorUtility.DisplayDialog(
                    "Apply Rich Document Outputs",
                    "Apply selected outputs from this rich document? This changes only selected token records or explicitly bound adapter targets.",
                    "Apply Selected",
                    "Cancel"))
                return;

            PungentRichDocumentPropagationApplySummary summary = PungentRichDocumentPropagationApplyService.ApplySelected(_document, entries);
            _status?.Invoke(summary.ToStatus());
            RefreshProposal();
        }

        private static bool HasChanged(PungentRichDocumentExtractedEntry entry)
        {
            return entry != null &&
                   entry.canApply &&
                   !string.Equals(entry.currentValue ?? string.Empty, entry.value ?? string.Empty, StringComparison.Ordinal);
        }

        private static string StatusFor(PungentRichDocumentExtractedEntry entry)
        {
            if (entry == null)
                return "Missing";
            if (!entry.canApply)
                return string.IsNullOrWhiteSpace(entry.disabledReason) ? "Disabled" : "Unresolved";
            return HasChanged(entry) ? "Changed" : "Same";
        }

        private static string EntryKey(PungentRichDocumentExtractedEntry entry)
        {
            if (entry == null)
                return string.Empty;
            return (entry.category ?? string.Empty) + "|" +
                   (entry.targetType ?? string.Empty) + "|" +
                   (entry.key ?? string.Empty) + "|" +
                   entry.sourceLine + "|" +
                   (entry.semanticBindingId ?? string.Empty) + "|" +
                   (entry.bindingExpression ?? string.Empty);
        }

        private static string Clamp(string value, int max)
        {
            string text = value ?? string.Empty;
            return text.Length <= max ? text : text.Substring(0, Math.Max(0, max - 3)).TrimEnd() + "...";
        }

        private static Color TintForCategory(string category)
        {
            if (string.Equals(category, "Token", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Cyan;
            if (string.Equals(category, "Reference Token", StringComparison.OrdinalIgnoreCase))
                return UtilityWindowTheme.Blue;
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
