using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow
    {
        private EditorGUILayout.VerticalScope BeginInspectorSection(string title, Color tint, string status = null, TextureButtonTone tone = TextureButtonTone.Secondary, params GUILayoutOption[] options)
        {
            float top = tone == TextureButtonTone.Ghost ? 4f : 6f;
            GUIStyle sectionStyle = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(2, 2, Mathf.RoundToInt(top), tone == TextureButtonTone.Ghost ? 5 : 7),
                margin = new RectOffset(0, 0, tone == TextureButtonTone.Ghost ? 3 : 6, tone == TextureButtonTone.Ghost ? 5 : 8)
            };

            var scope = new EditorGUILayout.VerticalScope(sectionStyle, options);
            DrawSectionTitle(title, tint, status);
            return scope;
        }

        private void DrawSectionTitle(string title, Color tint, string status = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, UtilityWindowTheme.SectionHeaderStyle);
                GUILayout.FlexibleSpace();
                if (!string.IsNullOrWhiteSpace(status))
                    UtilityWindowTheme.CountPill(status, tint);
            }
            DrawStudioDivider(tint, 0.36f);
        }

        private void DrawStudioDivider(Color tint, float alpha = 0.36f)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(tint.r, tint.g, tint.b, alpha));
        }

        private bool StudioButton(string label, Color tint, TextureButtonTone tone = TextureButtonTone.Secondary, params GUILayoutOption[] options)
        {
            return StudioButton(new GUIContent(label), tint, tone, options);
        }

        private bool StudioButton(GUIContent content, Color tint, TextureButtonTone tone = TextureButtonTone.Secondary, params GUILayoutOption[] options)
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

        private bool StudioButton(Rect rect, GUIContent content, Color tint, TextureButtonTone tone)
        {
            Event current = Event.current;
            bool hover = rect.Contains(current.mousePosition);
            bool enabled = GUI.enabled;

            if (current.type == EventType.Repaint)
            {
                float fillAlpha = tone == TextureButtonTone.Primary ? 0.86f : tone == TextureButtonTone.Ghost ? 0.08f : 0.32f;
                float borderAlpha = tone == TextureButtonTone.Primary ? 0.92f : 0.50f;
                Color fill = enabled
                    ? new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(fillAlpha + (hover ? 0.10f : 0f)))
                    : new Color(0.35f, 0.35f, 0.35f, 0.18f);
                Color border = enabled
                    ? new Color(tint.r, tint.g, tint.b, borderAlpha)
                    : new Color(0.45f, 0.45f, 0.45f, 0.22f);
                DrawStudioBox(rect, fill, border);

                Color old = GUI.contentColor;
                if (!enabled)
                    GUI.contentColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.64f) : new Color(0.45f, 0.45f, 0.47f);
                else if (tone == TextureButtonTone.Primary || tone == TextureButtonTone.Danger)
                    GUI.contentColor = UtilityWindowTheme.GetReadableTextColor(tint);
                else
                    GUI.contentColor = EditorGUIUtility.isProSkin ? new Color(0.90f, 0.92f, 0.95f) : new Color(0.16f, 0.17f, 0.19f);

                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(6, 6, 0, 1)
                };
                GUI.Label(rect, content, labelStyle);
                GUI.contentColor = old;
            }

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static void DrawStudioBox(Rect rect, Color fill, Color border)
        {
            EditorGUI.DrawRect(rect, fill);
            if (border.a <= 0f)
                return;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }

        private static Color PanelFill(Color tint, float alpha)
        {
            Color baseColor = EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.11f, 1f) : new Color(0.80f, 0.81f, 0.83f, 1f);
            Color fill = Color.Lerp(baseColor, tint, 0.18f);
            fill.a = alpha;
            return fill;
        }

        private static Color PanelBorder(Color tint, float alpha)
        {
            return new Color(tint.r, tint.g, tint.b, alpha);
        }

        private void DrawPopupChrome(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Color shadow = new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.26f : 0.14f);
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 0.98f)
                : new Color(0.82f, 0.82f, 0.84f, 0.98f);
            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), shadow);
            EditorGUI.DrawRect(rect, fill);
        }

        private void DrawViewPopup()
        {
            if (!_showViewPopup)
                return;

            Rect rect = new Rect(Mathf.Max(12f, position.width - 304f), 82f, 292f, 192f);
            DrawPopupChrome(rect);
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
                {
                    GUILayout.Label("View", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                        _showViewPopup = false;
                }

                EditorGUI.BeginChangeCheck();
                _checkerboard = EditorGUILayout.Toggle("Checkerboard", _checkerboard);
                _tileabilityMode = (TextureTileabilityMode)EditorGUILayout.EnumPopup(new GUIContent("Tileable", "Off draws normally. Preview Only tiles the preview. Generate Seamless changes generation."), _tileabilityMode);
                _seamOverlayMode = (TextureSeamOverlayMode)EditorGUILayout.EnumPopup(new GUIContent("Seam Overlay", "Show seam center lines or cached heatmap overlays."), _seamOverlayMode);
                _candidateDensity = EditorGUILayout.IntSlider("Texture Density", _candidateDensity, 0, 2);
                if (EditorGUI.EndChangeCheck())
                {
                    SyncTileabilitySettings();
                    SaveSession();
                }

                DrawInlineStatus(_tileabilityMode == TextureTileabilityMode.GenerateSeamless
                    ? "Generate Seamless changes procedural generation and seam repair."
                    : "Preview Only repeats the active result without changing generation.", UtilityWindowTheme.Neutral);
            }
            GUILayout.EndArea();
        }

        private void DrawMetricsPopup()
        {
            if (!_showMetricsPopup)
                return;

            Rect rect = new Rect(Mathf.Max(12f, position.width - 356f), 112f, 344f, 300f);
            DrawPopupChrome(rect);
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
                {
                    GUILayout.Label("Output Metrics", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Close", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                        _showMetricsPopup = false;
                }

                DrawInlineStatus(_previewDirty
                    ? "Metrics reflect the last generated preview. Refresh to update them."
                    : "Metrics are cached from the current preview.", _previewDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral);
                DrawManualMetric("Coverage", _manualOutputCoverage01, UtilityWindowTheme.Green);
                DrawManualMetric("Contrast", _manualOutputContrast01, UtilityWindowTheme.Blue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Tone Range", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(86f));
                    GUILayout.Label($"{_manualOutputToneMin01:0.00} - {_manualOutputToneMax01:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                DrawManualMetric("Seam", _manualOutputSeamScore01, UtilityWindowTheme.Cyan);
                DrawInlineStatus("Seam heatmap overlays are available from the View popup when a cached heatmap exists.", UtilityWindowTheme.Neutral);
            }
            GUILayout.EndArea();
        }

        private void DrawInfluenceLane(string label, float value, Color tint, string status)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(108f));
                Rect track = GUILayoutUtility.GetRect(40f, 8f, GUILayout.ExpandWidth(true), GUILayout.Height(8f));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(track, EditorGUIUtility.isProSkin ? new Color(0.09f, 0.09f, 0.10f) : new Color(0.72f, 0.72f, 0.74f));
                    Color fill = tint;
                    fill.a = 0.86f;
                    EditorGUI.DrawRect(new Rect(track.x, track.y, Mathf.Max(2f, track.width * Mathf.Clamp01(value)), track.height), fill);
                }
                EditorGUILayout.LabelField(status, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(74f));
            }
        }

        private void DrawInlineStatus(string message, Color tint)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill("note", tint, 44f);
                EditorGUILayout.LabelField(message, UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private static void DrawCenteredMiniLabel(Rect rect, string label)
        {
            GUIStyle style = new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(rect, label, style);
        }

        private static void DrawRange(string label, ref float min, ref float max, float floor, float ceiling)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(102f));
                min = EditorGUILayout.FloatField(min, GUILayout.Width(44f));
                Rect slider = GUILayoutUtility.GetRect(60f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                EditorGUI.MinMaxSlider(slider, ref min, ref max, floor, ceiling);
                max = EditorGUILayout.FloatField(max, GUILayout.Width(44f));
            }
            min = Mathf.Clamp(min, floor, ceiling);
            max = Mathf.Clamp(max, floor, ceiling);
            if (max < min)
                max = min;
        }

        private static bool IsPointInAnyRect(Vector2 point, Rect[] rects)
        {
            if (rects == null)
                return false;

            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].width > 0f && rects[i].height > 0f && rects[i].Contains(point))
                    return true;
            }

            return false;
        }

        private static Rect FitRect(Rect container, float sourceWidth, float sourceHeight)
        {
            float sourceAspect = sourceWidth / Mathf.Max(1f, sourceHeight);
            float containerAspect = container.width / Mathf.Max(1f, container.height);
            if (containerAspect > sourceAspect)
            {
                float width = container.height * sourceAspect;
                return new Rect(container.center.x - width * 0.5f, container.y, width, container.height);
            }

            float height = container.width / sourceAspect;
            return new Rect(container.x, container.center.y - height * 0.5f, container.width, height);
        }

        private static void DrawTiledTexture(Rect rect, Texture2D texture, int tiles)
        {
            if (texture == null)
                return;

            float tileWidth = rect.width / tiles;
            float tileHeight = rect.height / tiles;
            for (int y = 0; y < tiles; y++)
            {
                for (int x = 0; x < tiles; x++)
                    GUI.DrawTexture(new Rect(rect.x + x * tileWidth, rect.y + y * tileHeight, tileWidth, tileHeight), texture, ScaleMode.ScaleAndCrop, true);
            }
        }

        private static void DrawSeamGuide(Rect rect)
        {
            Color line = new Color(UtilityWindowTheme.Amber.r, UtilityWindowTheme.Amber.g, UtilityWindowTheme.Amber.b, 0.42f);
            EditorGUI.DrawRect(new Rect(rect.center.x - 0.5f, rect.y, 1f, rect.height), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.center.y - 0.5f, rect.width, 1f), line);
        }

        private void DrawSeamOverlay(Rect rect, Texture2D heatmap)
        {
            if (_seamOverlayMode == TextureSeamOverlayMode.Off)
                return;

            if (_seamOverlayMode == TextureSeamOverlayMode.Heatmap && heatmap != null)
            {
                GUI.DrawTexture(rect, heatmap, ScaleMode.ScaleToFit, true);
                return;
            }

            DrawSeamGuide(rect);
        }

        private static void DrawCheckerBackground(Rect rect)
        {
            Color a = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.78f, 0.78f, 0.78f);
            Color b = EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.88f, 0.88f, 0.88f);
            const float size = 16f;
            int rows = Mathf.CeilToInt(rect.height / size);
            int cols = Mathf.CeilToInt(rect.width / size);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    Rect tile = new Rect(rect.x + x * size, rect.y + y * size, size, size);
                    EditorGUI.DrawRect(tile, ((x + y) & 1) == 0 ? a : b);
                }
            }
        }

        private static void DrawPreviewBorder(Rect rect)
        {
            Color line = UtilityWindowTheme.ResizeHandleTint;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), line);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);
        }

        private Color ComposeTint()
        {
            return UtilityWindowTheme.Cyan;
        }

        private Color RefineTint()
        {
            return UtilityWindowTheme.Amber;
        }

        private Color ExportTint()
        {
            return UtilityWindowTheme.Green;
        }

        private Color HistoryTint()
        {
            return UtilityWindowTheme.Blue;
        }

        private Color BaseCountTint()
        {
            if (_bases.Count > ProceduralTextureCombinationUtility.SoftBaseWarningCount)
                return UtilityWindowTheme.Amber;
            return _bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount ? UtilityWindowTheme.Purple : UtilityWindowTheme.Green;
        }

        private Color BaseTint(int index)
        {
            Color[] colors = { UtilityWindowTheme.Cyan, UtilityWindowTheme.Green, UtilityWindowTheme.Amber, UtilityWindowTheme.Purple, UtilityWindowTheme.Blue, UtilityWindowTheme.Teal };
            return colors[Mathf.Abs(index) % colors.Length];
        }

        private Color TabTint(TextureInspectorTab tab)
        {
            switch (tab)
            {
                case TextureInspectorTab.Explore:
                    return UtilityWindowTheme.Cyan;
                case TextureInspectorTab.Refine:
                    return RefineTint();
                case TextureInspectorTab.Export:
                    return ExportTint();
                case TextureInspectorTab.History:
                    return HistoryTint();
                default:
                    return ComposeTint();
            }
        }

        private static string ShortTime(string utc)
        {
            if (string.IsNullOrWhiteSpace(utc))
                return "saved";
            return utc.Length > 16 ? utc.Substring(0, 16) : utc;
        }
    }
#endif
}
