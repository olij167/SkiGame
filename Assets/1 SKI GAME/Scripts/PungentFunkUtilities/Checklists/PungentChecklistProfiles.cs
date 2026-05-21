using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Checklists
{
    public enum PungentChecklistListKind
    {
        QualityGate = 0,
        ToDo = 10,
        Review = 20,
        Approval = 30,
        BugTriage = 40,
        ReleaseReadiness = 50,
        Migration = 60,
        Custom = 100
    }

    [Serializable]
    public sealed class PungentChecklistStateOptionDefinition
    {
        public string stateId = string.Empty;
        public string label = string.Empty;
        public string shortLabel = string.Empty;
        public string semanticRole = PungentChecklistStateRoles.None;
        public bool countsAsComplete;
        public bool countsAsFailure;
        public bool requiresComment;
        public string themeToken = PungentChecklistThemeTokens.Neutral;

        public void NormalizeInPlace()
        {
            stateId = PungentChecklistProfiles.NormalizeStateId(stateId);
            label = string.IsNullOrWhiteSpace(label) ? stateId : label.Trim();
            shortLabel = string.IsNullOrWhiteSpace(shortLabel) ? label : shortLabel.Trim();
            semanticRole = PungentChecklistStateRoles.Normalize(semanticRole);
            themeToken = PungentChecklistThemeTokens.Normalize(themeToken);
        }
    }

    [Serializable]
    public sealed class PungentChecklistStateProfileDefinition
    {
        public string profileId = string.Empty;
        public string label = string.Empty;
        public string listKind = PungentChecklistListKinds.QualityGate;
        public string defaultStateId = string.Empty;
        public string guidance = string.Empty;
        public List<PungentChecklistStateOptionDefinition> states = new List<PungentChecklistStateOptionDefinition>();

        public void NormalizeInPlace()
        {
            profileId = PungentChecklistProfiles.NormalizeProfileId(profileId);
            label = string.IsNullOrWhiteSpace(label) ? profileId : label.Trim();
            listKind = PungentChecklistListKinds.Normalize(listKind);
            defaultStateId = PungentChecklistProfiles.NormalizeStateId(defaultStateId);
            guidance = guidance == null ? string.Empty : guidance.Trim();
            states = states ?? new List<PungentChecklistStateOptionDefinition>();
            for (int i = 0; i < states.Count; i++)
                states[i]?.NormalizeInPlace();
            states.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.stateId));
            if (string.IsNullOrWhiteSpace(defaultStateId) && states.Count > 0)
                defaultStateId = states[0].stateId;
        }
    }

    public static class PungentChecklistListKinds
    {
        public const string QualityGate = "quality-gate";
        public const string ToDo = "to-do";
        public const string Review = "review";
        public const string Approval = "approval";
        public const string BugTriage = "bug-triage";
        public const string ReleaseReadiness = "release-readiness";
        public const string Migration = "migration";
        public const string Custom = "custom";

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return QualityGate;

            string clean = value.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");
            switch (clean)
            {
                case "qa":
                case "quality":
                case "qualitygate":
                case QualityGate:
                    return QualityGate;
                case "todo":
                case "task":
                case ToDo:
                    return ToDo;
                case Review:
                    return Review;
                case Approval:
                    return Approval;
                case "bug":
                case "issue":
                case "triage":
                case BugTriage:
                    return BugTriage;
                case "release":
                case ReleaseReadiness:
                    return ReleaseReadiness;
                case Migration:
                    return Migration;
                default:
                    return Custom;
            }
        }

        public static string GetDisplayName(string value)
        {
            switch (Normalize(value))
            {
                case ToDo: return "To-Do";
                case Review: return "Review";
                case Approval: return "Approval";
                case BugTriage: return "Bug Triage";
                case ReleaseReadiness: return "Release Readiness";
                case Migration: return "Migration";
                case Custom: return "Custom";
                default: return "Quality Gate";
            }
        }

        public static IReadOnlyList<string> BuiltInKinds => new[]
        {
            QualityGate,
            ToDo,
            Review,
            ReleaseReadiness,
            Migration,
            BugTriage
        };
    }

    public static class PungentChecklistStateRoles
    {
        public const string None = "none";
        public const string Success = "success";
        public const string Partial = "partial";
        public const string Fail = "fail";
        public const string Blocked = "blocked";
        public const string Skipped = "skipped";
        public const string Done = "done";
        public const string Approved = "approved";
        public const string Rejected = "rejected";
        public const string Open = "open";
        public const string Active = "active";

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return None;

            string clean = value.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");
            switch (clean)
            {
                case Success:
                case Partial:
                case Fail:
                case Blocked:
                case Skipped:
                case Done:
                case Approved:
                case Rejected:
                case Open:
                case Active:
                    return clean;
                default:
                    return None;
            }
        }
    }

    public static class PungentChecklistThemeTokens
    {
        public const string Neutral = "neutral";
        public const string Green = "green";
        public const string Amber = "amber";
        public const string Red = "red";
        public const string Blue = "blue";
        public const string Cyan = "cyan";
        public const string Purple = "purple";

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Neutral;

            string clean = value.Trim().ToLowerInvariant();
            switch (clean)
            {
                case Green:
                case Amber:
                case Red:
                case Blue:
                case Cyan:
                case Purple:
                    return clean;
                default:
                    return Neutral;
            }
        }
    }

    public static class PungentChecklistProfileIds
    {
        public const string QualityGate = "quality-gate.pass-partial-fail";
        public const string ToDo = "todo.not-started-in-progress-blocked-done";
        public const string Review = "review.needs-changes-approved-rejected";
        public const string ReleaseMigration = "release-migration.not-started-in-progress-done-skipped-blocked";
        public const string BugTriage = "bug-triage.open-investigating-fixed-verified-wont-fix";
    }

    public static class PungentChecklistProfiles
    {
        public const string StateUntested = "untested";
        public const string StatePass = "pass";
        public const string StatePartial = "partial";
        public const string StateFail = "fail";

        private static readonly List<PungentChecklistStateProfileDefinition> BuiltInProfiles = BuildProfiles();

        public static IReadOnlyList<PungentChecklistStateProfileDefinition> AllBuiltInProfiles => BuiltInProfiles;

        public static string NormalizeProfileId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return PungentChecklistProfileIds.QualityGate;

            string clean = value.Trim().ToLowerInvariant();
            switch (clean)
            {
                case "to-do.standard":
                case "todo.standard":
                    return PungentChecklistProfileIds.ToDo;
                case "review.approval":
                    return PungentChecklistProfileIds.Review;
                case "release-migration.standard":
                    return PungentChecklistProfileIds.ReleaseMigration;
                case "bug-triage.standard":
                    return PungentChecklistProfileIds.BugTriage;
                default:
                    return clean;
            }
        }

        public static string DefaultProfileIdForListKind(string listKind)
        {
            switch (PungentChecklistListKinds.Normalize(listKind))
            {
                case PungentChecklistListKinds.ToDo:
                    return PungentChecklistProfileIds.ToDo;
                case PungentChecklistListKinds.Review:
                    return PungentChecklistProfileIds.Review;
                case PungentChecklistListKinds.ReleaseReadiness:
                case PungentChecklistListKinds.Migration:
                    return PungentChecklistProfileIds.ReleaseMigration;
                case PungentChecklistListKinds.BugTriage:
                    return PungentChecklistProfileIds.BugTriage;
                default:
                    return PungentChecklistProfileIds.QualityGate;
            }
        }

        public static string NormalizeStateId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");
        }

        public static PungentChecklistStateProfileDefinition ResolveProfile(PungentChecklistDefinition checklist)
        {
            if (checklist != null && checklist.stateOptions != null && checklist.stateOptions.Count > 0)
            {
                PungentChecklistStateProfileDefinition custom = new PungentChecklistStateProfileDefinition
                {
                    profileId = string.IsNullOrWhiteSpace(checklist.stateProfileId) ? PungentChecklistProfileIds.QualityGate : checklist.stateProfileId,
                    label = "Checklist Profile",
                    listKind = checklist.listKind,
                    states = checklist.stateOptions.Select(Clone).ToList()
                };
                custom.NormalizeInPlace();
                return custom;
            }

            string profileId = NormalizeProfileId(checklist == null ? string.Empty : checklist.stateProfileId);
            PungentChecklistStateProfileDefinition profile = BuiltInProfiles.FirstOrDefault(item => string.Equals(item.profileId, profileId, StringComparison.OrdinalIgnoreCase));
            if (profile != null)
                return Clone(profile);

            string kind = PungentChecklistListKinds.Normalize(checklist == null ? string.Empty : checklist.listKind);
            profile = BuiltInProfiles.FirstOrDefault(item => string.Equals(item.listKind, kind, StringComparison.OrdinalIgnoreCase));
            return Clone(profile ?? BuiltInProfiles[0]);
        }

        public static IReadOnlyList<PungentChecklistStateOptionDefinition> ResolveStateOptions(PungentChecklistDefinition checklist)
        {
            PungentChecklistStateProfileDefinition profile = ResolveProfile(checklist);
            return profile.states;
        }

        public static PungentChecklistStateOptionDefinition ResolveState(PungentChecklistDefinition checklist, string stateId)
        {
            IReadOnlyList<PungentChecklistStateOptionDefinition> states = ResolveStateOptions(checklist);
            string clean = NormalizeStateId(stateId);
            PungentChecklistStateOptionDefinition state = states.FirstOrDefault(item => string.Equals(item.stateId, clean, StringComparison.OrdinalIgnoreCase));
            return state ?? states.FirstOrDefault() ?? QualityState(StateUntested, "Untested", "Open", PungentChecklistStateRoles.None, false, false, false, PungentChecklistThemeTokens.Neutral);
        }

        public static string DefaultStateId(PungentChecklistDefinition checklist)
        {
            PungentChecklistStateProfileDefinition profile = ResolveProfile(checklist);
            return string.IsNullOrWhiteSpace(profile.defaultStateId) && profile.states.Count > 0 ? profile.states[0].stateId : profile.defaultStateId;
        }

        public static string ToStateId(PungentChecklistItemState state)
        {
            switch (state)
            {
                case PungentChecklistItemState.Pass: return StatePass;
                case PungentChecklistItemState.Partial: return StatePartial;
                case PungentChecklistItemState.Fail: return StateFail;
                default: return StateUntested;
            }
        }

        public static PungentChecklistItemState ToCompatibilityState(string stateId)
        {
            string clean = NormalizeStateId(stateId);
            if (clean == StatePass)
                return PungentChecklistItemState.Pass;
            if (clean == StatePartial)
                return PungentChecklistItemState.Partial;
            if (clean == StateFail)
                return PungentChecklistItemState.Fail;
            return PungentChecklistItemState.Untested;
        }

        public static bool IsQualityGate(PungentChecklistDefinition checklist)
        {
            return string.Equals(ResolveProfile(checklist).profileId, PungentChecklistProfileIds.QualityGate, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CountsAsStarted(PungentChecklistDefinition checklist, string stateId)
        {
            string clean = NormalizeStateId(stateId);
            return !string.IsNullOrWhiteSpace(clean) &&
                   !string.Equals(clean, DefaultStateId(checklist), StringComparison.OrdinalIgnoreCase);
        }

        public static bool CountsAsProblem(PungentChecklistDefinition checklist, string stateId)
        {
            PungentChecklistStateOptionDefinition state = ResolveState(checklist, stateId);
            return state.countsAsFailure ||
                   string.Equals(state.semanticRole, PungentChecklistStateRoles.Fail, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state.semanticRole, PungentChecklistStateRoles.Blocked, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state.semanticRole, PungentChecklistStateRoles.Rejected, StringComparison.OrdinalIgnoreCase);
        }

        public static string BestCompletionStateId(PungentChecklistDefinition checklist)
        {
            IReadOnlyList<PungentChecklistStateOptionDefinition> states = ResolveStateOptions(checklist);
            PungentChecklistStateOptionDefinition state = states.FirstOrDefault(item => item.countsAsComplete);
            return state == null ? DefaultStateId(checklist) : state.stateId;
        }

        public static string BestPartialStateId(PungentChecklistDefinition checklist)
        {
            IReadOnlyList<PungentChecklistStateOptionDefinition> states = ResolveStateOptions(checklist);
            PungentChecklistStateOptionDefinition state = states.FirstOrDefault(item =>
                string.Equals(item.semanticRole, PungentChecklistStateRoles.Partial, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.semanticRole, PungentChecklistStateRoles.Active, StringComparison.OrdinalIgnoreCase));
            return state == null ? DefaultStateId(checklist) : state.stateId;
        }

        public static string BestFailureStateId(PungentChecklistDefinition checklist)
        {
            IReadOnlyList<PungentChecklistStateOptionDefinition> states = ResolveStateOptions(checklist);
            PungentChecklistStateOptionDefinition state = states.FirstOrDefault(item => item.countsAsFailure);
            return state == null ? DefaultStateId(checklist) : state.stateId;
        }

        public static PungentChecklistStateOptionDefinition Clone(PungentChecklistStateOptionDefinition source)
        {
            if (source == null)
                return null;

            PungentChecklistStateOptionDefinition copy = new PungentChecklistStateOptionDefinition
            {
                stateId = source.stateId,
                label = source.label,
                shortLabel = source.shortLabel,
                semanticRole = source.semanticRole,
                countsAsComplete = source.countsAsComplete,
                countsAsFailure = source.countsAsFailure,
                requiresComment = source.requiresComment,
                themeToken = source.themeToken
            };
            copy.NormalizeInPlace();
            return copy;
        }

        public static PungentChecklistStateProfileDefinition Clone(PungentChecklistStateProfileDefinition source)
        {
            if (source == null)
                return null;

            PungentChecklistStateProfileDefinition copy = new PungentChecklistStateProfileDefinition
            {
                profileId = source.profileId,
                label = source.label,
                listKind = source.listKind,
                defaultStateId = source.defaultStateId,
                guidance = source.guidance,
                states = (source.states ?? new List<PungentChecklistStateOptionDefinition>()).Select(Clone).Where(item => item != null).ToList()
            };
            copy.NormalizeInPlace();
            return copy;
        }

        private static List<PungentChecklistStateProfileDefinition> BuildProfiles()
        {
            return new List<PungentChecklistStateProfileDefinition>
            {
                Profile(
                    PungentChecklistProfileIds.QualityGate,
                    "Quality Gate",
                    PungentChecklistListKinds.QualityGate,
                    StateUntested,
                    "Use partial or failed items as follow-up work.",
                    QualityState(StateUntested, "Untested", "Open", PungentChecklistStateRoles.None, false, false, false, PungentChecklistThemeTokens.Neutral),
                    QualityState(StatePass, "Pass", "Pass", PungentChecklistStateRoles.Success, true, false, false, PungentChecklistThemeTokens.Green),
                    QualityState(StatePartial, "Partial", "Partial", PungentChecklistStateRoles.Partial, false, false, true, PungentChecklistThemeTokens.Amber),
                    QualityState(StateFail, "Fail", "Fail", PungentChecklistStateRoles.Fail, false, true, true, PungentChecklistThemeTokens.Red)),
                Profile(
                    PungentChecklistProfileIds.ToDo,
                    "To-Do",
                    PungentChecklistListKinds.ToDo,
                    "not-started",
                    "Move blocked or overdue items into follow-up notes.",
                    QualityState("not-started", "Not Started", "Open", PungentChecklistStateRoles.Open, false, false, false, PungentChecklistThemeTokens.Neutral),
                    QualityState("in-progress", "In Progress", "Doing", PungentChecklistStateRoles.Active, false, false, false, PungentChecklistThemeTokens.Blue),
                    QualityState("blocked", "Blocked", "Blocked", PungentChecklistStateRoles.Blocked, false, true, true, PungentChecklistThemeTokens.Red),
                    QualityState("done", "Done", "Done", PungentChecklistStateRoles.Done, true, false, false, PungentChecklistThemeTokens.Green)),
                Profile(
                    PungentChecklistProfileIds.Review,
                    "Review",
                    PungentChecklistListKinds.Review,
                    "not-reviewed",
                    "Track unresolved review feedback before approval.",
                    QualityState("not-reviewed", "Not Reviewed", "Open", PungentChecklistStateRoles.Open, false, false, false, PungentChecklistThemeTokens.Neutral),
                    QualityState("needs-changes", "Needs Changes", "Changes", PungentChecklistStateRoles.Partial, false, true, true, PungentChecklistThemeTokens.Amber),
                    QualityState("approved", "Approved", "Approved", PungentChecklistStateRoles.Approved, true, false, false, PungentChecklistThemeTokens.Green),
                    QualityState("rejected", "Rejected", "Rejected", PungentChecklistStateRoles.Rejected, false, true, true, PungentChecklistThemeTokens.Red)),
                Profile(
                    PungentChecklistProfileIds.ReleaseMigration,
                    "Release / Migration",
                    PungentChecklistListKinds.ReleaseReadiness,
                    "not-started",
                    "Resolve blocked items before release or migration sign-off.",
                    QualityState("not-started", "Not Started", "Open", PungentChecklistStateRoles.Open, false, false, false, PungentChecklistThemeTokens.Neutral),
                    QualityState("in-progress", "In Progress", "Doing", PungentChecklistStateRoles.Active, false, false, false, PungentChecklistThemeTokens.Blue),
                    QualityState("done", "Done", "Done", PungentChecklistStateRoles.Done, true, false, false, PungentChecklistThemeTokens.Green),
                    QualityState("skipped", "Skipped", "Skipped", PungentChecklistStateRoles.Skipped, true, false, true, PungentChecklistThemeTokens.Purple),
                    QualityState("blocked", "Blocked", "Blocked", PungentChecklistStateRoles.Blocked, false, true, true, PungentChecklistThemeTokens.Red)),
                Profile(
                    PungentChecklistProfileIds.BugTriage,
                    "Bug / Issue Triage",
                    PungentChecklistListKinds.BugTriage,
                    "open",
                    "Track investigation and verification status for issue follow-up.",
                    QualityState("open", "Open", "Open", PungentChecklistStateRoles.Open, false, false, false, PungentChecklistThemeTokens.Neutral),
                    QualityState("investigating", "Investigating", "Investigating", PungentChecklistStateRoles.Active, false, false, false, PungentChecklistThemeTokens.Blue),
                    QualityState("fixed", "Fixed", "Fixed", PungentChecklistStateRoles.Done, true, false, false, PungentChecklistThemeTokens.Green),
                    QualityState("verified", "Verified", "Verified", PungentChecklistStateRoles.Success, true, false, false, PungentChecklistThemeTokens.Green),
                    QualityState("wont-fix", "Won't Fix", "Won't Fix", PungentChecklistStateRoles.Skipped, true, false, true, PungentChecklistThemeTokens.Purple))
            };
        }

        private static PungentChecklistStateProfileDefinition Profile(
            string id,
            string label,
            string kind,
            string defaultState,
            string guidance,
            params PungentChecklistStateOptionDefinition[] states)
        {
            PungentChecklistStateProfileDefinition profile = new PungentChecklistStateProfileDefinition
            {
                profileId = id,
                label = label,
                listKind = kind,
                defaultStateId = defaultState,
                guidance = guidance,
                states = new List<PungentChecklistStateOptionDefinition>(states ?? new PungentChecklistStateOptionDefinition[0])
            };
            profile.NormalizeInPlace();
            return profile;
        }

        private static PungentChecklistStateOptionDefinition QualityState(
            string id,
            string label,
            string shortLabel,
            string role,
            bool complete,
            bool failure,
            bool requiresComment,
            string token)
        {
            PungentChecklistStateOptionDefinition state = new PungentChecklistStateOptionDefinition
            {
                stateId = id,
                label = label,
                shortLabel = shortLabel,
                semanticRole = role,
                countsAsComplete = complete,
                countsAsFailure = failure,
                requiresComment = requiresComment,
                themeToken = token
            };
            state.NormalizeInPlace();
            return state;
        }
    }
}
