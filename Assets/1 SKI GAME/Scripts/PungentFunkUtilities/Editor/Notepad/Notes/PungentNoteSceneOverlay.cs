using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class PungentNoteSceneOverlay
    {
        private static readonly List<PungentNote> CachedNotes = new List<PungentNote>();
        private static bool _dirty = true;
        private static bool _invalidateQueued;

        static PungentNoteSceneOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged -= InvalidateCache;
            Selection.selectionChanged += InvalidateCache;
            EditorApplication.hierarchyChanged -= InvalidateCache;
            EditorApplication.hierarchyChanged += InvalidateCache;
        }

        public static void InvalidateCache()
        {
            _dirty = true;
            _invalidateQueued = false;
        }

        public static void QueueInvalidateCache()
        {
            if (_invalidateQueued)
                return;

            _invalidateQueued = true;
            EditorApplication.delayCall += FlushQueuedInvalidation;
        }

        private static void FlushQueuedInvalidation()
        {
            _invalidateQueued = false;
            _dirty = true;
        }

        private static void RebuildCache()
        {
            CachedNotes.Clear();
            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            if (!settings.showSceneBadges || settings.surfaceMode == PungentNoteSurfaceMode.Hidden)
            {
                _dirty = false;
                return;
            }

            foreach (PungentNote note in PungentNoteStorage.Database.notes)
            {
                if (note == null || note.targets == null)
                    continue;
                if (!note.targets.Any(t => t != null && (t.type == PungentNoteTargetType.SceneObject || t.type == PungentNoteTargetType.ComponentInstance || t.type == PungentNoteTargetType.SerializedProperty)))
                    continue;
                if (!PungentNoteDisplaySettingsService.ShouldShowSurfaceNote(note, IsSelected(note)))
                    continue;
                if (PungentNoteContextResolver.ResolveGameObject(note) != null)
                    CachedNotes.Add(note);
            }

            _dirty = false;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (sceneView == null)
                return;
            if (_dirty)
                RebuildCache();

            List<IGrouping<GameObject, PungentNote>> groups = CachedNotes
                .GroupBy(PungentNoteContextResolver.ResolveGameObject)
                .Where(g => g.Key != null)
                .ToList();

            foreach (IGrouping<GameObject, PungentNote> group in groups)
            {
                if (!ShouldDrawGroup(group.Key, group.ToList()))
                    continue;

                PungentNote first = group.OrderBy(n => n.priority).First();
                Vector3 position = group.Key.transform.position + Vector3.up * 1.25f;
                float size = HandleUtility.GetHandleSize(position);
                Handles.color = PungentNoteGUI.PriorityTint(first.priority);
                Handles.DrawSolidDisc(position, sceneView.camera != null ? sceneView.camera.transform.forward : Vector3.forward, size * 0.045f);
            }

            Handles.BeginGUI();
            foreach (IGrouping<GameObject, PungentNote> group in groups)
            {
                List<PungentNote> notes = group.ToList();
                if (!ShouldDrawGroup(group.Key, notes))
                    continue;

                PungentNote first = notes.OrderBy(n => n.priority).First();
                Vector3 world = group.Key.transform.position + Vector3.up * 1.25f;
                Vector2 guiPoint = HandleUtility.WorldToGUIPoint(world);
                string label = (notes.Count > 1 ? notes.Count + " notes: " : string.Empty) + first.title;
                Rect rect = new Rect(guiPoint.x + 10f, guiPoint.y - 10f, Mathf.Clamp(72f + label.Length * 6f, 120f, 260f), 22f);
                Color tint = PungentNoteGUI.PriorityTint(first.priority);
                EditorGUI.DrawRect(rect, new Color(tint.r, tint.g, tint.b, 0.16f));
                if (GUI.Button(rect, label, EditorStyles.miniButton))
                {
                    if (notes.Count == 1)
                        PungentStickyNoteOverlayController.OpenEdit(first, rect, PungentStickyNoteOverlayOwner.SceneView, "Scene Notes");
                    else
                        PungentStickyNoteOverlayController.OpenStack(notes, rect, PungentStickyNoteOverlayOwner.SceneView, group.Key.name);
                }

                PungentStickyNoteOverlayController.RequestHoverPreview(
                    first,
                    rect,
                    PungentStickyNoteOverlayOwner.SceneView,
                    "Scene Notes",
                    PungentNoteDisplaySettingsService.Settings.showBrowserHoverPreviews,
                    false,
                    PungentNoteDisplaySettingsService.Settings.hoverPreviewDelaySeconds <= 0f ? 0.35f : PungentNoteDisplaySettingsService.Settings.hoverPreviewDelaySeconds);
            }

            PungentStickyNoteOverlayController.Draw(
                PungentStickyNoteOverlayOwner.SceneView,
                new Rect(0f, 0f, sceneView.position.width, sceneView.position.height));
            Handles.EndGUI();
        }

        private static bool ShouldDrawGroup(GameObject go, List<PungentNote> notes)
        {
            PungentNoteDisplaySettings settings = PungentNoteDisplaySettingsService.Settings;
            if (settings.surfaceMode == PungentNoteSurfaceMode.SelectedContextOnly && !Selection.gameObjects.Contains(go))
                return false;
            if (settings.surfaceMode == PungentNoteSurfaceMode.CriticalOnly && !notes.Any(n => n.priority == PungentNotePriority.Crucial || n.status == PungentNoteStatus.Blocked))
                return false;
            return true;
        }

        private static bool IsSelected(PungentNote note)
        {
            GameObject go = PungentNoteContextResolver.ResolveGameObject(note);
            return go != null && Selection.gameObjects.Contains(go);
        }
    }
#endif
}
