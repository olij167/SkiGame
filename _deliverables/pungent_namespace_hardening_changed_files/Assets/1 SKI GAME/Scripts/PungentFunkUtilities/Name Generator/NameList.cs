using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Content
{
    [CreateAssetMenu(menuName = "PungentFunk Utilities/Content Generation/Name List", fileName = "New Name List")]
    public class NameList : ScriptableObject
    {
        [Tooltip("Optional display name used by generator windows. Falls back to the asset name when empty.")]
        public string displayName;

        [Tooltip("Category or grouping label used by utility windows. Examples: First Names, Surnames, Places, Items.")]
        public string category;

        [Tooltip("Optional tags for filtering and discovery in generator windows.")]
        public List<string> tags = new List<string>();

        [Tooltip("The raw names or words in this list.")]
        public List<string> nameList = new List<string>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public int Count => nameList == null ? 0 : nameList.Count;

        public bool HasNames => Count > 0;

        public string GetRandomName()
        {
            if (nameList == null || nameList.Count == 0)
                return string.Empty;

            return nameList[UnityEngine.Random.Range(0, nameList.Count)] ?? string.Empty;
        }

        public string GetRandomName(System.Random random)
        {
            if (nameList == null || nameList.Count == 0)
                return string.Empty;

            if (random == null)
                return GetRandomName();

            return nameList[random.Next(0, nameList.Count)] ?? string.Empty;
        }

        public int NormalizeNames(bool trimWhitespace = true, bool removeEmpty = true, bool sort = false)
        {
            if (nameList == null)
                nameList = new List<string>();

            int changed = 0;
            for (int i = nameList.Count - 1; i >= 0; i--)
            {
                string before = nameList[i] ?? string.Empty;
                string after = trimWhitespace ? before.Trim() : before;

                if (removeEmpty && string.IsNullOrWhiteSpace(after))
                {
                    nameList.RemoveAt(i);
                    changed++;
                    continue;
                }

                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    nameList[i] = after;
                    changed++;
                }
            }

            if (sort)
            {
                nameList.Sort(StringComparer.CurrentCultureIgnoreCase);
                changed++;
            }

            return changed;
        }

        public int RemoveDuplicateNames(bool caseInsensitive = true, bool trimBeforeCompare = true)
        {
            if (nameList == null)
            {
                nameList = new List<string>();
                return 0;
            }

            var seen = new HashSet<string>(caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            int removed = 0;

            for (int i = nameList.Count - 1; i >= 0; i--)
            {
                string value = nameList[i] ?? string.Empty;
                string key = trimBeforeCompare ? value.Trim() : value;

                if (seen.Contains(key))
                {
                    nameList.RemoveAt(i);
                    removed++;
                }
                else
                {
                    seen.Add(key);
                }
            }

            return removed;
        }

        public void AddIfMissing(string value, bool caseInsensitive = true)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (nameList == null)
                nameList = new List<string>();

            string trimmed = value.Trim();
            StringComparison comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            for (int i = 0; i < nameList.Count; i++)
            {
                if (string.Equals(nameList[i], trimmed, comparison))
                    return;
            }

            nameList.Add(trimmed);
        }
    }

}