using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal sealed class PungentPlacementSurfaceBrushController
    {
        private const string PrefPrefix = "PungentFunkUtilities.AssetPlacementLab.SurfaceBrush.";

        private bool _active;
        private bool _eraseMode;
        private float _radius = 3f;
        private int _density = 8;
        private int _previewCap = 80;
        private RaycastHit _lastHit;
        private readonly List<PungentPlacementCandidate> _preview = new List<PungentPlacementCandidate>();

        public bool IsActive => _active;
        public string Summary { get; private set; } = "Brush idle.";

        public void LoadPrefs()
        {
            _radius = UtilityWindowPrefs.GetFloat(PrefPrefix + "Radius", _radius);
            _density = UtilityWindowPrefs.GetInt(PrefPrefix + "Density", _density);
            _previewCap = UtilityWindowPrefs.GetInt(PrefPrefix + "PreviewCap", _previewCap);
            _eraseMode = UtilityWindowPrefs.GetBool(PrefPrefix + "EraseMode", _eraseMode);
        }

        public void SavePrefs()
        {
            UtilityWindowPrefs.SetFloat(PrefPrefix + "Radius", _radius);
            UtilityWindowPrefs.SetInt(PrefPrefix + "Density", _density);
            UtilityWindowPrefs.SetInt(PrefPrefix + "PreviewCap", _previewCap);
            UtilityWindowPrefs.SetBool(PrefPrefix + "EraseMode", _eraseMode);
        }

        public bool DrawModuleUI(ref PungentPlacementAssetSetSO assetSet, ref PungentPlacementRuleSetSO ruleSet, ref Transform outputParent)
        {
            bool subscriptionChanged = false;
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                UtilityWindowTheme.SectionTitle("Surface Brush", UtilityWindowTheme.Blue, _active ? "Active" : "Idle");
                EditorGUILayout.LabelField("Paint or erase weighted asset-set entries on raycast surfaces with rule-set height, slope, layer, and random transform filtering.", UtilityWindowTheme.MutedMiniLabelStyle);
            }

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                assetSet = (PungentPlacementAssetSetSO)EditorGUILayout.ObjectField("Asset Set", assetSet, typeof(PungentPlacementAssetSetSO), false);
                ruleSet = (PungentPlacementRuleSetSO)EditorGUILayout.ObjectField("Rule Set", ruleSet, typeof(PungentPlacementRuleSetSO), false);
                outputParent = (Transform)EditorGUILayout.ObjectField("Output Parent", outputParent, typeof(Transform), true);
                _radius = EditorGUILayout.Slider("Brush Radius", _radius, 0.25f, 25f);
                _density = EditorGUILayout.IntSlider("Brush Density", _density, 1, 100);
                _previewCap = EditorGUILayout.IntSlider("Preview Cap", _previewCap, 8, 250);
                _eraseMode = EditorGUILayout.ToggleLeft("Erase Mode", _eraseMode);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool active = GUILayout.Toggle(_active, _active ? "Brush Active" : "Brush Idle", EditorStyles.miniButton, GUILayout.Height(26f));
                    if (active != _active)
                    {
                        _active = active;
                        subscriptionChanged = true;
                    }

                    using (new EditorGUI.DisabledScope(!_active || _eraseMode || _preview.Count == 0))
                    {
                        if (UtilityWindowTheme.TintedButton("Apply Preview", UtilityWindowTheme.Green, GUILayout.Height(26f)))
                            ApplyPreview(assetSet, ruleSet, outputParent, 0);
                    }
                }

                EditorGUILayout.HelpBox(_eraseMode ? "Scene click erases marked placement objects inside the brush radius." : "Move in Scene view to preview. Click to apply accepted preview positions.", MessageType.Info);
                EditorGUILayout.LabelField(Summary, UtilityWindowTheme.MutedMiniLabelStyle);
            }

            return subscriptionChanged;
        }

        public bool HandleSceneGUI(SceneView sceneView, PungentPlacementAssetSetSO assetSet, PungentPlacementRuleSetSO ruleSet, Transform outputParent, int seed)
        {
            if (!_active)
                return false;

            Event current = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            LayerMask mask = ruleSet != null ? ruleSet.surfaceMask : ~0;
            bool hasHit = Physics.Raycast(ray, out _lastHit, 10000f, mask);
            if (hasHit)
            {
                Handles.color = _eraseMode ? new Color(1f, 0.35f, 0.2f, 0.85f) : new Color(0.25f, 0.9f, 1f, 0.85f);
                Handles.DrawWireDisc(_lastHit.point, _lastHit.normal, _radius);
                RebuildPreview(_lastHit, assetSet, ruleSet, seed);

                if (!_eraseMode)
                    DrawPreviewHandles();
            }

            if (hasHit && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0 && !current.alt)
            {
                if (_eraseMode)
                    EraseAtHit();
                else
                    ApplyPreview(assetSet, ruleSet, outputParent, seed);
                current.Use();
                return true;
            }

            return hasHit;
        }

        private void DrawPreviewHandles()
        {
            for (int i = 0; i < _preview.Count; i++)
            {
                PungentPlacementCandidate candidate = _preview[i];
                if (candidate == null)
                    continue;
                Handles.SphereHandleCap(0, candidate.position, Quaternion.identity, HandleUtility.GetHandleSize(candidate.position) * 0.06f, EventType.Repaint);
            }
        }

        private void RebuildPreview(RaycastHit hit, PungentPlacementAssetSetSO assetSet, PungentPlacementRuleSetSO ruleSet, int seed)
        {
            _preview.Clear();
            if (_eraseMode || assetSet == null || !assetSet.HasUsableEntries)
                return;

            System.Random random = new System.Random(seed + Mathf.RoundToInt(hit.point.x * 31f) + Mathf.RoundToInt(hit.point.z * 17f));
            int target = Mathf.Min(_density, _previewCap);
            int attempts = Mathf.Max(target * 4, target);
            for (int i = 0; i < attempts && _preview.Count < target; i++)
            {
                Vector2 circle = RandomPointInCircle(random) * _radius;
                Vector3 tangent = Vector3.Cross(hit.normal, Vector3.forward);
                if (tangent.sqrMagnitude < 0.001f)
                    tangent = Vector3.Cross(hit.normal, Vector3.right);
                tangent.Normalize();
                Vector3 bitangent = Vector3.Cross(hit.normal, tangent).normalized;
                Vector3 sample = hit.point + tangent * circle.x + bitangent * circle.y + hit.normal * 0.2f;
                LayerMask mask = ruleSet != null ? ruleSet.surfaceMask : ~0;
                float distance = ruleSet != null ? ruleSet.raycastDistance : 50f;
                if (!Physics.Raycast(sample + hit.normal * 2f, -hit.normal, out RaycastHit surface, distance, mask))
                    continue;
                if (ruleSet != null && (!ruleSet.HeightAllowed(surface.point.y) || !ruleSet.SlopeAllowed(surface.normal)))
                    continue;

                PungentPlacementAssetEntry entry = assetSet.Pick(random);
                if (entry == null || entry.prefab == null)
                    continue;

                _preview.Add(new PungentPlacementCandidate
                {
                    prefab = entry.prefab,
                    assetEntry = entry,
                    position = surface.point + surface.normal * (ruleSet != null ? ruleSet.surfaceOffset : 0f) + entry.positionOffset,
                    rotation = entry.GetRandomRotation(random, surface.normal),
                    scale = entry.GetRandomScale(random),
                    surfaceNormal = surface.normal,
                    state = PungentPlacementCandidateState.Accepted,
                    index = _preview.Count
                });
            }

            Summary = $"Previewing {_preview.Count} brush candidate(s).";
        }

        private void ApplyPreview(PungentPlacementAssetSetSO assetSet, PungentPlacementRuleSetSO ruleSet, Transform outputParent, int seed)
        {
            if (_preview.Count == 0)
                return;

            PungentPlacementResult result = new PungentPlacementResult();
            result.candidates.AddRange(_preview);
            result.Recount();
            PungentPlacementContext context = new PungentPlacementContext
            {
                seed = seed,
                requestedCount = _preview.Count,
                scatterPattern = PungentPlacementScatterPattern.RandomMinSpacing,
                assetSet = assetSet,
                ruleSet = ruleSet,
                areaBounds = new Bounds(_lastHit.point, Vector3.one * _radius * 2f),
                outputParent = outputParent,
                previewRejected = false,
                heatmap = ruleSet != null ? ruleSet.heatmap : null,
                heatmapWorldRect = ruleSet != null ? ruleSet.heatmapWorldRect : new Rect(_lastHit.point.x - _radius, _lastHit.point.z - _radius, _radius * 2f, _radius * 2f),
                useHeatmap = ruleSet != null && ruleSet.useHeatmap
            };

            GameObject group = PungentPlacementApplyUtility.ApplyResult(result, context, $"Brush_{seed}");
            Summary = group != null ? $"Brush applied {result.acceptedCount} object(s)." : "Brush apply skipped.";
            if (group != null)
                Selection.activeGameObject = group;
        }

        private void EraseAtHit()
        {
            PungentPlacedAssetMarker[] markers = Object.FindObjectsOfType<PungentPlacedAssetMarker>();
            int removed = 0;
            for (int i = markers.Length - 1; i >= 0; i--)
            {
                PungentPlacedAssetMarker marker = markers[i];
                if (marker == null || marker.locked)
                    continue;
                if (Vector3.Distance(marker.transform.position, _lastHit.point) > _radius)
                    continue;
                Undo.DestroyObjectImmediate(marker.gameObject);
                removed++;
            }

            Summary = $"Brush erased {removed} marked object(s).";
        }

        private static Vector2 RandomPointInCircle(System.Random random)
        {
            float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
            float radius = Mathf.Sqrt((float)random.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
#endif
}
