using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    public static class PungentTokenGUI
    {
        public static void DrawTokenChip(string key, bool known)
        {
            key = PungentTokenParser.NormalizeKey(key);
            string label = "{" + key + "}";

            UtilityWindowTheme.CountPill(
                label,
                known ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Amber,
                Mathf.Clamp(52f + label.Length * 6f, 72f, 132f));
        }

        public static string DrawTagsField(List<string> tags)
        {
            return EditorGUILayout.TextField("Tags", tags == null ? string.Empty : string.Join(", ", tags.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray()));
        }

        public static List<string> ParseTags(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().TrimStart('#'))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string DrawExamplesField(List<string> examples)
        {
            return EditorGUILayout.TextField("Examples", examples == null ? string.Empty : string.Join(" | ", examples.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray()));
        }

        public static List<string> ParseExamples(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
#endif
}
