using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentPropertiesView : VisualElement
    {
        private readonly Action<PungentRichDocumentSemanticKind> _convertSelection;
        private readonly Action<PungentRichDocumentInsertionDefinition> _convertSelectionToCustomInsertion;
        private readonly Action<PungentRichDocumentAnnotationKind> _addAnnotation;
        private readonly Action<string> _markDirty;
        private readonly Action<string> _setStatus;
        private readonly Action _closeTray;
        private readonly Action _openPropagationPreview;

        private PungentRichDocument _document;
        private PungentRichDocumentTextSelectionState _selection;
        private string _bindingId = string.Empty;
        private string _annotationEditId = string.Empty;
        private readonly PungentAuthoringGuidedBindingState _bindingPickerState = new PungentAuthoringGuidedBindingState();

        public PungentRichDocumentPropertiesView(
            Action<PungentRichDocumentSemanticKind> convertSelection,
            Action<PungentRichDocumentAnnotationKind> addAnnotation,
            Action<string> markDirty,
            Action<string> setStatus,
            Action closeTray = null,
            Action<PungentRichDocumentInsertionDefinition> convertSelectionToCustomInsertion = null,
            Action openPropagationPreview = null)
        {
            _convertSelection = convertSelection;
            _convertSelectionToCustomInsertion = convertSelectionToCustomInsertion;
            _addAnnotation = addAnnotation;
            _markDirty = markDirty;
            _setStatus = setStatus;
            _closeTray = closeTray;
            _openPropagationPreview = openPropagationPreview;

            style.width = 286;
            style.minWidth = 248;
            style.flexShrink = 0;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.paddingTop = 12;
            style.paddingBottom = 12;
            style.borderLeftWidth = 1;
            style.borderLeftColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.28f, 0.29f, 0.31f) : new Color(0.68f, 0.7f, 0.72f));
            style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.145f, 0.15f, 0.155f) : new Color(0.88f, 0.895f, 0.91f));
        }

        public void Bind(PungentRichDocument document, PungentRichDocumentTextSelectionState selection, string bindingId)
        {
            _document = document;
            _selection = selection == null ? null : selection.Copy();
            _bindingId = bindingId ?? string.Empty;
            if (_selection != null && _selection.HasAnnotationSelection)
                _annotationEditId = _selection.annotationId;
            Refresh();
        }

        private void Refresh()
        {
            Clear();
            AddHeader("Properties");

            if (_document == null)
            {
                AddMuted("No rich document is open.");
                return;
            }

            PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.FindBinding(_document, _bindingId);
            if (binding != null)
            {
                DrawBinding(binding);
                return;
            }

            if (_selection != null && _selection.HasTokenSelection)
            {
                DrawTokenSelection(_selection);
                return;
            }

            if (_selection != null && _selection.HasTextSelection)
            {
                _annotationEditId = string.Empty;
                DrawSelectionActions(_selection);
                return;
            }

            PungentRichDocumentAnnotation annotation = FindAnnotation(_selection == null ? _annotationEditId : (string.IsNullOrWhiteSpace(_selection.annotationId) ? _annotationEditId : _selection.annotationId));
            if (annotation != null)
            {
                DrawAnnotation(annotation);
                return;
            }

            DrawDocumentSummary();
        }

        private void DrawDocumentSummary()
        {
            AddMuted("Select text to mark it as dialogue, quest, tutorial, copy, command, or a token-aware integration.");
            AddSeparator();
            AddKeyValue("Document", string.IsNullOrWhiteSpace(_document.title) ? "Untitled Document" : _document.title);
            AddKeyValue("Semantic bindings", (_document.semanticBindings == null ? 0 : _document.semanticBindings.Count).ToString());
            AddKeyValue("Comments/bookmarks", (_document.annotations == null ? 0 : _document.annotations.Count(annotation => annotation != null && !annotation.archived)).ToString());
            AddKeyValue("Targets", (_document.targets == null ? 0 : _document.targets.Count).ToString());
            DrawAnnotationSummary();
        }

        private void DrawAnnotationSummary()
        {
            List<PungentRichDocumentAnnotation> annotations = (_document.annotations ?? new List<PungentRichDocumentAnnotation>())
                .Where(annotation => annotation != null && !annotation.archived)
                .OrderBy(annotation => annotation.sourceSegmentIndex)
                .ThenBy(annotation => annotation.kind)
                .Take(12)
                .ToList();
            if (annotations.Count == 0)
                return;

            AddSeparator();
            AddMuted("Annotations");
            foreach (PungentRichDocumentAnnotation annotation in annotations)
            {
                PungentRichDocumentAnnotation captured = annotation;
                string label = (captured.kind == PungentRichDocumentAnnotationKind.Bookmark ? "Bookmark: " : "Comment: ") +
                               (string.IsNullOrWhiteSpace(captured.title) ? "Untitled" : captured.title);
                AddButton(Clamp(label, 54), () =>
                {
                    _annotationEditId = captured.id;
                    Refresh();
                }, captured.kind == PungentRichDocumentAnnotationKind.Bookmark ? UtilityWindowTheme.Blue : UtilityWindowTheme.Amber, "Open this annotation in the properties tray.");
            }
        }

        private void DrawSelectionActions(PungentRichDocumentTextSelectionState selection)
        {
            AddMuted("Selected text");
            Label preview = new Label(Clamp(selection.selectedText, 180));
            preview.style.whiteSpace = WhiteSpace.Normal;
            preview.style.marginBottom = 8;
            Add(preview);

            AddButton("Mark As Dialogue", () => _convertSelection?.Invoke(PungentRichDocumentSemanticKind.DialogueLine), UtilityWindowTheme.Purple);
            AddButton("Mark As Choice", () => _convertSelection?.Invoke(PungentRichDocumentSemanticKind.DialogueChoice), UtilityWindowTheme.Purple);
            AddButton("Mark As Quest Objective", () => _convertSelection?.Invoke(PungentRichDocumentSemanticKind.QuestObjective), UtilityWindowTheme.Green);
            AddButton("Mark As Tutorial Step", () => _convertSelection?.Invoke(PungentRichDocumentSemanticKind.TutorialStep), UtilityWindowTheme.Blue);
            AddButton("Mark As Game Copy", () => _convertSelection?.Invoke(PungentRichDocumentSemanticKind.GameCopy), UtilityWindowTheme.Teal);
            AddButton("Mark As Command", () => _convertSelection?.Invoke(PungentRichDocumentSemanticKind.Command), UtilityWindowTheme.Amber);

            if (_convertSelectionToCustomInsertion != null && PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions.Count > 0)
            {
                AddSeparator();
                List<PungentRichDocumentInsertionDefinition> chips = PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions
                    .Where(definition => definition != null && definition.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip)
                    .Take(8)
                    .ToList();
                if (chips.Count > 0)
                {
                    AddMuted("Custom chips");
                    foreach (PungentRichDocumentInsertionDefinition definition in chips)
                    {
                        PungentRichDocumentInsertionDefinition captured = definition;
                        AddButton("Make Chip: " + definition.displayName, () => _convertSelectionToCustomInsertion.Invoke(captured), PungentRichDocumentInsertionDefinitionRegistry.TintForDefinition(definition, UtilityWindowTheme.Purple), "Wrap the selected text as an inline custom chip.");
                    }
                }

                List<PungentRichDocumentInsertionDefinition> insertions = PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions
                    .Where(definition => definition != null && definition.renderMode != PungentRichDocumentInsertionRenderMode.InlineChip)
                    .Take(10)
                    .ToList();
                if (insertions.Count > 0)
                {
                    AddMuted("Custom insertions");
                    foreach (PungentRichDocumentInsertionDefinition definition in insertions)
                    {
                        PungentRichDocumentInsertionDefinition captured = definition;
                        AddButton("Use: " + definition.displayName, () => _convertSelectionToCustomInsertion.Invoke(captured), PungentRichDocumentInsertionDefinitionRegistry.TintForDefinition(definition, UtilityWindowTheme.Teal), "Wrap the selected text in this custom insertion.");
                    }
                }
            }

            AddSeparator();
            AddButton("Add Comment", () => _addAnnotation?.Invoke(PungentRichDocumentAnnotationKind.Comment), UtilityWindowTheme.Amber, "Attach a comment to the active line or selection.");
            AddButton("Add Bookmark", () => _addAnnotation?.Invoke(PungentRichDocumentAnnotationKind.Bookmark), UtilityWindowTheme.Blue, "Attach a jump marker to the active line or selection.");
        }

        private void DrawTokenSelection(PungentRichDocumentTextSelectionState selection)
        {
            AddMuted("Selected token");
            AddKeyValue("Type", string.IsNullOrWhiteSpace(selection.tokenKind) ? "Token" : selection.tokenKind);
            AddKeyValue("Source", selection.tokenRawText);
            if (!string.IsNullOrWhiteSpace(selection.tokenKey))
                AddKeyValue("Key", selection.tokenKey);

            AddSeparator();
            AddMuted("Reference and custom-token actions are available from the token context menu in the document. Bound reference tokens show their target here after refinement.");
            AddButton("Open Propagation Panel", () => _openPropagationPreview?.Invoke(), UtilityWindowTheme.Cyan, "Open the expanded propagation preview for current-document outputs.");
        }

        private void DrawAnnotation(PungentRichDocumentAnnotation annotation)
        {
            AddKeyValue("Annotation", annotation.kind == PungentRichDocumentAnnotationKind.Bookmark ? "Bookmark" : "Comment");
            if (!string.IsNullOrWhiteSpace(annotation.sourceText))
                AddKeyValue("Source", Clamp(annotation.sourceText, 140));
            if (annotation.IsStaleForSource(_selection == null ? annotation.sourceText : _selection.selectedText))
                AddMuted("Source text may have changed. Review or re-anchor this marker.");

            TextField title = new TextField(annotation.kind == PungentRichDocumentAnnotationKind.Bookmark ? "Bookmark Title" : "Comment Title") { value = annotation.title ?? string.Empty };
            title.RegisterValueChangedCallback(evt =>
            {
                annotation.title = evt.newValue ?? string.Empty;
                annotation.Touch();
                _markDirty?.Invoke("Unsaved annotation changes");
            });
            Add(title);

            TextField body = new TextField(annotation.kind == PungentRichDocumentAnnotationKind.Bookmark ? "Location Notes" : "Comment Body")
            {
                value = annotation.body ?? string.Empty,
                multiline = true
            };
            body.style.minHeight = 72;
            body.RegisterValueChangedCallback(evt =>
            {
                annotation.body = evt.newValue ?? string.Empty;
                annotation.Touch();
                _markDirty?.Invoke("Unsaved annotation changes");
            });
            Add(body);

            if (annotation.kind == PungentRichDocumentAnnotationKind.Comment)
            {
                Toggle resolved = new Toggle("Resolved") { value = annotation.resolved };
                resolved.style.marginBottom = 4;
                resolved.RegisterValueChangedCallback(evt =>
                {
                    annotation.resolved = evt.newValue;
                    annotation.Touch();
                    _markDirty?.Invoke("Unsaved annotation changes");
                });
                Add(resolved);
            }
            else
            {
                AddMuted("Bookmarks are jump markers. Use the title for the label and the notes field for optional context.");
            }

            AddSeparator();
            AddButton("Re-anchor To Selection", () =>
            {
                if (_selection == null || !_selection.HasSegment)
                {
                    _setStatus?.Invoke("Select a document line before re-anchoring the annotation.");
                    return;
                }

                annotation.sourceSegmentIndex = _selection.segmentIndex;
                annotation.sourceLine = _selection.segmentIndex + 1;
                annotation.sourceText = string.IsNullOrWhiteSpace(_selection.selectedText) ? annotation.sourceText : _selection.selectedText;
                annotation.sourceFingerprint = PungentRichDocumentValidationSuppression.ComputeFingerprint(annotation.sourceText);
                annotation.Touch();
                _markDirty?.Invoke("Re-anchored annotation");
                _setStatus?.Invoke("Re-anchored annotation.");
                Refresh();
            }, UtilityWindowTheme.Cyan, "Move this comment/bookmark to the currently active line or text selection.");
            AddButton("Archive Annotation", () =>
            {
                annotation.archived = true;
                annotation.Touch();
                _markDirty?.Invoke("Archived annotation");
                _setStatus?.Invoke("Archived annotation.");
                _annotationEditId = string.Empty;
                Refresh();
            }, UtilityWindowTheme.Neutral, "Hide this annotation without deleting document text.");
            AddButton("Back To Summary", () =>
            {
                _annotationEditId = string.Empty;
                Refresh();
            }, UtilityWindowTheme.Neutral, "Return to the document metadata and annotation summary.");
        }

        private void DrawBinding(PungentRichDocumentSemanticBinding binding)
        {
            AddKeyValue("Type", PungentRichDocumentSemanticParser.DisplayName(binding.kind));
            AddKeyValue("Id", binding.id);
            if (!string.IsNullOrWhiteSpace(binding.sourceText))
                AddKeyValue("Text", Clamp(binding.sourceText, 140));

            AddSeparator();
            AddMuted("Integration fields");
            EnsureDefaultBindingFields(binding);
            foreach (PungentRichDocumentSemanticField field in binding.fields)
            {
                if (field == null)
                    continue;

                TextField textField = new TextField(ObjectNames.NicifyVariableName(field.key))
                {
                    value = field.value ?? string.Empty
                };
                textField.style.marginBottom = 4;
                textField.RegisterValueChangedCallback(evt =>
                {
                    binding.SetField(field.key, evt.newValue ?? string.Empty);
                    _markDirty?.Invoke("Unsaved semantic binding changes");
                });
                Add(textField);
            }

            AddSeparator();
            DrawTargetBinding(binding);
        }

        private void DrawTargetBinding(PungentRichDocumentSemanticBinding binding)
        {
            AddMuted("Bound Text Field");
            if (binding.target != null && binding.target.HasTarget)
            {
                AddKeyValue("Target", binding.target.label);
                if (!string.IsNullOrWhiteSpace(binding.target.propertyPath))
                    AddKeyValue("Property Path", binding.target.propertyPath);
                if (PungentAuthoringProviderRegistry.TryResolveTarget(binding.target, out UnityEngine.Object resolved, out string error))
                    AddKeyValue("Resolved", resolved == null ? "Missing" : resolved.name);
                else if (!string.IsNullOrWhiteSpace(error))
                    AddMuted(error);

                PungentAuthoringBindingPreview sharedPreview = binding.kind == PungentRichDocumentSemanticKind.Token
                    ? PungentAuthoringBindingApplicationService.Preview(binding.target)
                    : null;
                PungentRichDocumentBindingPreview preview = binding.kind == PungentRichDocumentSemanticKind.Token
                    ? null
                    : PungentRichDocumentBindingApplicationService.Preview(binding);
                if (sharedPreview != null && sharedPreview.canRead)
                    AddKeyValue("Current Value", Clamp(sharedPreview.currentValue, 120));
                else if (preview != null && preview.canRead)
                    AddKeyValue("Current Value", Clamp(preview.currentValue, 120));
                else if (sharedPreview != null && !string.IsNullOrWhiteSpace(sharedPreview.disabledReason))
                    AddMuted(sharedPreview.disabledReason);
                else if (preview != null && !string.IsNullOrWhiteSpace(preview.disabledReason))
                    AddMuted(preview.disabledReason);
                if (PungentRichDocumentBindingApplicationService.CanApply(binding, out string applyReason))
                    AddKeyValue("Status", "Ready: writes text");
                else if (!string.IsNullOrWhiteSpace(applyReason))
                    AddKeyValue("Status", "Blocked: " + applyReason);

                AddApplyModeField(binding);

                VisualElement actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.flexWrap = Wrap.Wrap;
                Add(actions);
                actions.Add(MakeSmallButton("Ping", () => PingBoundTarget(binding.target), "Select and ping the bound Unity target."));
                actions.Add(MakeSmallButton("Copy Path", () => EditorGUIUtility.systemCopyBuffer = TargetCopyText(binding.target), "Copy the persisted target reference/path."));
                actions.Add(MakeSmallButton("Open Preview", () => _openPropagationPreview?.Invoke(), "Open the propagation preview."));
            }
            else
            {
                AddMuted("No target bound yet.");
                AddApplyModeField(binding);
            }

            IMGUIContainer guidedBinding = new IMGUIContainer(() =>
            {
                PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
                {
                    contextLabel = binding.kind == PungentRichDocumentSemanticKind.Token ? "Choose Reference Endpoint" : "Choose Text Field",
                    objectLabel = "Target Object",
                    componentLabel = "Component",
                    endpointLabel = binding.kind == PungentRichDocumentSemanticKind.Token ? "Endpoint" : "Text Field",
                    bindButtonLabel = binding.kind == PungentRichDocumentSemanticKind.Token ? "Bind Reference" : "Bind Text",
                    helpText = string.Empty,
                    showHelp = false,
                    allowedValueKinds = binding.kind == PungentRichDocumentSemanticKind.Token
                        ? new List<PungentAuthoringBindingValueKind>()
                        : new List<PungentAuthoringBindingValueKind> { PungentAuthoringBindingValueKind.Text }
                };

                PungentAuthoringGuidedBindingResult result = PungentAuthoringGuidedBindingView.Draw(_bindingPickerState, options);
                if (!result.bindClicked)
                    return;

                if (result.selectedEndpoint == null || !result.selectedEndpoint.CanBind)
                {
                    _setStatus?.Invoke("Selected field cannot be bound.");
                    return;
                }

                PungentRichDocumentSemanticBindingService.SetBindingEndpoint(_document, binding.id, result.selectedEndpoint);
                _markDirty?.Invoke("Bound semantic text to field");
                _setStatus?.Invoke("Bound text to " + result.selectedEndpoint.label + ".");
                schedule.Execute(Refresh);
            });
            guidedBinding.style.marginTop = 4;
            guidedBinding.style.marginBottom = 6;
            Add(guidedBinding);
        }

        private void AddApplyModeField(PungentRichDocumentSemanticBinding binding)
        {
            if (binding == null)
                return;

            IMGUIContainer modeField = new IMGUIContainer(() =>
            {
                EditorGUI.BeginChangeCheck();
                PungentAuthoringBindingApplyMode next = DrawApplyModePopup(binding.applyMode);
                if (EditorGUI.EndChangeCheck())
                {
                    binding.applyMode = next;
                    binding.NormalizeInPlace();
                    _markDirty?.Invoke("Changed semantic binding apply mode");
                    _setStatus?.Invoke("Apply mode set to " + ApplyModeLabel(binding.applyMode) + ".");
                }
            });
            modeField.style.marginBottom = 4;
            Add(modeField);
        }

        private static void EnsureDefaultBindingFields(PungentRichDocumentSemanticBinding binding)
        {
            if (binding == null)
                return;

            IEnumerable<PungentRichDocumentSemanticField> defaults = PungentRichDocumentSemanticParser.DefaultFieldsForKind(binding.kind);
            if (binding.kind == PungentRichDocumentSemanticKind.CustomInsertion)
            {
                PungentRichDocumentInsertionDefinition definition = PungentRichDocumentInsertionDefinitionRegistry.Find(
                    binding.GetField(PungentRichDocumentInsertionDefinitionRegistry.DefinitionFieldKey));
                if (definition != null)
                    defaults = PungentRichDocumentInsertionDefinitionRegistry.CreateDefaultFields(definition);
            }

            foreach (PungentRichDocumentSemanticField field in defaults)
            {
                if (field == null)
                    continue;
                if (string.IsNullOrWhiteSpace(binding.GetField(field.key)))
                    binding.SetField(field.key, field.value);
            }
        }

        private void AddHeader(string text)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;
            Add(row);

            Label label = new Label(text ?? string.Empty);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 14;
            label.style.flexGrow = 1f;
            row.Add(label);

            if (_closeTray != null)
            {
                Button close = new Button(_closeTray) { text = "<" };
                close.tooltip = "Collapse properties.";
                close.style.width = 24;
                close.style.height = 20;
                row.Add(close);
            }
        }

        private void AddMuted(string text)
        {
            Label label = new Label(text ?? string.Empty);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 11;
            label.style.marginBottom = 7;
            label.style.color = EditorGUIUtility.isProSkin ? new Color(0.66f, 0.68f, 0.7f) : new Color(0.38f, 0.39f, 0.4f);
            Add(label);
        }

        private void AddKeyValue(string key, string value)
        {
            Label label = new Label((key ?? string.Empty) + ": " + (value ?? string.Empty));
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 11;
            label.style.marginBottom = 5;
            Add(label);
        }

        private void AddButton(string text, Action action, Color tint, string tooltip = null)
        {
            Button button = new Button(action)
            {
                text = text ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            button.style.marginTop = 3;
            button.style.marginBottom = 3;
            button.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.82f));
            button.style.color = Color.white;
            Add(button);
        }

        private static Button MakeSmallButton(string text, Action action, string tooltip)
        {
            Button button = new Button(action)
            {
                text = text ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.height = 22;
            return button;
        }

        private void PingBoundTarget(PungentAuthoringTarget target)
        {
            if (target == null || !target.HasTarget)
                return;

            if (PungentRichDocumentBindingApplicationService.TryResolveUnityTarget(target, out UnityEngine.Object targetObject, out string error) && targetObject != null)
            {
                Selection.activeObject = targetObject;
                EditorGUIUtility.PingObject(targetObject);
                _setStatus?.Invoke("Pinged " + targetObject.name + ".");
                return;
            }

            _setStatus?.Invoke(string.IsNullOrWhiteSpace(error) ? "Target could not be resolved." : error);
        }

        private static string TargetCopyText(PungentAuthoringTarget target)
        {
            if (target == null)
                return string.Empty;

            return target.targetKind + "|" +
                   (target.customKind ?? string.Empty) + "|" +
                   (target.contextId ?? string.Empty) + "|" +
                   (target.rawValue ?? string.Empty) + "|" +
                   (target.propertyPath ?? string.Empty);
        }

        private static string ApplyModeLabel(PungentAuthoringBindingApplyMode mode)
        {
            switch (mode)
            {
                case PungentAuthoringBindingApplyMode.AutoApplyEditMode:
                    return "Auto in Edit Mode";
                case PungentAuthoringBindingApplyMode.AutoApplyAlways:
                    return "Auto in Edit + Play";
                default:
                    return "Manual Apply";
            }
        }

        private static PungentAuthoringBindingApplyMode DrawApplyModePopup(PungentAuthoringBindingApplyMode value)
        {
            PungentAuthoringBindingApplyMode[] values =
            {
                PungentAuthoringBindingApplyMode.ManualApply,
                PungentAuthoringBindingApplyMode.AutoApplyEditMode,
                PungentAuthoringBindingApplyMode.AutoApplyAlways
            };
            string[] labels = { "Manual Apply", "Auto in Edit Mode", "Auto in Edit + Play" };
            int index = Mathf.Clamp(Array.IndexOf(values, value), 0, values.Length - 1);
            return values[Mathf.Clamp(EditorGUILayout.Popup("Apply Mode", index, labels), 0, values.Length - 1)];
        }

        private PungentRichDocumentAnnotation FindAnnotation(string annotationId)
        {
            if (_document == null || _document.annotations == null || string.IsNullOrWhiteSpace(annotationId))
                return null;

            return _document.annotations.FirstOrDefault(annotation =>
                annotation != null && string.Equals(annotation.id, annotationId, StringComparison.OrdinalIgnoreCase));
        }

        private void AddSeparator()
        {
            VisualElement line = new VisualElement();
            line.style.height = 1;
            line.style.marginTop = 8;
            line.style.marginBottom = 8;
            line.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.32f, 0.33f, 0.35f) : new Color(0.7f, 0.72f, 0.74f));
            Add(line);
        }

        private static string Clamp(string text, int max)
        {
            string safe = text ?? string.Empty;
            return safe.Length <= max ? safe : safe.Substring(0, Math.Max(0, max - 3)).TrimEnd() + "...";
        }

    }
#endif
}
