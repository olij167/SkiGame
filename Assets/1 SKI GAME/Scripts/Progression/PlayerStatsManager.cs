using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace SkiGame.Progression
{
    public sealed class PlayerStatsManager : MonoBehaviour
    {
        public static PlayerStatsManager Instance { get; private set; }

        [Header("Persistence")]
        [SerializeField] private bool loadOnAwake = true;
        [SerializeField] private bool saveOnApplicationQuit = true;

        [Tooltip("If true, the session stats (and session tasks) are cleared every time runtime starts.")]
        [SerializeField] private bool resetSessionOnAwake = true;

        [Tooltip("Optional override path. If empty, uses Application.persistentDataPath/player_stats_profile.json")]
        [SerializeField] private string customPath = "";
        public PlayerStatsProfile Profile { get; private set; }

        public event Action<PlayerStatsProfile> OnProfileLoaded;
        public event Action<PlayerStatsProfile> OnProfileSaved;

        public string ActivePath
        {
            get
            {
                if (!string.IsNullOrEmpty(customPath))
                    return customPath;
                return PlayerStatsStorage.GetDefaultPath();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (loadOnAwake)
                Load();

            // Important: do this after Load(), before other systems begin reading/writing.
            if (resetSessionOnAwake)
                ResetSession();
        }

        public void Load()
        {
            Profile = PlayerStatsStorage.LoadOrCreate(ActivePath);
            OnProfileLoaded?.Invoke(Profile);
        }

        public void Save()
        {
            if (Profile == null)
                Profile = PlayerStatsStorage.CreateNew();

            PlayerStatsStorage.Save(Profile, ActivePath);
            OnProfileSaved?.Invoke(Profile);
        }

        public void ResetSession()
        {
            EnsureProfile();
            Profile.ResetSession();
        }

        [ContextMenu("Reset All Stats")]
        public void ResetAll()
        {
            EnsureProfile();
            Profile.ResetAll();
        }

        private void EnsureProfile()
        {
            if (Profile == null)
                Profile = PlayerStatsStorage.CreateNew();
        }

        private void OnApplicationQuit()
        {
            if (saveOnApplicationQuit)
                Save();
        }

        [ContextMenu("Clear All Run History (Hard Delete)")]
        private void Context_ClearRunRecords()
        {
            if (Profile == null)
            {
                Debug.LogWarning("[PlayerStatsManager] No Profile to clear.");
                return;
            }

            int removedRecords = Profile.ClearAllRunHistory(
                clearVisits: true,
                clearCounts: true,
                clearSessionRefs: true,
                recalcLifetimeRunAggregates: true);

            Debug.Log($"[PlayerStatsManager] Cleared run history. Removed {removedRecords} run record(s).");

            Save();

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            AssetDatabase.SaveAssets();
#endif
        }

        [ContextMenu("Prune segments under 25% coverage")]
        private void Context_PruneRunSegmentsUnder25()
        {
            if (Profile == null)
            {
                Debug.LogWarning("[PlayerStatsManager] No Profile to prune.");
                return;
            }

            int beforeTiny = Profile.CountRunAttemptsWhere(t => t != null && t.timeSeconds < 5.0f);
            int removed = Profile.PruneRunSegmentsBelowCoverage(0.25f, removeEmptyRecords: true, removeNoCoverageSegments: true);
            int afterTiny = Profile.CountRunAttemptsWhere(t => t != null && t.timeSeconds < 5.0f);

            Profile.RecalculateLifetimeRunAggregatesFromRunRecords();
            Save();

            Debug.Log($"[PlayerStatsManager] Pruned {removed} attempt(s). <5s attempts: {beforeTiny} -> {afterTiny}");

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);          // mark component dirty
            EditorSceneManager.MarkSceneDirty(gameObject.scene); // ensures scene saves (if profile is scene-serialized)
            AssetDatabase.SaveAssets();            // harmless; saves assets if any
#endif
        }

    }
}
