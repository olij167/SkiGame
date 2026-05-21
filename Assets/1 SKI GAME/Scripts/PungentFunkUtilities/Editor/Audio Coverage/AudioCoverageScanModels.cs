namespace PungentFunk.Utilities.Editor.Audio
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal enum AudioCoverageCueStatus
    {
        Valid,
        Warning,
        Invalid,
        Missing
    }

    internal enum AudioCoverageResultSeverity
    {
        Info,
        Warning,
        Error
    }

    internal sealed class AudioCoverageCueAuditRow
    {
        public string CueName;
        public AudioCoverageCueStatus Status;
        public bool HasCoverage;
        public bool IsIgnored;
        public int BindingCount;
        public int ScriptReferenceCount;
        public readonly List<int> EntryIndices = new List<int>();
        public readonly List<string> Issues = new List<string>();
    }

    internal sealed class AudioCoverageCatalogEntryInfo
    {
        public string CueName;
        public int Index;
        public bool HasClipProperty;
        public int ClipSlots;
        public int NonNullClipSlots;
        public bool HasVolume;
        public float Volume;
        public bool HasPitchRange;
        public Vector2 PitchRange;
        public bool HasSpatialBlend;
        public float SpatialBlend;
        public bool HasMinDistance;
        public float MinDistance;
        public bool HasMaxDistance;
        public float MaxDistance;
        public bool HasMixerGroup;
        public bool MixerGroupAssigned;
    }

    internal sealed class AudioCoverageScriptReferenceHit
    {
        public string Path;
        public string ScriptName;
    }

    internal sealed class AudioCoverageContextResult
    {
        public AudioCoverageResultSeverity Severity;
        public string Category;
        public string Message;
        public Object Context;
        public string Path;
    }

    internal sealed class AudioCoverageContextScanOptions
    {
        public bool SelectionOnly;
        public bool IncludeSceneReferences;
        public bool IncludeTerrainProfiles;
        public bool IncludeInteractionMatrices;
        public bool IncludeComponentExpectations;
        public bool IncludeRequiredFields;
    }
#endif
}

