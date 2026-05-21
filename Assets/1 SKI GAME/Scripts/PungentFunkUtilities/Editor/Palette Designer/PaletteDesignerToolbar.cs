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
        private void DrawToolbar()
        {
            ToolbarActionLayout layout = BuildToolbarActionLayout(position.width);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawToolbarPaletteSelector(layout.paletteWidth);

                if (layout.sourceVisible)
                    DrawToolbarOverlayButton(PaletteOverlayKind.Source, layout.tiny ? "Src" : "Source", "Palette asset, metadata, storage, and export actions.", layout.sourceWidth);

                using (new EditorGUI.DisabledScope(_activePalette == null))
                {
                    if (DrawToolbarGenerateButton(layout.iterateWidth))
                        IteratePalette();
                }

                GUILayout.FlexibleSpace();

                if (layout.saveVisible)
                {
                    using (new EditorGUI.DisabledScope(_activePalette == null))
                    {
                        if (GUILayout.Button(new GUIContent("Save", "Save the active palette asset."), EditorStyles.toolbarButton, GUILayout.Width(layout.saveWidth)))
                            SaveActivePalette();
                    }
                }

                if (layout.viewVisible)
                    DrawToolbarOverlayButton(PaletteOverlayKind.Settings, "View", "Preview and palette canvas preferences.", layout.viewWidth);

                if (layout.simulationVisible)
                    DrawToolbarSimulationChip(layout.simulationWidth);

                if (layout.inspectorVisible)
                    DrawToolbarInspectorButton(layout.inspectorWidth);

                if (GUILayout.Button(new GUIContent("More", "Hidden toolbar actions and secondary palette actions."), EditorStyles.toolbarDropDown, GUILayout.Width(layout.moreWidth)))
                    ShowToolbarOverflowMenu(layout.sourceVisible, layout.inspectorVisible, layout.saveVisible, layout.viewVisible, layout.simulationVisible);
            }
        }

        private struct ToolbarActionLayout
        {
            public bool tiny;
            public bool compact;
            public float paletteWidth;
            public float iterateWidth;
            public float moreWidth;
            public bool sourceVisible;
            public bool saveVisible;
            public bool inspectorVisible;
            public bool viewVisible;
            public bool simulationVisible;
            public float sourceWidth;
            public float saveWidth;
            public float inspectorWidth;
            public float viewWidth;
            public float simulationWidth;
        }

        private ToolbarActionLayout BuildToolbarActionLayout(float toolbarWidth)
        {
            var layout = new ToolbarActionLayout
            {
                tiny = toolbarWidth < 700f,
                compact = toolbarWidth < 940f,
                sourceWidth = MeasureToolbarButton(toolbarWidth < 700f ? "Src" : "Source", toolbarWidth < 700f ? 42f : 62f, 76f),
                saveWidth = MeasureToolbarButton("Save", 52f, 64f),
                inspectorWidth = MeasureToolbarButton("Inspector", 76f, 96f),
                viewWidth = MeasureToolbarButton("View", 52f, 64f),
                simulationWidth = MeasureToolbarButton(SimulationPreviewShortLabel(), toolbarWidth < 940f ? 84f : 116f, 150f),
                moreWidth = MeasureToolbarButton("More", 58f, 68f)
            };

            layout.iterateWidth = MeasureToolbarButton("Generate", layout.tiny ? 98f : 118f, 148f);
            layout.paletteWidth = Mathf.Clamp(toolbarWidth * (layout.tiny ? 0.34f : 0.30f), layout.tiny ? 118f : 160f, layout.compact ? 280f : 340f);

            float requiredWidth = layout.paletteWidth + layout.iterateWidth + layout.moreWidth + 18f;
            if (requiredWidth > toolbarWidth)
                layout.paletteWidth = Mathf.Max(104f, toolbarWidth - layout.iterateWidth - layout.moreWidth - 18f);

            float optionalWidth = Mathf.Max(0f, toolbarWidth - layout.paletteWidth - layout.iterateWidth - layout.moreWidth - 18f);

            layout.sourceVisible = ReserveToolbarWidth(ref optionalWidth, layout.sourceWidth);
            layout.saveVisible = ReserveToolbarWidth(ref optionalWidth, layout.saveWidth);
            layout.inspectorVisible = ReserveToolbarWidth(ref optionalWidth, layout.inspectorWidth);
            layout.viewVisible = ReserveToolbarWidth(ref optionalWidth, layout.viewWidth);
            layout.simulationVisible = SimulationPreviewActive() && ReserveToolbarWidth(ref optionalWidth, layout.simulationWidth);
            return layout;
        }

        private static float MeasureToolbarButton(string label, float minWidth, float maxWidth)
        {
            float measured = EditorStyles.toolbarButton.CalcSize(new GUIContent(label)).x + 18f;
            return Mathf.Ceil(Mathf.Clamp(measured, minWidth, maxWidth));
        }

        private static bool ReserveToolbarWidth(ref float remaining, float width)
        {
            float withGap = width + 4f;
            if (remaining < withGap)
                return false;

            remaining -= withGap;
            return true;
        }

        private bool DrawToolbarGenerateButton(float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, EditorStyles.toolbarButton, GUILayout.Width(width), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            rect.y += 1f;
            rect.height = Mathf.Max(16f, rect.height - 2f);
            return StudioButton(
                rect,
                new GUIContent("Generate", "Generate a starter palette or regenerate unlocked swatches. Space also runs this when no field is focused."),
                ControlsTint(),
                PaletteDesignerButtonTone.Primary);
        }

        private void DrawToolbarPaletteSelector(float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, EditorStyles.toolbarDropDown, GUILayout.Width(width), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            bool hover = rect.Contains(Event.current.mousePosition);
            bool active = _activeOverlay == PaletteOverlayKind.PalettePicker;
            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.toolbarDropDown.Draw(rect, GUIContent.none, hover, active, active, false);
                Rect strip = new Rect(rect.x + 4f, rect.y + 3f, Mathf.Min(46f, rect.width * 0.24f), Mathf.Max(1f, rect.height - 6f));
                DrawPaletteMiniStrip(strip, _activePalette);
                Rect labelRect = new Rect(strip.xMax + 5f, rect.y, Mathf.Max(20f, rect.width - strip.width - 21f), rect.height);
                string label = _activePalette != null ? PaletteDisplayName(_activePalette) : "Create / Select Palette";
                GUIStyle style = new GUIStyle(EditorStyles.toolbarButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                style.normal.background = null;
                style.hover.background = null;
                style.active.background = null;
                GUI.Label(labelRect, label, style);
            }

            string tooltip = _activePalette != null
                ? $"Palette: {PaletteDisplayName(_activePalette)}\n{AssetDatabase.GetAssetPath(_activePalette)}"
                : "Create or select a palette.";
            if (GUI.Button(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
                OpenOverlay(PaletteOverlayKind.PalettePicker, rect);
        }

        private static string PaletteDisplayName(PungentColourPaletteSO palette)
        {
            if (palette == null)
                return "Palette";
            if (!string.IsNullOrWhiteSpace(palette.paletteName))
                return palette.paletteName;
            return !string.IsNullOrWhiteSpace(palette.name) ? palette.name : "Palette";
        }

        private void DrawPaletteMiniStrip(Rect rect, PungentColourPaletteSO palette)
        {
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.19f) : new Color(0.68f, 0.69f, 0.72f));
            if (palette != null && palette.swatches != null && palette.swatches.Count > 0)
                DrawPaletteStripSegments(rect, palette.swatches);

            if (Event.current.type == EventType.Repaint)
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.45f) : new Color(1f, 1f, 1f, 0.48f));
        }

        private void DrawPaletteStripSegments(Rect rect, IList<PaletteSwatch> swatches)
        {
            if (swatches == null || swatches.Count == 0)
                return;

            int count = 0;
            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null)
                    count++;
            }
            if (count == 0)
                return;

            float x = rect.x;
            float segmentWidth = rect.width / count;
            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                Rect segment = new Rect(x, rect.y, Mathf.Ceil(segmentWidth), rect.height);
                EditorGUI.DrawRect(segment, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                x += segmentWidth;
            }
        }

        private bool DrawPaletteChoiceRow(PungentPaletteStorageUtility.PaletteAssetInfo info, bool active)
        {
            const float height = 34f;
            Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            Event current = Event.current;
            bool hover = rect.Contains(current.mousePosition);
            if (current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.17f) : new Color(0.70f, 0.71f, 0.74f));
                if (info.palette != null && info.palette.swatches != null && info.palette.swatches.Count > 0)
                    DrawPaletteStripSegments(rect, info.palette.swatches);

                Color overlay = new Color(0f, 0f, 0f, active ? 0.48f : hover ? 0.42f : 0.34f);
                EditorGUI.DrawRect(rect, overlay);
                Color border = active ? ControlsTint() : EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, hover ? 0.36f : 0.18f) : new Color(0f, 0f, 0f, hover ? 0.30f : 0.14f);
                Handles.DrawSolidRectangleWithOutline(rect, Color.clear, border);
                if (active)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), ControlsTint());

                GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(active ? 12 : 9, 8, 0, 1),
                    normal = { textColor = Color.white },
                    hover = { textColor = Color.white },
                    active = { textColor = Color.white },
                    focused = { textColor = Color.white }
                };
                GUI.Label(rect, string.IsNullOrWhiteSpace(info.displayName) ? "Palette" : info.displayName, labelStyle);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            return GUI.Button(rect, new GUIContent(string.Empty, info.path), GUIStyle.none);
        }

        private PungentColourPaletteSO CreateAndSelectPalette()
        {
            PungentColourPaletteSO palette = PungentPaletteStorageUtility.CreatePaletteAsset();
            if (palette != null)
            {
                MarkPaletteAssetCacheDirty();
                SetActivePalette(palette);
                RefreshPaletteAssetCache();
            }
            return palette;
        }

        private void DrawToolbarSimulationChip(float width)
        {
            if (!SimulationPreviewActive())
                return;

            using (UtilityWindowTheme.Background(UtilityWindowTheme.Amber))
            {
                if (GUILayout.Button(new GUIContent(SimulationPreviewShortLabel(), "Colour-deficiency simulation is active. Click to view original colours."), EditorStyles.toolbarButton, GUILayout.Width(width)))
                    DisableSimulationPreview();
            }
        }

        private void SaveActivePalette()
        {
            if (_activePalette == null)
                return;

            PungentPaletteStorageUtility.SaveExisting(_activePalette);
            _lastStatus = "Saved.";
        }

        private void DrawToolbarOverlayButton(PaletteOverlayKind kind, string label, string tooltip, float width)
        {
            bool active = IsWorkflowInspector(kind) ? _activeWorkflowInspector == kind : _activeOverlay == kind;
            if (GUILayout.Toggle(active, new GUIContent(label, $"{tooltip} {OverlayStatus(kind)}"), EditorStyles.toolbarButton, GUILayout.Width(width)) != active)
                OpenOverlay(kind, GUILayoutUtility.GetLastRect());
        }

        private void DrawToolbarInspectorButton(float width)
        {
            bool active = _activeWorkflowInspector != PaletteOverlayKind.None;
            if (GUILayout.Toggle(active, new GUIContent("Inspector", "Open the workflow inspector. Switch between Controls, Refine, Apply, and History inside the inspector header."), EditorStyles.toolbarButton, GUILayout.Width(width)) != active)
                OpenWorkflowInspector(DefaultWorkflowInspectorKind(), GUILayoutUtility.GetLastRect());
        }

        private void ShowToolbarOverflowMenu(bool sourceVisible, bool inspectorVisible, bool saveVisible, bool viewVisible, bool simulationVisible)
        {
            GenericMenu menu = new GenericMenu();

            if (!sourceVisible)
                menu.AddItem(new GUIContent("Source"), _activeOverlay == PaletteOverlayKind.Source, () => OpenOverlay(PaletteOverlayKind.Source));

            if (!inspectorVisible)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Inspector/Controls"), _activeWorkflowInspector == PaletteOverlayKind.Generate, () => OpenOverlay(PaletteOverlayKind.Generate));
                menu.AddItem(new GUIContent("Inspector/Refine"), _activeWorkflowInspector == PaletteOverlayKind.Guide, () => OpenOverlay(PaletteOverlayKind.Guide));
                menu.AddItem(new GUIContent("Inspector/Apply"), _activeWorkflowInspector == PaletteOverlayKind.Apply, () => OpenOverlay(PaletteOverlayKind.Apply));
                menu.AddItem(new GUIContent("Inspector/History"), _activeWorkflowInspector == PaletteOverlayKind.History, () => OpenOverlay(PaletteOverlayKind.History));
            }

            if (!saveVisible || !viewVisible)
                menu.AddSeparator("");

            if (!saveVisible)
            {
                if (_activePalette == null)
                    menu.AddDisabledItem(new GUIContent("Save"));
                else
                    menu.AddItem(new GUIContent("Save"), false, SaveActivePalette);
            }

            if (!viewVisible)
                menu.AddItem(new GUIContent("View Settings"), _activeOverlay == PaletteOverlayKind.Settings, () => OpenOverlay(PaletteOverlayKind.Settings));

            if (SimulationPreviewActive() && !simulationVisible)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(SimulationPreviewLabel() + "/View Original"), false, DisableSimulationPreview);
            }

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Load Selected"), false, () =>
            {
                if (Selection.activeObject is PungentColourPaletteSO selected)
                    SetActivePalette(selected);
                else
                    _lastStatus = "Select a PungentColourPaletteSO asset first.";
            });

            if (_activePalette == null)
            {
                menu.AddDisabledItem(new GUIContent("Save As"));
                menu.AddDisabledItem(new GUIContent("Duplicate"));
                menu.AddDisabledItem(new GUIContent("Copy Values"));
                menu.AddDisabledItem(new GUIContent("Copy JSON"));
                menu.AddDisabledItem(new GUIContent("Ping Asset"));
            }
            else
            {
                menu.AddItem(new GUIContent("Save As"), false, () =>
                {
                    PungentColourPaletteSO saved = PungentPaletteStorageUtility.SaveAsAsset(_activePalette);
                    if (saved != null)
                    {
                        MarkPaletteAssetCacheDirty();
                        SetActivePalette(saved);
                    }
                });
                menu.AddItem(new GUIContent("Duplicate"), false, () =>
                {
                    PungentColourPaletteSO duplicate = PungentPaletteStorageUtility.DuplicateAsset(_activePalette);
                    if (duplicate != null)
                    {
                        MarkPaletteAssetCacheDirty();
                        SetActivePalette(duplicate);
                    }
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Copy Values"), false, () =>
                {
                    PungentPaletteStorageUtility.CopySwatchValuesToClipboard(_activePalette);
                    _lastStatus = "Copied swatch values.";
                });
                menu.AddItem(new GUIContent("Copy JSON"), false, () =>
                {
                    PungentPaletteStorageUtility.CopyPaletteJsonToClipboard(_activePalette);
                    _lastStatus = "Copied JSON.";
                });
                menu.AddItem(new GUIContent("Ping Asset"), false, () => EditorGUIUtility.PingObject(_activePalette));
            }
            menu.ShowAsContext();
        }

        private void DrawEmptyState()
        {
            EnsurePaletteAssetCache();
            using (BeginStudioCard("No Palette Selected", UtilityWindowTheme.Amber, null, 0.10f, 0.04f))
            {
                EditorGUILayout.LabelField("Create a palette or choose an existing one.", UtilityWindowTheme.MutedMiniLabelStyle);
                if (StudioButton("Create Palette", UtilityWindowTheme.Green, PaletteDesignerButtonTone.Primary, GUILayout.Height(31f)))
                    CreateAndSelectPalette();

                GUILayout.Space(4f);
                if (_paletteAssetCache.Count == 0)
                {
                    EditorGUILayout.LabelField("No saved palettes found yet.", UtilityWindowTheme.MutedMiniLabelStyle);
                    return;
                }

                for (int i = 0; i < _paletteAssetCache.Count; i++)
                {
                    PungentPaletteStorageUtility.PaletteAssetInfo info = _paletteAssetCache[i];
                    if (DrawPaletteChoiceRow(info, info.palette == _activePalette))
                        SetActivePalette(info.palette);
                }
            }
        }

        private void DrawPalettePickerContent()
        {
            if (StudioButton("Create New", UtilityWindowTheme.Green, PaletteDesignerButtonTone.Primary, GUILayout.Height(28f)))
            {
                if (CreateAndSelectPalette() != null)
                    CloseUtilityPopup();
                return;
            }

            EnsurePaletteAssetCache();
            if (_paletteAssetCache.Count == 0)
            {
                DrawInspectorBodyText("No saved palettes found yet.");
                return;
            }

            GUILayout.Space(3f);
            for (int i = 0; i < _paletteAssetCache.Count; i++)
            {
                PungentPaletteStorageUtility.PaletteAssetInfo info = _paletteAssetCache[i];
                if (DrawPaletteChoiceRow(info, info.palette == _activePalette))
                {
                    SetActivePalette(info.palette);
                    CloseUtilityPopup();
                    return;
                }
            }
        }

        private void DrawAnalysisStatusChip()
        {
            int problems = _analysisReport != null && _analysisReport.problems != null ? _analysisReport.problems.Count : 0;
            int pairs = _analysisReport != null && _analysisReport.pairs != null ? _analysisReport.pairs.Count : 0;
            UtilityWindowTheme.CountPill(problems == 0 ? "Refine OK" : $"{problems} issues", problems == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 86f);
            UtilityWindowTheme.CountPill($"{pairs} pairs", UtilityWindowTheme.Cyan, 70f);
        }

        private void OpenApplyEntry()
        {
            FocusWorkflowInspector(PaletteOverlayKind.Apply);
            _showApply = true;

            if (CanApplyCurrentSwatch())
                ApplyCurrentSwatchToTargets();
            else
                _lastStatus = _applyTargets.Count == 0 ? "Scan apply targets before applying." : "Choose a swatch role or selected swatch before applying.";
        }

        private void DrawSourceTrayContent()
        {
            using (BeginStudioCard("Palette Source", UtilityWindowTheme.Blue, _activePalette != null ? "linked" : "none", 0.10f, 0.05f))
            {
                EditorGUI.BeginChangeCheck();
                PungentColourPaletteSO nextPalette = (PungentColourPaletteSO)EditorGUILayout.ObjectField("Palette", _activePalette, typeof(PungentColourPaletteSO), false);
                if (EditorGUI.EndChangeCheck())
                    SetActivePalette(nextPalette);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton("New", UtilityWindowTheme.Green, PaletteDesignerButtonTone.Primary, GUILayout.Height(25f)))
                        CreateAndSelectPalette();
                    if (StudioButton("Load Selected", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Secondary, GUILayout.Height(25f)))
                    {
                        if (Selection.activeObject is PungentColourPaletteSO selected)
                            SetActivePalette(selected);
                        else
                            _lastStatus = "Select a PungentColourPaletteSO asset first.";
                    }
                }

                using (new EditorGUI.DisabledScope(_activePalette == null))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (StudioButton("Save As", UtilityWindowTheme.Blue, PaletteDesignerButtonTone.Secondary, GUILayout.Height(23f)))
                        {
                            PungentColourPaletteSO saved = PungentPaletteStorageUtility.SaveAsAsset(_activePalette);
                            if (saved != null)
                            {
                                MarkPaletteAssetCacheDirty();
                                SetActivePalette(saved);
                            }
                        }
                        if (StudioButton("Duplicate", UtilityWindowTheme.Teal, PaletteDesignerButtonTone.Secondary, GUILayout.Height(23f)))
                        {
                            PungentColourPaletteSO duplicate = PungentPaletteStorageUtility.DuplicateAsset(_activePalette);
                            if (duplicate != null)
                            {
                                MarkPaletteAssetCacheDirty();
                                SetActivePalette(duplicate);
                            }
                        }
                        if (StudioButton("Ping", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Width(58f), GUILayout.Height(23f)))
                            EditorGUIUtility.PingObject(_activePalette);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (StudioButton("Copy Values", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Ghost, GUILayout.Height(23f)))
                        {
                            PungentPaletteStorageUtility.CopySwatchValuesToClipboard(_activePalette);
                            _lastStatus = "Copied swatch values.";
                        }
                        if (StudioButton("Copy JSON", UtilityWindowTheme.Purple, PaletteDesignerButtonTone.Ghost, GUILayout.Height(23f)))
                        {
                            PungentPaletteStorageUtility.CopyPaletteJsonToClipboard(_activePalette);
                            _lastStatus = "Copied JSON.";
                        }
                    }
                }
            }

            if (_activePalette != null)
                DrawPaletteInfoPanel();
            else
                DrawStudioHelpCard("Ready when you are", "Create or select a PungentColourPaletteSO asset to begin.", UtilityWindowTheme.Blue);
        }

        private void DrawHistoryTrayContent()
        {
            using (BeginInspectorSection("Iteration History", HistoryTint(), _generationHistory.Count > 0 ? $"{_generationHistory.Count} snapshot(s)" : "empty", PaletteDesignerSectionTone.Summary))
            {
                DrawInspectorBodyText("Previous palette states are recorded before Generate changes the current palette. Reapplying restores a snapshot with Undo and moves that card to the top.");

                if (_generationHistory.Count == 0)
                {
                    DrawStudioHelpCard("No snapshots yet", "Use Generate from the toolbar. Once an existing palette is refined, the state before each pass appears here for comparison.", HistoryTint());
                    return;
                }

                for (int i = 0; i < _generationHistory.Count; i++)
                    DrawHistorySnapshotCard(i, _generationHistory.Get(i));
            }
        }

        private void DrawHistorySnapshotCard(int index, PaletteGenerationVariant snapshot)
        {
            if (snapshot == null)
                return;

            bool active = index == _generationHistory.activeIndex;
            Color tint = active ? HistoryTint() : UtilityWindowTheme.Neutral;
            using (BeginStudioCard(string.IsNullOrWhiteSpace(snapshot.label) ? $"Iteration {index + 1}" : snapshot.label, tint, active ? "selected" : null, active ? 0.085f : 0.045f, active ? 0.045f : 0.018f))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton(active ? "Inspecting" : "Inspect", HistoryTint(), active ? PaletteDesignerButtonTone.Secondary : PaletteDesignerButtonTone.Ghost, GUILayout.Width(76f), GUILayout.Height(23f)))
                        _generationHistory.SetActive(index);

                    using (new EditorGUI.DisabledScope(snapshot.swatches == null || snapshot.swatches.Count == 0))
                    {
                        if (StudioButton("Apply This Iteration", HistoryTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Width(142f), GUILayout.Height(23f)))
                            ApplyHistorySnapshot(index, snapshot);
                        if (StudioButton("Copy HEX", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Width(82f), GUILayout.Height(23f)))
                            CopySnapshotHexValues(snapshot);
                    }
                }

                DrawHistoryPaletteRibbon(snapshot.swatches, 20f);
                DrawHistorySnapshotStatRow(snapshot);
                DrawHistorySnapshotSettings(snapshot);
            }
        }

        private void DrawHistorySnapshotStatRow(PaletteGenerationVariant snapshot)
        {
            if (snapshot == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (snapshot.diagnostics != null)
                    UtilityWindowTheme.CountPill(snapshot.diagnostics.harmonyMode.ToString(), HistoryTint(), 116f);
                UtilityWindowTheme.CountPill($"{snapshot.Count} swatches", UtilityWindowTheme.Neutral, 90f);
                UtilityWindowTheme.CountPill($"{CountLockedSwatchesIn(snapshot.swatches)} locked", UtilityWindowTheme.Neutral, 76f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawHistoryPaletteRibbon(IList<PaletteSwatch> swatches, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.84f, 0.84f, 0.86f));

            if (swatches == null || swatches.Count == 0)
                return;

            int count = 0;
            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null)
                    count++;
            }
            if (count == 0)
                return;

            float x = rect.x;
            float segmentWidth = rect.width / count;
            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                Rect segment = new Rect(x, rect.y, Mathf.Ceil(segmentWidth), rect.height);
                EditorGUI.DrawRect(segment, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                if (swatch.locked)
                    DrawHistoryLockedSwatchMarker(segment);
                x += segmentWidth;
            }
        }

        private void DrawHistoryLockedSwatchMarker(Rect segment)
        {
            float size = Mathf.Min(11f, Mathf.Max(7f, segment.height - 5f));
            Rect body = new Rect(segment.xMax - size - 4f, segment.y + 4f + size * 0.34f, size, size * 0.58f);
            Rect shackle = new Rect(body.x + size * 0.24f, segment.y + 3f, size * 0.52f, size * 0.62f);
            Color line = EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.62f)
                : new Color(1f, 1f, 1f, 0.74f);
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.82f)
                : new Color(0f, 0f, 0f, 0.58f);

            EditorGUI.DrawRect(body, fill);
            EditorGUI.DrawRect(new Rect(shackle.x, shackle.y + shackle.height * 0.5f, 1f, shackle.height * 0.5f), fill);
            EditorGUI.DrawRect(new Rect(shackle.xMax - 1f, shackle.y + shackle.height * 0.5f, 1f, shackle.height * 0.5f), fill);
            EditorGUI.DrawRect(new Rect(shackle.x, shackle.y, shackle.width, 1f), fill);
            EditorGUI.DrawRect(new Rect(body.x, body.y, body.width, 1f), line);
            EditorGUI.DrawRect(new Rect(body.x, body.yMax - 1f, body.width, 1f), line);
            EditorGUI.DrawRect(new Rect(body.x, body.y, 1f, body.height), line);
            EditorGUI.DrawRect(new Rect(body.xMax - 1f, body.y, 1f, body.height), line);
        }

        private void DrawHistorySnapshotSettings(PaletteGenerationVariant snapshot)
        {
            if (snapshot == null || snapshot.settings == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill($"Var {snapshot.settings.randomVariation:0.00}", UtilityWindowTheme.Neutral, 70f);
                UtilityWindowTheme.CountPill($"Sat {snapshot.settings.saturationRange.x:0.00}-{snapshot.settings.saturationRange.y:0.00}", UtilityWindowTheme.Neutral, 102f);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill($"Val {snapshot.settings.valueRange.x:0.00}-{snapshot.settings.valueRange.y:0.00}", UtilityWindowTheme.Neutral, 102f);
            }
        }

        private int CountLockedSwatchesIn(IList<PaletteSwatch> swatches)
        {
            if (swatches == null)
                return 0;

            int count = 0;
            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null && swatches[i].locked)
                    count++;
            }

            return count;
        }

        private void CopySnapshotHexValues(PaletteGenerationVariant snapshot)
        {
            if (snapshot == null || snapshot.swatches == null || snapshot.swatches.Count == 0)
                return;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < snapshot.swatches.Count; i++)
            {
                PaletteSwatch swatch = snapshot.swatches[i];
                if (swatch == null)
                    continue;

                string name = string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {i + 1}" : swatch.name;
                builder.Append(name);
                builder.Append(": ");
                builder.AppendLine(ColourConversionUtility.ToHexRGB(swatch.color));
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString();
            _lastStatus = "Copied history snapshot HEX values.";
        }

        private void ApplyHistorySnapshot(int index, PaletteGenerationVariant snapshot)
        {
            if (_activePalette == null || snapshot == null || snapshot.swatches == null || snapshot.swatches.Count == 0)
                return;

            List<PaletteSwatch> restored = snapshot.CloneSwatches();
            ChangePalette("Apply Palette History Iteration", () =>
            {
                _activePalette.swatches.Clear();
                _activePalette.swatches.AddRange(restored);
                RecalculatePriorities();
                _selectedSwatches.Clear();
                _selectedSwatch = -1;
                _selectionAnchor = -1;
            });

            _generationHistory.MoveToFront(index);
            _lastStatus = $"Applied {snapshot.label ?? "history iteration"} from history.";
            Repaint();
        }

        private void DrawSettingsOverlayContent()
        {
            using (BeginStudioCard("Palette Canvas", UtilityWindowTheme.Neutral, CurrentSwatchLayoutLabel(), 0.085f, 0.04f))
            {
                EditorGUILayout.LabelField("Swatches now reflow automatically from the available workbench dimensions.", UtilityWindowTheme.MutedMiniLabelStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Auto Layout", UtilityWindowTheme.Cyan, 92f);
                    UtilityWindowTheme.CountPill(CurrentSwatchLayoutLabel(), UtilityWindowTheme.Neutral, 76f);
                    GUILayout.FlexibleSpace();
                }
            }

            using (BeginStudioCard("Preview Mode", UtilityWindowTheme.Neutral, SimulationPreviewActive() ? SimulationPreviewLabel() : "Original", 0.055f, 0.025f))
            {
                DrawInspectorBodyText(SimulationPreviewActive()
                    ? "Preview only. Palette colours, generated values, history, and apply mappings are unchanged."
                    : "Show the palette through a colour-deficiency preview without changing palette data.");
                EditorGUI.BeginChangeCheck();
                _deficiencyPreview = (ColourDeficiencyPreviewMode)EditorGUILayout.EnumPopup("Deficiency Preview", _deficiencyPreview);
                if (EditorGUI.EndChangeCheck())
                {
                    SavePrefs();
                    Repaint();
                }

                if (SimulationPreviewActive() && StudioButton("View Original", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Secondary, GUILayout.Height(23f)))
                    DisableSimulationPreview();
            }
        }

        private void DrawContextualHarmonyCard(PaletteGenerationSuggestion suggestion)
        {
            string status = suggestion.openContext ? "open spectrum" : suggestion.chosenHarmonyMode.ToString();
            using (BeginInspectorSection("Next Generation Ranges", ControlsTint(), status, PaletteDesignerSectionTone.Primary))
            {
                DrawInspectorBodyText("These are the hue and value regions the next Generate pass will draw from. Locked swatches stay fixed; manual overrides guide the contextual ranges.");
                DrawAutoContextSummaryPills(suggestion);
                DrawAutoContextHueBands(suggestion);
                GUILayout.Space(2f);
                DrawAutoContextFactorChips();
                DrawAutoContextRangeLanes(suggestion);

                _showAutoContextDetails = EditorGUILayout.Foldout(_showAutoContextDetails, "Why this pass", true);
                if (_showAutoContextDetails)
                {
                    DrawAutoContextHarmonyScores(suggestion);
                    DrawAutoContextEffectiveSettings(suggestion);
                    DrawAutoContextDiagnosticNotes(suggestion, 3);
                }
                else
                {
                    DrawAutoContextDiagnosticNotes(suggestion, 1);
                }
            }
        }

        private void DrawAutoContextSummaryPills(PaletteGenerationSuggestion suggestion)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(suggestion.openContext ? "Open spectrum" : suggestion.chosenHarmonyMode.ToString(), ControlsTint(), CurrentContentWidth() < 420f ? 112f : 136f);
                UtilityWindowTheme.CountPill($"{suggestion.targetHueBands.Count} band(s)", UtilityWindowTheme.Neutral, 86f);
                if (suggestion.effectiveSettings != null)
                    UtilityWindowTheme.CountPill($"{suggestion.effectiveSettings.targetSwatchCount} slots", UtilityWindowTheme.Neutral, 74f);
                UtilityWindowTheme.CountPill($"{suggestion.colourfulLockedCount} locked", suggestion.colourfulLockedCount > 0 ? ControlsTint() : UtilityWindowTheme.Neutral, 82f);
                if (CurrentContentWidth() >= 420f)
                    UtilityWindowTheme.CountPill($"{suggestion.softCoverageCount} recent", UtilityWindowTheme.Neutral, 82f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawAutoContextEffectiveSettings(PaletteGenerationSuggestion suggestion)
        {
            if (suggestion.effectiveSettings == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill($"Var {suggestion.effectiveSettings.randomVariation:0.00}", ControlsTint(), 72f);
                UtilityWindowTheme.CountPill($"Sat {suggestion.effectiveSettings.saturationRange.x:0.00}-{suggestion.effectiveSettings.saturationRange.y:0.00}", UtilityWindowTheme.Neutral, 104f);
                UtilityWindowTheme.CountPill($"Val {suggestion.effectiveSettings.valueRange.x:0.00}-{suggestion.effectiveSettings.valueRange.y:0.00}", UtilityWindowTheme.Neutral, 104f);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawAutoContextDiagnosticNotes(PaletteGenerationSuggestion suggestion, int max)
        {
            if (suggestion.diagnostics == null || suggestion.diagnostics.notes == null)
                return;

            int count = Mathf.Min(max, suggestion.diagnostics.notes.Count);
            for (int i = 0; i < count; i++)
                DrawInspectorBodyText(suggestion.diagnostics.notes[i]);
        }

        private void DrawAutoContextHarmonyScores(PaletteGenerationSuggestion suggestion)
        {
            if (suggestion.harmonyScores == null || suggestion.harmonyScores.Count == 0)
                return;

            var scores = new List<PaletteHarmonyScore>(suggestion.harmonyScores);
            scores.Sort((a, b) => a.score.CompareTo(b.score));

            EditorGUILayout.LabelField("Harmony Fit", UtilityWindowTheme.SectionHeaderStyle);
            int max = Mathf.Min(3, scores.Count);
            for (int i = 0; i < max; i++)
            {
                PaletteHarmonyScore score = scores[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(score.mode.ToString(), GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(score.score.ToString("0.00"), i == 0 ? ControlsTint() : UtilityWindowTheme.Neutral, 58f);
                }
                if (i < max - 1)
                    DrawStudioDivider(i == 0 ? ControlsTint() : UtilityWindowTheme.Neutral, i == 0 ? 0.22f : 0.12f);
            }
        }

        private void DrawAutoContextHueBands(PaletteGenerationSuggestion suggestion)
        {
            if (suggestion.targetHueBands == null || suggestion.targetHueBands.Count == 0)
            {
                DrawStudioHelpCard("Open spectrum", "No narrow hue targets are needed yet. Generate can explore the full wheel until locked anchors guide the next pass.", UtilityWindowTheme.Neutral);
                return;
            }

            _hoveredAutoContextSwatchIndex = -1;
            _autoContextHoverReadout = null;

            Rect map = GUILayoutUtility.GetRect(10f, 76f, GUILayout.ExpandWidth(true), GUILayout.Height(76f));
            Rect track = new Rect(map.x + 2f, map.y + 17f, Mathf.Max(1f, map.width - 4f), 22f);
            Rect readout = new Rect(map.x + 2f, track.yMax + 18f, Mathf.Max(1f, map.width - 4f), 18f);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(track, EditorGUIUtility.isProSkin ? new Color(0.075f, 0.075f, 0.082f) : new Color(0.80f, 0.80f, 0.82f));
                for (int i = 0; i < 24; i++)
                {
                    float hue = i / 24f;
                    Color tick = Color.HSVToRGB(hue, 0.38f, EditorGUIUtility.isProSkin ? 0.34f : 0.94f);
                    tick.a = 0.24f;
                    Rect segment = new Rect(track.x + track.width * hue, track.y, Mathf.Max(1f, track.width / 24f), track.height);
                    EditorGUI.DrawRect(segment, tick);
                }
            }

            for (int i = 0; i < suggestion.targetHueBands.Count; i++)
            {
                PaletteHueBand band = suggestion.targetHueBands[i];
                DrawAutoContextHueBand(track, band, i + 1);
            }

            if (_showLockedAnchorFactors)
                DrawAutoContextLockedMarkers(map, track);
            if (_showRecentColourFactors)
                DrawAutoContextRecentMarkers(map, track);

            GUIStyle readoutStyle = InspectorBodyStyle();
            readoutStyle.clipping = TextClipping.Clip;
            string message = !string.IsNullOrWhiteSpace(_autoContextHoverReadout)
                ? _autoContextHoverReadout
                : (_showLockedAnchorFactors || _showRecentColourFactors ? "Hover a factor marker to preview its source swatch." : "Factor markers are hidden until enabled below.");
            GUI.Label(readout, message, readoutStyle);
        }

        private void DrawAutoContextHueBand(Rect track, PaletteHueBand band, int labelIndex)
        {
            if (band == null)
                return;

            float width = Mathf.Clamp01(band.width);
            float halfWidth = width * 0.5f;
            float start = band.centerHue - halfWidth;
            float end = band.centerHue + halfWidth;
            Color bandColor = Color.HSVToRGB(Mathf.Repeat(band.centerHue, 1f), 0.72f, 0.92f);
            bandColor.a = 0.92f;

            if (start < 0f)
            {
                DrawAutoContextHueBandSegment(track, start + 1f, 1f, bandColor);
                DrawAutoContextHueBandSegment(track, 0f, end, bandColor);
            }
            else if (end > 1f)
            {
                DrawAutoContextHueBandSegment(track, start, 1f, bandColor);
                DrawAutoContextHueBandSegment(track, 0f, end - 1f, bandColor);
            }
            else
            {
                DrawAutoContextHueBandSegment(track, start, end, bandColor);
            }

            float centerX = track.x + track.width * Mathf.Repeat(band.centerHue, 1f);
            Rect labelRect = new Rect(centerX - 10f, track.y + 3f, 20f, track.height - 6f);
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ReadableOn(bandColor) }
            };
            GUI.Label(labelRect, labelIndex.ToString(), labelStyle);
        }

        private void DrawAutoContextHueBandSegment(Rect track, float start, float end, Color colour)
        {
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            if (end <= start)
                return;

            Rect rect = new Rect(track.x + track.width * start, track.y + 2f, Mathf.Max(4f, track.width * (end - start)), track.height - 4f);
            EditorGUI.DrawRect(rect, colour);
        }

        private void DrawAutoContextLockedMarkers(Rect map, Rect track)
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return;

            int shown = 0;
            for (int i = 0; i < _activePalette.swatches.Count && shown < 8; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch == null || !swatch.locked)
                    continue;

                Color.RGBToHSV(swatch.color, out float hue, out float saturation, out _);
                if (saturation < 0.035f)
                    continue;

                DrawAutoContextHueMarker(
                    map,
                    track,
                    hue,
                    swatch.color,
                    i,
                    $"Locked anchor: {SwatchContextLabel(i, swatch)}",
                    true);
                shown++;
            }
        }

        private void DrawAutoContextRecentMarkers(Rect map, Rect track)
        {
            if (_recentAutoCoverageSwatches == null)
                return;

            int start = Mathf.Max(0, _recentAutoCoverageSwatches.Count - 8);
            for (int i = start; i < _recentAutoCoverageSwatches.Count; i++)
            {
                PaletteSwatch swatch = _recentAutoCoverageSwatches[i];
                if (swatch == null)
                    continue;

                Color.RGBToHSV(swatch.color, out float hue, out float saturation, out _);
                if (saturation < 0.035f)
                    continue;

                int sourceIndex = i < _recentAutoCoverageSwatchIndices.Count ? _recentAutoCoverageSwatchIndices[i] : -1;
                int resolvedIndex = ResolveRecentAutoContextSwatchIndex(sourceIndex, swatch);
                DrawAutoContextHueMarker(
                    map,
                    track,
                    hue,
                    swatch.color,
                    resolvedIndex,
                    $"Recent colour: {SwatchContextLabel(sourceIndex, swatch)}",
                    false);
            }
        }

        private void DrawAutoContextHueMarker(Rect map, Rect track, float hue, Color swatchColor, int swatchIndex, string readout, bool above)
        {
            float x = track.x + track.width * Mathf.Repeat(hue, 1f);
            Rect stem = above
                ? new Rect(x - 0.5f, track.y - 7f, 1f, 7f)
                : new Rect(x - 0.5f, track.yMax, 1f, 7f);
            Rect marker = above
                ? new Rect(x - 4f, track.y - 13f, 8f, 8f)
                : new Rect(x - 4f, track.yMax + 5f, 8f, 8f);

            Color markerColor = swatchColor;
            markerColor.a = 1f;
            Color outline = ReadableOn(markerColor);
            outline.a = 0.78f;
            Color stemColor = markerColor;
            stemColor.a = 0.78f;

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(stem, stemColor);
                EditorGUI.DrawRect(marker, markerColor);
                Handles.DrawSolidRectangleWithOutline(marker, Color.clear, outline);
            }

            EditorGUIUtility.AddCursorRect(marker, MouseCursor.Link);
            if (marker.Contains(Event.current.mousePosition))
            {
                _autoContextHoverReadout = readout;
                _hoveredAutoContextSwatchIndex = IsValidCurrentSwatchIndex(swatchIndex) ? swatchIndex : -1;
                if (Event.current.type == EventType.MouseMove)
                    Repaint();
            }
        }

        private void DrawAutoContextFactorChips()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool narrow = CurrentContentWidth() < 390f;
                bool nextLocked = InteractiveToggleChip(new GUIContent(narrow ? "Locked" : "Locked anchors", "Show locked swatches that are anchoring the next pass."), _showLockedAnchorFactors, ControlsTint(), narrow ? 82f : 126f);
                if (nextLocked != _showLockedAnchorFactors)
                {
                    _showLockedAnchorFactors = nextLocked;
                    if (!_showLockedAnchorFactors)
                        ClearAutoContextHover();
                    GUI.FocusControl(null);
                    Repaint();
                }

                bool nextRecent = InteractiveToggleChip(new GUIContent(narrow ? "Recent" : "Recent colours", "Show recent unlocked colours that contextual generation is avoiding."), _showRecentColourFactors, ControlsTint(), narrow ? 82f : 126f);
                if (nextRecent != _showRecentColourFactors)
                {
                    _showRecentColourFactors = nextRecent;
                    if (!_showRecentColourFactors)
                        ClearAutoContextHover();
                    GUI.FocusControl(null);
                    Repaint();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void ClearAutoContextHover()
        {
            _hoveredAutoContextSwatchIndex = -1;
            _autoContextHoverReadout = null;
        }

        private void DrawAutoContextRangeLanes(PaletteGenerationSuggestion suggestion)
        {
            if (suggestion == null || suggestion.effectiveSettings == null)
                return;

            PaletteGenerationSettings settings = suggestion.effectiveSettings;
            DrawMiniRangeLane("Hue", settings.hueRange, ControlsTint(), "Generated hue availability after contextual guidance and manual overrides.");
            DrawMiniRangeLane("Sat", settings.saturationRange, UtilityWindowTheme.Cyan, "Generated saturation range.");
            DrawMiniRangeLane("Value", settings.valueRange, UtilityWindowTheme.Amber, "Generated light/dark range.");
            DrawMiniValueLane("Contrast", settings.contrastInfluence, UtilityWindowTheme.Green, "Foreground/surface separation bias.");
        }

        private void DrawMiniRangeLane(string label, Vector2 range, Color tint, string tooltip)
        {
            const float laneLabelWidth = 64f;
            const float laneValueWidth = 74f;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(laneLabelWidth));
                Rect track = GUILayoutUtility.GetRect(40f, 8f, GUILayout.ExpandWidth(true), GUILayout.Height(8f));
                Rect valueLabel = GUILayoutUtility.GetRect(laneValueWidth, 14f, GUILayout.Width(laneValueWidth), GUILayout.Height(14f));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(track, EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.11f) : new Color(0.72f, 0.72f, 0.74f));
                    float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
                    float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
                    Color fill = EnsureReadableTint(tint, 3f);
                    fill.a = 0.86f;
                    EditorGUI.DrawRect(new Rect(track.x + track.width * min, track.y, Mathf.Max(2f, track.width * (max - min)), track.height), fill);
                }

                GUI.Label(valueLabel, $"{Mathf.Min(range.x, range.y):0.00}-{Mathf.Max(range.x, range.y):0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void DrawMiniValueLane(string label, float value, Color tint, string tooltip)
        {
            const float laneLabelWidth = 64f;
            const float laneValueWidth = 74f;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(label, tooltip), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(laneLabelWidth));
                Rect track = GUILayoutUtility.GetRect(40f, 8f, GUILayout.ExpandWidth(true), GUILayout.Height(8f));
                Rect valueLabel = GUILayoutUtility.GetRect(laneValueWidth, 14f, GUILayout.Width(laneValueWidth), GUILayout.Height(14f));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(track, EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.11f) : new Color(0.72f, 0.72f, 0.74f));
                    Color fill = EnsureReadableTint(tint, 3f);
                    fill.a = 0.86f;
                    EditorGUI.DrawRect(new Rect(track.x, track.y, Mathf.Max(2f, track.width * Mathf.Clamp01(value)), track.height), fill);
                }

                GUI.Label(valueLabel, value.ToString("0.00"), UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private bool IsValidCurrentSwatchIndex(int index)
        {
            return _activePalette != null &&
                   _activePalette.swatches != null &&
                   index >= 0 &&
                   index < _activePalette.swatches.Count &&
                   _activePalette.swatches[index] != null;
        }

        private int ResolveRecentAutoContextSwatchIndex(int sourceIndex, PaletteSwatch remembered)
        {
            if (remembered == null || _activePalette == null || _activePalette.swatches == null)
                return -1;

            if (IsValidCurrentSwatchIndex(sourceIndex) && SwatchesStillMatch(_activePalette.swatches[sourceIndex], remembered))
                return sourceIndex;

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch current = _activePalette.swatches[i];
                if (current != null && SwatchesStillMatch(current, remembered))
                    return i;
            }

            return -1;
        }

        private bool SwatchesStillMatch(PaletteSwatch current, PaletteSwatch remembered)
        {
            if (current == null || remembered == null)
                return false;

            float colourDelta =
                Mathf.Abs(current.color.r - remembered.color.r) +
                Mathf.Abs(current.color.g - remembered.color.g) +
                Mathf.Abs(current.color.b - remembered.color.b);
            if (colourDelta <= 0.035f)
                return true;

            return current.role == remembered.role &&
                   !string.IsNullOrWhiteSpace(current.name) &&
                   string.Equals(current.name, remembered.name, System.StringComparison.OrdinalIgnoreCase);
        }

        private string SwatchContextLabel(int index, PaletteSwatch swatch)
        {
            if (swatch == null)
                return "Unknown swatch";

            string name = string.IsNullOrWhiteSpace(swatch.name) ? (index >= 0 ? $"Swatch {index + 1}" : "Swatch") : swatch.name;
            return $"{name} - {Nicify(swatch.role.ToString())} - {ColourConversionUtility.ToHexRGB(swatch.color)}";
        }

    }
#endif
}
