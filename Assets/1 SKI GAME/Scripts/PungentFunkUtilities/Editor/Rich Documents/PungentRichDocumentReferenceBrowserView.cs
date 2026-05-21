using System;
using System.Collections.Generic;
using System.Linq;
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
    public static class PungentRichDocumentReferenceCandidateService
    {
        public static List<UnityEngine.Object> GetCandidateObjects(PungentRichDocument document)
        {
            List<UnityEngine.Object> candidates = new List<UnityEngine.Object>();

            foreach (UnityEngine.Object selected in Selection.objects ?? new UnityEngine.Object[0])
                AddUnique(candidates, selected);

            AddUnique(candidates, Selection.activeObject);

            foreach (PungentAuthoringTarget target in document?.targets ?? new List<PungentAuthoringTarget>())
                AddResolvedTarget(candidates, target);

            foreach (PungentRichDocumentSemanticBinding binding in document?.semanticBindings ?? new List<PungentRichDocumentSemanticBinding>())
            {
                AddResolvedTarget(candidates, binding == null ? null : binding.target);
                AddResolvedTarget(candidates, binding == null ? null : binding.bindingPath?.rootTarget);
            }

            return candidates;
        }

        public static UnityEngine.Object FindBestCandidate(PungentRichDocument document, PungentRichDocumentBindingExpression expression)
        {
            if (expression == null || string.IsNullOrWhiteSpace(expression.targetName))
                return GetCandidateObjects(document).FirstOrDefault();

            string clean = expression.targetName.Trim();
            return GetCandidateObjects(document).FirstOrDefault(candidate =>
                candidate != null &&
                (string.Equals(candidate.name, clean, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(candidate.GetType().Name, clean, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(candidate.GetType().FullName, clean, StringComparison.OrdinalIgnoreCase))) ??
                   GetCandidateObjects(document).FirstOrDefault();
        }

        public static string BuildReferenceToken(UnityEngine.Object targetObject, PungentAuthoringBindingEndpoint endpoint)
        {
            string targetName = SafeReferenceSegment(targetObject == null ? endpoint?.target?.label : targetObject.name);
            if (string.IsNullOrWhiteSpace(targetName))
                targetName = "Target";

            string componentName = string.Empty;
            string group = endpoint == null ? string.Empty : endpoint.groupLabel ?? string.Empty;
            string[] groupParts = group.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries);
            if (groupParts.Length > 1)
                componentName = SafeReferenceSegment(groupParts[groupParts.Length - 1]);

            string fieldName = endpoint == null || endpoint.target == null
                ? string.Empty
                : endpoint.target.propertyPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fieldName) && endpoint != null)
                fieldName = endpoint.label ?? endpoint.id ?? string.Empty;

            fieldName = SafeReferenceSegment(Leaf(fieldName));
            if (string.IsNullOrWhiteSpace(fieldName))
                return "{ref " + targetName + "}";

            if (string.IsNullOrWhiteSpace(componentName))
                return "{ref " + targetName + ":" + fieldName + "}";

            return "{ref " + targetName + ":" + componentName + ":" + fieldName + "}";
        }

        private static void AddResolvedTarget(List<UnityEngine.Object> candidates, PungentAuthoringTarget target)
        {
            if (target == null || !target.HasTarget)
                return;

            if (PungentAuthoringBindingApplicationService.TryResolveUnityTarget(target, out UnityEngine.Object targetObject, out string _) && targetObject != null)
                AddUnique(candidates, targetObject);
        }

        private static void AddUnique(List<UnityEngine.Object> candidates, UnityEngine.Object candidate)
        {
            if (candidate == null || candidates == null)
                return;

            if (!candidates.Any(existing => existing != null && existing.GetInstanceID() == candidate.GetInstanceID()))
                candidates.Add(candidate);
        }

        private static string Leaf(string value)
        {
            string clean = value ?? string.Empty;
            int dot = clean.LastIndexOf('.');
            if (dot >= 0 && dot < clean.Length - 1)
                clean = clean.Substring(dot + 1);
            int slash = clean.LastIndexOf('/');
            if (slash >= 0 && slash < clean.Length - 1)
                clean = clean.Substring(slash + 1);
            return clean;
        }

        private static string SafeReferenceSegment(string value)
        {
            string clean = (value ?? string.Empty).Trim();
            clean = clean.Replace("{", string.Empty).Replace("}", string.Empty).Replace(":", " ");
            while (clean.Contains("  "))
                clean = clean.Replace("  ", " ");
            return clean.Trim();
        }
    }

    public sealed class PungentRichDocumentReferenceBrowserView : VisualElement
    {
        private readonly PungentAuthoringGuidedBindingState _state = new PungentAuthoringGuidedBindingState();
        private readonly IMGUIContainer _container;
        private PungentRichDocument _document;
        private PungentRichDocumentBindingExpression _expression;
        private Action<PungentAuthoringBindingEndpoint, string> _commit;
        private Action<string> _status;
        private Action _cancel;
        private int _candidateIndex;

        public PungentRichDocumentReferenceBrowserView()
        {
            style.marginTop = 4;
            style.marginBottom = 8;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 8;
            style.paddingBottom = 8;
            style.borderLeftWidth = 3;
            style.borderTopWidth = 1;
            style.borderRightWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftColor = new StyleColor(UtilityWindowTheme.Cyan);
            style.borderTopColor = new StyleColor(new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.28f));
            style.borderRightColor = new StyleColor(new Color(0f, 0f, 0f, 0.16f));
            style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.18f));
            style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.16f, 0.17f, 0.18f) : new Color(0.94f, 0.965f, 0.98f));

            _container = new IMGUIContainer(Draw);
            Add(_container);
        }

        public void Bind(
            PungentRichDocument document,
            PungentRichDocumentBindingExpression expression,
            Action<PungentAuthoringBindingEndpoint, string> commit,
            Action<string> status,
            Action cancel)
        {
            _document = document;
            _expression = expression;
            _commit = commit;
            _status = status;
            _cancel = cancel;
            UnityEngine.Object candidate = PungentRichDocumentReferenceCandidateService.FindBestCandidate(document, expression);
            if (_state.targetObject == null)
                _state.targetObject = candidate;
        }

        private void Draw()
        {
            EditorGUILayout.LabelField("Reference Browser", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Pick an explicit object, component, and endpoint. This only resolves the reference token; it does not apply values.", MessageType.None);

            List<UnityEngine.Object> candidates = PungentRichDocumentReferenceCandidateService.GetCandidateObjects(_document);
            if (candidates.Count > 0)
            {
                if (_state.targetObject == null)
                    _state.targetObject = candidates[0];
                _candidateIndex = Mathf.Clamp(candidates.FindIndex(item => item == _state.targetObject), 0, candidates.Count - 1);
                string[] labels = candidates.Select(candidate => candidate == null ? "Missing" : candidate.name + " (" + ObjectNames.NicifyVariableName(candidate.GetType().Name) + ")").ToArray();
                int nextIndex = EditorGUILayout.Popup(new GUIContent("Candidate", "Current selection, document targets, and recent explicit bindings only."), _candidateIndex, labels);
                if (nextIndex != _candidateIndex)
                {
                    _candidateIndex = nextIndex;
                    _state.targetObject = candidates[_candidateIndex];
                    _state.selectedGroupLabel = string.Empty;
                    _state.selectedEndpointId = string.Empty;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a GameObject, component, or asset to browse reference endpoints.", MessageType.Info);
            }

            PungentAuthoringGuidedBindingOptions options = new PungentAuthoringGuidedBindingOptions
            {
                contextLabel = string.Empty,
                objectLabel = "Object",
                componentLabel = "Component",
                endpointLabel = "Endpoint",
                bindButtonLabel = "Use Reference",
                showHeader = false,
                showHelp = false,
                showPreview = true,
                showReadiness = true,
                showBindButton = true
            };

            PungentAuthoringGuidedBindingResult result = PungentAuthoringGuidedBindingView.Draw(_state, options);
            if (result.bindClicked)
            {
                if (result.selectedEndpoint == null || !result.selectedEndpoint.CanBind)
                {
                    _status?.Invoke(result.disabledReason ?? "Reference endpoint is not ready.");
                    return;
                }

                string token = PungentRichDocumentReferenceCandidateService.BuildReferenceToken(_state.targetObject, result.selectedEndpoint);
                _commit?.Invoke(result.selectedEndpoint, token);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Use Current Selection", "Use the active Unity selection as the reference root."), EditorStyles.miniButton))
                {
                    _state.targetObject = Selection.activeObject;
                    _state.selectedGroupLabel = string.Empty;
                    _state.selectedEndpointId = string.Empty;
                }

                if (GUILayout.Button(new GUIContent("Cancel", "Close this reference browser."), EditorStyles.miniButton, GUILayout.Width(72f)))
                    _cancel?.Invoke();
            }
        }
    }
#endif
}
