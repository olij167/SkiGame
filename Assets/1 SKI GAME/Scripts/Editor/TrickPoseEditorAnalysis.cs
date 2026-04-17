using System.Collections.Generic;
using UnityEngine;

public static class TrickPoseEditorAnalysis
{
    public enum OverlapSeverity
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public sealed class EntryReport
    {
        public int partialOverlapCount;
        public int blocksCount;
        public int ambiguousOverlapCount;
        public readonly List<string> blockedBy = new List<string>();
        public readonly List<string> blocks = new List<string>();
        public readonly List<string> partials = new List<string>();
        public readonly List<string> ambiguous = new List<string>();

        public OverlapSeverity Severity
        {
            get
            {
                if (blockedBy.Count > 0)
                    return OverlapSeverity.Error;
                if (ambiguousOverlapCount > 0)
                    return OverlapSeverity.Warning;
                if (partialOverlapCount > 0 || blocksCount > 0)
                    return OverlapSeverity.Info;
                return OverlapSeverity.None;
            }
        }

        public string Summary
        {
            get
            {
                if (blockedBy.Count > 0)
                    return $"Blocked by: {blockedBy[0]}";
                if (ambiguousOverlapCount > 0)
                    return $"Overlap: {ambiguousOverlapCount} ambiguous";
                if (blocksCount > 0)
                    return $"Blocks: {blocksCount} entries";
                if (partialOverlapCount > 0)
                    return $"Overlap: {partialOverlapCount} partial";
                return "Overlap: none";
            }
        }
    }

    public static EntryReport[] AnalyzeProfile(TrickPoseProfileSO profile)
    {
        if (profile == null || profile.entries == null)
            return new EntryReport[0];

        int count = profile.entries.Count;
        EntryReport[] reports = new EntryReport[count];
        for (int i = 0; i < count; i++)
            reports[i] = new EntryReport();

        for (int i = 0; i < count; i++)
        {
            TrickPoseEntry a = profile.entries[i];
            if (a == null || !a.enabled)
                continue;

            for (int j = i + 1; j < count; j++)
            {
                TrickPoseEntry b = profile.entries[j];
                if (b == null || !b.enabled)
                    continue;

                if (!CanOverlap(a, b))
                    continue;

                bool aCoversB = Covers(a, b);
                bool bCoversA = Covers(b, a);

                if (a.priority > b.priority && aCoversB)
                {
                    reports[i].blocksCount++;
                    reports[i].blocks.Add(NameOf(b));
                    reports[j].blockedBy.Add(NameOf(a));
                    continue;
                }

                if (b.priority > a.priority && bCoversA)
                {
                    reports[j].blocksCount++;
                    reports[j].blocks.Add(NameOf(a));
                    reports[i].blockedBy.Add(NameOf(b));
                    continue;
                }

                if (a.priority == b.priority)
                {
                    reports[i].ambiguousOverlapCount++;
                    reports[j].ambiguousOverlapCount++;
                    reports[i].ambiguous.Add(NameOf(b));
                    reports[j].ambiguous.Add(NameOf(a));
                    continue;
                }

                reports[i].partialOverlapCount++;
                reports[j].partialOverlapCount++;
                reports[i].partials.Add(NameOf(b));
                reports[j].partials.Add(NameOf(a));
            }
        }

        return reports;
    }

    public static bool CanOverlap(TrickPoseEntry a, TrickPoseEntry b)
    {
        if (a == null || b == null)
            return false;

        return Overlaps(a.requiredPoseFamily, b.requiredPoseFamily, SkiController.AerialPoseFamily.None) &&
               Overlaps(a.requiredPoseShape, b.requiredPoseShape, SkiController.AerialPoseShape.None) &&
               Overlaps(a.requiredOrientationModifier, b.requiredOrientationModifier, SkiController.AerialOrientationModifier.None) &&
               Overlaps(a.requiredPoseName, b.requiredPoseName) &&
               Overlaps(a.requireAirborne, b.requireAirborne) &&
               Overlaps(a.requirePoseButtonHeld, b.requirePoseButtonHeld) &&
               Overlaps(a.requiredSpinDirection, b.requiredSpinDirection) &&
               Overlaps(a.requiredFlipDirection, b.requiredFlipDirection) &&
               Overlaps(a.yawAngularVelocityRange, b.yawAngularVelocityRange) &&
               Overlaps(a.pitchAngularVelocityRange, b.pitchAngularVelocityRange) &&
               Overlaps(a.rollAngularVelocityRange, b.rollAngularVelocityRange) &&
               Overlaps(a.totalAngularSpeedRange, b.totalAngularSpeedRange);
    }

    public static bool Covers(TrickPoseEntry broader, TrickPoseEntry narrower)
    {
        if (broader == null || narrower == null)
            return false;

        return Covers(broader.requiredPoseFamily, narrower.requiredPoseFamily, SkiController.AerialPoseFamily.None) &&
               Covers(broader.requiredPoseShape, narrower.requiredPoseShape, SkiController.AerialPoseShape.None) &&
               Covers(broader.requiredOrientationModifier, narrower.requiredOrientationModifier, SkiController.AerialOrientationModifier.None) &&
               Covers(broader.requiredPoseName, narrower.requiredPoseName) &&
               Covers(broader.requireAirborne, narrower.requireAirborne) &&
               Covers(broader.requirePoseButtonHeld, narrower.requirePoseButtonHeld) &&
               Covers(broader.requiredSpinDirection, narrower.requiredSpinDirection) &&
               Covers(broader.requiredFlipDirection, narrower.requiredFlipDirection) &&
               Covers(broader.yawAngularVelocityRange, narrower.yawAngularVelocityRange) &&
               Covers(broader.pitchAngularVelocityRange, narrower.pitchAngularVelocityRange) &&
               Covers(broader.rollAngularVelocityRange, narrower.rollAngularVelocityRange) &&
               Covers(broader.totalAngularSpeedRange, narrower.totalAngularSpeedRange);
    }

    private static string NameOf(TrickPoseEntry entry)
    {
        return entry != null ? entry.GetSummary() : "(null)";
    }

    private static bool Overlaps<T>(T a, T b, T wildcard) where T : struct
    {
        return EqualityComparer<T>.Default.Equals(a, wildcard) ||
               EqualityComparer<T>.Default.Equals(b, wildcard) ||
               EqualityComparer<T>.Default.Equals(a, b);
    }

    private static bool Covers<T>(T broader, T narrower, T wildcard) where T : struct
    {
        return EqualityComparer<T>.Default.Equals(broader, wildcard) ||
               (!EqualityComparer<T>.Default.Equals(narrower, wildcard) && EqualityComparer<T>.Default.Equals(broader, narrower));
    }

    private static bool Overlaps(string a, string b)
    {
        return string.IsNullOrWhiteSpace(a) ||
               string.IsNullOrWhiteSpace(b) ||
               string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool Covers(string broader, string narrower)
    {
        return string.IsNullOrWhiteSpace(broader) ||
               (!string.IsNullOrWhiteSpace(narrower) && string.Equals(broader, narrower, System.StringComparison.OrdinalIgnoreCase));
    }

    private static bool Overlaps(TrickPoseBoolRequirement a, TrickPoseBoolRequirement b)
    {
        return a == TrickPoseBoolRequirement.Ignore ||
               b == TrickPoseBoolRequirement.Ignore ||
               a == b;
    }

    private static bool Covers(TrickPoseBoolRequirement broader, TrickPoseBoolRequirement narrower)
    {
        return broader == TrickPoseBoolRequirement.Ignore ||
               (narrower != TrickPoseBoolRequirement.Ignore && broader == narrower);
    }

    private static bool Overlaps(TrickPoseSpinDirectionRequirement a, TrickPoseSpinDirectionRequirement b)
    {
        return a == TrickPoseSpinDirectionRequirement.Any ||
               b == TrickPoseSpinDirectionRequirement.Any ||
               a == b;
    }

    private static bool Covers(TrickPoseSpinDirectionRequirement broader, TrickPoseSpinDirectionRequirement narrower)
    {
        return broader == TrickPoseSpinDirectionRequirement.Any ||
               (narrower != TrickPoseSpinDirectionRequirement.Any && broader == narrower);
    }

    private static bool Overlaps(TrickPoseFlipDirectionRequirement a, TrickPoseFlipDirectionRequirement b)
    {
        return a == TrickPoseFlipDirectionRequirement.Any ||
               b == TrickPoseFlipDirectionRequirement.Any ||
               a == b;
    }

    private static bool Covers(TrickPoseFlipDirectionRequirement broader, TrickPoseFlipDirectionRequirement narrower)
    {
        return broader == TrickPoseFlipDirectionRequirement.Any ||
               (narrower != TrickPoseFlipDirectionRequirement.Any && broader == narrower);
    }

    private static bool Overlaps(TrickPoseAngularVelocityRange a, TrickPoseAngularVelocityRange b)
    {
        if (!a.enabled || !b.enabled)
            return true;

        Vector2 ar = a.GetSortedRange();
        Vector2 br = b.GetSortedRange();
        return ar.x <= br.y && br.x <= ar.y;
    }

    private static bool Covers(TrickPoseAngularVelocityRange broader, TrickPoseAngularVelocityRange narrower)
    {
        if (!broader.enabled)
            return true;

        if (!narrower.enabled)
            return false;

        Vector2 br = broader.GetSortedRange();
        Vector2 nr = narrower.GetSortedRange();
        return br.x <= nr.x && br.y >= nr.y;
    }
}
