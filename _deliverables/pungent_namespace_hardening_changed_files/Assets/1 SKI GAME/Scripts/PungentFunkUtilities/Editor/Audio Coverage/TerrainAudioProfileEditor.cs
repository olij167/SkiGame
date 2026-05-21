using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Audio;

namespace PungentFunk.Utilities.Editor.Audio
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(TerrainAudioMaterialProfileSO))]
    public sealed class TerrainAudioProfileEditor : Editor
    {
        private const string PrefPrefix = "GenericUtility.TerrainAudioProfileEditor.";
        private const string PrefShowDetails = PrefPrefix + "ShowDetails";

        private bool _showDetails = true;

        private sealed class ProfileDiagnostics
        {
            public int TotalBindings;
            public int CompleteBindings;
            public int MissingBindings;
            public int DuplicateLayers;
            public readonly List<string> Messages = new List<string>();
        }

        private void OnEnable()
        {
            _showDetails = UtilityWindowPrefs.GetBool(PrefShowDetails, _showDetails);
        }

        private void OnDisable()
        {
            UtilityWindowPrefs.SetBool(PrefShowDetails, _showDetails);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            TerrainAudioMaterialProfileSO profile = (TerrainAudioMaterialProfileSO)target;
            ProfileDiagnostics diagnostics = BuildDiagnostics(profile);

            DrawHeader(profile, diagnostics);
            DrawProperties();
            DrawDiagnostics(profile, diagnostics);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                UtilityWindowPrefs.SetBool(PrefShowDetails, _showDetails);
        }

        private void DrawHeader(TerrainAudioMaterialProfileSO profile, ProfileDiagnostics diagnostics)
        {
            bool hasWarnings = diagnostics.MissingBindings > 0 || diagnostics.DuplicateLayers > 0 || profile.FallbackMaterial == null;
            Color tint = hasWarnings ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint)))
            {
                UtilityWindowTheme.SectionTitle("Terrain Audio Material Profile", tint, hasWarnings ? "warnings" : "ready");
                EditorGUILayout.LabelField(
                    "Maps TerrainLayer assets to audio surface materials so terrain contacts can resolve material-specific scrape, impact, drag, and slide responses.",
                    UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"Bindings {diagnostics.TotalBindings}", UtilityWindowTheme.Blue);
                    UtilityWindowTheme.CountPill($"Complete {diagnostics.CompleteBindings}", diagnostics.CompleteBindings == diagnostics.TotalBindings ? UtilityWindowTheme.Green : UtilityWindowTheme.Teal);
                    UtilityWindowTheme.CountPill($"Missing {diagnostics.MissingBindings}", diagnostics.MissingBindings == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber);
                    UtilityWindowTheme.CountPill($"Duplicates {diagnostics.DuplicateLayers}", diagnostics.DuplicateLayers == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Open Setup Coverage", UtilityWindowTheme.Teal, GUILayout.Height(22f)))
                        AudioCoverageContextWindow.Open();

                    using (new EditorGUI.DisabledScope(profile == null || profile.FallbackMaterial == null))
                    {
                        if (UtilityWindowTheme.TintedButton("Ping Fallback", UtilityWindowTheme.Neutral, GUILayout.Width(104f), GUILayout.Height(22f)))
                            PingAndSelect(profile.FallbackMaterial);
                    }
                }
            }
        }

        private void DrawProperties()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Profile Authoring", UtilityWindowTheme.Blue);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("layerBindings"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackMaterial"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("blendHysteresis"));
            }
        }

        private void DrawDiagnostics(TerrainAudioMaterialProfileSO profile, ProfileDiagnostics diagnostics)
        {
            Color tint = diagnostics.Messages.Count == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint)))
            {
                UtilityWindowTheme.SectionTitle("Diagnostics", tint, diagnostics.Messages.Count == 0 ? "clean" : diagnostics.Messages.Count.ToString());

                _showDetails = EditorGUILayout.Foldout(_showDetails, "Show detailed diagnostics", true);
                if (!_showDetails)
                    return;

                if (profile.FallbackMaterial == null)
                    EditorGUILayout.HelpBox("Fallback material is not assigned. Terrain resolution may return null for unmapped or unresolved terrain layers.", MessageType.Warning);

                if (diagnostics.Messages.Count == 0 && profile.FallbackMaterial != null)
                {
                    EditorGUILayout.HelpBox("No incomplete or duplicate terrain layer bindings were found.", MessageType.Info);
                    return;
                }

                for (int i = 0; i < diagnostics.Messages.Count; i++)
                    EditorGUILayout.LabelField($"� {diagnostics.Messages[i]}", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private static ProfileDiagnostics BuildDiagnostics(TerrainAudioMaterialProfileSO profile)
        {
            ProfileDiagnostics diagnostics = new ProfileDiagnostics();
            if (profile == null || profile.LayerBindings == null)
                return diagnostics;

            HashSet<TerrainLayer> seen = new HashSet<TerrainLayer>();
            HashSet<TerrainLayer> duplicateReported = new HashSet<TerrainLayer>();

            IReadOnlyList<TerrainLayerAudioBinding> bindings = profile.LayerBindings;
            diagnostics.TotalBindings = bindings.Count;

            for (int i = 0; i < bindings.Count; i++)
            {
                TerrainLayerAudioBinding binding = bindings[i];
                bool missingLayer = binding.layer == null;
                bool missingMaterial = binding.material == null;

                if (missingLayer || missingMaterial)
                {
                    diagnostics.MissingBindings++;
                    diagnostics.Messages.Add($"Binding {i}: {(missingLayer ? "missing TerrainLayer" : binding.layer.name)} / {(missingMaterial ? "missing AudioSurfaceMaterialSO" : binding.material.name)}");
                }
                else
                {
                    diagnostics.CompleteBindings++;
                }

                if (binding.layer != null && !seen.Add(binding.layer) && duplicateReported.Add(binding.layer))
                {
                    diagnostics.DuplicateLayers++;
                    diagnostics.Messages.Add($"Duplicate TerrainLayer binding: {binding.layer.name}. The first matching binding will resolve.");
                }
            }

            return diagnostics;
        }

        private static void PingAndSelect(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }
    }
    #endif

}