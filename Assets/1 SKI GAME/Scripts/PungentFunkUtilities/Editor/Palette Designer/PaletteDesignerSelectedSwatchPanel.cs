using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Colour;

namespace PungentFunk.Utilities.Editor.Colour
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public partial class PaletteDesignerWindow
    {
        private void DrawSelectedSwatchPanel()
        {
            using (BeginStudioCard("Selected Swatch", UtilityWindowTheme.Teal, IsValidSelectedSwatch() ? $"#{_selectedSwatch + 1}" : "none", 0.10f, 0.05f))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Edit the colour, role, order, and copy formats for the selected swatch.", UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                    if (IsValidSelectedSwatch())
                    {
                        PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
                        UtilityWindowTheme.CountPill(selected.locked ? "Locked" : "Editable", selected.locked ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 76f);
                    }
                }

                if (!IsValidSelectedSwatch())
                {
                    DrawStudioHelpCard("No swatch selected", "Select a colour in the palette to edit role, colour, notes, order, and copy formats here.", UtilityWindowTheme.Teal);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (StudioButton("Add Swatch", UtilityWindowTheme.Green, PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                            AddSwatch();
                        if (StudioButton(PrimaryIterationLabel(), UtilityWindowTheme.Purple, PaletteDesignerButtonTone.Primary, GUILayout.Height(26f)))
                            IteratePalette();
                        GUILayout.FlexibleSpace();
                    }
                    return;
                }

                PaletteSwatch swatch = _activePalette.swatches[_selectedSwatch];
                bool wide = CurrentContentWidth() >= 760f;
                if (wide)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawSelectedSwatchPreviewBlock(swatch, 148f);
                        DrawSelectedSwatchFields(swatch);
                        DrawSelectedSwatchOperations(swatch, 230f);
                    }
                }
                else
                {
                    DrawSelectedSwatchPreviewBlock(swatch, CurrentContentWidth());
                    DrawSelectedSwatchFields(swatch);
                    DrawSelectedSwatchOperations(swatch, -1f);
                }
            }
        }

        private void DrawSelectedSwatchPreviewBlock(PaletteSwatch swatch, float width)
        {
            float resolvedWidth = width > 0f ? Mathf.Clamp(width, 128f, 190f) : 148f;
            using (BeginStudioCard(null, UtilityWindowTheme.Neutral, null, 0.065f, 0.03f, GUILayout.Width(resolvedWidth)))
            {
                Rect preview = GUILayoutUtility.GetRect(resolvedWidth - 8f, 96f, GUILayout.ExpandWidth(true), GUILayout.Height(96f));
                EditorGUI.DrawRect(preview, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                EditorGUI.DrawRect(new Rect(preview.x, preview.yMax - 12f, preview.width, 12f), ColourContrastUtility.GetReadableTextColor(swatch.color));
                EditorGUILayout.LabelField(ColourConversionUtility.ToHexRGB(swatch.color), UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.LabelField(Nicify(swatch.role.ToString()), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawSelectedSwatchFields(PaletteSwatch swatch)
        {
            using (BeginStudioCard("Details", UtilityWindowTheme.Neutral, null, 0.065f, 0.03f))
            {
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.TextField("Name", swatch.name);
                Color color = EditorGUILayout.ColorField(new GUIContent("Colour"), swatch.color, true, true, false);
                PaletteSwatchRole role = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Role", swatch.role);
                int priority = EditorGUILayout.IntField("Priority", swatch.priority);
                string notes = EditorGUILayout.TextField("Notes", swatch.notes);
                if (EditorGUI.EndChangeCheck())
                {
                    ChangePalette("Edit Swatch", () =>
                    {
                        swatch.name = name;
                        swatch.color = color;
                        swatch.role = role;
                        swatch.priority = priority;
                        swatch.notes = notes;
                    });
                }
            }
        }

        private void DrawSelectedSwatchOperations(PaletteSwatch swatch, float width)
        {
            GUILayoutOption[] widthOptions = width > 0f ? new[] { GUILayout.Width(width) } : new GUILayoutOption[0];
            using (BeginStudioCard("Operations", UtilityWindowTheme.Teal, null, 0.075f, 0.035f, widthOptions))
            {
                DrawSelectedActionGroup("Generate", UtilityWindowTheme.Purple, () =>
                {
                    using (new EditorGUI.DisabledScope(swatch.locked))
                    {
                        if (StudioButton("Regenerate Swatch", UtilityWindowTheme.Purple, PaletteDesignerButtonTone.Secondary, GUILayout.Height(24f)))
                            RegenerateSwatch(_selectedSwatch);
                    }
                });

                DrawSelectedActionGroup("Lock", swatch.locked ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, () =>
                {
                    if (StudioButton(swatch.locked ? "Unlock" : "Lock", swatch.locked ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, PaletteDesignerButtonTone.Secondary, GUILayout.Height(22f)))
                        ChangePalette("Toggle Swatch Lock", () => swatch.locked = !swatch.locked);
                });

                DrawSelectedActionGroup("Move", UtilityWindowTheme.Blue, () =>
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(_selectedSwatch <= 0))
                        {
                            if (StudioButton("Earlier", UtilityWindowTheme.Blue, PaletteDesignerButtonTone.Ghost, GUILayout.Height(22f)))
                                MoveSelectedSwatch(-1);
                        }
                        using (new EditorGUI.DisabledScope(_activePalette.swatches == null || _selectedSwatch >= _activePalette.swatches.Count - 1))
                        {
                            if (StudioButton("Later", UtilityWindowTheme.Blue, PaletteDesignerButtonTone.Ghost, GUILayout.Height(22f)))
                                MoveSelectedSwatch(1);
                        }
                    }
                });

                DrawSelectedActionGroup("Copy", UtilityWindowTheme.Cyan, () =>
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (StudioButton("HEX", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Ghost, GUILayout.Height(22f)))
                            CopyText(ColourConversionUtility.ToHexRGB(swatch.color), "Copied Hex RGB.");
                        if (StudioButton("RGBA", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Ghost, GUILayout.Height(22f)))
                            CopyText(ColourConversionUtility.ToHexRGBA(swatch.color), "Copied Hex RGBA.");
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (StudioButton("RGB", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Ghost, GUILayout.Height(22f)))
                            CopyText(ColourConversionUtility.FormatRGB(swatch.color), "Copied RGB.");
                        if (StudioButton("HSV", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Ghost, GUILayout.Height(22f)))
                            CopyText(ColourConversionUtility.FormatHSV(swatch.color), "Copied HSV.");
                    }
                });

                DrawSelectedActionGroup("Danger", UtilityWindowTheme.Red, () =>
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (StudioButton("Duplicate", UtilityWindowTheme.Blue, PaletteDesignerButtonTone.Ghost, GUILayout.Height(22f)))
                            DuplicateSelectedSwatch();
                        if (StudioButton("Remove", UtilityWindowTheme.Red, PaletteDesignerButtonTone.Danger, GUILayout.Height(22f)))
                            RemoveSelectedSwatch();
                    }
                });
            }
        }

        private void DrawSelectedActionGroup(string title, Color tint, System.Action draw)
        {
            using (BeginStudioCard(title, tint, null, 0.055f, 0.025f))
            {
                draw?.Invoke();
            }
        }
    }
#endif
}
