namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System.Collections.Generic;

    /// <summary>
    /// Known Unity editor USS selectors used by the experimental editor-wide skin bridge.
    /// This intentionally uses generated USS only; it does not wrap IMGUI callbacks or patch Unity installation resources.
    /// </summary>
    public static class PungentEditorSkinSelectorMap
    {
        private static readonly PungentEditorStyleOverride[] _defaults =
        {
            // High-level Unity editor chrome and docked windows.
            new PungentEditorStyleOverride(".AppToolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 1f, true, "Main app toolbar."),
            new PungentEditorStyleOverride(".PreToolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.95f, true, "Pre-toolbar area."),
            new PungentEditorStyleOverride(".Toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.95f, true, "Generic toolbar."),
            new PungentEditorStyleOverride(".dockHeader", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 1f, true, "Dock header area."),
            new PungentEditorStyleOverride(".TabWindowBackground", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.95f, true, "Main docked window background."),
            new PungentEditorStyleOverride(".unity-imgui-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.20f, true, "Generic IMGUI container tint."),
            new PungentEditorStyleOverride(".unity-inspector-element", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.16f, true, "Inspector element background."),

            // Project, Hierarchy, tree/list views.
            new PungentEditorStyleOverride(".ProjectBrowserTopBarBg", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.95f, true, "Project Browser top bar."),
            new PungentEditorStyleOverride(".ProjectBrowserBottomBarBg", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.80f, true, "Project Browser bottom bar."),
            new PungentEditorStyleOverride(".ProjectBrowserIconAreaBg", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.18f, true, "Project icon area."),
            new PungentEditorStyleOverride(".project-browser-preview", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.18f, true, "Project preview panel."),
            new PungentEditorStyleOverride(".project-browser-details", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.16f, true, "Project detail panel."),
            new PungentEditorStyleOverride(".unity-project-browser-toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.92f, true, "Project browser toolbar."),
            new PungentEditorStyleOverride(".ScrollViewAlt", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.14f, true, "Alternating scroll view background."),
            new PungentEditorStyleOverride(".unity-scroll-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.14f, true, "UI Toolkit scroll view."),
            new PungentEditorStyleOverride(".unity-list-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.12f, true, "UI Toolkit list view."),
            new PungentEditorStyleOverride(".unity-tree-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.12f, true, "UI Toolkit tree view."),
            new PungentEditorStyleOverride(".unity-tree-view__item", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Tree item text."),
            new PungentEditorStyleOverride(".unity-tree-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.42f, true, "Selected tree item background."),
            new PungentEditorStyleOverride(".unity-collection-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.42f, true, "Selected collection item background."),
            new PungentEditorStyleOverride(".TV Selection", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.50f, true, "Tree view selection."),
            new PungentEditorStyleOverride(".TV LineBold", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleSecondary, 1f, true, "Tree view bold line style."),

            // Text and fields.
            new PungentEditorStyleOverride(".label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Common editor label text."),
            new PungentEditorStyleOverride(".unity-label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "UI Toolkit label text."),
            new PungentEditorStyleOverride(".unity-base-field__label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleSubtitleText, 1f, true, "Field label text."),
            new PungentEditorStyleOverride(".unity-base-field__input", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Field input text."),
            new PungentEditorStyleOverride(".unity-base-field__input", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.14f, true, "Field input background."),
            new PungentEditorStyleOverride(".unity-text-field__input", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Text field input text."),
            new PungentEditorStyleOverride(".unity-text-field__input", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.16f, true, "Text field input background."),
            new PungentEditorStyleOverride(".ToolbarSearchTextField", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.22f, true, "Toolbar search field."),
            new PungentEditorStyleOverride(".unity-toolbar-search-field", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.22f, true, "UI Toolkit toolbar search field."),
            new PungentEditorStyleOverride(".unity-toolbar-search-field__input", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "UI Toolkit toolbar search text."),

            // Buttons, tabs, popups.
            new PungentEditorStyleOverride(".toolbarbutton", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.32f, true, "Toolbar button background."),
            new PungentEditorStyleOverride(".toolbarbuttonRight", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.32f, true, "Toolbar button right segment."),
            new PungentEditorStyleOverride(".ToolbarDropDownToogleRight", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.42f, true, "Toolbar dropdown right segment."),
            new PungentEditorStyleOverride(".ToolbarPopupLeft", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.38f, true, "Toolbar popup left segment."),
            new PungentEditorStyleOverride(".ToolbarPopup", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.38f, true, "Toolbar popup."),
            new PungentEditorStyleOverride(".AppCommandLeft", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.42f, true, "App command left button."),
            new PungentEditorStyleOverride(".AppCommandMid", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.42f, true, "App command middle button."),
            new PungentEditorStyleOverride(".AppCommand", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.42f, true, "App command button."),
            new PungentEditorStyleOverride(".AppToolbarButtonLeft", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.38f, true, "App toolbar left button."),
            new PungentEditorStyleOverride(".AppToolbarButtonRight", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.38f, true, "App toolbar right button."),
            new PungentEditorStyleOverride(".DropDown", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.32f, true, "Dropdown control."),
            new PungentEditorStyleOverride(".MiniPopup", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleSecondary, 0.28f, true, "Mini popup background."),
            new PungentEditorStyleOverride(".ExposablePopupMenu", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.92f, true, "Popup menu background."),
            new PungentEditorStyleOverride(".minibutton", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.28f, true, "Mini button."),
            new PungentEditorStyleOverride(".unity-button", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.20f, true, "UI Toolkit button background."),
            new PungentEditorStyleOverride(".unity-button", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "UI Toolkit button text."),
            new PungentEditorStyleOverride(".unity-toolbar-button", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.18f, true, "UI Toolkit toolbar button background."),
            new PungentEditorStyleOverride(".unity-toolbar-button", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "UI Toolkit toolbar button text."),
            new PungentEditorStyleOverride(".dragtab-label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleTitleText, 1f, true, "Tab label text."),
            new PungentEditorStyleOverride(".unity-tab__label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleTitleText, 1f, true, "UI Toolkit tab label."),
            new PungentEditorStyleOverride(".unity-tab__content", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.72f, true, "UI Toolkit tab content."),
            new PungentEditorStyleOverride(".dragtab.active", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.34f, true, "Active dock tab."),
            new PungentEditorStyleOverride(".unity-tab--active", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.34f, true, "Active UI Toolkit tab."),


            // Scene Hierarchy / Hierarchy left rail and row content.
            // These selectors vary by Unity version; unknown selectors are safely ignored by USS.
            // Keep this block focused on hierarchy/tree-view coverage only.
            new PungentEditorStyleOverride(".SceneHierarchyWindow", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.96f, true, "Hierarchy window background."),
            new PungentEditorStyleOverride(".SceneHierarchyWindow .unity-imgui-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.46f, true, "Hierarchy IMGUI container backing tint."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.96f, true, "UI Toolkit hierarchy window."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-imgui-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.46f, true, "Hierarchy UI Toolkit/IMGUI bridge container."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.96f, true, "Hierarchy root variant."),

            new PungentEditorStyleOverride(".unity-scene-hierarchy-window__tree-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.48f, true, "Hierarchy tree view."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window__tree-view-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.46f, true, "Hierarchy tree view container."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy__tree-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.48f, true, "Hierarchy tree variant."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window__list-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.46f, true, "Hierarchy list view."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window__content-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.48f, true, "Hierarchy content container."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window__container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.48f, true, "Hierarchy container."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window__scroll-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.48f, true, "Hierarchy scroll view."),

            new PungentEditorStyleOverride(".unity-tree-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.24f, true, "Generic tree view backing."),
            new PungentEditorStyleOverride(".unity-tree-view__container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "Tree container."),
            new PungentEditorStyleOverride(".unity-tree-view__content-viewport", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "Tree content viewport."),
            new PungentEditorStyleOverride(".unity-tree-view__content-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "Tree content container."),
            new PungentEditorStyleOverride(".unity-collection-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.24f, true, "Collection view backing."),
            new PungentEditorStyleOverride(".unity-collection-view__scroll-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "Collection scroll view."),
            new PungentEditorStyleOverride(".unity-collection-view__content-viewport", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "Collection content viewport."),
            new PungentEditorStyleOverride(".unity-collection-view__content-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "Collection content container."),
            new PungentEditorStyleOverride(".unity-list-view", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.24f, true, "List view backing."),
            new PungentEditorStyleOverride(".unity-list-view__content-viewport", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "List content viewport."),
            new PungentEditorStyleOverride(".unity-list-view__content-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "List content container."),

            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-collection-view__item", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.18f, true, "Hierarchy collection row background."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-list-view__item", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.18f, true, "Hierarchy list row background."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-tree-view__item", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.18f, true, "Hierarchy tree row background."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-tree-view__item > *", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.18f, true, "Hierarchy tree row child background."),
            new PungentEditorStyleOverride(".unity-tree-view__item", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.14f, true, "Generic tree row background."),
            new PungentEditorStyleOverride(".unity-tree-view__item > *", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.14f, true, "Generic tree row child background."),
            new PungentEditorStyleOverride(".unity-tree-view__item-content", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Tree item content text."),
            new PungentEditorStyleOverride(".unity-tree-view__item-content-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.16f, true, "Tree item content container."),

            new PungentEditorStyleOverride(".unity-tree-view__item-indent", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.58f, true, "Tree item indent/left rail."),
            new PungentEditorStyleOverride(".unity-tree-view__item-indentation", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.58f, true, "Tree item indentation/left rail."),
            new PungentEditorStyleOverride(".unity-tree-view__item-indent-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.58f, true, "Tree item indent container."),
            new PungentEditorStyleOverride(".unity-tree-view__item-toggle", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.58f, true, "Hierarchy/tree item toggle background."),
            new PungentEditorStyleOverride(".unity-tree-view__item-toggle", PungentEditorSkinProperty.ImageTintColor, UtilityWindowTheme.RoleSecondary, 0.94f, true, "Hierarchy/tree item toggle tint."),
            new PungentEditorStyleOverride(".unity-tree-view__item-toggle .unity-toggle__input", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.58f, true, "Tree toggle input rail backing."),
            new PungentEditorStyleOverride(".unity-tree-view__item-toggle .unity-toggle__checkmark", PungentEditorSkinProperty.ImageTintColor, UtilityWindowTheme.RoleSecondary, 0.94f, true, "Tree toggle checkmark tint."),
            new PungentEditorStyleOverride(".unity-foldout__checkmark", PungentEditorSkinProperty.ImageTintColor, UtilityWindowTheme.RoleSecondary, 0.94f, true, "Foldout arrow tint."),
            new PungentEditorStyleOverride(".unity-foldout__text", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Foldout text."),

            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-collection-view__item:hover", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.24f, true, "Hierarchy collection row hover."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-list-view__item:hover", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.24f, true, "Hierarchy list row hover."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-tree-view__item:hover", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.24f, true, "Hierarchy tree row hover."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-tree-view__item:hover > *", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.24f, true, "Hierarchy tree row child hover."),

            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-collection-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.52f, true, "Hierarchy selected collection row."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-list-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.52f, true, "Hierarchy selected list row."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-tree-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.52f, true, "Hierarchy selected tree row."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-window .unity-tree-view__item--selected > *", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.52f, true, "Hierarchy selected tree row child."),
            new PungentEditorStyleOverride(".unity-tree-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.46f, true, "Selected tree item background."),
            new PungentEditorStyleOverride(".unity-collection-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.46f, true, "Selected collection item background."),
            new PungentEditorStyleOverride(".unity-list-view__item--selected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.46f, true, "Selected list item background."),

            new PungentEditorStyleOverride(".unity-scene-hierarchy-item", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Hierarchy item text."),
            new PungentEditorStyleOverride(".unity-scene-hierarchy-item__label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Hierarchy label text."),
            new PungentEditorStyleOverride(".unity-tree-view__item--selected .unity-scene-hierarchy-item__label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleTitleText, 1f, true, "Selected hierarchy label text."),
            new PungentEditorStyleOverride(".unity-tree-view__item--selected .unity-tree-view__item-content", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleTitleText, 1f, true, "Selected tree content text."),

            // Inspector structure and property fields.
            new PungentEditorStyleOverride(".unity-inspector-main-container", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.34f, true, "Inspector main container."),
            new PungentEditorStyleOverride(".unity-inspector-root", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.30f, true, "Inspector root."),
            new PungentEditorStyleOverride(".unity-inspector-editors-list", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.22f, true, "Inspector editors list."),
            new PungentEditorStyleOverride(".unity-inspector-editor", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.20f, true, "Inspector editor block."),
            new PungentEditorStyleOverride(".unity-inspector-titlebar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.78f, true, "Inspector titlebar."),
            new PungentEditorStyleOverride(".unity-inspector-titlebar-label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleTitleText, 1f, true, "Inspector titlebar label."),
            new PungentEditorStyleOverride(".unity-inspector-titlebar-icon", PungentEditorSkinProperty.ImageTintColor, UtilityWindowTheme.RoleSecondary, 0.90f, true, "Inspector titlebar icon."),
            new PungentEditorStyleOverride(".unity-object-header", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.80f, true, "Selected object header panel."),
            new PungentEditorStyleOverride(".unity-component-header", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.76f, true, "Component header panel."),
            new PungentEditorStyleOverride(".unity-property-field", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Property field text."),
            new PungentEditorStyleOverride(".unity-property-field__label", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleSubtitleText, 1f, true, "Property field label."),
            new PungentEditorStyleOverride(".unity-object-field", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Object field text."),
            new PungentEditorStyleOverride(".unity-object-field__input", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.16f, true, "Object field input background."),
            new PungentEditorStyleOverride(".unity-object-field__input", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Object field input text."),
            new PungentEditorStyleOverride(".unity-object-field__selector", PungentEditorSkinProperty.ImageTintColor, UtilityWindowTheme.RoleSecondary, 0.90f, true, "Object picker icon tint."),
            new PungentEditorStyleOverride(".unity-toggle__text", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Toggle text."),
            new PungentEditorStyleOverride(".unity-toggle__input", PungentEditorSkinProperty.BorderColor, UtilityWindowTheme.RoleNeutral, 0.55f, true, "Toggle input border."),


            // Script asset inspector / MonoScript preview. Coverage varies between IMGUI and UI Toolkit Unity versions.
            new PungentEditorStyleOverride(".ScriptInspector", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.28f, true, "Script inspector background."),
            new PungentEditorStyleOverride(".ScriptInspector", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Script inspector text."),
            new PungentEditorStyleOverride(".ScriptText", PungentEditorSkinProperty.Color, UtilityWindowTheme.RolePathText, 1f, true, "MonoScript preview text."),
            new PungentEditorStyleOverride(".ScriptTextNumber", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleMutedText, 1f, true, "MonoScript preview line numbers."),
            new PungentEditorStyleOverride(".ScriptTextPreprocessor", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleSecondary, 1f, true, "MonoScript preview preprocessor text."),
            new PungentEditorStyleOverride(".unity-script-inspector", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.30f, true, "UI Toolkit script inspector."),
            new PungentEditorStyleOverride(".unity-script-inspector__preview", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.18f, true, "UI Toolkit script preview background."),
            new PungentEditorStyleOverride(".unity-script-inspector__preview", PungentEditorSkinProperty.Color, UtilityWindowTheme.RolePathText, 1f, true, "UI Toolkit script preview text."),
            new PungentEditorStyleOverride(".unity-inspector-preview", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.16f, true, "Inspector preview background."),
            new PungentEditorStyleOverride(".unity-inspector-preview", PungentEditorSkinProperty.Color, UtilityWindowTheme.RolePathText, 1f, true, "Inspector preview text."),
            new PungentEditorStyleOverride(".unity-code-preview", PungentEditorSkinProperty.Color, UtilityWindowTheme.RolePathText, 1f, true, "Code preview text."),
            new PungentEditorStyleOverride(".unity-code-preview", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.24f, true, "Code preview background."),

            // Top editor toolbar variants.
            new PungentEditorStyleOverride(".unity-toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.98f, true, "UI Toolkit toolbar."),
            new PungentEditorStyleOverride(".unity-toolbar__contents", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.98f, true, "Toolbar content strip."),
            new PungentEditorStyleOverride(".unity-editor-toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.98f, true, "Editor toolbar."),
            new PungentEditorStyleOverride(".unity-main-toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.98f, true, "Main toolbar variant."),
            new PungentEditorStyleOverride(".unity-toolbar__left", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.98f, true, "Toolbar left zone."),
            new PungentEditorStyleOverride(".unity-toolbar__center", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.98f, true, "Toolbar center zone."),
            new PungentEditorStyleOverride(".unity-toolbar__right", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.98f, true, "Toolbar right zone."),
            new PungentEditorStyleOverride(".ToolbarLeftAlign", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.96f, true, "Toolbar left align area."),
            new PungentEditorStyleOverride(".ToolbarCenterAlign", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.96f, true, "Toolbar center align area."),
            new PungentEditorStyleOverride(".ToolbarRightAlign", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.96f, true, "Toolbar right align area."),
            new PungentEditorStyleOverride(".ToolbarZone", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.96f, true, "Toolbar zone."),
            new PungentEditorStyleOverride(".ToolbarButton", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.28f, true, "Toolbar button variant."),
            new PungentEditorStyleOverride(".ToolbarButton", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Toolbar button text."),
            new PungentEditorStyleOverride(".unity-overlay", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.86f, true, "Scene view overlay root."),
            new PungentEditorStyleOverride(".unity-overlay-header", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.86f, true, "Scene view overlay header."),
            new PungentEditorStyleOverride(".unity-scene-view-toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.88f, true, "Scene view toolbar."),

            // Console strips and filter toggles. These are intentionally grouped so future Unity selector drift is easier to maintain.
            new PungentEditorStyleOverride(".CN Toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.90f, true, "Console toolbar background."),
            new PungentEditorStyleOverride(".CN EntryBackEven", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.14f, true, "Console even row."),
            new PungentEditorStyleOverride(".CN EntryBackOdd", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.10f, true, "Console odd row."),
            new PungentEditorStyleOverride(".CN EntrySelected", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RolePrimary, 0.36f, true, "Console selected row."),
            new PungentEditorStyleOverride(".console-toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.90f, true, "Console toolbar variant."),
            new PungentEditorStyleOverride(".unity-console-toolbar", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.90f, true, "UI Toolkit console toolbar."),

            // Borders, radii, imagery and help boxes.
            new PungentEditorStyleOverride(".unity-button", PungentEditorSkinProperty.BorderColor, UtilityWindowTheme.RolePrimary, 0.55f, true, "Button border colour."),
            new PungentEditorStyleOverride(".unity-base-field__input", PungentEditorSkinProperty.BorderColor, UtilityWindowTheme.RoleNeutral, 0.45f, true, "Field border colour."),
            new PungentEditorStyleOverride(".unity-help-box", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleNeutral, 0.18f, true, "Help box background."),
            new PungentEditorStyleOverride(".unity-help-box", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleCardText, 1f, true, "Help box text."),
            new PungentEditorStyleOverride(".unity-icon", PungentEditorSkinProperty.ImageTintColor, UtilityWindowTheme.RoleSecondary, 0.88f, true, "Generic icon tint."),
            new PungentEditorStyleOverride(".unity-image", PungentEditorSkinProperty.ImageTintColor, UtilityWindowTheme.RoleSecondary, 0.78f, true, "Generic image tint."),
            new PungentEditorStyleOverride(".unity-button", PungentEditorSkinProperty.BorderRadius, UtilityWindowTheme.RolePrimary, 1f, true, "Button radius."),
            new PungentEditorStyleOverride(".unity-base-field__input", PungentEditorSkinProperty.BorderRadius, UtilityWindowTheme.RoleNeutral, 1f, true, "Input radius."),
            new PungentEditorStyleOverride(".unity-button", PungentEditorSkinProperty.BorderWidth, UtilityWindowTheme.RolePrimary, 1f, true, "Button border width."),
            new PungentEditorStyleOverride(".unity-base-field__input", PungentEditorSkinProperty.BorderWidth, UtilityWindowTheme.RoleNeutral, 1f, true, "Input border width."),

            // Fonts are skipped by the generator if no corresponding Pungent font role is assigned.
            new PungentEditorStyleOverride(".unity-label", PungentEditorSkinProperty.UnityFont, "Body", 1f, true, "Body font."),
            new PungentEditorStyleOverride(".unity-button", PungentEditorSkinProperty.UnityFont, "Body", 1f, true, "Button font."),
            new PungentEditorStyleOverride(".unity-tab__label", PungentEditorSkinProperty.UnityFont, "Heading", 1f, true, "Tab font."),
            new PungentEditorStyleOverride(".unity-base-field__label", PungentEditorSkinProperty.UnityFont, "Subheading", 1f, true, "Field label font."),

            // Deliberately off by default because these can feel invasive.
            new PungentEditorStyleOverride(".SceneTopBarBg", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.85f, false, "Scene top bar; disabled by default."),
            new PungentEditorStyleOverride(".GameViewBackground", PungentEditorSkinProperty.BackgroundColor, UtilityWindowTheme.RoleHeader, 0.85f, false, "Game view background; disabled by default."),
            new PungentEditorStyleOverride(".CN EntryInfoSmall", PungentEditorSkinProperty.Color, UtilityWindowTheme.RoleMutedText, 1f, true, "Console/info text."),
        };

        public static IReadOnlyList<PungentEditorStyleOverride> Defaults => _defaults;
    }
#endif
}
