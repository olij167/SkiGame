using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.POI
{
    [CreateAssetMenu(menuName = "SkiGame/POI/POI Group Definition", fileName = "POIGroup_")]
    public sealed class POIGroupDefinitionSO : ScriptableObject
    {
        public enum BuildMode
        {
            ExplicitIds = 0,
            FilterFromSceneRegistry = 10,
        }

        [Header("Identity")]
        public string id;
        public string title;

        [TextArea] public string description;

        [Header("Build Mode")]
        public BuildMode buildMode = BuildMode.FilterFromSceneRegistry;

        [Header("Filter (used when BuildMode = FilterFromSceneRegistry)")]
        public POIType typeFilter = POIType.Unknown;

        [Tooltip("If set, only POIs whose displayName contains this text are included (case-insensitive).")]
        public string nameContains;

        [Tooltip("If set, only POIs whose meta contains this text are included (case-insensitive). " +
                 "For SkiRuns, meta is currently Difficulty.ToString() (e.g., 'Green', 'Blue', etc.).")]
        public string metaContains;

        [Header("Resolved POI IDs (used at runtime)")]
        [SerializeField] private List<string> poiIds = new List<string>();

        public IReadOnlyList<string> POIIds => poiIds;
        public int Count => poiIds != null ? poiIds.Count : 0;

        public bool Contains(string poiId)
        {
            if (string.IsNullOrEmpty(poiId) || poiIds == null) return false;
            return poiIds.Contains(poiId);
        }

        public void SetExplicitIds(IEnumerable<string> ids)
        {
            poiIds ??= new List<string>();
            poiIds.Clear();

            if (ids == null) return;

            foreach (var s in ids)
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (!poiIds.Contains(s))
                    poiIds.Add(s);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild From Scene: PointOfInterestRegistry")]
        private void RebuildFromSceneRegistry()
        {
            var reg = UnityEngine.Object.FindFirstObjectByType<PointOfInterestRegistry>();
            if (reg == null)
            {
                Debug.LogWarning($"[{name}] No PointOfInterestRegistry found in the active scene. Cannot rebuild POI group.");
                return;
            }

            reg.Refresh();

            poiIds ??= new List<string>();
            poiIds.Clear();

            string n = string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();
            string m = string.IsNullOrWhiteSpace(metaContains) ? null : metaContains.Trim();

            bool Has(string hay, string needle)
            {
                if (string.IsNullOrEmpty(needle)) return true;
                if (string.IsNullOrEmpty(hay)) return false;
                return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            var list = reg.Current;
            for (int i = 0; i < list.Count; i++)
            {
                var poi = list[i];
                if (!poi.IsValid) continue;

                if (typeFilter != POIType.Unknown && poi.type != typeFilter)
                    continue;

                if (!Has(poi.displayName, n))
                    continue;

                if (!Has(poi.meta, m))
                    continue;

                if (!poiIds.Contains(poi.id))
                    poiIds.Add(poi.id);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[{name}] Rebuilt POI group. Count={poiIds.Count}");
        }
#endif
    }
}
