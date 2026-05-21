using System;
using System.Collections.Generic;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    [Serializable]
    public sealed class PungentBacklogSeedItem
    {
        public string stableKey = string.Empty;
        public string title = string.Empty;
        public string description = string.Empty;
        public string area = string.Empty;
        public string linkedUtilityId = string.Empty;
        public string futureUtilityId = string.Empty;
        public string suggestedFutureUtilityName = string.Empty;
        public PungentNoteKind kind = PungentNoteKind.FutureFeature;
        public PungentNoteStatus status = PungentNoteStatus.ToDo;
        public PungentNotePriority priority = PungentNotePriority.NiceToHave;
        public List<string> tags = new List<string>();
        public bool createFutureUtilityRecord;
    }

    public sealed class PungentBacklogSeedPreviewItem
    {
        public PungentBacklogSeedItem seed;
        public bool selected = true;
        public bool duplicate;
        public string skipReason = string.Empty;
    }

    public sealed class PungentBacklogSeedApplyResult
    {
        public int notesCreated;
        public int futureUtilitiesCreated;
        public int skipped;
        public List<string> noteIds = new List<string>();
        public List<string> futureUtilityIds = new List<string>();
    }
#endif
}
