using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    // Kept in the generated project file's stream-view path; the active surface is the canvas view.
    public class PungentRichDocumentDocumentCanvasView : VisualElement
    {
        private const string IssueInvalidTokenSyntax = "INVALID_TOKEN_SYNTAX";
        private const string IssueUnknownToken = "UNKNOWN_TOKEN";

        private readonly VisualElement _documentRoot;
        private PungentRichDocument _document;
        private PungentRichDocumentCanvasDocument _canvas = new PungentRichDocumentCanvasDocument();
        private TextField _activeTextField;
        private int _editingSegmentIndex = -1;
        private int _activeSegmentIndex = -1;
        private int _editingTokenSegmentIndex = -1;
        private int _editingTokenStart = -1;
        private int _dragSegmentIndex = -1;
        private int _dragTargetIndex = -1;
        private int _dragPointerId = -1;
        private int _referenceBrowserSegmentIndex = -1;
        private int _referenceBrowserTokenStart = -1;
        private bool _referenceBrowserInSegmentText;
        private float _dragSelectionStartPanelY;
        private bool _editingTokenInSegmentText;
        private readonly List<VisualElement> _segmentRows = new List<VisualElement>();
        private readonly PungentRichDocumentTextSelectionState _selection = new PungentRichDocumentTextSelectionState();
        private readonly PungentRichDocumentMultiSelectionState _multiSelection = new PungentRichDocumentMultiSelectionState();
        private VisualElement _dragSelectionBox;
        private PungentRichDocumentBindingExpression _referenceBrowserExpression;

        public Action<string> OnDirty;
        public Action<string> OnStatus;
        public Action<string> OnTokenReferenced;
        public Action OnSelectionChanged;
        public Action OpenTokenValidator;
        public Action OpenDocumentationLinks;
        public Func<string> TokenKeyProvider;
        public int FontSize = 14;
        public bool ShowLineNumbers;
        public bool ShowAnnotationMarkers = true;
        public bool ShowSemanticTint = true;
        public bool ShowTokenTechnicalText;
        public bool CompactLineSpacing;
        public bool ShowRawSyntaxHints;
        public PungentRichDocumentTextSelectionState SelectionState => _selection.Copy();
        public string SelectedSemanticBindingId => _selection.semanticBindingId;
        public string SelectedAnnotationId => _selection.annotationId;
        public int ActiveSegmentIndex => _activeSegmentIndex;
        public IReadOnlyList<int> SelectedSegmentIndices => _multiSelection.Indices;

        public PungentRichDocumentDocumentCanvasView()
        {
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1f;

            _documentRoot = new VisualElement();
            _documentRoot.style.flexDirection = FlexDirection.Column;
            _documentRoot.style.flexGrow = 1f;
            _documentRoot.style.minHeight = 320;
            _documentRoot.style.position = Position.Relative;
            _documentRoot.RegisterCallback<MouseDownEvent>(HandleDocumentRootMouseDown);
            _documentRoot.RegisterCallback<PointerDownEvent>(HandleDocumentRootPointerDown);
            _documentRoot.RegisterCallback<PointerMoveEvent>(HandleDocumentRootPointerMove);
            _documentRoot.RegisterCallback<PointerUpEvent>(HandleDocumentRootPointerUp);
            Add(_documentRoot);
        }

        public void Bind(PungentRichDocument document)
        {
            _document = document;
            PungentRichDocumentSemanticBindingService.RefreshBindingsFromBody(_document);
            ReparseFromDocument();
            Refresh();
        }

        public void ReparseFromDocument()
        {
            string body = _document == null ? string.Empty : _document.bodyText ?? string.Empty;
            _canvas = PungentRichDocumentDocumentCanvasParser.Parse(body);
            if (_canvas.segments.Count == 0)
            {
                _canvas.segments.Add(new PungentRichDocumentCanvasSegment
                {
                    kind = PungentRichDocumentStreamNodeKind.Text,
                    rawText = string.Empty,
                    text = string.Empty
                });
                _editingSegmentIndex = 0;
                _activeSegmentIndex = 0;
            }
        }

        public void CommitBodyToDocument()
        {
            if (_document == null)
                return;

            CommitActiveFieldValue();
            _document.bodyText = _canvas.Serialize();
            PungentRichDocumentSemanticBindingService.RefreshBindingsFromBody(_document);
        }

        public void InsertRawSnippet(string rawSnippet)
        {
            if (_document == null)
                return;

            string snippet = rawSnippet ?? string.Empty;
            if (!LooksLikeBlockSnippet(snippet) && TryInsertIntoActiveText(snippet))
            {
                CommitCanvas("Inserted text.", false);
                return;
            }

            CommitActiveFieldValue();
            List<PungentRichDocumentCanvasSegment> segments = PungentRichDocumentDocumentCanvasParser.Parse(snippet).segments;
            if (segments.Count == 0)
                segments.Add(new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Text, rawText = snippet, text = snippet });

            InsertSegmentsAtActivePosition(segments, "Inserted document content.");
        }

        public void InsertIntegration(PungentRichDocumentStreamNodeKind kind, string text = null)
        {
            if (_document == null)
                return;

            PungentRichDocumentSemanticKind semanticKind = SemanticKindForStreamKind(kind);
            if (semanticKind != PungentRichDocumentSemanticKind.None && TryConvertSelectionToSemantic(semanticKind))
                return;

            CommitActiveFieldValue();
            InsertSegmentsAtActivePosition(new[]
            {
                PungentRichDocumentDocumentCanvasParser.CreateIntegration(kind, text)
            }, "Inserted " + IntegrationLabel(kind) + ".");
        }

        public void InsertCustomInsertion(PungentRichDocumentInsertionDefinition definition)
        {
            if (_document == null || definition == null)
                return;

            CommitActiveFieldValue();
            InsertSegmentsAtActivePosition(new[]
            {
                PungentRichDocumentDocumentCanvasParser.CreateCustomInsertion(definition)
            }, "Inserted " + definition.displayName + ".");
        }

        public void ConvertSelectionToSemantic(PungentRichDocumentSemanticKind kind)
        {
            TryConvertSelectionToSemantic(kind);
        }

        public void ConvertSelectionToCustomInsertion(PungentRichDocumentInsertionDefinition definition)
        {
            TryConvertSelectionToCustomInsertion(definition);
        }

        public void AddCommentAtActiveLine()
        {
            AddAnnotationAtActiveSegment(PungentRichDocumentAnnotationKind.Comment);
        }

        public void AddBookmarkAtActiveLine()
        {
            AddAnnotationAtActiveSegment(PungentRichDocumentAnnotationKind.Bookmark);
        }

        public void MoveActiveSegment(int direction)
        {
            int index = _activeSegmentIndex;
            if (index < 0 && _selection.HasSegment)
                index = _selection.segmentIndex;
            if (index < 0)
            {
                OnStatus?.Invoke("Select a document line before moving it.");
                return;
            }

            MoveSegment(index, index + Math.Sign(direction));
        }

        public void InsertToken(string key)
        {
            string clean = PungentRichDocumentParser.NormalizeTokenKey(key);
            if (string.IsNullOrWhiteSpace(clean))
                clean = "tokenKey";

            if (_document == null)
                return;

            string tokenText = "{" + clean + "}";
            if (TryInsertIntoActiveText(tokenText))
            {
                OnTokenReferenced?.Invoke(clean);
                CommitCanvas("Inserted token " + tokenText + ".", true);
                return;
            }

            InsertRawSnippet(tokenText);
            OnTokenReferenced?.Invoke(clean);
        }

        public void ApplyBold()
        {
            ApplyInlineStyle("**", "bold text", "Applied bold styling.");
        }

        public void ApplyItalic()
        {
            ApplyInlineStyle("_", "italic text", "Applied italic styling.");
        }

        public void ApplyHeading(int level)
        {
            ApplyLineFormat(PungentRichDocumentStreamNodeKind.Heading, Mathf.Clamp(level, 1, 6));
        }

        public void ApplyQuote()
        {
            ApplyLineFormat(PungentRichDocumentStreamNodeKind.Quote, 0);
        }

        public void ApplyCodeBlock()
        {
            ApplyLineFormat(PungentRichDocumentStreamNodeKind.Code, 0);
        }

        public void ApplyDivider()
        {
            ApplyLineFormat(PungentRichDocumentStreamNodeKind.Divider, 0);
        }

        public void CreateCustomInsertionFromSelection()
        {
            if (_document == null)
                return;

            CommitActiveFieldValue();
            List<PungentRichDocumentCanvasSegment> segments = _multiSelection.Indices
                .Where(index => index >= 0 && index < _canvas.segments.Count)
                .Select(index => _canvas.segments[index])
                .Where(segment => segment != null)
                .ToList();

            bool hasTextSelection = _selection.HasTextSelection;
            if (segments.Count == 0 && _selection.HasSegment && _selection.segmentIndex >= 0 && _selection.segmentIndex < _canvas.segments.Count)
                segments.Add(_canvas.segments[_selection.segmentIndex]);

            PungentRichDocumentInsertionDefinition seed = PungentRichDocumentQuickInsertionCapture.CreateDefinition(
                segments,
                hasTextSelection ? _selection.selectedText : string.Empty,
                hasTextSelection || segments.Count <= 1);
            PungentRichDocumentInsertionDesignerWindow.OpenWithDefinition(seed);
            OnStatus?.Invoke("Captured the selection as a custom insertion draft.");
        }

        private void Refresh()
        {
            _documentRoot.Clear();
            _segmentRows.Clear();
            _activeTextField = null;

            if (_document == null)
            {
                _documentRoot.Add(CreateMutedLabel("No document selected."));
                return;
            }

            if (_canvas.segments.Count == 0)
                ReparseFromDocument();

            for (int i = 0; i < _canvas.segments.Count; i++)
                DrawSegment(i, _canvas.segments[i]);
        }

        private void DrawSegment(int index, PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return;

            VisualElement content;
            if (_editingSegmentIndex == index && segment.IsPlainTextEditable)
            {
                content = CreatePlainTextEditor(index, segment);
                AddSegmentElement(index, segment, content);
                return;
            }

            if (_editingSegmentIndex == index && IsInlineSemanticSegment(segment))
            {
                content = CreateInlineSemanticEditor(index, segment);
                AddSegmentElement(index, segment, content);
                return;
            }

            if (segment.semanticKind == PungentRichDocumentSemanticKind.CustomInsertion)
            {
                content = CreateCustomInsertionEditor(index, segment);
                AddSegmentElement(index, segment, content);
                return;
            }

            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.Blank:
                    content = CreateBlankSpace(index);
                    break;
                case PungentRichDocumentStreamNodeKind.Heading:
                    content = CreateTokenizedParagraph(index, segment, segment.text, true, true);
                    break;
                case PungentRichDocumentStreamNodeKind.Quote:
                    content = CreateQuote(index, segment);
                    break;
                case PungentRichDocumentStreamNodeKind.Divider:
                    content = CreateDivider(index, segment);
                    break;
                case PungentRichDocumentStreamNodeKind.Code:
                    content = CreateEmbeddedIntegration(index, segment);
                    break;
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                case PungentRichDocumentStreamNodeKind.TutorialStep:
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    content = CreateInlineSemanticIntegration(index, segment);
                    break;
                default:
                    content = CreateTokenizedParagraph(index, segment, segment.rawText, false, false);
                    break;
            }

            AddSegmentElement(index, segment, content);
        }

        private void AddSegmentElement(int index, PungentRichDocumentCanvasSegment segment, VisualElement content)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.marginBottom = 1;
            row.style.borderTopWidth = 0;
            row.style.borderTopColor = new StyleColor(new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0f));
            row.style.backgroundColor = new StyleColor(RowBackground(index));
            row.userData = index;
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                _activeSegmentIndex = index;
                if (evt.ctrlKey || evt.commandKey)
                {
                    _multiSelection.Toggle(index);
                    _selection.SetTextSelection(index, 0, 0, segment == null ? string.Empty : segment.ReadableText());
                    OnSelectionChanged?.Invoke();
                    RefreshActiveRowStyles();
                    evt.StopPropagation();
                    return;
                }

                if (evt.shiftKey)
                {
                    int anchor = _multiSelection.anchorIndex >= 0 ? _multiSelection.anchorIndex : _activeSegmentIndex;
                    _multiSelection.SetRange(anchor, index);
                    _selection.SetTextSelection(index, 0, 0, segment == null ? string.Empty : segment.ReadableText());
                    OnSelectionChanged?.Invoke();
                    RefreshActiveRowStyles();
                    evt.StopPropagation();
                    return;
                }

                _multiSelection.SetSingle(index);
                if (!_selection.HasTextSelection && !_selection.HasSemanticSelection && !_selection.HasAnnotationSelection)
                    _selection.SetTextSelection(index, 0, 0, segment == null ? string.Empty : segment.ReadableText());
                OnSelectionChanged?.Invoke();
                RefreshActiveRowStyles();
            });

            VisualElement gutter = CreateSegmentGutter(index, segment);
            UpdateGutterVisibility(gutter, index, segment, false);
            row.RegisterCallback<PointerEnterEvent>(_ => UpdateGutterVisibility(gutter, index, segment, true));
            row.RegisterCallback<PointerLeaveEvent>(_ => UpdateGutterVisibility(gutter, index, segment, false));
            row.Add(gutter);

            VisualElement body = new VisualElement();
            body.style.flexGrow = 1f;
            body.style.flexDirection = FlexDirection.Column;
            body.Add(content ?? CreateMutedLabel(string.Empty));
            AddReferenceBrowserIfNeeded(body, index);
            row.Add(body);

            _segmentRows.Add(row);
            _documentRoot.Add(row);
        }

        private VisualElement CreateSegmentGutter(int index, PungentRichDocumentCanvasSegment segment)
        {
            VisualElement gutter = new VisualElement();
            gutter.style.width = ShowLineNumbers ? 38 : 28;
            gutter.style.flexShrink = 0;
            gutter.style.marginRight = 8;
            gutter.style.alignItems = Align.Center;
            gutter.style.paddingTop = 1;
            gutter.style.paddingBottom = 1;
            gutter.tooltip = "Line handle: click to target insertions, drag to reorder, right-click for line actions.";

            bool active = index == _activeSegmentIndex;
            Label handle = new Label(ShowLineNumbers ? (active ? ">" : (index + 1).ToString()) : (active ? ">" : "::"));
            handle.tooltip = "Click to target this line. Drag up or down to reorder this document segment.";
            handle.style.width = ShowLineNumbers ? 32 : 22;
            handle.style.height = 20;
            handle.style.fontSize = 10;
            handle.style.unityTextAlign = TextAnchor.MiddleCenter;
            handle.style.marginBottom = 2;
            handle.style.borderTopLeftRadius = 3;
            handle.style.borderTopRightRadius = 3;
            handle.style.borderBottomLeftRadius = 3;
            handle.style.borderBottomRightRadius = 3;
            handle.style.backgroundColor = new StyleColor(active ? new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.55f) : new Color(0f, 0f, 0f, 0f));
            handle.style.color = new StyleColor(active ? Color.white : (EditorGUIUtility.isProSkin ? new Color(0.58f, 0.6f, 0.62f) : new Color(0.42f, 0.43f, 0.44f)));
            handle.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                _activeSegmentIndex = index;
                _selection.SetTextSelection(index, 0, 0, segment == null ? string.Empty : segment.ReadableText());
                OnSelectionChanged?.Invoke();
                RefreshActiveRowStyles();
                evt.StopPropagation();
            });
            handle.RegisterCallback<PointerDownEvent>(evt => BeginSegmentDrag(index, handle, evt));
            handle.RegisterCallback<PointerMoveEvent>(evt => UpdateSegmentDrag(handle, evt));
            handle.RegisterCallback<PointerUpEvent>(evt => CompleteSegmentDrag(handle, evt));
            handle.RegisterCallback<PointerCancelEvent>(evt => CancelSegmentDrag(handle, evt.pointerId));
            handle.AddManipulator(new ContextualMenuManipulator(evt => BuildLineHandleContextMenu(evt.menu, index, segment)));
            gutter.Add(handle);

            if (ShowAnnotationMarkers)
            {
                foreach (PungentRichDocumentAnnotation annotation in GetAnnotationsForSegment(index, segment))
                    gutter.Add(CreateAnnotationMarker(index, segment, annotation));
            }

            return gutter;
        }

        private void AddReferenceBrowserIfNeeded(VisualElement body, int index)
        {
            if (body == null ||
                index != _referenceBrowserSegmentIndex ||
                _referenceBrowserExpression == null ||
                _referenceBrowserTokenStart < 0)
                return;

            PungentRichDocumentReferenceBrowserView browser = new PungentRichDocumentReferenceBrowserView();
            browser.Bind(
                _document,
                _referenceBrowserExpression,
                (endpoint, tokenText) => CommitReferenceEndpoint(index, _referenceBrowserExpression, endpoint, tokenText, _referenceBrowserInSegmentText),
                OnStatus,
                () =>
                {
                    _referenceBrowserSegmentIndex = -1;
                    _referenceBrowserTokenStart = -1;
                    _referenceBrowserExpression = null;
                    Refresh();
                });
            body.Add(browser);
        }

        private void UpdateGutterVisibility(VisualElement gutter, int index, PungentRichDocumentCanvasSegment segment, bool hover)
        {
            if (gutter == null)
                return;

            bool active = index == _activeSegmentIndex;
            bool dragging = _dragSegmentIndex == index;
            bool hasAnnotations = ShowAnnotationMarkers && GetAnnotationsForSegment(index, segment).Any();
            gutter.style.opacity = hover || active || dragging || ShowLineNumbers || hasAnnotations ? 1f : 0f;
        }

        private void BeginSegmentDrag(int index, VisualElement handle, PointerDownEvent evt)
        {
            if (evt.button != 0 || handle == null || index < 0 || index >= _canvas.segments.Count)
                return;

            CommitActiveFieldValue();
            _dragSegmentIndex = index;
            _dragTargetIndex = index;
            _dragPointerId = evt.pointerId;
            _activeSegmentIndex = index;
            handle.CapturePointer(evt.pointerId);
            RefreshActiveRowStyles();
            RefreshDragDropIndicator();
            OnStatus?.Invoke("Drag the line handle up or down to reorder this document segment.");
            evt.StopPropagation();
        }

        private void UpdateSegmentDrag(VisualElement handle, PointerMoveEvent evt)
        {
            if (handle == null || _dragPointerId != evt.pointerId || _dragSegmentIndex < 0 || !handle.HasPointerCapture(evt.pointerId))
                return;

            _dragTargetIndex = SegmentIndexFromPanelY(evt.position.y);
            RefreshDragDropIndicator();
            evt.StopPropagation();
        }

        private void CompleteSegmentDrag(VisualElement handle, PointerUpEvent evt)
        {
            if (handle == null || _dragPointerId != evt.pointerId)
                return;

            if (handle.HasPointerCapture(evt.pointerId))
                handle.ReleasePointer(evt.pointerId);

            int from = _dragSegmentIndex;
            int to = _dragTargetIndex;
            _dragSegmentIndex = -1;
            _dragTargetIndex = -1;
            _dragPointerId = -1;
            RefreshDragDropIndicator();

            if (from >= 0 && to >= 0 && from != to)
                MoveSegment(from, to);
            else if (from >= 0)
                Refresh();

            evt.StopPropagation();
        }

        private void CancelSegmentDrag(VisualElement handle, int pointerId)
        {
            if (handle != null && handle.HasPointerCapture(pointerId))
                handle.ReleasePointer(pointerId);
            _dragSegmentIndex = -1;
            _dragTargetIndex = -1;
            _dragPointerId = -1;
            RefreshDragDropIndicator();
        }

        private void HandleDocumentRootPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !(evt.ctrlKey || evt.commandKey) || _segmentRows.Count == 0)
                return;

            CommitActiveFieldValue();
            _dragPointerId = evt.pointerId;
            _dragSelectionStartPanelY = evt.position.y;
            int start = SegmentIndexFromPanelY(evt.position.y);
            _multiSelection.BeginDrag(start);
            _activeSegmentIndex = start;
            _documentRoot.CapturePointer(evt.pointerId);
            ShowDragSelectionBox(evt.position.y, evt.position.y);
            RefreshActiveRowStyles();
            OnSelectionChanged?.Invoke();
            evt.StopPropagation();
        }

        private void HandleDocumentRootPointerMove(PointerMoveEvent evt)
        {
            if (!_multiSelection.isDragging || _dragPointerId != evt.pointerId || !_documentRoot.HasPointerCapture(evt.pointerId))
                return;

            _multiSelection.UpdateDrag(SegmentIndexFromPanelY(evt.position.y));
            ShowDragSelectionBox(_dragSelectionStartPanelY, evt.position.y);
            RefreshActiveRowStyles();
            OnSelectionChanged?.Invoke();
            evt.StopPropagation();
        }

        private void HandleDocumentRootPointerUp(PointerUpEvent evt)
        {
            if (!_multiSelection.isDragging || _dragPointerId != evt.pointerId)
                return;

            if (_documentRoot.HasPointerCapture(evt.pointerId))
                _documentRoot.ReleasePointer(evt.pointerId);

            _multiSelection.EndDrag();
            _dragPointerId = -1;
            HideDragSelectionBox();
            OnStatus?.Invoke("Selected " + _multiSelection.Count + " document element(s).");
            evt.StopPropagation();
        }

        private void ShowDragSelectionBox(float startPanelY, float currentPanelY)
        {
            if (_dragSelectionBox == null)
            {
                _dragSelectionBox = new VisualElement();
                _dragSelectionBox.style.position = Position.Absolute;
                _dragSelectionBox.style.left = 24;
                _dragSelectionBox.style.right = 4;
                _dragSelectionBox.style.borderTopWidth = 1;
                _dragSelectionBox.style.borderRightWidth = 1;
                _dragSelectionBox.style.borderBottomWidth = 1;
                _dragSelectionBox.style.borderLeftWidth = 1;
                _dragSelectionBox.style.borderTopColor = new StyleColor(UtilityWindowTheme.Blue);
                _dragSelectionBox.style.borderRightColor = new StyleColor(UtilityWindowTheme.Blue);
                _dragSelectionBox.style.borderBottomColor = new StyleColor(UtilityWindowTheme.Blue);
                _dragSelectionBox.style.borderLeftColor = new StyleColor(UtilityWindowTheme.Blue);
                _dragSelectionBox.style.backgroundColor = new StyleColor(new Color(UtilityWindowTheme.Blue.r, UtilityWindowTheme.Blue.g, UtilityWindowTheme.Blue.b, 0.08f));
                _documentRoot.Add(_dragSelectionBox);
            }

            Vector2 start = _documentRoot.WorldToLocal(new Vector2(0f, startPanelY));
            Vector2 current = _documentRoot.WorldToLocal(new Vector2(0f, currentPanelY));
            float top = Mathf.Min(start.y, current.y);
            float height = Mathf.Max(10f, Mathf.Abs(current.y - start.y));
            _dragSelectionBox.style.top = top;
            _dragSelectionBox.style.height = height;
            _dragSelectionBox.BringToFront();
        }

        private void HideDragSelectionBox()
        {
            if (_dragSelectionBox == null)
                return;

            _dragSelectionBox.RemoveFromHierarchy();
            _dragSelectionBox = null;
        }

        private void RefreshActiveRowStyles()
        {
            foreach (VisualElement row in _segmentRows)
            {
                if (row == null || !(row.userData is int))
                    continue;

                int index = (int)row.userData;
                row.style.backgroundColor = new StyleColor(RowBackground(index));
            }
        }

        private Color RowBackground(int index)
        {
            if (_multiSelection.Contains(index))
                return new Color(UtilityWindowTheme.Blue.r, UtilityWindowTheme.Blue.g, UtilityWindowTheme.Blue.b, 0.11f);
            if (index == _activeSegmentIndex)
                return new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.07f);
            return new Color(0f, 0f, 0f, 0f);
        }

        private void RefreshDragDropIndicator()
        {
            for (int i = 0; i < _segmentRows.Count; i++)
            {
                VisualElement row = _segmentRows[i];
                if (row == null)
                    continue;

                bool target = _dragSegmentIndex >= 0 && i == _dragTargetIndex;
                row.style.borderTopWidth = target ? 2 : 0;
                row.style.borderTopColor = new StyleColor(new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, target ? 0.95f : 0f));
            }
        }

        private int SegmentIndexFromPanelY(float panelY)
        {
            if (_segmentRows.Count == 0)
                return Mathf.Clamp(_dragSegmentIndex, 0, Math.Max(0, _canvas.segments.Count - 1));

            for (int i = 0; i < _segmentRows.Count; i++)
            {
                VisualElement row = _segmentRows[i];
                if (row == null)
                    continue;

                Rect bounds = row.worldBound;
                if (panelY <= bounds.y + bounds.height * 0.5f)
                    return Mathf.Clamp(i, 0, _canvas.segments.Count - 1);
            }

            return Mathf.Clamp(_segmentRows.Count - 1, 0, Math.Max(0, _canvas.segments.Count - 1));
        }

        private Button CreateAnnotationMarker(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentAnnotation annotation)
        {
            bool stale = annotation != null && annotation.IsStaleForSource(segment == null ? string.Empty : segment.ToRawText());
            Color tint = annotation != null && annotation.kind == PungentRichDocumentAnnotationKind.Bookmark ? UtilityWindowTheme.Blue : UtilityWindowTheme.Amber;
            if (stale)
                tint = UtilityWindowTheme.Neutral;

            Button marker = new Button(() => SelectAnnotation(index, segment, annotation))
            {
                text = annotation != null && annotation.kind == PungentRichDocumentAnnotationKind.Bookmark ? "B" : "C",
                tooltip = BuildAnnotationTooltip(annotation, stale)
            };
            marker.style.width = 24;
            marker.style.height = 18;
            marker.style.fontSize = 9;
            marker.style.marginTop = 1;
            marker.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, annotation != null && annotation.resolved ? 0.35f : 0.78f));
            marker.style.color = Color.white;
            return marker;
        }

        private TextField CreatePlainTextEditor(int index, PungentRichDocumentCanvasSegment segment)
        {
            TextField field = new TextField
            {
                value = EditableValue(segment),
                multiline = true
            };
            field.tooltip = "Enter creates a new paragraph. Shift+Enter keeps a line break.";
            field.style.minHeight = segment.kind == PungentRichDocumentStreamNodeKind.Heading ? 36 : 30;
            field.style.marginTop = 0;
            field.style.marginBottom = CompactLineSpacing ? (segment.kind == PungentRichDocumentStreamNodeKind.Heading ? 5 : 2) : (segment.kind == PungentRichDocumentStreamNodeKind.Heading ? 8 : 5);
            field.style.fontSize = segment.kind == PungentRichDocumentStreamNodeKind.Heading ? Mathf.Max(FontSize + 7, 21) : FontSize;
            field.style.unityFontStyleAndWeight = segment.kind == PungentRichDocumentStreamNodeKind.Heading ? FontStyle.Bold : FontStyle.Normal;
            field.style.whiteSpace = WhiteSpace.Normal;
            field.RegisterCallback<FocusInEvent>(_ =>
            {
                _activeTextField = field;
                _activeSegmentIndex = index;
                CaptureTextSelection(index, field);
            });
            field.RegisterValueChangedCallback(evt =>
            {
                ApplyEditableValue(segment, evt.newValue ?? string.Empty);
                CaptureTextSelection(index, field);
                if (IsReferenceDraft(evt.newValue))
                    OnStatus?.Invoke("Reference draft detected. Finish the token with } and click the chip to refine object, component, and field.");
                OnDirty?.Invoke("Unsaved changes");
            });
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                CaptureTextSelection(index, field);
                CommitActiveFieldValue();
                _editingSegmentIndex = -1;
                CommitCanvas("Unsaved changes.", true);
            });
            field.RegisterCallback<KeyUpEvent>(_ => CaptureTextSelection(index, field));
            field.RegisterCallback<MouseUpEvent>(_ => CaptureTextSelection(index, field));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !evt.shiftKey)
                {
                    SplitTextEditorAtCursor(index, segment, field);
                    evt.StopPropagation();
                }
            });
            field.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                CaptureTextSelection(index, field);
                BuildTextContextMenu(evt.menu, index, segment);
            }));
            field.schedule.Execute(() =>
            {
                _activeTextField = field;
                field.Focus();
                MoveFieldCursorToEnd(field);
            });
            return field;
        }

        private VisualElement CreateTokenizedParagraph(int index, PungentRichDocumentCanvasSegment segment, string text, bool heading, bool tokenRunsInSegmentText)
        {
            VisualElement paragraph = new VisualElement();
            paragraph.style.flexDirection = FlexDirection.Row;
            paragraph.style.flexWrap = Wrap.Wrap;
            paragraph.style.alignItems = Align.Center;
            paragraph.style.minHeight = heading ? 34 : 25;
            paragraph.style.marginTop = heading ? 8 : 0;
            paragraph.style.marginBottom = CompactLineSpacing ? (heading ? 5 : 2) : (heading ? 7 : 5);
            paragraph.style.paddingTop = 1;
            paragraph.style.paddingBottom = 1;
            paragraph.tooltip = "Click to edit. Right-click for text and integration actions.";
            paragraph.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                _activeSegmentIndex = index;
                _editingSegmentIndex = index;
                Refresh();
            });
            paragraph.AddManipulator(new ContextualMenuManipulator(evt => BuildTextContextMenu(evt.menu, index, segment)));

            if (!heading && segment != null && segment.scriptLineKind != PungentRichDocumentScriptLineKind.None)
                paragraph.Add(CreateScriptLineMarker(segment.scriptLineKind));

            string safeText = text ?? string.Empty;
            foreach (PungentRichDocumentTextRun run in PungentRichDocumentSemanticParser.ExtractRuns(safeText))
            {
                if (run == null)
                    continue;

                if (run.kind == PungentRichDocumentTextRunKind.Semantic)
                {
                    paragraph.Add(CreateSemanticInlineSpan(index, segment, run.semantic));
                    continue;
                }

                if (run.kind == PungentRichDocumentTextRunKind.Token && run.token != null)
                {
                    bool editingThisToken =
                        _editingTokenSegmentIndex == index &&
                        _editingTokenStart == run.token.startIndex &&
                        _editingTokenInSegmentText == tokenRunsInSegmentText;

                    paragraph.Add(editingThisToken
                        ? CreateTokenEditField(index, segment, run.token, tokenRunsInSegmentText)
                        : CreateTokenChip(index, segment, run.token, tokenRunsInSegmentText));
                    continue;
                }

                AddStyledTextSegments(paragraph, run.text, heading);
            }

            if (safeText.Length == 0)
                paragraph.Add(CreateMutedLabel(index == 0 ? "Start writing..." : "Click to write..."));

            return paragraph;
        }

        private VisualElement CreateQuote(int index, PungentRichDocumentCanvasSegment segment)
        {
            VisualElement quote = CreateTokenizedParagraph(index, segment, segment.text, false, true);
            quote.style.borderLeftWidth = 3;
            quote.style.borderLeftColor = new StyleColor(UtilityWindowTheme.Blue);
            quote.style.paddingLeft = 10;
            quote.style.marginTop = CompactLineSpacing ? 2 : 5;
            quote.style.marginBottom = CompactLineSpacing ? 3 : 7;
            return quote;
        }

        private VisualElement CreateBlankSpace(int index)
        {
            VisualElement blank = new VisualElement();
            blank.style.minHeight = CompactLineSpacing ? 10 : 17;
            blank.style.marginBottom = 1;
            blank.tooltip = "Click to write here.";
            blank.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                _activeSegmentIndex = index;
                _editingSegmentIndex = index;
                Refresh();
            });
            blank.AddManipulator(new ContextualMenuManipulator(evt => BuildTextContextMenu(evt.menu, index, _canvas.segments[index])));
            return blank;
        }

        private VisualElement CreateDivider(int index, PungentRichDocumentCanvasSegment segment)
        {
            VisualElement divider = new VisualElement();
            divider.style.height = 20;
            divider.style.justifyContent = Justify.Center;
            divider.style.marginTop = 5;
            divider.style.marginBottom = 5;
            divider.AddManipulator(new ContextualMenuManipulator(evt => BuildTextContextMenu(evt.menu, index, segment)));

            VisualElement line = new VisualElement();
            line.style.height = 1;
            line.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.46f, 0.47f, 0.49f) : new Color(0.72f, 0.74f, 0.76f));
            divider.Add(line);
            return divider;
        }

        private VisualElement CreateInlineSemanticIntegration(int index, PungentRichDocumentCanvasSegment segment)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 26;
            row.style.marginTop = 2;
            row.style.marginBottom = CompactLineSpacing ? 2 : 5;
            row.tooltip = "Integrated text. Click to select, double-click to edit source, or right-click for actions.";
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                _activeSegmentIndex = index;
                SelectSemanticSegment(index, segment);
                if (evt.button == 0 && evt.clickCount > 1)
                {
                    _editingSegmentIndex = index;
                    Refresh();
                }
            });
            row.AddManipulator(new ContextualMenuManipulator(evt => BuildIntegrationContextMenu(evt.menu, index, segment)));

            PungentRichDocumentSemanticKind semanticKind = segment.semanticKind == PungentRichDocumentSemanticKind.None
                ? SemanticKindForStreamKind(segment.kind)
                : segment.semanticKind;
            PungentRichDocumentInsertionDefinition customDefinition = DefinitionForSegment(segment);
            Color tint = semanticKind == PungentRichDocumentSemanticKind.CustomInsertion
                ? PungentRichDocumentInsertionDefinitionRegistry.TintForDefinition(customDefinition, TintForSemanticKind(semanticKind))
                : TintForSemanticKind(semanticKind);
            if (!ShowSemanticTint)
                tint = UtilityWindowTheme.Neutral;

            Label marker = new Label(semanticKind == PungentRichDocumentSemanticKind.CustomInsertion && customDefinition != null
                ? customDefinition.displayName
                : PungentRichDocumentSemanticParser.DisplayName(semanticKind));
            marker.style.fontSize = Mathf.Max(10, FontSize - 3);
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            marker.style.color = Color.white;
            marker.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.78f));
            marker.style.paddingLeft = 6;
            marker.style.paddingRight = 6;
            marker.style.paddingTop = 2;
            marker.style.paddingBottom = 2;
            marker.style.marginRight = 5;
            marker.style.borderTopLeftRadius = 8;
            marker.style.borderTopRightRadius = 8;
            marker.style.borderBottomLeftRadius = 8;
            marker.style.borderBottomRightRadius = 8;
            row.Add(marker);

            if (segment.kind == PungentRichDocumentStreamNodeKind.DialogueLine && !string.IsNullOrWhiteSpace(segment.speaker))
            {
                Label speaker = CreateTextSegment(segment.speaker.Trim() + ": ", false);
                speaker.style.unityFontStyleAndWeight = FontStyle.Bold;
                speaker.style.color = tint;
                row.Add(speaker);
            }

            if (segment.kind == PungentRichDocumentStreamNodeKind.CommandPlaceholder)
            {
                row.Add(CreateTextSegment(string.IsNullOrWhiteSpace(segment.commandKey) ? segment.text : segment.commandKey, false));
            }
            else
            {
                AddInlineRuns(row, index, segment, segment.text, false, true);
            }

            string issueCode = SegmentIssueCode(segment);
            if (!string.IsNullOrWhiteSpace(issueCode) && !IsSuppressed(issueCode, segment.ToRawText()))
            {
                Button warning = new Button(() => SuppressIssue(issueCode, segment.ToRawText()))
                {
                    text = "!",
                    tooltip = WarningTooltip(issueCode) + " Click to ignore this warning for this source."
                };
                warning.style.width = 22;
                warning.style.height = 20;
                warning.style.marginLeft = 4;
                warning.style.backgroundColor = new StyleColor(UtilityWindowTheme.Amber);
                warning.style.color = Color.white;
                row.Add(warning);
            }

            row.style.borderBottomWidth = ShowSemanticTint ? 1 : 0;
            row.style.borderBottomColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.42f));
            return row;
        }

        private VisualElement CreateInlineSemanticEditor(int index, PungentRichDocumentCanvasSegment segment)
        {
            if (segment != null && segment.semanticKind == PungentRichDocumentSemanticKind.CustomInsertion)
                return CreateCustomInsertionEditor(index, segment);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.marginTop = 2;
            row.style.marginBottom = CompactLineSpacing ? 3 : 6;

            Color tint = TintForSemanticKind(segment.semanticKind == PungentRichDocumentSemanticKind.None ? SemanticKindForStreamKind(segment.kind) : segment.semanticKind);
            if (!ShowSemanticTint)
                tint = UtilityWindowTheme.Neutral;
            Label marker = new Label(IntegrationLabel(segment.kind));
            marker.style.width = 84;
            marker.style.marginRight = 6;
            marker.style.color = tint;
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(marker);

            if (segment.kind == PungentRichDocumentStreamNodeKind.DialogueLine)
            {
                TextField speaker = new TextField { value = segment.speaker ?? string.Empty };
                speaker.style.width = 112;
                speaker.style.marginRight = 6;
                speaker.RegisterValueChangedCallback(evt =>
                {
                    segment.speaker = evt.newValue ?? string.Empty;
                    SetSemanticField(segment, "speaker", segment.speaker);
                    OnDirty?.Invoke("Unsaved changes");
                });
                row.Add(speaker);
            }

            TextField text = new TextField
            {
                value = segment.kind == PungentRichDocumentStreamNodeKind.CommandPlaceholder ? segment.commandKey ?? string.Empty : segment.text ?? string.Empty,
                multiline = true
            };
            text.style.flexGrow = 1f;
            text.style.minHeight = 28;
            text.style.fontSize = FontSize;
            text.RegisterValueChangedCallback(evt =>
            {
                if (segment.kind == PungentRichDocumentStreamNodeKind.CommandPlaceholder)
                {
                    segment.commandKey = PungentRichDocumentParser.NormalizeTokenKey(evt.newValue);
                    segment.text = segment.commandKey;
                    SetSemanticField(segment, "key", segment.commandKey);
                }
                else
                {
                    segment.text = evt.newValue ?? string.Empty;
                }

                OnDirty?.Invoke("Unsaved changes");
            });
            text.RegisterCallback<FocusOutEvent>(_ =>
            {
                _editingSegmentIndex = -1;
                CommitCanvas("Unsaved changes.", true);
            });
            row.Add(text);

            if (CanExtendInsertion(segment))
            {
                Button add = new Button(() => AddRelatedElementAfter(index, segment))
                {
                    text = "+",
                    tooltip = "Add another related line after this insertion."
                };
                add.style.width = 24;
                add.style.height = 24;
                add.style.marginLeft = 6;
                row.Add(add);
            }

            text.schedule.Execute(() => text.Focus());
            return row;
        }

        private VisualElement CreateCustomInsertionEditor(int index, PungentRichDocumentCanvasSegment segment)
        {
            PungentRichDocumentInsertionDefinition definition = DefinitionForSegment(segment);
            VisualElement root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.marginTop = 4;
            root.style.marginBottom = CompactLineSpacing ? 4 : 8;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.borderLeftWidth = 3;
            root.style.borderTopWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderBottomWidth = 1;
            Color tint = PungentRichDocumentInsertionDefinitionRegistry.TintForDefinition(definition, UtilityWindowTheme.Teal);
            if (!ShowSemanticTint)
                tint = UtilityWindowTheme.Neutral;
            root.style.borderLeftColor = new StyleColor(tint);
            root.style.borderTopColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.3f));
            root.style.borderRightColor = new StyleColor(new Color(0f, 0f, 0f, 0.13f));
            root.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.16f));
            root.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.18f, 0.19f, 0.2f) : new Color(0.96f, 0.97f, 0.975f));

            Label heading = new Label(definition == null ? "Custom Insertion" : definition.displayName);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.color = tint;
            heading.style.marginBottom = 5;
            root.Add(heading);

            TextField body = CreateIntegrationField("Text", segment.text, true, next => segment.text = next, index);
            root.Add(body);

            if (definition != null)
            {
                VisualElement fieldsRoot = new VisualElement();
                fieldsRoot.style.flexDirection = definition.layoutMode == PungentRichDocumentInsertionLayoutMode.Vertical
                    ? FlexDirection.Column
                    : FlexDirection.Row;
                fieldsRoot.style.flexWrap = definition.layoutMode == PungentRichDocumentInsertionLayoutMode.Vertical
                    ? Wrap.NoWrap
                    : Wrap.Wrap;
                fieldsRoot.style.alignItems = definition.layoutMode == PungentRichDocumentInsertionLayoutMode.InlineRow
                    ? Align.Center
                    : Align.Stretch;
                fieldsRoot.style.marginTop = 2;
                root.Add(fieldsRoot);

                foreach (PungentRichDocumentInsertionFieldDefinition field in definition.fields ?? new List<PungentRichDocumentInsertionFieldDefinition>())
                {
                    VisualElement fieldElement = CreateCustomField(index, segment, field);
                    if (definition.layoutMode != PungentRichDocumentInsertionLayoutMode.Vertical)
                    {
                        fieldElement.style.minWidth = definition.layoutMode == PungentRichDocumentInsertionLayoutMode.InlineRow ? 140 : 180;
                        fieldElement.style.marginRight = 8;
                    }
                    fieldsRoot.Add(fieldElement);
                }

                foreach (PungentRichDocumentInsertionRepeatableElementDefinition repeatable in definition.repeatableElements ?? new List<PungentRichDocumentInsertionRepeatableElementDefinition>())
                    DrawRepeatableEditor(root, index, segment, repeatable);
            }

            if (_editingSegmentIndex == index)
                body.schedule.Execute(() => body.Focus());
            return root;
        }

        private VisualElement CreateSemanticInlineSpan(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentSemanticSpan span)
        {
            if (span == null)
                return CreateTextSegment(string.Empty, false);

            VisualElement spanElement = new VisualElement();
            spanElement.style.flexDirection = FlexDirection.Row;
            spanElement.style.flexWrap = Wrap.Wrap;
            spanElement.style.alignItems = Align.Center;
            spanElement.style.marginLeft = 2;
            spanElement.style.marginRight = 2;
            spanElement.tooltip = PungentRichDocumentSemanticParser.DisplayName(span.kind) + " integration: " + span.sourceText;
            spanElement.RegisterCallback<MouseDownEvent>(evt =>
            {
                _activeSegmentIndex = index;
                SelectSemanticSpan(index, span);
                evt.StopPropagation();
            });
            spanElement.AddManipulator(new ContextualMenuManipulator(evt => BuildSemanticSpanContextMenu(evt.menu, index, segment, span)));

            Color tint = TintForSemanticKind(span.kind);
            if (!ShowSemanticTint)
                tint = UtilityWindowTheme.Neutral;
            Label marker = new Label(PungentRichDocumentSemanticParser.DisplayName(span.kind));
            marker.style.fontSize = Mathf.Max(10, FontSize - 3);
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            marker.style.color = tint;
            marker.style.marginRight = 3;
            spanElement.Add(marker);

            AddStyledTextSegments(spanElement, span.innerText, false);
            spanElement.style.borderBottomWidth = ShowSemanticTint ? 1 : 0;
            spanElement.style.borderBottomColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.55f));
            return spanElement;
        }

        private VisualElement CreateEmbeddedIntegration(int index, PungentRichDocumentCanvasSegment segment)
        {
            VisualElement embed = new VisualElement();
            embed.style.marginTop = CompactLineSpacing ? 5 : 8;
            embed.style.marginBottom = CompactLineSpacing ? 5 : 9;
            embed.style.paddingLeft = 11;
            embed.style.paddingRight = 11;
            embed.style.paddingTop = 8;
            embed.style.paddingBottom = 9;
            embed.style.borderLeftWidth = 4;
            embed.style.borderTopWidth = 1;
            embed.style.borderRightWidth = 1;
            embed.style.borderBottomWidth = 1;
            embed.style.borderTopLeftRadius = 4;
            embed.style.borderTopRightRadius = 4;
            embed.style.borderBottomLeftRadius = 4;
            embed.style.borderBottomRightRadius = 4;
            Color tint = TintForSegment(segment.kind);
            if (!ShowSemanticTint)
                tint = UtilityWindowTheme.Neutral;
            embed.style.borderLeftColor = new StyleColor(tint);
            embed.style.borderTopColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.28f));
            embed.style.borderRightColor = new StyleColor(new Color(0f, 0f, 0f, 0.13f));
            embed.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.16f));
            embed.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.19f, 0.20f, 0.205f) : new Color(0.95f, 0.965f, 0.97f));
            embed.RegisterCallback<MouseDownEvent>(_ => _activeSegmentIndex = index);
            embed.AddManipulator(new ContextualMenuManipulator(evt => BuildIntegrationContextMenu(evt.menu, index, segment)));

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 5;
            embed.Add(header);

            Label label = new Label(IntegrationLabel(segment.kind));
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = tint;
            header.Add(label);

            string issueCode = SegmentIssueCode(segment);
            if (!string.IsNullOrWhiteSpace(issueCode) && !IsSuppressed(issueCode, segment.ToRawText()))
            {
                Button warning = new Button(() => SuppressIssue(issueCode, segment.ToRawText()))
                {
                    text = "!",
                    tooltip = WarningTooltip(issueCode) + " Click to ignore this warning for this source."
                };
                warning.style.width = 22;
                warning.style.height = 20;
                warning.style.marginLeft = 6;
                warning.style.backgroundColor = new StyleColor(UtilityWindowTheme.Amber);
                warning.style.color = Color.white;
                header.Add(warning);
            }

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            header.Add(spacer);

            Button addElement = new Button(() => AddRelatedElementAfter(index, segment))
            {
                text = "+",
                tooltip = "Add another related element after this embed."
            };
            addElement.style.width = 24;
            addElement.style.height = 20;
            header.Add(addElement);

            Button menuButton = new Button(() => ShowEmbedMenu(index, segment))
            {
                text = "...",
                tooltip = "Embed actions"
            };
            menuButton.style.width = 30;
            menuButton.style.height = 20;
            header.Add(menuButton);

            DrawIntegrationFields(embed, index, segment);
            return embed;
        }

        private void DrawIntegrationFields(VisualElement embed, int index, PungentRichDocumentCanvasSegment segment)
        {
            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                    DrawDialogueFields(embed, index, segment);
                    break;
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    embed.Add(CreateIntegrationField("Command", segment.commandKey, false, next => segment.commandKey = next, index));
                    break;
                case PungentRichDocumentStreamNodeKind.Code:
                    embed.Add(CreateIntegrationField("Code", segment.text, true, next => segment.text = next, index, true));
                    embed.Add(CreateIntegrationField("Language", segment.language, false, next => segment.language = next, index));
                    break;
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    VisualElement row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Stretch;
                    embed.Add(row);

                    TextField label = CreateIntegrationField("Type", segment.label, false, next => segment.label = next, index);
                    label.style.width = 132;
                    label.style.marginRight = 8;
                    row.Add(label);

                    TextField copy = CreateIntegrationField("Text", segment.text, true, next => segment.text = next, index);
                    copy.style.flexGrow = 1f;
                    row.Add(copy);
                    DrawTokenChips(embed, segment.text, index, segment, true);
                    break;
                default:
                    embed.Add(CreateIntegrationField(FieldLabelForSegment(segment.kind), segment.text, true, next => segment.text = next, index));
                    DrawTokenChips(embed, segment.text, index, segment, true);
                    break;
            }
        }

        private void DrawDialogueFields(VisualElement embed, int index, PungentRichDocumentCanvasSegment segment)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            embed.Add(row);

            TextField speaker = CreateIntegrationField("Speaker", segment.speaker, false, next => segment.speaker = next, index);
            speaker.style.width = 150;
            speaker.style.marginRight = 8;
            row.Add(speaker);

            TextField line = CreateIntegrationField("Line", segment.text, true, next => segment.text = next, index);
            line.style.flexGrow = 1f;
            row.Add(line);
            DrawTokenChips(embed, segment.text, index, segment, true);
        }

        private TextField CreateIntegrationField(string label, string value, bool multiline, Action<string> setter, int index, bool monospace = false)
        {
            TextField field = new TextField(label)
            {
                value = value ?? string.Empty,
                multiline = multiline
            };
            field.style.minHeight = multiline ? 36 : 24;
            field.style.marginBottom = 4;
            field.style.fontSize = monospace ? Mathf.Max(12, FontSize - 1) : FontSize;
            field.RegisterCallback<FocusInEvent>(_ =>
            {
                _activeSegmentIndex = index;
                _activeTextField = null;
            });
            field.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            field.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            field.RegisterValueChangedCallback(evt =>
            {
                setter?.Invoke(evt.newValue ?? string.Empty);
                OnDirty?.Invoke("Unsaved changes");
            });
            field.RegisterCallback<FocusOutEvent>(_ => CommitCanvas("Unsaved changes.", true));
            field.AddManipulator(new ContextualMenuManipulator(evt => BuildIntegrationContextMenu(evt.menu, index, _canvas.segments[index])));
            return field;
        }

        private VisualElement CreateCustomField(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentInsertionFieldDefinition definition)
        {
            if (definition == null)
                return CreateMutedLabel(string.Empty);

            string value = GetSemanticField(segment, definition.key);
            if (definition.fieldKind == PungentRichDocumentInsertionFieldKind.Boolean)
            {
                Toggle toggle = new Toggle(definition.displayName)
                {
                    value = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                };
                toggle.style.marginBottom = 4;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    SetSemanticField(segment, definition.key, evt.newValue ? "true" : "false");
                    OnDirty?.Invoke("Unsaved insertion field changes");
                });
                toggle.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
                toggle.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                return toggle;
            }

            if (definition.fieldKind == PungentRichDocumentInsertionFieldKind.EnumText && definition.enumOptions != null && definition.enumOptions.Count > 0)
            {
                List<string> choices = definition.enumOptions.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
                if (!choices.Contains(value))
                    choices.Insert(0, string.IsNullOrWhiteSpace(value) ? definition.defaultValue ?? string.Empty : value);

                PopupField<string> popup = new PopupField<string>(definition.displayName, choices, string.IsNullOrWhiteSpace(value) ? choices[0] : value);
                popup.style.marginBottom = 4;
                popup.RegisterValueChangedCallback(evt =>
                {
                    SetSemanticField(segment, definition.key, evt.newValue ?? string.Empty);
                    OnDirty?.Invoke("Unsaved insertion field changes");
                });
                popup.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
                popup.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                return popup;
            }

            bool multiline = definition.multiline || definition.fieldKind == PungentRichDocumentInsertionFieldKind.LongText;
            return CreateIntegrationField(definition.displayName, string.IsNullOrEmpty(value) ? definition.defaultValue : value, multiline, next => SetSemanticField(segment, definition.key, next), index);
        }

        private void DrawRepeatableEditor(VisualElement root, int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentInsertionRepeatableElementDefinition repeatable)
        {
            if (root == null || segment == null || repeatable == null)
                return;

            int count = RepeatableCount(segment, repeatable);
            Label label = new Label(repeatable.displayName);
            label.style.fontSize = Mathf.Max(10, FontSize - 2);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 6;
            label.style.marginBottom = 3;
            root.Add(label);

            for (int item = 0; item < count; item++)
            {
                VisualElement itemRoot = new VisualElement();
                itemRoot.style.marginBottom = 5;
                itemRoot.style.paddingLeft = 8;
                itemRoot.style.borderLeftWidth = 2;
                itemRoot.style.borderLeftColor = new StyleColor(new Color(0.5f, 0.55f, 0.6f, 0.45f));
                root.Add(itemRoot);

                foreach (PungentRichDocumentInsertionFieldDefinition field in repeatable.fields ?? new List<PungentRichDocumentInsertionFieldDefinition>())
                {
                    if (field == null)
                        continue;

                    string key = PungentRichDocumentInsertionDefinitionRegistry.RepeatableFieldKey(repeatable.key, item, field.key);
                    PungentRichDocumentInsertionFieldDefinition clone = new PungentRichDocumentInsertionFieldDefinition
                    {
                        key = key,
                        displayName = field.displayName,
                        fieldKind = field.fieldKind,
                        defaultValue = field.defaultValue,
                        placeholder = field.placeholder,
                        enumOptions = field.enumOptions == null ? new List<string>() : new List<string>(field.enumOptions),
                        required = field.required,
                        multiline = field.multiline
                    };
                    itemRoot.Add(CreateCustomField(index, segment, clone));
                }
            }

            Button add = new Button(() => AddRepeatableElement(index, segment, repeatable))
            {
                text = "+ " + (string.IsNullOrWhiteSpace(repeatable.addButtonLabel) ? "Add " + repeatable.displayName : repeatable.addButtonLabel),
                tooltip = "Add another " + repeatable.displayName + " to this insertion."
            };
            add.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            add.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            add.style.alignSelf = Align.FlexStart;
            add.style.marginTop = 2;
            add.style.height = 24;
            root.Add(add);
        }

        private Label CreateTextSegment(string text, bool heading)
        {
            return CreateTextSegment(text, heading, heading ? FontStyle.Bold : FontStyle.Normal);
        }

        private Label CreateTextSegment(string text, bool heading, FontStyle fontStyle)
        {
            Label label = new Label(text ?? string.Empty);
            label.style.fontSize = heading ? Mathf.Max(FontSize + 7, 21) : FontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginRight = 1;
            label.style.color = EditorGUIUtility.isProSkin ? new Color(0.88f, 0.89f, 0.9f) : new Color(0.12f, 0.12f, 0.13f);
            return label;
        }

        private void AddStyledTextSegments(VisualElement parent, string text, bool heading)
        {
            if (parent == null)
                return;

            string source = text ?? string.Empty;
            int index = 0;
            while (index < source.Length)
            {
                int bold = source.IndexOf("**", index, StringComparison.Ordinal);
                int italic = source.IndexOf("_", index, StringComparison.Ordinal);
                int next = NextMarkerIndex(bold, italic);
                if (next < 0)
                {
                    parent.Add(CreateTextSegment(source.Substring(index), heading));
                    return;
                }

                if (next > index)
                    parent.Add(CreateTextSegment(source.Substring(index, next - index), heading));

                if (next == bold)
                {
                    int end = source.IndexOf("**", next + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        parent.Add(CreateTextSegment(source.Substring(next), heading));
                        return;
                    }

                    parent.Add(CreateTextSegment(source.Substring(next + 2, end - next - 2), heading, FontStyle.Bold));
                    index = end + 2;
                    continue;
                }

                int italicEnd = source.IndexOf("_", next + 1, StringComparison.Ordinal);
                if (italicEnd < 0)
                {
                    parent.Add(CreateTextSegment(source.Substring(next), heading));
                    return;
                }

                parent.Add(CreateTextSegment(source.Substring(next + 1, italicEnd - next - 1), heading, FontStyle.Italic));
                index = italicEnd + 1;
            }
        }

        private static int NextMarkerIndex(int bold, int italic)
        {
            if (bold < 0)
                return italic;
            if (italic < 0)
                return bold;
            return Math.Min(bold, italic);
        }

        private Label CreateScriptLineMarker(PungentRichDocumentScriptLineKind kind)
        {
            Color tint = TintForScriptLineKind(kind);
            if (!ShowSemanticTint)
                tint = UtilityWindowTheme.Neutral;

            string labelText = ShowRawSyntaxHints ? ScriptLineRawHint(kind) : ScriptLineDisplayName(kind);
            Label marker = new Label(labelText);
            marker.tooltip = ScriptLineTooltip(kind);
            marker.style.fontSize = Mathf.Max(10, FontSize - 3);
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            marker.style.color = tint;
            marker.style.marginRight = 6;
            marker.style.paddingLeft = 2;
            marker.style.paddingRight = 2;
            marker.style.borderBottomWidth = ShowSemanticTint ? 1 : 0;
            marker.style.borderBottomColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.55f));
            return marker;
        }

        private Label CreateMutedLabel(string text)
        {
            Label label = new Label(text ?? string.Empty);
            label.style.color = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.64f, 0.66f) : new Color(0.42f, 0.43f, 0.44f);
            label.style.fontSize = Mathf.Max(11, FontSize - 2);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private VisualElement CreateTokenChip(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, bool inSegmentText)
        {
            if (run != null && run.isCustomChip)
                return CreateCustomChip(index, segment, run, inSegmentText);
            if (run != null && run.isReferenceExpression)
                return CreateReferenceChip(index, segment, run, inSegmentText);
            if (run != null && (run.isCustomDefinitionExpression || run.isCustomReferenceExpression))
                return CreateCustomExpressionChip(index, segment, run, inSegmentText);

            PungentRichDocumentTokenResolution resolution = run.validSyntax
                ? PungentRichDocumentTokenResolver.Resolve(run.key)
                : null;

            string issueCode = TokenIssueCode(run, resolution);
            bool suppressed = !string.IsNullOrWhiteSpace(issueCode) && IsSuppressed(issueCode, run.rawText);
            string display = run.validSyntax && resolution != null && resolution.known ? resolution.displayText : run.rawText;
            if (ShowTokenTechnicalText && run.validSyntax && resolution != null && resolution.known)
                display += " {" + run.key + "}";
            string tooltip = run.validSyntax && resolution != null
                ? resolution.tooltip
                : "Invalid token syntax. Use letters, numbers, underscore, dot, or dash inside braces.";

            if (suppressed)
                tooltip += Environment.NewLine + "Warning ignored for this source.";

            Button chip = new Button(() =>
            {
                SelectTokenRun(index, run, string.Empty);
                OnSelectionChanged?.Invoke();
                _editingTokenSegmentIndex = index;
                _editingTokenStart = run.startIndex;
                _editingTokenInSegmentText = inSegmentText;
                _activeSegmentIndex = index;
                Refresh();
            })
            {
                text = display,
                tooltip = tooltip
            };

            Color tint = TokenTint(run, resolution, suppressed);
            chip.style.marginLeft = 2;
            chip.style.marginRight = 2;
            chip.style.marginTop = 1;
            chip.style.marginBottom = 1;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.height = 22;
            chip.style.borderTopLeftRadius = 10;
            chip.style.borderTopRightRadius = 10;
            chip.style.borderBottomLeftRadius = 10;
            chip.style.borderBottomRightRadius = 10;
            chip.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, suppressed ? 0.42f : 0.9f));
            chip.style.color = Color.white;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.AddManipulator(new ContextualMenuManipulator(evt => BuildTokenContextMenu(evt.menu, index, segment, run, inSegmentText, issueCode)));
            return chip;
        }

        private VisualElement CreateReferenceChip(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, bool inSegmentText)
        {
            PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.FindExpressionBinding(_document, run.bindingExpression);
            PungentRichDocumentBindingPreview preview = PungentRichDocumentBindingApplicationService.Preview(binding);
            bool canApply = PungentRichDocumentBindingApplicationService.CanApply(binding, out string applyReason);
            bool resolved = binding != null && binding.target != null && binding.target.HasTarget && preview != null && preview.canRead;
            bool canRead = preview != null && preview.canRead;
            string display = resolved && canRead && !string.IsNullOrWhiteSpace(preview.currentValue)
                ? preview.currentValue
                : run.rawText;
            string tooltip = "Reference token: " + run.rawText;
            if (binding != null && binding.target != null && binding.target.HasTarget)
                tooltip += Environment.NewLine + "Target: " + binding.target.label;
            if (preview != null && !string.IsNullOrWhiteSpace(preview.currentValue))
                tooltip += Environment.NewLine + "Current: " + preview.currentValue;
            if (preview != null && !string.IsNullOrWhiteSpace(preview.disabledReason))
                tooltip += Environment.NewLine + preview.disabledReason;
            if (!canApply && !string.IsNullOrWhiteSpace(applyReason))
                tooltip += Environment.NewLine + "Apply disabled: " + applyReason;
            if (!resolved)
                tooltip += Environment.NewLine + "Click to refine this reference.";

            Button chip = new Button(() =>
            {
                SelectReferenceToken(index, segment, run, inSegmentText, true);
            })
            {
                text = display,
                tooltip = tooltip
            };

            Color tint = resolved ? (canApply ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Blue) : UtilityWindowTheme.Amber;
            chip.style.marginLeft = 2;
            chip.style.marginRight = 2;
            chip.style.marginTop = 1;
            chip.style.marginBottom = 1;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.height = 22;
            chip.style.borderTopLeftRadius = 10;
            chip.style.borderTopRightRadius = 10;
            chip.style.borderBottomLeftRadius = 10;
            chip.style.borderBottomRightRadius = 10;
            chip.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.9f));
            chip.style.color = Color.white;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.AddManipulator(new ContextualMenuManipulator(evt => BuildTokenContextMenu(evt.menu, index, segment, run, inSegmentText, resolved ? string.Empty : "UNRESOLVED_REFERENCE")));
            return chip;
        }

        private VisualElement CreateCustomExpressionChip(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, bool inSegmentText)
        {
            PungentRichDocumentInsertionDefinition definition = run.isCustomReferenceExpression
                ? PungentRichDocumentInsertionDefinitionRegistry.Find(run.bindingExpression == null ? run.key : run.bindingExpression.customId)
                : null;
            bool definitionExists = definition != null || run.isCustomDefinitionExpression;
            string display = definition != null
                ? definition.displayName
                : !string.IsNullOrWhiteSpace(run.chipLabel)
                    ? run.chipLabel
                    : run.rawText;

            Button chip = new Button(() =>
            {
                _activeSegmentIndex = index;
                SelectTokenRun(index, run, string.Empty);
                OnSelectionChanged?.Invoke();
            })
            {
                text = display,
                tooltip = run.isCustomDefinitionExpression
                    ? "Custom definition token: " + run.rawText + Environment.NewLine + "Use the context menu to create or update this definition."
                    : "Custom reference token: " + run.rawText
            };

            Color tint = definitionExists ? UtilityWindowTheme.Purple : UtilityWindowTheme.Amber;
            chip.style.marginLeft = 2;
            chip.style.marginRight = 2;
            chip.style.marginTop = 1;
            chip.style.marginBottom = 1;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.height = 22;
            chip.style.borderTopLeftRadius = 10;
            chip.style.borderTopRightRadius = 10;
            chip.style.borderBottomLeftRadius = 10;
            chip.style.borderBottomRightRadius = 10;
            chip.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.9f));
            chip.style.color = Color.white;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.AddManipulator(new ContextualMenuManipulator(evt => BuildTokenContextMenu(evt.menu, index, segment, run, inSegmentText, definitionExists ? string.Empty : "UNKNOWN_CUSTOM_REFERENCE")));
            return chip;
        }

        private VisualElement CreateCustomChip(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, bool inSegmentText)
        {
            PungentRichDocumentInsertionDefinition definition = PungentRichDocumentInsertionDefinitionRegistry.FindForChipLabel(run.chipLabel);
            string display = string.IsNullOrWhiteSpace(run.chipLabel) ? "Custom Chip" : run.chipLabel.Trim();
            Button chip = new Button(() =>
            {
                SelectTokenRun(index, run, string.Empty);
                OnSelectionChanged?.Invoke();
                _editingTokenSegmentIndex = index;
                _editingTokenStart = run.startIndex;
                _editingTokenInSegmentText = inSegmentText;
                _activeSegmentIndex = index;
                Refresh();
            })
            {
                text = display,
                tooltip = "Custom chip: " + run.rawText + Environment.NewLine + "Click to edit the chip label."
            };

            Color tint = PungentRichDocumentInsertionDefinitionRegistry.TintForDefinition(definition, UtilityWindowTheme.Purple);
            chip.style.marginLeft = 2;
            chip.style.marginRight = 2;
            chip.style.marginTop = 1;
            chip.style.marginBottom = 1;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 8;
            chip.style.paddingTop = 1;
            chip.style.paddingBottom = 1;
            chip.style.height = 22;
            chip.style.borderTopLeftRadius = 10;
            chip.style.borderTopRightRadius = 10;
            chip.style.borderBottomLeftRadius = 10;
            chip.style.borderBottomRightRadius = 10;
            chip.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.9f));
            chip.style.color = Color.white;
            chip.style.unityFontStyleAndWeight = FontStyle.Bold;
            chip.AddManipulator(new ContextualMenuManipulator(evt => BuildTokenContextMenu(evt.menu, index, segment, run, inSegmentText, string.Empty)));
            return chip;
        }

        private TextField CreateTokenEditField(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, bool inSegmentText)
        {
            TextField field = new TextField
            {
                value = TokenEditValue(run),
                multiline = false
            };
            field.style.width = Mathf.Clamp(78 + run.key.Length * 7, 92, 240);
            field.style.height = 24;
            field.style.marginLeft = 2;
            field.style.marginRight = 2;
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitTokenEdit(index, segment, run, field.value, inSegmentText, true);
                    evt.StopPropagation();
                }
            });
            field.RegisterCallback<FocusOutEvent>(_ => CommitTokenEdit(index, segment, run, field.value, inSegmentText, true));
            field.schedule.Execute(() => field.Focus());
            return field;
        }

        private void DrawTokenChips(VisualElement parent, string text, int index, PungentRichDocumentCanvasSegment segment, bool inSegmentText)
        {
            List<PungentRichDocumentCanvasTokenRun> tokens = PungentRichDocumentDocumentCanvasParser.ExtractTokenRuns(text);
            if (tokens.Count == 0)
                return;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 4;
            parent.Add(row);

            foreach (PungentRichDocumentCanvasTokenRun token in tokens)
            {
                bool editingThisToken =
                    _editingTokenSegmentIndex == index &&
                    _editingTokenStart == token.startIndex &&
                    _editingTokenInSegmentText == inSegmentText;

                row.Add(editingThisToken
                    ? CreateTokenEditField(index, segment, token, inSegmentText)
                    : CreateTokenChip(index, segment, token, inSegmentText));
            }
        }

        private void AddInlineRuns(VisualElement parent, int index, PungentRichDocumentCanvasSegment segment, string text, bool heading, bool tokenRunsInSegmentText)
        {
            foreach (PungentRichDocumentTextRun run in PungentRichDocumentSemanticParser.ExtractRuns(text ?? string.Empty))
            {
                if (run == null)
                    continue;

                if (run.kind == PungentRichDocumentTextRunKind.Token && run.token != null)
                {
                    bool editingThisToken =
                        _editingTokenSegmentIndex == index &&
                        _editingTokenStart == run.token.startIndex &&
                        _editingTokenInSegmentText == tokenRunsInSegmentText;
                    parent.Add(editingThisToken
                        ? CreateTokenEditField(index, segment, run.token, tokenRunsInSegmentText)
                        : CreateTokenChip(index, segment, run.token, tokenRunsInSegmentText));
                    continue;
                }

                if (run.kind == PungentRichDocumentTextRunKind.Semantic && run.semantic != null)
                    parent.Add(CreateSemanticInlineSpan(index, segment, run.semantic));
                else
                    AddStyledTextSegments(parent, run.text, heading);
            }
        }

        private void CommitTokenEdit(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, string nextKey, bool inSegmentText, bool keepBraces)
        {
            if (segment == null || run == null)
                return;

            string clean = PungentRichDocumentParser.NormalizeTokenKey(nextKey);
            string replacement;
            if (run.isReferenceExpression)
            {
                clean = (nextKey ?? string.Empty).Trim().Trim('{', '}').Trim();
                if (!clean.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
                    clean = "ref " + clean;
                replacement = keepBraces ? "{" + clean + "}" : clean;
            }
            else if (run.isCustomDefinitionExpression)
            {
                clean = (nextKey ?? string.Empty).Trim().Trim('{', '}').Trim();
                if (!clean.StartsWith("custom ", StringComparison.OrdinalIgnoreCase))
                    clean = "custom " + clean;
                replacement = keepBraces ? "{" + clean + "}" : clean;
            }
            else if (run.isCustomReferenceExpression)
            {
                clean = (nextKey ?? string.Empty).Trim().Trim('{', '}').Trim();
                replacement = keepBraces ? "{" + clean + "}" : clean;
            }
            else if (run.isCustomChip)
            {
                clean = (nextKey ?? string.Empty).Trim();
                replacement = keepBraces ? "{chip:" + clean + "}" : clean;
            }
            else
            {
                replacement = keepBraces ? "{" + clean + "}" : clean;
            }
            string source = inSegmentText ? segment.text ?? string.Empty : segment.rawText ?? string.Empty;
            if (run.startIndex < 0 || run.startIndex + run.length > source.Length)
                return;

            string next = source.Substring(0, run.startIndex) + replacement + source.Substring(run.startIndex + run.length);
            if (inSegmentText)
                segment.text = next;
            else
            {
                segment.rawText = next;
                segment.text = next;
            }

            _editingTokenSegmentIndex = -1;
            _editingTokenStart = -1;
            if (!run.isCustomChip && !run.isReferenceExpression && !run.isCustomDefinitionExpression && !run.isCustomReferenceExpression && PungentRichDocumentParser.IsValidTokenKey(clean))
                OnTokenReferenced?.Invoke(clean);
            CommitCanvas("Edited token " + replacement + ".", true);
        }

        private static string TokenEditValue(PungentRichDocumentCanvasTokenRun run)
        {
            if (run == null)
                return string.Empty;
            if (run.isReferenceExpression && run.bindingExpression != null)
            {
                string path = run.bindingExpression.targetName;
                if (run.bindingExpression.pathSegments != null && run.bindingExpression.pathSegments.Count > 0)
                    path += ":" + string.Join(":", run.bindingExpression.pathSegments.ToArray());
                return path;
            }
            if (run.isCustomDefinitionExpression && run.bindingExpression != null)
                return run.bindingExpression.customId + (string.IsNullOrWhiteSpace(run.bindingExpression.displayText) ? string.Empty : ":" + run.bindingExpression.displayText);
            if (run.isCustomChip)
                return run.chipLabel;
            return run.key;
        }

        private static bool IsReferenceDraft(string value)
        {
            string text = value ?? string.Empty;
            int open = text.LastIndexOf('{');
            if (open < 0)
                return false;
            int close = text.IndexOf('}', open);
            if (close >= 0)
                return false;
            string draft = text.Substring(open + 1).TrimStart();
            return draft.StartsWith("ref", StringComparison.OrdinalIgnoreCase);
        }

        private void SelectReferenceToken(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, bool inSegmentText, bool openBrowser)
        {
            if (run == null)
                return;

            PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.FindExpressionBinding(_document, run.bindingExpression);
            _activeSegmentIndex = index;
            _multiSelection.SetSingle(index);
            _selection.SetTokenSelection(index, run.rawText, run.key, "Reference", binding == null ? string.Empty : binding.id);
            if (openBrowser)
            {
                _referenceBrowserSegmentIndex = index;
                _referenceBrowserTokenStart = run.startIndex;
                _referenceBrowserInSegmentText = inSegmentText;
                _referenceBrowserExpression = run.bindingExpression;
            }
            OnSelectionChanged?.Invoke();
            Refresh();
        }

        private void SelectTokenRun(int index, PungentRichDocumentCanvasTokenRun run, string bindingId)
        {
            if (run == null)
                return;

            string kind = run.isCustomDefinitionExpression
                ? "Custom Definition"
                : run.isCustomReferenceExpression
                    ? "Custom Reference"
                    : run.isCustomChip
                        ? "Custom Chip"
                        : "Token";
            _activeSegmentIndex = index;
            _multiSelection.SetSingle(index);
            _selection.SetTokenSelection(index, run.rawText, run.key, kind, bindingId);
        }

        private void CommitReferenceEndpoint(int index, PungentRichDocumentBindingExpression expression, PungentAuthoringBindingEndpoint endpoint, string tokenText, bool inSegmentText)
        {
            if (expression == null || endpoint == null || string.IsNullOrWhiteSpace(tokenText))
                return;
            if (index < 0 || index >= _canvas.segments.Count)
                return;

            PungentRichDocumentCanvasSegment segment = _canvas.segments[index];
            string source = inSegmentText ? segment.text ?? string.Empty : segment.rawText ?? segment.text ?? string.Empty;
            int start = _referenceBrowserTokenStart >= 0 ? _referenceBrowserTokenStart : expression.sourceIndex;
            int length = _referenceBrowserTokenStart >= 0 ? expression.rawText.Length : expression.sourceLength;
            if (start < 0 || length <= 0 || start + length > source.Length)
            {
                int fallback = source.IndexOf(expression.rawText ?? string.Empty, StringComparison.Ordinal);
                if (fallback < 0)
                {
                    OnStatus?.Invoke("Could not find the reference token in the active line.");
                    return;
                }

                start = fallback;
                length = expression.rawText.Length;
            }

            string next = source.Substring(0, start) + tokenText + source.Substring(start + length);
            if (inSegmentText)
                segment.text = next;
            else
            {
                segment.rawText = next;
                segment.text = next;
            }

            PungentRichDocumentBindingExpression updatedExpression;
            if (!PungentRichDocumentBindingExpressionParser.TryParseToken(tokenText, out updatedExpression))
                updatedExpression = expression;

            PungentRichDocumentSemanticBindingService.SetReferenceExpressionEndpoint(_document, updatedExpression, endpoint);
            _referenceBrowserSegmentIndex = -1;
            _referenceBrowserTokenStart = -1;
            _referenceBrowserExpression = null;
            _activeSegmentIndex = index;
            _selection.SetTokenSelection(index, tokenText, updatedExpression.targetName, "Reference", updatedExpression.id);
            CommitCanvas("Resolved reference token.", true);
        }

        private void BuildTextContextMenu(DropdownMenu menu, int index, PungentRichDocumentCanvasSegment segment)
        {
            menu.AppendAction("Edit", _ =>
            {
                _activeSegmentIndex = index;
                _editingSegmentIndex = index;
                Refresh();
            });
            menu.AppendAction("Insert Token", _ => InsertToken(TokenKeyProvider == null ? "tokenKey" : TokenKeyProvider.Invoke()));
            menu.AppendAction("Add Comment", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Comment));
            menu.AppendAction("Add Bookmark", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Bookmark));
            menu.AppendSeparator();
            AppendSelectionSemanticActions(menu);
            menu.AppendAction("Custom/Create Insertion From Selection", _ => CreateCustomInsertionFromSelection());
            menu.AppendSeparator();
            AppendConversionActions(menu, index);
            menu.AppendSeparator();
            menu.AppendAction("Copy Text", _ => EditorGUIUtility.systemCopyBuffer = segment?.ReadableText() ?? string.Empty);
            menu.AppendAction("Copy Raw Source", _ => EditorGUIUtility.systemCopyBuffer = segment?.ToRawText() ?? string.Empty);
            menu.AppendAction("Delete", _ => DeleteSegment(index));
        }

        private void BuildLineHandleContextMenu(DropdownMenu menu, int index, PungentRichDocumentCanvasSegment segment)
        {
            menu.AppendAction("Use As Insertion Target", _ =>
            {
                _activeSegmentIndex = index;
                _selection.SetTextSelection(index, 0, 0, segment == null ? string.Empty : segment.ReadableText());
                OnSelectionChanged?.Invoke();
                Refresh();
            });
            menu.AppendAction("Move Line Up", _ => MoveSegment(index, index - 1), _ => index > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Move Line Down", _ => MoveSegment(index, index + 1), _ => index < _canvas.segments.Count - 1 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendSeparator();
            menu.AppendAction("Add Comment", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Comment));
            menu.AppendAction("Add Bookmark", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Bookmark));
            menu.AppendAction("Create Custom Insertion From Selection", _ => CreateCustomInsertionFromSelection());
            menu.AppendSeparator();
            menu.AppendAction("Copy Raw Source", _ => EditorGUIUtility.systemCopyBuffer = segment?.ToRawText() ?? string.Empty);
        }

        private void BuildIntegrationContextMenu(DropdownMenu menu, int index, PungentRichDocumentCanvasSegment segment)
        {
            AppendConversionActions(menu, index);
            menu.AppendSeparator();
            menu.AppendAction("Insert Token", _ => InsertToken(TokenKeyProvider == null ? "tokenKey" : TokenKeyProvider.Invoke()));
            menu.AppendAction("Add Comment", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Comment));
            menu.AppendAction("Add Bookmark", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Bookmark));
            menu.AppendAction("Create Custom Insertion From Selection", _ => CreateCustomInsertionFromSelection());
            menu.AppendAction("Link Documentation Target", _ => OpenDocumentationLinks?.Invoke());
            menu.AppendAction("Open Token Validator", _ => OpenTokenValidator?.Invoke());
            string issueCode = SegmentIssueCode(segment);
            if (!string.IsNullOrWhiteSpace(issueCode) && !IsSuppressed(issueCode, segment.ToRawText()))
                menu.AppendAction("Ignore This Warning", _ => SuppressIssue(issueCode, segment.ToRawText()));
            menu.AppendSeparator();
            menu.AppendAction("Remove Formatter", _ => RemoveFormatter(index));
            menu.AppendAction("Copy Text", _ => EditorGUIUtility.systemCopyBuffer = segment?.ReadableText() ?? string.Empty);
            menu.AppendAction("Copy Raw Source", _ => EditorGUIUtility.systemCopyBuffer = segment?.ToRawText() ?? string.Empty);
            menu.AppendAction("Delete Integration", _ => DeleteSegment(index));
        }

        private void BuildTokenContextMenu(DropdownMenu menu, int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentCanvasTokenRun run, bool inSegmentText, string issueCode)
        {
            menu.AppendAction("Select Token", _ =>
            {
                if (run != null && run.isReferenceExpression)
                    SelectReferenceToken(index, segment, run, inSegmentText, false);
                else
                {
                    SelectTokenRun(index, run, string.Empty);
                    OnSelectionChanged?.Invoke();
                    RefreshActiveRowStyles();
                }
            });
            menu.AppendAction("Edit Token", _ =>
            {
                _editingTokenSegmentIndex = index;
                _editingTokenStart = run.startIndex;
                _editingTokenInSegmentText = inSegmentText;
                Refresh();
            });
            if (run != null && run.isReferenceExpression)
                menu.AppendAction("Refine Reference", _ => SelectReferenceToken(index, segment, run, inSegmentText, true));
            if (run != null && run.isCustomDefinitionExpression)
                menu.AppendAction("Create/Update Custom Definition", _ => CreateOrUpdateCustomDefinition(run));
            if (run != null && !run.isReferenceExpression && !run.isCustomDefinitionExpression && !run.isCustomReferenceExpression && !run.isCustomChip)
                menu.AppendAction("Create Custom Chip From Token", _ => CreateCustomChipDefinition(run.key));
            menu.AppendAction("Remove Token Formatting", _ => CommitTokenEdit(index, segment, run, TokenEditValue(run), inSegmentText, false));
            if (!string.IsNullOrWhiteSpace(issueCode) && !IsSuppressed(issueCode, run.rawText))
                menu.AppendAction("Ignore This Warning", _ => SuppressIssue(issueCode, run.rawText));
            menu.AppendSeparator();
            menu.AppendAction("Open Token Validator", _ => OpenTokenValidator?.Invoke());
            menu.AppendAction("Copy Raw Token", _ => EditorGUIUtility.systemCopyBuffer = run.rawText ?? string.Empty);
        }

        private void CreateOrUpdateCustomDefinition(PungentRichDocumentCanvasTokenRun run)
        {
            if (run == null || run.bindingExpression == null)
                return;

            PungentRichDocumentInsertionDefinitionStorage.EnsureLoaded();
            if (!PungentRichDocumentBindingExpressionParser.AddOrUpdateCustomDefinition(PungentRichDocumentInsertionDefinitionStorage.Database, run.bindingExpression))
            {
                OnStatus?.Invoke("Unable to create custom definition.");
                return;
            }

            if (PungentRichDocumentInsertionDefinitionStorage.Save(out string error))
            {
                PungentRichDocumentInsertionDefinitionStorage.Reload();
                OnStatus?.Invoke("Created or updated custom insertion definition " + run.bindingExpression.customId + ".");
                Refresh();
                return;
            }

            OnStatus?.Invoke(string.IsNullOrWhiteSpace(error) ? "Unable to save custom definition." : error);
        }

        private void CreateCustomChipDefinition(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            PungentRichDocumentInsertionDefinitionStorage.EnsureLoaded();
            PungentRichDocumentInsertionDefinition definition = PungentRichDocumentInsertionDefinition.CreateChip(key);
            PungentRichDocumentInsertionDefinitionStorage.Database.AddOrUpdate(definition);
            if (PungentRichDocumentInsertionDefinitionStorage.Save(out string error))
            {
                PungentRichDocumentInsertionDefinitionStorage.Reload();
                OnStatus?.Invoke("Created custom chip definition " + definition.displayName + ".");
                Refresh();
                return;
            }

            OnStatus?.Invoke(error);
        }

        private void BuildSemanticSpanContextMenu(DropdownMenu menu, int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentSemanticSpan span)
        {
            menu.AppendAction("Select Integration", _ => SelectSemanticSpan(index, span));
            menu.AppendAction("Add Comment", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Comment));
            menu.AppendAction("Add Bookmark", _ => AddAnnotationAtSegment(index, PungentRichDocumentAnnotationKind.Bookmark));
            menu.AppendSeparator();
            menu.AppendAction("Convert To/Dialogue", _ => ReplaceSemanticSpanKind(index, segment, span, PungentRichDocumentSemanticKind.DialogueLine));
            menu.AppendAction("Convert To/Choice", _ => ReplaceSemanticSpanKind(index, segment, span, PungentRichDocumentSemanticKind.DialogueChoice));
            menu.AppendAction("Convert To/Quest Objective", _ => ReplaceSemanticSpanKind(index, segment, span, PungentRichDocumentSemanticKind.QuestObjective));
            menu.AppendAction("Convert To/Tutorial Step", _ => ReplaceSemanticSpanKind(index, segment, span, PungentRichDocumentSemanticKind.TutorialStep));
            menu.AppendAction("Convert To/Game Copy", _ => ReplaceSemanticSpanKind(index, segment, span, PungentRichDocumentSemanticKind.GameCopy));
            menu.AppendAction("Convert To/Command", _ => ReplaceSemanticSpanKind(index, segment, span, PungentRichDocumentSemanticKind.Command));
            menu.AppendSeparator();
            menu.AppendAction("Remove Integration Formatting", _ => ReplaceSemanticSpanWithText(index, segment, span));
            menu.AppendAction("Copy Text", _ => EditorGUIUtility.systemCopyBuffer = span.innerText ?? string.Empty);
            menu.AppendAction("Copy Raw Source", _ => EditorGUIUtility.systemCopyBuffer = span.sourceText ?? string.Empty);
        }

        private void AppendSelectionSemanticActions(DropdownMenu menu)
        {
            DropdownMenuAction.Status status = _selection.HasTextSelection ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            menu.AppendAction("Mark Selection As/Dialogue", _ => TryConvertSelectionToSemantic(PungentRichDocumentSemanticKind.DialogueLine), _ => status);
            menu.AppendAction("Mark Selection As/Choice", _ => TryConvertSelectionToSemantic(PungentRichDocumentSemanticKind.DialogueChoice), _ => status);
            menu.AppendAction("Mark Selection As/Quest Objective", _ => TryConvertSelectionToSemantic(PungentRichDocumentSemanticKind.QuestObjective), _ => status);
            menu.AppendAction("Mark Selection As/Tutorial Step", _ => TryConvertSelectionToSemantic(PungentRichDocumentSemanticKind.TutorialStep), _ => status);
            menu.AppendAction("Mark Selection As/Game Copy", _ => TryConvertSelectionToSemantic(PungentRichDocumentSemanticKind.GameCopy), _ => status);
            menu.AppendAction("Mark Selection As/Command", _ => TryConvertSelectionToSemantic(PungentRichDocumentSemanticKind.Command), _ => status);

            foreach (PungentRichDocumentInsertionDefinition definition in PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions)
            {
                if (definition == null)
                    continue;

                string path = definition.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip
                    ? "Mark Selection As/Custom Chip/" + definition.displayName
                    : "Mark Selection As/Custom Insertion/" + definition.displayName;
                PungentRichDocumentInsertionDefinition captured = definition;
                menu.AppendAction(path, _ => TryConvertSelectionToCustomInsertion(captured), _ => status);
            }
        }

        private void AppendConversionActions(DropdownMenu menu, int index)
        {
            menu.AppendAction("Convert/Plain Text", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.Text));
            menu.AppendAction("Convert/Heading", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.Heading));
            menu.AppendAction("Convert/Quote", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.Quote));
            menu.AppendAction("Convert/Code", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.Code));
            menu.AppendAction("Convert/Dialogue Line", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.DialogueLine));
            menu.AppendAction("Convert/Dialogue Choice", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.DialogueChoice));
            menu.AppendAction("Convert/Quest Objective", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.QuestObjective));
            menu.AppendAction("Convert/Tutorial Step", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.TutorialStep));
            menu.AppendAction("Convert/Command", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.CommandPlaceholder));
            menu.AppendAction("Convert/Game Copy", _ => ConvertSegment(index, PungentRichDocumentStreamNodeKind.GameCopy));

            foreach (PungentRichDocumentInsertionDefinition definition in PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions)
            {
                if (definition == null || definition.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip)
                    continue;

                PungentRichDocumentInsertionDefinition captured = definition;
                menu.AppendAction("Convert/Custom Insertion/" + definition.displayName, _ => ConvertSegmentToCustomInsertion(index, captured));
            }
        }

        private void ShowEmbedMenu(int index, PungentRichDocumentCanvasSegment segment)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Related Element"), false, () => AddRelatedElementAfter(index, segment));
            menu.AddItem(new GUIContent("Remove Formatter"), false, () => RemoveFormatter(index));
            menu.AddItem(new GUIContent("Copy Raw Source"), false, () => EditorGUIUtility.systemCopyBuffer = segment?.ToRawText() ?? string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, () => DeleteSegment(index));
            menu.ShowAsContext();
        }

        private void ConvertSegment(int index, PungentRichDocumentStreamNodeKind kind)
        {
            if (index < 0 || index >= _canvas.segments.Count)
                return;

            string readable = _canvas.segments[index].ReadableText();
            PungentRichDocumentCanvasSegment replacement = PungentRichDocumentDocumentCanvasParser.CreateIntegration(kind, readable);
            if (kind == PungentRichDocumentStreamNodeKind.Text)
            {
                replacement.kind = PungentRichDocumentStreamNodeKind.Text;
                replacement.rawText = readable;
                replacement.text = readable;
            }
            else if (kind == PungentRichDocumentStreamNodeKind.Heading)
            {
                replacement.kind = kind;
                replacement.text = StripLeadingConvention(readable);
                replacement.headingLevel = 2;
            }
            else if (kind == PungentRichDocumentStreamNodeKind.Quote)
            {
                replacement.kind = kind;
                replacement.text = StripLeadingConvention(readable);
            }
            else if (kind == PungentRichDocumentStreamNodeKind.DialogueLine)
            {
                SplitSpeaker(readable, out replacement.speaker, out replacement.text);
            }
            else if (kind == PungentRichDocumentStreamNodeKind.CommandPlaceholder)
            {
                replacement.commandKey = PungentRichDocumentParser.NormalizeTokenKey(readable).Replace(" ", "_");
            }

            _canvas.segments[index] = replacement;
            _activeSegmentIndex = index;
            _editingSegmentIndex = replacement.IsPlainTextEditable ? index : -1;
            CommitCanvas("Converted to " + IntegrationLabel(kind) + ".", true);
        }

        private void ApplyInlineStyle(string marker, string placeholder, string status)
        {
            if (_document == null)
                return;

            string safeMarker = marker ?? string.Empty;
            string safePlaceholder = string.IsNullOrWhiteSpace(placeholder) ? "text" : placeholder;
            if (string.IsNullOrEmpty(safeMarker))
                return;

            if (TryWrapActiveFieldSelection(safeMarker))
            {
                CommitCanvas(status, false);
                return;
            }

            CommitActiveFieldValue();
            if (_selection != null && _selection.HasTextSelection && TryWrapStoredSelection(safeMarker))
            {
                CommitCanvas(status, true);
                OnSelectionChanged?.Invoke();
                return;
            }

            if (TryInsertIntoActiveText(safeMarker + safePlaceholder + safeMarker))
            {
                CommitCanvas(status, true);
                return;
            }

            InsertRawSnippet(safeMarker + safePlaceholder + safeMarker);
        }

        private bool TryWrapActiveFieldSelection(string marker)
        {
            if (_activeTextField == null || _editingSegmentIndex < 0 || _editingSegmentIndex >= _canvas.segments.Count)
                return false;

            string current = _activeTextField.value ?? string.Empty;
            int cursor = Mathf.Clamp(ReadTextFieldIndex(_activeTextField, "cursorIndex", current.Length), 0, current.Length);
            int select = Mathf.Clamp(ReadTextFieldIndex(_activeTextField, "selectIndex", cursor), 0, current.Length);
            int start = Math.Min(cursor, select);
            int end = Math.Max(cursor, select);
            if (end <= start)
                return false;

            string next = current.Substring(0, start) + marker + current.Substring(start, end - start) + marker + current.Substring(end);
            _activeTextField.SetValueWithoutNotify(next);
            ApplyEditableValue(_canvas.segments[_editingSegmentIndex], next);
            WriteTextFieldIndex(_activeTextField, "cursorIndex", start);
            WriteTextFieldIndex(_activeTextField, "selectIndex", end + marker.Length * 2);
            return true;
        }

        private bool TryWrapStoredSelection(string marker)
        {
            if (_selection == null || !_selection.HasTextSelection)
                return false;
            if (_selection.segmentIndex < 0 || _selection.segmentIndex >= _canvas.segments.Count)
                return false;

            PungentRichDocumentCanvasSegment segment = _canvas.segments[_selection.segmentIndex];
            if (segment == null || !segment.IsPlainTextEditable)
                return false;

            string source = PungentRichDocumentTextCommandRouter.SegmentEditableSource(segment);
            if (_selection.startIndex < 0 || _selection.endIndex > source.Length || _selection.endIndex <= _selection.startIndex)
                return false;

            string selected = source.Substring(_selection.startIndex, _selection.endIndex - _selection.startIndex);
            string next = source.Substring(0, _selection.startIndex) + marker + selected + marker + source.Substring(_selection.endIndex);
            PungentRichDocumentTextCommandRouter.ApplySegmentEditableSource(segment, next);
            _activeSegmentIndex = _selection.segmentIndex;
            _selection.SetTextSelection(_activeSegmentIndex, _selection.startIndex + marker.Length, _selection.endIndex + marker.Length, next);
            return true;
        }

        private void ApplyLineFormat(PungentRichDocumentStreamNodeKind kind, int headingLevel)
        {
            if (_document == null)
                return;

            CommitActiveFieldValue();
            int index = _selection != null && _selection.HasSegment ? _selection.segmentIndex : _activeSegmentIndex;
            if (index >= 0 && index < _canvas.segments.Count)
            {
                ReplaceSegmentWithLineFormat(index, kind, headingLevel);
                return;
            }

            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.Heading:
                    InsertRawSnippet(new string('#', Mathf.Clamp(headingLevel, 1, 6)) + " Heading");
                    break;
                case PungentRichDocumentStreamNodeKind.Quote:
                    InsertRawSnippet("> Quote");
                    break;
                case PungentRichDocumentStreamNodeKind.Code:
                    InsertIntegration(PungentRichDocumentStreamNodeKind.Code);
                    break;
                case PungentRichDocumentStreamNodeKind.Divider:
                    InsertRawSnippet("---");
                    break;
            }
        }

        private void ReplaceSegmentWithLineFormat(int index, PungentRichDocumentStreamNodeKind kind, int headingLevel)
        {
            if (index < 0 || index >= _canvas.segments.Count)
                return;

            string readable = _canvas.segments[index].ReadableText();
            string clean = StripLeadingConvention(readable);
            PungentRichDocumentCanvasSegment replacement;
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.Heading:
                    replacement = new PungentRichDocumentCanvasSegment
                    {
                        kind = PungentRichDocumentStreamNodeKind.Heading,
                        text = string.IsNullOrWhiteSpace(clean) ? "Heading" : clean,
                        headingLevel = Mathf.Clamp(headingLevel, 1, 6)
                    };
                    break;
                case PungentRichDocumentStreamNodeKind.Quote:
                    replacement = new PungentRichDocumentCanvasSegment
                    {
                        kind = PungentRichDocumentStreamNodeKind.Quote,
                        text = string.IsNullOrWhiteSpace(clean) ? "Quote" : clean
                    };
                    break;
                case PungentRichDocumentStreamNodeKind.Code:
                    replacement = PungentRichDocumentDocumentCanvasParser.CreateIntegration(PungentRichDocumentStreamNodeKind.Code, string.IsNullOrWhiteSpace(clean) ? "code" : clean);
                    break;
                case PungentRichDocumentStreamNodeKind.Divider:
                    replacement = new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Divider };
                    break;
                default:
                    replacement = new PungentRichDocumentCanvasSegment
                    {
                        kind = PungentRichDocumentStreamNodeKind.Text,
                        rawText = clean,
                        text = clean
                    };
                    break;
            }

            _canvas.segments[index] = replacement;
            _activeSegmentIndex = index;
            _editingSegmentIndex = replacement.IsPlainTextEditable ? index : -1;
            _selection.SetTextSelection(index, 0, 0, replacement.ReadableText());
            OnSelectionChanged?.Invoke();
            CommitCanvas("Applied " + IntegrationLabel(kind) + ".", true);
        }

        private void ConvertSegmentToCustomInsertion(int index, PungentRichDocumentInsertionDefinition definition)
        {
            if (index < 0 || index >= _canvas.segments.Count || definition == null)
                return;

            string readable = _canvas.segments[index].ReadableText();
            PungentRichDocumentCanvasSegment replacement = PungentRichDocumentDocumentCanvasParser.CreateCustomInsertion(definition);
            replacement.text = string.IsNullOrWhiteSpace(readable) ? replacement.text : StripLeadingConvention(readable);
            _canvas.segments[index] = replacement;
            _activeSegmentIndex = index;
            _editingSegmentIndex = index;
            CommitCanvas("Converted to " + definition.displayName + ".", true);
        }

        private bool TryConvertSelectionToSemantic(PungentRichDocumentSemanticKind kind)
        {
            if (kind == PungentRichDocumentSemanticKind.None)
                return false;

            CommitActiveFieldValue();
            if (!PungentRichDocumentTextCommandRouter.WrapSelection(
                    _canvas,
                    _selection,
                    kind,
                    PungentRichDocumentSemanticParser.DefaultFieldsForKind(kind),
                    out string status))
            {
                OnStatus?.Invoke(status);
                return false;
            }

            _selection.Clear();
            CommitCanvas(status, true);
            OnSelectionChanged?.Invoke();
            return true;
        }

        private bool TryConvertSelectionToCustomInsertion(PungentRichDocumentInsertionDefinition definition)
        {
            if (definition == null)
                return false;

            if (definition.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip)
                return TryReplaceSelectionWithCustomChip(definition);

            CommitActiveFieldValue();
            if (!PungentRichDocumentTextCommandRouter.WrapSelection(
                    _canvas,
                    _selection,
                    PungentRichDocumentSemanticKind.CustomInsertion,
                    PungentRichDocumentInsertionDefinitionRegistry.CreateDefaultFields(definition),
                    out string status))
            {
                OnStatus?.Invoke(status);
                return false;
            }

            _selection.Clear();
            CommitCanvas("Applied " + definition.displayName + " insertion.", true);
            OnSelectionChanged?.Invoke();
            return true;
        }

        private bool TryReplaceSelectionWithCustomChip(PungentRichDocumentInsertionDefinition definition)
        {
            CommitActiveFieldValue();
            if (_canvas == null || _selection == null || !_selection.HasTextSelection)
            {
                OnStatus?.Invoke("Select text before applying a custom chip.");
                return false;
            }

            if (_selection.segmentIndex < 0 || _selection.segmentIndex >= _canvas.segments.Count)
            {
                OnStatus?.Invoke("Selection is no longer available.");
                return false;
            }

            PungentRichDocumentCanvasSegment segment = _canvas.segments[_selection.segmentIndex];
            if (segment == null || !segment.IsPlainTextEditable)
            {
                OnStatus?.Invoke("This content is not editable as inline text.");
                return false;
            }

            string source = PungentRichDocumentTextCommandRouter.SegmentEditableSource(segment);
            if (_selection.startIndex < 0 || _selection.endIndex > source.Length || _selection.endIndex <= _selection.startIndex)
            {
                OnStatus?.Invoke("Selection range is no longer valid.");
                return false;
            }

            string label = string.IsNullOrWhiteSpace(definition.syntaxAlias) ? definition.displayName : definition.syntaxAlias;
            string replacement = "{chip:" + label + "}";
            string next = source.Substring(0, _selection.startIndex) + replacement + source.Substring(_selection.endIndex);
            PungentRichDocumentTextCommandRouter.ApplySegmentEditableSource(segment, next);
            _activeSegmentIndex = _selection.segmentIndex;
            _selection.Clear();
            CommitCanvas("Applied " + definition.displayName + " chip.", true);
            OnSelectionChanged?.Invoke();
            return true;
        }

        private void ReplaceSemanticSpanKind(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentSemanticSpan span, PungentRichDocumentSemanticKind kind)
        {
            if (span == null || kind == PungentRichDocumentSemanticKind.None)
                return;

            string replacement = PungentRichDocumentSemanticParser.CreateSemanticTag(kind, span.innerText, PungentRichDocumentSemanticParser.DefaultFieldsForKind(kind), span.id);
            if (PungentRichDocumentTextCommandRouter.ReplaceSemanticSpan(_canvas, index, span, replacement, out string status))
                CommitCanvas("Converted integration to " + PungentRichDocumentSemanticParser.DisplayName(kind) + ".", true);
            else
                OnStatus?.Invoke(status);
        }

        private void ReplaceSemanticSpanWithText(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentSemanticSpan span)
        {
            if (span == null)
                return;

            if (PungentRichDocumentTextCommandRouter.ReplaceSemanticSpan(_canvas, index, span, span.innerText, out string status))
                CommitCanvas("Removed integration formatting and preserved text.", true);
            else
                OnStatus?.Invoke(status);
        }

        private void RemoveFormatter(int index)
        {
            if (index < 0 || index >= _canvas.segments.Count)
                return;

            string readable = _canvas.segments[index].ReadableText();
            _canvas.segments[index] = new PungentRichDocumentCanvasSegment
            {
                kind = PungentRichDocumentStreamNodeKind.Text,
                rawText = readable,
                text = readable
            };
            _activeSegmentIndex = index;
            _editingSegmentIndex = index;
            CommitCanvas("Removed formatter and preserved text.", true);
        }

        private void DeleteSegment(int index)
        {
            if (index < 0 || index >= _canvas.segments.Count)
                return;

            _canvas.segments.RemoveAt(index);
            if (_canvas.segments.Count == 0)
                _canvas.segments.Add(new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Text });
            _activeSegmentIndex = Mathf.Clamp(index - 1, 0, _canvas.segments.Count - 1);
            _editingSegmentIndex = -1;
            CommitCanvas("Deleted document content.", true);
        }

        private void MoveSegment(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _canvas.segments.Count)
                return;

            int target = Mathf.Clamp(toIndex, 0, _canvas.segments.Count - 1);
            if (fromIndex == target)
                return;

            CommitActiveFieldValue();
            PungentRichDocumentCanvasSegment moving = _canvas.segments[fromIndex];
            _canvas.segments.RemoveAt(fromIndex);
            _canvas.segments.Insert(target, moving);
            RemapAnnotationSegmentAnchors(fromIndex, target);
            _activeSegmentIndex = target;
            _editingSegmentIndex = -1;
            _selection.SetTextSelection(target, 0, 0, moving == null ? string.Empty : moving.ReadableText());
            OnSelectionChanged?.Invoke();
            CommitCanvas("Moved document line.", true);
        }

        private void RemapAnnotationSegmentAnchors(int fromIndex, int toIndex)
        {
            if (_document == null || _document.annotations == null || fromIndex == toIndex)
                return;

            foreach (PungentRichDocumentAnnotation annotation in _document.annotations)
            {
                if (annotation == null || annotation.sourceSegmentIndex < 0)
                    continue;

                if (annotation.sourceSegmentIndex == fromIndex)
                {
                    annotation.sourceSegmentIndex = toIndex;
                    annotation.sourceLine = toIndex + 1;
                    annotation.Touch();
                    continue;
                }

                if (fromIndex < toIndex &&
                    annotation.sourceSegmentIndex > fromIndex &&
                    annotation.sourceSegmentIndex <= toIndex)
                {
                    annotation.sourceSegmentIndex--;
                    annotation.sourceLine = annotation.sourceSegmentIndex + 1;
                    annotation.Touch();
                }
                else if (fromIndex > toIndex &&
                         annotation.sourceSegmentIndex >= toIndex &&
                         annotation.sourceSegmentIndex < fromIndex)
                {
                    annotation.sourceSegmentIndex++;
                    annotation.sourceLine = annotation.sourceSegmentIndex + 1;
                    annotation.Touch();
                }
            }
        }

        private void AddRelatedElementAfter(int index, PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return;

            PungentRichDocumentStreamNodeKind nextKind = segment.kind;
            if (segment.kind == PungentRichDocumentStreamNodeKind.DialogueLine)
                nextKind = PungentRichDocumentStreamNodeKind.DialogueLine;
            else if (segment.kind == PungentRichDocumentStreamNodeKind.DialogueChoice)
                nextKind = PungentRichDocumentStreamNodeKind.DialogueChoice;
            else if (segment.kind == PungentRichDocumentStreamNodeKind.Code)
                nextKind = PungentRichDocumentStreamNodeKind.Text;

            PungentRichDocumentCanvasSegment related = PungentRichDocumentDocumentCanvasParser.CreateIntegration(nextKind);
            int insertAt = Mathf.Clamp(index + 1, 0, _canvas.segments.Count);
            _canvas.segments.Insert(insertAt, related);
            _activeSegmentIndex = insertAt;
            _editingSegmentIndex = related.IsPlainTextEditable ? insertAt : -1;
            CommitCanvas("Added related " + IntegrationLabel(nextKind) + ".", true);
        }

        private void AddRepeatableElement(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentInsertionRepeatableElementDefinition repeatable)
        {
            if (segment == null || repeatable == null)
                return;

            int nextIndex = RepeatableCount(segment, repeatable);
            foreach (PungentRichDocumentInsertionFieldDefinition field in repeatable.fields ?? new List<PungentRichDocumentInsertionFieldDefinition>())
            {
                if (field == null || string.IsNullOrWhiteSpace(field.key))
                    continue;

                SetSemanticField(segment, PungentRichDocumentInsertionDefinitionRegistry.RepeatableFieldKey(repeatable.key, nextIndex, field.key), field.defaultValue);
            }

            _activeSegmentIndex = index;
            _editingSegmentIndex = index;
            CommitCanvas("Added " + repeatable.displayName.ToLowerInvariant() + ".", true);
        }

        private void AddAnnotationAtActiveSegment(PungentRichDocumentAnnotationKind kind)
        {
            int index = _activeSegmentIndex;
            if (index < 0 && _selection.HasSegment)
                index = _selection.segmentIndex;
            AddAnnotationAtSegment(index, kind);
        }

        private void AddAnnotationAtSegment(int index, PungentRichDocumentAnnotationKind kind)
        {
            if (_document == null)
                return;

            if (_canvas.segments.Count == 0)
                ReparseFromDocument();
            if (index < 0 || index >= _canvas.segments.Count)
                index = Mathf.Clamp(_canvas.segments.Count - 1, 0, _canvas.segments.Count - 1);
            if (index < 0)
                return;

            if (_document.annotations == null)
                _document.annotations = new List<PungentRichDocumentAnnotation>();

            PungentRichDocumentCanvasSegment segment = _canvas.segments[index];
            string source = segment == null ? string.Empty : segment.ToRawText();
            PungentRichDocumentAnnotation annotation = PungentRichDocumentAnnotation.Create(kind, source, index, segment == null ? 0 : segment.sourceLine);
            _document.annotations.Add(annotation);
            _activeSegmentIndex = index;
            _selection.SetAnnotationSelection(index, annotation.id, source);
            OnSelectionChanged?.Invoke();
            CommitCanvas("Added " + AnnotationKindLabel(kind).ToLowerInvariant() + ".", true);
        }

        private void SelectAnnotation(int index, PungentRichDocumentCanvasSegment segment, PungentRichDocumentAnnotation annotation)
        {
            if (annotation == null)
                return;

            _activeSegmentIndex = index;
            _selection.SetAnnotationSelection(index, annotation.id, segment == null ? annotation.sourceText : segment.ToRawText());
            OnSelectionChanged?.Invoke();
        }

        private void SuppressIssue(string issueCode, string source)
        {
            if (_document == null || string.IsNullOrWhiteSpace(issueCode))
                return;

            _document.SuppressValidation(issueCode, source, "Ignored from Rich Document Editor.");
            CommitCanvas("Ignored warning for this source.", true);
        }

        private bool IsSuppressed(string issueCode, string source)
        {
            return _document != null && _document.IsValidationSuppressed(issueCode, source);
        }

        private IEnumerable<PungentRichDocumentAnnotation> GetAnnotationsForSegment(int index, PungentRichDocumentCanvasSegment segment)
        {
            if (_document == null || _document.annotations == null)
                return Enumerable.Empty<PungentRichDocumentAnnotation>();

            string source = segment == null ? string.Empty : segment.ToRawText();
            string fingerprint = PungentRichDocumentValidationSuppression.ComputeFingerprint(source);
            return _document.annotations
                .Where(annotation => annotation != null && !annotation.archived)
                .Where(annotation =>
                    annotation.sourceSegmentIndex == index ||
                    (!string.IsNullOrWhiteSpace(annotation.sourceFingerprint) &&
                     string.Equals(annotation.sourceFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(annotation => annotation.kind)
                .ThenBy(annotation => annotation.createdUtc, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void CommitCanvas(string status, bool refresh)
        {
            CommitBodyToDocument();
            OnDirty?.Invoke(status);
            OnStatus?.Invoke(status);
            if (refresh)
            {
                ReparseFromDocument();
                Refresh();
            }
        }

        private void CommitActiveFieldValue()
        {
            if (_activeTextField == null || _editingSegmentIndex < 0 || _editingSegmentIndex >= _canvas.segments.Count)
                return;

            ApplyEditableValue(_canvas.segments[_editingSegmentIndex], _activeTextField.value ?? string.Empty);
        }

        private bool TryInsertIntoActiveText(string text)
        {
            if (_activeTextField != null && _editingSegmentIndex >= 0 && _editingSegmentIndex < _canvas.segments.Count)
            {
                string current = _activeTextField.value ?? string.Empty;
                int cursor = Mathf.Clamp(ReadTextFieldIndex(_activeTextField, "cursorIndex", current.Length), 0, current.Length);
                int select = Mathf.Clamp(ReadTextFieldIndex(_activeTextField, "selectIndex", cursor), 0, current.Length);
                int start = Math.Min(cursor, select);
                int end = Math.Max(cursor, select);
                string next = current.Substring(0, start) + text + current.Substring(end);
                _activeTextField.SetValueWithoutNotify(next);
                ApplyEditableValue(_canvas.segments[_editingSegmentIndex], next);
                WriteTextFieldIndex(_activeTextField, "cursorIndex", start + (text ?? string.Empty).Length);
                WriteTextFieldIndex(_activeTextField, "selectIndex", start + (text ?? string.Empty).Length);
                return true;
            }

            if (_activeSegmentIndex >= 0 &&
                _activeSegmentIndex < _canvas.segments.Count &&
                _canvas.segments[_activeSegmentIndex].IsPlainTextEditable)
            {
                PungentRichDocumentCanvasSegment segment = _canvas.segments[_activeSegmentIndex];
                string current = EditableValue(segment);
                string next = string.IsNullOrWhiteSpace(current) ? text : current.TrimEnd() + " " + text;
                ApplyEditableValue(segment, next);
                _editingSegmentIndex = _activeSegmentIndex;
                return true;
            }

            return false;
        }

        private void InsertSegmentsAtActivePosition(IEnumerable<PungentRichDocumentCanvasSegment> segments, string status)
        {
            List<PungentRichDocumentCanvasSegment> inserted = (segments ?? Enumerable.Empty<PungentRichDocumentCanvasSegment>())
                .Where(segment => segment != null)
                .ToList();
            if (inserted.Count == 0)
                return;

            int insertAt = Mathf.Clamp(_activeSegmentIndex + 1, 0, _canvas.segments.Count);
            if (_activeSegmentIndex >= 0 &&
                _activeSegmentIndex < _canvas.segments.Count &&
                _canvas.segments[_activeSegmentIndex].kind == PungentRichDocumentStreamNodeKind.Text &&
                string.IsNullOrWhiteSpace(_canvas.segments[_activeSegmentIndex].rawText))
            {
                insertAt = _activeSegmentIndex;
                _canvas.segments.RemoveAt(_activeSegmentIndex);
            }

            _canvas.segments.InsertRange(insertAt, inserted);
            _activeSegmentIndex = insertAt;
            _editingSegmentIndex = inserted[0].IsPlainTextEditable ? insertAt : -1;
            CommitCanvas(status, true);
        }

        private void SplitTextEditorAtCursor(int index, PungentRichDocumentCanvasSegment segment, TextField field)
        {
            if (index < 0 || index >= _canvas.segments.Count)
                return;

            string value = field.value ?? string.Empty;
            int cursor = Mathf.Clamp(ReadTextFieldIndex(field, "cursorIndex", value.Length), 0, value.Length);
            string before = value.Substring(0, cursor).TrimEnd();
            string after = value.Substring(cursor).TrimStart();

            ApplyEditableValue(segment, before);
            _canvas.segments[index] = SegmentFromEditedValue(segment, before);
            PungentRichDocumentCanvasSegment next = new PungentRichDocumentCanvasSegment
            {
                kind = PungentRichDocumentStreamNodeKind.Text,
                rawText = after,
                text = after
            };
            _canvas.segments.Insert(index + 1, next);
            _activeTextField = null;
            _activeSegmentIndex = index + 1;
            _editingSegmentIndex = index + 1;
            CommitCanvas("Added paragraph.", true);
        }

        private void HandleDocumentRootMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.target != _documentRoot)
                return;

            _canvas.segments.Add(new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Text });
            _activeSegmentIndex = _canvas.segments.Count - 1;
            _editingSegmentIndex = _activeSegmentIndex;
            CommitCanvas("Added paragraph.", true);
            evt.StopPropagation();
        }

        private void CaptureTextSelection(int index, TextField field)
        {
            if (field == null)
                return;

            string source = field.value ?? string.Empty;
            int cursor = Mathf.Clamp(ReadTextFieldIndex(field, "cursorIndex", source.Length), 0, source.Length);
            int select = Mathf.Clamp(ReadTextFieldIndex(field, "selectIndex", cursor), 0, source.Length);
            _selection.SetTextSelection(index, Math.Min(cursor, select), Math.Max(cursor, select), source);
            OnSelectionChanged?.Invoke();
        }

        private void SelectSemanticSegment(int index, PungentRichDocumentCanvasSegment segment)
        {
            if (_document == null || segment == null)
                return;

            EnsureSegmentHasSemanticIdentity(segment);
            string raw = segment.ToRawText();
            if (!PungentRichDocumentSemanticParser.TryParseWholeSemanticTag(raw, out PungentRichDocumentSemanticSpan span))
            {
                span = new PungentRichDocumentSemanticSpan
                {
                    id = segment.semanticId,
                    kind = segment.semanticKind == PungentRichDocumentSemanticKind.None ? SemanticKindForStreamKind(segment.kind) : segment.semanticKind,
                    innerText = segment.text ?? string.Empty,
                    sourceText = raw,
                    fields = segment.semanticFields ?? new List<PungentRichDocumentSemanticField>()
                };
            }

            PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.GetOrCreateBinding(_document, span);
            _selection.SetSemanticSelection(index, binding == null ? string.Empty : binding.id, segment.ReadableText());
            OnSelectionChanged?.Invoke();
        }

        private void SelectSemanticSpan(int index, PungentRichDocumentSemanticSpan span)
        {
            if (_document == null || span == null)
                return;

            PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.GetOrCreateBinding(_document, span);
            _selection.SetSemanticSelection(index, binding == null ? string.Empty : binding.id, span.innerText);
            OnSelectionChanged?.Invoke();
        }

        private static void EnsureSegmentHasSemanticIdentity(PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return;

            if (string.IsNullOrWhiteSpace(segment.semanticId))
                segment.semanticId = Guid.NewGuid().ToString("N");
            if (segment.semanticKind == PungentRichDocumentSemanticKind.None)
                segment.semanticKind = SemanticKindForStreamKind(segment.kind);
            if (segment.semanticFields == null)
                segment.semanticFields = PungentRichDocumentSemanticParser.DefaultFieldsForKind(segment.semanticKind);
            if (segment.kind == PungentRichDocumentStreamNodeKind.DialogueLine && !string.IsNullOrWhiteSpace(segment.speaker))
                SetSemanticField(segment, "speaker", segment.speaker);
            if (segment.kind == PungentRichDocumentStreamNodeKind.CommandPlaceholder && !string.IsNullOrWhiteSpace(segment.commandKey))
                SetSemanticField(segment, "key", segment.commandKey);
            if (segment.kind == PungentRichDocumentStreamNodeKind.GameCopy && !string.IsNullOrWhiteSpace(segment.label))
                SetSemanticField(segment, "category", segment.label);
            segment.isSemanticSpan = true;
        }

        private static PungentRichDocumentInsertionDefinition DefinitionForSegment(PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null || segment.semanticFields == null)
                return null;

            string definitionId = PungentRichDocumentInsertionDefinitionRegistry.GetDefinitionId(segment.semanticFields);
            return PungentRichDocumentInsertionDefinitionRegistry.Find(definitionId);
        }

        private static string GetSemanticField(PungentRichDocumentCanvasSegment segment, string key)
        {
            if (segment == null || segment.semanticFields == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            PungentRichDocumentSemanticField field = segment.semanticFields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return field == null ? string.Empty : field.value ?? string.Empty;
        }

        private static int RepeatableCount(PungentRichDocumentCanvasSegment segment, PungentRichDocumentInsertionRepeatableElementDefinition repeatable)
        {
            if (segment == null || repeatable == null || repeatable.fields == null || repeatable.fields.Count == 0)
                return 0;

            int max = -1;
            string prefix = PungentRichDocumentInsertionDefinitionRegistry.RepeatablePrefix + PungentAuthoringId.Normalize(repeatable.key) + ".";
            foreach (PungentRichDocumentSemanticField field in segment.semanticFields ?? new List<PungentRichDocumentSemanticField>())
            {
                if (field == null || string.IsNullOrWhiteSpace(field.key) || !field.key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string remainder = field.key.Substring(prefix.Length);
                int dot = remainder.IndexOf('.');
                if (dot <= 0)
                    continue;

                int parsed;
                if (int.TryParse(remainder.Substring(0, dot), out parsed))
                    max = Math.Max(max, parsed);
            }

            return Math.Max(1, max + 1);
        }

        private static bool CanExtendInsertion(PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return false;

            if (segment.semanticKind == PungentRichDocumentSemanticKind.CustomInsertion)
            {
                PungentRichDocumentInsertionDefinition definition = DefinitionForSegment(segment);
                return definition != null && definition.HasRepeatableElements;
            }

            return segment.kind == PungentRichDocumentStreamNodeKind.DialogueLine ||
                   segment.kind == PungentRichDocumentStreamNodeKind.DialogueChoice ||
                   segment.kind == PungentRichDocumentStreamNodeKind.QuestObjective ||
                   segment.kind == PungentRichDocumentStreamNodeKind.TutorialStep;
        }

        private static void SetSemanticField(PungentRichDocumentCanvasSegment segment, string key, string value)
        {
            if (segment == null || string.IsNullOrWhiteSpace(key))
                return;

            if (segment.semanticFields == null)
                segment.semanticFields = new List<PungentRichDocumentSemanticField>();

            PungentRichDocumentSemanticField field = segment.semanticFields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            if (field == null)
                segment.semanticFields.Add(PungentRichDocumentSemanticField.Create(key, value));
            else
                field.value = value ?? string.Empty;
        }

        private static string EditableValue(PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return string.Empty;

            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.Heading:
                case PungentRichDocumentStreamNodeKind.Quote:
                    return segment.text ?? string.Empty;
                case PungentRichDocumentStreamNodeKind.Blank:
                    return string.Empty;
                default:
                    return segment.rawText ?? segment.text ?? string.Empty;
            }
        }

        private static void ApplyEditableValue(PungentRichDocumentCanvasSegment segment, string value)
        {
            if (segment == null)
                return;

            string safe = value ?? string.Empty;
            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.Heading:
                case PungentRichDocumentStreamNodeKind.Quote:
                    segment.text = safe;
                    break;
                case PungentRichDocumentStreamNodeKind.Blank:
                case PungentRichDocumentStreamNodeKind.Text:
                    segment.kind = PungentRichDocumentStreamNodeKind.Text;
                    segment.rawText = safe;
                    segment.text = safe;
                    break;
            }
        }

        private static PungentRichDocumentCanvasSegment SegmentFromEditedValue(PungentRichDocumentCanvasSegment original, string value)
        {
            if (original != null && original.kind == PungentRichDocumentStreamNodeKind.Heading)
                return new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Heading, text = value ?? string.Empty, headingLevel = original.headingLevel <= 0 ? 2 : original.headingLevel };
            if (original != null && original.kind == PungentRichDocumentStreamNodeKind.Quote)
                return new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Quote, text = value ?? string.Empty };
            if (string.IsNullOrWhiteSpace(value))
                return new PungentRichDocumentCanvasSegment { kind = PungentRichDocumentStreamNodeKind.Blank };

            return PungentRichDocumentDocumentCanvasParser.CreateSegmentFromRaw(value ?? string.Empty);
        }

        private static bool LooksLikeBlockSnippet(string snippet)
        {
            string trimmed = (snippet ?? string.Empty).TrimStart();
            return trimmed.Contains("\n") ||
                   trimmed.StartsWith("#", StringComparison.Ordinal) ||
                   trimmed.StartsWith(">", StringComparison.Ordinal) ||
                   trimmed.StartsWith("---", StringComparison.Ordinal) ||
                   trimmed.StartsWith("```", StringComparison.Ordinal) ||
                   trimmed.StartsWith("[[", StringComparison.Ordinal) ||
                   trimmed.StartsWith("<command:", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("<<", StringComparison.Ordinal) ||
                   trimmed.StartsWith("->", StringComparison.Ordinal) ||
                   trimmed.StartsWith("* ", StringComparison.Ordinal) ||
                   trimmed.StartsWith("*\t", StringComparison.Ordinal) ||
                   trimmed.StartsWith("TODO", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("- [", StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith("1. [step]", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInlineSemanticSegment(PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return false;

            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                case PungentRichDocumentStreamNodeKind.TutorialStep:
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    return true;
                default:
                    return false;
            }
        }

        private static string TokenIssueCode(PungentRichDocumentCanvasTokenRun run, PungentRichDocumentTokenResolution resolution)
        {
            if (run == null)
                return string.Empty;
            if (run.isCustomChip)
                return string.Empty;
            if (!run.validSyntax)
                return IssueInvalidTokenSyntax;
            if (resolution != null && !resolution.known)
                return IssueUnknownToken;
            return string.Empty;
        }

        private static Color TokenTint(PungentRichDocumentCanvasTokenRun run, PungentRichDocumentTokenResolution resolution, bool suppressed)
        {
            if (suppressed)
                return UtilityWindowTheme.Neutral;
            if (run != null && run.isCustomChip)
                return UtilityWindowTheme.Purple;
            if (run == null || !run.validSyntax)
                return UtilityWindowTheme.Red;
            if (resolution == null || !resolution.known)
                return UtilityWindowTheme.Amber;
            return UtilityWindowTheme.Teal;
        }

        private static string SegmentIssueCode(PungentRichDocumentCanvasSegment segment)
        {
            if (segment == null)
                return string.Empty;

            switch (segment.kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                    return string.IsNullOrWhiteSpace(segment.text) ? "EMPTY_SPEAKER_LINE" : string.Empty;
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                    return string.IsNullOrWhiteSpace(segment.text) ? "EMPTY_DIALOGUE_CHOICE" : string.Empty;
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                    return string.IsNullOrWhiteSpace(segment.text) ? "EMPTY_QUEST_OBJECTIVE" : string.Empty;
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    return string.IsNullOrWhiteSpace(segment.commandKey) ? "EMPTY_COMMAND_PLACEHOLDER" : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static string WarningTooltip(string issueCode)
        {
            switch (issueCode)
            {
                case "EMPTY_SPEAKER_LINE": return "Speaker line is missing dialogue text.";
                case "EMPTY_DIALOGUE_CHOICE": return "Dialogue choice is empty.";
                case "EMPTY_QUEST_OBJECTIVE": return "Quest objective is empty.";
                case "EMPTY_COMMAND_PLACEHOLDER": return "Command placeholder is missing its command key.";
                default: return "This source has a local validation warning.";
            }
        }

        private static string BuildAnnotationTooltip(PungentRichDocumentAnnotation annotation, bool stale)
        {
            if (annotation == null)
                return string.Empty;

            string title = string.IsNullOrWhiteSpace(annotation.title) ? AnnotationKindLabel(annotation.kind) : annotation.title.Trim();
            string body = string.IsNullOrWhiteSpace(annotation.body) ? "Click to edit." : annotation.body.Trim();
            return title + Environment.NewLine + body + (stale ? Environment.NewLine + "Source text changed; review or re-anchor this annotation." : string.Empty);
        }

        private static string AnnotationKindLabel(PungentRichDocumentAnnotationKind kind)
        {
            return kind == PungentRichDocumentAnnotationKind.Bookmark ? "Bookmark" : "Comment";
        }

        private static string ScriptLineDisplayName(PungentRichDocumentScriptLineKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentScriptLineKind.YarnOption: return "Option";
                case PungentRichDocumentScriptLineKind.YarnCommand: return "Command";
                case PungentRichDocumentScriptLineKind.InkChoice: return "Choice";
                case PungentRichDocumentScriptLineKind.InkTag: return "Tag";
                case PungentRichDocumentScriptLineKind.InkKnot: return "Knot";
                case PungentRichDocumentScriptLineKind.InkDivert: return "Divert";
                case PungentRichDocumentScriptLineKind.AuthorComment: return "Comment";
                case PungentRichDocumentScriptLineKind.Todo: return "TODO";
                default: return string.Empty;
            }
        }

        private static string ScriptLineRawHint(PungentRichDocumentScriptLineKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentScriptLineKind.YarnOption: return "->";
                case PungentRichDocumentScriptLineKind.YarnCommand: return "<< >>";
                case PungentRichDocumentScriptLineKind.InkChoice: return "*";
                case PungentRichDocumentScriptLineKind.InkTag: return "#";
                case PungentRichDocumentScriptLineKind.InkKnot: return "===";
                case PungentRichDocumentScriptLineKind.InkDivert: return "->";
                case PungentRichDocumentScriptLineKind.AuthorComment: return "//";
                case PungentRichDocumentScriptLineKind.Todo: return "TODO";
                default: return string.Empty;
            }
        }

        private static string ScriptLineTooltip(PungentRichDocumentScriptLineKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentScriptLineKind.YarnOption: return "Yarn-style option line. Recognition is local styling only; no Yarn export is performed in this pass.";
                case PungentRichDocumentScriptLineKind.YarnCommand: return "Yarn-style command line. Commands should be applied through explicit bindings or later adapters.";
                case PungentRichDocumentScriptLineKind.InkChoice: return "Ink-style choice marker. Recognition is local styling only; no Ink export is performed in this pass.";
                case PungentRichDocumentScriptLineKind.InkTag: return "Ink-style tag marker for game-side metadata.";
                case PungentRichDocumentScriptLineKind.InkKnot: return "Ink-style knot marker for script structure.";
                case PungentRichDocumentScriptLineKind.InkDivert: return "Ink-style divert marker for script flow.";
                case PungentRichDocumentScriptLineKind.AuthorComment: return "Author-facing script comment.";
                case PungentRichDocumentScriptLineKind.Todo: return "Author-facing TODO note.";
                default: return string.Empty;
            }
        }

        private static string IntegrationLabel(PungentRichDocumentStreamNodeKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine: return "Dialogue";
                case PungentRichDocumentStreamNodeKind.DialogueChoice: return "Choice";
                case PungentRichDocumentStreamNodeKind.QuestObjective: return "Objective";
                case PungentRichDocumentStreamNodeKind.TutorialStep: return "Tutorial Step";
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder: return "Command";
                case PungentRichDocumentStreamNodeKind.Code: return "Code";
                case PungentRichDocumentStreamNodeKind.Quote: return "Quote";
                case PungentRichDocumentStreamNodeKind.GameCopy: return "Game Copy";
                case PungentRichDocumentStreamNodeKind.Heading: return "Heading";
                default: return "Text";
            }
        }

        private static string FieldLabelForSegment(PungentRichDocumentStreamNodeKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueChoice: return "Choice";
                case PungentRichDocumentStreamNodeKind.QuestObjective: return "Objective";
                case PungentRichDocumentStreamNodeKind.TutorialStep: return "Step";
                default: return "Text";
            }
        }

        private static Color TintForSegment(PungentRichDocumentStreamNodeKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine:
                case PungentRichDocumentStreamNodeKind.DialogueChoice:
                    return UtilityWindowTheme.Purple;
                case PungentRichDocumentStreamNodeKind.QuestObjective:
                    return UtilityWindowTheme.Green;
                case PungentRichDocumentStreamNodeKind.TutorialStep:
                    return UtilityWindowTheme.Blue;
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder:
                    return UtilityWindowTheme.Amber;
                case PungentRichDocumentStreamNodeKind.GameCopy:
                    return UtilityWindowTheme.Teal;
                case PungentRichDocumentStreamNodeKind.Code:
                    return UtilityWindowTheme.Neutral;
                default:
                    return UtilityWindowTheme.Cyan;
            }
        }

        private static PungentRichDocumentSemanticKind SemanticKindForStreamKind(PungentRichDocumentStreamNodeKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentStreamNodeKind.DialogueLine: return PungentRichDocumentSemanticKind.DialogueLine;
                case PungentRichDocumentStreamNodeKind.DialogueChoice: return PungentRichDocumentSemanticKind.DialogueChoice;
                case PungentRichDocumentStreamNodeKind.QuestObjective: return PungentRichDocumentSemanticKind.QuestObjective;
                case PungentRichDocumentStreamNodeKind.TutorialStep: return PungentRichDocumentSemanticKind.TutorialStep;
                case PungentRichDocumentStreamNodeKind.CommandPlaceholder: return PungentRichDocumentSemanticKind.Command;
                case PungentRichDocumentStreamNodeKind.GameCopy: return PungentRichDocumentSemanticKind.GameCopy;
                default: return PungentRichDocumentSemanticKind.None;
            }
        }

        private static Color TintForSemanticKind(PungentRichDocumentSemanticKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentSemanticKind.DialogueLine:
                case PungentRichDocumentSemanticKind.DialogueChoice:
                    return UtilityWindowTheme.Purple;
                case PungentRichDocumentSemanticKind.QuestObjective:
                    return UtilityWindowTheme.Green;
                case PungentRichDocumentSemanticKind.TutorialStep:
                    return UtilityWindowTheme.Blue;
                case PungentRichDocumentSemanticKind.Command:
                    return UtilityWindowTheme.Amber;
                case PungentRichDocumentSemanticKind.GameCopy:
                case PungentRichDocumentSemanticKind.HintCopy:
                case PungentRichDocumentSemanticKind.ItemCopy:
                case PungentRichDocumentSemanticKind.CharacterCopy:
                    return UtilityWindowTheme.Teal;
                case PungentRichDocumentSemanticKind.Token:
                    return UtilityWindowTheme.Cyan;
                case PungentRichDocumentSemanticKind.CustomChip:
                    return UtilityWindowTheme.Purple;
                case PungentRichDocumentSemanticKind.CustomInsertion:
                    return UtilityWindowTheme.Teal;
                default:
                    return UtilityWindowTheme.Neutral;
            }
        }

        private static Color TintForScriptLineKind(PungentRichDocumentScriptLineKind kind)
        {
            switch (kind)
            {
                case PungentRichDocumentScriptLineKind.YarnOption:
                case PungentRichDocumentScriptLineKind.InkChoice:
                    return UtilityWindowTheme.Purple;
                case PungentRichDocumentScriptLineKind.YarnCommand:
                    return UtilityWindowTheme.Amber;
                case PungentRichDocumentScriptLineKind.InkTag:
                    return UtilityWindowTheme.Teal;
                case PungentRichDocumentScriptLineKind.InkKnot:
                case PungentRichDocumentScriptLineKind.InkDivert:
                    return UtilityWindowTheme.Blue;
                case PungentRichDocumentScriptLineKind.Todo:
                    return UtilityWindowTheme.Red;
                case PungentRichDocumentScriptLineKind.AuthorComment:
                    return UtilityWindowTheme.Neutral;
                default:
                    return UtilityWindowTheme.Cyan;
            }
        }

        private static void SplitSpeaker(string text, out string speaker, out string line)
        {
            speaker = "NPC";
            line = text ?? string.Empty;
            int colon = (text ?? string.Empty).IndexOf(':');
            if (colon <= 0)
                return;

            speaker = text.Substring(0, colon).Trim();
            line = text.Substring(colon + 1).Trim();
            if (string.IsNullOrWhiteSpace(speaker))
                speaker = "NPC";
        }

        private static string StripLeadingConvention(string text)
        {
            string clean = (text ?? string.Empty).Trim();
            if (clean.StartsWith("- [choice]", StringComparison.OrdinalIgnoreCase))
                return clean.Substring(10).Trim();
            if (clean.StartsWith("- [objective]", StringComparison.OrdinalIgnoreCase))
                return clean.Substring(13).Trim();
            if (clean.StartsWith("1. [step]", StringComparison.OrdinalIgnoreCase))
                return clean.Substring(9).Trim();
            if (clean.StartsWith(">", StringComparison.Ordinal))
                return clean.TrimStart('>').Trim();
            if (clean.StartsWith("#", StringComparison.Ordinal))
                return clean.TrimStart('#').Trim();
            return clean;
        }

        private static int ReadTextFieldIndex(TextField field, string propertyName, int fallback)
        {
            if (field == null)
                return fallback;

            PropertyInfo property = typeof(TextField).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(int))
                return fallback;

            try
            {
                return (int)property.GetValue(field, null);
            }
            catch
            {
                return fallback;
            }
        }

        private static void WriteTextFieldIndex(TextField field, string propertyName, int value)
        {
            if (field == null)
                return;

            PropertyInfo property = typeof(TextField).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(int) || !property.CanWrite)
                return;

            try
            {
                property.SetValue(field, value, null);
            }
            catch
            {
                // Older Unity versions expose selection differently; insertion still succeeds without moving the caret.
            }
        }

        private static void MoveFieldCursorToEnd(TextField field)
        {
            if (field == null)
                return;

            int end = (field.value ?? string.Empty).Length;
            WriteTextFieldIndex(field, "cursorIndex", end);
            WriteTextFieldIndex(field, "selectIndex", end);
        }
    }
#endif
}
