using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(PungentModularSpatialOutput))]
    public sealed class PungentModularSpatialOutputEditor : Editor
    {
        private enum RecipeInspectorTab { Source, Placement, SurfaceGaps, Preview, Diagnostics }

        private PungentModularSpatialOutput _output;
        private RecipeInspectorTab _selectedTab = RecipeInspectorTab.Source;
        private bool _sourceFoldout = true;
        private bool _prefabsFoldout = true;
        private bool _placementFoldout = true;
        private bool _surfaceFoldout = false;
        private bool _gapsFoldout = false;
        private bool _previewFoldout = false;
        private bool _diagnosticsFoldout = true;
        private string _status = string.Empty;

        private void OnEnable()
        {
            _output = target as PungentModularSpatialOutput;
        }

        private void OnSceneGUI()
        {
            if (_output == null)
                _output = target as PungentModularSpatialOutput;
            if (_output == null)
                return;

            PungentSpatialOutputRecipeEditorUtility.SelectRecipe(_output);
            PungentSpatialOutputRecipeEditorUtility.DrawScenePreview(_output, selected: true);
        }

        public override void OnInspectorGUI()
        {
            if (_output == null)
                _output = target as PungentModularSpatialOutput;
            if (_output == null)
                return;

            serializedObject.Update();
            DrawRecipeHeader();
            DrawInspectorTabs();

            EditorGUI.BeginChangeCheck();
            switch (_selectedTab)
            {
                case RecipeInspectorTab.Source:
                    DrawSourceSection();
                    break;
                case RecipeInspectorTab.Placement:
                    DrawPrefabsSection();
                    DrawPlacementSection();
                    break;
                case RecipeInspectorTab.SurfaceGaps:
                    DrawSurfaceSection();
                    DrawGapsSection();
                    break;
                case RecipeInspectorTab.Preview:
                    DrawPreviewSection();
                    break;
                case RecipeInspectorTab.Diagnostics:
                    DrawDiagnosticsSection();
                    break;
            }
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed)
                ApplyRecipeChanges();
        }

        private void DrawInspectorTabs()
        {
            EditorGUILayout.Space(3f);
            string[] labels = { "Source", "Placement", "Surface / Gaps", "Preview", "Diagnostics" };
            _selectedTab = (RecipeInspectorTab)GUILayout.Toolbar((int)_selectedTab, labels, GUILayout.Height(26f));
            EditorGUILayout.Space(3f);
        }

        private void DrawRecipeHeader()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                string sourceName = _output.source != null ? _output.source.name : "No source";
                UtilityWindowTheme.SectionTitle("Spatial Output Recipe", UtilityWindowTheme.Green, sourceName + " / " + _output.sourceMode);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UtilityWindowTheme.TintedButton("Apply Recipe", UtilityWindowTheme.Green, GUILayout.Height(28f)))
                    {
                        PungentSpatialOutputRecipeEditorUtility.ApplyRecipe(_output);
                    }

                    using (new EditorGUI.DisabledScope(_output.ExistingGeneratedChildCount == 0))
                    {
                        if (GUILayout.Button("Clear", GUILayout.Height(28f), GUILayout.Width(76f)))
                        {
                            PungentSpatialOutputRecipeEditorUtility.ClearRecipe(_output);
                        }

                        if (GUILayout.Button("Select Generated", GUILayout.Height(28f), GUILayout.Width(126f)))
                        {
                            Transform root = _output.ExistingGeneratedRoot;
                            if (root != null)
                                Selection.activeGameObject = root.gameObject;
                        }
                    }

                    if (GUILayout.Button("Frame Source", GUILayout.Height(28f), GUILayout.Width(104f)))
                        FrameSource();
                    using (new EditorGUI.DisabledScope(_output.ExistingGeneratedRoot == null))
                    {
                        if (GUILayout.Button("Frame Generated", GUILayout.Height(28f), GUILayout.Width(122f)))
                            FrameGenerated();
                    }
                }
            }
        }

        private void DrawSourceSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                _sourceFoldout = EditorGUILayout.Foldout(_sourceFoldout, "Source", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_sourceFoldout)
                    return;

                DrawProperty("source", "Source", "Path or area component used as this recipe source.");
                DrawProperty("sourceMode", "Mode", "Centerline, corridor side, both sides, or area boundary.");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Source", GUILayout.Height(24f)))
                    {
                        if (_output.source != null)
                            Selection.activeGameObject = _output.source.gameObject;
                    }
                    if (GUILayout.Button("Open Toolkit", GUILayout.Height(24f), GUILayout.Width(104f)))
                    {
                        if (_output.source is ModularPathSpawner path)
                            PungentPathAuthoringToolkitWindow.OpenWithPath(path);
                        else if (_output.source is PungentAreaAuthoringShape area)
                            PungentPathAuthoringToolkitWindow.OpenWithArea(area);
                        else
                            PungentPathAuthoringToolkitWindow.Open();
                    }
                    if (GUILayout.Button("Validate Lane", GUILayout.Height(24f), GUILayout.Width(104f)))
                        _status = PungentSpatialOutputRecipeEditorUtility.ValidateRecipe(_output);
                }
            }
        }

        private void DrawPrefabsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                _prefabsFoldout = EditorGUILayout.Foldout(_prefabsFoldout, "Prefabs", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_prefabsFoldout)
                    return;

                DrawProperty("segmentPrefab", "Segment Prefab", "Prefab repeated along the generated lane.");
                DrawProperty("pointPrefab", "Point Prefab", "Optional prefab placed at source points.");
            }
        }

        private void DrawPlacementSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Green)))
            {
                _placementFoldout = EditorGUILayout.Foldout(_placementFoldout, "Placement", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_placementFoldout)
                    return;

                DrawProperty("useSockets", "Use Sockets", "Use named child transforms to align segment joins.");
                DrawProperty("startSocketName", "Start Socket", "Child transform name used as the segment start socket.");
                DrawProperty("endSocketName", "End Socket", "Child transform name used as the segment end socket.");
                DrawProperty("placeByEndpointsWhenUsingSockets", "Place By Endpoints", "Map sockets to sampled endpoints.");
                DrawProperty("usePrefabLength", "Use Prefab Length", "Use prefab bounds to decide spacing.");
                DrawProperty("prefabLengthAxis", "Length Axis", "Local prefab axis treated as the length direction.");
                DrawProperty(_output.usePrefabLength ? "manualSegmentLength" : "fixedSpacing", _output.usePrefabLength ? "Manual Length Fallback" : "Fixed Spacing", "Segment placement spacing.");
                DrawProperty("startOffset", "Start Offset", "Distance offset before first placement.");
            }
        }

        private void DrawSurfaceSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple)))
            {
                _surfaceFoldout = EditorGUILayout.Foldout(_surfaceFoldout, "Surface", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_surfaceFoldout)
                    return;

                DrawProperty("conformToSurface", "Surface Conform", "Raycast generated output to the surface mask.");
                DrawProperty("surfaceMask", "Surface Mask", "Layer mask used for surface sampling.");
                DrawProperty("raycastStartHeight", "Raycast Height", "Height above each point used when raycasting downward.");
                DrawProperty("yOffset", "Y Offset", "Vertical offset applied after surface sampling.");
                DrawProperty("alignToSurfaceNormal", "Align To Normal", "Use sampled surface normal as generated-object up.");
                DrawProperty("sampleSurfaceAtSegmentEnds", "Sample Segment Ends", "Sample both endpoints before placement.");
            }
        }

        private void DrawGapsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _gapsFoldout = EditorGUILayout.Foldout(_gapsFoldout, "Gaps", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_gapsFoldout)
                    return;

                DrawProperty("autoGapSelfIntersections", "Auto Gap Self Intersections", "Skip sections where this recipe lane crosses itself.");
                DrawProperty("autoGapCorridorOverlaps", "Auto Gap Corridor Overlaps", "Skip and bridge corridor overlap windows.");
                DrawProperty("autoGapPaddingNormalized", "Auto Gap Padding", "Normalized padding around detected gap windows.");
                DrawProperty("manualGaps", "Manual Gaps", "Normalized spans to skip during generation.");
            }
        }

        private void DrawPreviewSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral)))
            {
                _previewFoldout = EditorGUILayout.Foldout(_previewFoldout, "Preview & Rebuild", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_previewFoldout)
                    return;

                DrawProperty("previewWhileEditing", "Preview While Editing", "Draw lightweight recipe preview.");
                DrawProperty("drawGizmos", "Draw Gizmos", "Draw recipe preview gizmos.");
                DrawProperty("drawUnselectedGizmo", "Draw Unselected", "Draw lightweight recipe gizmos while unselected.");
                DrawProperty("autoRebuildInEditor", "Auto Rebuild", "Rebuild immediately after recipe changes.");
                if (_output.autoRebuildInEditor)
                    DrawProperty("editorRebuildDebounce", "Rebuild Debounce", "Reserved debounce value for recipe scheduling.");
            }
        }

        private void DrawDiagnosticsSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber)))
            {
                _diagnosticsFoldout = EditorGUILayout.Foldout(_diagnosticsFoldout, "Diagnostics", true, UtilityWindowTheme.SectionHeaderStyle);
                if (!_diagnosticsFoldout)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill("Generated " + _output.ExistingGeneratedChildCount, _output.ExistingGeneratedChildCount >= ModularPathSpawner.HighGeneratedObjectWarningThreshold ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 118f);
                    UtilityWindowTheme.CountPill(_output.source != null ? "Source OK" : "No Source", _output.source != null ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 98f);
                    UtilityWindowTheme.CountPill("Gaps " + (_output.manualGaps != null ? _output.manualGaps.Count : 0), UtilityWindowTheme.Neutral, 84f);
                    GUILayout.FlexibleSpace();
                }

                if (_output.segmentPrefab == null && _output.pointPrefab == null)
                    EditorGUILayout.HelpBox("Assign a segment prefab, point prefab, or both before applying this recipe.", MessageType.Info);
                if (!string.IsNullOrWhiteSpace(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }

        private void DrawProperty(string propertyName, string label, string tooltip)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
        }

        private void ApplyRecipeChanges()
        {
            EditorUtility.SetDirty(_output);
            PungentSpatialAuthoringSceneObjectCache.MarkDirty();
            PungentSpatialAuthoringEditorState.RequestSceneRepaint();
            if (_output.autoRebuildInEditor)
                _output.Rebuild();
        }

        private void FrameSource()
        {
            if (_output.source == null)
            {
                _status = "No source assigned.";
                return;
            }

            PungentSpatialAuthoringActions.FrameSpatialObject(_output.source);
        }

        private void FrameGenerated()
        {
            Transform root = _output.ExistingGeneratedRoot;
            if (root == null)
            {
                _status = "No generated root to frame.";
                return;
            }

            if (TryGetHierarchyBounds(root, out Bounds bounds) && SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(bounds, false);
            else if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(root.position, Vector3.one), false);
        }

        private string ValidateLane()
        {
            if (_output.source == null)
                return "Assign a path or area source before validating.";

            if (_output.sourceMode == PungentModularOutputSourceMode.AreaBoundary && !(_output.source is PungentAreaAuthoringShape))
                return "Area Boundary mode requires an area source.";

            if (_output.sourceMode != PungentModularOutputSourceMode.AreaBoundary && !(_output.source is ModularPathSpawner))
                return "Path center/corridor modes require a path source.";

            if (_output.source is ModularPathSpawner path)
                return path.PointCount >= 2 ? "Lane source is valid: " + path.PointCount + " path points." : "Path lane needs at least two points.";

            if (_output.source is PungentAreaAuthoringShape area)
            {
                _validationScratch.Clear();
                PungentSpatialValidationUtility.ValidateArea(area, _validationScratch);
                return _validationScratch.Count == 0
                    ? "Area boundary lane is valid: " + area.PolygonPointCount + " boundary points."
                    : "Area boundary has " + _validationScratch.Count + " validation issue(s).";
            }

            return "Unsupported source component.";
        }

        private readonly System.Collections.Generic.List<PungentSpatialValidationIssue> _validationScratch = new System.Collections.Generic.List<PungentSpatialValidationIssue>();

        private static bool TryGetHierarchyBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            bool found = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!found)
                {
                    bounds = renderers[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!found)
                {
                    bounds = colliders[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }

            if (found)
                return true;

            bounds = new Bounds(root.position, Vector3.one);
            return true;
        }
    }
#endif
}
