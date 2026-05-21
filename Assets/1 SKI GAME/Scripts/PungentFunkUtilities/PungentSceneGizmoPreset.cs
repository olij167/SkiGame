using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    [CreateAssetMenu(fileName = "Scene Gizmo Preset", menuName = "PungentFunk Utilities/Scene Tools/Scene Gizmo Preset")]
    public sealed class PungentSceneGizmoPreset : ScriptableObject
    {
        [Tooltip("Stable identifier used by browser filters and applied rule metadata.")]
        public string presetId = "scene-gizmo-preset";
        [Tooltip("Display name shown in preset menus and browser status summaries.")]
        public string displayName = "Scene Gizmo Preset";
        [Tooltip("Preset category used by menus and browser filters.")]
        public string category = "General";
        [Tooltip("Short authoring note shown in preset tooling.")]
        [TextArea(2, 4)]
        public string description = "Reusable no-code scene gizmo preset.";
        [Tooltip("If enabled, applying this preset clears existing source rules before adding preset rules.")]
        public bool replaceExistingRules;
        [Tooltip("Default scene visibility applied to sources when this preset is applied.")]
        public bool drawInScene = true;
        [Tooltip("Default label visibility applied to sources when this preset is applied.")]
        public bool drawLabels = true;
        [Tooltip("When enabled, gizmos hide if the source component is disabled.")]
        public bool drawOnlyWhenComponentEnabled = true;
        [Tooltip("Rules copied into a source when this preset is applied. Scene object references are stripped when creating preset assets from scene sources.")]
        public List<PungentSceneGizmoSource.GizmoRule> rules = new List<PungentSceneGizmoSource.GizmoRule>();

        public List<PungentSceneGizmoSource.GizmoRule> CreateRuleCopies()
        {
            List<PungentSceneGizmoSource.GizmoRule> copies = new List<PungentSceneGizmoSource.GizmoRule>();
            if (rules == null)
                return copies;

            for (int i = 0; i < rules.Count; i++)
            {
                PungentSceneGizmoSource.GizmoRule copy = PungentSceneGizmoSource.CloneRule(rules[i]);
                if (copy == null)
                    continue;

                copy.presetId = string.IsNullOrWhiteSpace(presetId) ? name : presetId;
                copy.presetCategory = category;
                copies.Add(copy);
            }

            return copies;
        }
    }
}
