using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SkiGame.Tricks
{
    public enum TrickRequirementFlavor
    {
        Any = 0,
        Technical = 10,
        Style = 20,
        Hybrid = 30
    }

    public enum TrickScoreEmphasis
    {
        Balanced = 0,
        Technical = 10,
        Style = 20
    }

    public enum TrickSpinRequirementDirection
    {
        Any = 0,
        Clockwise = 1,
        CounterClockwise = -1
    }

    public enum TrickFlipRequirementDirection
    {
        Any = 0,
        Frontflip = 1,
        Backflip = -1
    }

    [Serializable]
    public sealed class TrickRequirementDefinition
    {
        public TrickRequirementFlavor flavor = TrickRequirementFlavor.Any;
        [Min(0)] public int minimumSpinDegrees;
        public TrickSpinRequirementDirection requiredSpinDirection = TrickSpinRequirementDirection.Any;
        [Min(0)] public int minimumFlipCount;
        public TrickFlipRequirementDirection requiredFlipDirection = TrickFlipRequirementDirection.Any;
        public bool requiresGrind;
        public bool requiresSlide;
        public bool requiresValidAuthoredPose;
        public bool requiresPoseRotationCombo;
        public SkiController.AerialPoseFamily requiredPoseFamily = SkiController.AerialPoseFamily.None;
        public SkiController.AerialPoseShape requiredPoseShape = SkiController.AerialPoseShape.None;
        public SkiController.AerialOrientationModifier requiredOrientationModifier = SkiController.AerialOrientationModifier.None;
        public string explicitPoseLabel;
        public bool requiresSwitchLanding;
        public bool requiresNoseLanding;
        public bool requiresTailLanding;
        public string requiredZoneId;

        public bool IsEmpty()
        {
            return flavor == TrickRequirementFlavor.Any &&
                   minimumSpinDegrees <= 0 &&
                   requiredSpinDirection == TrickSpinRequirementDirection.Any &&
                   minimumFlipCount <= 0 &&
                   requiredFlipDirection == TrickFlipRequirementDirection.Any &&
                   !requiresGrind &&
                   !requiresSlide &&
                   !requiresValidAuthoredPose &&
                   !requiresPoseRotationCombo &&
                   requiredPoseFamily == SkiController.AerialPoseFamily.None &&
                   requiredPoseShape == SkiController.AerialPoseShape.None &&
                   requiredOrientationModifier == SkiController.AerialOrientationModifier.None &&
                   string.IsNullOrWhiteSpace(explicitPoseLabel) &&
                   !requiresSwitchLanding &&
                   !requiresNoseLanding &&
                   !requiresTailLanding &&
                   string.IsNullOrWhiteSpace(requiredZoneId);
        }

        public string BuildSummary()
        {
            List<string> parts = new List<string>();

            if (minimumSpinDegrees > 0)
                parts.Add($"{minimumSpinDegrees}+ spin");

            if (requiredSpinDirection != TrickSpinRequirementDirection.Any)
                parts.Add(requiredSpinDirection == TrickSpinRequirementDirection.Clockwise ? "clockwise" : "counter-clockwise");

            if (minimumFlipCount > 0)
                parts.Add($"{minimumFlipCount}+ flip");

            if (requiredFlipDirection != TrickFlipRequirementDirection.Any)
                parts.Add(requiredFlipDirection == TrickFlipRequirementDirection.Frontflip ? "frontflip" : "backflip");

            if (requiresGrind)
                parts.Add("grind");

            if (requiresSlide)
                parts.Add("slide");

            if (requiresValidAuthoredPose)
                parts.Add("authored pose");

            if (requiredPoseFamily != SkiController.AerialPoseFamily.None)
                parts.Add(requiredPoseFamily.ToString());

            if (requiredPoseShape != SkiController.AerialPoseShape.None)
                parts.Add(requiredPoseShape.ToString());

            if (requiredOrientationModifier != SkiController.AerialOrientationModifier.None)
                parts.Add(SkiController.GetAerialOrientationModifierLabel(requiredOrientationModifier));

            if (!string.IsNullOrWhiteSpace(explicitPoseLabel))
                parts.Add(explicitPoseLabel.Trim());

            if (requiresPoseRotationCombo)
                parts.Add("pose + rotation");

            if (requiresSwitchLanding)
                parts.Add("switch landing");

            if (requiresNoseLanding)
                parts.Add("nose landing");

            if (requiresTailLanding)
                parts.Add("tail landing");

            if (!string.IsNullOrWhiteSpace(requiredZoneId))
                parts.Add($"zone {requiredZoneId.Trim()}");

            if (parts.Count == 0)
                return "Any landed trick";

            return string.Join(", ", parts);
        }
    }

    public readonly struct TrickDescriptor
    {
        public readonly bool success;
        public readonly string displayName;
        public readonly int spinDegrees;
        public readonly SkierTrickTracker.SpinDirection spinDirection;
        public readonly int flipCount;
        public readonly bool frontflip;
        public readonly bool backflip;
        public readonly bool usedGrind;
        public readonly bool usedSlide;
        public readonly bool usedTuck;
        public readonly bool usedStyle;
        public readonly bool usedValidPose;
        public readonly bool poseOnly;
        public readonly bool poseRotationCombo;
        public readonly string primaryPoseLabel;
        public readonly string[] poseLabels;
        public readonly SkiController.AerialPoseFamily primaryPoseFamily;
        public readonly SkiController.AerialPoseFamily[] poseFamilies;
        public readonly SkiController.AerialPoseShape primaryPoseShape;
        public readonly SkiController.AerialPoseShape[] poseShapes;
        public readonly SkiController.AerialOrientationModifier primaryOrientationModifier;
        public readonly SkiController.AerialOrientationModifier[] orientationModifiers;
        public readonly bool switchLanding;
        public readonly bool noseLanding;
        public readonly bool tailLanding;
        public readonly bool hadBounceFollowup;
        public readonly bool hadTipBounceFollowup;
        public readonly int comboVariety;
        public readonly string zoneId;
        public readonly string canonicalSignature;

        public TrickDescriptor(
            bool success,
            string displayName,
            int spinDegrees,
            SkierTrickTracker.SpinDirection spinDirection,
            int flipCount,
            bool frontflip,
            bool backflip,
            bool usedGrind,
            bool usedSlide,
            bool usedTuck,
            bool usedStyle,
            bool usedValidPose,
            bool poseOnly,
            bool poseRotationCombo,
            string primaryPoseLabel,
            string[] poseLabels,
            SkiController.AerialPoseFamily primaryPoseFamily,
            SkiController.AerialPoseFamily[] poseFamilies,
            SkiController.AerialPoseShape primaryPoseShape,
            SkiController.AerialPoseShape[] poseShapes,
            SkiController.AerialOrientationModifier primaryOrientationModifier,
            SkiController.AerialOrientationModifier[] orientationModifiers,
            bool switchLanding,
            bool noseLanding,
            bool tailLanding,
            bool hadBounceFollowup,
            bool hadTipBounceFollowup,
            int comboVariety,
            string zoneId,
            string canonicalSignature)
        {
            this.success = success;
            this.displayName = displayName ?? string.Empty;
            this.spinDegrees = Mathf.Max(0, spinDegrees);
            this.spinDirection = spinDirection;
            this.flipCount = Mathf.Max(0, flipCount);
            this.frontflip = frontflip;
            this.backflip = backflip;
            this.usedGrind = usedGrind;
            this.usedSlide = usedSlide;
            this.usedTuck = usedTuck;
            this.usedStyle = usedStyle;
            this.usedValidPose = usedValidPose;
            this.poseOnly = poseOnly;
            this.poseRotationCombo = poseRotationCombo;
            this.primaryPoseLabel = primaryPoseLabel ?? string.Empty;
            this.poseLabels = poseLabels ?? Array.Empty<string>();
            this.primaryPoseFamily = primaryPoseFamily;
            this.poseFamilies = poseFamilies ?? Array.Empty<SkiController.AerialPoseFamily>();
            this.primaryPoseShape = primaryPoseShape;
            this.poseShapes = poseShapes ?? Array.Empty<SkiController.AerialPoseShape>();
            this.primaryOrientationModifier = primaryOrientationModifier;
            this.orientationModifiers = orientationModifiers ?? Array.Empty<SkiController.AerialOrientationModifier>();
            this.switchLanding = switchLanding;
            this.noseLanding = noseLanding;
            this.tailLanding = tailLanding;
            this.hadBounceFollowup = hadBounceFollowup;
            this.hadTipBounceFollowup = hadTipBounceFollowup;
            this.comboVariety = Mathf.Max(1, comboVariety);
            this.zoneId = zoneId ?? string.Empty;
            this.canonicalSignature = canonicalSignature ?? string.Empty;
        }
    }

    public readonly struct TrickScoreBreakdown
    {
        public readonly int rawScore;
        public readonly int finalScore;
        public readonly int technicalScore;
        public readonly int styleScore;
        public readonly int landingScore;
        public readonly int varietyScore;
        public readonly int integrationScore;
        public readonly float repetitionPenalty01;

        public TrickScoreBreakdown(
            int rawScore,
            int finalScore,
            int technicalScore,
            int styleScore,
            int landingScore,
            int varietyScore,
            int integrationScore,
            float repetitionPenalty01)
        {
            this.rawScore = Mathf.Max(0, rawScore);
            this.finalScore = Mathf.Max(0, finalScore);
            this.technicalScore = Mathf.Max(0, technicalScore);
            this.styleScore = Mathf.Max(0, styleScore);
            this.landingScore = Mathf.Max(0, landingScore);
            this.varietyScore = Mathf.Max(0, varietyScore);
            this.integrationScore = Mathf.Max(0, integrationScore);
            this.repetitionPenalty01 = Mathf.Clamp01(repetitionPenalty01);
        }
    }

    public sealed class TrickScoreHistory
    {
        private readonly Dictionary<string, int> _signatureCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public void Register(TrickDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor.canonicalSignature))
                return;

            _signatureCounts.TryGetValue(descriptor.canonicalSignature, out int count);
            _signatureCounts[descriptor.canonicalSignature] = count + 1;
        }

        public float GetPenalty01(TrickDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor.canonicalSignature))
                return 0f;

            _signatureCounts.TryGetValue(descriptor.canonicalSignature, out int count);
            return count switch
            {
                <= 0 => 0f,
                1 => 0.12f,
                2 => 0.24f,
                3 => 0.36f,
                _ => 0.48f
            };
        }

        public void Clear()
        {
            _signatureCounts.Clear();
        }
    }

    public static class TrickActivityRules
    {
        public static TrickDescriptor CreateDescriptor(SkierTrickTracker.TrickResult result)
        {
            return new TrickDescriptor(
                result.success,
                result.displayName,
                result.spinDegrees,
                result.spinDirection,
                result.flipCount,
                result.frontflip,
                result.backflip,
                result.usedGrind,
                result.usedSlide,
                result.usedTuck,
                result.usedStyle,
                result.usedValidPose,
                result.poseOnly,
                result.poseRotationCombo,
                result.primaryPoseLabel,
                result.poseLabels,
                result.primaryPoseFamily,
                result.poseFamilies,
                result.primaryPoseShape,
                result.poseShapes,
                result.primaryOrientationModifier,
                result.orientationModifiers,
                result.switchLanding,
                result.noseLanding,
                result.tailLanding,
                result.hadBounceFollowup,
                result.hadTipBounceFollowup,
                result.comboVariety,
                result.zoneId,
                result.canonicalSignature);
        }

        public static TrickDescriptor CreateDescriptorFromSignalData(Progression.QuestSignalData data)
        {
            if (data == null)
                return default;

            return new TrickDescriptor(
                ParseBool(data.GetTag("success")),
                data.GetTag("name"),
                ParseInt(data.GetTag("spinDegrees")),
                ParseSpinDirection(data.GetTag("spinDirection")),
                ParseInt(data.GetTag("flipCount")),
                ParseBool(data.GetTag("frontflip")),
                ParseBool(data.GetTag("backflip")),
                ParseBool(data.GetTag("usedGrind")),
                ParseBool(data.GetTag("usedSlide")),
                ParseBool(data.GetTag("usedTuck")),
                ParseBool(data.GetTag("usedStyle")),
                ParseBool(data.GetTag("usedValidPose")),
                ParseBool(data.GetTag("poseOnly")),
                ParseBool(data.GetTag("poseRotationCombo")),
                data.GetTag("primaryPoseLabel"),
                SplitList(data.GetTag("poseLabels")),
                ParsePoseFamily(data.GetTag("primaryPoseFamily")),
                ParsePoseFamilies(data.GetTag("poseFamilies")),
                ParsePoseShape(data.GetTag("primaryPoseShape")),
                ParsePoseShapes(data.GetTag("poseShapes")),
                ParseOrientation(data.GetTag("primaryOrientation")),
                ParseOrientations(data.GetTag("orientations")),
                ParseBool(data.GetTag("switchLanding")),
                ParseBool(data.GetTag("noseLanding")),
                ParseBool(data.GetTag("tailLanding")),
                ParseBool(data.GetTag("hadBounce")),
                ParseBool(data.GetTag("hadTipBounce")),
                Mathf.Max(1, ParseInt(data.GetTag("comboVariety"))),
                data.GetTag("zoneId"),
                data.GetTag("signature"));
        }

        public static bool Matches(TrickRequirementDefinition requirement, TrickDescriptor descriptor)
        {
            if (requirement == null || requirement.IsEmpty())
                return descriptor.success;

            if (!descriptor.success)
                return false;

            if (requirement.minimumSpinDegrees > 0 && descriptor.spinDegrees < requirement.minimumSpinDegrees)
                return false;

            if (requirement.requiredSpinDirection != TrickSpinRequirementDirection.Any &&
                descriptor.spinDirection != ToSpinDirection(requirement.requiredSpinDirection))
                return false;

            if (requirement.minimumFlipCount > 0 && descriptor.flipCount < requirement.minimumFlipCount)
                return false;

            if (requirement.requiredFlipDirection != TrickFlipRequirementDirection.Any)
            {
                if (requirement.requiredFlipDirection == TrickFlipRequirementDirection.Frontflip && !descriptor.frontflip)
                    return false;

                if (requirement.requiredFlipDirection == TrickFlipRequirementDirection.Backflip && !descriptor.backflip)
                    return false;
            }

            if (requirement.requiresGrind && !descriptor.usedGrind)
                return false;

            if (requirement.requiresSlide && !descriptor.usedSlide)
                return false;

            if (requirement.requiresValidAuthoredPose && !descriptor.usedValidPose)
                return false;

            if (requirement.requiresPoseRotationCombo && !descriptor.poseRotationCombo)
                return false;

            if (requirement.requiredPoseFamily != SkiController.AerialPoseFamily.None &&
                !Contains(descriptor.poseFamilies, requirement.requiredPoseFamily))
                return false;

            if (requirement.requiredPoseShape != SkiController.AerialPoseShape.None &&
                !Contains(descriptor.poseShapes, requirement.requiredPoseShape))
                return false;

            if (requirement.requiredOrientationModifier != SkiController.AerialOrientationModifier.None &&
                !Contains(descriptor.orientationModifiers, requirement.requiredOrientationModifier))
                return false;

            if (!string.IsNullOrWhiteSpace(requirement.explicitPoseLabel) &&
                !ContainsIgnoreCase(descriptor.poseLabels, requirement.explicitPoseLabel))
                return false;

            if (requirement.requiresSwitchLanding && !descriptor.switchLanding)
                return false;

            if (requirement.requiresNoseLanding && !descriptor.noseLanding)
                return false;

            if (requirement.requiresTailLanding && !descriptor.tailLanding)
                return false;

            if (!string.IsNullOrWhiteSpace(requirement.requiredZoneId) &&
                !string.Equals(descriptor.zoneId ?? string.Empty, requirement.requiredZoneId.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;

            return requirement.flavor switch
            {
                TrickRequirementFlavor.Technical => descriptor.spinDegrees > 0 || descriptor.flipCount > 0 || descriptor.usedGrind || descriptor.usedSlide,
                TrickRequirementFlavor.Style => descriptor.usedValidPose || descriptor.switchLanding || descriptor.noseLanding || descriptor.tailLanding,
                TrickRequirementFlavor.Hybrid => descriptor.poseRotationCombo || ((descriptor.usedValidPose || descriptor.usedSlide || descriptor.usedGrind) && (descriptor.spinDegrees > 0 || descriptor.flipCount > 0)),
                _ => true
            };
        }

        public static TrickScoreBreakdown Score(TrickDescriptor descriptor, TrickScoreHistory history, TrickScoreEmphasis emphasis = TrickScoreEmphasis.Balanced)
        {
            if (!descriptor.success)
                return new TrickScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0f);

            int spinScore = Mathf.RoundToInt(descriptor.spinDegrees / 45f);
            int flipScore = descriptor.flipCount * 70;
            int technicalScore = spinScore + flipScore;

            if (descriptor.usedGrind)
                technicalScore += descriptor.noseLanding || descriptor.tailLanding ? 55 : 45;

            if (descriptor.usedSlide)
                technicalScore += descriptor.noseLanding || descriptor.tailLanding ? 42 : 34;

            int styleScore = 0;
            if (descriptor.usedValidPose)
            {
                styleScore += descriptor.poseOnly ? 28 : 44;
                styleScore += descriptor.primaryPoseShape switch
                {
                    SkiController.AerialPoseShape.Compact => 12,
                    SkiController.AerialPoseShape.Driving => 14,
                    SkiController.AerialPoseShape.LaidOut => 18,
                    _ => 8
                };
            }

            if (descriptor.poseRotationCombo)
                styleScore += 26;

            styleScore += descriptor.primaryOrientationModifier switch
            {
                SkiController.AerialOrientationModifier.Inverted => 36,
                SkiController.AerialOrientationModifier.ChestDown => 30,
                SkiController.AerialOrientationModifier.ChestUp => 26,
                SkiController.AerialOrientationModifier.OnSide => 24,
                SkiController.AerialOrientationModifier.Sideways => 24,
                SkiController.AerialOrientationModifier.Switch => 18,
                SkiController.AerialOrientationModifier.Rising => 12,
                SkiController.AerialOrientationModifier.Diving => 12,
                _ => 0
            };

            int landingScore = 0;
            if (descriptor.switchLanding)
                landingScore += 20;
            if (descriptor.noseLanding)
                landingScore += 26;
            if (descriptor.tailLanding)
                landingScore += 26;
            if (descriptor.hadBounceFollowup)
                landingScore += descriptor.hadTipBounceFollowup ? 18 : 10;

            int varietyScore = Mathf.Max(0, descriptor.comboVariety - 1) * 14;
            int integrationScore = 0;

            if (descriptor.usedValidPose && (descriptor.usedGrind || descriptor.usedSlide))
                integrationScore += 14;

            if ((descriptor.usedGrind || descriptor.usedSlide) && (descriptor.spinDegrees > 0 || descriptor.flipCount > 0))
                integrationScore += 18;

            if (descriptor.poseRotationCombo)
                integrationScore += 12;

            int raw = technicalScore + styleScore + landingScore + varietyScore + integrationScore;

            raw = emphasis switch
            {
                TrickScoreEmphasis.Technical => Mathf.RoundToInt(raw + technicalScore * 0.15f - styleScore * 0.05f),
                TrickScoreEmphasis.Style => Mathf.RoundToInt(raw + styleScore * 0.18f - technicalScore * 0.05f),
                _ => raw
            };

            raw = Mathf.Max(raw, descriptor.poseOnly ? 40 : 55);

            float penalty01 = history != null ? history.GetPenalty01(descriptor) : 0f;
            int final = Mathf.Max(10, Mathf.RoundToInt(raw * (1f - penalty01)));

            return new TrickScoreBreakdown(raw, final, technicalScore, styleScore, landingScore, varietyScore, integrationScore, penalty01);
        }

        public static string BuildCanonicalSignature(TrickDescriptor descriptor)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(descriptor.spinDegrees);
            sb.Append('|');
            sb.Append(descriptor.spinDirection);
            sb.Append('|');
            sb.Append(descriptor.flipCount);
            sb.Append('|');
            sb.Append(descriptor.frontflip ? 'F' : descriptor.backflip ? 'B' : 'N');
            sb.Append('|');
            sb.Append(descriptor.usedGrind ? 'G' : 'g');
            sb.Append(descriptor.usedSlide ? 'S' : 's');
            sb.Append(descriptor.usedValidPose ? 'P' : 'p');
            sb.Append(descriptor.poseRotationCombo ? 'R' : 'r');
            sb.Append('|');
            sb.Append(descriptor.primaryPoseFamily);
            sb.Append('|');
            sb.Append(descriptor.primaryPoseShape);
            sb.Append('|');
            sb.Append(descriptor.primaryOrientationModifier);
            sb.Append('|');
            sb.Append((descriptor.primaryPoseLabel ?? string.Empty).Trim().ToLowerInvariant());
            sb.Append('|');
            sb.Append(descriptor.switchLanding ? 'W' : 'w');
            sb.Append(descriptor.noseLanding ? 'N' : 'n');
            sb.Append(descriptor.tailLanding ? 'T' : 't');
            sb.Append('|');
            sb.Append(descriptor.hadTipBounceFollowup ? "tip" : descriptor.hadBounceFollowup ? "bounce" : "none");
            return sb.ToString();
        }

        public static string JoinList<T>(IEnumerable<T> values)
        {
            if (values == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (T value in values)
            {
                string text = value?.ToString();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (sb.Length > 0)
                    sb.Append('|');

                sb.Append(text.Trim());
            }

            return sb.ToString();
        }

        private static bool Contains<T>(T[] values, T target) where T : struct, Enum
        {
            if (values == null || values.Length == 0)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(values[i], target))
                    return true;
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string[] values, string target)
        {
            if (values == null || values.Length == 0 || string.IsNullOrWhiteSpace(target))
                return false;

            string trimmedTarget = target.Trim();
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i]?.Trim(), trimmedTarget, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string[] SplitList(string joined)
        {
            return string.IsNullOrWhiteSpace(joined)
                ? Array.Empty<string>()
                : joined.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
        }

        private static bool ParseBool(string value)
        {
            return bool.TryParse(value, out bool parsed) && parsed;
        }

        private static SkierTrickTracker.SpinDirection ParseSpinDirection(string value)
        {
            return Enum.TryParse(value, true, out SkierTrickTracker.SpinDirection parsed)
                ? parsed
                : SkierTrickTracker.SpinDirection.None;
        }

        private static SkiController.AerialPoseFamily ParsePoseFamily(string value)
        {
            return Enum.TryParse(value, true, out SkiController.AerialPoseFamily parsed)
                ? parsed
                : SkiController.AerialPoseFamily.None;
        }

        private static SkiController.AerialPoseFamily[] ParsePoseFamilies(string value)
        {
            string[] parts = SplitList(value);
            List<SkiController.AerialPoseFamily> result = new List<SkiController.AerialPoseFamily>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (Enum.TryParse(parts[i], true, out SkiController.AerialPoseFamily parsed) &&
                    parsed != SkiController.AerialPoseFamily.None)
                {
                    result.Add(parsed);
                }
            }

            return result.ToArray();
        }

        private static SkiController.AerialPoseShape ParsePoseShape(string value)
        {
            return Enum.TryParse(value, true, out SkiController.AerialPoseShape parsed)
                ? parsed
                : SkiController.AerialPoseShape.None;
        }

        private static SkiController.AerialPoseShape[] ParsePoseShapes(string value)
        {
            string[] parts = SplitList(value);
            List<SkiController.AerialPoseShape> result = new List<SkiController.AerialPoseShape>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (Enum.TryParse(parts[i], true, out SkiController.AerialPoseShape parsed) &&
                    parsed != SkiController.AerialPoseShape.None)
                {
                    result.Add(parsed);
                }
            }

            return result.ToArray();
        }

        private static SkiController.AerialOrientationModifier ParseOrientation(string value)
        {
            return Enum.TryParse(value, true, out SkiController.AerialOrientationModifier parsed)
                ? parsed
                : SkiController.AerialOrientationModifier.None;
        }

        private static SkiController.AerialOrientationModifier[] ParseOrientations(string value)
        {
            string[] parts = SplitList(value);
            List<SkiController.AerialOrientationModifier> result = new List<SkiController.AerialOrientationModifier>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                if (Enum.TryParse(parts[i], true, out SkiController.AerialOrientationModifier parsed) &&
                    parsed != SkiController.AerialOrientationModifier.None)
                {
                    result.Add(parsed);
                }
            }

            return result.ToArray();
        }

        private static SkierTrickTracker.SpinDirection ToSpinDirection(TrickSpinRequirementDirection direction)
        {
            return direction switch
            {
                TrickSpinRequirementDirection.Clockwise => SkierTrickTracker.SpinDirection.Clockwise,
                TrickSpinRequirementDirection.CounterClockwise => SkierTrickTracker.SpinDirection.CounterClockwise,
                _ => SkierTrickTracker.SpinDirection.None
            };
        }
    }
}
