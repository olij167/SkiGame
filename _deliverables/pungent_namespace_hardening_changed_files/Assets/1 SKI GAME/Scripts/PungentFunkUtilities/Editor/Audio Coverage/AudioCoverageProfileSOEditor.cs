using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Audio;

namespace PungentFunk.Utilities.Editor.Audio
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(AudioCoverageProfileSO))]
    public sealed class AudioCoverageProfileSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AudioCoverageProfileSO profile = (AudioCoverageProfileSO)target;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Audio Coverage Profile", UtilityWindowTheme.Blue, profile.cueBindings != null ? profile.cueBindings.Count.ToString() : null);
                EditorGUILayout.LabelField(
                    "Defines cue-to-component/event binding coverage, runtime source diagnostics, and setup validation rules for the Audio Coverage tools.",
                    UtilityWindowTheme.MutedMiniLabelStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Open Catalog Coverage", UtilityWindowTheme.Blue, GUILayout.Width(160f)))
                    {
                        SessionState.SetString("GenericUtility.AudioCoverage.ActiveProfileGuid", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile)));
                        AudioCoverageWindow.Open();
                    }

                    if (UtilityWindowTheme.TintedButton("Open Setup Coverage", UtilityWindowTheme.Teal, GUILayout.Width(150f)))
                        AudioCoverageContextWindow.Open();

                    if (UtilityWindowTheme.TintedButton("Reset Generic Defaults", UtilityWindowTheme.Amber, GUILayout.Width(160f)))
                    {
                        if (EditorUtility.DisplayDialog("Reset Audio Coverage Profile", "Replace this profile's rules with the default generic audio coverage profile?", "Reset", "Cancel"))
                        {
                            Undo.RecordObject(profile, "Reset Audio Coverage Profile");
                            profile.ResetToGenericDefaults(clearExistingRules: true);
                            EditorUtility.SetDirty(profile);
                        }
                    }
                }
            }

            EditorGUILayout.Space(6f);
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }
    }
    #endif

}