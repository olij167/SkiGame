using UnityEngine;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;
    #endif

    /// <summary>
    /// Optional marker component. The editor window no longer requires this component,
    /// but it is kept for backwards compatibility with existing scenes/prefabs.
    /// </summary>
    public class TerrainAlignTool : MonoBehaviour
    {
    }

    #if UNITY_EDITOR
    public class SurfaceAlignToolWindow : EditorWindow
    {
        private const string PrefPrefix = "GenericUtilities.SurfaceAlignTool.";

        private bool _autoAlign = true;
        private bool _alignRotationToNormal = true;
        private bool _preserveYaw = true;
        private bool _alignRendererBoundsBottom = false;
        private bool _ignoreSelectedColliders = true;
        private bool _showScenePreview = true;
        private float _heightOffset = 0f;
        private LayerMask _raycastMask = ~0;
        private float _rayStartHeight = 1000f;
        private float _rayDistance = 5000f;
        private string _status = "Ready";
        private Vector2 _bodyScroll;
        private Vector2 _alignmentScroll;
        private Vector2 _raycastScroll;
        private float _alignmentPanelHeight = 178f;
        private float _raycastPanelHeight = 184f;

        public static void ShowWindow()
        {
            SurfaceAlignToolWindow window = GetWindow<SurfaceAlignToolWindow>("Surface Align");
            window.minSize = new Vector2(440f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPrefs();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            SavePrefs();
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= Repaint;
        }

        private void OnGUI()
        {
            UtilityWindowTheme.EnsureStyles();
            UtilityWindowTheme.Header(
                "Surface Align Tool",
                "Align selected scene objects to colliders using a downward raycast. Useful for terrain, props, set dressing, roads, paths, and modular environment placement.",
                _status);

            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);

            DrawAlignmentSection();
            UtilityWindowTheme.VerticalResizeHandle(
                ref _alignmentPanelHeight,
                112f,
                Mathf.Max(112f, position.height - 220f),
                SavePrefs,
                "Drag to resize the alignment section.");

            DrawRaycastSection();
            UtilityWindowTheme.VerticalResizeHandle(
                ref _raycastPanelHeight,
                118f,
                Mathf.Max(118f, position.height - 220f),
                SavePrefs,
                "Drag to resize the raycast section.");

            DrawActionSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAlignmentSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue, 0.20f, 0.10f), GUILayout.Height(_alignmentPanelHeight)))
            {
                UtilityWindowTheme.SectionTitle("Alignment", UtilityWindowTheme.Blue, $"Selected: {Selection.transforms?.Length ?? 0}");

                _alignmentScroll = EditorGUILayout.BeginScrollView(_alignmentScroll);

                EditorGUI.BeginChangeCheck();
                _autoAlign = EditorGUILayout.ToggleLeft(new GUIContent("Auto align while moving selection", "When enabled, selected transforms are re-aligned after scene-handle drags and relevant scene-view input."), _autoAlign);
                _alignRendererBoundsBottom = EditorGUILayout.ToggleLeft(new GUIContent("Use renderer bounds bottom instead of pivot", "Offsets the transform so the combined renderer bounds bottom sits on the hit surface. Disable this for pivot-based placement."), _alignRendererBoundsBottom);
                _alignRotationToNormal = EditorGUILayout.ToggleLeft(new GUIContent("Align rotation to surface normal", "Rotates the object so its up axis follows the hit surface normal."), _alignRotationToNormal);

                using (new EditorGUI.DisabledScope(!_alignRotationToNormal))
                {
                    _preserveYaw = EditorGUILayout.ToggleLeft(new GUIContent("Preserve yaw/forward direction", "Projects the object's current forward vector onto the hit plane before aligning up to the normal."), _preserveYaw);
                }

                _heightOffset = EditorGUILayout.FloatField(new GUIContent("Surface Offset", "Distance to offset the object away from the hit surface along the normal."), _heightOffset);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRaycastSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal, 0.18f, 0.09f), GUILayout.Height(_raycastPanelHeight)))
            {
                UtilityWindowTheme.SectionTitle("Raycast", UtilityWindowTheme.Teal);

                _raycastScroll = EditorGUILayout.BeginScrollView(_raycastScroll);

                EditorGUI.BeginChangeCheck();
                _raycastMask = LayerMaskField(new GUIContent("Surface Mask", "Colliders on these layers can receive alignment raycasts."), _raycastMask);
                _rayStartHeight = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Ray Start Height", "How far above each object the downward ray starts."), _rayStartHeight));
                _rayDistance = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Ray Distance", "Maximum downward distance for the surface raycast."), _rayDistance));
                _ignoreSelectedColliders = EditorGUILayout.ToggleLeft(new GUIContent("Ignore selected objects' own colliders", "Avoids snapping an object to its own collider when it has colliders in the raycast mask."), _ignoreSelectedColliders);
                _showScenePreview = EditorGUILayout.ToggleLeft(new GUIContent("Draw scene preview rays", "Shows a lightweight ray and hit normal preview for selected objects."), _showScenePreview);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawActionSection()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.16f, 0.08f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(Selection.transforms == null || Selection.transforms.Length == 0))
                    {
                        if (UtilityWindowTheme.TintedButton("Align Selected Now", UtilityWindowTheme.Amber, GUILayout.Height(30f)))
                            AlignSelectionNow();
                    }

                    if (UtilityWindowTheme.TintedButton("Reset Defaults", UtilityWindowTheme.Neutral, GUILayout.Width(112f), GUILayout.Height(30f)))
                        ResetDefaults();
                }

                EditorGUILayout.LabelField(
                    "Tip: set the Surface Mask to the layers you want placement tools to respect. This is not terrain-specific; any collider in the mask can act as the target surface.",
                    UtilityWindowTheme.MutedMiniLabelStyle);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            Transform[] selected = Selection.transforms;
            if (selected == null || selected.Length == 0)
                return;

            if (_showScenePreview)
                DrawScenePreview(selected);

            if (!_autoAlign)
                return;

            Event e = Event.current;
            if (e == null)
                return;

            bool shouldUpdate = e.type == EventType.MouseDrag || e.type == EventType.MouseUp || e.type == EventType.KeyUp;
            if (!shouldUpdate)
                return;

            bool changedAny = AlignTransforms(selected, "Auto Align To Surface", recordUndo: true);
            if (changedAny)
            {
                MarkTouchedScenesDirty(selected);
                sceneView.Repaint();
                _status = $"Aligned {selected.Length} selected object(s).";
            }
        }

        private void DrawScenePreview(Transform[] selected)
        {
            Color previous = Handles.color;
            Handles.color = new Color(0.30f, 0.78f, 0.95f, 0.65f);

            foreach (Transform transform in selected)
            {
                if (transform == null)
                    continue;

                Vector3 origin = GetRayOrigin(transform);
                Vector3 end = origin + Vector3.down * Mathf.Min(_rayDistance, Mathf.Max(1f, _rayStartHeight * 1.5f));
                Handles.DrawLine(origin, end);

                if (TryRaycast(transform, out RaycastHit hit))
                {
                    Handles.DrawWireDisc(hit.point, hit.normal, HandleUtility.GetHandleSize(hit.point) * 0.12f);
                    Handles.DrawLine(hit.point, hit.point + hit.normal * HandleUtility.GetHandleSize(hit.point) * 0.35f);
                }
            }

            Handles.color = previous;
        }

        private void AlignSelectionNow()
        {
            Transform[] selected = Selection.transforms;
            if (selected == null || selected.Length == 0)
                return;

            bool changedAny = AlignTransforms(selected, "Align To Surface", recordUndo: true);
            if (changedAny)
            {
                MarkTouchedScenesDirty(selected);
                _status = $"Aligned {selected.Length} selected object(s).";
                SceneView.lastActiveSceneView?.Repaint();
            }
            else
            {
                _status = "No valid target surface found for the selected object(s).";
            }
        }

        private bool AlignTransforms(Transform[] transforms, string undoLabel, bool recordUndo)
        {
            bool changedAny = false;

            foreach (Transform transform in transforms)
            {
                if (transform == null)
                    continue;

                if (TryAlignTransform(transform, undoLabel, recordUndo))
                    changedAny = true;
            }

            return changedAny;
        }

        private bool TryAlignTransform(Transform transform, string undoLabel, bool recordUndo)
        {
            if (!TryRaycast(transform, out RaycastHit hit))
                return false;

            Vector3 targetPosition = CalculateAlignedPosition(transform, hit);
            Quaternion targetRotation = transform.rotation;

            if (_alignRotationToNormal)
                targetRotation = CalculateAlignedRotation(transform, hit.normal);

            if (Approximately(transform.position, targetPosition) && Quaternion.Angle(transform.rotation, targetRotation) < 0.01f)
                return false;

            if (recordUndo)
                Undo.RecordObject(transform, undoLabel);

            transform.position = targetPosition;
            transform.rotation = targetRotation;
            EditorUtility.SetDirty(transform);
            return true;
        }

        private bool TryRaycast(Transform transform, out RaycastHit bestHit)
        {
            Vector3 origin = GetRayOrigin(transform);
            Ray ray = new Ray(origin, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, _rayDistance, _raycastMask, QueryTriggerInteraction.Ignore);

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (_ignoreSelectedColliders && IsColliderOwnedBySelection(hit.collider.transform))
                    continue;

                bestHit = hit;
                return true;
            }

            bestHit = default;
            return false;
        }

        private Vector3 GetRayOrigin(Transform transform)
        {
            return transform.position + Vector3.up * _rayStartHeight;
        }

        private bool IsColliderOwnedBySelection(Transform colliderTransform)
        {
            if (colliderTransform == null)
                return false;

            Transform[] selected = Selection.transforms;
            if (selected == null)
                return false;

            foreach (Transform selectedTransform in selected)
            {
                if (selectedTransform == null)
                    continue;

                if (colliderTransform == selectedTransform || colliderTransform.IsChildOf(selectedTransform))
                    return true;
            }

            return false;
        }

        private Vector3 CalculateAlignedPosition(Transform transform, RaycastHit hit)
        {
            Vector3 targetPosition = hit.point + hit.normal * _heightOffset;

            if (!_alignRendererBoundsBottom)
                return targetPosition;

            if (!TryGetCombinedRendererBounds(transform, out Bounds bounds))
                return targetPosition;

            float currentBottom = bounds.min.y;
            float desiredBottom = targetPosition.y;
            float deltaY = desiredBottom - currentBottom;
            return transform.position + Vector3.up * deltaY;
        }

        private Quaternion CalculateAlignedRotation(Transform transform, Vector3 normal)
        {
            if (_preserveYaw)
            {
                Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, normal);
                if (projectedForward.sqrMagnitude < 0.0001f)
                    projectedForward = Vector3.ProjectOnPlane(Vector3.forward, normal);

                if (projectedForward.sqrMagnitude < 0.0001f)
                    projectedForward = Vector3.ProjectOnPlane(Vector3.right, normal);

                return Quaternion.LookRotation(projectedForward.normalized, normal);
            }

            return Quaternion.FromToRotation(transform.up, normal) * transform.rotation;
        }

        private static bool TryGetCombinedRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static void MarkTouchedScenesDirty(Transform[] transforms)
        {
            HashSet<Scene> scenes = new HashSet<Scene>();
            foreach (Transform transform in transforms)
            {
                if (transform == null)
                    continue;

                Scene scene = transform.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded && scenes.Add(scene))
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < 0.0000001f;
        }

        private LayerMask LayerMaskField(GUIContent label, LayerMask selected)
        {
            var layers = UnityEditorInternal.InternalEditorUtility.layers;
            int[] layerNumbers = new int[layers.Length];

            for (int i = 0; i < layers.Length; i++)
                layerNumbers[i] = LayerMask.NameToLayer(layers[i]);

            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Length; i++)
            {
                if (((1 << layerNumbers[i]) & selected.value) > 0)
                    maskWithoutEmpty |= 1 << i;
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);

            int mask = 0;
            for (int i = 0; i < layerNumbers.Length; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) > 0)
                    mask |= 1 << layerNumbers[i];
            }

            selected.value = mask;
            return selected;
        }

        private void LoadPrefs()
        {
            _autoAlign = UtilityWindowPrefs.GetBool(PrefPrefix + "AutoAlign", _autoAlign);
            _alignRotationToNormal = UtilityWindowPrefs.GetBool(PrefPrefix + "AlignRotation", _alignRotationToNormal);
            _preserveYaw = UtilityWindowPrefs.GetBool(PrefPrefix + "PreserveYaw", _preserveYaw);
            _alignRendererBoundsBottom = UtilityWindowPrefs.GetBool(PrefPrefix + "RendererBoundsBottom", _alignRendererBoundsBottom);
            _ignoreSelectedColliders = UtilityWindowPrefs.GetBool(PrefPrefix + "IgnoreSelectedColliders", _ignoreSelectedColliders);
            _showScenePreview = UtilityWindowPrefs.GetBool(PrefPrefix + "ShowScenePreview", _showScenePreview);
            _heightOffset = UtilityWindowPrefs.GetFloat(PrefPrefix + "HeightOffset", _heightOffset);
            _raycastMask = UtilityWindowPrefs.GetInt(PrefPrefix + "RaycastMask", _raycastMask.value);
            _rayStartHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "RayStartHeight", _rayStartHeight);
            _rayDistance = UtilityWindowPrefs.GetFloat(PrefPrefix + "RayDistance", _rayDistance);
            _alignmentPanelHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "AlignmentPanelHeight", _alignmentPanelHeight);
            _raycastPanelHeight = UtilityWindowPrefs.GetFloat(PrefPrefix + "RaycastPanelHeight", _raycastPanelHeight);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetBool(PrefPrefix + "AutoAlign", _autoAlign);
            UtilityWindowPrefs.SetBool(PrefPrefix + "AlignRotation", _alignRotationToNormal);
            UtilityWindowPrefs.SetBool(PrefPrefix + "PreserveYaw", _preserveYaw);
            UtilityWindowPrefs.SetBool(PrefPrefix + "RendererBoundsBottom", _alignRendererBoundsBottom);
            UtilityWindowPrefs.SetBool(PrefPrefix + "IgnoreSelectedColliders", _ignoreSelectedColliders);
            UtilityWindowPrefs.SetBool(PrefPrefix + "ShowScenePreview", _showScenePreview);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "HeightOffset", _heightOffset);
            UtilityWindowPrefs.SetInt(PrefPrefix + "RaycastMask", _raycastMask.value);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "RayStartHeight", _rayStartHeight);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "RayDistance", _rayDistance);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "AlignmentPanelHeight", _alignmentPanelHeight);
            UtilityWindowPrefs.SetFloat(PrefPrefix + "RaycastPanelHeight", _raycastPanelHeight);
        }

        private void ResetDefaults()
        {
            _autoAlign = true;
            _alignRotationToNormal = true;
            _preserveYaw = true;
            _alignRendererBoundsBottom = false;
            _ignoreSelectedColliders = true;
            _showScenePreview = true;
            _heightOffset = 0f;
            _raycastMask = ~0;
            _rayStartHeight = 1000f;
            _rayDistance = 5000f;
            _status = "Defaults restored.";
            SavePrefs();
            Repaint();
        }
    }

    /// <summary>
    /// Legacy wrapper kept so any saved editor layout referencing the old window class does not hard-fail.
    /// New usage should open SurfaceAlignToolWindow from Tools/PungentFunk/Scene/Surface Align Tool.
    /// </summary>
    public class TerrainAlignToolWindow : SurfaceAlignToolWindow
    {
    }
    #endif

}
