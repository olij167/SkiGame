namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Safe style explorer for discovering built-in GUIStyle names.
    /// It can apply reversible session-only GUIStyle text/font overrides alongside the USS bridge; it does not use IMGUI interception.
    /// </summary>
    public sealed class PungentEditorStyleExplorerWindow : EditorWindow
    {
        private const string RiskTitle = "Enable Experimental Editor Styling?";
        private const string RiskMessage =
            "The Editor Style Explorer and experimental editor skin bridge can change Unity editor fonts, icons, GUIStyle text, and USS styling for the current project/editor session.\n\n" +
            "This feature is not fully stable yet. It can make current editor styles look broken, and recovery may require Disable + Restore, a repaint/domain refresh, or restarting the Unity Editor.\n\n" +
            "Continue with this experimental style change?";

        private static bool _riskAcceptedThisSession;

        private Vector2 _scroll;
        private Vector2 _mappingScroll;
        private string _search = string.Empty;
        private GUIStyle _selected;
        private UtilityWindowTheme.TextRole _selectedTextRole = UtilityWindowTheme.TextRole.Body;
        private int _selectedColorRoleIndex = 12;
        private bool _includeFont = true;
        private bool _includeBackground;
        private readonly List<GUIStyle> _styles = new List<GUIStyle>();

        public static void ShowWindow()
        {
            PungentEditorStyleExplorerWindow window = GetWindow<PungentEditorStyleExplorerWindow>("Editor Style Explorer");
            window.minSize = new Vector2(680f, 480f);
            window.Refresh();
            window.Show();
        }

        internal static bool ConfirmExperimentalStyleChange(string actionLabel)
        {
            if (_riskAcceptedThisSession)
                return true;

            string message = string.IsNullOrWhiteSpace(actionLabel)
                ? RiskMessage
                : actionLabel + "\n\n" + RiskMessage;

            _riskAcceptedThisSession = EditorUtility.DisplayDialog(RiskTitle, message, "Continue", "Cancel");
            return _riskAcceptedThisSession;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header("Editor Style Explorer", "Theme Editor accessory for experimental fonts, icons, and editor style mappings.", EditorGUIUtility.isProSkin ? "Unity Dark" : "Unity Light");
            DrawRiskStatusPanel();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _search = GUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.MinWidth(180f));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    Refresh();
                if (_selected != null && GUILayout.Button("Copy Selected Name", EditorStyles.toolbarButton, GUILayout.Width(140f)))
                {
                    EditorGUIUtility.systemCopyBuffer = _selected.name;
                    ShowNotification(new GUIContent("Copied: " + _selected.name));
                }
                if (GUILayout.Button("Apply Text/Fonts", EditorStyles.toolbarButton, GUILayout.Width(126f)))
                {
                    if (ConfirmExperimentalStyleChange("Apply session GUIStyle text/font overrides to known Unity editor styles."))
                        PungentEditorGuiStyleBridge.ApplySessionThemeToAllStyles(false);
                }

                if (GUILayout.Button("Apply Gutter Fallback", EditorStyles.toolbarButton, GUILayout.Width(142f)))
                {
                    if (ConfirmExperimentalStyleChange("Apply the native hierarchy gutter GUIStyle fallback."))
                        PungentEditorGuiStyleBridge.ApplyNativeHierarchyGutterFallback();
                }

                if (GUILayout.Button("Reset Session", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    PungentEditorGuiStyleBridge.DisableAndRestore();
            }

            DrawTextRoleMappingPanel();
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue), GUILayout.Width(Mathf.Min(320f, position.width * 0.46f))))
                {
                    UtilityWindowTheme.SectionTitle("GUIStyles", UtilityWindowTheme.Blue, _styles.Count.ToString());
                    _scroll = EditorGUILayout.BeginScrollView(_scroll);
                    for (int i = 0; i < _styles.Count; i++)
                    {
                        GUIStyle style = _styles[i];
                        if (style == null || !Matches(style.name))
                            continue;

                        bool selected = ReferenceEquals(style, _selected);
                        using (UtilityWindowTheme.Background(selected ? UtilityWindowTheme.Cyan : Color.clear))
                        {
                            if (GUILayout.Button(string.IsNullOrEmpty(style.name) ? "<unnamed>" : style.name, EditorStyles.miniButton))
                                _selected = style;
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
                {
                    UtilityWindowTheme.SectionTitle("Preview", UtilityWindowTheme.Teal, _selected == null ? "none" : _selected.name);
                    if (_selected == null)
                    {
                        EditorGUILayout.LabelField("Select a GUIStyle name from the left list.", UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Name", _selected.name, UtilityWindowTheme.BodyStyle);
                        EditorGUILayout.Space(4f);
                        GUILayout.Label("The quick brown fox jumps over the lazy dog", _selected, GUILayout.MinHeight(24f));
                        GUILayout.Button("Button Preview", _selected, GUILayout.MinHeight(24f));
                        EditorGUILayout.Space(8f);

                        EditorGUILayout.LabelField("Session Override", UtilityWindowTheme.SectionHeaderStyle);
                        _selectedTextRole = (UtilityWindowTheme.TextRole)EditorGUILayout.EnumPopup(new GUIContent("Font Role", "Optional Pungent font role to assign to this GUIStyle for the current editor session."), _selectedTextRole);
                        _selectedColorRoleIndex = Mathf.Clamp(_selectedColorRoleIndex, 0, UtilityWindowTheme.EditableColorRoles.Length - 1);
                        _selectedColorRoleIndex = EditorGUILayout.Popup(new GUIContent("Text Colour Role", "Theme colour role to apply to the selected GUIStyle text states."), _selectedColorRoleIndex, UtilityWindowTheme.EditableColorRoles.Select(UtilityWindowTheme.GetRoleDisplayName).ToArray());
                        _includeFont = EditorGUILayout.Toggle(new GUIContent("Apply Font", "Assign the selected Pungent font role if a font asset is configured."), _includeFont);
                        _includeBackground = EditorGUILayout.Toggle(new GUIContent("Subtle Background", "Optional: add a subtle generated background to this style. Keep disabled for most built-in styles."), _includeBackground);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (UtilityWindowTheme.TintedButton("Apply To Selected", UtilityWindowTheme.Cyan, GUILayout.Height(23f)))
                            {
                                if (ConfirmExperimentalStyleChange("Apply a session override to the selected GUIStyle."))
                                {
                                    string role = UtilityWindowTheme.EditableColorRoles[_selectedColorRoleIndex];
                                    PungentEditorGuiStyleBridge.ApplyThemeToStyle(_selected, _selectedTextRole, role, _includeFont, _includeBackground);
                                }
                            }
                            if (UtilityWindowTheme.TintedButton("Reset Session", UtilityWindowTheme.Red, GUILayout.Height(23f)))
                                PungentEditorGuiStyleBridge.DisableAndRestore();
                        }

                        EditorGUILayout.Space(8f);
                        EditorGUILayout.LabelField("Notes", "USS bridge generation handles broad editor colours. This panel can also apply reversible session-only GUIStyle text/font overrides for IMGUI styles.", UtilityWindowTheme.MutedMiniLabelStyle);
                    }
                }
            }
        }

        private void DrawRiskStatusPanel()
        {
            bool bridge = PungentEditorSkinBridge.ExperimentalBridgeEnabled;
            bool session = PungentEditorGuiStyleBridge.SessionOverridesEnabled;
            bool applyOnLoad = PungentEditorSkinBridge.ApplyOnLoad;

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Amber, 0.12f, 0.04f, 5, 3)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Recovery", UtilityWindowTheme.Amber, "experimental");
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(bridge ? "USS bridge on" : "USS bridge off", bridge ? UtilityWindowTheme.Cyan : UtilityWindowTheme.Neutral, 94f);
                    UtilityWindowTheme.CountPill(session ? "session on" : "session off", session ? UtilityWindowTheme.Green : UtilityWindowTheme.Neutral, 82f);
                    UtilityWindowTheme.CountPill(applyOnLoad ? "launch on" : "launch off", applyOnLoad ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 82f);
                    if (GUILayout.Button(new GUIContent("Disable + Restore", "Clear generated USS files and restore reversible GUIStyle session overrides."), EditorStyles.miniButton, GUILayout.Width(118f), GUILayout.Height(20f)))
                        PungentEditorSkinBridge.DisableAndRestore();
                }

                if (bridge || session || applyOnLoad)
                {
                    EditorGUILayout.HelpBox("Experimental editor styling is active. If Unity editor styles look broken, use Disable + Restore, then repaint, refresh the domain, or restart the Unity Editor.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("Opening this accessory is safe. Applying editor-wide USS or GUIStyle changes will ask for confirmation first.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
            }
        }

        private void DrawTextRoleMappingPanel()
        {
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Purple, 0.16f, 0.06f, 6, 4)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.SectionTitle("Mappings", UtilityWindowTheme.Purple, "USS + session GUIStyle");
                    GUILayout.FlexibleSpace();

                    if (UtilityWindowTheme.TintedButton("Reset Mapping", UtilityWindowTheme.Neutral, GUILayout.Width(104f), GUILayout.Height(22f)))
                    {
                        PungentEditorTextRoleMap.ResetAll();
                        if (PungentEditorSkinBridge.ExperimentalBridgeEnabled)
                        {
                            if (ConfirmExperimentalStyleChange("Regenerate the experimental editor USS bridge after resetting text-role mappings."))
                                PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
                        }
                    }

                    if (UtilityWindowTheme.TintedButton("Regenerate USS", UtilityWindowTheme.Cyan, GUILayout.Width(112f), GUILayout.Height(22f)))
                    {
                        if (ConfirmExperimentalStyleChange("Regenerate the experimental editor USS bridge files."))
                            PungentEditorSkinBridge.GenerateAndImportCurrentTheme();
                    }

                    if (UtilityWindowTheme.TintedButton("Apply Session", UtilityWindowTheme.Green, GUILayout.Width(104f), GUILayout.Height(22f)))
                    {
                        if (ConfirmExperimentalStyleChange("Apply session GUIStyle text/font overrides to mapped editor targets."))
                            PungentEditorGuiStyleBridge.ApplySessionThemeToAllStyles(false);
                    }
                }

                EditorGUILayout.LabelField(
                    "Choose which Appearance Lab font role is assigned to common editor text groups. USS controls UI Toolkit selectors; Session controls IMGUI GUIStyles. Session styling now preserves Unity's original font sizes.",
                    UtilityWindowTheme.MutedMiniLabelStyle);

                bool fallback = PungentEditorTextRoleMap.NativeHierarchyGutterGuiStyleFallbackEnabled;
                EditorGUI.BeginChangeCheck();
                fallback = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        "Advanced: native hierarchy gutter GUIStyle fallback",
                        "Use a reversible IMGUI GUIStyle fallback for the far-left hierarchy strip if USS cannot reach it. Keep this disabled unless the hierarchy gutter is visibly detached."),
                    fallback);

                if (EditorGUI.EndChangeCheck())
                    PungentEditorTextRoleMap.NativeHierarchyGutterGuiStyleFallbackEnabled = fallback;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Editor target", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
                    EditorGUILayout.LabelField("Font role", EditorStyles.miniBoldLabel, GUILayout.Width(104f));
                    EditorGUILayout.LabelField("USS", EditorStyles.miniBoldLabel, GUILayout.Width(42f));
                    EditorGUILayout.LabelField("Session", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
                    EditorGUILayout.LabelField("Notes", EditorStyles.miniBoldLabel);
                }

                _mappingScroll = EditorGUILayout.BeginScrollView(_mappingScroll, GUILayout.Height(212f));

                for (int i = 0; i < PungentEditorTextRoleMap.OrderedTargets.Length; i++)
                {
                    PungentEditorTextTarget target = PungentEditorTextRoleMap.OrderedTargets[i];

                    EditorGUI.BeginChangeCheck();

                    UtilityWindowTheme.TextRole role = PungentEditorTextRoleMap.GetTextRole(target);
                    bool applyUss = PungentEditorTextRoleMap.GetApplyUss(target);
                    bool applyGui = PungentEditorTextRoleMap.GetApplyGuiStyle(target);

                    using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(i % 2 == 0 ? UtilityWindowTheme.HeaderTint : UtilityWindowTheme.Neutral, 0.08f, 0.025f, 3, 1)))
                    {
                        EditorGUILayout.LabelField(PungentEditorTextRoleMap.GetDisplayName(target), UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(150f));
                        role = (UtilityWindowTheme.TextRole)EditorGUILayout.EnumPopup(role, GUILayout.Width(104f));
                        applyUss = EditorGUILayout.Toggle(applyUss, GUILayout.Width(42f));
                        applyGui = EditorGUILayout.Toggle(applyGui, GUILayout.Width(58f));
                        EditorGUILayout.LabelField(PungentEditorTextRoleMap.GetDescription(target), UtilityWindowTheme.MutedMiniLabelStyle);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        PungentEditorTextRoleMap.SetTextRole(target, role);
                        PungentEditorTextRoleMap.SetApplyUss(target, applyUss);
                        PungentEditorTextRoleMap.SetApplyGuiStyle(target, applyGui);
                    }
                }

                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(PungentEditorGuiStyleBridge.LastAction, UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();

                    if (PungentEditorSkinBridge.ExperimentalBridgeEnabled)
                        UtilityWindowTheme.CountPill("USS bridge on", UtilityWindowTheme.Cyan, 88f);
                    else
                        UtilityWindowTheme.CountPill("USS bridge off", UtilityWindowTheme.Neutral, 88f);

                    if (PungentEditorGuiStyleBridge.SessionOverridesEnabled)
                        UtilityWindowTheme.CountPill("session on", UtilityWindowTheme.Green, 78f);
                    else
                        UtilityWindowTheme.CountPill("session off", UtilityWindowTheme.Neutral, 78f);
                }
            }
        }

        private bool Matches(string name)
        {
            return string.IsNullOrWhiteSpace(_search) || (!string.IsNullOrEmpty(name) && name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void Refresh()
        {
            _styles.Clear();
            if (GUI.skin != null)
            {
                if (GUI.skin.customStyles != null)
                    _styles.AddRange(GUI.skin.customStyles);
                AddIfNamed(GUI.skin.box);
                AddIfNamed(GUI.skin.button);
                AddIfNamed(GUI.skin.label);
                AddIfNamed(GUI.skin.textField);
                AddIfNamed(GUI.skin.textArea);
                AddIfNamed(GUI.skin.window);
            }

            _styles.Sort((a, b) => string.Compare(a == null ? string.Empty : a.name, b == null ? string.Empty : b.name, StringComparison.OrdinalIgnoreCase));
        }

        private void AddIfNamed(GUIStyle style)
        {
            if (style != null && !_styles.Contains(style))
                _styles.Add(style);
        }
    }
#endif
}
