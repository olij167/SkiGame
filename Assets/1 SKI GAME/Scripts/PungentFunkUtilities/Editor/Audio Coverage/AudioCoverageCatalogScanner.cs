using PungentFunk.Utilities.Audio;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Scanning;

namespace PungentFunk.Utilities.Editor.Audio
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal static class AudioCoverageCatalogScanner
    {
        public static void Rebuild(
            ScriptableObject catalog,
            AudioCoverageProfileSO profile,
            string selectedCueName,
            bool refreshScriptReferences,
            List<AudioCoverageCueAuditRow> rows,
            Dictionary<string, List<AudioCoverageCatalogEntryInfo>> catalogEntriesByCue,
            Dictionary<string, List<AudioCoverageScriptReferenceHit>> scriptHitsByCue,
            Dictionary<string, int> runtimeCountsByTypeName,
            HashSet<string> missingRuntimeTypes)
        {
            RefreshCatalogEntryCache(catalog, profile, catalogEntriesByCue);
            if (refreshScriptReferences)
                RefreshScriptReferences(profile, selectedCueName, catalogEntriesByCue, scriptHitsByCue);
            RefreshRuntimeSourceCounts(profile, runtimeCountsByTypeName, missingRuntimeTypes);
            BuildAuditRows(catalog, profile, selectedCueName, rows, catalogEntriesByCue, scriptHitsByCue);
        }

        public static PungentAuditScanJob CreateAuditJob(ScriptableObject catalog, AudioCoverageProfileSO profile, string selectedCueName, PungentAuditScanMode mode)
        {
            CooperativeCatalogAudit audit = new CooperativeCatalogAudit(catalog, profile, selectedCueName);
            PungentAuditScanJob job = PungentAuditScanJob.CreateCooperative("audio-catalog-coverage", "Audio Catalog Coverage", audit.Step);
            job.canPause = true;
            job.canCancel = true;
            job.capabilities = PungentAuditScanJobCapabilities.Cooperative |
                               PungentAuditScanJobCapabilities.BackgroundSafe |
                               PungentAuditScanJobCapabilities.UsesAssetDatabase |
                               PungentAuditScanJobCapabilities.ScanOnly;
            job.Report(0f, 0, 1, "Queued catalog coverage audit.", false, "Queued");
            return job;
        }

        public static void AddRowsToScanResult(PungentScanResult scan, ScriptableObject catalog, IReadOnlyList<AudioCoverageCueAuditRow> rows)
        {
            if (scan == null || rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                AudioCoverageCueAuditRow row = rows[i];
                if (row == null || row.Issues.Count == 0)
                    continue;

                PungentScanSeverity severity = row.Status == AudioCoverageCueStatus.Invalid
                    ? PungentScanSeverity.Error
                    : row.Status == AudioCoverageCueStatus.Valid
                        ? PungentScanSeverity.Success
                        : PungentScanSeverity.Warning;

                string title = GetCueIssueGroup(row);
                for (int issueIndex = 0; issueIndex < row.Issues.Count; issueIndex++)
                    scan.AddIssue(severity, title, row.CueName + ": " + row.Issues[issueIndex], catalog, catalog != null ? AssetDatabase.GetAssetPath(catalog) : null, "AUDIO_CATALOG_" + row.Status.ToString().ToUpperInvariant());
            }
        }

        public static string[] GetCueNames(AudioCoverageProfileSO profile, string selectedCueName, Dictionary<string, List<AudioCoverageCatalogEntryInfo>> catalogEntriesByCue)
        {
            SortedSet<string> cueNames = new SortedSet<string>(StringComparer.Ordinal);

            Type cueType = profile != null ? FindType(profile.cueEnumTypeName) : null;
            if (cueType != null && cueType.IsEnum)
            {
                string[] names = Enum.GetNames(cueType);
                for (int i = 0; i < names.Length; i++)
                    if (!IsNoneCue(profile, names[i]))
                        cueNames.Add(names[i]);
            }

            if (catalogEntriesByCue != null)
            {
                foreach (string key in catalogEntriesByCue.Keys)
                    if (!IsNoneCue(profile, key))
                        cueNames.Add(key);
            }

            if (profile != null && profile.cueBindings != null)
            {
                for (int i = 0; i < profile.cueBindings.Count; i++)
                {
                    AudioCueBindingRule binding = profile.cueBindings[i];
                    if (binding != null && !string.IsNullOrWhiteSpace(binding.cueName) && !IsNoneCue(profile, binding.cueName))
                        cueNames.Add(binding.cueName);
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedCueName) && !IsNoneCue(profile, selectedCueName))
                cueNames.Add(selectedCueName);

            string[] result = new string[cueNames.Count];
            cueNames.CopyTo(result);
            return result;
        }

        public static bool IsNoneCue(AudioCoverageProfileSO profile, string cueName)
        {
            if (string.IsNullOrWhiteSpace(cueName))
                return true;
            return profile != null && string.Equals(cueName, profile.noneCueName, StringComparison.Ordinal);
        }

        private static void RefreshCatalogEntryCache(ScriptableObject catalog, AudioCoverageProfileSO profile, Dictionary<string, List<AudioCoverageCatalogEntryInfo>> catalogEntriesByCue)
        {
            catalogEntriesByCue.Clear();
            if (catalog == null || profile == null || string.IsNullOrWhiteSpace(profile.cueListPropertyName))
                return;

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty list = serializedCatalog.FindProperty(profile.cueListPropertyName);
            if (list == null || !list.isArray)
                return;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                SerializedProperty cueProperty = element.FindPropertyRelative(profile.cueIdPropertyName);
                string cueName = ReadCueName(cueProperty);
                if (string.IsNullOrWhiteSpace(cueName) || IsNoneCue(profile, cueName))
                    continue;

                AudioCoverageCatalogEntryInfo entry = BuildCatalogEntryInfo(profile, cueName, i, element);
                if (!catalogEntriesByCue.TryGetValue(cueName, out List<AudioCoverageCatalogEntryInfo> entries))
                {
                    entries = new List<AudioCoverageCatalogEntryInfo>();
                    catalogEntriesByCue.Add(cueName, entries);
                }
                entries.Add(entry);
            }
        }

        private static AudioCoverageCatalogEntryInfo BuildCatalogEntryInfo(AudioCoverageProfileSO profile, string cueName, int index, SerializedProperty element)
        {
            AudioCoverageCatalogEntryInfo entry = new AudioCoverageCatalogEntryInfo
            {
                CueName = cueName,
                Index = index
            };

            SerializedProperty clips = FindRelative(element, profile.clipsPropertyName);
            entry.HasClipProperty = clips != null;
            if (clips != null && clips.isArray)
            {
                entry.ClipSlots = clips.arraySize;
                for (int i = 0; i < clips.arraySize; i++)
                {
                    SerializedProperty slot = clips.GetArrayElementAtIndex(i);
                    if (slot != null && slot.propertyType == SerializedPropertyType.ObjectReference && slot.objectReferenceValue != null)
                        entry.NonNullClipSlots++;
                }
            }

            entry.HasVolume = TryReadFloat(element, profile.volumePropertyName, out entry.Volume);
            entry.HasPitchRange = TryReadVector2(element, profile.pitchRangePropertyName, out entry.PitchRange);
            entry.HasSpatialBlend = TryReadFloat(element, profile.spatialBlendPropertyName, out entry.SpatialBlend);
            entry.HasMinDistance = TryReadFloat(element, profile.minDistancePropertyName, out entry.MinDistance);
            entry.HasMaxDistance = TryReadFloat(element, profile.maxDistancePropertyName, out entry.MaxDistance);

            SerializedProperty mixer = FindRelative(element, profile.mixerGroupPropertyName);
            entry.HasMixerGroup = mixer != null && mixer.propertyType == SerializedPropertyType.ObjectReference;
            entry.MixerGroupAssigned = !entry.HasMixerGroup || mixer.objectReferenceValue != null;
            return entry;
        }

        private static void BuildAuditRows(
            ScriptableObject catalog,
            AudioCoverageProfileSO profile,
            string selectedCueName,
            List<AudioCoverageCueAuditRow> rows,
            Dictionary<string, List<AudioCoverageCatalogEntryInfo>> catalogEntriesByCue,
            Dictionary<string, List<AudioCoverageScriptReferenceHit>> scriptHitsByCue)
        {
            rows.Clear();
            string[] cueNames = GetCueNames(profile, selectedCueName, catalogEntriesByCue);
            for (int i = 0; i < cueNames.Length; i++)
            {
                string cueName = cueNames[i];
                if (string.IsNullOrWhiteSpace(cueName) || IsNoneCue(profile, cueName))
                    continue;

                AudioCoverageCueAuditRow row = new AudioCoverageCueAuditRow { CueName = cueName };
                if (catalogEntriesByCue.TryGetValue(cueName, out List<AudioCoverageCatalogEntryInfo> entries))
                    for (int e = 0; e < entries.Count; e++)
                        row.EntryIndices.Add(entries[e].Index);

                if (profile != null)
                {
                    row.BindingCount = profile.CountBindingsForCue(cueName, includeIgnored: false);
                    row.IsIgnored = profile.IsCueIgnored(cueName);
                }

                row.ScriptReferenceCount = scriptHitsByCue.TryGetValue(cueName, out List<AudioCoverageScriptReferenceHit> hits) ? hits.Count : 0;
                row.HasCoverage = row.IsIgnored || row.BindingCount > 0 || (profile != null && profile.treatScriptReferencesAsCoverage && row.ScriptReferenceCount > 0);
                ValidateRow(catalog, profile, row, catalogEntriesByCue);
                rows.Add(row);
            }
        }

        private static void ValidateRow(ScriptableObject catalog, AudioCoverageProfileSO profile, AudioCoverageCueAuditRow row, Dictionary<string, List<AudioCoverageCatalogEntryInfo>> catalogEntriesByCue)
        {
            bool hasWarning = false;
            bool hasInvalid = false;

            if (catalog != null)
            {
                if (!catalogEntriesByCue.TryGetValue(row.CueName, out List<AudioCoverageCatalogEntryInfo> entries) || entries.Count == 0)
                {
                    row.Status = AudioCoverageCueStatus.Missing;
                    row.Issues.Add("No catalog entry exists for this cue.");
                    if (row.HasCoverage)
                        row.Issues.Add("This cue has binding/script coverage and should probably exist in the catalog.");
                    return;
                }

                if (entries.Count > 1)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Duplicate catalog entries found: {entries.Count}.");
                }

                for (int i = 0; i < entries.Count; i++)
                    ValidateCatalogEntry(entries[i], row, ref hasWarning, ref hasInvalid);
            }
            else
            {
                hasWarning = true;
                row.Issues.Add("No catalog asset assigned; catalog entry validation skipped.");
            }

            if (profile == null)
            {
                hasWarning = true;
                row.Issues.Add("No AudioCoverageProfileSO assigned, so component/event binding coverage is unknown.");
            }
            else if (profile.warnWhenCueHasNoCoverage && !row.HasCoverage && !row.IsIgnored)
            {
                hasWarning = true;
                row.Issues.Add("No profile binding or script reference coverage found for this cue.");
            }

            row.Status = hasInvalid ? AudioCoverageCueStatus.Invalid : hasWarning ? AudioCoverageCueStatus.Warning : AudioCoverageCueStatus.Valid;
        }

        private static void ValidateCatalogEntry(AudioCoverageCatalogEntryInfo entry, AudioCoverageCueAuditRow row, ref bool hasWarning, ref bool hasInvalid)
        {
            if (entry == null)
                return;

            if (entry.HasClipProperty)
            {
                if (entry.ClipSlots == 0)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Entry {entry.Index}: no clips assigned.");
                }
                else if (entry.NonNullClipSlots == 0)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Entry {entry.Index}: all clip slots are null.");
                }
                else if (entry.NonNullClipSlots < entry.ClipSlots)
                {
                    hasWarning = true;
                    row.Issues.Add($"Entry {entry.Index}: some clip slots are null.");
                }
            }

            if (entry.HasVolume && entry.Volume <= 0f)
            {
                hasWarning = true;
                row.Issues.Add($"Entry {entry.Index}: volume is zero or below.");
            }

            if (entry.HasPitchRange)
            {
                if (entry.PitchRange.x > entry.PitchRange.y)
                {
                    hasInvalid = true;
                    row.Issues.Add($"Entry {entry.Index}: pitch range is reversed.");
                }
                else if (Mathf.Abs(entry.PitchRange.y - entry.PitchRange.x) > 0.5f)
                {
                    hasWarning = true;
                    row.Issues.Add($"Entry {entry.Index}: pitch range is unusually wide.");
                }
            }

            if (entry.HasMinDistance && entry.HasMaxDistance && entry.MaxDistance < entry.MinDistance)
            {
                hasInvalid = true;
                row.Issues.Add($"Entry {entry.Index}: max distance is less than min distance.");
            }

            if (entry.HasSpatialBlend && (entry.SpatialBlend < 0f || entry.SpatialBlend > 1f))
            {
                hasWarning = true;
                row.Issues.Add($"Entry {entry.Index}: spatial blend is outside 0-1.");
            }

            if (entry.HasMixerGroup && !entry.MixerGroupAssigned)
            {
                hasWarning = true;
                row.Issues.Add($"Entry {entry.Index}: no mixer group assigned.");
            }
        }

        private static void RefreshScriptReferences(AudioCoverageProfileSO profile, string selectedCueName, Dictionary<string, List<AudioCoverageCatalogEntryInfo>> catalogEntriesByCue, Dictionary<string, List<AudioCoverageScriptReferenceHit>> scriptHitsByCue)
        {
            scriptHitsByCue.Clear();
            if (profile == null || !profile.treatScriptReferencesAsCoverage)
                return;

            string[] cueNames = GetCueNames(profile, selectedCueName, catalogEntriesByCue);
            if (cueNames.Length == 0)
                return;

            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                    continue;

                string text = script.text;
                if (string.IsNullOrEmpty(text) || !MatchesAnyScriptToken(profile, text))
                    continue;

                for (int c = 0; c < cueNames.Length; c++)
                {
                    string cueName = cueNames[c];
                    if (IsNoneCue(profile, cueName) || !text.Contains(cueName))
                        continue;

                    if (!scriptHitsByCue.TryGetValue(cueName, out List<AudioCoverageScriptReferenceHit> hits))
                    {
                        hits = new List<AudioCoverageScriptReferenceHit>();
                        scriptHitsByCue.Add(cueName, hits);
                    }

                    hits.Add(new AudioCoverageScriptReferenceHit
                    {
                        Path = path,
                        ScriptName = script.name
                    });
                }
            }
        }

        private static bool MatchesAnyScriptToken(AudioCoverageProfileSO profile, string text)
        {
            if (profile == null || profile.scriptSearchTokens == null || profile.scriptSearchTokens.Count == 0)
                return true;

            for (int i = 0; i < profile.scriptSearchTokens.Count; i++)
            {
                string token = profile.scriptSearchTokens[i];
                if (!string.IsNullOrWhiteSpace(token) && text.Contains(token))
                    return true;
            }

            return false;
        }

        private static void RefreshRuntimeSourceCounts(AudioCoverageProfileSO profile, Dictionary<string, int> runtimeCountsByTypeName, HashSet<string> missingRuntimeTypes)
        {
            runtimeCountsByTypeName.Clear();
            missingRuntimeTypes.Clear();

            if (profile == null || profile.runtimeHookTypeNames == null)
                return;

            for (int i = 0; i < profile.runtimeHookTypeNames.Count; i++)
            {
                string typeName = profile.runtimeHookTypeNames[i];
                if (string.IsNullOrWhiteSpace(typeName))
                    continue;

                Type type = FindType(typeName);
                if (type == null || !typeof(Object).IsAssignableFrom(type))
                {
                    missingRuntimeTypes.Add(typeName);
                    runtimeCountsByTypeName[typeName] = 0;
                    continue;
                }

#if UNITY_2023_1_OR_NEWER
                runtimeCountsByTypeName[typeName] = Object.FindObjectsByType(type, FindObjectsInactive.Exclude).Length;
#else
                runtimeCountsByTypeName[typeName] = Object.FindObjectsOfType(type).Length;
#endif
            }
        }

        private static string GetCueIssueGroup(AudioCoverageCueAuditRow row)
        {
            if (row == null)
                return "Audio cue issue";
            if (row.Status == AudioCoverageCueStatus.Missing)
                return "Missing cue";
            if (row.Status == AudioCoverageCueStatus.Invalid)
                return "Invalid or duplicate cue";
            if (row.ScriptReferenceCount > 0)
                return "Script reference hit";
            return "Incomplete cue";
        }

        private static string ReadCueName(SerializedProperty cueProperty)
        {
            if (cueProperty == null)
                return string.Empty;

            switch (cueProperty.propertyType)
            {
                case SerializedPropertyType.Enum:
                    if (cueProperty.enumDisplayNames != null && cueProperty.enumValueIndex >= 0 && cueProperty.enumValueIndex < cueProperty.enumDisplayNames.Length)
                        return cueProperty.enumDisplayNames[cueProperty.enumValueIndex];
                    return cueProperty.enumValueIndex.ToString();
                case SerializedPropertyType.String:
                    return cueProperty.stringValue;
                case SerializedPropertyType.ObjectReference:
                    return cueProperty.objectReferenceValue != null ? cueProperty.objectReferenceValue.name : string.Empty;
                case SerializedPropertyType.Integer:
                    return cueProperty.intValue.ToString();
                default:
                    return cueProperty.displayName;
            }
        }

        private static SerializedProperty FindRelative(SerializedProperty parent, string relativeName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(relativeName))
                return null;
            return parent.FindPropertyRelative(relativeName);
        }

        private static bool TryReadFloat(SerializedProperty parent, string relativeName, out float value)
        {
            value = 0f;
            SerializedProperty property = FindRelative(parent, relativeName);
            if (property == null)
                return false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    value = property.floatValue;
                    return true;
                case SerializedPropertyType.Integer:
                    value = property.intValue;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadVector2(SerializedProperty parent, string relativeName, out Vector2 value)
        {
            value = Vector2.zero;
            SerializedProperty property = FindRelative(parent, relativeName);
            if (property == null || property.propertyType != SerializedPropertyType.Vector2)
                return false;

            value = property.vector2Value;
            return true;
        }

        private static Type FindType(string typeName)
        {
            return string.IsNullOrWhiteSpace(typeName) ? null : PungentEditorPerformanceUtility.ResolveTypeCached(typeName);
        }

        private sealed class CooperativeCatalogAudit
        {
            private enum Stage
            {
                Begin,
                ValidateConfiguration,
                IndexCatalog,
                RuntimeSources,
                BuildCueList,
                BuildRows,
                Publish,
                Done
            }

            private readonly ScriptableObject _catalog;
            private readonly AudioCoverageProfileSO _profile;
            private readonly string _selectedCueName;
            private readonly List<AudioCoverageCueAuditRow> _rows = new List<AudioCoverageCueAuditRow>();
            private readonly Dictionary<string, List<AudioCoverageCatalogEntryInfo>> _catalogEntriesByCue = new Dictionary<string, List<AudioCoverageCatalogEntryInfo>>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<AudioCoverageScriptReferenceHit>> _scriptHitsByCue = new Dictionary<string, List<AudioCoverageScriptReferenceHit>>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _runtimeCountsByTypeName = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly HashSet<string> _missingRuntimeTypes = new HashSet<string>(StringComparer.Ordinal);
            private PungentScanSession _session;
            private PungentScanResult _result;
            private string[] _cueNames = Array.Empty<string>();
            private int _cueIndex;
            private Stage _stage = Stage.Begin;

            public CooperativeCatalogAudit(ScriptableObject catalog, AudioCoverageProfileSO profile, string selectedCueName)
            {
                _catalog = catalog;
                _profile = profile;
                _selectedCueName = selectedCueName;
            }

            public PungentAuditScanStepResult Step(PungentAuditScanContext context)
            {
                if (context.IsCancellationRequested())
                    return Cancel("Audio Catalog Coverage cancelled before the next catalog checkpoint.");
                if (context.IsPauseRequested())
                    return PungentAuditScanStepResult.Continue("Audio Catalog Coverage paused.");

                switch (_stage)
                {
                    case Stage.Begin:
                        _session = new PungentScanSession("audio-catalog-coverage", "Audio Catalog Coverage");
                        _result = _session.Begin(PungentScanScope.ProjectAssets, "Catalog");
                        _stage = Stage.ValidateConfiguration;
                        context.Report(0.05f, 0, 1, "Resolving catalog coverage configuration.", false, "Resolve config");
                        return PungentAuditScanStepResult.Continue("Resolving catalog coverage configuration.");

                    case Stage.ValidateConfiguration:
                        if (_profile == null)
                            _result.AddIssue(PungentScanSeverity.Warning, "Not configured", "No AudioCoverageProfileSO is assigned. Open Audio Catalog Coverage and assign or create a profile before catalog coverage can be evaluated.", null, null, "AUDIO_CATALOG_NOT_CONFIGURED");
                        if (_catalog == null)
                            _result.AddIssue(PungentScanSeverity.Warning, "Not configured", "No catalog asset is assigned. Open Audio Catalog Coverage and assign or find a catalog asset before catalog entries can be evaluated.", _profile, _profile != null ? AssetDatabase.GetAssetPath(_profile) : null, "AUDIO_CATALOG_NOT_CONFIGURED");
                        if (_profile == null || _catalog == null)
                        {
                            PungentScanResult notConfigured = _session.Complete(0, 0, 0, 0, _profile == null ? "No AudioCoverageProfileSO is configured." : "No catalog asset is configured.");
                            _stage = Stage.Done;
                            return PungentAuditScanStepResult.NotConfigured(notConfigured.StatusMessage, notConfigured);
                        }
                        _stage = Stage.IndexCatalog;
                        context.Report(0.12f, 0, 1, "Indexing catalog entries.", true, "Index catalog");
                        return PungentAuditScanStepResult.Continue("Indexing catalog entries.");

                    case Stage.IndexCatalog:
                        RefreshCatalogEntryCache(_catalog, _profile, _catalogEntriesByCue);
                        _stage = Stage.RuntimeSources;
                        context.Report(0.25f, _catalogEntriesByCue.Count, Math.Max(1, _catalogEntriesByCue.Count), "Indexed " + _catalogEntriesByCue.Count + " catalog cue(s).", false, "Index catalog");
                        return PungentAuditScanStepResult.Continue("Catalog entries indexed.");

                    case Stage.RuntimeSources:
                        RefreshRuntimeSourceCounts(_profile, _runtimeCountsByTypeName, _missingRuntimeTypes);
                        _stage = Stage.BuildCueList;
                        context.Report(0.34f, _runtimeCountsByTypeName.Count, Math.Max(1, _runtimeCountsByTypeName.Count), "Runtime source counts refreshed.", false, "Runtime sources");
                        return PungentAuditScanStepResult.Continue("Runtime source counts refreshed.");

                    case Stage.BuildCueList:
                        _cueNames = GetCueNames(_profile, _selectedCueName, _catalogEntriesByCue);
                        _cueIndex = 0;
                        _rows.Clear();
                        _stage = Stage.BuildRows;
                        context.Report(0.40f, 0, Math.Max(1, _cueNames.Length), "Building cue audit rows.", false, "Cue rows");
                        return PungentAuditScanStepResult.Continue("Building cue audit rows.");

                    case Stage.BuildRows:
                        int batch = context.Mode == PungentAuditScanMode.BackgroundIdle ? 8 : 32;
                        int limit = Math.Min(_cueNames.Length, _cueIndex + batch);
                        while (_cueIndex < limit)
                        {
                            string cueName = _cueNames[_cueIndex++];
                            if (string.IsNullOrWhiteSpace(cueName) || IsNoneCue(_profile, cueName))
                                continue;
                            AudioCoverageCueAuditRow row = new AudioCoverageCueAuditRow { CueName = cueName };
                            if (_catalogEntriesByCue.TryGetValue(cueName, out List<AudioCoverageCatalogEntryInfo> entries))
                                for (int e = 0; e < entries.Count; e++)
                                    row.EntryIndices.Add(entries[e].Index);
                            if (_profile != null)
                            {
                                row.BindingCount = _profile.CountBindingsForCue(cueName, includeIgnored: false);
                                row.IsIgnored = _profile.IsCueIgnored(cueName);
                            }
                            row.ScriptReferenceCount = _scriptHitsByCue.TryGetValue(cueName, out List<AudioCoverageScriptReferenceHit> hits) ? hits.Count : 0;
                            row.HasCoverage = row.IsIgnored || row.BindingCount > 0 || (_profile != null && _profile.treatScriptReferencesAsCoverage && row.ScriptReferenceCount > 0);
                            ValidateRow(_catalog, _profile, row, _catalogEntriesByCue);
                            _rows.Add(row);
                        }

                        float progress = _cueNames.Length == 0 ? 0.75f : 0.40f + (0.45f * _cueIndex / Mathf.Max(1, _cueNames.Length));
                        context.Report(progress, _cueIndex, _cueNames.Length, _cueIndex + " / " + _cueNames.Length + " cues checked.", false, "Cue rows");
                        if (_cueIndex < _cueNames.Length)
                            return PungentAuditScanStepResult.Continue(_cueIndex + " / " + _cueNames.Length + " cues checked.");
                        _stage = Stage.Publish;
                        return PungentAuditScanStepResult.Continue("Publishing Audio Catalog Coverage result.");

                    case Stage.Publish:
                        AddRowsToScanResult(_result, _catalog, _rows);
                        int matched = 0;
                        for (int i = 0; i < _rows.Count; i++)
                            if (_rows[i].Status != AudioCoverageCueStatus.Valid)
                                matched++;
                        string status = "Scan complete: " + _rows.Count + " cue(s), " + matched + " finding(s).";
                        PungentScanResult completed = _session.Complete(_rows.Count, matched, 0, 0, status);
                        context.Report(1f, _rows.Count, Math.Max(1, _rows.Count), status, false, "Complete");
                        _stage = Stage.Done;
                        return PungentAuditScanStepResult.Complete(status, completed);

                    default:
                        return PungentAuditScanStepResult.Complete("Audio Catalog Coverage already completed.", _result);
                }
            }

            private PungentAuditScanStepResult Cancel(string status)
            {
                if (_session != null)
                    _session.Cancel(status, false);
                _stage = Stage.Done;
                return PungentAuditScanStepResult.Cancelled(status);
            }
        }
    }
#endif
}

