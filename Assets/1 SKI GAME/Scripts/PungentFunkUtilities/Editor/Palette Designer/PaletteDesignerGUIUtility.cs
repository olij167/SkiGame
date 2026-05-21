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
        private enum PaletteDesignerButtonTone
        {
            Primary,
            Secondary,
            Danger,
            Ghost
        }

        private enum PaletteDesignerSectionTone
        {
            Summary,
            Primary,
            Detail
        }

        private float CurrentContentWidth()
        {
            if (_contentWidthOverride > 0f)
                return Mathf.Max(260f, _contentWidthOverride);

            return Mathf.Max(320f, position.width - 24f);
        }

        private float EstimatePaletteBoardHeight()
        {
            int count = _activePalette != null && _activePalette.swatches != null ? _activePalette.swatches.Count : 0;
            if (count <= 0)
                return 180f;

            if (_resolvedSwatchLayoutMode == PaletteSwatchLayoutMode.VerticalStack)
                return 112f + count * 84f;

            float available = CurrentContentWidth();
            int columns = Mathf.Clamp(Mathf.FloorToInt((available + SwatchGap) / (SwatchPreferredWidth + SwatchGap)), 1, 12);
            float tileWidth = Mathf.Floor((available - ((columns - 1) * SwatchGap)) / columns);
            while (columns > 1 && tileWidth < SwatchMinWidth)
            {
                columns--;
                tileWidth = Mathf.Floor((available - ((columns - 1) * SwatchGap)) / columns);
            }

            int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
            return 104f + rows * 170f;
        }

        private string CurrentSwatchLayoutLabel()
        {
            switch (_resolvedSwatchLayoutMode)
            {
                case PaletteSwatchLayoutMode.VerticalStack:
                    return "Stack";
                default:
                    return "Grid";
            }
        }

        private static bool TintedMiniButton(GUIContent content, Color tint, params GUILayoutOption[] options)
        {
            using (UtilityWindowTheme.Background(tint))
                return GUILayout.Button(content, EditorStyles.miniButton, options);
        }

        private EditorGUILayout.VerticalScope BeginStudioCard(string title, Color tint, string status = null, float fill = 0.085f, float border = 0.04f, params GUILayoutOption[] options)
        {
            var scope = new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, fill, border, 7, 3), options);
            if (!string.IsNullOrWhiteSpace(title))
                DrawStudioCardTitle(title, tint, status);
            return scope;
        }

        private EditorGUILayout.VerticalScope BeginInspectorSection(string title, Color tint, string status = null, PaletteDesignerSectionTone tone = PaletteDesignerSectionTone.Detail, params GUILayoutOption[] options)
        {
            GUIStyle sectionStyle = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(2, 2, tone == PaletteDesignerSectionTone.Summary ? 3 : 5, tone == PaletteDesignerSectionTone.Detail ? 5 : 7),
                margin = new RectOffset(0, 0, tone == PaletteDesignerSectionTone.Summary ? 2 : 6, tone == PaletteDesignerSectionTone.Detail ? 7 : 9)
            };

            var scope = new EditorGUILayout.VerticalScope(sectionStyle, options);
            if (!string.IsNullOrWhiteSpace(title))
                DrawStudioCardTitle(title, tint, status);
            return scope;
        }

        private GUIStyle InspectorBodyStyle()
        {
            var style = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            style.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.80f, 0.82f, 0.86f)
                : new Color(0.22f, 0.23f, 0.25f);
            return style;
        }

        private void DrawInspectorBodyText(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                EditorGUILayout.LabelField(message, InspectorBodyStyle());
        }

        private bool SimulationPreviewActive()
        {
            return _deficiencyPreview != ColourDeficiencyPreviewMode.None;
        }

        private string SimulationPreviewLabel()
        {
            return SimulationPreviewActive()
                ? "Simulation: " + _deficiencyPreview
                : "Original colours";
        }

        private string SimulationPreviewShortLabel()
        {
            return SimulationPreviewActive()
                ? "Sim: " + _deficiencyPreview
                : "Original";
        }

        private void DisableSimulationPreview()
        {
            if (!SimulationPreviewActive())
                return;

            _deficiencyPreview = ColourDeficiencyPreviewMode.None;
            SavePrefs();
            _lastStatus = "Simulation disabled. Showing original palette colours.";
            Repaint();
        }

        private Color RefineTint()
        {
            return WorkflowTint(PaletteOverlayKind.Guide);
        }

        private Color ControlsTint()
        {
            return WorkflowTint(PaletteOverlayKind.Generate);
        }

        private Color ApplyTint()
        {
            return WorkflowTint(PaletteOverlayKind.Apply);
        }

        private Color HistoryTint()
        {
            return WorkflowTint(PaletteOverlayKind.History);
        }

        private Color WorkflowTint(PaletteOverlayKind kind)
        {
            float hueOffset = 0f;
            switch (kind)
            {
                case PaletteOverlayKind.Guide:
                    hueOffset = 0.33f;
                    break;
                case PaletteOverlayKind.Apply:
                    hueOffset = 0.58f;
                    break;
                case PaletteOverlayKind.History:
                    hueOffset = 0.83f;
                    break;
            }

            WorkflowTintBase(out float baseHue, out float saturation, out float value);
            Color tint = Color.HSVToRGB(Mathf.Repeat(baseHue + hueOffset, 1f), saturation, value);
            tint.a = 1f;
            return tint;
        }

        private void WorkflowTintBase(out float hue, out float saturation, out float value)
        {
            Color.RGBToHSV(UtilityWindowTheme.Purple, out float h, out float s, out float v);
            hue = h;
            saturation = Mathf.Clamp(s, 0.42f, 0.82f);
            value = Mathf.Clamp(v * (EditorGUIUtility.isProSkin ? 0.72f : 0.62f), EditorGUIUtility.isProSkin ? 0.18f : 0.16f, EditorGUIUtility.isProSkin ? 0.42f : 0.34f);

            // Keep all inspector identity hues on one tone ladder so active white labels
            // remain readable without per-tab darkening drift.
            float[] offsets = { 0f, 0.33f, 0.58f, 0.83f };
            for (int step = 0; step < 24; step++)
            {
                float candidateValue = Mathf.Lerp(value, 0.10f, step / 23f);
                bool readable = true;
                for (int i = 0; i < offsets.Length; i++)
                {
                    Color candidate = Color.HSVToRGB(Mathf.Repeat(hue + offsets[i], 1f), saturation, candidateValue);
                    candidate.a = 1f;
                    if (UtilityWindowTheme.GetContrastRatio(Color.white, candidate) < 4.5f)
                    {
                        readable = false;
                        break;
                    }
                }

                if (readable)
                {
                    value = candidateValue;
                    return;
                }
            }

            value = 0.10f;
        }

        private Color ReadableOn(Color background)
        {
            background.a = 1f;
            return UtilityWindowTheme.GetReadableTextColor(background);
        }

        private Color EnsureReadableTint(Color tint, float minContrast)
        {
            tint.a = 1f;
            Color text = ReadableOn(tint);
            if (UtilityWindowTheme.GetContrastRatio(text, tint) >= minContrast)
                return tint;

            bool preferLightText = UtilityWindowTheme.GetContrastRatio(Color.white, tint) >= UtilityWindowTheme.GetContrastRatio(Color.black, tint);
            Color target = preferLightText ? Color.black : Color.white;
            Color result = tint;
            for (int i = 0; i < 16; i++)
            {
                result = Color.Lerp(tint, target, (i + 1) / 16f);
                result.a = 1f;
                if (UtilityWindowTheme.GetContrastRatio(ReadableOn(result), result) >= minContrast)
                    return result;
            }

            return result;
        }

        private EditorGUILayout.VerticalScope BeginRefineSection(string title, string status = null, PaletteDesignerSectionTone tone = PaletteDesignerSectionTone.Detail, params GUILayoutOption[] options)
        {
            return BeginInspectorSection(title, RefineTint(), status, tone, options);
        }

        private void DrawStudioCardTitle(string title, Color tint, string status = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrWhiteSpace(status))
                    UtilityWindowTheme.CountPill(status, tint);
            }

            DrawStudioDivider(tint, 0.46f);
        }

        private void DrawStudioDivider(Color tint, float alpha = 0.36f)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(tint.r, tint.g, tint.b, alpha));
        }

        private bool StudioButton(string label, Color tint, PaletteDesignerButtonTone tone = PaletteDesignerButtonTone.Secondary, params GUILayoutOption[] options)
        {
            return StudioButton(new GUIContent(label), tint, tone, options);
        }

        private bool StudioButton(GUIContent content, Color tint, PaletteDesignerButtonTone tone = PaletteDesignerButtonTone.Secondary, params GUILayoutOption[] options)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                padding = new RectOffset(7, 7, 0, 0)
            };

            Rect rect = GUILayoutUtility.GetRect(content, style, options);
            return StudioButton(rect, content, tint, tone);
        }

        private bool StudioButton(Rect rect, GUIContent content, Color tint, PaletteDesignerButtonTone tone = PaletteDesignerButtonTone.Secondary)
        {
            Event current = Event.current;
            bool hover = rect.Contains(current.mousePosition);
            bool enabled = GUI.enabled;

            if (current.type == EventType.Repaint)
            {
                float fillAlpha = tone == PaletteDesignerButtonTone.Primary ? 0.86f : tone == PaletteDesignerButtonTone.Ghost ? 0.08f : 0.34f;
                float borderAlpha = tone == PaletteDesignerButtonTone.Primary ? 0.92f : 0.52f;
                Color readableTint = tone == PaletteDesignerButtonTone.Primary || tone == PaletteDesignerButtonTone.Danger
                    ? EnsureReadableTint(tint, 4.5f)
                    : tint;
                Color fill = enabled
                    ? new Color(readableTint.r, readableTint.g, readableTint.b, Mathf.Clamp01(fillAlpha + (hover ? 0.10f : 0f)))
                    : new Color(0.35f, 0.35f, 0.35f, 0.18f);
                Color border = enabled
                    ? new Color(readableTint.r, readableTint.g, readableTint.b, borderAlpha)
                    : new Color(0.45f, 0.45f, 0.45f, 0.22f);
                DrawStudioBox(rect, fill, border);

                Color old = GUI.contentColor;
                if (!enabled)
                    GUI.contentColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.64f) : new Color(0.45f, 0.45f, 0.47f);
                else if (tone == PaletteDesignerButtonTone.Primary || tone == PaletteDesignerButtonTone.Danger)
                    GUI.contentColor = ReadableOn(readableTint);
                else
                    GUI.contentColor = EditorGUIUtility.isProSkin ? new Color(0.90f, 0.92f, 0.95f) : new Color(0.16f, 0.17f, 0.19f);

                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(7, 7, 0, 0)
                };
                GUI.Label(rect, content, labelStyle);
                GUI.contentColor = old;
            }

            return enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private bool InteractiveToggleChip(GUIContent content, bool value, Color tint, float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 20f, GUILayout.Width(width), GUILayout.Height(20f));
            Event current = Event.current;
            bool hover = rect.Contains(current.mousePosition);
            bool enabled = GUI.enabled;

            Color chipTint = value ? EnsureReadableTint(tint, 4.5f) : UtilityWindowTheme.Neutral;
            Color borderTint = value ? chipTint : Color.Lerp(UtilityWindowTheme.Neutral, tint, 0.28f);
            if (current.type == EventType.Repaint)
            {
                Color border = enabled
                    ? new Color(borderTint.r, borderTint.g, borderTint.b, value ? 0.92f : hover ? 0.86f : 0.72f)
                    : new Color(0.45f, 0.45f, 0.45f, 0.18f);

                using (UtilityWindowTheme.Background(chipTint))
                    UtilityWindowTheme.CountPillStyle.Draw(rect, GUIContent.none, hover, false, value, false);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
                EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);

                Rect checkRect = new Rect(rect.x + 6f, rect.y + 5f, 10f, 10f);
                Color checkBorder = value ? new Color(chipTint.r, chipTint.g, chipTint.b, 0.95f) : new Color(border.r, border.g, border.b, Mathf.Max(border.a, 0.88f));
                Color inactiveCheckFill = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, hover ? 0.18f : 0.14f)
                    : new Color(0f, 0f, 0f, hover ? 0.14f : 0.10f);
                DrawStudioBox(checkRect, value ? new Color(chipTint.r, chipTint.g, chipTint.b, 0.82f) : inactiveCheckFill, checkBorder);
                if (value)
                {
                    Color readable = ReadableOn(chipTint);
                    readable.a = 0.92f;
                    EditorGUI.DrawRect(new Rect(checkRect.x + 3f, checkRect.y + 3f, 4f, 4f), readable);
                }

                Color textColor = enabled
                    ? ReadableOn(chipTint)
                    : (EditorGUIUtility.isProSkin ? new Color(0.54f, 0.54f, 0.56f) : new Color(0.48f, 0.48f, 0.50f));
                Rect labelRect = new Rect(rect.x + 21f, rect.y, rect.width - 25f, rect.height);
                var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 2, 0, 1),
                    normal = { textColor = textColor },
                    hover = { textColor = textColor },
                    active = { textColor = textColor },
                    focused = { textColor = textColor },
                    onNormal = { textColor = textColor },
                    onHover = { textColor = textColor },
                    onActive = { textColor = textColor },
                    onFocused = { textColor = textColor }
                };
                GUI.Label(labelRect, content, labelStyle);
            }

            if (enabled)
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
                {
                    current.Use();
                    return !value;
                }
            }

            return value;
        }

        private void DrawStudioBox(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }

        private void DrawStudioHelpCard(string title, string message, Color tint)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(tint, 0.075f, 0.035f, 5, 2)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                    EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle);
                EditorGUILayout.LabelField(message, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawColourChip(Color color, float width = 38f, float height = 22f)
        {
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            Color preview = ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, color);
            EditorGUI.DrawRect(rect, preview);
            if (Event.current.type == EventType.Repaint)
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.45f : 0.24f));
        }

        private int CountLockedSwatches()
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return 0;

            int count = 0;
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                if (_activePalette.swatches[i] != null && _activePalette.swatches[i].locked)
                    count++;
            }
            return count;
        }
    }
#endif
}
