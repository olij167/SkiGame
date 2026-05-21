using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public enum PungentSpatialProfile
    {
        Generic,
        AIPath,
        CameraPath,
        EventSequencePath,
        PlacementPath,
        RoadPath,
        TerrainModifierPath,
        MapPath,
        RuntimeUIPath,
        PlacementArea,
        TerrainPaintArea,
        WeatherZone,
        TimeZone,
        SeasonZone,
        MapRegion,
        SpawnArea,
        TriggerArea,
        NavigationArea,
        AudioArea,
        LightingArea,
        GameplayRuleArea,
        BiomeArea
    }

    [Serializable]
    public struct PungentSpatialTag : IEquatable<PungentSpatialTag>
    {
        [SerializeField] private string value;

        public PungentSpatialTag(string value)
        {
            this.value = Normalize(value);
        }

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public bool Equals(PungentSpatialTag other)
        {
            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is PungentSpatialTag other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static string Normalize(string tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? string.Empty : tag.Trim();
        }
    }

    [Serializable]
    public struct PungentSpatialLayer : IEquatable<PungentSpatialLayer>
    {
        [SerializeField] private string name;
        [SerializeField] private int priority;

        public PungentSpatialLayer(string name, int priority = 0)
        {
            this.name = Normalize(name);
            this.priority = priority;
        }

        public string Name => name ?? string.Empty;
        public int Priority => priority;
        public bool IsEmpty => string.IsNullOrWhiteSpace(Name);

        public bool Equals(PungentSpatialLayer other)
        {
            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is PungentSpatialLayer other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }

        public override string ToString()
        {
            return Name;
        }

        public static string Normalize(string layer)
        {
            return string.IsNullOrWhiteSpace(layer) ? string.Empty : layer.Trim();
        }
    }

    [Serializable]
    public sealed class PungentSpatialObjectMetadata
    {
        public const int CurrentVersion = 1;

        [Tooltip("Stable generic identifier used by scene authoring registries, runtime queries, outputs, and optional bridges.")]
        public string stableId;

        [Tooltip("Human-readable name shown in generic spatial authoring tools.")]
        public string displayName;

        [TextArea(2, 4)]
        public string description;

        [Tooltip("Reusable semantic role for generic consumers. Keep project-specific meaning in project adapters.")]
        public PungentSpatialProfile semanticProfile = PungentSpatialProfile.Generic;

        [Tooltip("Lightweight generic layer/category used by runtime queries and authoring filters.")]
        public PungentSpatialLayer layer = new PungentSpatialLayer("Default");

        [Tooltip("Generic tags used by runtime queries and optional export/bridge packages.")]
        public List<PungentSpatialTag> tags = new List<PungentSpatialTag>();

        [Tooltip("Forward-compatible metadata schema version.")]
        public int version = CurrentVersion;

        [Tooltip("Whether editor tools should show this object in authoring lists and previews.")]
        public bool editorVisible = true;

        [Tooltip("Whether runtime query services should consider this object.")]
        public bool runtimeEnabled = true;

        [Tooltip("Optional scene/tool color used by generic previews.")]
        public Color color = new Color(0.25f, 0.85f, 1f, 0.85f);

        [Tooltip("Optional icon key for future UI/icon-provider bridges.")]
        public string iconKey;

        [TextArea(2, 5)]
        public string notes;

        [Tooltip("Optional documentation/help links. These are plain strings so runtime data stays package-safe.")]
        public List<string> documentationLinks = new List<string>();

        [NonSerialized] private readonly List<string> tagNameCache = new List<string>();

        public string StableId
        {
            get
            {
                EnsureStableId();
                return stableId;
            }
        }

        public string DisplayNameOrFallback(string fallback)
        {
            return string.IsNullOrWhiteSpace(displayName) ? (fallback ?? string.Empty) : displayName.Trim();
        }

        public IReadOnlyList<string> TagNames
        {
            get
            {
                tagNameCache.Clear();
                if (tags == null)
                    return tagNameCache;

                for (int i = 0; i < tags.Count; i++)
                {
                    string tag = tags[i].Value;
                    if (!string.IsNullOrWhiteSpace(tag))
                        tagNameCache.Add(tag);
                }

                return tagNameCache;
            }
        }

        public void EnsureStableId()
        {
            if (!string.IsNullOrWhiteSpace(stableId))
                return;

            stableId = Guid.NewGuid().ToString("N");
        }

        public bool HasTag(string tag)
        {
            string normalized = PungentSpatialTag.Normalize(tag);
            if (string.IsNullOrWhiteSpace(normalized) || tags == null)
                return false;

            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i].Value, normalized, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public bool MatchesLayerOrProfile(string layerOrProfile)
        {
            if (string.IsNullOrWhiteSpace(layerOrProfile))
                return true;

            string normalized = layerOrProfile.Trim();
            if (string.Equals(layer.Name, normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(semanticProfile.ToString(), normalized, StringComparison.OrdinalIgnoreCase);
        }

        public void Normalize(string fallbackName)
        {
            EnsureStableId();
            version = Mathf.Max(1, version);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = fallbackName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(layer.Name))
                layer = new PungentSpatialLayer("Default", layer.Priority);

            if (tags == null)
                tags = new List<PungentSpatialTag>();
            for (int i = tags.Count - 1; i >= 0; i--)
            {
                if (tags[i].IsEmpty)
                    tags.RemoveAt(i);
            }

            if (documentationLinks == null)
                documentationLinks = new List<string>();
        }
    }

    public interface IPungentSpatialObjectMetadataProvider
    {
        bool TryGetSpatialMetadata(out PungentSpatialObjectMetadata metadata);
    }
}
