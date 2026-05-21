using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Checklists
{
#if UNITY_EDITOR
    public sealed class PungentChecklistProgressSummary
    {
        public int total;
        public int unstarted;
        public int started;
        public int complete;
        public int problem;
        public readonly Dictionary<string, int> countsByStateId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int Count(string stateId)
        {
            countsByStateId.TryGetValue(PungentChecklistProfiles.NormalizeStateId(stateId), out int count);
            return count;
        }
    }

    public static class PungentChecklistUtilityStateService
    {
        public sealed class StateSnapshot
        {
            private readonly Dictionary<string, PungentChecklistStateOptionDefinition> _statesById = new Dictionary<string, PungentChecklistStateOptionDefinition>(StringComparer.OrdinalIgnoreCase);
            private readonly Func<string, PungentChecklistDefinition> _findChecklist;

            public StateSnapshot(PungentChecklistDefinition checklist, Func<string, PungentChecklistDefinition> findChecklist = null)
            {
                Checklist = checklist;
                _findChecklist = findChecklist;

                PungentChecklistStateProfileDefinition profile = PungentChecklistProfiles.ResolveProfile(checklist);
                StateOptions = profile.states ?? new List<PungentChecklistStateOptionDefinition>();
                ProfileLabel = string.IsNullOrWhiteSpace(profile.label) ? profile.profileId : profile.label;
                DefaultStateId = string.IsNullOrWhiteSpace(profile.defaultStateId) && StateOptions.Count > 0
                    ? StateOptions[0].stateId
                    : PungentChecklistProfiles.NormalizeStateId(profile.defaultStateId);
                IsQualityGate = string.Equals(profile.profileId, PungentChecklistProfileIds.QualityGate, StringComparison.OrdinalIgnoreCase);

                for (int i = 0; i < StateOptions.Count; i++)
                {
                    PungentChecklistStateOptionDefinition state = StateOptions[i];
                    if (state == null || string.IsNullOrWhiteSpace(state.stateId))
                        continue;
                    _statesById[state.stateId] = state;
                }
            }

            public PungentChecklistDefinition Checklist { get; }
            public IReadOnlyList<PungentChecklistStateOptionDefinition> StateOptions { get; }
            public string ProfileLabel { get; }
            public string DefaultStateId { get; }
            public bool IsQualityGate { get; }

            public string GetStateId(PungentChecklistItemDefinition item)
            {
                if (item == null)
                    return DefaultStateId;

                if (IsComputedFromChild(item))
                    return TranslateChildState(GetChildChecklistStateId(item, _findChecklist, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

                if (IsQualityGate)
                    return PungentChecklistProfiles.ToStateId(GetCompatibilityState(Checklist, item));

                string value = UtilityWindowPrefs.GetString(StateIdKey(Checklist, item), string.Empty);
                return string.IsNullOrWhiteSpace(value) ? DefaultStateId : ResolveState(value).stateId;
            }

            public PungentChecklistStateOptionDefinition ResolveState(string stateId)
            {
                string clean = PungentChecklistProfiles.NormalizeStateId(stateId);
                if (!string.IsNullOrWhiteSpace(clean) && _statesById.TryGetValue(clean, out PungentChecklistStateOptionDefinition state))
                    return state;
                return StateOptions.Count > 0
                    ? StateOptions[0]
                    : PungentChecklistProfiles.ResolveState(Checklist, PungentChecklistProfiles.StateUntested);
            }

            public bool CountsAsStarted(string stateId)
            {
                string clean = PungentChecklistProfiles.NormalizeStateId(stateId);
                return !string.IsNullOrWhiteSpace(clean) &&
                       !string.Equals(clean, DefaultStateId, StringComparison.OrdinalIgnoreCase);
            }

            public bool CountsAsProblem(string stateId)
            {
                PungentChecklistStateOptionDefinition state = ResolveState(stateId);
                return state.countsAsFailure ||
                       string.Equals(state.semanticRole, PungentChecklistStateRoles.Fail, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(state.semanticRole, PungentChecklistStateRoles.Blocked, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(state.semanticRole, PungentChecklistStateRoles.Rejected, StringComparison.OrdinalIgnoreCase);
            }

            private string TranslateChildState(string childStateId)
            {
                switch (PungentChecklistProfiles.ToCompatibilityState(childStateId))
                {
                    case PungentChecklistItemState.Pass:
                        return BestCompletionStateId();
                    case PungentChecklistItemState.Partial:
                        return BestPartialStateId();
                    case PungentChecklistItemState.Fail:
                        return BestFailureStateId();
                    default:
                        return DefaultStateId;
                }
            }

            private string BestCompletionStateId()
            {
                for (int i = 0; i < StateOptions.Count; i++)
                    if (StateOptions[i] != null && StateOptions[i].countsAsComplete)
                        return StateOptions[i].stateId;
                return DefaultStateId;
            }

            private string BestPartialStateId()
            {
                for (int i = 0; i < StateOptions.Count; i++)
                {
                    PungentChecklistStateOptionDefinition state = StateOptions[i];
                    if (state == null)
                        continue;
                    if (string.Equals(state.semanticRole, PungentChecklistStateRoles.Partial, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(state.semanticRole, PungentChecklistStateRoles.Active, StringComparison.OrdinalIgnoreCase))
                        return state.stateId;
                }
                return DefaultStateId;
            }

            private string BestFailureStateId()
            {
                for (int i = 0; i < StateOptions.Count; i++)
                    if (StateOptions[i] != null && StateOptions[i].countsAsFailure)
                        return StateOptions[i].stateId;
                return DefaultStateId;
            }
        }

        public const string PrefPrefix = "PungentFunkUtilities.ChecklistUtility.";

        public static string GetStateId(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, Func<string, PungentChecklistDefinition> findChecklist = null)
        {
            return GetStateId(checklist, item, findChecklist, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        public static string GetStateId(
            PungentChecklistDefinition checklist,
            PungentChecklistItemDefinition item,
            Func<string, PungentChecklistDefinition> findChecklist,
            HashSet<string> childStack)
        {
            if (item == null)
                return PungentChecklistProfiles.DefaultStateId(checklist);

            if (IsComputedFromChild(item))
                return TranslateCompatibilityChildState(checklist, GetChildChecklistStateId(item, findChecklist, childStack));

            if (PungentChecklistProfiles.IsQualityGate(checklist))
                return PungentChecklistProfiles.ToStateId(GetCompatibilityState(checklist, item));

            string value = UtilityWindowPrefs.GetString(StateIdKey(checklist, item), string.Empty);
            return string.IsNullOrWhiteSpace(value)
                ? PungentChecklistProfiles.DefaultStateId(checklist)
                : PungentChecklistProfiles.ResolveState(checklist, value).stateId;
        }

        public static PungentChecklistItemState GetCompatibilityState(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, Func<string, PungentChecklistDefinition> findChecklist = null)
        {
            if (item != null && IsComputedFromChild(item))
                return PungentChecklistProfiles.ToCompatibilityState(GetStateId(checklist, item, findChecklist));

            int value = UtilityWindowPrefs.GetInt(StateKey(checklist, item), (int)PungentChecklistItemState.Untested);
            return Enum.IsDefined(typeof(PungentChecklistItemState), value) ? (PungentChecklistItemState)value : PungentChecklistItemState.Untested;
        }

        public static void SetStateId(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item, string stateId)
        {
            if (checklist == null || item == null || string.IsNullOrWhiteSpace(item.id) || IsComputedFromChild(item))
                return;

            string clean = PungentChecklistProfiles.ResolveState(checklist, stateId).stateId;
            if (PungentChecklistProfiles.IsQualityGate(checklist))
                UtilityWindowPrefs.SetInt(StateKey(checklist, item), (int)PungentChecklistProfiles.ToCompatibilityState(clean));
            else
                UtilityWindowPrefs.SetString(StateIdKey(checklist, item), clean);
        }

        public static string GetComment(PungentChecklistDefinition checklist, string itemId)
        {
            return UtilityWindowPrefs.GetString(NoteKey(checklist, itemId), string.Empty);
        }

        public static void SetComment(PungentChecklistDefinition checklist, string itemId, string comment)
        {
            UtilityWindowPrefs.SetString(NoteKey(checklist, itemId), comment ?? string.Empty);
        }

        public static void ClearItemState(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item)
        {
            SetStateId(checklist, item, PungentChecklistProfiles.DefaultStateId(checklist));
        }

        public static PungentChecklistProgressSummary Count(PungentChecklistDefinition checklist, Func<string, PungentChecklistDefinition> findChecklist = null, bool includeArchived = true)
        {
            return Count(new StateSnapshot(checklist, findChecklist), includeArchived);
        }

        public static PungentChecklistProgressSummary Count(StateSnapshot snapshot, bool includeArchived = true)
        {
            PungentChecklistProgressSummary summary = new PungentChecklistProgressSummary();
            if (snapshot == null || snapshot.Checklist == null)
                return summary;

            foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(snapshot.Checklist))
            {
                if (item == null || (!includeArchived && item.archived))
                    continue;

                string stateId = snapshot.GetStateId(item);
                PungentChecklistStateOptionDefinition state = snapshot.ResolveState(stateId);
                summary.total++;
                if (!summary.countsByStateId.ContainsKey(state.stateId))
                    summary.countsByStateId[state.stateId] = 0;
                summary.countsByStateId[state.stateId]++;

                if (snapshot.CountsAsStarted(state.stateId))
                    summary.started++;
                else
                    summary.unstarted++;
                if (state.countsAsComplete)
                    summary.complete++;
                if (snapshot.CountsAsProblem(state.stateId))
                    summary.problem++;
            }

            return summary;
        }

        public static string BuildResultsText(PungentChecklistDefinition checklist, Func<string, PungentChecklistDefinition> findChecklist = null)
        {
            if (checklist == null)
                return string.Empty;

            StateSnapshot snapshot = new StateSnapshot(checklist, findChecklist);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(checklist.title + " results:");
            foreach (PungentChecklistStateOptionDefinition state in snapshot.StateOptions)
                builder.AppendLine(state.label.ToUpperInvariant() + ": " + JoinIds(snapshot, state.stateId, state.requiresComment || state.countsAsFailure));
            string guidance = BuildGuidanceText(snapshot);
            if (!string.IsNullOrWhiteSpace(guidance))
                builder.AppendLine("Guidance: " + guidance);
            return builder.ToString().Trim();
        }

        public static string BuildGuidanceText(PungentChecklistDefinition checklist, Func<string, PungentChecklistDefinition> findChecklist = null)
        {
            return BuildGuidanceText(new StateSnapshot(checklist, findChecklist));
        }

        public static string BuildGuidanceText(StateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Checklist == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            foreach (PungentChecklistSectionDefinition section in snapshot.Checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                bool needsFollowUp = (section.items ?? new List<PungentChecklistItemDefinition>()).Any(item =>
                {
                    string stateId = snapshot.GetStateId(item);
                    PungentChecklistStateOptionDefinition state = snapshot.ResolveState(stateId);
                    return state.requiresComment || snapshot.CountsAsProblem(stateId);
                });

                if (needsFollowUp && !string.IsNullOrWhiteSpace(section.guidancePrompt))
                    builder.AppendLine(section.guidancePrompt);
            }

            return builder.ToString().Trim();
        }

        public static string JoinIds(PungentChecklistDefinition checklist, string stateId, Func<string, PungentChecklistDefinition> findChecklist = null, bool includeNotes = false)
        {
            return JoinIds(new StateSnapshot(checklist, findChecklist), stateId, includeNotes);
        }

        public static string JoinIds(StateSnapshot snapshot, string stateId, bool includeNotes = false)
        {
            string clean = PungentChecklistProfiles.NormalizeStateId(stateId);
            if (snapshot == null || snapshot.Checklist == null)
                return "None";

            List<string> values = PungentChecklistSerialization.EnumerateItems(snapshot.Checklist)
                .Where(item => string.Equals(snapshot.GetStateId(item), clean, StringComparison.OrdinalIgnoreCase))
                .Select(item =>
                {
                    string note = GetComment(snapshot.Checklist, item.id);
                    string id = !string.IsNullOrWhiteSpace(item.linkedStateKey) ? item.id + " [" + item.linkedStateKey + "]" : item.id;
                    return includeNotes && !string.IsNullOrWhiteSpace(note) ? id + " - " + note.Trim() : id;
                })
                .ToList();
            return values.Count == 0 ? "None" : string.Join(", ", values.ToArray());
        }

        public static void Reset(IEnumerable<PungentChecklistDefinition> checklists)
        {
            foreach (PungentChecklistDefinition checklist in checklists ?? new PungentChecklistDefinition[0])
            {
                foreach (PungentChecklistItemDefinition item in PungentChecklistSerialization.EnumerateItems(checklist))
                {
                    if (!IsComputedFromChild(item))
                        ClearItemState(checklist, item);
                    SetComment(checklist, item.id, string.Empty);
                }
            }
        }

        public static bool IsComputedFromChild(PungentChecklistItemDefinition item)
        {
            return item != null &&
                   !string.IsNullOrWhiteSpace(item.childChecklistId) &&
                   string.Equals(item.parentStateMode, PungentChecklistConstants.ParentStateComputedFromChild, StringComparison.OrdinalIgnoreCase);
        }

        public static string StateKey(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.linkedStateKey))
                return PrefPrefix + "LinkedState." + SafeKey(item.linkedStateKey);
            return PrefPrefix + "State." + SafeKey(checklist == null ? string.Empty : checklist.checklistId) + "." + SafeKey(item == null ? string.Empty : item.id);
        }

        public static string StateIdKey(PungentChecklistDefinition checklist, PungentChecklistItemDefinition item)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.linkedStateKey))
                return PrefPrefix + "LinkedStateId." + SafeKey(item.linkedStateKey);
            return PrefPrefix + "StateId." + SafeKey(checklist == null ? string.Empty : checklist.checklistId) + "." + SafeKey(item == null ? string.Empty : item.id);
        }

        public static string NoteKey(PungentChecklistDefinition checklist, string itemId)
        {
            return PrefPrefix + "Note." + SafeKey(checklist == null ? string.Empty : checklist.checklistId) + "." + SafeKey(itemId);
        }

        public static string NoteCacheKey(PungentChecklistDefinition checklist, string itemId)
        {
            return SafeKey(checklist == null ? string.Empty : checklist.checklistId) + "." + SafeKey(itemId);
        }

        private static string GetChildChecklistStateId(PungentChecklistItemDefinition item, Func<string, PungentChecklistDefinition> findChecklist, HashSet<string> childStack)
        {
            string childId = item == null ? string.Empty : item.childChecklistId;
            if (string.IsNullOrWhiteSpace(childId) || findChecklist == null)
                return PungentChecklistProfiles.StateFail;

            if (childStack == null)
                childStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!childStack.Add(childId))
                return PungentChecklistProfiles.StateFail;

            try
            {
                PungentChecklistDefinition child = findChecklist(childId);
                if (child == null)
                    return PungentChecklistProfiles.StateFail;

                return AggregateChecklistStateId(child, findChecklist, childStack);
            }
            finally
            {
                childStack.Remove(childId);
            }
        }

        private static string AggregateChecklistStateId(PungentChecklistDefinition checklist, Func<string, PungentChecklistDefinition> findChecklist, HashSet<string> childStack)
        {
            List<PungentChecklistItemDefinition> required = PungentChecklistSerialization.EnumerateItems(checklist)
                .Where(item => item != null && !item.isOptional && !item.archived)
                .ToList();
            if (required.Count == 0)
                return PungentChecklistProfiles.StateUntested;

            int complete = 0;
            int started = 0;
            int problem = 0;
            foreach (PungentChecklistItemDefinition childItem in required)
            {
                string stateId = GetStateId(checklist, childItem, findChecklist, childStack);
                PungentChecklistStateOptionDefinition state = PungentChecklistProfiles.ResolveState(checklist, stateId);
                if (state.countsAsComplete)
                    complete++;
                if (PungentChecklistProfiles.CountsAsStarted(checklist, stateId))
                    started++;
                if (PungentChecklistProfiles.CountsAsProblem(checklist, stateId))
                    problem++;
            }

            if (problem > 0)
                return PungentChecklistProfiles.StateFail;
            if (complete == required.Count)
                return PungentChecklistProfiles.StatePass;
            if (started > 0)
                return PungentChecklistProfiles.StatePartial;
            return PungentChecklistProfiles.StateUntested;
        }

        private static string TranslateCompatibilityChildState(PungentChecklistDefinition parent, string childStateId)
        {
            switch (PungentChecklistProfiles.ToCompatibilityState(childStateId))
            {
                case PungentChecklistItemState.Pass:
                    return PungentChecklistProfiles.BestCompletionStateId(parent);
                case PungentChecklistItemState.Partial:
                    return PungentChecklistProfiles.BestPartialStateId(parent);
                case PungentChecklistItemState.Fail:
                    return PungentChecklistProfiles.BestFailureStateId(parent);
                default:
                    return PungentChecklistProfiles.DefaultStateId(parent);
            }
        }

        private static string SafeKey(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
#endif
}
