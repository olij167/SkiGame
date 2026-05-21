using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public static class PungentBoardAuthoringPickerGUI
    {
        private const int MaxItems = 600;
        private const int MaxVisibleItems = 80;

        private sealed class Candidate
        {
            public PungentAuthoringMetadata metadata;
            public string providerId;
            public string providerName;
            public string searchText;
        }

        private static readonly List<Candidate> Candidates = new List<Candidate>();
        private static Vector2 _scroll;
        private static string _search = string.Empty;
        private static string _status = "Picker not refreshed.";
        private static bool _loaded;

        public static void Draw(PungentAuthoringReference reference, ref bool changed)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Authoring Picker", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Refresh", "Enumerate currently registered authoring providers once. This does not run during repaint."), GUILayout.Width(76f)))
                    Refresh();

                using (new EditorGUI.DisabledScope(!_loaded))
                {
                    if (GUILayout.Button(new GUIContent("Clear", "Clear cached picker results."), GUILayout.Width(56f)))
                    {
                        Candidates.Clear();
                        _loaded = false;
                        _status = "Picker cache cleared.";
                    }
                }

                GUILayout.FlexibleSpace();
            }

            _search = EditorGUILayout.TextField(new GUIContent("Filter", "Filter cached authoring items by title, ID, kind, provider, status, or tags."), _search);
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);

            if (!_loaded)
            {
                EditorGUILayout.HelpBox("Press Refresh to list items from installed authoring providers. The Board Editor does not scan providers every draw.", MessageType.Info);
                return;
            }

            List<Candidate> filtered = FilteredCandidates().Take(MaxVisibleItems).ToList();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(92f), GUILayout.MaxHeight(220f));
            for (int i = 0; i < filtered.Count; i++)
                DrawCandidateRow(filtered[i], reference, ref changed);
            EditorGUILayout.EndScrollView();

            int matchCount = FilteredCandidates().Count();
            if (matchCount > MaxVisibleItems)
                EditorGUILayout.LabelField((matchCount - MaxVisibleItems) + " more cached item(s). Refine the filter to narrow the list.", EditorStyles.miniLabel);
        }

        private static void Refresh()
        {
            Candidates.Clear();
            int providerCount = 0;
            int errorCount = 0;

            foreach (IPungentAuthoringProvider provider in PungentAuthoringProviderRegistry.GetProviders())
            {
                if (provider == null)
                    continue;

                if ((provider.Capabilities & PungentAuthoringProviderCapabilities.EnumerateItems) == 0)
                    continue;

                providerCount++;
                try
                {
                    foreach (PungentAuthoringMetadata metadata in provider.EnumerateItems() ?? Enumerable.Empty<PungentAuthoringMetadata>())
                    {
                        if (metadata == null)
                            continue;

                        metadata.NormalizeInPlace();
                        if (string.IsNullOrWhiteSpace(metadata.id))
                            continue;

                        if (string.IsNullOrWhiteSpace(metadata.sourceProviderId))
                            metadata.sourceProviderId = provider.ProviderId;

                        Candidates.Add(new Candidate
                        {
                            metadata = metadata,
                            providerId = provider.ProviderId,
                            providerName = provider.DisplayName,
                            searchText = BuildSearchText(metadata, provider)
                        });

                        if (Candidates.Count >= MaxItems)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Debug.LogWarning("PungentFunk Board authoring picker could not enumerate provider '" + provider.ProviderId + "': " + ex.Message);
                }

                if (Candidates.Count >= MaxItems)
                    break;
            }

            Candidates.Sort((a, b) =>
            {
                int kind = string.Compare(a.metadata.KindLabel, b.metadata.KindLabel, StringComparison.OrdinalIgnoreCase);
                return kind != 0 ? kind : string.Compare(a.metadata.title, b.metadata.title, StringComparison.OrdinalIgnoreCase);
            });

            _loaded = true;
            _status = "Cached " + Candidates.Count + " item(s) from " + providerCount + " provider(s)" +
                      (Candidates.Count >= MaxItems ? " (limit reached)" : string.Empty) +
                      (errorCount > 0 ? " with " + errorCount + " provider warning(s)." : ".");
        }

        private static IEnumerable<Candidate> FilteredCandidates()
        {
            string query = (_search ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return Candidates;

            return Candidates.Where(candidate => candidate != null &&
                                                 !string.IsNullOrWhiteSpace(candidate.searchText) &&
                                                 candidate.searchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void DrawCandidateRow(Candidate candidate, PungentAuthoringReference reference, ref bool changed)
        {
            if (candidate == null || candidate.metadata == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(candidate.metadata.title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Link", "Link this node to the selected authoring item."), GUILayout.Width(54f)))
                    {
                        ApplyCandidate(candidate, reference);
                        changed = true;
                    }
                }

                string subtitle = candidate.metadata.KindLabel + " / " + candidate.providerName;
                if (!string.IsNullOrWhiteSpace(candidate.metadata.status))
                    subtitle += " / " + candidate.metadata.status;
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);

                if (!string.IsNullOrWhiteSpace(candidate.metadata.summary))
                    EditorGUILayout.LabelField(Preview(candidate.metadata.summary, 140), EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void ApplyCandidate(Candidate candidate, PungentAuthoringReference reference)
        {
            if (candidate == null || candidate.metadata == null || reference == null)
                return;

            reference.itemKind = candidate.metadata.kind;
            reference.customKind = candidate.metadata.customKind;
            reference.itemId = candidate.metadata.id;
            reference.providerId = string.IsNullOrWhiteSpace(candidate.metadata.sourceProviderId) ? candidate.providerId : candidate.metadata.sourceProviderId;
            reference.label = candidate.metadata.title;
            reference.sourceContext = candidate.metadata.packageCapabilityId;
            reference.NormalizeInPlace();
        }

        private static string BuildSearchText(PungentAuthoringMetadata metadata, IPungentAuthoringProvider provider)
        {
            IEnumerable<string> parts = new[]
            {
                metadata.id,
                metadata.title,
                metadata.summary,
                metadata.KindLabel,
                metadata.status,
                metadata.priority,
                metadata.visibility,
                metadata.sourceProviderId,
                provider != null ? provider.ProviderId : string.Empty,
                provider != null ? provider.DisplayName : string.Empty
            }.Concat(metadata.tags ?? new List<string>());

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray());
        }

        private static string Preview(string value, int maxLength)
        {
            string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace("\r", " ").Replace("\n", " ");
            return clean.Length <= maxLength ? clean : clean.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
        }
    }
#endif
}
