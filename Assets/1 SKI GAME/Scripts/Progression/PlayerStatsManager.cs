using System;
using UnityEngine;

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
    }
}
