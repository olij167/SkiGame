using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System;
    using System.Globalization;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public enum PungentSceneGizmoPresetInstanceState
    {
        Unlinked,
        Base,
        Customized,
        Outdated,
        Locked,
        Mixed,
        MissingPreset
    }

    public struct PungentSceneGizmoPresetDiffStatus
    {
        public PungentSceneGizmoPresetInstanceState state;
        public PungentSceneGizmoPreset linkedPreset;
        public string linkedPresetId;
        public string linkedPresetGuid;
        public string sourceHash;
        public string presetHash;
        public string appliedHash;
        public string stateLabel;
        public string message;
        public bool hasLinkedPreset;
        public bool hasResolvedPreset;
        public bool hasLocalChanges;
        public bool canUpdateFromPreset;
        public bool canRevertToPreset;
        public bool canOverwritePreset;
        public bool canSaveAsNewPreset;
        public bool isAssetPreset;
    }

    public static class PungentSceneGizmoPresetDiffUtility
    {
        public static PungentSceneGizmoPresetDiffStatus GetStatus(PungentSceneGizmoSource source)
        {
            PungentSceneGizmoPresetDiffStatus status = new PungentSceneGizmoPresetDiffStatus
            {
                state = PungentSceneGizmoPresetInstanceState.Unlinked,
                stateLabel = "Unlinked",
                message = "This source is not linked to a preset.",
                canSaveAsNewPreset = source != null && source.rules != null && source.rules.Count > 0
            };

            if (source == null)
                return status;

            status.linkedPreset = source.linkedPreset;
            status.linkedPresetId = string.IsNullOrWhiteSpace(source.linkedPresetId) ? PresetId(source.linkedPreset) : source.linkedPresetId;
            status.linkedPresetGuid = source.linkedPresetGuid;
            status.appliedHash = source.linkedPresetHash ?? string.Empty;
            status.hasLinkedPreset = source.linkedPreset != null ||
                                     !string.IsNullOrWhiteSpace(source.linkedPresetGuid) ||
                                     !string.IsNullOrWhiteSpace(source.linkedPresetId);

            status.sourceHash = ComputeSourceHash(source);
            if (!status.hasLinkedPreset)
                return status;

            if (!TryResolveLinkedPreset(source, out PungentSceneGizmoPreset preset, out bool destroyWhenDone))
            {
                status.state = PungentSceneGizmoPresetInstanceState.MissingPreset;
                status.stateLabel = "Missing";
                status.message = "The linked preset could not be resolved. Relink, save as new, or clear the link.";
                return status;
            }

            try
            {
                status.linkedPreset = preset;
                status.linkedPresetId = PresetId(preset);
                status.linkedPresetGuid = GetPresetGuid(preset);
                status.presetHash = ComputePresetHash(preset);
                status.hasResolvedPreset = true;
                status.isAssetPreset = IsPersistentPreset(preset);

                bool mixed = IsMixedOrAppended(source, preset, status.linkedPresetId);
                bool matchesCurrentPreset = string.Equals(status.sourceHash, status.presetHash, StringComparison.Ordinal);
                bool matchesAppliedSnapshot = !string.IsNullOrWhiteSpace(status.appliedHash) &&
                                              string.Equals(status.sourceHash, status.appliedHash, StringComparison.Ordinal);

                if (source.lockPresetPropagation)
                {
                    status.state = PungentSceneGizmoPresetInstanceState.Locked;
                    status.stateLabel = "Locked";
                    status.message = matchesCurrentPreset
                        ? "Linked and locked. Automatic workspace updates will skip this source."
                        : "Locked with local differences. Unlock before batch updating from the preset.";
                }
                else if (mixed)
                {
                    status.state = PungentSceneGizmoPresetInstanceState.Mixed;
                    status.stateLabel = "Mixed";
                    status.message = "This source has appended or multiple preset content. Save as a new preset or edit it as a custom source.";
                }
                else if (matchesCurrentPreset)
                {
                    status.state = PungentSceneGizmoPresetInstanceState.Base;
                    status.stateLabel = "Base";
                    status.message = "This source matches its linked preset.";
                }
                else if (matchesAppliedSnapshot && !string.Equals(status.appliedHash, status.presetHash, StringComparison.Ordinal))
                {
                    status.state = PungentSceneGizmoPresetInstanceState.Outdated;
                    status.stateLabel = "Outdated";
                    status.message = "The linked preset changed after this source was applied. Update when ready.";
                }
                else
                {
                    status.state = PungentSceneGizmoPresetInstanceState.Customized;
                    status.stateLabel = "Custom";
                    status.message = "This source has local edits that differ from the linked preset.";
                }

                status.hasLocalChanges = status.state == PungentSceneGizmoPresetInstanceState.Customized ||
                                         status.state == PungentSceneGizmoPresetInstanceState.Mixed ||
                                         (status.state == PungentSceneGizmoPresetInstanceState.Locked && !matchesCurrentPreset);
                bool differsFromPreset = !matchesCurrentPreset || status.state == PungentSceneGizmoPresetInstanceState.Mixed;
                status.canUpdateFromPreset = status.hasResolvedPreset && !source.lockPresetPropagation && differsFromPreset;
                status.canRevertToPreset = status.hasResolvedPreset && differsFromPreset;
                status.canOverwritePreset = status.isAssetPreset && differsFromPreset;
                status.canSaveAsNewPreset = source.rules != null && source.rules.Count > 0;
                return status;
            }
            finally
            {
                if (destroyWhenDone && preset != null)
                    UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        public static bool TryResolveLinkedPreset(PungentSceneGizmoSource source, out PungentSceneGizmoPreset preset, out bool destroyWhenDone)
        {
            preset = null;
            destroyWhenDone = false;
            if (source == null)
                return false;

            if (source.linkedPreset != null)
            {
                preset = source.linkedPreset;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(source.linkedPresetGuid))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(source.linkedPresetGuid);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    preset = AssetDatabase.LoadAssetAtPath<PungentSceneGizmoPreset>(assetPath);
                    if (preset != null)
                        return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(source.linkedPresetId))
            {
                PungentSceneGizmoBuiltInPreset builtIn = PungentSceneGizmoPresetLibrary.Find(source.linkedPresetId);
                if (builtIn != null)
                {
                    preset = PungentSceneGizmoPresetLibrary.CreateTransientPreset(builtIn);
                    destroyWhenDone = preset != null;
                    return preset != null;
                }
            }

            return false;
        }

        public static string ComputeSourceHash(PungentSceneGizmoSource source)
        {
            if (source == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder(2048);
            AppendSourceDefaults(builder, source.drawInScene, source.drawLabels, source.drawOnlyWhenComponentEnabled);
            AppendRules(builder, source.rules);
            return Hash(builder.ToString());
        }

        public static string ComputePresetHash(PungentSceneGizmoPreset preset)
        {
            if (preset == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder(2048);
            AppendSourceDefaults(builder, preset.drawInScene, preset.drawLabels, preset.drawOnlyWhenComponentEnabled);
            AppendRules(builder, preset.rules);
            return Hash(builder.ToString());
        }

        public static string GetPresetGuid(PungentSceneGizmoPreset preset)
        {
            if (preset == null || !IsPersistentPreset(preset))
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(preset);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        public static string PresetId(PungentSceneGizmoPreset preset)
        {
            if (preset == null)
                return string.Empty;

            return string.IsNullOrWhiteSpace(preset.presetId) ? preset.name : preset.presetId;
        }

        public static bool IsPersistentPreset(PungentSceneGizmoPreset preset)
        {
            return preset != null && EditorUtility.IsPersistent(preset) && !string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(preset));
        }

        private static bool IsMixedOrAppended(PungentSceneGizmoSource source, PungentSceneGizmoPreset preset, string presetId)
        {
            int sourceCount = source.rules != null ? source.rules.Count : 0;
            int presetCount = preset.rules != null ? preset.rules.Count : 0;
            if (sourceCount != presetCount)
                return true;

            if (string.IsNullOrWhiteSpace(presetId) || source.rules == null)
                return false;

            for (int i = 0; i < source.rules.Count; i++)
            {
                PungentSceneGizmoSource.GizmoRule rule = source.rules[i];
                if (rule == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(rule.presetId) &&
                    !string.Equals(rule.presetId, presetId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void AppendSourceDefaults(StringBuilder builder, bool drawInScene, bool drawLabels, bool drawOnlyWhenComponentEnabled)
        {
            builder.Append("source|");
            builder.Append(drawInScene ? "1" : "0").Append('|');
            builder.Append(drawLabels ? "1" : "0").Append('|');
            builder.Append(drawOnlyWhenComponentEnabled ? "1" : "0").AppendLine();
        }

        private static void AppendRules(StringBuilder builder, System.Collections.Generic.IList<PungentSceneGizmoSource.GizmoRule> rules)
        {
            int count = rules != null ? rules.Count : 0;
            builder.Append("rules=").Append(count.ToString(CultureInfo.InvariantCulture)).AppendLine();
            if (rules == null)
                return;

            for (int i = 0; i < rules.Count; i++)
                AppendRule(builder, rules[i]);
        }

        private static void AppendRule(StringBuilder builder, PungentSceneGizmoSource.GizmoRule rule)
        {
            if (rule == null)
            {
                builder.AppendLine("rule=null");
                return;
            }

            builder.Append("rule|");
            Append(builder, rule.name);
            Append(builder, rule.enabled);
            Append(builder, rule.drawWhen);
            Append(builder, rule.shape);
            Append(builder, rule.color);
            Append(builder, rule.positionMode);
            Append(builder, rule.localOffset);
            Append(builder, rule.worldPosition);
            Append(builder, rule.positionFieldPath);
            Append(builder, rule.sizeMode);
            Append(builder, rule.size);
            Append(builder, rule.vectorSize);
            Append(builder, rule.sizeFieldPath);
            Append(builder, rule.direction);
            Append(builder, rule.directionFieldPath);
            Append(builder, rule.label);
            Append(builder, rule.labelFieldPath);
            Append(builder, rule.useTargetRotation);
            Append(builder, rule.condition);
            Append(builder, rule.conditionFieldPath);
            Append(builder, rule.conditionExpectedValue);
            Append(builder, rule.maxDrawDistance);
            Append(builder, rule.drawLabelsWhenSelectedOnly);
            Append(builder, rule.useStateColorMap);
            AppendStateColorMap(builder, rule.stateColorMap);
            builder.AppendLine();
        }

        private static void AppendStateColorMap(StringBuilder builder, PungentSceneGizmoStateColorMap map)
        {
            if (map == null)
            {
                builder.Append("map:null|");
                return;
            }

            Append(builder, map.enabled);
            Append(builder, map.fallbackColor);
            Append(builder, map.useFallbackWhenNoEntryMatches);
            int count = map.entries != null ? map.entries.Count : 0;
            Append(builder, count);
            if (map.entries == null)
                return;

            for (int i = 0; i < map.entries.Count; i++)
            {
                PungentSceneGizmoStateColorEntry entry = map.entries[i];
                if (entry == null)
                {
                    builder.Append("entry:null|");
                    continue;
                }

                Append(builder, entry.name);
                Append(builder, entry.enabled);
                Append(builder, entry.priority);
                Append(builder, entry.color);
                Append(builder, entry.sourceKind);
                Append(builder, entry.memberPath);
                Append(builder, entry.expectedValue);
                Append(builder, entry.numericComparison);
                Append(builder, entry.numericThreshold);
                Append(builder, entry.numericThresholdMax);
                Append(builder, entry.animatorParameter);
                Append(builder, entry.activeStateProviderPath);
            }
        }

        private static void Append(StringBuilder builder, bool value)
        {
            builder.Append(value ? "1" : "0").Append('|');
        }

        private static void Append(StringBuilder builder, int value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
        }

        private static void Append(StringBuilder builder, float value)
        {
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value ?? string.Empty).Append('|');
        }

        private static void Append(StringBuilder builder, Enum value)
        {
            builder.Append(value != null ? value.ToString() : string.Empty).Append('|');
        }

        private static void Append(StringBuilder builder, Color value)
        {
            builder.Append(ColorUtility.ToHtmlStringRGBA(value)).Append('|');
        }

        private static void Append(StringBuilder builder, Vector3 value)
        {
            builder.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static string Hash(string value)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= prime;
                    }
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
    }
#endif
}
