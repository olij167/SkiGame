using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Content
{
    [CreateAssetMenu(menuName = "PungentFunk Utilities/Content Generation/Mad-Lib Name List", fileName = "New Mad-Lib Name List")]
    public class MadLibNameList : ScriptableObject
    {
        [Tooltip("Generated results. This remains for compatibility with existing assets and workflows.")]
        public List<string> nameList = new List<string>();

        [Tooltip("First half / prefix options. These are never mutated by generation.")]
        public List<string> prefixList = new List<string>();

        [Tooltip("Second half / suffix options. Uppercase suffixes are treated as separate words by default.")]
        public List<string> suffixList = new List<string>();

        [Header("Generation Defaults")]
        public bool capitalizePrefix = true;
        public bool spaceBeforeUppercaseSuffix = true;
        public bool trimParts = true;
        public bool avoidDuplicateGeneratedNames = true;

        public string GenerateName()
        {
            return GenerateName(null);
        }

        public string GenerateName(System.Random random)
        {
            if (prefixList == null || prefixList.Count == 0 || suffixList == null || suffixList.Count == 0)
                return string.Empty;

            int p = random != null ? random.Next(0, prefixList.Count) : UnityEngine.Random.Range(0, prefixList.Count);
            int s = random != null ? random.Next(0, suffixList.Count) : UnityEngine.Random.Range(0, suffixList.Count);

            return CombineParts(prefixList[p], suffixList[s], capitalizePrefix, spaceBeforeUppercaseSuffix, trimParts);
        }

        public void GenerateNamesList(int numOfNames)
        {
            GenerateNamesList(numOfNames, null, avoidDuplicateGeneratedNames);
        }

        public void GenerateNamesList(int numOfNames, System.Random random, bool avoidDuplicates)
        {
            if (nameList == null)
                nameList = new List<string>();

            int count = Mathf.Max(0, numOfNames);
            int attempts = 0;
            int maxAttempts = Mathf.Max(count * 20, 100);
            var seen = new HashSet<string>(nameList, StringComparer.OrdinalIgnoreCase);

            int generatedCount = 0;
            while (generatedCount < count && attempts < maxAttempts)
            {
                attempts++;
                string generated = GenerateName(random);
                if (string.IsNullOrWhiteSpace(generated))
                    break;

                if (avoidDuplicates && seen.Contains(generated))
                    continue;

                nameList.Add(generated);
                seen.Add(generated);
                generatedCount++;
            }
        }

        public int NormalizeAll(bool sort = false)
        {
            int changed = 0;
            changed += NormalizeList(nameList, sort);
            changed += NormalizeList(prefixList, sort);
            changed += NormalizeList(suffixList, sort);
            return changed;
        }

        public int RemoveDuplicateNames()
        {
            int removed = 0;
            removed += RemoveDuplicates(nameList);
            removed += RemoveDuplicates(prefixList);
            removed += RemoveDuplicates(suffixList);
            return removed;
        }

        public static string CombineParts(string prefix, string suffix, bool capitalizePrefix = true, bool spaceBeforeUppercaseSuffix = true, bool trimParts = true)
        {
            string left = prefix ?? string.Empty;
            string right = suffix ?? string.Empty;

            if (trimParts)
            {
                left = left.Trim();
                right = right.Trim();
            }

            if (capitalizePrefix)
                left = FirstLetterCapital(left);

            if (string.IsNullOrEmpty(left))
                return right;
            if (string.IsNullOrEmpty(right))
                return left;

            bool suffixStartsUppercase = right.Length > 0 && char.IsUpper(right[0]);
            string separator = spaceBeforeUppercaseSuffix && suffixStartsUppercase ? " " : string.Empty;
            return left + separator + right;
        }

        private static string FirstLetterCapital(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length == 1)
                return char.ToUpperInvariant(value[0]).ToString();

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static int NormalizeList(List<string> list, bool sort)
        {
            if (list == null)
                return 0;

            int changed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string before = list[i] ?? string.Empty;
                string after = before.Trim();
                if (string.IsNullOrWhiteSpace(after))
                {
                    list.RemoveAt(i);
                    changed++;
                    continue;
                }

                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    list[i] = after;
                    changed++;
                }
            }

            if (sort)
            {
                list.Sort(StringComparer.CurrentCultureIgnoreCase);
                changed++;
            }

            return changed;
        }

        private static int RemoveDuplicates(List<string> list)
        {
            if (list == null)
                return 0;

            int removed = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string key = (list[i] ?? string.Empty).Trim();
                if (seen.Contains(key))
                {
                    list.RemoveAt(i);
                    removed++;
                }
                else
                {
                    seen.Add(key);
                }
            }

            return removed;
        }
    }

}