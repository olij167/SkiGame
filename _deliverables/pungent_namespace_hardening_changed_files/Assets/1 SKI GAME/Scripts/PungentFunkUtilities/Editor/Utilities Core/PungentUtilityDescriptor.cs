namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Describes a reusable PungentFunk utility window or action.
    /// Descriptors are consumed by the control panel, context menus, shortcuts, and tray systems.
    /// </summary>
    [Serializable]
    public sealed class PungentUtilityDescriptor
    {
        public string Id;
        public string DisplayName;
        public string Category;
        public string Lab;
        public string Module;
        public string Description;
        public string MenuPath;
        public string[] Tags;
        public string WindowTypeName;
        public bool SupportsSceneOverlay;
        public bool SupportsContextMenu;
        public bool SupportsSelection;
        public bool IsLabHub;
        public int SortOrder;
        public string PackageStatus;
        public string[] RelatedUtilityIds;
        public Texture2D Icon;

        private Action _openAction;

        public PungentUtilityDescriptor(
            string id,
            string displayName,
            string category,
            string description,
            string menuPath,
            string windowTypeName,
            string[] tags = null,
            bool supportsSceneOverlay = false,
            bool supportsContextMenu = false,
            bool supportsSelection = false,
            Action openAction = null,
            string lab = null,
            string module = null,
            int sortOrder = 0,
            bool isLabHub = false,
            string packageStatus = null,
            string[] relatedUtilityIds = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim();
            Lab = PungentUtilityLabs.Normalize(lab);
            Module = string.IsNullOrWhiteSpace(module) ? Category : module.Trim();
            Description = description ?? string.Empty;
            MenuPath = menuPath ?? string.Empty;
            WindowTypeName = windowTypeName ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
            SupportsSceneOverlay = supportsSceneOverlay;
            SupportsContextMenu = supportsContextMenu;
            SupportsSelection = supportsSelection;
            IsLabHub = isLabHub;
            SortOrder = sortOrder;
            PackageStatus = PungentUtilityPackageStatus.Normalize(packageStatus);
            RelatedUtilityIds = relatedUtilityIds ?? Array.Empty<string>();
            _openAction = openAction;
        }

        public bool CanOpen => _openAction != null || ResolveWindowType() != null;

        public string Status => PungentUtilityPackageStatus.Normalize(PackageStatus);

        public bool HasRelatedUtilities => RelatedUtilityIds != null && RelatedUtilityIds.Length > 0;

        public void SetOpenAction(Action action)
        {
            _openAction = action;
        }

        public void Open()
        {
            OpenDirect();
        }

        /// <summary>
        /// Backwards-compatible open method used by the Utility Tray and older callers.
        /// </summary>
        public void OpenDirect()
        {
            if (_openAction != null)
            {
                _openAction.Invoke();
                return;
            }

            Type type = ResolveWindowType();
            if (type == null)
            {
                EditorUtility.DisplayDialog("PungentFunk Utilities", $"Could not find utility window type '{WindowTypeName}'.", "OK");
                return;
            }

            EditorWindow window = EditorWindow.GetWindow(type, false, DisplayName);
            window.titleContent = new GUIContent(DisplayName, Icon);
            window.Show();
        }

        public Type ResolveWindowType()
        {
            if (string.IsNullOrEmpty(WindowTypeName))
                return null;

            Type direct = Type.GetType(WindowTypeName);
            if (direct != null)
                return direct;

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(WindowTypeName);
                if (type != null)
                    return type;

                foreach (Type candidate in assembly.GetTypes())
                {
                    if (string.Equals(candidate.FullName, WindowTypeName, StringComparison.Ordinal) ||
                        string.Equals(candidate.Name, WindowTypeName, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string q = query.Trim();
            return Contains(DisplayName, q) ||
                   Contains(Category, q) ||
                   Contains(Lab, q) ||
                   Contains(Module, q) ||
                   Contains(Status, q) ||
                   Contains(Description, q) ||
                   Contains(MenuPath, q) ||
                   Contains(WindowTypeName, q) ||
                   TagsContain(q) ||
                   RelatedIdsContain(q);
        }

        private bool TagsContain(string query)
        {
            if (Tags == null)
                return false;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (Contains(Tags[i], query))
                    return true;
            }

            return false;
        }

        private bool RelatedIdsContain(string query)
        {
            if (RelatedUtilityIds == null)
                return false;

            for (int i = 0; i < RelatedUtilityIds.Length; i++)
            {
                if (Contains(RelatedUtilityIds[i], query))
                    return true;
            }

            return false;
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
    #endif

}
