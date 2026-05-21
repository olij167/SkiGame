namespace PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring
{
#if UNITY_EDITOR && PUNGENTFUNK_INTERNAL_DEVTOOLS
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public static class UtilityThemePresetSourceWriter
    {
        public const string GeneratedAssetPath = "Assets/1 SKI GAME/Scripts/PungentFunkUtilities/Editor/Utilities Core/Window Themes/UtilityThemePresetLibrary.Generated.cs";

        public static IReadOnlyList<UtilityThemePresetSnapshot> LoadGeneratedSnapshots()
        {
            return UtilityThemePresetLibrary.GeneratedPresets
                .Select(UtilityThemePresetSnapshot.FromDefinition)
                .OrderBy(s => (int)s.preset)
                .ToArray();
        }

        public static bool OverwriteSelectedWithCurrent(out string message)
        {
            UtilityWindowTheme.ThemePreset preset = UtilityWindowTheme.ActivePreset;
            var snapshots = LoadGeneratedSnapshots().ToList();
            int index = snapshots.FindIndex(s => s.preset == preset);
            if (index < 0)
            {
                message = "The active preset is a fallback/legacy entry. Add it as a generated preset before overwriting.";
                return false;
            }

            snapshots[index] = UtilityThemePresetSnapshot.CaptureCurrent(preset, hiddenOverride: snapshots[index].hiddenFromGallery);
            return WriteSnapshots(snapshots, true, out message);
        }

        public static bool SetArchiveState(UtilityWindowTheme.ThemePreset preset, bool archived, out string message)
        {
            var snapshots = LoadGeneratedSnapshots().ToList();
            UtilityThemePresetSnapshot snapshot = snapshots.FirstOrDefault(s => s.preset == preset);
            if (snapshot == null)
            {
                message = "Only generated preset entries can be archived or unarchived by the source writer.";
                return false;
            }

            snapshot.hiddenFromGallery = archived;
            if (archived && !snapshot.tags.Any(t => string.Equals(t, "archived", StringComparison.OrdinalIgnoreCase)))
                snapshot.tags = snapshot.tags.Concat(new[] { "archived" }).ToArray();

            return WriteSnapshots(snapshots, false, out message);
        }

        public static bool RemoveGeneratedEntry(UtilityWindowTheme.ThemePreset preset, out string message)
        {
            var snapshots = LoadGeneratedSnapshots().Where(s => s.preset != preset).ToList();
            if (snapshots.Count == UtilityThemePresetLibrary.GeneratedPresets.Count())
            {
                message = "No generated source entry exists for " + preset + ".";
                return false;
            }

            return WriteSnapshots(snapshots, false, out message);
        }

        public static bool AddFromCurrent(out string message)
        {
            UtilityWindowTheme.ThemePreset? slot = FindAvailableSlot();
            if (!slot.HasValue)
            {
                message = "No archived or fallback enum slot is available for a new generated preset.";
                return false;
            }

            UtilityThemePresetDefinition active = UtilityThemePresetLibrary.Get(UtilityWindowTheme.ActivePreset);
            string displayName = active.DisplayName + " Copy";
            var snapshots = LoadGeneratedSnapshots().ToList();
            snapshots.Add(UtilityThemePresetSnapshot.CaptureCurrent(slot.Value, displayName, hiddenOverride: false));
            return WriteSnapshots(snapshots, true, out message);
        }

        public static string BuildValidationWarning(UtilityThemePresetSnapshot snapshot)
        {
            var warnings = new List<string>();
            CheckContrast(warnings, "title text/header", snapshot, UtilityWindowTheme.RoleTitleText, UtilityWindowTheme.RoleHeader, 5.5f);
            CheckContrast(warnings, "body text/panel", snapshot, UtilityWindowTheme.RoleCardText, UtilityWindowTheme.RoleHeader, 4.5f);
            CheckContrast(warnings, "muted text/panel", snapshot, UtilityWindowTheme.RoleMutedText, UtilityWindowTheme.RoleHeader, 3.0f);

            if (snapshot.panelAlphaDark < 0.08f || snapshot.panelAlphaDark > 0.60f)
                warnings.Add("panel dark alpha is outside the expected 0.08-0.60 range");
            if (snapshot.panelBorderOpacity < 0.05f || snapshot.panelBorderOpacity > 1.0f)
                warnings.Add("border opacity is outside the expected readable range");
            if (snapshot.density < 0.75f || snapshot.density > 1.35f)
                warnings.Add("density is outside the supported theme range");

            return warnings.Count == 0 ? string.Empty : string.Join("\n", warnings.ToArray());
        }

        private static bool WriteSnapshots(List<UtilityThemePresetSnapshot> snapshots, bool reapplyActive, out string message)
        {
            string source = UtilityThemePresetSourceGenerator.Generate(snapshots);
            string absolutePath = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, GeneratedAssetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, source);
            AssetDatabase.Refresh();

            if (reapplyActive)
                EditorApplication.delayCall += () => UtilityWindowTheme.ApplyPreset(UtilityWindowTheme.ActivePreset);

            message = "Wrote " + snapshots.Count + " generated preset definition(s).";
            return true;
        }

        private static UtilityWindowTheme.ThemePreset? FindAvailableSlot()
        {
            var generated = new HashSet<UtilityWindowTheme.ThemePreset>(UtilityThemePresetLibrary.GeneratedPresets.Select(p => p.Preset));
            foreach (UtilityThemePresetDefinition preset in UtilityThemePresetLibrary.Presets.OrderBy(p => (int)p.Preset))
            {
                if (!generated.Contains(preset.Preset) && preset.HiddenFromGallery)
                    return preset.Preset;
            }

            return null;
        }

        private static void CheckContrast(List<string> warnings, string label, UtilityThemePresetSnapshot snapshot, string foregroundRole, string backgroundRole, float target)
        {
            if (!snapshot.colors.TryGetValue(foregroundRole, out Color foreground) || !snapshot.colors.TryGetValue(backgroundRole, out Color background))
                return;

            float ratio = UtilityWindowTheme.GetContrastRatio(foreground, background);
            if (ratio < target)
                warnings.Add(label + " contrast is " + ratio.ToString("0.00") + ":1; target is " + target.ToString("0.0") + ":1");
        }
    }
#endif
}
