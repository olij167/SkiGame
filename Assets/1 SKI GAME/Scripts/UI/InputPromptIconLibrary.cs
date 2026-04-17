using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.UI
{
    [CreateAssetMenu(fileName = "InputPromptIconLibrary", menuName = "SkiGame/UI/Input Prompt Icon Library")]
    public sealed class InputPromptIconLibrary : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string key;
            public Sprite sprite;
            public string textFallback;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, Entry> _lookup;

        public bool TryGetIcon(string key, out Sprite sprite, out string textFallback)
        {
            EnsureLookup();

            if (_lookup.TryGetValue(NormalizeKey(key), out var entry))
            {
                sprite = entry.sprite;
                textFallback = entry.textFallback;
                return sprite != null;
            }

            sprite = null;
            textFallback = string.Empty;
            return false;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(entries[i].key))
                    continue;

                _lookup[NormalizeKey(entries[i].key)] = entries[i];
            }
        }

        private static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : key.Trim().ToLowerInvariant();
        }
    }
}