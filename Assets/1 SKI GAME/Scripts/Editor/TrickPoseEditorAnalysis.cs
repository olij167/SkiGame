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

    private enum LegacyOrientationKind
    {
        None = 0,
        Switch = 1,
        Sideways = 2,
        AnySide = 3
    }

    private struct EffectiveConditions
    {
        public TrickPoseVerticalOrientationRequirement vertical;
        public TrickPoseHorizontalOrientationRequirement horizontal;
        public TrickPoseMotionStateRequirement motion;
        public LegacyOrientationKind legacyKind;
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
                }
                else
                {
                    reports[i].partialOverlapCount++;
                    reports[j].partialOverlapCount++;
                    reports[i].partials.Add(NameOf(b));
                    reports[j].partials.Add(NameOf(a));
                }
            }
        }

        return reports;
    }

    public static bool CanOverlap(TrickPoseEntry a, TrickPoseEntry b)
    {
        if (a == null || b == null)
            return false;

        EffectiveConditions effectiveA = BuildEffectiveConditions(a);
        EffectiveConditions effectiveB = BuildEffectiveConditions(b);

        return Overlaps(a.requiredPoseFamily, b.requiredPoseFamily, SkiController.AerialPoseFamily.None) &&
               Overlaps(a.requiredPoseShape, b.requiredPoseShape, SkiController.AerialPoseShape.None) &&
               Overlaps(effectiveA.vertical, effectiveB.vertical) &&
               Overlaps(effectiveA.horizontal, effectiveB.horizontal, effectiveA.legacyKind, effectiveB.legacyKind) &&
               Overlaps(effectiveA.motion, effectiveB.motion) &&
               OverlapsLegacyKinds(effectiveA.legacyKind, effectiveB.legacyKind) &&
               Overlaps(a.requiredPoseName, b.requiredPoseName) &&
               Overlaps(a.requireAirborne, b.requireAirborne) &&
               Overlaps(a.requirePoseButtonHeld, b.requirePoseButtonHeld) &&
               OverlapsAdvancedConditions(a, b) &&
               Overlaps(a.entryPitchAngleRange, b.entryPitchAngleRange) &&
               Overlaps(a.entryYawAngleRange, b.entryYawAngleRange) &&
               Overlaps(a.entryRollAngleRange, b.entryRollAngleRange);
    }

    public static bool Covers(TrickPoseEntry broader, TrickPoseEntry narrower)
    {
        if (broader == null || narrower == null)
            return false;

        EffectiveConditions broad = BuildEffectiveConditions(broader);
        EffectiveConditions narrow = BuildEffectiveConditions(narrower);

        return Covers(broader.requiredPoseFamily, narrower.requiredPoseFamily, SkiController.AerialPoseFamily.None) &&
               Covers(broader.requiredPoseShape, narrower.requiredPoseShape, SkiController.AerialPoseShape.None) &&
               Covers(broad.vertical, narrow.vertical) &&
               Covers(broad.horizontal, narrow.horizontal, broad.legacyKind, narrow.legacyKind) &&
               Covers(broad.motion, narrow.motion) &&
               CoversLegacyKinds(broad.legacyKind, narrow.legacyKind) &&
               Covers(broader.requiredPoseName, narrower.requiredPoseName) &&
               Covers(broader.requireAirborne, narrower.requireAirborne) &&
               Covers(broader.requirePoseButtonHeld, narrower.requirePoseButtonHeld) &&
               CoversAdvancedConditions(broader, narrower) &&
               Covers(broader.entryPitchAngleRange, narrower.entryPitchAngleRange) &&
               Covers(broader.entryYawAngleRange, narrower.entryYawAngleRange) &&
               Covers(broader.entryRollAngleRange, narrower.entryRollAngleRange);
    }

    private static string NameOf(TrickPoseEntry entry)
    {
        return entry != null ? entry.GetSummary() : "(null)";
    }

    private static EffectiveConditions BuildEffectiveConditions(TrickPoseEntry entry)
    {
        EffectiveConditions result = new EffectiveConditions
        {
            vertical = entry.requiredVerticalOrientation,
            horizontal = entry.requiredHorizontalOrientation,
            motion = entry.requiredMotionState,
            legacyKind = LegacyOrientationKind.None
        };

        if (!UsesLegacyOrientationModifier(entry))
            return result;

        switch (entry.requiredOrientationModifier)
        {
            case SkiController.AerialOrientationModifier.Inverted:
                result.vertical = TrickPoseVerticalOrientationRequirement.Inverted;
                break;
            case SkiController.AerialOrientationModifier.ChestDown:
                result.vertical = TrickPoseVerticalOrientationRequirement.ChestDown;
                break;
            case SkiController.AerialOrientationModifier.ChestUp:
                result.vertical = TrickPoseVerticalOrientationRequirement.ChestUp;
                break;
            case SkiController.AerialOrientationModifier.OnSide:
                result.legacyKind = LegacyOrientationKind.AnySide;
                break;
            case SkiController.AerialOrientationModifier.Rising:
                result.motion = TrickPoseMotionStateRequirement.Rising;
                break;
            case SkiController.AerialOrientationModifier.Diving:
                result.motion = TrickPoseMotionStateRequirement.Diving;
                break;
            case SkiController.AerialOrientationModifier.Switch:
                result.legacyKind = LegacyOrientationKind.Switch;
                break;
            case SkiController.AerialOrientationModifier.Sideways:
                result.legacyKind = LegacyOrientationKind.Sideways;
                break;
        }

        return result;
    }

    private static bool UsesLegacyOrientationModifier(TrickPoseEntry entry)
    {
        return entry != null &&
               entry.requiredOrientationModifier != SkiController.AerialOrientationModifier.None &&
               entry.requiredVerticalOrientation == TrickPoseVerticalOrientationRequirement.Any &&
               entry.requiredHorizontalOrientation == TrickPoseHorizontalOrientationRequirement.Any &&
               entry.requiredMotionState == TrickPoseMotionStateRequirement.Any;
    }

    private static bool OverlapsAdvancedConditions(TrickPoseEntry a, TrickPoseEntry b)
    {
        if (!a.useAdvancedModifierConditions || !b.useAdvancedModifierConditions)
            return true;

        return Overlaps(a.requiredSpinDirection, b.requiredSpinDirection) &&
               Overlaps(a.requiredFlipDirection, b.requiredFlipDirection) &&
               Overlaps(a.yawAngularVelocityRange, b.yawAngularVelocityRange) &&
               Overlaps(a.pitchAngularVelocityRange, b.pitchAngularVelocityRange) &&
               Overlaps(a.rollAngularVelocityRange, b.rollAngularVelocityRange) &&
               Overlaps(a.totalAngularSpeedRange, b.totalAngularSpeedRange);
    }

    private static bool CoversAdvancedConditions(TrickPoseEntry broader, TrickPoseEntry narrower)
    {
        if (!broader.useAdvancedModifierConditions)
            return true;
        if (!narrower.useAdvancedModifierConditions)
            return false;

        return Covers(broader.requiredSpinDirection, narrower.requiredSpinDirection) &&
               Covers(broader.requiredFlipDirection, narrower.requiredFlipDirection) &&
               Covers(broader.yawAngularVelocityRange, narrower.yawAngularVelocityRange) &&
               Covers(broader.pitchAngularVelocityRange, narrower.pitchAngularVelocityRange) &&
               Covers(broader.rollAngularVelocityRange, narrower.rollAngularVelocityRange) &&
               Covers(broader.totalAngularSpeedRange, narrower.totalAngularSpeedRange);
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

    private static bool Overlaps(TrickPoseVerticalOrientationRequirement a, TrickPoseVerticalOrientationRequirement b)
    {
        return Overlaps(a, b, TrickPoseVerticalOrientationRequirement.Any);
    }

    private static bool Covers(TrickPoseVerticalOrientationRequirement broader, TrickPoseVerticalOrientationRequirement narrower)
    {
        return Covers(broader, narrower, TrickPoseVerticalOrientationRequirement.Any);
    }

    private static bool Overlaps(TrickPoseHorizontalOrientationRequirement a, TrickPoseHorizontalOrientationRequirement b, LegacyOrientationKind legacyA, LegacyOrientationKind legacyB)
    {
        bool aAnySide = legacyA == LegacyOrientationKind.AnySide;
        bool bAnySide = legacyB == LegacyOrientationKind.AnySide;
        if (aAnySide && bAnySide)
            return true;
        if (aAnySide)
            return b == TrickPoseHorizontalOrientationRequirement.Any ||
                   b == TrickPoseHorizontalOrientationRequirement.LeftSide ||
                   b == TrickPoseHorizontalOrientationRequirement.RightSide;
        if (bAnySide)
            return a == TrickPoseHorizontalOrientationRequirement.Any ||
                   a == TrickPoseHorizontalOrientationRequirement.LeftSide ||
                   a == TrickPoseHorizontalOrientationRequirement.RightSide;
        return Overlaps(a, b, TrickPoseHorizontalOrientationRequirement.Any);
    }

    private static bool Covers(TrickPoseHorizontalOrientationRequirement broader, TrickPoseHorizontalOrientationRequirement narrower, LegacyOrientationKind broadLegacy, LegacyOrientationKind narrowLegacy)
    {
        bool broaderAnySide = broadLegacy == LegacyOrientationKind.AnySide;
        bool narrowerAnySide = narrowLegacy == LegacyOrientationKind.AnySide;
        if (broaderAnySide)
            return narrowerAnySide ||
                   narrower == TrickPoseHorizontalOrientationRequirement.LeftSide ||
                   narrower == TrickPoseHorizontalOrientationRequirement.RightSide;
        if (narrowerAnySide)
            return false;
        return Covers(broader, narrower, TrickPoseHorizontalOrientationRequirement.Any);
    }

    private static bool Overlaps(TrickPoseMotionStateRequirement a, TrickPoseMotionStateRequirement b)
    {
        return Overlaps(a, b, TrickPoseMotionStateRequirement.Any);
    }

    private static bool Covers(TrickPoseMotionStateRequirement broader, TrickPoseMotionStateRequirement narrower)
    {
        return Covers(broader, narrower, TrickPoseMotionStateRequirement.Any);
    }

    private static bool OverlapsLegacyKinds(LegacyOrientationKind a, LegacyOrientationKind b)
    {
        if (a == LegacyOrientationKind.None || b == LegacyOrientationKind.None)
            return true;
        if (a == LegacyOrientationKind.AnySide || b == LegacyOrientationKind.AnySide)
            return a == b;
        return a == b;
    }

    private static bool CoversLegacyKinds(LegacyOrientationKind broader, LegacyOrientationKind narrower)
    {
        if (broader == LegacyOrientationKind.None)
            return true;
        if (narrower == LegacyOrientationKind.None)
            return false;
        if (broader == LegacyOrientationKind.AnySide)
            return narrower == LegacyOrientationKind.AnySide;
        return broader == narrower;
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

    private static bool Overlaps(TrickPoseEulerAngleRange a, TrickPoseEulerAngleRange b)
    {
        if (!a.enabled || !b.enabled)
            return true;

        Vector2 ar = a.GetSortedRange();
        Vector2 br = b.GetSortedRange();
        return ar.x <= br.y && br.x <= ar.y;
    }

    private static bool Covers(TrickPoseEulerAngleRange broader, TrickPoseEulerAngleRange narrower)
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
