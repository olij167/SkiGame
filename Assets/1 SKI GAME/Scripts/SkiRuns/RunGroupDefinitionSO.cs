using System;
using System.Collections.Generic;
using SkiGame.Runs;
using UnityEngine;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Run Group Definition", fileName = "RunGroup_")]
    public sealed class RunGroupDefinitionSO : ScriptableObject
    {
        public enum BuildMode
        {
            ExplicitIds = 0,
            FilterFromScene = 10,
        }

        [Header("Identity")]
        public string id;
        public string title;

        [TextArea] public string description;

        [Header("Build Mode")]
        public BuildMode buildMode = BuildMode.FilterFromScene;

        [Header("Filter (used when BuildMode = FilterFromScene)")]
        public SkiRunDifficulty difficultyFilter = SkiRunDifficulty.Green;

        [Tooltip("If enabled, difficultyFilter is ignored (includes all difficulties).")]
        public bool includeAllDifficulties = true;

        [Tooltip("If set, only runs whose RunName contains this text are included (case-insensitive).")]
        public string nameContains;

        [Header("Resolved Run IDs (used at runtime)")]
        [SerializeField] private List<string> runIds = new List<string>();

        public IReadOnlyList<string> RunIds => runIds;
        public int Count => runIds != null ? runIds.Count : 0;

        public bool Contains(string runId)
        {
            if (string.IsNullOrEmpty(runId) || runIds == null) return false;
            return runIds.Contains(runId);
        }

        public void SetExplicitIds(IEnumerable<string> ids)
        {
            runIds ??= new List<string>();
            runIds.Clear();

            if (ids == null) return;

            foreach (var s in ids)
            {
                if (string.IsNullOrEmpty(s)) continue;
                if (!runIds.Contains(s))
                    runIds.Add(s);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild From Scene: SkiRunLine")]
        private void RebuildFromScene()
        {
#if UNITY_2023_1_OR_NEWER
            var runs = UnityEngine.Object.FindObjectsByType<SkiRunLine>(FindObjectsSortMode.None);
#else
            var runs = UnityEngine.Object.FindObjectsOfType<SkiRunLine>();
#endif
            if (runs == null || runs.Length == 0)
            {
                Debug.LogWarning($"[{name}] No SkiRunLine found in the active scene. Cannot rebuild Run group.");
                return;
            }

            runIds ??= new List<string>();
            runIds.Clear();

            string n = string.IsNullOrWhiteSpace(nameContains) ? null : nameContains.Trim();

            bool Has(string hay, string needle)
            {
                if (string.IsNullOrEmpty(needle)) return true;
                if (string.IsNullOrEmpty(hay)) return false;
                return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            for (int i = 0; i < runs.Length; i++)
            {
                var r = runs[i];
                if (r == null) continue;

                if (!includeAllDifficulties && r.Difficulty != difficultyFilter)
                    continue;

                if (!Has(r.RunName, n))
                    continue;

                if (!string.IsNullOrEmpty(r.RunId) && !runIds.Contains(r.RunId))
                    runIds.Add(r.RunId);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[{name}] Rebuilt Run group. Count={runIds.Count}");
        }
#endif
    }
}
