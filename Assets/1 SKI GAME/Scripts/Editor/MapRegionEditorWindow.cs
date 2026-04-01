#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SkiGame.Map;

public sealed class MapRegionEditorWindow : EditorWindow
{
    private enum EditorTool
    {
        Select = 0,
        Cut = 1,
        SetLabel = 2
    }

    private const float SidebarWidth = 360f;
    private const float MinCanvasHeight = 680f;
    private const float MinZoom = 0.35f;
    private const float MaxZoom = 6.0f;
    private const float BorderClosePixels = 14f;
    private const float StartClosePixels = 14f;
    private const float BoundaryHitPixels = 18f;

    private MapRegionSet _regionSet;
    private Vector2 _sidebarScroll;
    private Vector2 _faceListScroll;

    private string _selectedFaceId;
    private string _selectedVertexId;

    private EditorTool _tool = EditorTool.Select;

    private bool _showFill = true;
    private bool _showBorders = true;
    private bool _showLabels = true;
    private bool _showVertices = true;
    private bool _snapToVertices = true;

    private float _vertexHandleSize = 10f;
    private float _vertexSnapPixels = 12f;

    private Rect _lastCanvasRect;
    private Rect _lastImageRect;

    private bool _draggingVertex;
    private string _dragVertexId;

    private bool _panning;
    private Vector2 _panOffset = Vector2.zero;
    private float _zoom = 1f;

    private string _pendingFaceId;
    private string _cutOwnerFaceId;
    private readonly List<string> _cutPathVertexIds = new List<string>();
    private bool _cutActive;
    private bool _cutTouchesBorder;
    private int _cutStartingBorderEdge = -1;
    private int _cutClosingBorderEdge = -1;

    private bool _cutStartedOnOwnerBoundary;
    private string _cutStartOwnerBoundaryVertexId;
    private bool _cutStartBoundaryIsHole;
    private int _cutStartBoundaryHoleIndex = -1;

    [MenuItem("SkiGame/Map/Region Editor")]
    public static void Open()
    {
        GetWindow<MapRegionEditorWindow>("Map Regions");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        _regionSet = (MapRegionSet)EditorGUILayout.ObjectField("Region Set", _regionSet, typeof(MapRegionSet), false);

        if (_regionSet == null)
        {
            EditorGUILayout.HelpBox("Assign a MapRegionSet asset.", MessageType.Info);
            return;
        }

        if (_regionSet.MapData == null)
        {
            EditorGUILayout.HelpBox("MapRegionSet needs a MapData reference.", MessageType.Warning);
            return;
        }

        _regionSet.EnsureInitialized();
        EnsureSelectionIsValid();

        DrawToolbar();
        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSidebar();
            GUILayout.Space(8f);
            DrawCanvasArea(_regionSet.MapData);
        }

        HandleKeyboard();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Reset Partition", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                if (EditorUtility.DisplayDialog("Reset Partition", "Reset the region set to a single full-map face?", "Reset", "Cancel"))
                {
                    Undo.RecordObject(_regionSet, "Reset Partition");
                    _regionSet.ResetToSingleFace();
                    _selectedFaceId = MapRegionSet.RootFaceId;
                    _selectedVertexId = null;
                    CancelCutInternal(removePendingFace: true);
                    _panOffset = Vector2.zero;
                    _zoom = 1f;
                    EditorUtility.SetDirty(_regionSet);
                }
            }

            GUILayout.Space(8f);

            GUI.backgroundColor = _tool == EditorTool.Select ? new Color(0.72f, 0.90f, 1f) : Color.white;
            if (GUILayout.Button("Select", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                _tool = EditorTool.Select;

            GUI.backgroundColor = _tool == EditorTool.Cut ? new Color(0.72f, 1f, 0.72f) : Color.white;
            if (GUILayout.Button("Cut", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                _tool = EditorTool.Cut;

            GUI.backgroundColor = _tool == EditorTool.SetLabel ? new Color(1f, 0.88f, 0.60f) : Color.white;
            if (GUILayout.Button("Set Label", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                _tool = EditorTool.SetLabel;

            GUI.backgroundColor = Color.white;

            GUILayout.Space(8f);

            if (GUILayout.Button("New Region", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                BeginNewRegionCut();

            using (new EditorGUI.DisabledScope(!_cutActive))
            {
                if (GUILayout.Button("Cancel Cut", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    CancelCutInternal(removePendingFace: true);
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField(
                _tool == EditorTool.Cut
                    ? "Cut: click to add points. Close back to start or reach image border to finish. Wheel = zoom, MMB drag = pan."
                    : _tool == EditorTool.SetLabel
                        ? "Set Label: click in a face to place its label anchor."
                        : "Select: move shared vertices. Wheel = zoom, MMB drag = pan.",
                GUILayout.Height(18f));
        }
    }

    private void DrawSidebar()
    {
        using (new GUILayout.VerticalScope(GUILayout.Width(SidebarWidth)))
        {
            _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

            DrawFaceListPanel();
            GUILayout.Space(10f);
            DrawSelectedFacePanel();
            GUILayout.Space(10f);
            DrawOptionsPanel();
            GUILayout.Space(10f);
            DrawWorkflowPanel();

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawFaceListPanel()
    {
        EditorGUILayout.LabelField("Faces", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            _faceListScroll = EditorGUILayout.BeginScrollView(_faceListScroll, GUILayout.Height(220f));

            for (int i = 0; i < _regionSet.Faces.Count; i++)
            {
                var face = _regionSet.Faces[i];
                if (face == null)
                    continue;

                bool selected = string.Equals(_selectedFaceId, face.id, StringComparison.Ordinal);
                Color prev = GUI.backgroundColor;

                if (selected)
                    GUI.backgroundColor = new Color(0.84f, 0.91f, 1f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    string label = string.Equals(face.id, MapRegionSet.RootFaceId, StringComparison.Ordinal)
                        ? $"{face.displayName} (Base)"
                        : face.displayName;

                    if (GUILayout.Toggle(selected, label, "Button"))
                    {
                        _selectedFaceId = face.id;
                        _selectedVertexId = null;
                    }

                    Rect swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f));

                    if (GUI.Button(swatch, GUIContent.none, GUIStyle.none))
                    {
                        RandomizeFaceColors(face);
                    }

                    EditorGUI.DrawRect(swatch, face.fillColor);
                    EditorGUIUtility.AddCursorRect(swatch, MouseCursor.Link);
                }

                GUI.backgroundColor = prev;
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Region"))
                    BeginNewRegionCut();

                using (new EditorGUI.DisabledScope(SelectedFace == null || string.Equals(SelectedFace.id, MapRegionSet.RootFaceId, StringComparison.Ordinal)))
                {
                    if (GUILayout.Button("Delete Face"))
                        DeleteSelectedFace();
                }
            }
        }
    }

    private void DrawSelectedFacePanel()
    {
        EditorGUILayout.LabelField("Selected Face", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            if (SelectedFace == null)
            {
                EditorGUILayout.HelpBox("Select a face to edit its properties.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();

            SelectedFace.displayName = EditorGUILayout.TextField("Name", SelectedFace.displayName);
            SelectedFace.fillColor = EditorGUILayout.ColorField("Fill", SelectedFace.fillColor);
            SelectedFace.borderColor = EditorGUILayout.ColorField("Border", SelectedFace.borderColor);
            SelectedFace.labelAnchorUv = Clamp01(EditorGUILayout.Vector2Field("Label Anchor UV", SelectedFace.labelAnchorUv));

            EditorGUILayout.LabelField("Outer Vertices", SelectedFace.outerVertexIds != null ? SelectedFace.outerVertexIds.Count.ToString() : "0");
            EditorGUILayout.LabelField("Hole Count", SelectedFace.holeLoops != null ? SelectedFace.holeLoops.Count.ToString() : "0");
            EditorGUILayout.LabelField("Selected Vertex", string.IsNullOrWhiteSpace(_selectedVertexId) ? "None" : _selectedVertexId);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_regionSet, "Edit Face Properties");
                EditorUtility.SetDirty(_regionSet);
            }

            GUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Center Label"))
                {
                    Undo.RecordObject(_regionSet, "Center Face Label");
                    List<Vector2> outer = MapRegionUtility.ResolveLoopUv(_regionSet, SelectedFace.outerVertexIds);
                    SelectedFace.labelAnchorUv = Clamp01(MapRegionUtility.ComputeCentroid(outer, SelectedFace.labelAnchorUv));
                    EditorUtility.SetDirty(_regionSet);
                }

                using (new EditorGUI.DisabledScope(!_cutActive))
                {
                    if (GUILayout.Button("Finish Cut"))
                        TryFinalizeCutFromCurrentState();
                }
            }
        }
    }

    private void DrawOptionsPanel()
    {
        EditorGUILayout.LabelField("Canvas Options", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            _showFill = EditorGUILayout.ToggleLeft("Show Fill", _showFill);
            _showBorders = EditorGUILayout.ToggleLeft("Show Borders", _showBorders);
            _showLabels = EditorGUILayout.ToggleLeft("Show Labels", _showLabels);
            _showVertices = EditorGUILayout.ToggleLeft("Show Vertices", _showVertices);
            _snapToVertices = EditorGUILayout.ToggleLeft("Snap To Existing Vertices", _snapToVertices);

            _vertexHandleSize = EditorGUILayout.Slider("Vertex Handle Size", _vertexHandleSize, 6f, 18f);
            _vertexSnapPixels = EditorGUILayout.Slider("Vertex Snap (px)", _vertexSnapPixels, 4f, 40f);

            EditorGUILayout.LabelField("Zoom", _zoom.ToString("0.00x"));
            if (GUILayout.Button("Reset View"))
            {
                _zoom = 1f;
                _panOffset = Vector2.zero;
                Repaint();
            }
        }
    }

    private void DrawWorkflowPanel()
    {
        EditorGUILayout.LabelField("Workflow", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.HelpBox(
                "This editor is topology-first:\n\n" +
                "• Vertices are shared globally.\n" +
                "• Faces reuse those shared vertices.\n" +
                "• Cuts can start anywhere inside the image.\n" +
                "• Clicking outside the image uses the nearest border point.\n" +
                "• A cut finishes when it closes back to its start or reaches the image border.\n" +
                "• Moving a shared vertex updates every face that uses it.\n",
                MessageType.None);
        }
    }

    private void DrawCanvasArea(MapData mapData)
    {
        using (new GUILayout.VerticalScope())
        {
            Texture2D tex = mapData.BackgroundTexture;

            Rect canvasRect = GUILayoutUtility.GetRect(900f, MinCanvasHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _lastCanvasRect = canvasRect;

            EditorGUI.DrawRect(canvasRect, new Color(0.10f, 0.10f, 0.10f, 1f));

            Rect imageRect = GetZoomedImageRect(canvasRect, tex);
            _lastImageRect = imageRect;

            // Clip the zoomed image to the canvas area so it never draws over the sidebar/controls.
            if (tex != null)
            {
                GUI.BeginClip(canvasRect);
                Rect localImageRect = new Rect(
                    imageRect.x - canvasRect.x,
                    imageRect.y - canvasRect.y,
                    imageRect.width,
                    imageRect.height);

                GUI.DrawTexture(localImageRect, tex, ScaleMode.StretchToFill);
                GUI.EndClip();
            }

            DrawCanvasFrame(imageRect);
            DrawFaces(imageRect);
            DrawCutPreview(imageRect);
            HandleCanvasInput(canvasRect, imageRect);
        }
    }

    private Rect GetZoomedImageRect(Rect canvasRect, Texture2D tex)
    {
        Rect baseRect = GetContainedRect(canvasRect, tex);

        float width = baseRect.width * _zoom;
        float height = baseRect.height * _zoom;

        Vector2 center = baseRect.center + _panOffset;
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    private void DrawCanvasFrame(Rect drawRect)
    {
        Handles.BeginGUI();
        Handles.color = new Color(1f, 1f, 1f, 0.22f);
        Handles.DrawAAPolyLine(2f, new Vector3(drawRect.xMin, drawRect.yMin), new Vector3(drawRect.xMax, drawRect.yMin));
        Handles.DrawAAPolyLine(2f, new Vector3(drawRect.xMax, drawRect.yMin), new Vector3(drawRect.xMax, drawRect.yMax));
        Handles.DrawAAPolyLine(2f, new Vector3(drawRect.xMax, drawRect.yMax), new Vector3(drawRect.xMin, drawRect.yMax));
        Handles.DrawAAPolyLine(2f, new Vector3(drawRect.xMin, drawRect.yMax), new Vector3(drawRect.xMin, drawRect.yMin));
        Handles.EndGUI();
    }

    private void DrawFaces(Rect drawRect)
    {
        Handles.BeginGUI();

        for (int i = 0; i < _regionSet.Faces.Count; i++)
        {
            var face = _regionSet.Faces[i];
            if (face == null || !face.IsValid)
                continue;

            bool selected = string.Equals(_selectedFaceId, face.id, StringComparison.Ordinal);

            List<Vector2> outerUv = MapRegionUtility.ResolveLoopUv(_regionSet, face.outerVertexIds);
            Vector3[] outerGui = ConvertUvToGui(drawRect, outerUv);

            if (_showFill && outerUv.Count >= 3)
            {
                bool hasHoles = face.holeLoops != null && face.holeLoops.Count > 0;

                if (!hasHoles)
                {
                    Color fill = face.fillColor;
                    if (selected)
                        fill = new Color(fill.r, fill.g, fill.b, Mathf.Clamp01(fill.a + 0.08f));

                    DrawTriangulatedFaceFill(drawRect, outerUv, fill);
                }
            }

            if (_showBorders)
            {
                Handles.color = selected ? Color.white : face.borderColor;
                DrawLoopPolyline(drawRect, outerUv, selected ? 3f : 2f);

                if (face.holeLoops != null)
                {
                    for (int h = 0; h < face.holeLoops.Count; h++)
                    {
                        var hole = face.holeLoops[h];
                        if (hole == null || !hole.IsValid)
                            continue;

                        List<Vector2> holeUv = MapRegionUtility.ResolveLoopUv(_regionSet, hole.vertexIds);
                        DrawLoopPolyline(drawRect, holeUv, selected ? 3f : 2f);
                    }
                }
            }

            if (_showLabels)
            {
                Vector3 labelPos = UvToGui(drawRect, face.labelAnchorUv);
                GUI.Label(new Rect(labelPos.x + 6f, labelPos.y + 6f, 220f, 20f), face.displayName);

                if (selected)
                {
                    Rect labelRect = new Rect(labelPos.x - 4f, labelPos.y - 4f, 8f, 8f);
                    EditorGUI.DrawRect(labelRect, new Color(1f, 0.82f, 0.20f, 1f));
                }
            }
        }

        if (_showVertices)
        {
            for (int i = 0; i < _regionSet.Vertices.Count; i++)
            {
                var v = _regionSet.Vertices[i];
                if (v == null)
                    continue;

                bool selected = string.Equals(_selectedVertexId, v.id, StringComparison.Ordinal);
                Vector3 gui = UvToGui(drawRect, v.uv);
                Rect r = new Rect(gui.x - _vertexHandleSize * 0.5f, gui.y - _vertexHandleSize * 0.5f, _vertexHandleSize, _vertexHandleSize);

                EditorGUI.DrawRect(r, selected ? Color.yellow : Color.white);
            }
        }

        Handles.EndGUI();
    }

    private void DrawCutPreview(Rect drawRect)
    {
        if (!_cutActive || _cutPathVertexIds == null || _cutPathVertexIds.Count == 0)
            return;

        List<Vector3> path = new List<Vector3>();

        for (int i = 0; i < _cutPathVertexIds.Count; i++)
        {
            if (_regionSet.TryGetVertexUv(_cutPathVertexIds[i], out Vector2 uv))
                path.Add(UvToGui(drawRect, uv));
        }

        if (Event.current != null)
        {
            Vector2 nextGui = Event.current.mousePosition;
            if (!string.IsNullOrWhiteSpace(_cutOwnerFaceId))
            {
                nextGui = IsInsideImage(drawRect, Event.current.mousePosition)
                    ? Event.current.mousePosition
                    : GetNearestBorderGui(drawRect, Event.current.mousePosition);
            }

            path.Add(nextGui);
        }

        if (path.Count >= 2)
        {
            Handles.BeginGUI();
            Handles.color = Color.cyan;
            Handles.DrawAAPolyLine(3f, path.ToArray());
            Handles.EndGUI();
        }
    }

    private void HandleCanvasInput(Rect canvasRect, Rect drawRect)
    {
        Event e = Event.current;
        if (e == null || !canvasRect.Contains(e.mousePosition))
            return;

        HandleZoomAndPan(canvasRect, drawRect, e);

        if (_draggingVertex)
        {
            HandleVertexDrag(drawRect, e);
            return;
        }

        switch (_tool)
        {
            case EditorTool.Select:
                HandleSelectTool(drawRect, e);
                break;

            case EditorTool.Cut:
                HandleCutTool(drawRect, e);
                break;

            case EditorTool.SetLabel:
                HandleSetLabelTool(drawRect, e);
                break;
        }
    }

    private void HandleZoomAndPan(Rect canvasRect, Rect drawRect, Event e)
    {
        if (e.type == EventType.ScrollWheel)
        {
            Vector2 mouse = e.mousePosition;
            float oldZoom = _zoom;
            float zoomDelta = -e.delta.y * 0.06f;
            _zoom = Mathf.Clamp(_zoom * (1f + zoomDelta), MinZoom, MaxZoom);

            Vector2 oldCenterToMouse = mouse - drawRect.center;
            if (Mathf.Abs(oldZoom) > 0.0001f)
                _panOffset += oldCenterToMouse * (1f - (_zoom / oldZoom));

            Repaint();
            e.Use();
            return;
        }

        if (e.button == 2 && e.type == EventType.MouseDown)
        {
            _panning = true;
            e.Use();
            return;
        }

        if (_panning && e.button == 2 && e.type == EventType.MouseDrag)
        {
            _panOffset += e.delta;
            Repaint();
            e.Use();
            return;
        }

        if (_panning && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
        {
            _panning = false;
            e.Use();
        }
    }

    private void HandleSelectTool(Rect drawRect, Event e)
    {
        if (e.button == 2 || _panning)
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // Ctrl/Cmd + left click inserts a shared vertex on the nearest edge of the selected face.
            if ((e.control || e.command) && SelectedFace != null)
            {
                if (TryInsertSharedVertexOnNearestEdge(drawRect, e.mousePosition, SelectedFace, out string insertedVertexId))
                {
                    _selectedVertexId = insertedVertexId;
                    EditorUtility.SetDirty(_regionSet);
                    Repaint();
                    e.Use();
                    return;
                }
            }

            if (TryFindVertexNear(drawRect, e.mousePosition, out SharedMapVertex vertex))
            {
                _selectedVertexId = vertex.id;
                _selectedFaceId = FindOwningFaceIdForVertex(vertex.id) ?? _selectedFaceId;
                _draggingVertex = true;
                _dragVertexId = vertex.id;
                e.Use();
                return;
            }

            if (IsInsideImage(drawRect, e.mousePosition))
            {
                string faceId = ResolveFaceUnderMouse(drawRect, e.mousePosition);
                if (!string.IsNullOrWhiteSpace(faceId))
                {
                    _selectedFaceId = faceId;
                    _selectedVertexId = null;
                    e.Use();
                }
            }
        }
        else if (e.type == EventType.MouseDown && e.button == 1)
        {
            // Right click removes a shared vertex if safe.
            if (TryFindVertexNear(drawRect, e.mousePosition, out SharedMapVertex vertex))
            {
                if (TryRemoveSharedVertex(vertex.id))
                {
                    _selectedVertexId = null;
                    EditorUtility.SetDirty(_regionSet);
                    Repaint();
                    e.Use();
                    return;
                }
            }
        }
    }

    private void HandleVertexDrag(Rect drawRect, Event e)
    {
        if (!_draggingVertex)
            return;

        var v = _regionSet.GetVertexById(_dragVertexId);
        if (v == null)
        {
            _draggingVertex = false;
            _dragVertexId = null;
            return;
        }

        if (e.type == EventType.MouseDrag || e.type == EventType.MouseMove)
        {
            Undo.RecordObject(_regionSet, "Move Shared Vertex");
            Vector2 gui = IsInsideImage(drawRect, e.mousePosition) ? e.mousePosition : GetNearestBorderGui(drawRect, e.mousePosition);
            Vector2 uv = Clamp01(GuiToUv(drawRect, gui));

            if (_snapToVertices)
                uv = SnapUvToOtherVertices(uv, _dragVertexId);

            // Only lock if this vertex is actually part of a root-loop border edge on the image boundary.
            if (TryGetRootBoundaryLock(_dragVertexId, out BorderLockMode lockMode))
                uv = ApplyBorderLock(uv, lockMode);

            v.uv = uv;
            EditorUtility.SetDirty(_regionSet);
            Repaint();
            e.Use();
        }
        else if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp)
        {
            _draggingVertex = false;
            _dragVertexId = null;
            e.Use();
        }
    }

    private void HandleCutTool(Rect drawRect, Event e)
    {
        if (e.button == 2 || _panning)
            return;

        if (!_cutActive || e.type != EventType.MouseDown || e.button != 0)
            return;

        bool insideImage = IsInsideImage(drawRect, e.mousePosition);
        Vector2 gui = insideImage ? e.mousePosition : GetNearestBorderGui(drawRect, e.mousePosition);
        Vector2 uv = Clamp01(GuiToUv(drawRect, gui));

        // First point: lock owner once, and allow starting on any owner boundary loop (outer or hole).
        if (_cutPathVertexIds.Count == 0)
        {
            Undo.RecordObject(_regionSet, "Start Cut");

            string ownerFaceId = ResolveFaceForNewCutStart(uv, !insideImage);
            _cutOwnerFaceId = string.IsNullOrWhiteSpace(ownerFaceId) ? MapRegionSet.RootFaceId : ownerFaceId;

            string startVertexId = null;
            _cutStartedOnOwnerBoundary = false;
            _cutStartOwnerBoundaryVertexId = null;
            _cutStartBoundaryIsHole = false;
            _cutStartBoundaryHoleIndex = -1;

            if (insideImage)
            {
                if (TryGetOrCreateVertexOnOwnerBoundary(drawRect, gui, _cutOwnerFaceId, out startVertexId, out bool isHole, out int holeIndex))
                {
                    _cutStartedOnOwnerBoundary = true;
                    _cutStartOwnerBoundaryVertexId = startVertexId;
                    _cutStartBoundaryIsHole = isHole;
                    _cutStartBoundaryHoleIndex = holeIndex;
                    _cutTouchesBorder = false;
                    _cutStartingBorderEdge = -1;
                }
                else
                {
                    startVertexId = TryFindVertexNear(drawRect, gui, out SharedMapVertex existingStart)
                        ? existingStart.id
                        : _regionSet.CreateVertex(uv);

                    _cutTouchesBorder = false;
                    _cutStartingBorderEdge = -1;
                }
            }
            else
            {
                if (!TryGetOrCreateBorderVertexOnOwnerFace(drawRect, gui, _cutOwnerFaceId, out startVertexId))
                    return;

                _cutTouchesBorder = true;
                _cutStartingBorderEdge = GetNearestBorderEdge(drawRect, gui);
                _cutStartedOnOwnerBoundary = true;
                _cutStartOwnerBoundaryVertexId = startVertexId;
                _cutStartBoundaryIsHole = false;
                _cutStartBoundaryHoleIndex = -1;
            }

            _cutPathVertexIds.Add(startVertexId);

            var pending = _regionSet.GetFaceById(_pendingFaceId);
            if (pending != null)
                pending.labelAnchorUv = uv;

            EditorUtility.SetDirty(_regionSet);
            Repaint();
            e.Use();
            return;
        }

        // Close back to start = closed loop cut
        if (TryFindVertexNear(drawRect, gui, out SharedMapVertex existingVertex))
        {
            string vertexId = existingVertex.id;

            if (string.Equals(vertexId, _cutPathVertexIds[0], StringComparison.Ordinal) && _cutPathVertexIds.Count >= 3)
            {
                Undo.RecordObject(_regionSet, "Close Loop Cut");
                EnsureLastVertexAppended(vertexId);
                FinalizeClosedLoopCut();
                EditorUtility.SetDirty(_regionSet);
                Repaint();
                e.Use();
                return;
            }
        }

        // If the cut started on an owner boundary, prefer closing on that SAME boundary component
        // before treating the click as a generic shared-vertex append.
        if (insideImage && _cutStartedOnOwnerBoundary && _cutPathVertexIds.Count >= 2)
        {
            if (TryGetOrCreateVertexOnSpecificOwnerBoundary(
                    drawRect,
                    gui,
                    _cutOwnerFaceId,
                    _cutStartBoundaryIsHole,
                    _cutStartBoundaryHoleIndex,
                    out string ownerBoundaryVertexId) &&
                !string.Equals(ownerBoundaryVertexId, _cutStartOwnerBoundaryVertexId, StringComparison.Ordinal))
            {
                Undo.RecordObject(_regionSet, "Close Owner Boundary Cut");
                AppendVertexToCut(ownerBoundaryVertexId);
                FinalizeOwnerBoundaryCut();
                EditorUtility.SetDirty(_regionSet);
                Repaint();
                e.Use();
                return;
            }
        }

        // Border finish
        if (!insideImage)
        {
            Undo.RecordObject(_regionSet, "Close Border Cut");

            if (!TryGetOrCreateBorderVertexOnOwnerFace(drawRect, gui, _cutOwnerFaceId, out string borderVertexId))
                return;

            AppendVertexToCut(borderVertexId);
            _cutTouchesBorder = true;
            _cutClosingBorderEdge = GetNearestBorderEdge(drawRect, gui);

            FinalizeBorderCut();
            EditorUtility.SetDirty(_regionSet);
            Repaint();
            e.Use();
            return;
        }

        // Reuse an existing shared vertex if near one.
        if (TryFindVertexNear(drawRect, gui, out SharedMapVertex nearbyVertex))
        {
            AppendVertexToCut(nearbyVertex.id);
            EditorUtility.SetDirty(_regionSet);
            Repaint();
            e.Use();
            return;
        }

        // Interior point
        Undo.RecordObject(_regionSet, "Append Cut Point");
        string newVertexId = _regionSet.CreateVertex(uv);
        AppendVertexToCut(newVertexId);

        var pendingFace = _regionSet.GetFaceById(_pendingFaceId);
        if (pendingFace != null && _cutPathVertexIds.Count == 2)
            pendingFace.labelAnchorUv = uv;

        EditorUtility.SetDirty(_regionSet);
        Repaint();
        e.Use();
    }

    private void HandleSetLabelTool(Rect drawRect, Event e)
    {
        if (e.button == 2 || _panning)
            return;

        if (e.type != EventType.MouseDown || e.button != 0 || !IsInsideImage(drawRect, e.mousePosition))
            return;

        string faceId = ResolveFaceUnderMouse(drawRect, e.mousePosition);
        if (string.IsNullOrWhiteSpace(faceId))
            return;

        MapRegionFace face = _regionSet.GetFaceById(faceId);
        if (face == null)
            return;

        Undo.RecordObject(_regionSet, "Set Face Label Anchor");
        face.labelAnchorUv = Clamp01(GuiToUv(drawRect, e.mousePosition));
        _selectedFaceId = face.id;
        EditorUtility.SetDirty(_regionSet);
        e.Use();
    }

    private void BeginNewRegionCut()
    {
        Undo.RecordObject(_regionSet, "Create Pending Face");
        CancelCutInternal(removePendingFace: true);

        Color fill = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 0.18f);
        MapRegionFace face = _regionSet.CreateFace($"Region {_regionSet.Faces.Count + 1}", fill, Color.white);

        _pendingFaceId = face.id;
        _selectedFaceId = face.id;
        _selectedVertexId = null;
        _cutOwnerFaceId = null;
        _cutPathVertexIds.Clear();
        _cutActive = true;
        _cutTouchesBorder = false;
        _cutStartingBorderEdge = -1;
        _cutClosingBorderEdge = -1;
        _cutStartedOnOwnerBoundary = false;
        _cutStartOwnerBoundaryVertexId = null;
        _cutStartBoundaryIsHole = false;
        _cutStartBoundaryHoleIndex = -1;
        _tool = EditorTool.Cut;

        EditorUtility.SetDirty(_regionSet);
    }

    private void CancelCutInternal(bool removePendingFace)
    {
        if (removePendingFace && !string.IsNullOrWhiteSpace(_pendingFaceId))
            _regionSet.RemoveFaceById(_pendingFaceId);

        _pendingFaceId = null;
        _cutOwnerFaceId = null;
        _cutPathVertexIds.Clear();
        _cutActive = false;
        _cutTouchesBorder = false;
        _cutStartingBorderEdge = -1;
        _cutClosingBorderEdge = -1;
        _cutStartedOnOwnerBoundary = false;
        _cutStartOwnerBoundaryVertexId = null;
        _cutStartBoundaryIsHole = false;
        _cutStartBoundaryHoleIndex = -1;
    }

    private void TryFinalizeCutFromCurrentState()
    {
        if (!_cutActive || _cutPathVertexIds.Count < 2)
            return;

        if (_cutTouchesBorder)
        {
            FinalizeBorderCut();
            return;
        }

        if (_cutPathVertexIds.Count >= 3 &&
            string.Equals(_cutPathVertexIds[0], _cutPathVertexIds[_cutPathVertexIds.Count - 1], StringComparison.Ordinal))
        {
            FinalizeClosedLoopCut();
            return;
        }

        if (_cutStartedOnOwnerBoundary && _cutPathVertexIds.Count >= 3)
        {
            FinalizeOwnerBoundaryCut();
        }
    }

    private void FinalizeClosedLoopCut()
    {
        MapRegionFace pending = _regionSet.GetFaceById(_pendingFaceId);
        MapRegionFace owner = _regionSet.GetFaceById(_cutOwnerFaceId);

        if (pending == null || owner == null || _cutPathVertexIds.Count < 4)
            return;

        List<string> loop = new List<string>(_cutPathVertexIds);
        MapRegionUtility.NormalizeLoop(loop);
        MapRegionUtility.EnsureClockwise(_regionSet, loop);

        pending.outerVertexIds = new List<string>(loop);
        pending.holeLoops = new List<MapRegionLoop>();
        pending.labelAnchorUv = MapRegionUtility.ComputeCentroid(MapRegionUtility.ResolveLoopUv(_regionSet, pending.outerVertexIds), pending.labelAnchorUv);

        owner.holeLoops.Add(new MapRegionLoop
        {
            vertexIds = new List<string>(loop)
        });
        MapRegionUtility.EnsureCounterClockwise(_regionSet, owner.holeLoops[owner.holeLoops.Count - 1].vertexIds);

        _selectedFaceId = pending.id;
        _selectedVertexId = null;
        _regionSet.RemoveUnusedVertices();
        CancelCutInternal(removePendingFace: false);
    }

    private void FinalizeOwnerBoundaryCut()
    {
        MapRegionFace pending = _regionSet.GetFaceById(_pendingFaceId);
        MapRegionFace owner = _regionSet.GetFaceById(_cutOwnerFaceId);

        if (pending == null || owner == null || _cutPathVertexIds.Count < 3)
            return;

        List<string> boundaryLoop = !_cutStartBoundaryIsHole
            ? owner.outerVertexIds
            : (owner.holeLoops != null && _cutStartBoundaryHoleIndex >= 0 && _cutStartBoundaryHoleIndex < owner.holeLoops.Count
                ? owner.holeLoops[_cutStartBoundaryHoleIndex].vertexIds
                : null);

        if (boundaryLoop == null || boundaryLoop.Count < 2)
            return;

        string startId = _cutPathVertexIds[0];
        string endId = _cutPathVertexIds[_cutPathVertexIds.Count - 1];

        int startIndex = boundaryLoop.IndexOf(startId);
        int endIndex = boundaryLoop.IndexOf(endId);

        if (startIndex < 0 || endIndex < 0 || startIndex == endIndex)
            return;

        List<string> arcForward = MapRegionUtility.BuildBoundaryPath(boundaryLoop, endIndex, startIndex, true);
        List<string> arcBackward = MapRegionUtility.BuildBoundaryPath(boundaryLoop, endIndex, startIndex, false);

        List<string> loopA = BuildLoop(_cutPathVertexIds, arcForward);
        List<string> loopB = BuildLoop(_cutPathVertexIds, arcBackward);

        MapRegionUtility.NormalizeLoop(loopA);
        MapRegionUtility.NormalizeLoop(loopB);

        float areaA = Mathf.Abs(MapRegionUtility.ComputeSignedArea(MapRegionUtility.ResolveLoopUv(_regionSet, loopA)));
        float areaB = Mathf.Abs(MapRegionUtility.ComputeSignedArea(MapRegionUtility.ResolveLoopUv(_regionSet, loopB)));

        List<string> newLoop = areaA <= areaB ? loopA : loopB;
        List<string> ownerRemainingLoop = areaA <= areaB ? loopB : loopA;

        MapRegionUtility.EnsureClockwise(_regionSet, newLoop);

        pending.outerVertexIds = new List<string>(newLoop);
        pending.holeLoops = new List<MapRegionLoop>();
        pending.labelAnchorUv = MapRegionUtility.ComputeCentroid(
            MapRegionUtility.ResolveLoopUv(_regionSet, pending.outerVertexIds),
            pending.labelAnchorUv);

        if (_cutStartBoundaryIsHole)
        {
            // We split a hole loop. The remaining loop stays as a hole.
            MapRegionUtility.EnsureCounterClockwise(_regionSet, ownerRemainingLoop);
            owner.holeLoops[_cutStartBoundaryHoleIndex].vertexIds = new List<string>(ownerRemainingLoop);
        }
        else
        {
            // We split the owner's outer loop.
            MapRegionUtility.EnsureClockwise(_regionSet, ownerRemainingLoop);
            owner.outerVertexIds = new List<string>(ownerRemainingLoop);
        }

        _selectedFaceId = pending.id;
        _selectedVertexId = null;
        _regionSet.RemoveUnusedVertices();
        CancelCutInternal(removePendingFace: false);
    }

    private void FinalizeBorderCut()
    {
        MapRegionFace pending = _regionSet.GetFaceById(_pendingFaceId);
        MapRegionFace owner = _regionSet.GetFaceById(_cutOwnerFaceId);

        if (pending == null || owner == null || _cutPathVertexIds.Count < 2)
            return;

        string startId = _cutPathVertexIds[0];
        string endId = _cutPathVertexIds[_cutPathVertexIds.Count - 1];

        List<string> ownerLoop = owner.outerVertexIds;
        int startIndex = ownerLoop.IndexOf(startId);
        int endIndex = ownerLoop.IndexOf(endId);

        if (startIndex < 0 || endIndex < 0)
            return;

        // Only build the two complementary loops on the locked owner face.
        List<string> arcForward = MapRegionUtility.BuildBoundaryPath(ownerLoop, endIndex, startIndex, true);
        List<string> arcBackward = MapRegionUtility.BuildBoundaryPath(ownerLoop, endIndex, startIndex, false);

        List<string> loopA = BuildLoop(_cutPathVertexIds, arcForward);
        List<string> loopB = BuildLoop(_cutPathVertexIds, arcBackward);

        MapRegionUtility.NormalizeLoop(loopA);
        MapRegionUtility.NormalizeLoop(loopB);

        float areaA = Mathf.Abs(MapRegionUtility.ComputeSignedArea(MapRegionUtility.ResolveLoopUv(_regionSet, loopA)));
        float areaB = Mathf.Abs(MapRegionUtility.ComputeSignedArea(MapRegionUtility.ResolveLoopUv(_regionSet, loopB)));

        List<string> newLoop = areaA <= areaB ? loopA : loopB;
        List<string> ownerNewLoop = areaA <= areaB ? loopB : loopA;

        MapRegionUtility.EnsureClockwise(_regionSet, newLoop);
        MapRegionUtility.EnsureClockwise(_regionSet, ownerNewLoop);

        pending.outerVertexIds = new List<string>(newLoop);
        pending.holeLoops = new List<MapRegionLoop>();
        pending.labelAnchorUv = MapRegionUtility.ComputeCentroid(
            MapRegionUtility.ResolveLoopUv(_regionSet, pending.outerVertexIds),
            pending.labelAnchorUv);

        owner.outerVertexIds = new List<string>(ownerNewLoop);

        _selectedFaceId = pending.id;
        _selectedVertexId = null;
        _regionSet.RemoveUnusedVertices();
        CancelCutInternal(removePendingFace: false);
    }

    private static List<string> BuildLoop(List<string> cutPath, List<string> arc)
    {
        List<string> result = new List<string>();

        if (cutPath != null)
            result.AddRange(cutPath);

        if (arc != null && arc.Count > 0)
        {
            int start = 0;
            if (result.Count > 0 && string.Equals(result[result.Count - 1], arc[0], StringComparison.Ordinal))
                start = 1;

            for (int i = start; i < arc.Count; i++)
                result.Add(arc[i]);
        }

        return result;
    }

    private void AppendVertexToCut(string vertexId)
    {
        if (string.IsNullOrWhiteSpace(vertexId))
            return;

        if (_cutPathVertexIds.Count == 0 || !string.Equals(_cutPathVertexIds[_cutPathVertexIds.Count - 1], vertexId, StringComparison.Ordinal))
            _cutPathVertexIds.Add(vertexId);
    }

    private void EnsureLastVertexAppended(string vertexId)
    {
        if (_cutPathVertexIds.Count == 0 || !string.Equals(_cutPathVertexIds[_cutPathVertexIds.Count - 1], vertexId, StringComparison.Ordinal))
            _cutPathVertexIds.Add(vertexId);
    }

    private string ResolveFaceForNewCutStart(Vector2 uv, bool cameFromBorder)
    {
        Vector2 sampleUv = uv;

        if (cameFromBorder)
            sampleUv = NudgeBorderUvInward(uv);

        MapRegionFace face = MapRegionUtility.ResolveRegion(_regionSet, sampleUv);
        return face != null ? face.id : MapRegionSet.RootFaceId;
    }

    private bool TryGetOrCreateBorderVertexOnOwnerFace(Rect drawRect, Vector2 borderGui, string ownerFaceId, out string vertexId)
    {
        vertexId = null;

        MapRegionFace owner = _regionSet.GetFaceById(ownerFaceId);
        if (owner == null || owner.outerVertexIds == null || owner.outerVertexIds.Count < 2)
            return false;

        Vector2 borderUv = Clamp01(GuiToUv(drawRect, borderGui));

        // Reuse only vertices already on the owner's outer loop and on the image border.
        for (int i = 0; i < owner.outerVertexIds.Count; i++)
        {
            string id = owner.outerVertexIds[i];
            if (!_regionSet.TryGetVertexUv(id, out Vector2 uv))
                continue;

            if (!IsUvOnImageBorder(uv))
                continue;

            float dist = Vector2.Distance(UvToGui(drawRect, uv), borderGui);
            if (dist <= BoundaryHitPixels)
            {
                vertexId = id;
                return true;
            }
        }

        // Otherwise insert a new border vertex only into the owner's outer loop.
        int insertAfterIndex = -1;
        float bestDist = float.MaxValue;
        Vector2 bestUv = default;

        for (int i = 0; i < owner.outerVertexIds.Count; i++)
        {
            int j = (i + 1) % owner.outerVertexIds.Count;

            if (!_regionSet.TryGetVertexUv(owner.outerVertexIds[i], out Vector2 aUv) ||
                !_regionSet.TryGetVertexUv(owner.outerVertexIds[j], out Vector2 bUv))
                continue;

            if (!IsEdgeOnImageBorder(aUv, bUv))
                continue;

            Vector2 aGui = UvToGui(drawRect, aUv);
            Vector2 bGui = UvToGui(drawRect, bUv);
            Vector2 closestGui = MapRegionUtility.ClosestPointOnSegment(borderGui, aGui, bGui);
            float dist = Vector2.Distance(borderGui, closestGui);

            if (dist < bestDist)
            {
                float t = MapRegionUtility.ClosestPointParameterOnSegment(closestGui, aGui, bGui);
                bestDist = dist;
                bestUv = Vector2.Lerp(aUv, bUv, t);
                insertAfterIndex = i;
            }
        }

        if (insertAfterIndex < 0 || bestDist > BoundaryHitPixels)
            return false;

        vertexId = _regionSet.CreateVertex(bestUv);
        owner.outerVertexIds.Insert(insertAfterIndex + 1, vertexId);
        return true;
    }

    private bool TryGetOrCreateVertexOnOwnerBoundary(
    Rect drawRect,
    Vector2 gui,
    string ownerFaceId,
    out string vertexId,
    out bool isHole,
    out int holeIndex)
    {
        vertexId = null;
        isHole = false;
        holeIndex = -1;

        MapRegionFace owner = _regionSet.GetFaceById(ownerFaceId);
        if (owner == null)
            return false;

        // Outer first
        if (TryGetOrCreateVertexOnSpecificLoop(drawRect, gui, owner.outerVertexIds, out vertexId))
        {
            isHole = false;
            holeIndex = -1;
            return true;
        }

        // Then holes
        if (owner.holeLoops != null)
        {
            for (int i = 0; i < owner.holeLoops.Count; i++)
            {
                var hole = owner.holeLoops[i];
                if (hole == null || hole.vertexIds == null || hole.vertexIds.Count < 2)
                    continue;

                if (TryGetOrCreateVertexOnSpecificLoop(drawRect, gui, hole.vertexIds, out vertexId))
                {
                    isHole = true;
                    holeIndex = i;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetOrCreateVertexOnSpecificOwnerBoundary(
        Rect drawRect,
        Vector2 gui,
        string ownerFaceId,
        bool isHole,
        int holeIndex,
        out string vertexId)
    {
        vertexId = null;

        MapRegionFace owner = _regionSet.GetFaceById(ownerFaceId);
        if (owner == null)
            return false;

        List<string> loopIds = !isHole
            ? owner.outerVertexIds
            : (owner.holeLoops != null && holeIndex >= 0 && holeIndex < owner.holeLoops.Count ? owner.holeLoops[holeIndex].vertexIds : null);

        if (loopIds == null || loopIds.Count < 2)
            return false;

        return TryGetOrCreateVertexOnSpecificLoop(drawRect, gui, loopIds, out vertexId);
    }

    private bool TryGetOrCreateVertexOnSpecificLoop(Rect drawRect, Vector2 gui, List<string> loopIds, out string vertexId)
    {
        vertexId = null;

        if (loopIds == null || loopIds.Count < 2)
            return false;

        Vector2 uv = Clamp01(GuiToUv(drawRect, gui));

        // Reuse nearby loop vertex first.
        for (int i = 0; i < loopIds.Count; i++)
        {
            string id = loopIds[i];
            if (!_regionSet.TryGetVertexUv(id, out Vector2 loopUv))
                continue;

            float dist = Vector2.Distance(UvToGui(drawRect, loopUv), gui);
            if (dist <= BoundaryHitPixels)
            {
                vertexId = id;
                return true;
            }
        }

        // Otherwise insert on nearest edge of this specific loop.
        int insertAfterIndex = -1;
        float bestDist = float.MaxValue;
        Vector2 bestUv = default;

        for (int i = 0; i < loopIds.Count; i++)
        {
            int j = (i + 1) % loopIds.Count;

            if (!_regionSet.TryGetVertexUv(loopIds[i], out Vector2 aUv) ||
                !_regionSet.TryGetVertexUv(loopIds[j], out Vector2 bUv))
                continue;

            Vector2 aGui = UvToGui(drawRect, aUv);
            Vector2 bGui = UvToGui(drawRect, bUv);
            Vector2 closestGui = MapRegionUtility.ClosestPointOnSegment(gui, aGui, bGui);
            float dist = Vector2.Distance(gui, closestGui);

            if (dist < bestDist)
            {
                float t = MapRegionUtility.ClosestPointParameterOnSegment(closestGui, aGui, bGui);
                bestDist = dist;
                bestUv = Vector2.Lerp(aUv, bUv, t);
                insertAfterIndex = i;
            }
        }

        if (insertAfterIndex < 0 || bestDist > BoundaryHitPixels)
            return false;

        vertexId = _regionSet.CreateVertex(bestUv);
        loopIds.Insert(insertAfterIndex + 1, vertexId);
        return true;
    }

    private bool TryGetOrCreateVertexOnOwnerOuterBoundary(Rect drawRect, Vector2 gui, string ownerFaceId, out string vertexId)
    {
        vertexId = null;

        MapRegionFace owner = _regionSet.GetFaceById(ownerFaceId);
        if (owner == null || owner.outerVertexIds == null || owner.outerVertexIds.Count < 2)
            return false;

        Vector2 uv = Clamp01(GuiToUv(drawRect, gui));

        // Reuse nearby vertex on the owner's outer loop first.
        for (int i = 0; i < owner.outerVertexIds.Count; i++)
        {
            string id = owner.outerVertexIds[i];
            if (!_regionSet.TryGetVertexUv(id, out Vector2 loopUv))
                continue;

            float dist = Vector2.Distance(UvToGui(drawRect, loopUv), gui);
            if (dist <= _vertexSnapPixels)
            {
                vertexId = id;
                return true;
            }
        }

        // Otherwise insert on the nearest outer-loop edge.
        int insertAfterIndex = -1;
        float bestDist = float.MaxValue;
        Vector2 bestUv = default;

        for (int i = 0; i < owner.outerVertexIds.Count; i++)
        {
            int j = (i + 1) % owner.outerVertexIds.Count;

            if (!_regionSet.TryGetVertexUv(owner.outerVertexIds[i], out Vector2 aUv) ||
                !_regionSet.TryGetVertexUv(owner.outerVertexIds[j], out Vector2 bUv))
                continue;

            Vector2 aGui = UvToGui(drawRect, aUv);
            Vector2 bGui = UvToGui(drawRect, bUv);
            Vector2 closestGui = MapRegionUtility.ClosestPointOnSegment(gui, aGui, bGui);
            float dist = Vector2.Distance(gui, closestGui);

            if (dist < bestDist)
            {
                float t = MapRegionUtility.ClosestPointParameterOnSegment(closestGui, aGui, bGui);
                bestDist = dist;
                bestUv = Vector2.Lerp(aUv, bUv, t);
                insertAfterIndex = i;
            }
        }

        if (insertAfterIndex < 0 || bestDist > _vertexSnapPixels * 1.5f)
            return false;

        vertexId = _regionSet.CreateVertex(bestUv);
        owner.outerVertexIds.Insert(insertAfterIndex + 1, vertexId);
        return true;
    }

    private bool TryInsertSharedVertexOnNearestEdge(Rect drawRect, Vector2 mousePos, MapRegionFace face, out string insertedVertexId)
    {
        insertedVertexId = null;

        if (face == null)
            return false;

        float bestDist = float.MaxValue;
        string bestA = null;
        string bestB = null;
        Vector2 bestUv = default;
        bool found = false;

        EvaluateNearestEdgeOnLoop(drawRect, mousePos, face.outerVertexIds, ref bestDist, ref bestA, ref bestB, ref bestUv, ref found);

        if (face.holeLoops != null)
        {
            for (int i = 0; i < face.holeLoops.Count; i++)
            {
                var hole = face.holeLoops[i];
                if (hole == null || hole.vertexIds == null || hole.vertexIds.Count < 2)
                    continue;

                EvaluateNearestEdgeOnLoop(drawRect, mousePos, hole.vertexIds, ref bestDist, ref bestA, ref bestB, ref bestUv, ref found);
            }
        }

        if (!found || bestDist > _vertexSnapPixels * 1.5f)
            return false;

        Undo.RecordObject(_regionSet, "Insert Shared Vertex On Edge");

        insertedVertexId = _regionSet.CreateVertex(bestUv);
        InsertVertexOnAllMatchingEdges(bestA, bestB, insertedVertexId);
        return true;
    }

    private void InsertVertexOnAllMatchingEdges(string aId, string bId, string newVertexId)
    {
        for (int i = 0; i < _regionSet.Faces.Count; i++)
        {
            var face = _regionSet.Faces[i];
            if (face == null)
                continue;

            InsertVertexOnLoopIfEdgeMatches(face.outerVertexIds, aId, bId, newVertexId);

            if (face.holeLoops != null)
            {
                for (int h = 0; h < face.holeLoops.Count; h++)
                {
                    var hole = face.holeLoops[h];
                    if (hole == null)
                        continue;

                    InsertVertexOnLoopIfEdgeMatches(hole.vertexIds, aId, bId, newVertexId);
                }
            }
        }
    }

    private void EvaluateNearestEdgeOnLoop(
        Rect drawRect,
        Vector2 mousePos,
        List<string> ids,
        ref float bestDist,
        ref string bestA,
        ref string bestB,
        ref Vector2 bestUv,
        ref bool found)
    {
        if (ids == null || ids.Count < 2)
            return;

        for (int i = 0; i < ids.Count; i++)
        {
            int j = (i + 1) % ids.Count;

            if (!_regionSet.TryGetVertexUv(ids[i], out Vector2 aUv) ||
                !_regionSet.TryGetVertexUv(ids[j], out Vector2 bUv))
                continue;

            Vector2 aGui = UvToGui(drawRect, aUv);
            Vector2 bGui = UvToGui(drawRect, bUv);

            Vector2 closestGui = MapRegionUtility.ClosestPointOnSegment(mousePos, aGui, bGui);
            float dist = Vector2.Distance(mousePos, closestGui);

            if (dist < bestDist)
            {
                float t = MapRegionUtility.ClosestPointParameterOnSegment(closestGui, aGui, bGui);
                bestDist = dist;
                bestA = ids[i];
                bestB = ids[j];
                bestUv = Vector2.Lerp(aUv, bUv, t);
                found = true;
            }
        }
    }

    private bool TryRemoveSharedVertex(string vertexId)
    {
        if (string.IsNullOrWhiteSpace(vertexId))
            return false;

        // Do not remove if any loop would become invalid.
        for (int i = 0; i < _regionSet.Faces.Count; i++)
        {
            var face = _regionSet.Faces[i];
            if (face == null)
                continue;

            if (face.outerVertexIds != null && face.outerVertexIds.Contains(vertexId) && face.outerVertexIds.Count <= 3)
                return false;

            if (face.holeLoops != null)
            {
                for (int h = 0; h < face.holeLoops.Count; h++)
                {
                    var hole = face.holeLoops[h];
                    if (hole == null || hole.vertexIds == null)
                        continue;

                    if (hole.vertexIds.Contains(vertexId) && hole.vertexIds.Count <= 3)
                        return false;
                }
            }
        }

        Undo.RecordObject(_regionSet, "Remove Shared Vertex");

        for (int i = 0; i < _regionSet.Faces.Count; i++)
        {
            var face = _regionSet.Faces[i];
            if (face == null)
                continue;

            RemoveVertexFromLoop(face.outerVertexIds, vertexId);

            if (face.holeLoops != null)
            {
                for (int h = face.holeLoops.Count - 1; h >= 0; h--)
                {
                    var hole = face.holeLoops[h];
                    if (hole == null || hole.vertexIds == null)
                        continue;

                    RemoveVertexFromLoop(hole.vertexIds, vertexId);

                    if (hole.vertexIds.Count < 3)
                        face.holeLoops.RemoveAt(h);
                }
            }
        }

        _regionSet.RemoveUnusedVertices();
        return true;
    }

    private static void RemoveVertexFromLoop(List<string> ids, string vertexId)
    {
        if (ids == null)
            return;

        for (int i = ids.Count - 1; i >= 0; i--)
        {
            if (string.Equals(ids[i], vertexId, StringComparison.Ordinal))
                ids.RemoveAt(i);
        }
    }

    private static void InsertVertexOnLoopIfEdgeMatches(List<string> ids, string aId, string bId, string newVertexId)
    {
        if (ids == null || ids.Count < 2)
            return;

        for (int i = 0; i < ids.Count; i++)
        {
            int j = (i + 1) % ids.Count;
            string a = ids[i];
            string b = ids[j];

            if ((string.Equals(a, aId, StringComparison.Ordinal) && string.Equals(b, bId, StringComparison.Ordinal)) ||
                (string.Equals(a, bId, StringComparison.Ordinal) && string.Equals(b, aId, StringComparison.Ordinal)))
            {
                ids.Insert(i + 1, newVertexId);
                i++;
            }
        }
    }

    private bool TryFindVertexNear(Rect drawRect, Vector2 mousePos, out SharedMapVertex vertex)
    {
        vertex = null;
        float best = float.MaxValue;

        for (int i = 0; i < _regionSet.Vertices.Count; i++)
        {
            var v = _regionSet.Vertices[i];
            if (v == null)
                continue;

            float dist = Vector2.Distance(mousePos, UvToGui(drawRect, v.uv));
            if (dist <= _vertexSnapPixels && dist < best)
            {
                best = dist;
                vertex = v;
            }
        }

        return vertex != null;
    }

    private string ResolveFaceUnderMouse(Rect drawRect, Vector2 mousePos)
    {
        Vector2 uv = Clamp01(GuiToUv(drawRect, mousePos));
        MapRegionFace face = MapRegionUtility.ResolveRegion(_regionSet, uv);
        return face != null ? face.id : null;
    }

    private string FindOwningFaceIdForVertex(string vertexId)
    {
        if (string.IsNullOrWhiteSpace(vertexId))
            return null;

        for (int i = 0; i < _regionSet.Faces.Count; i++)
        {
            var face = _regionSet.Faces[i];
            if (face == null)
                continue;

            if (face.outerVertexIds != null && face.outerVertexIds.Contains(vertexId))
                return face.id;

            if (face.holeLoops != null)
            {
                for (int h = 0; h < face.holeLoops.Count; h++)
                {
                    var hole = face.holeLoops[h];
                    if (hole != null && hole.vertexIds != null && hole.vertexIds.Contains(vertexId))
                        return face.id;
                }
            }
        }

        return null;
    }

    private void RandomizeFaceColors(MapRegionFace face)
    {
        if (face == null || _regionSet == null)
            return;

        Undo.RecordObject(_regionSet, "Randomize Face Color");

        Color random = UnityEngine.Random.ColorHSV(
            0f, 1f,
            0.55f, 0.95f,
            0.65f, 1f);

        face.fillColor = new Color(random.r, random.g, random.b, face.fillColor.a);
        face.borderColor = new Color(random.r, random.g, random.b, face.borderColor.a);

        EditorUtility.SetDirty(_regionSet);
        Repaint();
    }

    private void DeleteSelectedFace()
    {
        if (SelectedFace == null || string.Equals(SelectedFace.id, MapRegionSet.RootFaceId, StringComparison.Ordinal))
            return;

        if (!EditorUtility.DisplayDialog("Delete Face", $"Delete '{SelectedFace.displayName}'?", "Delete", "Cancel"))
            return;

        Undo.RecordObject(_regionSet, "Delete Face");
        _regionSet.RemoveFaceById(SelectedFace.id);
        _regionSet.RemoveUnusedVertices();
        _selectedFaceId = MapRegionSet.RootFaceId;
        _selectedVertexId = null;
        EditorUtility.SetDirty(_regionSet);
    }

    private Vector2 SnapUvToOtherVertices(Vector2 uv, string ignoredVertexId)
    {
        if (!_snapToVertices)
            return uv;

        float best = float.MaxValue;
        Vector2 bestUv = uv;
        bool found = false;

        for (int i = 0; i < _regionSet.Vertices.Count; i++)
        {
            var v = _regionSet.Vertices[i];
            if (v == null || string.Equals(v.id, ignoredVertexId, StringComparison.Ordinal))
                continue;

            float dist = Vector2.Distance(UvToGui(_lastImageRect, uv), UvToGui(_lastImageRect, v.uv));
            if (dist <= _vertexSnapPixels && dist < best)
            {
                best = dist;
                bestUv = v.uv;
                found = true;
            }
        }

        return found ? bestUv : uv;
    }

    private void EnsureSelectionIsValid()
    {
        if (_regionSet == null)
            return;

        if (SelectedFace == null)
            _selectedFaceId = _regionSet.Faces.Count > 0 ? _regionSet.Faces[0].id : null;

        if (!string.IsNullOrWhiteSpace(_selectedVertexId) && _regionSet.GetVertexById(_selectedVertexId) == null)
            _selectedVertexId = null;
    }

    private void HandleKeyboard()
    {
        Event e = Event.current;
        if (e == null)
            return;

        if (_draggingVertex && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
        {
            _draggingVertex = false;
            _dragVertexId = null;
            e.Use();
            return;
        }

        if (_panning && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 2)
        {
            _panning = false;
            e.Use();
            return;
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            if (_cutActive)
            {
                CancelCutInternal(removePendingFace: true);
                EditorUtility.SetDirty(_regionSet);
                Repaint();
                e.Use();
            }
        }
    }

    private static void DrawLoopPolyline(Rect drawRect, List<Vector2> loopUv, float width)
    {
        if (loopUv == null || loopUv.Count < 2)
            return;

        List<Vector3> pts = new List<Vector3>(loopUv.Count + 1);
        for (int i = 0; i < loopUv.Count; i++)
            pts.Add(UvToGui(drawRect, loopUv[i]));

        pts.Add(UvToGui(drawRect, loopUv[0]));
        Handles.DrawAAPolyLine(width, pts.ToArray());
    }

    private void DrawTriangulatedFaceFill(Rect drawRect, List<Vector2> polygonUv, Color color)
    {
        if (polygonUv == null || polygonUv.Count < 3)
            return;

        List<int> tris = TriangulatePolygon(polygonUv);
        if (tris == null || tris.Count < 3)
            return;

        Handles.BeginGUI();
        Handles.color = color;

        for (int i = 0; i + 2 < tris.Count; i += 3)
        {
            Vector3 a = UvToGui(drawRect, polygonUv[tris[i]]);
            Vector3 b = UvToGui(drawRect, polygonUv[tris[i + 1]]);
            Vector3 c = UvToGui(drawRect, polygonUv[tris[i + 2]]);

            Handles.DrawAAConvexPolygon(a, b, c);
        }

        Handles.EndGUI();
    }

    private List<int> TriangulatePolygon(List<Vector2> polygon)
    {
        List<int> result = new List<int>();
        if (polygon == null || polygon.Count < 3)
            return result;

        List<int> indices = new List<int>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++)
            indices.Add(i);

        bool isClockwise = MapRegionUtility.ComputeSignedArea(polygon) < 0f;

        int guard = 0;
        while (indices.Count > 3 && guard < 4096)
        {
            guard++;
            bool earFound = false;

            for (int i = 0; i < indices.Count; i++)
            {
                int prev = indices[(i - 1 + indices.Count) % indices.Count];
                int curr = indices[i];
                int next = indices[(i + 1) % indices.Count];

                Vector2 a = polygon[prev];
                Vector2 b = polygon[curr];
                Vector2 c = polygon[next];

                if (!IsEar(a, b, c, polygon, indices, prev, curr, next, isClockwise))
                    continue;

                result.Add(prev);
                result.Add(curr);
                result.Add(next);
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
                break;
        }

        if (indices.Count == 3)
        {
            result.Add(indices[0]);
            result.Add(indices[1]);
            result.Add(indices[2]);
        }

        return result;
    }

    private bool IsEar(
        Vector2 a,
        Vector2 b,
        Vector2 c,
        List<Vector2> polygon,
        List<int> activeIndices,
        int prev,
        int curr,
        int next,
        bool isClockwise)
    {
        float cross = Cross(b - a, c - b);
        if (isClockwise ? cross > 0f : cross < 0f)
            return false;

        for (int i = 0; i < activeIndices.Count; i++)
        {
            int idx = activeIndices[i];
            if (idx == prev || idx == curr || idx == next)
                continue;

            if (PointInTriangle(polygon[idx], a, b, c))
                return false;
        }

        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
        bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);

        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static Vector3[] ConvertUvToGui(Rect drawRect, List<Vector2> pts)
    {
        Vector3[] result = new Vector3[pts.Count];
        for (int i = 0; i < pts.Count; i++)
            result[i] = UvToGui(drawRect, pts[i]);

        return result;
    }

    private static Rect GetContainedRect(Rect outer, Texture2D tex)
    {
        if (tex == null || tex.width <= 0 || tex.height <= 0)
            return outer;

        float texAspect = tex.width / (float)tex.height;
        float outerAspect = outer.width / Mathf.Max(1f, outer.height);

        if (outerAspect > texAspect)
        {
            float width = outer.height * texAspect;
            float x = outer.x + (outer.width - width) * 0.5f;
            return new Rect(x, outer.y, width, outer.height);
        }
        else
        {
            float height = outer.width / texAspect;
            float y = outer.y + (outer.height - height) * 0.5f;
            return new Rect(outer.x, y, outer.width, height);
        }
    }

    private static bool IsInsideImage(Rect rect, Vector2 p)
    {
        return rect.Contains(p);
    }

    private static Vector2 GuiToUv(Rect rect, Vector2 gui)
    {
        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, gui.x);
        float v = Mathf.InverseLerp(rect.yMax, rect.yMin, gui.y);
        return new Vector2(u, v);
    }

    private static Vector3 UvToGui(Rect rect, Vector2 uv)
    {
        float x = Mathf.Lerp(rect.xMin, rect.xMax, uv.x);
        float y = Mathf.Lerp(rect.yMax, rect.yMin, uv.y);
        return new Vector3(x, y, 0f);
    }

    private static Vector2 Clamp01(Vector2 v)
    {
        v.x = Mathf.Clamp01(v.x);
        v.y = Mathf.Clamp01(v.y);
        return v;
    }

    private static Vector2 GetNearestBorderGui(Rect rect, Vector2 rawMouse)
    {
        float left = Mathf.Abs(rawMouse.x - rect.xMin);
        float right = Mathf.Abs(rawMouse.x - rect.xMax);
        float top = Mathf.Abs(rawMouse.y - rect.yMin);
        float bottom = Mathf.Abs(rawMouse.y - rect.yMax);

        float min = Mathf.Min(left, right, top, bottom);

        if (min == left)
            return new Vector2(rect.xMin, Mathf.Clamp(rawMouse.y, rect.yMin, rect.yMax));
        if (min == right)
            return new Vector2(rect.xMax, Mathf.Clamp(rawMouse.y, rect.yMin, rect.yMax));
        if (min == top)
            return new Vector2(Mathf.Clamp(rawMouse.x, rect.xMin, rect.xMax), rect.yMin);

        return new Vector2(Mathf.Clamp(rawMouse.x, rect.xMin, rect.xMax), rect.yMax);
    }

    private static int GetNearestBorderEdge(Rect rect, Vector2 gui)
    {
        float left = Mathf.Abs(gui.x - rect.xMin);
        float right = Mathf.Abs(gui.x - rect.xMax);
        float top = Mathf.Abs(gui.y - rect.yMin);
        float bottom = Mathf.Abs(gui.y - rect.yMax);

        float min = Mathf.Min(left, right, top, bottom);

        if (min == top) return 0;    // top edge: (0,0)->(1,0)
        if (min == right) return 1;  // right edge
        if (min == bottom) return 2; // bottom edge
        return 3;                    // left edge
    }

    private int FindNearestBorderEdgeForUv(Vector2 uv)
    {
        float top = Mathf.Abs(uv.y - 0f);
        float right = Mathf.Abs(uv.x - 1f);
        float bottom = Mathf.Abs(uv.y - 1f);
        float left = Mathf.Abs(uv.x - 0f);

        float min = Mathf.Min(top, right, bottom, left);

        if (min == top) return 0;
        if (min == right) return 1;
        if (min == bottom) return 2;
        return 3;
    }

    private static Vector2 NudgeBorderUvInward(Vector2 uv)
    {
        const float eps = 0.0005f;

        if (Mathf.Approximately(uv.x, 0f))
            uv.x = eps;
        else if (Mathf.Approximately(uv.x, 1f))
            uv.x = 1f - eps;

        if (Mathf.Approximately(uv.y, 0f))
            uv.y = eps;
        else if (Mathf.Approximately(uv.y, 1f))
            uv.y = 1f - eps;

        return uv;
    }

    private static bool IsUvOnImageBorder(Vector2 uv)
    {
        return Mathf.Approximately(uv.x, 0f) ||
               Mathf.Approximately(uv.x, 1f) ||
               Mathf.Approximately(uv.y, 0f) ||
               Mathf.Approximately(uv.y, 1f);
    }

    private static bool IsEdgeOnImageBorder(Vector2 a, Vector2 b)
    {
        return (Mathf.Approximately(a.x, 0f) && Mathf.Approximately(b.x, 0f)) ||
               (Mathf.Approximately(a.x, 1f) && Mathf.Approximately(b.x, 1f)) ||
               (Mathf.Approximately(a.y, 0f) && Mathf.Approximately(b.y, 0f)) ||
               (Mathf.Approximately(a.y, 1f) && Mathf.Approximately(b.y, 1f));
    }

    private bool IsVertexOnRootBoundary(string vertexId)
    {
        MapRegionFace root = _regionSet.GetFaceById(MapRegionSet.RootFaceId);
        if (root == null || root.outerVertexIds == null)
            return false;

        return root.outerVertexIds.Contains(vertexId);
    }

    private static Vector2 ClampUvToNearestImageBorder(Vector2 uv)
    {
        float distLeft = Mathf.Abs(uv.x - 0f);
        float distRight = Mathf.Abs(uv.x - 1f);
        float distTop = Mathf.Abs(uv.y - 0f);
        float distBottom = Mathf.Abs(uv.y - 1f);

        float min = Mathf.Min(distLeft, distRight, distTop, distBottom);

        if (min == distTop)
            uv.y = 0f;
        else if (min == distRight)
            uv.x = 1f;
        else if (min == distBottom)
            uv.y = 1f;
        else
            uv.x = 0f;

        return uv;
    }

    private enum BorderLockMode
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 3,
        Bottom = 4,
        TopLeftCorner = 5,
        TopRightCorner = 6,
        BottomLeftCorner = 7,
        BottomRightCorner = 8
    }

    private bool TryGetRootBoundaryLock(string vertexId, out BorderLockMode lockMode)
    {
        lockMode = BorderLockMode.None;

        MapRegionFace root = _regionSet.GetFaceById(MapRegionSet.RootFaceId);
        if (root == null || root.outerVertexIds == null || root.outerVertexIds.Count < 2)
            return false;

        int index = root.outerVertexIds.IndexOf(vertexId);
        if (index < 0)
            return false;

        int prevIndex = (index - 1 + root.outerVertexIds.Count) % root.outerVertexIds.Count;
        int nextIndex = (index + 1) % root.outerVertexIds.Count;

        if (!_regionSet.TryGetVertexUv(root.outerVertexIds[prevIndex], out Vector2 prevUv) ||
            !_regionSet.TryGetVertexUv(root.outerVertexIds[index], out Vector2 thisUv) ||
            !_regionSet.TryGetVertexUv(root.outerVertexIds[nextIndex], out Vector2 nextUv))
            return false;

        bool onLeft = (Mathf.Approximately(thisUv.x, 0f) && Mathf.Approximately(prevUv.x, 0f)) ||
                      (Mathf.Approximately(thisUv.x, 0f) && Mathf.Approximately(nextUv.x, 0f));

        bool onRight = (Mathf.Approximately(thisUv.x, 1f) && Mathf.Approximately(prevUv.x, 1f)) ||
                       (Mathf.Approximately(thisUv.x, 1f) && Mathf.Approximately(nextUv.x, 1f));

        bool onTop = (Mathf.Approximately(thisUv.y, 0f) && Mathf.Approximately(prevUv.y, 0f)) ||
                     (Mathf.Approximately(thisUv.y, 0f) && Mathf.Approximately(nextUv.y, 0f));

        bool onBottom = (Mathf.Approximately(thisUv.y, 1f) && Mathf.Approximately(prevUv.y, 1f)) ||
                        (Mathf.Approximately(thisUv.y, 1f) && Mathf.Approximately(nextUv.y, 1f));

        if (onTop && onLeft) { lockMode = BorderLockMode.TopLeftCorner; return true; }
        if (onTop && onRight) { lockMode = BorderLockMode.TopRightCorner; return true; }
        if (onBottom && onLeft) { lockMode = BorderLockMode.BottomLeftCorner; return true; }
        if (onBottom && onRight) { lockMode = BorderLockMode.BottomRightCorner; return true; }
        if (onLeft) { lockMode = BorderLockMode.Left; return true; }
        if (onRight) { lockMode = BorderLockMode.Right; return true; }
        if (onTop) { lockMode = BorderLockMode.Top; return true; }
        if (onBottom) { lockMode = BorderLockMode.Bottom; return true; }

        return false;
    }

    private static Vector2 ApplyBorderLock(Vector2 uv, BorderLockMode lockMode)
    {
        switch (lockMode)
        {
            case BorderLockMode.Left:
                uv.x = 0f;
                break;
            case BorderLockMode.Right:
                uv.x = 1f;
                break;
            case BorderLockMode.Top:
                uv.y = 0f;
                break;
            case BorderLockMode.Bottom:
                uv.y = 1f;
                break;
            case BorderLockMode.TopLeftCorner:
                uv.x = 0f;
                uv.y = 0f;
                break;
            case BorderLockMode.TopRightCorner:
                uv.x = 1f;
                uv.y = 0f;
                break;
            case BorderLockMode.BottomLeftCorner:
                uv.x = 0f;
                uv.y = 1f;
                break;
            case BorderLockMode.BottomRightCorner:
                uv.x = 1f;
                uv.y = 1f;
                break;
        }

        return uv;
    }

    private MapRegionFace SelectedFace => _regionSet != null ? _regionSet.GetFaceById(_selectedFaceId) : null;
}
#endif