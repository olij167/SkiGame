using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum TrickPoseCoverageClassification
{
    Covered = 0,
    Ambiguous = 1,
    Suppressed = 2,
    Gap = 3
}

[System.Serializable]
public sealed class TrickPoseCoverageSettings
{
    public enum SampleDensity
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public SampleDensity density = SampleDensity.Low;
    public bool airborneOnly = true;
    public bool includeGroundedStates;
    public bool includeNoPoseInputStates;
    public bool collapseInactiveStateVariants = true;
    public bool skipZeroAngularDuplicateStates = true;
    public bool allowUnsafeSampleCount;
    public int nearestEntryCount = 3;
    public int safeSampleLimit = 4000;
    public int hardSampleLimit = 12000;
    public float yawMagnitude = 300f;
    public float pitchMagnitude = 300f;
    public float rollMagnitude = 180f;
}

public sealed class TrickPoseCoverageEntryDiagnostic
{
    public TrickPoseEntry entry;
    public bool matches;
    public int priority;
    public int specificity;
    public int failCount;
    public float heuristicDistance;
    public readonly List<string> failReasons = new List<string>();
    public string Summary => entry != null ? entry.GetSummary() : "(none)";
}

public sealed class TrickPoseCoverageContextEvaluation
{
    public TrickPoseEditorPreviewContext context;
    public TrickPoseCoverageClassification classification;
    public TrickPoseEntry bestEntry;
    public readonly List<TrickPoseEntry> matches = new List<TrickPoseEntry>();
    public readonly List<TrickPoseEntry> suppressedEntries = new List<TrickPoseEntry>();
    public readonly List<TrickPoseEntry> ambiguousEntries = new List<TrickPoseEntry>();
    public readonly List<TrickPoseCoverageEntryDiagnostic> nearestEntries = new List<TrickPoseCoverageEntryDiagnostic>();
    public string Summary;
}

public sealed class TrickPoseCoverageSampleSummary
{
    public TrickPoseEditorPreviewContext context;
    public TrickPoseCoverageClassification classification;
    public TrickPoseEntry bestEntry;
    public string Summary;
}

public sealed class TrickPoseCoverageGapCluster
{
    public string key;
    public TrickPoseCoverageSampleSummary representative;
    public readonly List<TrickPoseEditorPreviewContext> samples = new List<TrickPoseEditorPreviewContext>();
    public string traitSummary;
    public string suggestion;
    public int Count => samples.Count;
}

public sealed class TrickPoseCoverageReport
{
    public TrickPoseCoverageSettings settings;
    public int estimatedSampleCount;
    public int totalSamples;
    public int coveredCount;
    public int ambiguousCount;
    public int suppressedCount;
    public int gapCount;
    public readonly List<TrickPoseCoverageGapCluster> gapClusters = new List<TrickPoseCoverageGapCluster>();
}

public sealed class TrickPoseCoverageAnalysisJob
{
    public TrickPoseProfileSO profile;
    public SkiController controller;
    public TrickPoseCoverageSettings settings;
    public readonly List<TrickPoseEditorPreviewContext> contexts = new List<TrickPoseEditorPreviewContext>();
    public readonly Dictionary<string, TrickPoseCoverageGapCluster> clustersByKey = new Dictionary<string, TrickPoseCoverageGapCluster>();
    public readonly TrickPoseCoverageReport report = new TrickPoseCoverageReport();
    public int nextIndex;
    public bool canceled;
}

public static class TrickPoseCoverageAnalyzer
{
    public static int EstimateSampleCount(TrickPoseCoverageSettings settings)
    {
        if (settings == null)
            return 0;

        int yawCount = GetAxisSampleCount(settings.density);
        int pitchCount = GetAxisSampleCount(settings.density);
        int rollCount = GetAxisSampleCount(settings.density);
        int airborneCount = settings.airborneOnly ? 1 : (settings.includeGroundedStates ? 2 : 1);

        int total = 0;
        for (int airborneIndex = 0; airborneIndex < airborneCount; airborneIndex++)
        {
            bool airborne = settings.airborneOnly || airborneIndex == 0;
            bool[] poseInputOptions = !settings.includeNoPoseInputStates
                ? new[] { true }
                : (!airborne && settings.collapseInactiveStateVariants ? new[] { false } : new[] { true, false });

            for (int poseIndex = 0; poseIndex < poseInputOptions.Length; poseIndex++)
                total += EstimateInputStateCount(airborne, poseInputOptions[poseIndex], settings) * yawCount * pitchCount * rollCount;
        }

        return total;
    }

    public static bool ExceedsHardLimit(TrickPoseCoverageSettings settings, out int estimate)
    {
        estimate = EstimateSampleCount(settings);
        return settings != null && estimate > settings.hardSampleLimit;
    }

    public static bool RequiresUnsafeOverride(TrickPoseCoverageSettings settings, out int estimate)
    {
        estimate = EstimateSampleCount(settings);
        return settings != null && estimate > settings.safeSampleLimit;
    }

    public static TrickPoseCoverageAnalysisJob CreateJob(TrickPoseProfileSO profile, SkiController controller, TrickPoseCoverageSettings settings)
    {
        TrickPoseCoverageAnalysisJob job = new TrickPoseCoverageAnalysisJob
        {
            profile = profile,
            controller = controller,
            settings = CloneSettings(settings)
        };

        job.report.settings = CloneSettings(settings);
        job.report.estimatedSampleCount = EstimateSampleCount(settings);
        job.contexts.AddRange(GenerateSampleContexts(controller, settings));
        return job;
    }

    public static bool ProcessJob(TrickPoseCoverageAnalysisJob job, int samplesPerStep)
    {
        if (job == null || job.canceled)
            return true;

        int maxIndex = Mathf.Min(job.contexts.Count, job.nextIndex + Mathf.Max(1, samplesPerStep));
        for (int i = job.nextIndex; i < maxIndex; i++)
        {
            TrickPoseEditorPreviewContext context = job.contexts[i];
            TrickPoseCoverageSampleSummary summary = EvaluateSampleSummary(job.profile, context);
            job.report.totalSamples++;

            switch (summary.classification)
            {
                case TrickPoseCoverageClassification.Covered:
                    job.report.coveredCount++;
                    break;
                case TrickPoseCoverageClassification.Ambiguous:
                    job.report.ambiguousCount++;
                    break;
                case TrickPoseCoverageClassification.Suppressed:
                    job.report.suppressedCount++;
                    break;
                case TrickPoseCoverageClassification.Gap:
                    job.report.gapCount++;
                    AddGapSample(job, summary);
                    break;
            }
        }

        job.nextIndex = maxIndex;
        if (job.nextIndex < job.contexts.Count)
            return false;

        FinalizeJob(job);
        return true;
    }

    public static void CancelJob(TrickPoseCoverageAnalysisJob job)
    {
        if (job != null)
            job.canceled = true;
    }

    public static TrickPoseCoverageContextEvaluation EvaluateContext(TrickPoseProfileSO profile, TrickPoseEditorPreviewContext context, int nearestEntryCount)
    {
        TrickPoseCoverageContextEvaluation evaluation = new TrickPoseCoverageContextEvaluation
        {
            context = context,
            classification = TrickPoseCoverageClassification.Gap,
            Summary = "No authored entries match this state."
        };

        if (profile == null || context == null || profile.entries == null)
            return evaluation;

        List<TrickPoseEntry> matches = TrickPoseEditorPreviewUtility.EvaluateMatchingEntries(profile, context);
        evaluation.matches.AddRange(matches);

        if (matches.Count > 0)
        {
            TrickPoseEntry best = TrickPoseEditorPreviewUtility.EvaluateBestMatchingEntry(profile, context);
            evaluation.bestEntry = best;
            List<TrickPoseEntry> tiedTopMatches = FindTopMatches(matches);
            if (tiedTopMatches.Count > 1)
            {
                evaluation.classification = TrickPoseCoverageClassification.Ambiguous;
                evaluation.ambiguousEntries.AddRange(tiedTopMatches);
                evaluation.Summary = $"Ambiguous between {JoinEntryNames(tiedTopMatches, 3)}.";
            }
            else if (matches.Count > 1)
            {
                evaluation.classification = TrickPoseCoverageClassification.Suppressed;
                for (int i = 0; i < matches.Count; i++)
                {
                    if (matches[i] != best)
                        evaluation.suppressedEntries.Add(matches[i]);
                }

                evaluation.Summary = best != null
                    ? $"{best.GetSummary()} wins and suppresses {evaluation.suppressedEntries.Count} nearby match(es)."
                    : "Multiple entries match with one clear winner.";
            }
            else
            {
                evaluation.classification = TrickPoseCoverageClassification.Covered;
                evaluation.Summary = best != null ? $"Covered by {best.GetSummary()}." : "Covered by a single authored entry.";
            }
        }

        evaluation.nearestEntries.AddRange(BuildNearestDiagnostics(profile, context, nearestEntryCount, evaluation.bestEntry));
        return evaluation;
    }

    public static string DescribeContext(TrickPoseEditorPreviewContext context)
    {
        if (context == null)
            return "(no context)";

        StringBuilder builder = new StringBuilder();
        builder.Append(context.airborne ? "Airborne" : "Grounded");
        builder.Append(" | Pose ").Append(context.poseInputHeld ? "Held" : "Off");
        builder.Append(" | Slot ").Append(TrickPoseAuthoredStateFormatter.Format(context));
        builder.Append(" | Inputs ").Append(DescribeInputs(context));
        builder.Append(" | EntryRot ").Append(context.entryEulerAngles.ToString("0.#"));
        builder.Append(" | Y ").Append(context.yawAngularVelocity.ToString("0.#"));
        builder.Append(" P ").Append(context.pitchAngularVelocity.ToString("0.#"));
        builder.Append(" R ").Append(context.rollAngularVelocity.ToString("0.#"));
        builder.Append(" | Total ").Append(context.totalAngularSpeed.ToString("0.#"));
        return builder.ToString();
    }

    public static string BuildGapSuggestion(TrickPoseEditorPreviewContext context, TrickPoseCoverageEntryDiagnostic nearest)
    {
        if (context == null)
            return "Review this uncovered state and decide whether it should stay intentionally blank.";

        if (!context.airborne || !context.poseInputHeld)
            return "If this state should remain non-trickable, leave it blank. Otherwise add a deliberate fallback for inactive gating.";

        if (nearest == null || nearest.entry == null)
            return "Consider authoring a dedicated sibling entry for this uncovered posture pocket.";

        if (nearest.failReasons.Count == 0)
            return $"Review {nearest.entry.GetSummary()} for accidental isolation around this state.";

        string firstReason = nearest.failReasons[0];
        if (firstReason.Contains("Entry Pitch") || firstReason.Contains("Entry Yaw") || firstReason.Contains("Entry Roll"))
            return $"This sits near {nearest.entry.GetSummary()}. Prefer tuning the entry rotation range before falling back to angular velocity gates.";

        if (firstReason.Contains("Yaw") || firstReason.Contains("Pitch") || firstReason.Contains("Roll") || firstReason.Contains("Total Speed"))
            return $"This sits near {nearest.entry.GetSummary()}. Prefer a small range expansion or a neighboring variant instead of making it swallow too much space.";

        if (firstReason.Contains("Family") || firstReason.Contains("Shape") || firstReason.Contains("Orientation") || firstReason.Contains("Motion"))
            return $"This pocket differs structurally from {nearest.entry.GetSummary()}. A dedicated sibling entry is likely safer than broadening the existing one.";

        return $"Compare against {nearest.entry.GetSummary()} and decide whether a narrow condition tweak or a new sibling entry better preserves intentionality.";
    }

    private static TrickPoseCoverageSampleSummary EvaluateSampleSummary(TrickPoseProfileSO profile, TrickPoseEditorPreviewContext context)
    {
        TrickPoseCoverageSampleSummary summary = new TrickPoseCoverageSampleSummary
        {
            context = context,
            classification = TrickPoseCoverageClassification.Gap,
            bestEntry = null,
            Summary = "No authored entries match this state."
        };

        if (profile == null || context == null || profile.entries == null)
            return summary;

        List<TrickPoseEntry> matches = TrickPoseEditorPreviewUtility.EvaluateMatchingEntries(profile, context);
        if (matches.Count == 0)
            return summary;

        TrickPoseEntry best = TrickPoseEditorPreviewUtility.EvaluateBestMatchingEntry(profile, context);
        summary.bestEntry = best;
        List<TrickPoseEntry> tiedTopMatches = FindTopMatches(matches);

        if (tiedTopMatches.Count > 1)
        {
            summary.classification = TrickPoseCoverageClassification.Ambiguous;
            summary.Summary = $"Ambiguous between {JoinEntryNames(tiedTopMatches, 3)}.";
        }
        else if (matches.Count > 1)
        {
            summary.classification = TrickPoseCoverageClassification.Suppressed;
            summary.Summary = best != null ? $"{best.GetSummary()} suppresses nearby matches." : "Multiple entries match with one clear winner.";
        }
        else
        {
            summary.classification = TrickPoseCoverageClassification.Covered;
            summary.Summary = best != null ? $"Covered by {best.GetSummary()}." : "Covered by a single authored entry.";
        }

        return summary;
    }

    private static void AddGapSample(TrickPoseCoverageAnalysisJob job, TrickPoseCoverageSampleSummary summary)
    {
        string key = BuildGapClusterKey(summary.context, job.settings);
        if (!job.clustersByKey.TryGetValue(key, out TrickPoseCoverageGapCluster cluster))
        {
            cluster = new TrickPoseCoverageGapCluster
            {
                key = key,
                representative = summary,
                traitSummary = DescribeContext(summary.context),
                suggestion = BuildGapSuggestion(summary.context, null)
            };
            job.clustersByKey.Add(key, cluster);
        }

        cluster.samples.Add(summary.context);
    }

    private static void FinalizeJob(TrickPoseCoverageAnalysisJob job)
    {
        job.report.gapClusters.Clear();
        foreach (TrickPoseCoverageGapCluster cluster in job.clustersByKey.Values)
            job.report.gapClusters.Add(cluster);

        job.report.gapClusters.Sort((a, b) =>
        {
            int countCompare = b.Count.CompareTo(a.Count);
            if (countCompare != 0)
                return countCompare;

            return string.CompareOrdinal(a.key, b.key);
        });
    }

    private static List<TrickPoseEditorPreviewContext> GenerateSampleContexts(SkiController controller, TrickPoseCoverageSettings settings)
    {
        List<TrickPoseEditorPreviewContext> contexts = new List<TrickPoseEditorPreviewContext>();
        float[] yawValues = BuildAxisSamples(settings.density, settings.yawMagnitude);
        float[] pitchValues = BuildAxisSamples(settings.density, settings.pitchMagnitude);
        float[] rollValues = BuildAxisSamples(settings.density, settings.rollMagnitude);

        bool[] airborneOptions = settings.airborneOnly
            ? new[] { true }
            : settings.includeGroundedStates ? new[] { true, false } : new[] { true };

        for (int airborneIndex = 0; airborneIndex < airborneOptions.Length; airborneIndex++)
        {
            bool airborne = airborneOptions[airborneIndex];
            bool[] poseInputOptions = !settings.includeNoPoseInputStates
                ? new[] { true }
                : (!airborne && settings.collapseInactiveStateVariants ? new[] { false } : new[] { true, false });

            for (int poseHeldIndex = 0; poseHeldIndex < poseInputOptions.Length; poseHeldIndex++)
            {
                bool poseHeld = poseInputOptions[poseHeldIndex];
                foreach (InputState inputState in BuildInputStates(airborne, poseHeld, settings))
                {
                    foreach (float yaw in yawValues)
                    {
                        foreach (float pitch in pitchValues)
                        {
                            foreach (float roll in rollValues)
                            {
                                if (settings.skipZeroAngularDuplicateStates &&
                                    !airborne &&
                                    Mathf.Approximately(yaw, 0f) &&
                                    Mathf.Approximately(pitch, 0f) &&
                                    Mathf.Approximately(roll, 0f) &&
                                    inputState.index > 0)
                                {
                                    continue;
                                }

                                TrickPoseInfluencePreviewState state = new TrickPoseInfluencePreviewState
                                {
                                    poseInputHeld = poseHeld,
                                    tuckInput = inputState.tuck,
                                    leftInput = inputState.left,
                                    rightInput = inputState.right,
                                    airborne = airborne,
                                    leanInput = inputState.lean,
                                    rising = inputState.rising,
                                    diving = inputState.diving,
                                    manualRotationActive = inputState.manualRotationActive,
                                    manualRotationEuler = inputState.manualRotationEuler,
                                    yawAngularVelocity = yaw,
                                    pitchAngularVelocity = pitch,
                                    rollAngularVelocity = roll
                                };

                                TrickPoseEditorPreviewContext context = TrickPoseEditorPreviewUtility.BuildFromInfluences(controller, state);
                                if (context != null)
                                    contexts.Add(context);
                            }
                        }
                    }
                }
            }
        }

        return contexts;
    }

    private static List<TrickPoseCoverageEntryDiagnostic> BuildNearestDiagnostics(TrickPoseProfileSO profile, TrickPoseEditorPreviewContext context, int nearestEntryCount, TrickPoseEntry bestEntry)
    {
        List<TrickPoseCoverageEntryDiagnostic> diagnostics = new List<TrickPoseCoverageEntryDiagnostic>();
        if (profile == null || profile.entries == null || context == null)
            return diagnostics;

        for (int i = 0; i < profile.entries.Count; i++)
        {
            TrickPoseEntry entry = profile.entries[i];
            if (entry == null || !entry.enabled)
                continue;

            TrickPoseCoverageEntryDiagnostic diagnostic = BuildDiagnostic(entry, context);
            diagnostic.matches = diagnostic.failCount == 0;
            diagnostics.Add(diagnostic);
        }

        diagnostics.Sort(CompareDiagnostics);
        if (bestEntry != null)
        {
            diagnostics.Sort((a, b) =>
            {
                if (a.entry == bestEntry && b.entry != bestEntry)
                    return -1;
                if (b.entry == bestEntry && a.entry != bestEntry)
                    return 1;
                return CompareDiagnostics(a, b);
            });
        }

        if (diagnostics.Count > nearestEntryCount)
            diagnostics.RemoveRange(nearestEntryCount, diagnostics.Count - nearestEntryCount);
        return diagnostics;
    }

    private static TrickPoseCoverageEntryDiagnostic BuildDiagnostic(TrickPoseEntry entry, TrickPoseEditorPreviewContext context)
    {
        TrickPoseCoverageEntryDiagnostic diagnostic = new TrickPoseCoverageEntryDiagnostic
        {
            entry = entry,
            priority = entry.priority,
            specificity = entry.GetSpecificityScore(),
            heuristicDistance = EstimateDistance(entry, context)
        };

        List<string> failReasons = TrickPoseEditorPreviewUtility.BuildFailReasons(entry, context, 4);
        diagnostic.failReasons.AddRange(failReasons);
        diagnostic.failCount = failReasons.Count;
        return diagnostic;
    }

    private static int CompareDiagnostics(TrickPoseCoverageEntryDiagnostic a, TrickPoseCoverageEntryDiagnostic b)
    {
        int failCompare = a.failCount.CompareTo(b.failCount);
        if (failCompare != 0)
            return failCompare;

        int distanceCompare = a.heuristicDistance.CompareTo(b.heuristicDistance);
        if (distanceCompare != 0)
            return distanceCompare;

        int priorityCompare = b.priority.CompareTo(a.priority);
        if (priorityCompare != 0)
            return priorityCompare;

        return b.specificity.CompareTo(a.specificity);
    }

    private static float EstimateDistance(TrickPoseEntry entry, TrickPoseEditorPreviewContext context)
    {
        float score = 0f;

        if (entry.requiredPoseFamily != SkiController.AerialPoseFamily.None && entry.requiredPoseFamily != context.poseFamily)
            score += 3f;
        if (entry.requiredPoseShape != SkiController.AerialPoseShape.None && entry.requiredPoseShape != context.poseShape)
            score += 3f;
        if (entry.requiredVerticalOrientation != TrickPoseVerticalOrientationRequirement.Any && entry.requiredVerticalOrientation != context.verticalOrientation)
            score += 2.5f;
        if (entry.requiredHorizontalOrientation != TrickPoseHorizontalOrientationRequirement.Any && entry.requiredHorizontalOrientation != context.horizontalOrientation)
            score += 2.5f;
        if (entry.requiredMotionState != TrickPoseMotionStateRequirement.Any && entry.requiredMotionState != context.motionState)
            score += 2f;
        if (UsesLegacyOrientationModifier(entry) && entry.requiredOrientationModifier != context.orientationModifier)
            score += 2.5f;
        if (!string.IsNullOrWhiteSpace(entry.requiredPoseName) && !string.Equals(entry.requiredPoseName, context.poseName, System.StringComparison.OrdinalIgnoreCase))
            score += 3.5f;

        score += ScoreBool(entry.requireAirborne, context.airborne);
        score += ScoreBool(entry.requirePoseButtonHeld, context.poseInputHeld);
        if (entry.useAdvancedModifierConditions)
        {
            score += ScoreSpin(entry.requiredSpinDirection, context.spinDirectionSign);
            score += ScoreFlip(entry.requiredFlipDirection, context.flipDirectionSign);
            score += ScoreRange(entry.yawAngularVelocityRange, context.yawAngularVelocity, 120f);
            score += ScoreRange(entry.pitchAngularVelocityRange, context.pitchAngularVelocity, 120f);
            score += ScoreRange(entry.rollAngularVelocityRange, context.rollAngularVelocity, 90f);
            score += ScoreRange(entry.totalAngularSpeedRange, context.totalAngularSpeed, 150f);
        }
        score += ScoreAngleRange(entry.entryPitchAngleRange, context.entryEulerAngles.x);
        score += ScoreAngleRange(entry.entryYawAngleRange, context.entryEulerAngles.y);
        score += ScoreAngleRange(entry.entryRollAngleRange, context.entryEulerAngles.z);
        return score;
    }

    private static float ScoreBool(TrickPoseBoolRequirement requirement, bool value)
    {
        return requirement switch
        {
            TrickPoseBoolRequirement.True => value ? 0f : 2f,
            TrickPoseBoolRequirement.False => value ? 2f : 0f,
            _ => 0f
        };
    }

    private static float ScoreSpin(TrickPoseSpinDirectionRequirement requirement, int value)
    {
        return requirement == TrickPoseSpinDirectionRequirement.Any || value == (int)requirement ? 0f : 1.5f;
    }

    private static float ScoreFlip(TrickPoseFlipDirectionRequirement requirement, int value)
    {
        return requirement == TrickPoseFlipDirectionRequirement.Any || value == (int)requirement ? 0f : 1.5f;
    }

    private static float ScoreRange(TrickPoseAngularVelocityRange range, float value, float normalization)
    {
        if (!range.enabled)
            return 0f;

        Vector2 sorted = range.GetSortedRange();
        if (value < sorted.x)
            return Mathf.Clamp01((sorted.x - value) / normalization) + 0.2f;
        if (value > sorted.y)
            return Mathf.Clamp01((value - sorted.y) / normalization) + 0.2f;
        return 0f;
    }

    private static float ScoreAngleRange(TrickPoseEulerAngleRange range, float value)
    {
        if (!range.enabled)
            return 0f;

        Vector2 sorted = range.GetSortedRange();
        if (value < sorted.x)
            return Mathf.Clamp01((sorted.x - value) / 90f) + 0.2f;
        if (value > sorted.y)
            return Mathf.Clamp01((value - sorted.y) / 90f) + 0.2f;
        return 0f;
    }

    private static List<TrickPoseEntry> FindTopMatches(List<TrickPoseEntry> matches)
    {
        List<TrickPoseEntry> topMatches = new List<TrickPoseEntry>();
        int bestPriority = int.MinValue;
        int bestSpecificity = int.MinValue;

        for (int i = 0; i < matches.Count; i++)
        {
            TrickPoseEntry match = matches[i];
            if (match == null)
                continue;

            int priority = match.priority;
            int specificity = match.GetSpecificityScore();
            if (priority > bestPriority || (priority == bestPriority && specificity > bestSpecificity))
            {
                topMatches.Clear();
                topMatches.Add(match);
                bestPriority = priority;
                bestSpecificity = specificity;
            }
            else if (priority == bestPriority && specificity == bestSpecificity)
            {
                topMatches.Add(match);
            }
        }

        return topMatches;
    }

    private static string JoinEntryNames(List<TrickPoseEntry> entries, int maxCount)
    {
        if (entries == null || entries.Count == 0)
            return "(none)";

        int count = Mathf.Min(entries.Count, maxCount);
        List<string> names = new List<string>(count);
        for (int i = 0; i < count; i++)
            names.Add(entries[i] != null ? entries[i].GetSummary() : "(null)");

        if (entries.Count > maxCount)
            names.Add("...");

        return string.Join(", ", names);
    }

    private static TrickPoseCoverageSettings CloneSettings(TrickPoseCoverageSettings source)
    {
        if (source == null)
            return new TrickPoseCoverageSettings();

        return new TrickPoseCoverageSettings
        {
            density = source.density,
            airborneOnly = source.airborneOnly,
            includeGroundedStates = source.includeGroundedStates,
            includeNoPoseInputStates = source.includeNoPoseInputStates,
            collapseInactiveStateVariants = source.collapseInactiveStateVariants,
            skipZeroAngularDuplicateStates = source.skipZeroAngularDuplicateStates,
            allowUnsafeSampleCount = source.allowUnsafeSampleCount,
            nearestEntryCount = source.nearestEntryCount,
            safeSampleLimit = source.safeSampleLimit,
            hardSampleLimit = source.hardSampleLimit,
            yawMagnitude = source.yawMagnitude,
            pitchMagnitude = source.pitchMagnitude,
            rollMagnitude = source.rollMagnitude
        };
    }

    private static string BuildGapClusterKey(TrickPoseEditorPreviewContext context, TrickPoseCoverageSettings settings)
    {
        if (context == null)
            return "none";

        return string.Join("|",
            context.airborne ? "air" : "ground",
            context.poseInputHeld ? "pose" : "nopose",
            context.poseFamily.ToString(),
            context.poseShape.ToString(),
            context.verticalOrientation.ToString(),
            context.horizontalOrientation.ToString(),
            context.motionState.ToString(),
            DescribeInputs(context),
            BandLabel(context.yawAngularVelocity, settings.yawMagnitude, settings.density),
            BandLabel(context.pitchAngularVelocity, settings.pitchMagnitude, settings.density),
            BandLabel(context.rollAngularVelocity, settings.rollMagnitude, settings.density));
    }

    private static string DescribeInputs(TrickPoseEditorPreviewContext context)
    {
        if (context == null)
            return "none";

        List<string> labels = new List<string>();
        if (context.leftInput)
            labels.Add("Left");
        if (context.rightInput)
            labels.Add("Right");
        if (context.tuckInput)
            labels.Add("Tuck");
        else if (context.leanInput >= 0.35f)
            labels.Add("ForwardLean");
        else if (context.leanInput <= -0.35f)
            labels.Add("BackwardLean");
        if (context.rising)
            labels.Add("Rising");
        if (context.diving)
            labels.Add("Diving");
        if (context.hasPresentationRotation)
            labels.Add(FormatOrientation(context.orientationModifier));

        return labels.Count > 0 ? string.Join("/", labels) : "Neutral";
    }

    private static string DescribePoseShape(SkiController.AerialPoseShape shape)
    {
        return shape switch
        {
            SkiController.AerialPoseShape.Compact => "Compact/Tucked",
            SkiController.AerialPoseShape.Driving => "Forward Lean",
            SkiController.AerialPoseShape.LaidOut => "Backward Lean/Extended",
            _ => shape.ToString()
        };
    }

    private static string FormatOrientation(SkiController.AerialOrientationModifier orientation)
    {
        string label = SkiController.GetAerialOrientationModifierLabel(orientation);
        return string.IsNullOrWhiteSpace(label) ? "None" : label;
    }

    private static bool UsesLegacyOrientationModifier(TrickPoseEntry entry)
    {
        return entry != null &&
               entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None &&
               entry.requiredVerticalOrientation == TrickPoseVerticalOrientationRequirement.Any &&
               entry.requiredHorizontalOrientation == TrickPoseHorizontalOrientationRequirement.Any &&
               entry.requiredMotionState == TrickPoseMotionStateRequirement.Any;
    }

    private static string BandLabel(float value, float magnitude, TrickPoseCoverageSettings.SampleDensity density)
    {
        float step = density switch
        {
            TrickPoseCoverageSettings.SampleDensity.Low => magnitude / 2f,
            TrickPoseCoverageSettings.SampleDensity.High => magnitude / 6f,
            _ => magnitude / 4f
        };

        if (Mathf.Approximately(step, 0f))
            return "0";

        return Mathf.RoundToInt(value / step).ToString();
    }

    private static int GetAxisSampleCount(TrickPoseCoverageSettings.SampleDensity density)
    {
        return density switch
        {
            TrickPoseCoverageSettings.SampleDensity.Low => 5,
            TrickPoseCoverageSettings.SampleDensity.High => 7,
            _ => 7
        };
    }

    private static float[] BuildAxisSamples(TrickPoseCoverageSettings.SampleDensity density, float magnitude)
    {
        magnitude = Mathf.Max(1f, Mathf.Abs(magnitude));
        switch (density)
        {
            case TrickPoseCoverageSettings.SampleDensity.Low:
                return new[] { -magnitude, -magnitude * 0.5f, 0f, magnitude * 0.5f, magnitude };

            case TrickPoseCoverageSettings.SampleDensity.High:
                return new[]
                {
                    -magnitude, -magnitude * 0.66f, -magnitude * 0.33f,
                    0f,
                    magnitude * 0.33f, magnitude * 0.66f, magnitude
                };

            default:
                return new[] { -magnitude, -magnitude * 0.5f, -magnitude * 0.2f, 0f, magnitude * 0.2f, magnitude * 0.5f, magnitude };
        }
    }

    private static int EstimateInputStateCount(bool airborne, bool poseHeld, TrickPoseCoverageSettings settings)
    {
        if (!airborne || !poseHeld)
            return settings.collapseInactiveStateVariants ? 1 : 4;

        return 144;
    }

    private static IEnumerable<InputState> BuildInputStates(bool airborne, bool poseHeld, TrickPoseCoverageSettings settings)
    {
        List<InputState> states = new List<InputState>();
        if (!airborne || !poseHeld)
        {
            states.Add(new InputState { index = 0 });
            if (!settings.collapseInactiveStateVariants)
            {
                states.Add(new InputState { left = true, index = 1 });
                states.Add(new InputState { right = true, index = 2 });
                states.Add(new InputState { left = true, right = true, index = 3 });
            }

            return states;
        }

        bool[] tuckOptions = { false, true };
        float[] leanOptions = { 0f, 0.65f, -0.65f };
        InputState[] directionStates =
        {
            new InputState { index = 0 },
            new InputState { left = true, index = 1 },
            new InputState { right = true, index = 2 },
            new InputState { left = true, right = true, index = 3 }
        };
        OrientationInputState[] orientationStates =
        {
            new OrientationInputState(),
            new OrientationInputState { rising = true },
            new OrientationInputState { diving = true },
            new OrientationInputState { manualRotationActive = true, manualRotationEuler = new Vector3(0f, 0f, 90f) },
            new OrientationInputState { manualRotationActive = true, manualRotationEuler = new Vector3(90f, 0f, 0f) },
            new OrientationInputState { manualRotationActive = true, manualRotationEuler = new Vector3(-90f, 0f, 0f) }
        };

        int counter = 0;
        for (int directionIndex = 0; directionIndex < directionStates.Length; directionIndex++)
        {
            for (int tuckIndex = 0; tuckIndex < tuckOptions.Length; tuckIndex++)
            {
                for (int leanIndex = 0; leanIndex < leanOptions.Length; leanIndex++)
                {
                    for (int orientationIndex = 0; orientationIndex < orientationStates.Length; orientationIndex++)
                    {
                        InputState state = directionStates[directionIndex];
                        state.tuck = tuckOptions[tuckIndex];
                        state.lean = state.tuck ? 0f : leanOptions[leanIndex];
                        state.rising = orientationStates[orientationIndex].rising;
                        state.diving = orientationStates[orientationIndex].diving;
                        state.manualRotationActive = orientationStates[orientationIndex].manualRotationActive;
                        state.manualRotationEuler = orientationStates[orientationIndex].manualRotationEuler;
                        state.index = counter++;
                        states.Add(state);
                    }
                }
            }
        }

        return states;
    }

    private struct InputState
    {
        public bool left;
        public bool right;
        public bool tuck;
        public float lean;
        public bool rising;
        public bool diving;
        public bool manualRotationActive;
        public Vector3 manualRotationEuler;
        public int index;
    }

    private struct OrientationInputState
    {
        public bool rising;
        public bool diving;
        public bool manualRotationActive;
        public Vector3 manualRotationEuler;
    }
}
