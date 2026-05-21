using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed partial class ProceduralTextureLabWindow
    {
        private void DrawBlendLabWorkspace(float width)
        {
            using (BeginInspectorSection("Blend Bench", UtilityWindowTheme.Purple, BlendSourceLabel(), TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                DrawBlendLabToolbar();
                Rect braid = GUILayoutUtility.GetRect(260f, 210f, GUILayout.ExpandWidth(true), GUILayout.Height(Mathf.Min(260f, Mathf.Max(190f, position.height * 0.28f))));
                DrawBlendBraid(braid);
                Rect viewport = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(160f));
                if (_candidates.Count == 0)
                    DrawCandidateEmptyState(viewport);
                else
                    DrawComposeCandidateGrid(viewport);
                DrawTextureEditOverlay();
            }
        }

        private void DrawBlendLabInspector()
        {
            using (BeginInspectorSection("Blend Lab", UtilityWindowTheme.Purple, BlendSourceLabel(), TextureButtonTone.Secondary))
            {
                DrawInlineStatus("Pick two textures, decide which traits come from B, then generate blend candidates.", UtilityWindowTheme.Neutral);
                DrawBlendSourcePicker("Texture A", false);
                DrawBlendSourcePicker("Texture B", true);
                EditorGUI.BeginChangeCheck();
                _blendLab.structureFromB01 = EditorGUILayout.Slider("Structure from B", _blendLab.structureFromB01, 0f, 1f);
                _blendLab.densityFromB01 = EditorGUILayout.Slider("Density from B", _blendLab.densityFromB01, 0f, 1f);
                _blendLab.scaleFromB01 = EditorGUILayout.Slider("Scale from B", _blendLab.scaleFromB01, 0f, 1f);
                _blendLab.detailFromB01 = EditorGUILayout.Slider("Detail from B", _blendLab.detailFromB01, 0f, 1f);
                _blendLab.toneFromB01 = EditorGUILayout.Slider("Tone from B", _blendLab.toneFromB01, 0f, 1f);
                _blendLab.colourFromB01 = EditorGUILayout.Slider("Colour from B", _blendLab.colourFromB01, 0f, 1f);
                _blendLab.blendOrderFromB01 = EditorGUILayout.Slider("Blend Order from B", _blendLab.blendOrderFromB01, 0f, 1f);
                _blendLab.seamFromB01 = EditorGUILayout.Slider("Seam from B", _blendLab.seamFromB01, 0f, 1f);
                _blendLab.randomDetail01 = EditorGUILayout.Slider("Random Detail", _blendLab.randomDetail01, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    _blendLab.Clamp();
                    RequestSessionSave();
                    RebuildBlendOutputPreview();
                    Repaint();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!HasBlendSources()))
                    {
                        if (StudioButton("Generate Blends", UtilityWindowTheme.Purple, TextureButtonTone.Primary, GUILayout.Height(24f)))
                            GenerateBlendCandidates();
                    }
                    if (StudioButton("Use Selected A/B", UtilityWindowTheme.Neutral, TextureButtonTone.Ghost, GUILayout.Height(24f)))
                        PickBlendSourcesFromSelection();
                }
            }
        }

        private void DrawBlendLabToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill($"A {BlendSourceName(false)}", UtilityWindowTheme.Blue, 128f);
                UtilityWindowTheme.CountPill($"B {BlendSourceName(true)}", UtilityWindowTheme.Green, 128f);
                UtilityWindowTheme.CountPill($"Detail {_blendLab.randomDetail01:0.00}", UtilityWindowTheme.Cyan, 92f);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!HasBlendSources()))
                    if (StudioButton("Generate Blends", UtilityWindowTheme.Purple, TextureButtonTone.Primary, GUILayout.Width(128f), GUILayout.Height(24f)))
                        GenerateBlendCandidates();
            }
        }

        private void DrawBlendBraid(Rect rect)
        {
            ProceduralTextureCandidate a = ResolveBlendSource(false);
            ProceduralTextureCandidate b = ResolveBlendSource(true);
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Purple, 0.06f), PanelBorder(UtilityWindowTheme.Purple, 0.26f));

            float side = Mathf.Min(110f, rect.height - 28f);
            Rect aRect = new Rect(rect.x + 14f, rect.y + 18f, side, side);
            Rect bRect = new Rect(rect.xMax - side - 14f, rect.y + 18f, side, side);
            Rect outRect = new Rect(rect.center.x - side * 0.5f, rect.y + 18f, side, side);
            DrawCandidateMiniPreview(aRect, a, "A");
            DrawCandidateMiniPreview(bRect, b, "B");
            if (_blendOutputPreview == null && HasBlendSources())
                RebuildBlendOutputPreview();
            DrawTextureMiniPreview(outRect, _blendOutputPreview, "Blend");

            float laneY = rect.y + side + 34f;
            float laneHeight = 15f;
            DrawBlendLane(new Rect(rect.x + 14f, laneY, rect.width - 28f, laneHeight), "Structure", _blendLab.structureFromB01, a, b);
            DrawBlendLane(new Rect(rect.x + 14f, laneY + 19f, rect.width - 28f, laneHeight), "Tone", _blendLab.toneFromB01, a, b);
            DrawBlendLane(new Rect(rect.x + 14f, laneY + 38f, rect.width - 28f, laneHeight), "Detail", _blendLab.detailFromB01, a, b);
        }

        private void DrawBlendLane(Rect rect, string label, float fromB, ProceduralTextureCandidate a, ProceduralTextureCandidate b)
        {
            GUI.Label(new Rect(rect.x, rect.y - 2f, 68f, rect.height), label, UtilityWindowTheme.MutedMiniLabelStyle);
            Rect lane = new Rect(rect.x + 72f, rect.y, rect.width - 72f, rect.height);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(lane, PanelFill(UtilityWindowTheme.Neutral, 0.12f));
            Rect aSegment = new Rect(lane.x, lane.y, lane.width * (1f - Mathf.Clamp01(fromB)), lane.height);
            Rect bSegment = new Rect(aSegment.xMax, lane.y, lane.width - aSegment.width, lane.height);
            if (a?.preview != null)
                GUI.DrawTexture(aSegment, a.preview, ScaleMode.ScaleAndCrop, true);
            if (b?.preview != null)
                GUI.DrawTexture(bSegment, b.preview, ScaleMode.ScaleAndCrop, true);
            DrawStudioBox(lane, Color.clear, PanelBorder(UtilityWindowTheme.Purple, 0.34f));
        }

        private void DrawReferenceMatchWorkspace(float width)
        {
            using (BeginInspectorSection("Reference Match", UtilityWindowTheme.Blue, _referenceTexture != null ? _referenceTexture.name : "no reference", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                DrawReferenceMatchToolbar();
                Rect diagram = GUILayoutUtility.GetRect(260f, 250f, GUILayout.ExpandWidth(true), GUILayout.Height(Mathf.Min(300f, Mathf.Max(220f, position.height * 0.34f))));
                DrawReferenceDecomposition(diagram);
                Rect viewport = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(160f));
                if (_candidates.Count == 0)
                    DrawCandidateEmptyState(viewport);
                else
                    DrawComposeCandidateGrid(viewport);
                DrawTextureEditOverlay();
            }
        }

        private void DrawReferenceMatchInspector()
        {
            using (BeginInspectorSection("Reference Match", UtilityWindowTheme.Blue, _referenceTexture != null ? "ready" : "missing", TextureButtonTone.Secondary))
            {
                EditorGUI.BeginChangeCheck();
                _referenceTexture = (Texture2D)EditorGUILayout.ObjectField("Reference", _referenceTexture, typeof(Texture2D), false);
                _referenceMatch.referenceStrength01 = EditorGUILayout.Slider("Strength", _referenceMatch.referenceStrength01, 0f, 1f);
                _referenceMatch.matchStructure = EditorGUILayout.ToggleLeft("Match Structure", _referenceMatch.matchStructure);
                _referenceMatch.matchDensity = EditorGUILayout.ToggleLeft("Match Density", _referenceMatch.matchDensity);
                _referenceMatch.matchTone = EditorGUILayout.ToggleLeft("Match Tone", _referenceMatch.matchTone);
                _referenceMatch.matchColour = EditorGUILayout.ToggleLeft("Match Colour / Palette", _referenceMatch.matchColour);
                _referenceMatch.matchSeam = EditorGUILayout.ToggleLeft("Match Seam", _referenceMatch.matchSeam);
                _referenceMatch.useAsStampSource = EditorGUILayout.ToggleLeft("Use as Stamp Source", _referenceMatch.useAsStampSource);
                if (EditorGUI.EndChangeCheck())
                {
                    _referenceMatch.referenceAssetPath = _referenceTexture != null ? AssetDatabase.GetAssetPath(_referenceTexture) : string.Empty;
                    _referenceMatch.Clamp();
                    QueueReferenceFeaturePreviewRebuild();
                    RequestSessionSave();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_referenceTexture == null))
                    {
                        if (StudioButton("Extract", UtilityWindowTheme.Blue, TextureButtonTone.Secondary, GUILayout.Height(24f)))
                            QueueReferenceFeaturePreviewRebuild();
                        if (StudioButton("Generate Matches", UtilityWindowTheme.Blue, TextureButtonTone.Primary, GUILayout.Height(24f)))
                            GenerateReferenceMatches();
                    }
                }
                DrawInlineStatus(_referenceTexture == null ? "Assign a texture reference. Extraction is preview-only until Generate Matches." : "Reference extraction is cached and non-destructive.", _referenceTexture == null ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral);
            }
        }

        private void DrawReferenceMatchToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _referenceTexture = (Texture2D)EditorGUILayout.ObjectField(_referenceTexture, typeof(Texture2D), false, GUILayout.Width(220f));
                if (GUILayout.Button("Extract", EditorStyles.miniButton, GUILayout.Width(72f)))
                    QueueReferenceFeaturePreviewRebuild();
                using (new EditorGUI.DisabledScope(_referenceTexture == null))
                    if (StudioButton("Generate Matches", UtilityWindowTheme.Blue, TextureButtonTone.Primary, GUILayout.Width(132f), GUILayout.Height(22f)))
                        GenerateReferenceMatches();
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill($"Strength {_referenceMatch.referenceStrength01:0.00}", UtilityWindowTheme.Blue, 110f);
            }
        }

        private void DrawReferenceDecomposition(Rect rect)
        {
            if (_referenceFeaturePreviewDirty)
                QueueReferenceFeaturePreviewRebuild();
            if (Event.current.type == EventType.Repaint)
                DrawStudioBox(rect, PanelFill(UtilityWindowTheme.Blue, 0.06f), PanelBorder(UtilityWindowTheme.Blue, 0.24f));
            if (_referenceTexture == null)
            {
                DrawInlineCenteredMessage(rect, "Assign a reference texture to inspect extracted traits.");
                return;
            }
            float sourceSize = Mathf.Min(118f, rect.height - 28f);
            DrawTextureMiniPreview(new Rect(rect.x + 14f, rect.y + 16f, sourceSize, sourceSize), _referenceSourcePreview != null ? _referenceSourcePreview : _referenceTexture, "Reference");
            float x = rect.x + sourceSize + 32f;
            float tile = Mathf.Min(72f, (rect.width - sourceSize - 70f) / 3f);
            DrawFeatureTile(new Rect(x, rect.y + 18f, tile, tile), "Structure", _referenceFeaturePreview?.structureMap, _referenceMatch.matchStructure);
            DrawFeatureTile(new Rect(x + tile + 10f, rect.y + 18f, tile, tile), "Density", _referenceFeaturePreview?.densityMask, _referenceMatch.matchDensity);
            DrawFeatureTile(new Rect(x + (tile + 10f) * 2f, rect.y + 18f, tile, tile), "Tone", _referenceFeaturePreview?.toneHistogram, _referenceMatch.matchTone);
            DrawFeatureTile(new Rect(x, rect.y + tile + 46f, tile, tile), "Colour", _referenceFeaturePreview?.colourRamp, _referenceMatch.matchColour);
            DrawFeatureTile(new Rect(x + tile + 10f, rect.y + tile + 46f, tile, tile), "Seam", _referenceFeaturePreview?.seamEdgeStrip, _referenceMatch.matchSeam);
            DrawFeatureTile(new Rect(x + (tile + 10f) * 2f, rect.y + tile + 46f, tile, tile), "Stamp", _referenceFeaturePreview?.stampShapePreview, _referenceMatch.useAsStampSource);
        }

        private void DrawPresetBrowserWorkspace(float width)
        {
            EnsureTexturePresets();
            using (BeginInspectorSection("Preset Browser", UtilityWindowTheme.Green, $"{VisiblePresetIndices().Count} shown", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                DrawPresetToolbar();
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
                {
                    Rect grid = GUILayoutUtility.GetRect(width * 0.58f, 260f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(220f));
                    DrawPresetGrid(grid);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(width * 0.36f, 250f, 420f)), GUILayout.ExpandHeight(true)))
                        DrawPresetRecipeStack();
                }
            }
        }

        private void DrawPresetBrowserInspector()
        {
            using (BeginInspectorSection("Preset Browser", UtilityWindowTheme.Green, $"{VisiblePresetIndices().Count} shown", TextureButtonTone.Secondary))
            {
                _presetSearch = EditorGUILayout.TextField("Search", _presetSearch);
                _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, 0, Mathf.Max(0, _texturePresets.Count - 1));
                DrawPresetActionRow(_selectedPresetIndex);
                DrawInlineStatus("Presets are editable recipe starters. Use them, guide with them, or add their layers to Manual Compose.", UtilityWindowTheme.Neutral);
            }
        }

        private void DrawPresetToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _presetSearch = EditorGUILayout.TextField(_presetSearch, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.MinWidth(140f));
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(52f)))
                    _presetSearch = string.Empty;
                GUILayout.FlexibleSpace();
                DrawPresetActionRow(_selectedPresetIndex);
            }
        }

        private void DrawMaterialMapPrepWorkspace(float width)
        {
            using (BeginInspectorSection("Material Map Prep", ExportTint(), _mapIntent.ToString(), TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                DrawMaterialMapPrepToolbar();
                Rect preview = GUILayoutUtility.GetRect(240f, 280f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(220f));
                ProceduralTextureCandidate active = IsValidActiveCandidate() ? _candidates[_activeCandidateIndex] : null;
                DrawMapPrepPreview(preview, active);
                DrawMapPreviewStrip();
            }
        }

        private void DrawMaterialMapPrepInspector()
        {
            using (BeginInspectorSection("Map Prep", ExportTint(), _mapIntent.ToString(), TextureButtonTone.Secondary))
            {
                EditorGUI.BeginChangeCheck();
                ProceduralTextureMapIntent nextIntent = (ProceduralTextureMapIntent)EditorGUILayout.EnumPopup("Intent", _mapIntent);
                if (EditorGUI.EndChangeCheck())
                    ApplyMapIntent(nextIntent);
                DrawMapToggle(ref _export.diffuse, "Diffuse / Albedo");
                DrawMapToggle(ref _export.height, "Height");
                DrawMapToggle(ref _export.normal, "Normal");
                DrawMapToggle(ref _export.roughness, "Roughness");
                DrawMapToggle(ref _export.smoothness, "Smoothness");
                DrawMapToggle(ref _export.metallic, "Metallic");
                DrawMapToggle(ref _export.alpha, "Alpha Mask");
                _export.normalStrength = EditorGUILayout.Slider("Normal Strength", _export.normalStrength, 0.1f, 8f);
                DrawRange("Alpha Range", ref _export.alphaMin, ref _export.alphaMax, 0f, 1f);
                if (StudioButton("Refresh Map Previews", ExportTint(), TextureButtonTone.Secondary, GUILayout.Height(24f)))
                    QueueMapPreviewRebuild();
                using (new EditorGUI.DisabledScope(SelectedExportTargets().Count == 0 && _previewValues == null))
                    if (StudioButton("Export Selected Maps", ExportTint(), TextureButtonTone.Primary, GUILayout.Height(28f)))
                        ExportMaps();
                DrawInlineStatus(_assetProductionBridgeStatus, UtilityWindowTheme.Neutral);
            }
        }

        private void DrawMaterialMapPrepToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                ProceduralTextureMapIntent nextIntent = (ProceduralTextureMapIntent)EditorGUILayout.EnumPopup("Intent", _mapIntent, GUILayout.Width(240f));
                if (EditorGUI.EndChangeCheck())
                    ApplyMapIntent(nextIntent);
                UtilityWindowTheme.CountPill($"{SelectedExportTargets().Count} selected", SelectedExportTargets().Count > 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 96f);
                GUILayout.FlexibleSpace();
                if (StudioButton("Export", ExportTint(), TextureButtonTone.Primary, GUILayout.Width(86f), GUILayout.Height(24f)))
                    ExportMaps();
            }
        }

        private void DrawHistoryLineageWorkspace(float width)
        {
            using (BeginInspectorSection("Lineage", HistoryTint(), $"{_combinationHistory.Count + _baseHistory.Count}", TextureButtonTone.Secondary, GUILayout.ExpandHeight(true)))
            {
                var snapshots = CombinedSnapshots();
                if (snapshots.Count == 0)
                {
                    Rect empty = GUILayoutUtility.GetRect(240f, 260f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawInlineCenteredMessage(empty, "Generate, guide, export, or save snapshots to build lineage.");
                    return;
                }
                _historyWorkspaceScroll = EditorGUILayout.BeginScrollView(_historyWorkspaceScroll, GUILayout.ExpandHeight(true));
                DrawLineageGraph(snapshots);
                DrawSnapshotGrid(snapshots);
                EditorGUILayout.EndScrollView();
            }
        }

    }
#endif
}
