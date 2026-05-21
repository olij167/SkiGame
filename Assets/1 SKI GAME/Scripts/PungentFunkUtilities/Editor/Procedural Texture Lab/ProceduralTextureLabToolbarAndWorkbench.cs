using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow
    {
        private struct ToolbarActionLayout
        {
            public bool tiny;
            public bool randomizeVisible;
            public bool lockVisible;
            public bool duplicateVisible;
            public bool viewVisible;
            public bool inspectorVisible;
            public float generateWidth;
            public float randomizeWidth;
            public float lockWidth;
            public float duplicateWidth;
            public float inspectorWidth;
            public float viewWidth;
            public float moreWidth;
        }

        private void DrawToolbar()
        {
            ToolbarActionLayout layout = BuildToolbarActionLayout(position.width);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (StudioButton(
                    GUILayoutUtility.GetRect(layout.generateWidth, EditorGUIUtility.singleLineHeight, EditorStyles.toolbarButton, GUILayout.Width(layout.generateWidth)),
                    new GUIContent(_generationRunning ? "Cancel" : "Generate", _generationRunning ? "Cancel the current generation queue." : "Regenerate the active workflow. Guides constrain random exploration. Space also runs this when no field is focused."),
                    ComposeTint(),
                    TextureButtonTone.Primary))
                {
                    if (_generationRunning)
                        CancelGenerationJobs(true);
                    else
                        RunPrimaryGenerateAction();
                }

                if (layout.randomizeVisible)
                    DrawToolbarRandomizeButton(layout.randomizeWidth);

                if (layout.lockVisible)
                    DrawToolbarLockButton(layout.lockWidth);

                if (layout.duplicateVisible)
                    DrawToolbarDuplicateButton(layout.duplicateWidth);

                if (layout.inspectorVisible)
                    DrawToolbarInspectorButton(layout.inspectorWidth);

                if (layout.viewVisible)
                {
                    if (GUILayout.Button(new GUIContent("View", "Preview display, tile preview, and candidate density."), EditorStyles.toolbarDropDown, GUILayout.Width(layout.viewWidth)))
                        ToggleViewPopup();
                }

                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill($"{_influences.Count} guides", _influences.Count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 88f);
                UtilityWindowTheme.CountPill($"{_combination.candidateCount} textures", UtilityWindowTheme.Cyan, 104f);

                if (GUILayout.Button(new GUIContent("More", "Secondary texture generator actions."), EditorStyles.toolbarDropDown, GUILayout.Width(layout.moreWidth)))
                    ShowMoreMenu(layout.randomizeVisible, layout.inspectorVisible, layout.viewVisible);
            }
        }

        private ToolbarActionLayout BuildToolbarActionLayout(float toolbarWidth)
        {
            var layout = new ToolbarActionLayout
            {
                tiny = toolbarWidth < 720f,
                generateWidth = MeasureToolbarButton("Generate", toolbarWidth < 720f ? 94f : 112f, 144f),
                randomizeWidth = MeasureToolbarButton("Randomize", toolbarWidth < 720f ? 82f : 104f, 118f),
                lockWidth = MeasureToolbarButton("Lock", 58f, 74f),
                duplicateWidth = MeasureToolbarButton("Duplicate", 76f, 98f),
                inspectorWidth = MeasureToolbarButton("Inspector", 78f, 98f),
                viewWidth = MeasureToolbarButton("View", 52f, 64f),
                moreWidth = MeasureToolbarButton("More", 58f, 68f)
            };

            float optionalWidth = Mathf.Max(0f, toolbarWidth - layout.generateWidth - layout.moreWidth - 210f);
            layout.randomizeVisible = ReserveToolbarWidth(ref optionalWidth, layout.randomizeWidth);
            layout.lockVisible = ReserveToolbarWidth(ref optionalWidth, layout.lockWidth);
            layout.duplicateVisible = ReserveToolbarWidth(ref optionalWidth, layout.duplicateWidth);
            layout.inspectorVisible = ReserveToolbarWidth(ref optionalWidth, layout.inspectorWidth);
            layout.viewVisible = ReserveToolbarWidth(ref optionalWidth, layout.viewWidth);
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

        private void DrawToolbarRandomizeButton(float width)
        {
            using (new EditorGUI.DisabledScope(_generationRunning))
            {
                if (GUILayout.Button(new GUIContent("Randomize", "Randomize unlocked texture parameters, then generate."), EditorStyles.toolbarButton, GUILayout.Width(width)))
                    RandomizeUnlockedVariants();
            }
        }

        private void DrawToolbarLockButton(float width)
        {
            using (new EditorGUI.DisabledScope(_selectedCandidates.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("Guide", "Toggle whether selected textures will become Guides on Generate."), EditorStyles.toolbarButton, GUILayout.Width(width)))
                    ToggleSelectedVariantLocks();
            }
        }

        private void DrawToolbarDuplicateButton(float width)
        {
            using (new EditorGUI.DisabledScope(_selectedCandidates.Count == 0 || _candidates.Count >= 16))
            {
                if (GUILayout.Button(new GUIContent("Duplicate", "Duplicate the selected texture into the grid."), EditorStyles.toolbarButton, GUILayout.Width(width)))
                    DuplicateSelectedVariant();
            }
        }

        private void DrawToolbarInspectorButton(float width)
        {
            bool active = _inspectorOpen;
            if (GUILayout.Toggle(active, new GUIContent("Inspector", "Open Compose, Refine, Export, and History."), EditorStyles.toolbarButton, GUILayout.Width(width)) != active)
                ToggleInspector();
        }

        private void DrawWorkbench()
        {
            _inspectorInline = ShouldDrawInlineInspector() && _inspectorOpen;
            float inspectorWidth = _inspectorInline ? ClampInspectorWidth(_inspectorWidth) : 0f;
            _inspectorWidth = inspectorWidth > 0f ? inspectorWidth : _inspectorWidth;
            float workspaceWidth = Mathf.Max(WorkspaceMinWidth, position.width - inspectorWidth - (_inspectorInline ? InspectorHandleWidth : 0f));

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(workspaceWidth), GUILayout.ExpandHeight(true)))
                    DrawWorkspacePanel(workspaceWidth);

                if (_inspectorInline)
                {
                    DrawInspectorResizeHandle();
                    DrawInspectorColumn(inspectorWidth);
                }
            }

            if (!_inspectorInline && _inspectorOpen)
                DrawInspectorPopup();
        }

        private void DrawWorkspacePanel(float width)
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.075f, 0.035f, 7, 3), GUILayout.ExpandHeight(true)))
            {
                DrawWorkspaceHeader();
                switch (_activeInspector)
                {
                    case TextureInspectorTab.Explore:
                        DrawExploreWorkspace(width);
                        break;
                    case TextureInspectorTab.Refine:
                        DrawRefineWorkspace(width);
                        break;
                    case TextureInspectorTab.Export:
                        DrawExportWorkspace(width);
                        break;
                    case TextureInspectorTab.History:
                        DrawHistoryWorkspace(width);
                        break;
                    default:
                        DrawComposeWorkspace(width);
                        break;
                }
            }
        }

        private void DrawWorkspaceHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.SectionTitle(WorkspaceTitle(), UtilityWindowTheme.Blue, _previewDirty ? "dirty" : "live");
                GUILayout.FlexibleSpace();
                if (_previewDirty && GUILayout.Button(new GUIContent("Refresh", "Regenerate the current preview."), EditorStyles.miniButton, GUILayout.Width(72f)))
                    GeneratePreview(true);
            }
            DrawStudioDivider(UtilityWindowTheme.Blue, 0.32f);
        }

        private string WorkspaceTitle()
        {
            switch (_activeInspector)
            {
                case TextureInspectorTab.Refine:
                    return "Refine Workbench";
                case TextureInspectorTab.Export:
                    return "Export Targets";
                case TextureInspectorTab.History:
                    return "Texture History";
                case TextureInspectorTab.Explore:
                    return "Explore Workbench";
                default:
                    return "Compose Workbench";
            }
        }

        private void DrawExploreWorkspace(float width)
        {
            ApplyExploreStrategyToLegacyWorkflow();
            using (BeginInspectorSection("Explore", UtilityWindowTheme.Cyan, _exploreStrategy.ToString(), TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    _exploreStrategy = (TextureExploreStrategy)EditorGUILayout.EnumPopup(new GUIContent("Strategy", "Random, guided, or constrained exploration."), _exploreStrategy, GUILayout.Width(180f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        ApplyExploreStrategyToLegacyWorkflow();
                        RequestSessionSave();
                    }
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"{_candidates.Count} texture{(_candidates.Count == 1 ? string.Empty : "s")}", UtilityWindowTheme.Cyan, 96f);
                }

                if (_activeWorkflow == TextureDesignWorkflow.ConstraintMatch)
                    DrawConstraintMatchWorkspace(width);
                else if (_activeWorkflow == TextureDesignWorkflow.PresetBrowser)
                    DrawPresetBrowserWorkspace(width);
                else
                    DrawRandomExploreWorkspace(width);
            }
        }

        private void DrawComposeWorkspace(float width)
        {
            using (BeginInspectorSection("Compose", UtilityWindowTheme.Cyan, "Manual Compose", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                _activeDesignerPhase = TextureDesignerPhase.Compose;
                if (_activeWorkflow != TextureDesignWorkflow.ManualCompose)
                    _activeWorkflow = TextureDesignWorkflow.ManualCompose;

                switch (_activeWorkflow)
                {
                    case TextureDesignWorkflow.ManualCompose:
                        DrawManualComposeWorkspace(width);
                        break;
                    case TextureDesignWorkflow.RandomExplore:
                        DrawRandomExploreWorkspace(width);
                        break;
                    case TextureDesignWorkflow.GuidedRefine:
                        DrawGuidedRefineWorkspace(width);
                        break;
                    case TextureDesignWorkflow.ConstraintMatch:
                        DrawConstraintMatchWorkspace(width);
                        break;
                    case TextureDesignWorkflow.BlendLab:
                        DrawBlendLabWorkspace(width);
                        break;
                    case TextureDesignWorkflow.ReferenceMatch:
                        DrawReferenceMatchWorkspace(width);
                        break;
                    case TextureDesignWorkflow.PresetBrowser:
                        DrawPresetBrowserWorkspace(width);
                        break;
                    case TextureDesignWorkflow.MaterialMapPrep:
                        DrawMaterialMapPrepWorkspace(width);
                        break;
                    default:
                        DrawWorkflowPlaceholder(width, _activeWorkflow);
                        break;
                }
            }
        }

        private void DrawComposeWorkflowHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _activeWorkflow = (TextureDesignWorkflow)EditorGUILayout.EnumPopup(new GUIContent("Workflow", "Choose how the Compose surface should behave."), _activeWorkflow);
                TextureTileabilityMode nextTileability = (TextureTileabilityMode)EditorGUILayout.EnumPopup(new GUIContent("Tileable", "Preview repeats the texture. Generate Seamless wraps stamps and spacing during generation."), _tileabilityMode, GUILayout.Width(190f));
                if (EditorGUI.EndChangeCheck())
                {
                    _tileabilityMode = nextTileability;
                    SyncTileabilitySettings();
                    _lastStatus = _tileabilityMode == TextureTileabilityMode.GenerateSeamless
                        ? "Generate Seamless will wrap stamps and spacing for new textures."
                        : "Updated tileability mode.";
                    SavePrefs();
                    RequestSessionSave();
                }
            }
        }

        private void DrawRandomExploreWorkspace(float width)
        {
            using (BeginInspectorSection("Texture Grid", UtilityWindowTheme.Cyan, _candidates.Count == 0 ? "empty" : $"{_candidates.Count}", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_candidates.Count == 0)
                    {
                        EditorGUILayout.LabelField("Generate a texture grid.", UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                    else
                    {
                        UtilityWindowTheme.CountPill($"{_selectedCandidates.Count} selected", _selectedCandidates.Count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 96f);
                        if (IsValidActiveCandidate())
                            UtilityWindowTheme.CountPill($"active #{_activeCandidateIndex + 1}", UtilityWindowTheme.Blue, 82f);
                        GUILayout.FlexibleSpace();
                        UtilityWindowTheme.CountPill($"{GridLockedVariantCount()} guides", GridLockedVariantCount() > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 82f);
                    }
                    GUILayout.FlexibleSpace();
                }

                DrawInfluenceShelf(width);

                Rect viewport = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(160f));
                if (_candidates.Count == 0)
                {
                    DrawCandidateEmptyState(viewport);
                    return;
                }

                DrawComposeCandidateGrid(viewport);
                DrawTextureEditOverlay();
            }
        }

        private void DrawGuidedRefineWorkspace(float width)
        {
            using (BeginInspectorSection("Similarity Canvas", UtilityWindowTheme.Blue, IsValidActiveCandidate() ? $"target #{_activeCandidateIndex + 1}" : "no target", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                if (!IsValidActiveCandidate() && _candidates.Count > 0)
                    SetActiveCandidate(0, false);

                if (!IsValidActiveCandidate())
                {
                    Rect viewport = GUILayoutUtility.GetRect(240f, 280f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawCandidateEmptyState(viewport);
                    return;
                }

                ProceduralTextureCandidate active = _candidates[_activeCandidateIndex];
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(active.label, UtilityWindowTheme.Blue, 106f);
                    UtilityWindowTheme.CountPill($"Similarity {SimilarityBand(active.similarityTarget01)}", UtilityWindowTheme.Green, 132f);
                    UtilityWindowTheme.CountPill($"{PreservedTraitCount(active)} traits", UtilityWindowTheme.Purple, 82f);
                    GUILayout.FlexibleSpace();
                    if (StudioButton("Generate Similar", ComposeTint(), TextureButtonTone.Primary, GUILayout.Width(132f), GUILayout.Height(24f)))
                        GenerateSimilarFromActiveCandidate();
                }

                Rect canvas = GUILayoutUtility.GetRect(280f, Mathf.Max(260f, position.height * 0.48f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(260f));
                DrawGuidedRefineSimilarityRing(canvas, active);
                DrawRefineCandidateStrip();
            }
        }

        private void DrawConstraintMatchWorkspace(float width)
        {
            using (BeginInspectorSection("Constraint Grid", UtilityWindowTheme.Amber, _candidates.Count == 0 ? "empty" : $"{_candidates.Count}", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int passCount = ConstraintPassCount();
                    UtilityWindowTheme.CountPill($"Pass {passCount}/{_candidates.Count}", passCount > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 94f);
                    UtilityWindowTheme.CountPill($"Seam > {_combination.seamScoreThreshold:0.00}", UtilityWindowTheme.Amber, 106f);
                    UtilityWindowTheme.CountPill($"Tileable {_tileabilityMode}", UtilityWindowTheme.Blue, 142f);
                    if (_candidates.Count > 0)
                        UtilityWindowTheme.CountPill($"Best {BestConstraintScore():0.00}", UtilityWindowTheme.Green, 82f);
                    GUILayout.FlexibleSpace();
                    if (StudioButton("Generate", ComposeTint(), TextureButtonTone.Primary, GUILayout.Width(92f), GUILayout.Height(24f)))
                        GenerateCandidates();
                    using (new EditorGUI.DisabledScope(_candidates.Count <= 1))
                    {
                        if (StudioButton("Sort by Score", UtilityWindowTheme.Amber, TextureButtonTone.Secondary, GUILayout.Width(108f), GUILayout.Height(24f)))
                            SortCandidatesByConstraintScore();
                    }
                    if (StudioButton("Relax", UtilityWindowTheme.Neutral, TextureButtonTone.Ghost, GUILayout.Width(64f), GUILayout.Height(24f)))
                        RelaxConstraintTarget();
                }

                DrawConstraintScoreMatrix();

                Rect viewport = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(160f));
                if (_candidates.Count == 0)
                {
                    DrawCandidateEmptyState(viewport);
                    return;
                }

                DrawComposeCandidateGrid(viewport);
                DrawTextureEditOverlay();
            }
        }

        private float BestConstraintScore()
        {
            float best = 0f;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i] != null)
                    best = Mathf.Max(best, _candidates[i].constraintScore01);
            }
            return best;
        }

        private void DrawConstraintScoreMatrix()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 92f, GUILayout.ExpandWidth(true), GUILayout.Height(92f));
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Amber, 0.055f), PanelBorder(UtilityWindowTheme.Amber, 0.22f));

            if (_candidates.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 18f), "Generate textures to score seam, coverage, contrast, and scale.", UtilityWindowTheme.MutedMiniLabelStyle);
                return;
            }

            float x = rect.x + 8f;
            float columnWidth = Mathf.Max(82f, (rect.width - 16f) / Mathf.Min(_candidates.Count, 8));
            for (int i = 0; i < _candidates.Count && i < 8; i++)
            {
                ProceduralTextureCandidate candidate = _candidates[i];
                Rect cell = new Rect(x, rect.y + 8f, columnWidth - 6f, rect.height - 16f);
                DrawConstraintScoreCell(cell, i, candidate);
                x += columnWidth;
            }
        }

        private void DrawConstraintScoreCell(Rect rect, int index, ProceduralTextureCandidate candidate)
        {
            if (candidate == null)
                return;

            Color tint = candidate.constraintScore01 >= 0.76f ? UtilityWindowTheme.Green : candidate.constraintScore01 >= 0.52f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral;
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(tint, 0.08f), PanelBorder(tint, 0.36f));

            bool passes = CandidatePassesConstraints(candidate);
            GUI.Label(new Rect(rect.x + 5f, rect.y + 3f, rect.width - 46f, 14f), $"#{index + 1}  {candidate.constraintScore01:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
            DrawConstraintPassBadge(new Rect(rect.xMax - 40f, rect.y + 3f, 34f, 14f), passes);
            DrawMetricRow(new Rect(rect.x + 5f, rect.y + 23f, rect.width - 10f, 7f), "S", candidate.seamScore01, candidate.seamScore01 >= _combination.seamScoreThreshold, UtilityWindowTheme.Blue);
            DrawMetricRow(new Rect(rect.x + 5f, rect.y + 36f, rect.width - 10f, 7f), "V", candidate.coverage01, IsWithin(candidate.coverage01, _combination.constraintCoverageMin01, _combination.constraintCoverageMax01), UtilityWindowTheme.Green);
            DrawMetricRow(new Rect(rect.x + 5f, rect.y + 49f, rect.width - 10f, 7f), "C", candidate.contrast01, IsWithin(candidate.contrast01, _combination.constraintContrastMin01, _combination.constraintContrastMax01), UtilityWindowTheme.Purple);
            DrawMetricRow(new Rect(rect.x + 5f, rect.y + 62f, rect.width - 10f, 7f), "R", candidate.scale01, IsWithin(candidate.scale01, _combination.constraintScaleMin01, _combination.constraintScaleMax01), UtilityWindowTheme.Cyan);
            GUI.Label(new Rect(rect.x + 5f, rect.yMax - 15f, rect.width - 10f, 13f), passes ? "Pass" : "Review targets", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static void DrawMiniScoreBar(Rect rect, float value, Color tint)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.08f, 0.08f, 0.085f) : new Color(0.72f, 0.72f, 0.74f));
            Color fill = tint;
            fill.a = 0.78f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), fill);
        }

        private static void DrawMetricRow(Rect rect, string label, float value, bool passing, Color tint)
        {
            GUI.Label(new Rect(rect.x, rect.y - 5f, 14f, 14f), label, UtilityWindowTheme.MutedMiniLabelStyle);
            DrawMiniScoreBar(new Rect(rect.x + 15f, rect.y, rect.width - 34f, rect.height), value, passing ? tint : UtilityWindowTheme.Neutral);
            GUI.Label(new Rect(rect.xMax - 17f, rect.y - 5f, 18f, 14f), passing ? "OK" : "!", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private static void DrawConstraintPassBadge(Rect rect, bool passes)
        {
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(passes ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 0.18f), PanelBorder(passes ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 0.54f));
            DrawCenteredMiniLabel(rect, passes ? "Pass" : "Check");
        }

        private static bool IsWithin(float value, float min, float max)
        {
            return value >= min && value <= max;
        }

        private void DrawManualComposeWorkspace(float width)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioButton("Add Layer", ComposeTint(), TextureButtonTone.Primary, GUILayout.Height(24f)))
                    AddBase();
                using (new EditorGUI.DisabledScope(_selectedBase < 0 || _selectedBase >= _bases.Count))
                {
                    if (StudioButton("Duplicate", ComposeTint(), TextureButtonTone.Secondary, GUILayout.Height(24f)))
                        DuplicateSelectedBase();
                    if (StudioButton("Delete", UtilityWindowTheme.Red, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                        DeleteSelectedBase();
                }

                int selectedVariant = FirstSelectedVariantIndex();
                using (new EditorGUI.DisabledScope(selectedVariant < 0 || selectedVariant >= _candidates.Count || _bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount))
                {
                    if (StudioButton("Add Selected as Layer", UtilityWindowTheme.Green, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                        AddCandidateAsBase(selectedVariant);
                }

                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                _tileabilityMode = (TextureTileabilityMode)EditorGUILayout.EnumPopup(new GUIContent("Tileable", "Off, preview-only tiling, or generation-time seamless output."), _tileabilityMode, GUILayout.Width(164f), GUILayout.Height(24f));
                if (EditorGUI.EndChangeCheck())
                {
                    SyncTileabilitySettings();
                    MarkDirty("Updated tileability mode.");
                }
                EditorGUI.BeginChangeCheck();
                _manualComposeAutoPreview = GUILayout.Toggle(_manualComposeAutoPreview, new GUIContent("Auto Preview", "Automatically refresh the combined preview after manual layer edits."), EditorStyles.miniButton, GUILayout.Height(24f), GUILayout.Width(92f));
                if (EditorGUI.EndChangeCheck())
                {
                    _lastStatus = _manualComposeAutoPreview ? "Auto preview enabled." : "Auto preview paused.";
                    if (_manualComposeAutoPreview && _previewDirty)
                        QueueManualComposeAutoPreview("Manual compose changed.");
                    RequestSessionSave();
                }
                if (StudioButton("Refresh Preview", UtilityWindowTheme.Blue, TextureButtonTone.Ghost, GUILayout.Height(24f), GUILayout.Width(112f)))
                    GeneratePreview(true);
            }

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                float railWidth = Mathf.Clamp(width * 0.28f, 220f, 320f);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(railWidth), GUILayout.ExpandHeight(true)))
                    DrawManualComposeAnalysisRail();

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    DrawManualComposeOutputPreview();
            }
        }

        private void DrawManualComposeOutputPreview()
        {
            string status = _previewDirty
                ? (_manualComposeAutoPreview ? "auto queued" : "dirty")
                : "live";
            using (BeginInspectorSection("Combined Output", UtilityWindowTheme.Blue, status, TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                Rect previewRect = GUILayoutUtility.GetRect(260f, 360f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawCheckerBackground(previewRect);
                if (_previewTexture != null)
                {
                    Rect fitted = FitRect(previewRect, _previewTexture.width, _previewTexture.height);
                    if (_combination.tilePreview)
                        DrawTiledTexture(previewRect, _previewTexture, 2);
                    else
                        GUI.DrawTexture(fitted, _previewTexture, ScaleMode.ScaleToFit, true);
                    DrawManualContributionHighlight(_combination.tilePreview ? previewRect : fitted);
                    DrawSeamOverlay(_combination.tilePreview ? previewRect : fitted, null);
                }
                else
                {
                    DrawInlineCenteredMessage(previewRect, _manualComposeAutoPreview ? "Auto preview will build the current layer stack." : "Refresh preview to see the current layer stack.");
                }
                DrawPreviewBorder(previewRect);

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"{EnabledBaseCount()} layer{(EnabledBaseCount() == 1 ? string.Empty : "s")} on", ComposeTint(), 96f);
                    UtilityWindowTheme.CountPill(_previewDirty ? "Dirty" : "Synced", _previewDirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 72f);
                    UtilityWindowTheme.CountPill(_combination.tilePreview ? "2x Tile" : "1x", UtilityWindowTheme.Blue, 66f);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawManualComposeAnalysisRail()
        {
            _manualContributionHoverIndex = -1;
            using (BeginInspectorSection("Layer Contribution", ComposeTint(), $"{EnabledBaseCount()} active", TextureButtonTone.Secondary))
            {
                using (new EditorGUI.DisabledScope(_manualContributionHighlightIndex < 0))
                {
                    if (GUILayout.Button(new GUIContent("Clear Highlight", "Clear the diagnostic layer highlight overlay."), EditorStyles.miniButton, GUILayout.Height(20f)))
                        _manualContributionHighlightIndex = -1;
                }

                if (_manualLayerContributions.Count == 0)
                {
                    DrawInlineStatus("Layer analysis updates after the next preview refresh.", UtilityWindowTheme.Neutral);
                }
                else
                {
                    int shown = 0;
                    for (int i = 0; i < _manualLayerContributions.Count; i++)
                    {
                        if (i >= _bases.Count || _bases[i] == null || !_bases[i].enabled)
                            continue;
                        DrawManualContributionRow(_manualLayerContributions[i]);
                        shown++;
                    }
                    if (shown == 0)
                        DrawInlineStatus("All layers are muted.", UtilityWindowTheme.Amber);
                }
            }

            using (BeginInspectorSection("Output Metrics", UtilityWindowTheme.Blue, _previewDirty ? "stale" : "cached", TextureButtonTone.Ghost))
            {
                DrawManualMetric("Coverage", _manualOutputCoverage01, UtilityWindowTheme.Green);
                DrawManualMetric("Contrast", _manualOutputContrast01, UtilityWindowTheme.Blue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Tone", UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(58f));
                    GUILayout.Label($"{_manualOutputToneMin01:0.00} - {_manualOutputToneMax01:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                if (_combination.tilePreview || _tileabilityMode != TextureTileabilityMode.Off)
                    DrawManualMetric("Seam", _manualOutputSeamScore01, UtilityWindowTheme.Cyan);
                if (GUILayout.Button(new GUIContent("Metrics", "Open detailed cached output metrics and seam diagnostics."), EditorStyles.miniButton, GUILayout.Height(20f)))
                    _showMetricsPopup = true;
            }
        }

        private void DrawManualContributionRow(ManualLayerContributionEntry entry)
        {
            if (entry == null)
                return;

            Rect row = GUILayoutUtility.GetRect(64f, 44f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(row, PanelFill(ComposeTint(), entry.index == _selectedBase ? 0.13f : 0.055f), PanelBorder(ComposeTint(), entry.index == _selectedBase ? 0.56f : 0.20f));

            Rect strip = new Rect(row.x + 6f, row.y + 7f, 54f, row.height - 14f);
            DrawCheckerBackground(strip);
            if (entry.strip != null)
                GUI.DrawTexture(strip, entry.strip, ScaleMode.StretchToFill, true);
            DrawStudioBox(strip, Color.clear, PanelBorder(UtilityWindowTheme.Neutral, 0.24f));

            Rect label = new Rect(strip.xMax + 7f, row.y + 5f, row.width - 70f, 16f);
            GUI.Label(label, $"{entry.index + 1}. {entry.label}", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(label.x, label.yMax + 1f, label.width, 14f), $"{BlendModeLabel(entry.blendMode)} / {entry.weight:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);

            Rect bar = new Rect(label.x, row.yMax - 9f, Mathf.Max(24f, label.width - 4f), 4f);
            EditorGUI.DrawRect(bar, PanelFill(UtilityWindowTheme.Neutral, 0.18f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(entry.contribution01), bar.height), ComposeTint());

            if (row.Contains(Event.current.mousePosition))
                _manualContributionHoverIndex = entry.index;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
            {
                SelectSingleBase(entry.index);
                _manualContributionHighlightIndex = entry.index;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawManualContributionHighlight(Rect rect)
        {
            int highlightIndex = _manualContributionHoverIndex >= 0 ? _manualContributionHoverIndex : _manualContributionHighlightIndex;
            if (highlightIndex < 0)
                return;

            ManualLayerContributionEntry entry = null;
            for (int i = 0; i < _manualLayerContributions.Count; i++)
            {
                if (_manualLayerContributions[i] != null && _manualLayerContributions[i].index == highlightIndex)
                {
                    entry = _manualLayerContributions[i];
                    break;
                }
            }

            if (entry == null || entry.highlight == null)
                return;

            if (_combination.tilePreview)
                DrawTiledTexture(rect, entry.highlight, 2);
            else
                GUI.DrawTexture(rect, entry.highlight, ScaleMode.ScaleToFit, true);
            DrawStudioBox(rect, Color.clear, PanelBorder(ComposeTint(), 0.72f));
        }

        private void DrawManualBlendStackRibbon(Rect rect)
        {
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Neutral, 0.055f), PanelBorder(UtilityWindowTheme.Neutral, 0.20f));

            int enabledCount = Mathf.Max(1, EnabledBaseCount());
            float y = rect.y + 8f;
            float height = Mathf.Max(14f, (rect.height - 16f - Mathf.Max(0, enabledCount - 1) * 4f) / enabledCount);
            int order = 0;
            for (int i = 0; i < _bases.Count; i++)
            {
                ProceduralTextureBaseSettings textureBase = _bases[i];
                if (textureBase == null || !textureBase.enabled)
                    continue;

                float width01 = Mathf.Clamp01(textureBase.weight);
                Rect lane = new Rect(rect.x + 8f, y + order * (height + 4f), rect.width - 16f, height);
                EditorGUI.DrawRect(lane, PanelFill(UtilityWindowTheme.Neutral, 0.16f));
                EditorGUI.DrawRect(new Rect(lane.x, lane.y, lane.width * Mathf.Max(0.08f, width01), lane.height), PanelFill(ComposeTint(), i == _selectedBase ? 0.54f : 0.32f));
                GUI.Label(new Rect(lane.x + 5f, lane.y - 1f, lane.width - 10f, lane.height + 2f), $"{i + 1} {BlendModeLabel(textureBase.blendMode)}", UtilityWindowTheme.MutedMiniLabelStyle);
                order++;
            }
        }

        private void DrawManualMetric(string label, float value, Color tint)
        {
            Rect row = GUILayoutUtility.GetRect(64f, 18f, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(row.x, row.y, 58f, row.height), label, UtilityWindowTheme.MutedMiniLabelStyle);
            Rect bar = new Rect(row.x + 64f, row.y + 6f, row.width - 108f, 5f);
            EditorGUI.DrawRect(bar, PanelFill(UtilityWindowTheme.Neutral, 0.18f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value), bar.height), tint);
            GUI.Label(new Rect(row.xMax - 38f, row.y, 38f, row.height), value.ToString("0.00"), UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawWorkflowPlaceholder(float width, TextureDesignWorkflow workflow)
        {
            Rect viewport = GUILayoutUtility.GetRect(240f, 260f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(viewport, PanelFill(UtilityWindowTheme.Neutral, 0.055f), PanelBorder(UtilityWindowTheme.Neutral, 0.22f));

            Rect content = new Rect(viewport.x + 18f, viewport.y + 18f, viewport.width - 36f, viewport.height - 36f);
            GUI.Label(new Rect(content.x, content.y, content.width, 22f), ObjectNames.NicifyVariableName(workflow.ToString()), EditorStyles.boldLabel);
            GUI.Label(new Rect(content.x, content.y + 28f, content.width, 42f), WorkflowPlaceholderCopy(workflow), UtilityWindowTheme.MutedMiniLabelStyle);

            Rect button = new Rect(content.x, content.y + 82f, 148f, 26f);
            if (StudioButton(button, new GUIContent("Use Random Explore", "Return to the implemented candidate-grid workflow."), ComposeTint(), TextureButtonTone.Primary))
                _activeWorkflow = TextureDesignWorkflow.RandomExplore;
        }

        private static string WorkflowPlaceholderCopy(TextureDesignWorkflow workflow)
        {
            switch (workflow)
            {
                case TextureDesignWorkflow.GuidedRefine:
                    return "Guided Refine will use a focus canvas and similarity controls. Use the Refine phase for the current single-texture refinement path.";
                case TextureDesignWorkflow.BlendLab:
                    return "Blend Lab will expose A/B trait blending. The workflow route is visible, but the braid controls are still pending.";
                case TextureDesignWorkflow.ReferenceMatch:
                    return "Reference Match will decompose a source texture into structure, density, tone, palette, and seam traits.";
                case TextureDesignWorkflow.ConstraintMatch:
                    return "Constraint Match will score generated variants against coverage, contrast, scale, seam, and intent targets.";
                case TextureDesignWorkflow.PresetBrowser:
                    return "Preset Browser will show editable recipe templates. Preset storage and recipe stack previews are still pending.";
                case TextureDesignWorkflow.MaterialMapPrep:
                    return "Material Map Prep will focus map intent, map strip previews, and optional export bridge handoff.";
                default:
                    return "This workflow route is available for review and will be expanded in a later implementation pass.";
            }
        }

        private void DrawExportWorkspace(float width)
        {
            using (BeginInspectorSection("Selected Textures", ExportTint(), _selectedCandidates.Count == 0 ? "none" : $"{_selectedCandidates.Count}", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                Rect viewport = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(160f));
                if (_candidates.Count == 0)
                {
                    DrawCandidateEmptyState(viewport);
                    return;
                }

                DrawComposeCandidateGrid(viewport);
                DrawInlineStatus("Export uses the selected textures. Use the Export inspector to choose maps and write PNGs.", ExportTint());
            }
        }

        private void DrawHistoryWorkspace(float width)
        {
            DrawHistoryLineageWorkspace(width);
        }

        private static void DrawInlineCenteredMessage(Rect rect, string message)
        {
            GUI.Label(new Rect(rect.x + 12f, rect.center.y - 10f, rect.width - 24f, 20f), message, UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawInfluenceShelf(float width)
        {
            if (_influences.Count == 0)
                return;

            using (BeginInspectorSection("Guides", UtilityWindowTheme.Green, $"{_influences.Count}", TextureButtonTone.Ghost))
            {
                Rect viewport = GUILayoutUtility.GetRect(width, 142f, GUILayout.ExpandWidth(true), GUILayout.Height(142f));
                float tileWidth = 176f;
                float contentWidth = _influences.Count * tileWidth + Mathf.Max(0, _influences.Count - 1) * BaseGap;
                Rect view = new Rect(0f, 0f, Mathf.Max(viewport.width, contentWidth), 124f);
                _influenceScroll = GUI.BeginScrollView(viewport, _influenceScroll, view, contentWidth > viewport.width, false);
                for (int i = 0; i < _influences.Count; i++)
                    DrawInfluenceShelfTile(i, new Rect(i * (tileWidth + BaseGap), 0f, tileWidth, 118f));
                GUI.EndScrollView();
            }
        }

        private void DrawInfluenceShelfTile(int index, Rect rect)
        {
            if (index < 0 || index >= _influences.Count)
                return;

            ProceduralTextureCandidate influence = _influences[index];
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Green, 0.12f), PanelBorder(UtilityWindowTheme.Green, 0.62f));

            Rect image = new Rect(rect.x + 5f, rect.y + 5f, 54f, 54f);
            DrawCheckerBackground(image);
            if (influence.preview != null)
                GUI.DrawTexture(FitRect(image, influence.preview.width, influence.preview.height), influence.preview, ScaleMode.ScaleToFit, true);

            GUI.Label(new Rect(image.xMax + 7f, rect.y + 7f, rect.width - image.width - 42f, 18f), influence.label, EditorStyles.miniBoldLabel);
            if (GUI.Button(new Rect(rect.xMax - 28f, rect.y + 7f, 22f, 18f), new GUIContent("X", "Remove this guide. It can be restored from History."), EditorStyles.miniButton))
                RemoveInfluence(index);

            Rect slider = new Rect(image.xMax + 7f, rect.y + 42f, rect.width - image.width - 20f, 16f);
            EditorGUI.BeginChangeCheck();
            influence.influenceWeight = GUI.HorizontalSlider(slider, Mathf.Clamp01(influence.influenceWeight), 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                influence.guideStrength01 = Mathf.Clamp01(influence.influenceWeight);
                QueueInfluenceFeaturePreviewRebuild();
                RequestSessionSave();
            }

            Rect similaritySlider = new Rect(image.xMax + 7f, rect.y + 68f, rect.width - image.width - 20f, 16f);
            EditorGUI.BeginChangeCheck();
            influence.similarityTarget01 = GUI.HorizontalSlider(similaritySlider, Mathf.Clamp01(influence.similarityTarget01 <= 0f ? 0.55f : influence.similarityTarget01), 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                QueueInfluenceFeaturePreviewRebuild();
                RequestSessionSave();
            }

            Rect preserveRect = new Rect(rect.x + 7f, rect.yMax - 25f, 68f, 18f);
            bool preserve = influence.guidanceMode == ProceduralTextureGuidanceMode.Preserve;
            bool nextPreserve = GUI.Toggle(preserveRect, preserve, new GUIContent("Preserve", "Preserve broad structure, density, scale, detail, and tone from this guide."), EditorStyles.miniButton);
            if (nextPreserve != preserve)
            {
                influence.guidanceMode = nextPreserve ? ProceduralTextureGuidanceMode.Preserve : ProceduralTextureGuidanceMode.Guide;
                influence.preserveStructure = nextPreserve;
                influence.preserveDensity = nextPreserve;
                influence.preserveScale = nextPreserve;
                influence.preserveDetail = nextPreserve;
                influence.preserveTone = nextPreserve;
                QueueInfluenceFeaturePreviewRebuild();
                RequestSessionSave();
            }

            GUI.Label(new Rect(preserveRect.xMax + 6f, rect.yMax - 24f, rect.width - preserveRect.width - 18f, 18f), $"Strength {influence.influenceWeight:0.00}  Similarity {influence.similarityTarget01:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
        }

        private void DrawCandidateEmptyState(Rect viewport)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(viewport, EditorGUIUtility.isProSkin ? new Color(0.06f, 0.06f, 0.065f, 0.44f) : new Color(0.76f, 0.77f, 0.79f, 0.30f));

            Rect button = new Rect(viewport.center.x - 58f, viewport.center.y - 14f, 116f, 28f);
            if (StudioButton(button, new GUIContent("Generate", "Create a candidate batch."), ComposeTint(), TextureButtonTone.Primary))
                GenerateCandidates();
        }

        private void DrawComposeCandidateGrid(Rect viewport)
        {
            int count = Mathf.Max(1, _candidates.Count);
            int columns = BestCandidateColumnCount(count, viewport.width, viewport.height);
            int rows = Mathf.CeilToInt(count / (float)columns);
            float tileWidth = Mathf.Floor((viewport.width - (columns - 1) * BaseGap) / columns);
            float tileHeight = Mathf.Floor((viewport.height - (rows - 1) * BaseGap) / rows);
            if (tileHeight < 112f)
                tileHeight = 112f;
            float contentHeight = Mathf.Max(viewport.height, rows * tileHeight + Mathf.Max(0, rows - 1) * BaseGap);

            Rect view = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 16f), contentHeight);
            _candidateScroll = GUI.BeginScrollView(viewport, _candidateScroll, view, false, contentHeight > viewport.height);
            for (int i = 0; i < _candidates.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                Rect rect = new Rect(column * (tileWidth + BaseGap), row * (tileHeight + BaseGap), tileWidth, tileHeight);
                DrawCandidateCard(i, rect);
            }
            GUI.EndScrollView();
        }

        private int BestCandidateColumnCount(int count, float width, float height)
        {
            if (count >= 16)
                return 4;

            int maxColumns = Mathf.Clamp(count, 1, 4);
            int bestColumns = 1;
            float bestScore = float.MinValue;
            for (int columns = 1; columns <= maxColumns; columns++)
            {
                int rows = Mathf.CeilToInt(count / (float)columns);
                float tileWidth = (width - (columns - 1) * BaseGap) / columns;
                float tileHeight = (height - (rows - 1) * BaseGap) / rows;
                float aspect = tileWidth / Mathf.Max(1f, tileHeight);
                float score = Mathf.Min(tileWidth, tileHeight) - Mathf.Abs(aspect - 1f) * 36f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestColumns = columns;
                }
            }
            return Mathf.Max(1, bestColumns);
        }

        private void DrawCandidateCard(int index, Rect rect)
        {
            ProceduralTextureCandidate candidate = _candidates[index];
            bool selected = _selectedCandidates.Contains(index);
            bool locked = candidate.locked;
            bool hover = rect.Contains(Event.current.mousePosition);
            Color tint = locked ? UtilityWindowTheme.Green : selected ? UtilityWindowTheme.Purple : UtilityWindowTheme.Neutral;
            if (Event.current.type == EventType.Repaint)
            {
                DrawStudioBox(rect, PanelFill(tint, selected || locked ? 0.13f : 0.055f), PanelBorder(tint, selected || locked ? 0.82f : hover ? 0.42f : 0.22f));
                if (selected)
                    DrawStudioBox(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), Color.clear, PanelBorder(UtilityWindowTheme.Purple, 0.72f));
            }

            Rect topBar = new Rect(rect.x, rect.y, rect.width, 28f);
            Rect bottomBar = new Rect(rect.x, rect.yMax - (locked ? 56f : 32f), rect.width, locked ? 56f : 32f);
            Color shade = EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.48f) : new Color(1f, 1f, 1f, 0.62f);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(topBar, shade);
                EditorGUI.DrawRect(bottomBar, shade);
            }

            Rect imageRect = new Rect(rect.x + 6f, rect.y + 32f, rect.width - 12f, rect.height - (locked ? 94f : 70f));
            DrawCheckerBackground(imageRect);
            if (candidate.preview != null)
            {
                Rect fitted = FitRect(imageRect, candidate.preview.width, candidate.preview.height);
                GUI.DrawTexture(fitted, candidate.preview, ScaleMode.ScaleToFit, true);
                DrawSeamOverlay(fitted, candidate.seamHeatmap);
            }

            Rect selectRect = new Rect(rect.x + 6f, rect.y + 5f, 24f, 18f);
            Rect lockRect = new Rect(selectRect.xMax + 4f, selectRect.y, 42f, 18f);
            Rect menuRect = new Rect(rect.xMax - 34f, selectRect.y, 28f, 18f);
            Rect editRect = new Rect(menuRect.x - 42f, selectRect.y, 38f, 18f);
            GUI.Label(new Rect(lockRect.xMax + 6f, rect.y + 5f, Mathf.Max(40f, editRect.x - lockRect.xMax - 10f), 18f), candidate.label, EditorStyles.miniBoldLabel);

                if (GUI.Toggle(selectRect, selected, new GUIContent(selected ? "X" : "", selected ? "Deselect texture." : "Select texture."), EditorStyles.miniButton) != selected)
                ToggleCandidateSelection(index, true);
            if (GUI.Button(lockRect, new GUIContent(locked ? "Guide" : "None", locked ? "Remove this texture as a pending Guide." : "Use this texture as a Guide on the next Generate."), EditorStyles.miniButton))
                ToggleVariantLock(index);
            if (GUI.Button(editRect, new GUIContent("Edit", "Open texture parameters."), EditorStyles.miniButton))
                OpenTextureEditOverlay(index, editRect);
            if (GUI.Button(menuRect, new GUIContent("...", "Texture actions."), EditorStyles.miniButton))
                ShowTextureCardMenu(index);

            Rect influenceSliderRect = Rect.zero;
            if (locked)
            {
                Rect sliderRect = new Rect(rect.x + 8f, rect.yMax - 24f, rect.width - 68f, 16f);
                influenceSliderRect = sliderRect;
                Rect weightRect = new Rect(sliderRect.xMax + 4f, sliderRect.y - 1f, 56f, 18f);
                EditorGUI.BeginChangeCheck();
                candidate.influenceWeight = GUI.HorizontalSlider(sliderRect, Mathf.Clamp01(candidate.influenceWeight), 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    candidate.guideStrength01 = Mathf.Clamp01(candidate.influenceWeight);
                    RequestSessionSave();
                }
                GUI.Label(weightRect, $"{candidate.influenceWeight:0.00}", UtilityWindowTheme.MutedMiniLabelStyle);
                string guideStatus = _activeWorkflow == TextureDesignWorkflow.ConstraintMatch
                    ? $"{ConstraintStatus(candidate)} - Seam {candidate.seamScore01:0.00}"
                    : "Becomes a Guide on Generate";
                if (_activeWorkflow != TextureDesignWorkflow.ConstraintMatch && (_tileabilityMode == TextureTileabilityMode.GenerateSeamless || _combination.generateSeamless))
                    guideStatus += $" - Seam {candidate.seamScore01:0.00}";
                GUI.Label(new Rect(rect.x + 8f, rect.yMax - 49f, rect.width - 16f, 16f), guideStatus, UtilityWindowTheme.MutedMiniLabelStyle);
            }
            else
            {
                string state = _activeWorkflow == TextureDesignWorkflow.ConstraintMatch
                    ? ConstraintStatus(candidate)
                    : selected ? "Selected" : "Unguided";
                if (_tileabilityMode == TextureTileabilityMode.GenerateSeamless || _combination.generateSeamless)
                    state += $" - Seam {candidate.seamScore01:0.00}";
                GUI.Label(new Rect(rect.x + 8f, rect.yMax - 24f, rect.width - 16f, 18f), state, UtilityWindowTheme.MutedMiniLabelStyle);
            }

            HandleCandidateCardInput(index, rect, selectRect, lockRect, editRect, menuRect, influenceSliderRect);
        }

        private string ConstraintStatus(ProceduralTextureCandidate candidate)
        {
            if (candidate == null)
                return "No score";
            return CandidatePassesConstraints(candidate)
                ? $"Pass {candidate.constraintScore01:0.00}"
                : $"Review {candidate.constraintScore01:0.00}";
        }

        private void HandleCandidateCardInput(int index, Rect rect, params Rect[] controls)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || !rect.Contains(current.mousePosition) || IsPointInAnyRect(current.mousePosition, controls))
                return;

            if (current.button == 0)
            {
                if (current.clickCount == 2)
                {
                    if (_candidates[index].locked)
                    {
                        _lastStatus = "Guided textures are preserved. Remove Guide to regenerate this texture.";
                        Repaint();
                    }
                    else
                    {
                        SelectTextureCard(index, current.control || current.command || current.shift);
                        RegenerateVariant(index, "Regenerated texture.");
                    }
                    current.Use();
                    return;
                }

                if (_activeInspector == TextureInspectorTab.Refine)
                {
                    SetActiveCandidate(index, true);
                    _selectedCandidates.Clear();
                    _selectedCandidates.Add(index);
                    _candidates[index].selected = true;
                    if (current.control || current.command || current.shift)
                        _lastStatus = "Only one texture can be refined at a time.";
                }
                else
                {
                    SelectTextureCard(index, current.control || current.command || current.shift);
                    if (_activeInspector == TextureInspectorTab.Export)
                        SetActiveCandidate(index, false);
                }
                current.Use();
            }
        }

        private void SelectTextureCard(int index, bool additive)
        {
            if (!additive)
            {
                _selectedCandidates.Clear();
                for (int i = 0; i < _candidates.Count; i++)
                    _candidates[i].selected = false;
            }

            if (additive && _selectedCandidates.Contains(index))
            {
                _selectedCandidates.Remove(index);
                _candidates[index].selected = false;
            }
            else
            {
                _selectedCandidates.Add(index);
                _candidates[index].selected = true;
                SetActiveCandidate(index, false);
            }

            if (!additive && _textureEditOverlayOpen && _textureEditOverlayIndex != index)
                CloseTextureEditOverlay();
            Repaint();
        }

        private void ShowTextureCardMenu(int index)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("View"), false, () => AdoptCandidate(index));
            if (_candidates[index].EditSourceKind == ProceduralTextureEditSourceKind.Recipe)
                menu.AddItem(new GUIContent("Edit Recipe"), false, () => OpenCandidateInManualComposer(index, false));
            else
                menu.AddDisabledItem(new GUIContent("Edit Recipe"));
            menu.AddItem(new GUIContent("Add as Baked Layer"), false, () => AddCandidateAsBase(index));
            menu.AddItem(new GUIContent("Use as Guide"), false, () => UseCandidateAsGuide(index));
            menu.AddItem(new GUIContent("Export"), false, () => ExportCandidate(index));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Edit Parameters"), false, () => OpenTextureEditOverlay(index, Rect.zero));
            if (_candidates[index].locked)
                menu.AddDisabledItem(new GUIContent("Regenerate Guided Texture"));
            else
                menu.AddItem(new GUIContent("Regenerate"), false, () => RegenerateVariant(index, "Regenerated texture."));
            menu.AddItem(new GUIContent(_candidates[index].locked ? "Remove Grid Guide Marker" : "Mark as Guide on Generate"), false, () => ToggleVariantLock(index));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Blend Lab/Pick as A"), false, () => PickBlendSource(index, false));
            menu.AddItem(new GUIContent("Blend Lab/Pick as B"), false, () => PickBlendSource(index, true));
            if (_candidates[index].EditSourceKind == ProceduralTextureEditSourceKind.Recipe)
                menu.AddItem(new GUIContent("Manual Compose/Append Recipe Layers"), false, () => OpenCandidateInManualComposer(index, true));
            else
                menu.AddDisabledItem(new GUIContent("Manual Compose/Append Recipe Layers"));
            menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateVariant(index));
            menu.ShowAsContext();
        }

        private void OpenTextureEditOverlay(int index)
        {
            OpenTextureEditOverlay(index, Rect.zero);
        }

        private void OpenTextureEditOverlay(int index, Rect anchorRect)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            if (_selectedCandidates.Count != 1 || !_selectedCandidates.Contains(index))
                SelectTextureCard(index, false);

            _textureEditOverlayIndex = index;
            _textureEditOverlayOpen = true;
            _textureEditOverlayAnchorRect = anchorRect;
            _textureEditOverlayRect = Rect.zero;
            _textureEditOverlayScroll = Vector2.zero;
            _foldoutBlend = false;
            _foldoutPattern = false;
            _foldoutDensity = false;
            _foldoutStamp = false;
            _foldoutPatternDetails = false;
            _foldoutSource = false;
            Repaint();
        }

        private void CloseTextureEditOverlay()
        {
            _textureEditOverlayOpen = false;
            _textureEditOverlayIndex = -1;
            _textureEditOverlayAnchorRect = Rect.zero;
            _textureEditOverlayRect = Rect.zero;
        }

        private void HandleTextureEditOverlayInput()
        {
            if (!_textureEditOverlayOpen)
                return;

            CalculateTextureEditOverlayRect();
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                CloseTextureEditOverlay();
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown &&
                !_textureEditOverlayRect.Contains(evt.mousePosition) &&
                !_textureEditOverlayAnchorRect.Contains(evt.mousePosition))
            {
                CloseTextureEditOverlay();
                evt.Use();
            }
        }

        private Rect CalculateTextureEditOverlayRect()
        {
            float height = Mathf.Min(520f, Mathf.Max(300f, position.height - 150f));
            float width = Mathf.Clamp(position.width - 48f, 380f, 660f);
            float x = _textureEditOverlayAnchorRect.width > 0f
                ? _textureEditOverlayAnchorRect.center.x - width * 0.5f
                : (position.width - width) * 0.5f;
            x = Mathf.Clamp(x, 12f, Mathf.Max(12f, position.width - width - 12f));

            float y = _textureEditOverlayAnchorRect.height > 0f
                ? Mathf.Max(92f, _textureEditOverlayAnchorRect.yMax + 4f)
                : 92f;
            y = Mathf.Min(y, Mathf.Max(92f, position.height - height - 44f));

            _textureEditOverlayRect = new Rect(x, y, width, height);
            return _textureEditOverlayRect;
        }

        private static void DrawTextureOverlayChrome(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.26f));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.12f, 0.98f) : new Color(0.88f, 0.88f, 0.88f, 0.98f));
        }

        private void DrawTextureEditOverlay()
        {
            if (!_textureEditOverlayOpen || _textureEditOverlayIndex < 0 || _textureEditOverlayIndex >= _candidates.Count)
                return;

            ProceduralTextureCandidate variant = _candidates[_textureEditOverlayIndex];
            ProceduralTextureBaseSettings editableBase = FirstEditableBase(variant);
            if (variant == null || editableBase == null)
                return;

            Rect overlay = CalculateTextureEditOverlayRect();
            if (Event.current.type == EventType.Repaint)
                DrawTextureOverlayChrome(overlay);

            GUILayout.BeginArea(overlay, EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{variant.label} Parameters", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Close", "Close parameter overlay."), EditorStyles.miniButton, GUILayout.Width(52f)))
                    CloseTextureEditOverlay();
            }

            _textureEditOverlayScroll = EditorGUILayout.BeginScrollView(_textureEditOverlayScroll);
            EditorGUI.BeginChangeCheck();
            DrawTextureOverlayControls(editableBase);
            if (EditorGUI.EndChangeCheck())
            {
                editableBase.Clamp();
                RegenerateVariant(_textureEditOverlayIndex, "Updated texture parameters.");
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Done", GUILayout.Width(82f)))
                    CloseTextureEditOverlay();
            }
            GUILayout.EndArea();
        }

        private void DrawTextureOverlayControls(ProceduralTextureBaseSettings textureBase)
        {
            if (textureBase.locks == null)
                textureBase.locks = new ProceduralTextureBaseLockSettings();

            _foldoutBlend = DrawTextureOverlayFoldout("Blend", ref textureBase.locks.blend, _foldoutBlend);
            if (_foldoutBlend)
                DrawOverlayBlendControls(textureBase);

            bool patternSectionLocked = textureBase.locks.pattern && textureBase.locks.seed;
            bool nextPatternSectionLocked = patternSectionLocked;
            _foldoutPattern = DrawTextureOverlayFoldout("Placement", ref nextPatternSectionLocked, _foldoutPattern);
            if (nextPatternSectionLocked != patternSectionLocked)
            {
                textureBase.locks.pattern = nextPatternSectionLocked;
                textureBase.locks.seed = nextPatternSectionLocked;
            }
            if (_foldoutPattern)
                DrawOverlayPatternControls(textureBase);

            _foldoutDensity = DrawTextureOverlayFoldout("Amount / Spacing", ref textureBase.locks.density, _foldoutDensity);
            if (_foldoutDensity)
                DrawOverlayDensityControls(textureBase);

            _foldoutStamp = DrawTextureOverlayFoldout("Shape", ref textureBase.locks.stamp, _foldoutStamp);
            if (_foldoutStamp)
                DrawOverlayStampControls(textureBase);

            if (HasPlacementDetails(textureBase.generation.pattern))
            {
                _foldoutPatternDetails = DrawTextureOverlayFoldout("Placement Details", ref textureBase.locks.patternSpecific, _foldoutPatternDetails);
                if (_foldoutPatternDetails)
                {
                    using (new EditorGUI.DisabledScope(textureBase.locks.patternSpecific))
                        DrawPatternSpecificControls(textureBase.generation);
                }
            }

            _foldoutSource = DrawTextureOverlayFoldout("Texture Inputs", ref textureBase.locks.source, _foldoutSource);
            if (_foldoutSource)
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.source))
                {
                    DrawStampSpecificControls(textureBase.generation);
                    if (textureBase.useBakedValues)
                        DrawInlineStatus($"Texture layer {textureBase.bakedWidth}x{textureBase.bakedHeight}.", UtilityWindowTheme.Green);
                }
            }
        }

        private bool DrawTextureOverlayFoldout(string label, ref bool locked, bool expanded)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool nextExpanded = EditorGUILayout.Foldout(expanded, label, true);
                GUILayout.FlexibleSpace();
                locked = EditorGUILayout.Toggle(new GUIContent("", $"Lock all {label.ToLowerInvariant()} controls."), locked, GUILayout.Width(18f));
                return nextExpanded;
            }
        }

        private void DrawOverlayBlendControls(ProceduralTextureBaseSettings textureBase)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Name", GUILayout.Width(74f));
                using (new EditorGUI.DisabledScope(textureBase.locks.blend))
                    textureBase.name = EditorGUILayout.TextField(textureBase.name);
                textureBase.locks.blend = EditorGUILayout.Toggle(textureBase.locks.blend, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Mode", GUILayout.Width(74f));
                using (new EditorGUI.DisabledScope(textureBase.locks.blend))
                    textureBase.blendMode = DrawBlendModePopup(EditorGUILayout.GetControlRect(), GUIContent.none, textureBase.blendMode);
                textureBase.locks.blend = EditorGUILayout.Toggle(textureBase.locks.blend, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Opacity", GUILayout.Width(74f));
                using (new EditorGUI.DisabledScope(textureBase.locks.blend))
                    textureBase.weight = EditorGUILayout.Slider(textureBase.weight, 0f, 1f);
                textureBase.locks.blend = EditorGUILayout.Toggle(textureBase.locks.blend, GUILayout.Width(18f));
            }
        }

        private void DrawOverlayPatternControls(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureGenerationSettings settings = textureBase.generation;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Placement", GUILayout.Width(74f));
                using (new EditorGUI.DisabledScope(textureBase.locks.pattern))
                    settings.pattern = (ProceduralTexturePattern)EditorGUILayout.EnumPopup(settings.pattern);
                textureBase.locks.pattern = EditorGUILayout.Toggle(textureBase.locks.pattern, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Seed", GUILayout.Width(74f));
                using (new EditorGUI.DisabledScope(textureBase.locks.seed))
                    settings.seed = EditorGUILayout.IntField(settings.seed);
                textureBase.locks.seed = EditorGUILayout.Toggle(textureBase.locks.seed, GUILayout.Width(18f));
            }
        }

        private void DrawOverlayDensityControls(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureGenerationSettings settings = textureBase.generation;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.density))
                    settings.count = EditorGUILayout.IntSlider("Count", settings.count, 1, 512);
                textureBase.locks.density = EditorGUILayout.Toggle(textureBase.locks.density, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.density))
                    settings.minSpacing01 = EditorGUILayout.Slider("Spacing", settings.minSpacing01, 0f, 0.5f);
                textureBase.locks.density = EditorGUILayout.Toggle(textureBase.locks.density, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.density))
                    settings.globalJitter01 = EditorGUILayout.Slider("Jitter", settings.globalJitter01, 0f, 1f);
                textureBase.locks.density = EditorGUILayout.Toggle(textureBase.locks.density, GUILayout.Width(18f));
            }
        }

        private void DrawOverlayStampControls(ProceduralTextureBaseSettings textureBase)
        {
            ProceduralTextureGenerationSettings settings = textureBase.generation;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.stamp))
                    settings.stampShape = (ProceduralTextureStampShape)EditorGUILayout.EnumPopup("Shape", settings.stampShape);
                textureBase.locks.stamp = EditorGUILayout.Toggle(textureBase.locks.stamp, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.stamp))
                    DrawRange("Radius", ref settings.radiusMin01, ref settings.radiusMax01, 0.001f, 0.5f);
                textureBase.locks.stamp = EditorGUILayout.Toggle(textureBase.locks.stamp, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.stamp))
                    DrawRange("Intensity", ref settings.intensityMin, ref settings.intensityMax, 0f, 2f);
                textureBase.locks.stamp = EditorGUILayout.Toggle(textureBase.locks.stamp, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.stamp))
                    settings.contrast = EditorGUILayout.Slider("Contrast", settings.contrast, 0.1f, 8f);
                textureBase.locks.stamp = EditorGUILayout.Toggle(textureBase.locks.stamp, GUILayout.Width(18f));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(textureBase.locks.stamp))
                    settings.edgeFalloff = EditorGUILayout.Slider("Edge", settings.edgeFalloff, 0f, 1f);
                textureBase.locks.stamp = EditorGUILayout.Toggle(textureBase.locks.stamp, GUILayout.Width(18f));
            }
        }

        private void DrawRefineWorkspace(float width)
        {
            if (!IsValidActiveCandidate() && _candidates.Count > 0)
                SetActiveCandidate(0, false);

            if (!IsValidActiveCandidate())
            {
                Rect viewport = GUILayoutUtility.GetRect(240f, 280f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawCandidateEmptyState(viewport);
                return;
            }

            ProceduralTextureCandidate active = _candidates[_activeCandidateIndex];
            Rect previewRect = GUILayoutUtility.GetRect(240f, Mathf.Max(260f, position.height * 0.52f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawCheckerBackground(previewRect);
            if (active.preview != null)
            {
                if (_combination.tilePreview)
                    DrawTiledTexture(previewRect, active.preview, 2);
                else
                {
                    Rect fitted = FitRect(previewRect, active.preview.width, active.preview.height);
                    GUI.DrawTexture(fitted, active.preview, ScaleMode.ScaleToFit, true);
                    DrawSeamOverlay(fitted, active.seamHeatmap);
                }
            }
            if (_combination.tilePreview)
            {
                DrawSeamGuide(previewRect);
                if (_seamOverlayMode == TextureSeamOverlayMode.Heatmap && active.seamHeatmap != null)
                    GUI.DrawTexture(previewRect, active.seamHeatmap, ScaleMode.ScaleToFit, true);
            }
            DrawPreviewBorder(previewRect);

            DrawRefineCandidateStrip();
        }

        private void DrawRefineCandidateStrip()
        {
            if (_candidates.Count == 0)
                return;

            Rect viewport = GUILayoutUtility.GetRect(80f, 108f, GUILayout.ExpandWidth(true), GUILayout.Height(108f));
            float tileWidth = 118f;
            float contentWidth = _candidates.Count * tileWidth + Mathf.Max(0, _candidates.Count - 1) * BaseGap;
            Rect view = new Rect(0f, 0f, Mathf.Max(viewport.width, contentWidth), 92f);
            _candidateScroll = GUI.BeginScrollView(viewport, _candidateScroll, view, contentWidth > viewport.width, false);
            for (int i = 0; i < _candidates.Count; i++)
            {
                Rect rect = new Rect(i * (tileWidth + BaseGap), 0f, tileWidth, 86f);
                DrawRefineStripCandidate(i, rect);
            }
            GUI.EndScrollView();
        }

        private void DrawRefineStripCandidate(int index, Rect rect)
        {
            ProceduralTextureCandidate candidate = _candidates[index];
            bool active = index == _activeCandidateIndex;
            Color tint = active ? UtilityWindowTheme.Blue : UtilityWindowTheme.Neutral;
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(tint, active ? 0.13f : 0.055f), PanelBorder(tint, active ? 0.82f : 0.24f));

            Rect image = new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 24f);
            DrawCheckerBackground(image);
            if (candidate.preview != null)
                GUI.DrawTexture(FitRect(image, candidate.preview.width, candidate.preview.height), candidate.preview, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 19f, rect.width - 12f, 16f), candidate.label, UtilityWindowTheme.MutedMiniLabelStyle);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                SetActiveCandidate(index, true);
                Event.current.Use();
            }
        }
    }
#endif
}
