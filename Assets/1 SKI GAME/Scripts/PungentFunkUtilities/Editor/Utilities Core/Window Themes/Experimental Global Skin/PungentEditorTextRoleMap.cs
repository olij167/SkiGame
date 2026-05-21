namespace PungentFunk.Utilities.Editor.Theme
{
#if UNITY_EDITOR
    using System;

    public enum PungentEditorTextTarget
    {
        HierarchyItems,
        HierarchyGutter,
        InspectorHeaders,
        InspectorLabels,
        PropertyFields,
        ToolbarButtons,
        Tabs,
        ConsoleRows,
        ProjectItems,
        CodePreview,
        PathLabels,
        Links,
        MutedHelpText
    }

    public static class PungentEditorTextRoleMap
    {
        private const string PrefPrefix = "GenericUtility.WindowTheme.ExperimentalEditorSkin.TextRoleMap.";
        private const string PrefRolePrefix = PrefPrefix + "Role.";
        private const string PrefApplyUssPrefix = PrefPrefix + "ApplyUss.";
        private const string PrefApplyGuiStylePrefix = PrefPrefix + "ApplyGuiStyle.";
        private const string PrefNativeHierarchyGutterFallback = PrefPrefix + "NativeHierarchyGutterFallback";

        public static readonly PungentEditorTextTarget[] OrderedTargets =
        {
            PungentEditorTextTarget.HierarchyItems,
            PungentEditorTextTarget.HierarchyGutter,
            PungentEditorTextTarget.InspectorHeaders,
            PungentEditorTextTarget.InspectorLabels,
            PungentEditorTextTarget.PropertyFields,
            PungentEditorTextTarget.ToolbarButtons,
            PungentEditorTextTarget.Tabs,
            PungentEditorTextTarget.ConsoleRows,
            PungentEditorTextTarget.ProjectItems,
            PungentEditorTextTarget.CodePreview,
            PungentEditorTextTarget.PathLabels,
            PungentEditorTextTarget.Links,
            PungentEditorTextTarget.MutedHelpText
        };

        public static bool NativeHierarchyGutterGuiStyleFallbackEnabled
        {
            get => UtilityWindowPrefs.GetBool(PrefNativeHierarchyGutterFallback, false);
            set => UtilityWindowPrefs.SetBool(PrefNativeHierarchyGutterFallback, value);
        }

        public static UtilityWindowTheme.TextRole GetTextRole(PungentEditorTextTarget target)
        {
            UtilityWindowTheme.TextRole fallback = GetDefaultTextRole(target);
            int stored = UtilityWindowPrefs.GetInt(PrefRolePrefix + target, (int)fallback);

            if (!Enum.IsDefined(typeof(UtilityWindowTheme.TextRole), stored))
                return fallback;

            return (UtilityWindowTheme.TextRole)stored;
        }

        public static void SetTextRole(PungentEditorTextTarget target, UtilityWindowTheme.TextRole role)
        {
            UtilityWindowPrefs.SetInt(PrefRolePrefix + target, (int)role);
        }

        public static bool GetApplyUss(PungentEditorTextTarget target)
        {
            return UtilityWindowPrefs.GetBool(PrefApplyUssPrefix + target, GetDefaultApplyUss(target));
        }

        public static void SetApplyUss(PungentEditorTextTarget target, bool value)
        {
            UtilityWindowPrefs.SetBool(PrefApplyUssPrefix + target, value);
        }

        public static bool GetApplyGuiStyle(PungentEditorTextTarget target)
        {
            return UtilityWindowPrefs.GetBool(PrefApplyGuiStylePrefix + target, GetDefaultApplyGuiStyle(target));
        }

        public static void SetApplyGuiStyle(PungentEditorTextTarget target, bool value)
        {
            UtilityWindowPrefs.SetBool(PrefApplyGuiStylePrefix + target, value);
        }

        public static void ResetAll()
        {
            for (int i = 0; i < OrderedTargets.Length; i++)
            {
                PungentEditorTextTarget target = OrderedTargets[i];
                SetTextRole(target, GetDefaultTextRole(target));
                SetApplyUss(target, GetDefaultApplyUss(target));
                SetApplyGuiStyle(target, GetDefaultApplyGuiStyle(target));
            }

            NativeHierarchyGutterGuiStyleFallbackEnabled = false;
        }

        public static string GetDisplayName(PungentEditorTextTarget target)
        {
            switch (target)
            {
                case PungentEditorTextTarget.HierarchyItems: return "Hierarchy items";
                case PungentEditorTextTarget.HierarchyGutter: return "Hierarchy gutter";
                case PungentEditorTextTarget.InspectorHeaders: return "Inspector headers";
                case PungentEditorTextTarget.InspectorLabels: return "Inspector labels";
                case PungentEditorTextTarget.PropertyFields: return "Property fields";
                case PungentEditorTextTarget.ToolbarButtons: return "Toolbar buttons";
                case PungentEditorTextTarget.Tabs: return "Dock tabs";
                case PungentEditorTextTarget.ConsoleRows: return "Console rows";
                case PungentEditorTextTarget.ProjectItems: return "Project items";
                case PungentEditorTextTarget.CodePreview: return "Code/script preview";
                case PungentEditorTextTarget.PathLabels: return "Path labels";
                case PungentEditorTextTarget.Links: return "Links";
                case PungentEditorTextTarget.MutedHelpText: return "Muted/help text";
                default: return ObjectNamesSafe(target.ToString());
            }
        }

        public static string GetDescription(PungentEditorTextTarget target)
        {
            switch (target)
            {
                case PungentEditorTextTarget.HierarchyItems: return "Scene hierarchy object names and tree item labels.";
                case PungentEditorTextTarget.HierarchyGutter: return "Foldouts, indent rail, and native hierarchy gutter fallback.";
                case PungentEditorTextTarget.InspectorHeaders: return "Component headers, object headers, and title rows.";
                case PungentEditorTextTarget.InspectorLabels: return "Inspector property labels and foldout captions.";
                case PungentEditorTextTarget.PropertyFields: return "Text fields, object fields, dropdown inputs, and numeric fields.";
                case PungentEditorTextTarget.ToolbarButtons: return "Toolbar buttons and editor chrome controls.";
                case PungentEditorTextTarget.Tabs: return "Docked window tab labels.";
                case PungentEditorTextTarget.ConsoleRows: return "Console/log text when accessible.";
                case PungentEditorTextTarget.ProjectItems: return "Project browser and tree/list item labels.";
                case PungentEditorTextTarget.CodePreview: return "MonoScript/code preview text.";
                case PungentEditorTextTarget.PathLabels: return "Asset paths, breadcrumbs, and file-like labels.";
                case PungentEditorTextTarget.Links: return "Link-like editor text when accessible.";
                case PungentEditorTextTarget.MutedHelpText: return "Hints, help text, breadcrumbs, and subdued labels.";
                default: return string.Empty;
            }
        }

        public static string GetColorRole(PungentEditorTextTarget target)
        {
            switch (target)
            {
                case PungentEditorTextTarget.InspectorHeaders:
                case PungentEditorTextTarget.Tabs:
                    return UtilityWindowTheme.RoleTitleText;

                case PungentEditorTextTarget.InspectorLabels:
                    return UtilityWindowTheme.RoleSubtitleText;

                case PungentEditorTextTarget.PathLabels:
                case PungentEditorTextTarget.CodePreview:
                    return UtilityWindowTheme.RolePathText;

                case PungentEditorTextTarget.Links:
                    return UtilityWindowTheme.RoleSecondary;

                case PungentEditorTextTarget.MutedHelpText:
                    return UtilityWindowTheme.RoleMutedText;

                default:
                    return UtilityWindowTheme.RoleCardText;
            }
        }

        public static bool IsBackgroundSafe(PungentEditorTextTarget target)
        {
            switch (target)
            {
                case PungentEditorTextTarget.HierarchyItems:
                case PungentEditorTextTarget.HierarchyGutter:
                case PungentEditorTextTarget.PropertyFields:
                case PungentEditorTextTarget.CodePreview:
                case PungentEditorTextTarget.ProjectItems:
                    return true;

                default:
                    return false;
            }
        }

        public static string GetUssSelectors(PungentEditorTextTarget target)
        {
            switch (target)
            {
                case PungentEditorTextTarget.HierarchyItems:
                    return ".unity-scene-hierarchy-item, .unity-scene-hierarchy-item__label, .unity-tree-view__item, .unity-tree-view__item-content, .unity-base-tree-view__item, .unity-base-tree-view__item-content";

                case PungentEditorTextTarget.HierarchyGutter:
                    return ".unity-tree-view__item-toggle, .unity-tree-view__item-indent, .unity-tree-view__item-indentation, .unity-base-tree-view__item-toggle, .unity-base-tree-view__item-indent, .unity-base-tree-view__item-indentation, .unity-foldout__checkmark";

                case PungentEditorTextTarget.InspectorHeaders:
                    return ".unity-inspector-titlebar-label, .unity-inspector-element__header, .unity-inspector-object-name, .unity-inspector-main-container .unity-bold-label";

                case PungentEditorTextTarget.InspectorLabels:
                    return ".unity-base-field__label, .unity-property-field__label, .unity-foldout__text, .unity-inspector-main-container .unity-label";

                case PungentEditorTextTarget.PropertyFields:
                    return ".unity-base-field__input, .unity-text-field__input, .unity-object-field__input, .unity-popup-field__input, .unity-base-popup-field__input";

                case PungentEditorTextTarget.ToolbarButtons:
                    return ".unity-toolbar-button, .unity-button, .ToolbarButton, .unity-toolbar-menu, .unity-toolbar .unity-label";

                case PungentEditorTextTarget.Tabs:
                    return ".unity-tab__label, .dragtab-label, .dockHeader .unity-label";

                case PungentEditorTextTarget.ConsoleRows:
                    return ".ConsoleWindow .unity-label, .unity-console-window .unity-label, .console-row, .console-log-entry";

                case PungentEditorTextTarget.ProjectItems:
                    return ".ProjectBrowser .unity-label, .unity-project-browser .unity-label, .unity-project-browser .unity-tree-view__item, .unity-project-browser .unity-list-view__item";

                case PungentEditorTextTarget.CodePreview:
                    return ".ScriptInspector, .ScriptText, .ScriptTextNumber, .ScriptTextPreprocessor, .unity-script-inspector, .unity-script-inspector__preview, .unity-inspector-preview, .unity-code-preview";

                case PungentEditorTextTarget.PathLabels:
                    return ".unity-breadcrumbs, .unity-breadcrumbs__item, .unity-object-field-display, .unity-object-field-display__label";

                case PungentEditorTextTarget.Links:
                    return ".unity-link, .unity-text-element--link, .link-label";

                case PungentEditorTextTarget.MutedHelpText:
                    return ".unity-help-box .unity-label, .unity-tooltip, .unity-inspector-main-container .unity-text-element, .unity-base-field__help-text";

                default:
                    return string.Empty;
            }
        }

        public static PungentEditorTextTarget ResolveTargetFromGuiStyleName(string styleName)
        {
            string lower = (styleName ?? string.Empty).ToLowerInvariant();

            if (lower.Contains("script") || lower.Contains("code") || lower.Contains("preprocessor"))
                return PungentEditorTextTarget.CodePreview;

            if (lower.Contains("mono") || lower.Contains("breadcrumb") || lower.Contains("path"))
                return PungentEditorTextTarget.PathLabels;

            if (lower.Contains("component") || lower.Contains("inspector") || lower.Contains("object header"))
                return PungentEditorTextTarget.InspectorHeaders;

            if (lower.Contains("project") || lower.Contains("browser"))
                return PungentEditorTextTarget.ProjectItems;

            if (lower.Contains("console") || lower.Contains("log"))
                return PungentEditorTextTarget.ConsoleRows;

            if (lower.Contains("toolbar") || lower.Contains("button") || lower.Contains("popup") || lower.Contains("dropdown"))
                return PungentEditorTextTarget.ToolbarButtons;

            if (lower.Contains("title") || lower.Contains("header") || lower.Contains("dragtab") || lower.Contains("tab"))
                return PungentEditorTextTarget.Tabs;

            if (lower.Contains("mini") || lower.Contains("hint") || lower.Contains("info") || lower.Contains("help"))
                return PungentEditorTextTarget.MutedHelpText;

            if (IsHierarchyOrTreeStyleName(lower))
                return lower.Contains("toggle") || lower.Contains("indent") || lower.Contains("selection")
                    ? PungentEditorTextTarget.HierarchyGutter
                    : PungentEditorTextTarget.HierarchyItems;

            if (lower.Contains("foldout"))
                return PungentEditorTextTarget.InspectorLabels;

            if (lower.Contains("objectfield") || lower.Contains("textfield") || lower.Contains("text field") || lower.Contains("field") || lower.Contains("textarea") || lower.Contains("text area"))
                return PungentEditorTextTarget.PropertyFields;

            if (lower.Contains("link"))
                return PungentEditorTextTarget.Links;

            return PungentEditorTextTarget.HierarchyItems;
        }

        public static bool IsProbablyNonTextGuiStyle(string styleName)
        {
            string lower = (styleName ?? string.Empty).ToLowerInvariant();
            return lower.Contains("icon") ||
                   lower.Contains("image") ||
                   lower.Contains("texture") ||
                   (lower.Contains("preview") && !lower.Contains("text") && !lower.Contains("script") && !lower.Contains("code"));
        }

        public static bool IsHierarchyOrTreeStyleName(string lower)
        {
            if (string.IsNullOrEmpty(lower))
                return false;

            return lower.Contains("hierarchy") ||
                   lower.Contains("tree") ||
                   lower.Contains("tv ") ||
                   lower == "tv line" ||
                   lower == "tv linebold" ||
                   lower == "tv selection" ||
                   lower.Contains("scenehierarchy") ||
                   lower.Contains("scene hierarchy") ||
                   lower.Contains("foldout predrop") ||
                   lower.Contains("foldout header");
        }

        private static UtilityWindowTheme.TextRole GetDefaultTextRole(PungentEditorTextTarget target)
        {
            switch (target)
            {
                case PungentEditorTextTarget.InspectorHeaders:
                    return UtilityWindowTheme.TextRole.Subheading;

                case PungentEditorTextTarget.Tabs:
                    return UtilityWindowTheme.TextRole.Subheading;

                case PungentEditorTextTarget.InspectorLabels:
                    return UtilityWindowTheme.TextRole.Body;

                case PungentEditorTextTarget.PropertyFields:
                    return UtilityWindowTheme.TextRole.Field;

                case PungentEditorTextTarget.CodePreview:
                    return UtilityWindowTheme.TextRole.Code;

                case PungentEditorTextTarget.PathLabels:
                    return UtilityWindowTheme.TextRole.Path;

                case PungentEditorTextTarget.Links:
                    return UtilityWindowTheme.TextRole.Link;

                case PungentEditorTextTarget.MutedHelpText:
                    return UtilityWindowTheme.TextRole.Muted;

                default:
                    return UtilityWindowTheme.TextRole.Body;
            }
        }

        private static bool GetDefaultApplyUss(PungentEditorTextTarget target)
        {
            return true;
        }

        private static bool GetDefaultApplyGuiStyle(PungentEditorTextTarget target)
        {
            switch (target)
            {
                case PungentEditorTextTarget.HierarchyGutter:
                    return false;

                default:
                    return true;
            }
        }

        private static string ObjectNamesSafe(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var chars = value.ToCharArray();
            var result = string.Empty;
            for (int i = 0; i < chars.Length; i++)
            {
                if (i > 0 && char.IsUpper(chars[i]))
                    result += " ";
                result += chars[i];
            }

            return result;
        }
    }
#endif
}