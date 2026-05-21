using System;
using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.Checklists
{
    [Serializable]
    public sealed class PungentChecklistRunState
    {
        public string runId = string.Empty;
        public string checklistId = string.Empty;
        public string listKind = PungentChecklistListKinds.QualityGate;
        public string contextProviderId = string.Empty;
        public string contextItemId = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public List<PungentChecklistItemRunState> items = new List<PungentChecklistItemRunState>();

        public void NormalizeInPlace()
        {
            runId = string.IsNullOrWhiteSpace(runId) ? checklistId : runId.Trim();
            checklistId = PungentAuthoringId.Normalize(checklistId);
            listKind = PungentChecklistListKinds.Normalize(listKind);
            contextProviderId = contextProviderId == null ? string.Empty : contextProviderId.Trim();
            contextItemId = PungentAuthoringId.Normalize(contextItemId);
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? DateTime.UtcNow.ToString("o") : createdUtc.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? createdUtc : updatedUtc.Trim();
            items = items ?? new List<PungentChecklistItemRunState>();
            for (int i = 0; i < items.Count; i++)
                items[i]?.NormalizeInPlace();
            items.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.itemId));
        }
    }

    [Serializable]
    public sealed class PungentChecklistItemRunState
    {
        public string itemId = string.Empty;
        public string stateId = string.Empty;
        public string comment = string.Empty;
        public string updatedUtc = string.Empty;
        public string updatedBy = string.Empty;

        public void NormalizeInPlace()
        {
            itemId = PungentAuthoringId.Normalize(itemId);
            stateId = PungentChecklistProfiles.NormalizeStateId(stateId);
            comment = comment == null ? string.Empty : comment.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? DateTime.UtcNow.ToString("o") : updatedUtc.Trim();
            updatedBy = updatedBy == null ? string.Empty : updatedBy.Trim();
        }
    }

    public interface IPungentChecklistRunStateStore
    {
        bool TryLoad(string runId, out PungentChecklistRunState run);
        bool Save(PungentChecklistRunState run, out string error);
        bool Delete(string runId, out string error);
    }
}
