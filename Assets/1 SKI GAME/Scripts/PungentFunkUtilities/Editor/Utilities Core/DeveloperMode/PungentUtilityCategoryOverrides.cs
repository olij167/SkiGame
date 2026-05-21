namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Project-local browser category appearance overrides.
    /// Factory category IDs/defaults remain in PungentUtilityCategories; this store only keeps reversible developer-mode edits.
    /// </summary>
    public enum PungentUtilityCategoryLifecycle
    {
        Visible,
        Hidden,
        Archived
    }

    [FilePath("ProjectSettings/PungentFunkUtilities/UtilityCategoryOverrides.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityCategoryOverrides : ScriptableSingleton<PungentUtilityCategoryOverrides>
    {
        [Serializable]
        public sealed class CategoryOverride
        {
            public string categoryId;
            public bool projectLocal;
            public PungentUtilityCategoryLifecycle lifecycle = PungentUtilityCategoryLifecycle.Visible;

            public bool overrideDisplayName;
            public string displayName;

            public bool overrideTint;
            public Color tint = Color.white;
        }

        [SerializeField] private List<CategoryOverride> _overrides = new List<CategoryOverride>();

        public static event Action Changed;

        public IReadOnlyList<CategoryOverride> Overrides
        {
            get
            {
                NormalizeInMemory();
                return _overrides;
            }
        }

        public bool HasOverride(string categoryId)
        {
            CategoryOverride item = FindInternal(categoryId);
            return item != null && HasAnyOverrideEnabled(item);
        }

        public bool HasTintOverride(string categoryId)
        {
            CategoryOverride item = FindInternal(categoryId);
            return item != null && item.overrideTint;
        }

        public bool HasDisplayNameOverride(string categoryId)
        {
            CategoryOverride item = FindInternal(categoryId);
            return item != null && item.overrideDisplayName;
        }

        public bool HasProjectLocalRecord(string categoryId)
        {
            CategoryOverride item = FindInternal(categoryId);
            return item != null && item.projectLocal;
        }

        public PungentUtilityCategoryLifecycle GetLifecycle(string categoryId)
        {
            CategoryOverride item = FindInternal(categoryId);
            return item == null ? PungentUtilityCategoryLifecycle.Visible : item.lifecycle;
        }

        public bool IsVisible(string categoryId)
        {
            return GetLifecycle(categoryId) == PungentUtilityCategoryLifecycle.Visible;
        }

        public string GetDisplayName(string categoryId, string fallback)
        {
            CategoryOverride item = FindInternal(categoryId);
            if (item != null && item.overrideDisplayName && !string.IsNullOrWhiteSpace(item.displayName))
                return item.displayName.Trim();

            return string.IsNullOrWhiteSpace(fallback) ? PungentUtilityCategories.Normalize(categoryId) : fallback;
        }

        public Color GetTint(string categoryId, Color fallback)
        {
            CategoryOverride item = FindInternal(categoryId);
            return item != null && item.overrideTint ? item.tint : fallback;
        }

        public CategoryOverride CreateEditableCopy(string categoryId, string fallbackDisplayName, Color fallbackTint)
        {
            string normalized = PungentUtilityCategories.Normalize(categoryId);
            CategoryOverride existing = FindInternal(normalized);
            CategoryOverride copy = existing != null
                ? Clone(existing)
                : new CategoryOverride { categoryId = normalized };

            copy.categoryId = normalized;
            if (!PungentUtilityCategories.IsFactoryCategory(normalized))
                copy.projectLocal = true;
            if (string.IsNullOrWhiteSpace(copy.displayName))
                copy.displayName = fallbackDisplayName;
            if (!copy.overrideTint)
                copy.tint = fallbackTint;

            NormalizeOverride(copy);
            return copy;
        }

        public void SetDisplayNameOverride(string categoryId, string displayName)
        {
            CategoryOverride item = CreateEditableCopy(
                categoryId,
                PungentUtilityCategories.GetFactoryDisplayName(categoryId),
                PungentUtilityCategories.GetFactoryTint(categoryId));

            item.overrideDisplayName = !string.IsNullOrWhiteSpace(displayName);
            item.displayName = displayName ?? string.Empty;
            SetOverride(item);
        }

        public void SetTintOverride(string categoryId, Color tint)
        {
            CategoryOverride item = CreateEditableCopy(
                categoryId,
                PungentUtilityCategories.GetFactoryDisplayName(categoryId),
                PungentUtilityCategories.GetFactoryTint(categoryId));

            item.overrideTint = true;
            item.tint = tint;
            SetOverride(item);
        }

        public void ClearDisplayNameOverride(string categoryId)
        {
            CategoryOverride item = FindInternal(categoryId);
            if (item == null)
                return;

            item = Clone(item);
            item.overrideDisplayName = false;
            item.displayName = PungentUtilityCategories.GetFactoryDisplayName(categoryId);
            SetOverride(item);
        }

        public void ClearTintOverride(string categoryId)
        {
            CategoryOverride item = FindInternal(categoryId);
            if (item == null)
                return;

            item = Clone(item);
            item.overrideTint = false;
            item.tint = PungentUtilityCategories.GetFactoryTint(categoryId);
            SetOverride(item);
        }

        public void SetOverride(CategoryOverride categoryOverride)
        {
            if (categoryOverride == null || string.IsNullOrWhiteSpace(categoryOverride.categoryId))
                return;

            NormalizeOverride(categoryOverride);

            if (!HasAnyOverrideEnabled(categoryOverride))
            {
                RemoveOverride(categoryOverride.categoryId);
                return;
            }

            int index = _overrides.FindIndex(item => string.Equals(item.categoryId, categoryOverride.categoryId, StringComparison.OrdinalIgnoreCase));
            CategoryOverride copy = Clone(categoryOverride);
            if (index >= 0)
                _overrides[index] = copy;
            else
                _overrides.Add(copy);

            SaveStore();
        }

        public void RemoveOverride(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
                return;

            string normalized = PungentUtilityCategories.Normalize(categoryId);
            NormalizeInMemory();
            _overrides.RemoveAll(item => string.Equals(item.categoryId, normalized, StringComparison.OrdinalIgnoreCase));
            SaveStore();
        }

        public void CreateProjectLocalCategory(string categoryId, string displayName, Color tint)
        {
            string normalized = PungentUtilityCategories.Normalize(categoryId);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            CategoryOverride item = CreateEditableCopy(normalized, displayName, tint);
            item.projectLocal = true;
            item.lifecycle = PungentUtilityCategoryLifecycle.Visible;
            item.overrideDisplayName = !string.IsNullOrWhiteSpace(displayName);
            item.displayName = displayName ?? string.Empty;
            item.overrideTint = true;
            item.tint = tint;
            SetOverride(item);
        }

        public void SaveStore()
        {
            NormalizeInMemory();
            _overrides.Sort((a, b) => PungentUtilityCategories.SortKey(a.categoryId).CompareTo(PungentUtilityCategories.SortKey(b.categoryId)));
            Save(true);
            Changed?.Invoke();
        }

        private CategoryOverride FindInternal(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
                return null;

            NormalizeInMemory();
            string normalized = PungentUtilityCategories.Normalize(categoryId);
            return _overrides.FirstOrDefault(item => string.Equals(item.categoryId, normalized, StringComparison.OrdinalIgnoreCase));
        }

        private void NormalizeInMemory()
        {
            if (_overrides == null)
                _overrides = new List<CategoryOverride>();

            _overrides.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.categoryId));

            for (int i = 0; i < _overrides.Count; i++)
                NormalizeOverride(_overrides[i]);

            for (int i = _overrides.Count - 1; i >= 0; i--)
            {
                string id = _overrides[i].categoryId;
                int first = _overrides.FindIndex(item => string.Equals(item.categoryId, id, StringComparison.OrdinalIgnoreCase));
                if (first >= 0 && first != i)
                    _overrides.RemoveAt(i);
            }
        }

        private static void NormalizeOverride(CategoryOverride categoryOverride)
        {
            if (categoryOverride == null)
                return;

            categoryOverride.categoryId = PungentUtilityCategories.Normalize(categoryOverride.categoryId);
            categoryOverride.displayName = categoryOverride.displayName ?? string.Empty;
            if (!Enum.IsDefined(typeof(PungentUtilityCategoryLifecycle), categoryOverride.lifecycle))
                categoryOverride.lifecycle = PungentUtilityCategoryLifecycle.Visible;
            if (!PungentUtilityCategories.IsFactoryCategory(categoryOverride.categoryId))
                categoryOverride.projectLocal = true;
        }

        private static CategoryOverride Clone(CategoryOverride source)
        {
            if (source == null)
                return null;

            return new CategoryOverride
            {
                categoryId = source.categoryId,
                projectLocal = source.projectLocal,
                lifecycle = source.lifecycle,
                overrideDisplayName = source.overrideDisplayName,
                displayName = source.displayName,
                overrideTint = source.overrideTint,
                tint = source.tint
            };
        }

        private static bool HasAnyOverrideEnabled(CategoryOverride categoryOverride)
        {
            return categoryOverride != null &&
                   (categoryOverride.projectLocal ||
                    categoryOverride.lifecycle != PungentUtilityCategoryLifecycle.Visible ||
                    categoryOverride.overrideDisplayName ||
                    categoryOverride.overrideTint);
        }
    }
#endif
}
