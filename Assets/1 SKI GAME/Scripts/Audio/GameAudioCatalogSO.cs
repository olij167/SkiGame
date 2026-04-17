using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SkiGame.Audio
{
    [CreateAssetMenu(fileName = "GameAudioCatalog", menuName = "SkiGame/Audio/Game Audio Catalog")]
    public sealed class GameAudioCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class CueDefinition
        {
            public GameAudioCueId id = GameAudioCueId.None;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            public Vector2 pitchRange = Vector2.one;
            [Range(0f, 1f)] public float spatialBlend = 1f;
            [Min(0f)] public float minDistance = 5f;
            [Min(0f)] public float maxDistance = 40f;
            [Min(0f)] public float cooldownSeconds = 0f;
            public AudioMixerGroup mixerGroup;
        }

        [SerializeField] private List<CueDefinition> cues = new List<CueDefinition>();

        private Dictionary<GameAudioCueId, CueDefinition> _lookup;

        public IReadOnlyList<CueDefinition> Cues => cues;

        public bool TryGet(GameAudioCueId id, out CueDefinition cue)
        {
            if (_lookup == null || _lookup.Count != CountDistinctConfiguredIds())
                RebuildLookup();

            return _lookup.TryGetValue(id, out cue);
        }

        public int FindIndex(GameAudioCueId id)
        {
            if (cues == null)
                return -1;

            for (int i = 0; i < cues.Count; i++)
            {
                CueDefinition cue = cues[i];
                if (cue != null && cue.id == id)
                    return i;
            }

            return -1;
        }

        public List<int> FindIndices(GameAudioCueId id)
        {
            List<int> indices = new List<int>();

            if (cues == null)
                return indices;

            for (int i = 0; i < cues.Count; i++)
            {
                CueDefinition cue = cues[i];
                if (cue != null && cue.id == id)
                    indices.Add(i);
            }

            return indices;
        }

        public bool Contains(GameAudioCueId id)
        {
            return FindIndex(id) >= 0;
        }

        public CueDefinition AddCue(GameAudioCueId id)
        {
            if (cues == null)
                cues = new List<CueDefinition>();

            CueDefinition cue = CreateDefaultCue(id);
            cues.Add(cue);
            RebuildLookup();
            return cue;
        }

        public int AddMissingCue(GameAudioCueId id)
        {
            if (Contains(id))
                return FindIndex(id);

            AddCue(id);
            return cues.Count - 1;
        }

        public int AddAllMissingCues()
        {
            int added = 0;

            foreach (GameAudioCueId cueId in Enum.GetValues(typeof(GameAudioCueId)))
            {
                if (cueId == GameAudioCueId.None)
                    continue;

                if (Contains(cueId))
                    continue;

                AddCue(cueId);
                added++;
            }

            SortByEnumOrder();
            RebuildLookup();
            return added;
        }

        public int RemoveDuplicateIds(bool keepFirst = true)
        {
            if (cues == null || cues.Count <= 1)
                return 0;

            HashSet<GameAudioCueId> seen = new HashSet<GameAudioCueId>();
            int removed = 0;

            if (keepFirst)
            {
                for (int i = cues.Count - 1; i >= 0; i--)
                {
                    CueDefinition cue = cues[i];
                    if (cue == null || cue.id == GameAudioCueId.None)
                        continue;

                    if (!seen.Add(cue.id))
                    {
                        cues.RemoveAt(i);
                        removed++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < cues.Count; i++)
                {
                    CueDefinition cue = cues[i];
                    if (cue == null || cue.id == GameAudioCueId.None)
                        continue;

                    if (!seen.Add(cue.id))
                    {
                        cues.RemoveAt(i);
                        i--;
                        removed++;
                    }
                }
            }

            RebuildLookup();
            return removed;
        }

        public void SortByEnumOrder()
        {
            if (cues == null)
                return;

            cues.Sort((a, b) =>
            {
                if (ReferenceEquals(a, b))
                    return 0;
                if (a == null)
                    return 1;
                if (b == null)
                    return -1;

                return ((int)a.id).CompareTo((int)b.id);
            });

            RebuildLookup();
        }

        public int NormalizeAllDefaults()
        {
            if (cues == null)
                return 0;

            int changed = 0;

            for (int i = 0; i < cues.Count; i++)
            {
                CueDefinition cue = cues[i];
                if (cue == null || cue.id == GameAudioCueId.None)
                    continue;

                ApplySuggestedDefaults(cue, preserveAssignedClips: true, preserveMixerGroup: true);
                changed++;
            }

            RebuildLookup();
            return changed;
        }

        public static CueDefinition CreateDefaultCue(GameAudioCueId id)
        {
            CueDefinition cue = new CueDefinition
            {
                id = id,
                clips = Array.Empty<AudioClip>()
            };

            ApplySuggestedDefaults(cue, preserveAssignedClips: true, preserveMixerGroup: true);
            return cue;
        }

        public static void ApplySuggestedDefaults(CueDefinition cue, bool preserveAssignedClips, bool preserveMixerGroup)
        {
            if (cue == null)
                return;

            AudioClip[] clips = preserveAssignedClips ? cue.clips : Array.Empty<AudioClip>();
            AudioMixerGroup mixerGroup = preserveMixerGroup ? cue.mixerGroup : null;

            cue.volume = 1f;
            cue.cooldownSeconds = 0f;

            if (IsUiCue(cue.id))
            {
                cue.pitchRange = Vector2.one;
                cue.spatialBlend = 0f;
                cue.minDistance = 1f;
                cue.maxDistance = 20f;
            }
            else
            {
                cue.pitchRange = new Vector2(0.98f, 1.02f);
                cue.spatialBlend = 1f;
                cue.minDistance = 5f;
                cue.maxDistance = 40f;
            }

            if (cue.id == GameAudioCueId.RaceCountdownTick)
                cue.cooldownSeconds = 0.03f;

            cue.clips = clips ?? Array.Empty<AudioClip>();
            cue.mixerGroup = mixerGroup;
        }

        public static bool IsUiCue(GameAudioCueId id)
        {
            int value = (int)id;
            return value >= 10 && value < 100;
        }

        public static bool IsWorldCue(GameAudioCueId id)
        {
            return id != GameAudioCueId.None && !IsUiCue(id);
        }

        private int CountDistinctConfiguredIds()
        {
            if (cues == null)
                return 0;

            HashSet<GameAudioCueId> ids = new HashSet<GameAudioCueId>();
            for (int i = 0; i < cues.Count; i++)
            {
                CueDefinition cue = cues[i];
                if (cue == null || cue.id == GameAudioCueId.None)
                    continue;

                ids.Add(cue.id);
            }

            return ids.Count;
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<GameAudioCueId, CueDefinition>();

            if (cues == null)
                return;

            for (int i = 0; i < cues.Count; i++)
            {
                CueDefinition cue = cues[i];
                if (cue == null || cue.id == GameAudioCueId.None)
                    continue;

                _lookup[cue.id] = cue;
            }
        }
    }
}