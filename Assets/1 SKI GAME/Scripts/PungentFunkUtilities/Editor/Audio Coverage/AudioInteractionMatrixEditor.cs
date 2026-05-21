using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Audio;

namespace PungentFunk.Utilities.Editor.Audio
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(AudioInteractionMatrixSO))]
    public sealed class AudioInteractionMatrixEditor : Editor
    {
        private const string PrefPrefix = "GenericUtility.AudioInteractionMatrixEditor.";
        private const string PrefLibraryGuid = PrefPrefix + "LibraryGuid";
        private const string PrefShowExact = PrefPrefix + "ShowExact";
        private const string PrefShowMissing = PrefPrefix + "ShowMissing";
        private const string PrefMissingLimit = PrefPrefix + "MissingLimit";

        private AudioMaterialLibrarySO _library;
        private Vector2 _missingScroll;
        private bool _showExact = true;
        private bool _showMissing = true;
        private int _missingDisplayLimit = 40;

        private sealed class MissingPair
        {
            public AudioSurfaceMaterialSO A;
            public AudioSurfaceMaterialSO B;
            public string Label;
        }

        private void OnEnable()
        {
            _showExact = UtilityWindowPrefs.GetBool(PrefShowExact, _showExact);
            _showMissing = UtilityWindowPrefs.GetBool(PrefShowMissing, _showMissing);
            _missingDisplayLimit = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefMissingLimit, _missingDisplayLimit), 5, 500);
            _library = LoadAssetFromGuid<AudioMaterialLibrarySO>(UtilityWindowPrefs.GetString(PrefLibraryGuid, string.Empty));
            if (_library == null)
                _library = TryFindLibrary();
        }

        private void OnDisable()
        {
            SavePrefs();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AudioInteractionMatrixSO matrix = (AudioInteractionMatrixSO)target;

            DrawHeader(matrix);
            DrawMatrixProperties();
            DrawCoverageConfiguration();

            if (_library != null)
                DrawCoverage(matrix, _library);
            else
                DrawNoLibraryHelp();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawHeader(AudioInteractionMatrixSO matrix)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                int exactCount = CountNonNull(matrix != null ? matrix.ExactProfiles : null);
                int fallbackCount = CountFallbacks(matrix != null ? matrix.CategoryFallbacks : null);
                UtilityWindowTheme.SectionTitle("Audio Interaction Matrix", UtilityWindowTheme.Blue, $"{exactCount} exact / {fallbackCount} fallback");
                EditorGUILayout.LabelField(
                    "Defines contact-audio profiles for material pairs. Coverage checks compare this matrix against an AudioMaterialLibrarySO and highlight unhandled pairs.",
                    UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Open Setup Coverage", UtilityWindowTheme.Teal, GUILayout.Height(22f)))
                        AudioCoverageContextWindow.Open();

                    using (new EditorGUI.DisabledScope(_library == null))
                    {
                        if (UtilityWindowTheme.TintedButton("Ping Library", UtilityWindowTheme.Neutral, GUILayout.Width(100f), GUILayout.Height(22f)))
                            PingAndSelect(_library);
                    }
                }
            }
        }

        private void DrawMatrixProperties()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                UtilityWindowTheme.SectionTitle("Matrix Authoring", UtilityWindowTheme.Purple);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("exactProfiles"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("categoryFallbacks"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultSoftProfile"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultHardProfile"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultScrapeProfile"));
            }
        }

        private void DrawCoverageConfiguration()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Cyan)))
            {
                UtilityWindowTheme.SectionTitle("Coverage Source", UtilityWindowTheme.Cyan, _library != null ? _library.name : "No Library");
                EditorGUI.BeginChangeCheck();
                _library = (AudioMaterialLibrarySO)EditorGUILayout.ObjectField(
                    new GUIContent("Coverage Library", "Material library used to determine every material pair that should have exact or fallback coverage."),
                    _library,
                    typeof(AudioMaterialLibrarySO),
                    false);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Find Library", GUILayout.Width(92f)))
                        _library = TryFindLibrary();
                    if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                        Repaint();
                    GUILayout.FlexibleSpace();
                    _showExact = GUILayout.Toggle(_showExact, "Show Exact", EditorStyles.miniButtonLeft, GUILayout.Width(88f));
                    _showMissing = GUILayout.Toggle(_showMissing, "Show Missing", EditorStyles.miniButtonRight, GUILayout.Width(98f));
                }

                _missingDisplayLimit = EditorGUILayout.IntSlider(
                    new GUIContent("Missing Display Limit", "Maximum missing pair rows to draw in this inspector."),
                    _missingDisplayLimit,
                    5,
                    500);
            }
        }

        private void DrawNoLibraryHelp()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                UtilityWindowTheme.SectionTitle("Coverage", UtilityWindowTheme.Amber, "library required");
                EditorGUILayout.HelpBox("Assign an AudioMaterialLibrarySO to inspect pair coverage gaps.", MessageType.Info);
            }
        }

        private void DrawCoverage(AudioInteractionMatrixSO matrix, AudioMaterialLibrarySO library)
        {
            IReadOnlyList<AudioSurfaceMaterialSO> materials = library.GetAll();
            List<MissingPair> missing = FindMissingPairs(matrix, materials);
            List<string> duplicatePairs = FindDuplicateExactPairs(matrix);
            int exactCount = CountNonNull(matrix.ExactProfiles);
            int fallbackCount = CountFallbacks(matrix.CategoryFallbacks);
            int materialCount = CountNonNull(materials);
            int totalExpectedPairs = materialCount * (materialCount + 1) / 2;

            Color panelTint = missing.Count > 0 || duplicatePairs.Count > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(panelTint)))
            {
                UtilityWindowTheme.SectionTitle("Coverage", panelTint, missing.Count == 0 ? "complete" : $"{missing.Count} missing");

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"Materials {materialCount}", UtilityWindowTheme.Blue);
                    UtilityWindowTheme.CountPill($"Expected Pairs {totalExpectedPairs}", UtilityWindowTheme.Purple);
                    UtilityWindowTheme.CountPill($"Exact {exactCount}", UtilityWindowTheme.Teal);
                    UtilityWindowTheme.CountPill($"Fallbacks {fallbackCount}", UtilityWindowTheme.Cyan);
                    UtilityWindowTheme.CountPill($"Missing {missing.Count}", missing.Count == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber);
                }

                if (duplicatePairs.Count > 0)
                {
                    EditorGUILayout.HelpBox($"Duplicate exact pair profiles found: {duplicatePairs.Count}. Matrix resolution uses the first matching profile.", MessageType.Warning);
                    for (int i = 0; i < Mathf.Min(duplicatePairs.Count, 10); i++)
                        EditorGUILayout.LabelField($"• {duplicatePairs[i]}", UtilityWindowTheme.MutedMiniLabelStyle);
                }

                if (_showExact)
                    DrawExactProfiles(matrix);

                if (_showMissing)
                    DrawMissingPairs(missing);
            }
        }

        private void DrawExactProfiles(AudioInteractionMatrixSO matrix)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.12f, 0.06f, 6, 3)))
            {
                UtilityWindowTheme.SectionTitle("Exact Profiles", UtilityWindowTheme.Teal, CountNonNull(matrix.ExactProfiles).ToString());
                IReadOnlyList<AudioInteractionProfileSO> exactProfiles = matrix.ExactProfiles;
                if (exactProfiles == null || exactProfiles.Count == 0)
                {
                    EditorGUILayout.LabelField("No exact profiles assigned.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                for (int i = 0; i < exactProfiles.Count; i++)
                {
                    AudioInteractionProfileSO profile = exactProfiles[i];
                    if (profile == null)
                        continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(profile, typeof(AudioInteractionProfileSO), false);
                        EditorGUILayout.LabelField(GetPairLabel(profile.MaterialA, profile.MaterialB), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(150f));
                        if (GUILayout.Button("Open", GUILayout.Width(56f)))
                            PingAndSelect(profile);
                    }
                }
            }
        }

        private void DrawMissingPairs(List<MissingPair> missing)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(missing.Count == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 0.12f, 0.06f, 6, 3)))
            {
                UtilityWindowTheme.SectionTitle("Missing Pair Coverage", missing.Count == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, missing.Count.ToString());
                if (missing.Count == 0)
                {
                    EditorGUILayout.HelpBox("No pair coverage gaps found against the selected library.", MessageType.Info);
                    return;
                }

                EditorGUILayout.HelpBox("These material pairs have neither an exact profile nor a category fallback. Add exact profiles or category fallbacks where the default soft/hard/scrape fallback is not specific enough.", MessageType.Warning);
                int drawCount = Mathf.Min(missing.Count, _missingDisplayLimit);
                _missingScroll = EditorGUILayout.BeginScrollView(_missingScroll, GUILayout.MinHeight(80f), GUILayout.MaxHeight(220f));
                for (int i = 0; i < drawCount; i++)
                {
                    MissingPair pair = missing[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"• {pair.Label}", UtilityWindowTheme.PathLabelStyle);
                        if (GUILayout.Button("A", GUILayout.Width(28f)))
                            PingAndSelect(pair.A);
                        if (GUILayout.Button("B", GUILayout.Width(28f)))
                            PingAndSelect(pair.B);
                    }
                }
                EditorGUILayout.EndScrollView();

                if (missing.Count > drawCount)
                    EditorGUILayout.LabelField($"Showing {drawCount} of {missing.Count}. Increase the display limit to inspect more.", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private static List<MissingPair> FindMissingPairs(AudioInteractionMatrixSO matrix, IReadOnlyList<AudioSurfaceMaterialSO> materials)
        {
            List<MissingPair> missing = new List<MissingPair>();
            if (matrix == null || materials == null)
                return missing;

            for (int i = 0; i < materials.Count; i++)
            {
                AudioSurfaceMaterialSO a = materials[i];
                if (a == null)
                    continue;

                for (int j = i; j < materials.Count; j++)
                {
                    AudioSurfaceMaterialSO b = materials[j];
                    if (b == null)
                        continue;

                    if (matrix.FindExact(a, b) != null || matrix.FindCategoryFallback(a, b) != null)
                        continue;

                    missing.Add(new MissingPair
                    {
                        A = a,
                        B = b,
                        Label = GetPairLabel(a, b)
                    });
                }
            }

            return missing;
        }

        private static List<string> FindDuplicateExactPairs(AudioInteractionMatrixSO matrix)
        {
            List<string> duplicates = new List<string>();
            if (matrix == null || matrix.ExactProfiles == null)
                return duplicates;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < matrix.ExactProfiles.Count; i++)
            {
                AudioInteractionProfileSO profile = matrix.ExactProfiles[i];
                if (profile == null || profile.MaterialA == null || profile.MaterialB == null)
                    continue;

                string key = GetPairKey(profile.MaterialA, profile.MaterialB);
                if (!seen.Add(key) && reported.Add(key))
                    duplicates.Add(GetPairLabel(profile.MaterialA, profile.MaterialB));
            }

            return duplicates;
        }

        private static string GetPairKey(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b)
        {
            int aId = a != null ? a.GetInstanceID() : 0;
            int bId = b != null ? b.GetInstanceID() : 0;
            if (aId > bId)
            {
                int temp = aId;
                aId = bId;
                bId = temp;
            }

            return $"{aId}:{bId}";
        }

        private static string GetPairLabel(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b)
        {
            string aName = a != null ? a.name : "?";
            string bName = b != null ? b.name : "?";
            return $"{aName} x {bName}";
        }

        private static int CountNonNull<T>(IReadOnlyList<T> list) where T : UnityEngine.Object
        {
            if (list == null)
                return 0;

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    count++;
            }

            return count;
        }

        private static int CountFallbacks(IReadOnlyList<AudioCategoryFallbackRule> list)
        {
            if (list == null)
                return 0;

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].profile != null)
                    count++;
            }

            return count;
        }

        private static AudioMaterialLibrarySO TryFindLibrary()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioMaterialLibrarySO");
            if (guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<AudioMaterialLibrarySO>(path);
        }

        private static T LoadAssetFromGuid<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void PingAndSelect(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefShowExact, _showExact);
            UtilityWindowPrefs.SetBool(PrefShowMissing, _showMissing);
            UtilityWindowPrefs.SetInt(PrefMissingLimit, _missingDisplayLimit);
            string path = _library != null ? AssetDatabase.GetAssetPath(_library) : string.Empty;
            UtilityWindowPrefs.SetString(PrefLibraryGuid, string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
        }
    }
    #endif

}
