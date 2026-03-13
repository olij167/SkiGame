#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace SkiGame.RunsEditor
{
    [Overlay(typeof(SceneView), "Ski Run Authoring", defaultDisplay = true)]
    public sealed class SkiRunLineSceneOverlay : Overlay
    {
        private VisualElement _root;
        private ToolbarToggle _modePath;
        private ToolbarToggle _modeFlags;
        private ToolbarToggle _modeFences;
        private ToolbarToggle _tPoints;
        private ToolbarToggle _tBoundary;
        private ToolbarToggle _tPairs;
        private ToolbarToggle _tIntersections;
        private ToolbarToggle _tGateEdit;
        private ToolbarToggle _tFenceEdit;
        private Foldout _hotkeysFoldout;
        private Label _hotkeysLabel;
        private double _nextRefreshTime;
        public override VisualElement CreatePanelContent()
        {
            _root = new VisualElement();
            _root.style.paddingLeft = 6;
            _root.style.paddingRight = 6;
            _root.style.paddingTop = 4;
            _root.style.paddingBottom = 4;
            _root.style.minWidth = 360;
            // ---- Row 1: Mode (exclusive toggles; avoids RadioButtonGroup clipping in Toolbar) ----
            var rowMode = new Toolbar();
            _root.Add(rowMode);
            var modeLabel = new Label("Mode");
            modeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            modeLabel.style.marginRight = 6;
            rowMode.Add(modeLabel);
            _modePath = new ToolbarToggle { text = "Path (1)", tooltip = "Edit run path/points. Hotkey: 1" };
            _modeFlags = new ToolbarToggle { text = "Flags (2)", tooltip = "Edit flags/gates. Hotkey: 2" };
            _modeFences = new ToolbarToggle { text = "Fences (3)", tooltip = "Edit fences/holes. Hotkey: 3" };
            _modePath.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue) return;
                if (!SkiRunLineEditor.OverlayHasActiveRun()) return;
                SkiRunLineEditor.OverlaySetMode(0);
                RequestRefresh();
            });
            _modeFlags.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue) return;
                if (!SkiRunLineEditor.OverlayHasActiveRun()) return;
                SkiRunLineEditor.OverlaySetMode(1);
                RequestRefresh();
            });
            _modeFences.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue) return;
                if (!SkiRunLineEditor.OverlayHasActiveRun()) return;
                SkiRunLineEditor.OverlaySetMode(2);
                RequestRefresh();
            });
            rowMode.Add(_modePath);
            rowMode.Add(_modeFlags);
            rowMode.Add(_modeFences);
            // ---- Row 2: Common toggles ----
            var rowCommon = new Toolbar();
            _root.Add(rowCommon);
            _tBoundary = MakeToggle("Boundary", "Show corridor boundary preview in scene", v =>
            {
                SkiRunLineEditor.OverlaySetShowBoundaryPreview(v);
                RequestRefresh();
            });
            rowCommon.Add(_tBoundary);
            _tPairs = MakeToggle("Pairs", "Show sampled gate pair preview in scene", v =>
            {
                SkiRunLineEditor.OverlaySetShowPairPreview(v);
                RequestRefresh();
            });
            rowCommon.Add(_tPairs);
            _tIntersections = MakeToggle("Intersections", "Show intersection/overlap diagnostics in scene", v =>
            {
                SkiRunLineEditor.OverlaySetShowIntersectionOverlay(v);
                RequestRefresh();
            });
            rowCommon.Add(_tIntersections);
            // ---- Row 3: Mode-specific tools ----
            var rowTools = new Toolbar();
            _root.Add(rowTools);
            _tPoints = MakeToggle("Points", "Show point gizmos and enable point editing interactions (Path mode)", v =>
            {
                SkiRunLineEditor.OverlaySetShowPointGizmos(v);
                RequestRefresh();
            });
            rowTools.Add(_tPoints);
            _tGateEdit = MakeToggle("Gate Edit (G)", "Enable gate editing handles (Flags mode). Hotkey: G", v =>
            {
                // Auto switch mode
                if (v) SkiRunLineEditor.OverlaySetMode(1);
                SkiRunLineEditor.OverlaySetGateEditMode(v);
                RequestRefresh();
            });
            rowTools.Add(_tGateEdit);
            _tFenceEdit = MakeToggle("Fence Edit", "Enable fence hole editing (Fences mode)", v =>
            {
                // Auto switch mode
                if (v) SkiRunLineEditor.OverlaySetMode(2);
                SkiRunLineEditor.OverlaySetFenceHoleEditMode(v);
                RequestRefresh();
            });
            rowTools.Add(_tFenceEdit);
            _hotkeysFoldout = new Foldout();
            _hotkeysFoldout.text = "Hotkeys";
            _hotkeysFoldout.value = false;
            _hotkeysFoldout.style.marginTop = 4;
            _root.Add(_hotkeysFoldout);
            _hotkeysLabel = new Label();
            _hotkeysLabel.style.whiteSpace = WhiteSpace.Normal;
            _hotkeysLabel.style.fontSize = 11;
            _hotkeysFoldout.Add(_hotkeysLabel);
            HookEditorEvents();
            RefreshFromEditor();
            return _root;
        }
        public override void OnWillBeDestroyed()
        {
            UnhookEditorEvents();
            base.OnWillBeDestroyed();
        }
        private ToolbarToggle MakeToggle(string label, string tooltip, System.Action<bool> onChanged)
        {
            var t = new ToolbarToggle { text = label, tooltip = tooltip };
            t.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return t;
        }
        private void HookEditorEvents()
        {
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;
        }
        private void UnhookEditorEvents()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
        }
        private void OnSelectionChanged()
        {
            RequestRefresh();
        }
        private void OnUndoRedo()
        {
            RequestRefresh();
        }
        private void OnEditorUpdate()
        {
            // Light throttle to keep overlay in sync when inspector focus changes etc.
            if (_root == null) return;
            if (EditorApplication.timeSinceStartup < _nextRefreshTime) return;
            _nextRefreshTime = EditorApplication.timeSinceStartup + 0.25;
            RefreshFromEditor();
        }
        private void RequestRefresh()
        {
            _nextRefreshTime = 0; // next update refreshes immediately
            SceneView.RepaintAll();
        }
        private void RefreshFromEditor()
        {
            bool hasRun = SkiRunLineEditor.OverlayHasActiveRun();
            _root?.SetEnabled(hasRun);
            if (!hasRun)
            {
                if (_hotkeysLabel != null)
                    _hotkeysLabel.text = "Select a SkiRunLine to view mode-specific shortcuts.";
                return;
            }
            int mode = SkiRunLineEditor.OverlayGetMode();
            // Exclusive selection
            _modePath.SetValueWithoutNotify(mode == 0);
            _modeFlags.SetValueWithoutNotify(mode == 1);
            _modeFences.SetValueWithoutNotify(mode == 2);
            _tBoundary.SetValueWithoutNotify(SkiRunLineEditor.OverlayGetShowBoundaryPreview());
            _tPairs.SetValueWithoutNotify(SkiRunLineEditor.OverlayGetShowPairPreview());
            _tIntersections.SetValueWithoutNotify(SkiRunLineEditor.OverlayGetShowIntersectionOverlay());
            _tPoints.SetValueWithoutNotify(SkiRunLineEditor.OverlayGetShowPointGizmos());
            _tGateEdit.SetValueWithoutNotify(SkiRunLineEditor.OverlayGetGateEditMode());
            _tFenceEdit.SetValueWithoutNotify(SkiRunLineEditor.OverlayGetFenceHoleEditMode());
            // Mode gating:
            // - Points toggle only relevant in Path mode
            // - Gate Edit only relevant in Flags mode
            // - Fence Edit only relevant in Fences mode
            _tPoints.style.display = (mode == 0) ? DisplayStyle.Flex : DisplayStyle.None;
            _tGateEdit.style.display = (mode == 1) ? DisplayStyle.Flex : DisplayStyle.None;
            _tFenceEdit.style.display = (mode == 2) ? DisplayStyle.Flex : DisplayStyle.None;
            if (_hotkeysLabel != null)
                _hotkeysLabel.text = BuildHotkeySummary(mode);
        }
        private static string BuildHotkeySummary(int mode)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                "Common",
                "• 1 / 2 / 3 — Switch Path / Flags / Fences",
                "• Tab — Cycle mode",
                "• H — Toggle the in-scene hotkey panel",
                "• B — Toggle boundary preview",
                "• P — Toggle gate pair preview",
                "• I — Toggle intersection overlay",
                "• O — Toggle overlap avoidance preview",
                "• Esc — Clear selection",
                "• Ctrl+Shift+R — Rebuild flags",
                "• Ctrl+Shift+M — Bake metrics"
            };
            switch (mode)
            {
                case 0:
                    lines.Add("");
                    lines.Add("Path");
                    lines.Add("• Use scene point handles to shape the run path");
                    lines.Add("• Open Painter when you want terrain paint workflow for the active run");
                    break;
                case 1:
                    lines.Add("");
                    lines.Add("Flags");
                    lines.Add("• G — Toggle gate edit mode");
                    lines.Add("• Shift+Click — Insert gate at nearest sampled pair");
                    lines.Add("• Alt+Click — Remove inserted gate or toggle enable state");
                    lines.Add("• Right-click — Open selected gate context menu");
                    break;
                case 2:
                    lines.Add("");
                    lines.Add("Fences");
                    lines.Add("• Shift+Click — Add a fence gap on the chosen side");
                    lines.Add("• Click — Select a gap");
                    lines.Add("• Drag — Move selected gap endpoints");
                    lines.Add("• Delete / Backspace — Remove selected gap");
                    break;
            }
            return string.Join("\n", lines);
        }
    }
}
#endif