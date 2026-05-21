using PungentFunk.Utilities.Colour;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow
    {
        private void DrawInspectorColumn(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                DrawInspectorHeader(Mathf.Max(320f, width - 20f));
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll, false, true, GUILayout.ExpandHeight(true));
                DrawInspectorBody(Mathf.Max(260f, width - 28f));
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawInspectorPopup()
        {
            Rect rect = new Rect(Mathf.Max(12f, position.width - _inspectorWidth - 12f), 82f, Mathf.Min(_inspectorWidth, position.width - 24f), Mathf.Max(260f, position.height - 96f));
            DrawPopupChrome(rect);
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            DrawInspectorHeader(Mathf.Max(300f, rect.width - 20f));
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll, false, true, GUILayout.ExpandHeight(true));
            DrawInspectorBody(Mathf.Max(260f, rect.width - 28f));
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawInspectorHeader(float width)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(24f)))
            {
                TextureInspectorTab next = DrawInspectorSelector(_activeInspector, width - 30f);
                if (next != _activeInspector)
                {
                    _activeInspector = next;
                    ApplyInspectorPhaseRouting(next);
                    _inspectorScroll = Vector2.zero;
                    SavePrefs();
                    SaveSession();
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("x", "Close inspector. Escape also closes popups."), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                {
                    _inspectorOpen = false;
                    SavePrefs();
                    SaveSession();
                }
            }
        }

        private void ApplyInspectorPhaseRouting(TextureInspectorTab tab)
        {
            switch (tab)
            {
                case TextureInspectorTab.Explore:
                    _activeDesignerPhase = TextureDesignerPhase.Explore;
                    ApplyExploreStrategyToLegacyWorkflow();
                    break;
                case TextureInspectorTab.Compose:
                    _activeDesignerPhase = TextureDesignerPhase.Compose;
                    _activeWorkflow = TextureDesignWorkflow.ManualCompose;
                    _composeMode = TextureComposeMode.Unguided;
                    break;
                case TextureInspectorTab.Refine:
                    _activeDesignerPhase = TextureDesignerPhase.Refine;
                    break;
                case TextureInspectorTab.Export:
                    _activeDesignerPhase = TextureDesignerPhase.Export;
                    break;
                case TextureInspectorTab.History:
                    _activeDesignerPhase = TextureDesignerPhase.History;
                    break;
            }
        }

        private TextureInspectorTab DrawInspectorSelector(TextureInspectorTab active, float availableWidth)
        {
            TextureInspectorTab selected = active;
            float buttonWidth = Mathf.Floor(Mathf.Max(66f, (availableWidth - 8f) / 5f));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawInspectorTabButton(TextureInspectorTab.Explore, "Explore", "Generate and score texture ideas.", active, buttonWidth))
                    selected = TextureInspectorTab.Explore;
                if (DrawInspectorTabButton(TextureInspectorTab.Compose, "Compose", "Generate, lock, and combine texture variants.", active, buttonWidth))
                    selected = TextureInspectorTab.Compose;
                if (DrawInspectorTabButton(TextureInspectorTab.Refine, "Refine", "Adjust levels, tone, colour mapping, and tile preview.", active, buttonWidth))
                    selected = TextureInspectorTab.Refine;
                if (DrawInspectorTabButton(TextureInspectorTab.Export, "Export", "Export PNG maps and importer settings.", active, buttonWidth))
                    selected = TextureInspectorTab.Export;
                if (DrawInspectorTabButton(TextureInspectorTab.History, "History", "Restore previous base recipes and combinations.", active, buttonWidth))
                    selected = TextureInspectorTab.History;
            }
            return selected;
        }

        private bool DrawInspectorTabButton(TextureInspectorTab tab, string label, string tooltip, TextureInspectorTab active, float width)
        {
            bool isActive = tab == active;
            GUIContent content = new GUIContent(label, tooltip);
            if (!isActive)
                return GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(width));

            Rect rect = GUILayoutUtility.GetRect(width, 20f, EditorStyles.toolbarButton, GUILayout.Width(width), GUILayout.Height(20f));
            if (Event.current.type == EventType.Repaint)
            {
                Color tint = TabTint(tab);
                DrawStudioBox(rect, tint, PanelBorder(tint, 0.86f));
                GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    normal = { textColor = Color.white }
                };
                GUI.Label(rect, content, labelStyle);
            }
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void DrawInspectorBody(float width)
        {
            switch (_activeInspector)
            {
                case TextureInspectorTab.Explore:
                    DrawExploreInspector(width);
                    break;
                case TextureInspectorTab.Refine:
                    DrawRefineInspector(width);
                    break;
                case TextureInspectorTab.Export:
                    DrawExportInspector(width);
                    break;
                case TextureInspectorTab.History:
                    DrawHistoryInspector(width);
                    break;
                default:
                    DrawComposeInspector(width);
                    break;
            }
        }

        private void DrawExploreInspector(float width)
        {
            using (BeginInspectorSection("Explore", ComposeTint(), _exploreStrategy.ToString(), TextureButtonTone.Secondary))
            {
                DrawInlineStatus("Explore generates texture ideas. Presets and constraints are strategies, not separate authoring phases.", UtilityWindowTheme.Neutral);
                EditorGUI.BeginChangeCheck();
                _exploreStrategy = (TextureExploreStrategy)EditorGUILayout.EnumPopup(new GUIContent("Strategy", "Random finds broad ideas. Guided uses sources. Constrained scores against targets."), _exploreStrategy);
                _combination.candidateCount = EditorGUILayout.IntSlider("Textures", _combination.candidateCount, 1, 16);
                DrawInverseGenerationSliders();
                DrawTileabilityGenerationControls();
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyExploreStrategyToLegacyWorkflow();
                    RequestSessionSave();
                    Repaint();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton(_generationRunning ? "Stop" : "Generate", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(28f)))
                    {
                        if (_generationRunning)
                            CancelGenerationJobs(true);
                        else
                            GenerateCandidates();
                    }
                    if (StudioButton("Presets", UtilityWindowTheme.Purple, TextureButtonTone.Ghost, GUILayout.Height(28f)))
                    {
                        _exploreStrategy = TextureExploreStrategy.Presets;
                        _activeWorkflow = TextureDesignWorkflow.PresetBrowser;
                    }
                }
            }
        }

        private void DrawRefineInspector(float width)
        {
            if (_selectedCandidates.Count > 1 && IsValidActiveCandidate())
                SelectSingleVariantForRefine(_activeCandidateIndex, true);
            if (_selectedCandidates.Count == 0 && IsValidActiveCandidate())
                SelectSingleVariantForRefine(_activeCandidateIndex, false);

            using (BeginInspectorSection("Output", RefineTint(), $"{_combination.width}x{_combination.height}", TextureButtonTone.Secondary))
            {
                if (IsValidActiveCandidate())
                    DrawInlineStatus($"Refining {_candidates[_activeCandidateIndex].label}. Only one texture can be refined at a time.", UtilityWindowTheme.Neutral);
                EditorGUI.BeginChangeCheck();
                _combination.width = EditorGUILayout.IntField("Width", _combination.width);
                _combination.height = EditorGUILayout.IntField("Height", _combination.height);
                _combination.candidateCount = EditorGUILayout.IntSlider("Textures", _combination.candidateCount, 1, 16);
                _combination.mipChain = EditorGUILayout.Toggle("Mip Chain", _combination.mipChain);
                _combination.linear = EditorGUILayout.Toggle("Linear", _combination.linear);
                _tileabilityMode = (TextureTileabilityMode)EditorGUILayout.EnumPopup(new GUIContent("Tileable", "Preview repeats the texture. Generate Seamless affects regeneration."), _tileabilityMode);
                if (EditorGUI.EndChangeCheck())
                {
                    SyncTileabilitySettings();
                    RegeneratePreviewFromInspector("Updated output.");
                }
            }

            using (BeginInspectorSection("Tone", RefineTint(), _previewDirty ? "dirty" : "synced", TextureButtonTone.Secondary))
            {
                EditorGUI.BeginChangeCheck();
                DrawRange("Levels", ref _combination.inputMin, ref _combination.inputMax, 0f, 1f);
                _combination.gamma = EditorGUILayout.Slider("Gamma", _combination.gamma, 0.1f, 4f);
                _combination.contrast = EditorGUILayout.Slider("Contrast", _combination.contrast, 0f, 2f);
                _combination.brightness = EditorGUILayout.Slider("Brightness", _combination.brightness, -1f, 1f);
                _combination.autoBalance = EditorGUILayout.Toggle("Auto Balance", _combination.autoBalance);
                using (new EditorGUI.DisabledScope(!_combination.autoBalance))
                    _combination.autoBalanceStrength = EditorGUILayout.Slider("Balance Strength", _combination.autoBalanceStrength, 0f, 1f);
                _combination.threshold = EditorGUILayout.Slider("Threshold", _combination.threshold, 0f, 1f);
                _combination.invert = EditorGUILayout.Toggle("Invert", _combination.invert);
                _combination.blur = EditorGUILayout.Slider("Blur", _combination.blur, 0f, 1f);
                _combination.sharpen = EditorGUILayout.Slider("Sharpen", _combination.sharpen, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                    RegeneratePreviewFromInspector("Updated refinement.");
            }

            using (BeginInspectorSection("Colour", UtilityWindowTheme.Purple, _combination.paletteMode.ToString(), TextureButtonTone.Secondary))
            {
                EditorGUI.BeginChangeCheck();
                _combination.colorMode = (ProceduralTextureColorMode)EditorGUILayout.EnumPopup("Mode", _combination.colorMode);
                _combination.paletteMode = (ProceduralTexturePaletteMode)EditorGUILayout.EnumPopup("Palette", _combination.paletteMode);
                _palette = (PungentColourPaletteSO)EditorGUILayout.ObjectField("Palette Asset", _palette, typeof(PungentColourPaletteSO), false);
                if (_combination.colorMode == ProceduralTextureColorMode.ForegroundBackground)
                {
                    _combination.backgroundColor = EditorGUILayout.ColorField("Background", _combination.backgroundColor);
                    _combination.foregroundColor = EditorGUILayout.ColorField("Foreground", _combination.foregroundColor);
                }
                else if (_combination.colorMode == ProceduralTextureColorMode.Gradient)
                {
                    _combination.gradient = EditorGUILayout.GradientField("Gradient", _combination.gradient);
                }
                _combination.outputOpacity = EditorGUILayout.Slider("Opacity", _combination.outputOpacity, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                    RegeneratePreviewFromInspector("Updated colour mapping.");

                if (_combination.paletteMode != ProceduralTexturePaletteMode.None && _palette == null)
                    DrawInlineStatus("Palette mapping is waiting for a PungentColourPaletteSO.", UtilityWindowTheme.Amber);
            }
        }

        private void DrawExportInspector(float width)
        {
            using (BeginInspectorSection("PNG Maps", ExportTint(), $"{SelectedExportTargets().Count} selected", TextureButtonTone.Secondary))
            {
                _lastSaveFolder = EditorGUILayout.TextField("Folder", _lastSaveFolder);
                DrawMapToggle(ref _export.diffuse, "Diffuse / Albedo");
                DrawMapToggle(ref _export.height, "Height");
                DrawMapToggle(ref _export.normal, "Normal");
                DrawMapToggle(ref _export.roughness, "Roughness");
                DrawMapToggle(ref _export.smoothness, "Smoothness");
                DrawMapToggle(ref _export.metallic, "Metallic");
                DrawMapToggle(ref _export.alpha, "Alpha Mask");
                _export.normalStrength = EditorGUILayout.Slider("Normal Strength", _export.normalStrength, 0.1f, 8f);
            }

            using (BeginInspectorSection("Transparency", ExportTint(), _export.transparentPng ? "active" : "off", TextureButtonTone.Secondary))
            {
                _export.transparentPng = EditorGUILayout.Toggle("Transparent PNG", _export.transparentPng);
                using (new EditorGUI.DisabledScope(!_export.transparentPng))
                    DrawRange("Luminance Range", ref _export.alphaMin, ref _export.alphaMax, 0f, 1f);
            }

            using (new EditorGUI.DisabledScope(SelectedExportTargets().Count == 0 && _previewValues == null))
            {
                if (StudioButton("Export Selected Maps", ExportTint(), TextureButtonTone.Primary, GUILayout.Height(30f)))
                    ExportMaps();
            }

            if (SelectedExportTargets().Count == 0 && _previewValues == null)
                DrawInlineStatus("Generate or select textures before exporting.", UtilityWindowTheme.Amber);
        }

        private void DrawHistoryInspector(float width)
        {
            DrawTextureLibraryInspector();

            using (BeginInspectorSection("History", HistoryTint(), _historyTab == TextureHistoryTab.Bases ? "Guides" : _historyTab.ToString(), TextureButtonTone.Secondary))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(_historyTab == TextureHistoryTab.Combinations, "Combinations", EditorStyles.miniButtonLeft))
                        _historyTab = TextureHistoryTab.Combinations;
                    if (GUILayout.Toggle(_historyTab == TextureHistoryTab.Bases, "Guides", EditorStyles.miniButtonRight))
                        _historyTab = TextureHistoryTab.Bases;
                }
            }

            System.Collections.Generic.List<TextureHistorySnapshot> source = _historyTab == TextureHistoryTab.Bases ? _baseHistory : _combinationHistory;
            if (source.Count == 0)
            {
                DrawInlineStatus("Snapshots appear after generating, restoring, or editing texture variants.", UtilityWindowTheme.Neutral);
                return;
            }

            _historyScroll = EditorGUILayout.BeginScrollView(_historyScroll, GUILayout.MaxHeight(620f));
            for (int i = 0; i < source.Count; i++)
                DrawHistoryCard(source, i);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHistoryCard(System.Collections.Generic.List<TextureHistorySnapshot> source, int index)
        {
            TextureHistorySnapshot snapshot = source[index];
            using (BeginInspectorSection(snapshot.label, HistoryTint(), ShortTime(snapshot.createdUtc), TextureButtonTone.Ghost))
            {
                if (snapshot.preview != null)
                {
                    Rect rect = GUILayoutUtility.GetRect(80f, 96f, GUILayout.ExpandWidth(true));
                    DrawCheckerBackground(rect);
                    GUI.DrawTexture(FitRect(rect, snapshot.preview.width, snapshot.preview.height), snapshot.preview, ScaleMode.ScaleToFit, true);
                    DrawPreviewBorder(rect);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Restore", EditorStyles.miniButton))
                        RestoreSnapshot(snapshot);
                    GUILayout.Label($"{snapshot.influences?.Length ?? 0} guide(s)", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private static void DrawMapToggle(ref bool value, string label)
        {
            value = EditorGUILayout.ToggleLeft(label, value);
        }

        private void RegeneratePreviewFromInspector(string status)
        {
            _combination.Clamp();
            if (IsValidActiveCandidate())
            {
                ProceduralTextureCandidate active = _candidates[_activeCandidateIndex];
                active.settings = _combination.Clone();
                RegenerateVariant(_activeCandidateIndex, status);
            }
            else
            {
                GeneratePreview(true);
                _lastStatus = status;
                SaveSession();
            }
        }
    }
#endif
}
