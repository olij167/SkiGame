using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    public static class PaletteGeneratorUtility
    {
        private static readonly PaletteSwatchRole[] DefaultRoleOrder =
        {
            PaletteSwatchRole.Background,
            PaletteSwatchRole.Panel,
            PaletteSwatchRole.Text,
            PaletteSwatchRole.MutedText,
            PaletteSwatchRole.Accent,
            PaletteSwatchRole.AccentSecondary,
            PaletteSwatchRole.Highlight,
            PaletteSwatchRole.Warning,
            PaletteSwatchRole.Success,
            PaletteSwatchRole.Error,
            PaletteSwatchRole.Outline,
            PaletteSwatchRole.Shadow
        };

        public static List<PaletteSwatch> Generate(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches = null)
        {
            return GenerateResult(settings, currentSwatches).CloneSwatches();
        }

        public static PaletteGenerationResult GenerateResult(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches = null, string label = null)
        {
            return GenerateResult(settings, currentSwatches, label, null);
        }

        public static PaletteGenerationResult GenerateResult(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, string label, PaletteGenerationSuggestion suggestion)
        {
            settings = settings != null ? settings.Clone() : new PaletteGenerationSettings();
            if (suggestion != null && suggestion.effectiveSettings != null)
            {
                int requestedCount = settings.targetSwatchCount;
                bool preserveLocked = settings.preserveLockedSwatches;
                settings = suggestion.effectiveSettings.Clone();
                settings.targetSwatchCount = requestedCount;
                settings.preserveLockedSwatches = preserveLocked;
            }
            settings.Clamp();

            PaletteGenerationDiagnostics diagnostics = CreateDiagnostics(settings, currentSwatches);
            ApplySuggestionDiagnostics(diagnostics, suggestion);
            List<PaletteSwatch> swatches = GenerateSwatches(settings, currentSwatches, diagnostics, suggestion);
            diagnostics.generatedCount = swatches.Count;

            var variant = new PaletteGenerationVariant
            {
                id = Guid.NewGuid().ToString("N"),
                label = string.IsNullOrWhiteSpace(label) ? BuildVariantLabel(settings, diagnostics) : label,
                createdUtc = DateTime.UtcNow.ToString("u"),
                settings = settings.Clone(),
                diagnostics = diagnostics,
                swatches = swatches
            };

            return new PaletteGenerationResult
            {
                success = swatches.Count > 0,
                message = swatches.Count > 0 ? $"Generated {swatches.Count} swatch(es)." : "No swatches generated.",
                variant = variant
            };
        }

        public static List<PaletteSwatch> RegenerateUnlocked(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches)
        {
            return RegenerateUnlockedResult(settings, currentSwatches).CloneSwatches();
        }

        public static PaletteGenerationResult RegenerateUnlockedResult(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, string label = "Regenerated Unlocked")
        {
            return RegenerateUnlockedResult(settings, currentSwatches, label, null);
        }

        public static PaletteGenerationResult RegenerateUnlockedResult(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, string label, PaletteGenerationSuggestion suggestion)
        {
            settings = settings != null ? settings.Clone() : new PaletteGenerationSettings();
            settings.preserveLockedSwatches = true;
            settings.targetSwatchCount = currentSwatches != null && currentSwatches.Count > 0 ? currentSwatches.Count : settings.targetSwatchCount;
            if (suggestion != null && suggestion.effectiveSettings != null)
            {
                suggestion = CloneSuggestionForExecution(suggestion);
                suggestion.effectiveSettings.preserveLockedSwatches = true;
                suggestion.effectiveSettings.targetSwatchCount = settings.targetSwatchCount;
            }
            return GenerateResult(settings, currentSwatches, label, suggestion);
        }

        public static PaletteGenerationSuggestion SuggestFromLockedSwatches(PaletteGenerationSettings baseSettings, IList<PaletteSwatch> swatches)
        {
            baseSettings = baseSettings != null ? baseSettings.Clone() : new PaletteGenerationSettings();
            baseSettings.Clamp();

            var suggestion = new PaletteGenerationSuggestion
            {
                autoContextAvailable = true,
                requestedHarmonyMode = baseSettings.harmonyMode,
                chosenHarmonyMode = baseSettings.harmonyMode,
                anchorHue = Mathf.Repeat((baseSettings.hueRange.x + baseSettings.hueRange.y) * 0.5f, 1f),
                effectiveSettings = baseSettings.Clone(),
                diagnostics = new PaletteGenerationDiagnostics
                {
                    requestedCount = Mathf.Clamp(baseSettings.targetSwatchCount, 1, 64),
                    harmonyMode = baseSettings.harmonyMode,
                    usedSeed = baseSettings.useSeed,
                    seed = baseSettings.seed
                }
            };

            CollectLockedProfiles(swatches, suggestion.lockedProfiles);
            CollectUnlockedCoverageProfiles(swatches, suggestion.softProfiles);
            suggestion.softCoverageCount = suggestion.softProfiles.Count;
            suggestion.lockedCount = suggestion.lockedProfiles.Count;
            for (int i = 0; i < suggestion.lockedProfiles.Count; i++)
            {
                if (suggestion.lockedProfiles[i].colourful)
                    suggestion.colourfulLockedCount++;
            }

            if (suggestion.colourfulLockedCount == 0)
            {
                suggestion.openContext = true;
                suggestion.effectiveSettings.hueRange = new Vector2(0f, 1f);
                suggestion.effectiveSettings.saturationRange = ExpandRange(baseSettings.saturationRange, 0.28f, 0.96f, 0.72f);
                suggestion.effectiveSettings.valueRange = ExpandRange(baseSettings.valueRange, 0.16f, 0.98f, 0.72f);
                suggestion.effectiveSettings.randomVariation = Mathf.Clamp01(Mathf.Max(baseSettings.randomVariation, 0.42f));
                suggestion.diagnostics.AddNote("Contextual generation is open: lock swatches to guide the next pass.");
                if (suggestion.softCoverageCount > 0)
                    suggestion.diagnostics.AddNote($"Using {suggestion.softCoverageCount} current unlocked colour(s) as soft coverage so the next pass explores new ground.");
                suggestion.targetHueBands.Add(new PaletteHueBand("Open spectrum", 0.5f, 1f, 1f));
                ReweightTargetHueBandsFromCoverage(suggestion.targetHueBands, suggestion.softProfiles, suggestion.effectiveSettings.randomVariation, suggestion.diagnostics);
                return suggestion;
            }

            bool forceRequestedHarmony = baseSettings.harmonyMode != ColourHarmonyMode.Contextual;
            PaletteHarmonyFit fit = forceRequestedHarmony
                ? ScoreForcedHarmonyMode(baseSettings.harmonyMode, baseSettings, suggestion.lockedProfiles, suggestion.harmonyScores)
                : ChooseHarmonyFromLocks(baseSettings, suggestion.lockedProfiles, suggestion.harmonyScores);
            suggestion.anchorHue = fit.anchorHue;
            suggestion.chosenHarmonyMode = fit.mode;
            suggestion.effectiveSettings.harmonyMode = fit.mode;
            suggestion.effectiveSettings.randomVariation = ResolveAutoVariation(baseSettings.randomVariation, suggestion.colourfulLockedCount);
            suggestion.effectiveSettings.saturationRange = ResolveAutoSaturationRange(baseSettings, suggestion.lockedProfiles, suggestion.softProfiles);
            suggestion.effectiveSettings.valueRange = ResolveAutoValueRange(baseSettings, suggestion.lockedProfiles, suggestion.softProfiles);
            suggestion.effectiveSettings.hueRange = new Vector2(0f, 1f);
            suggestion.effectiveSettings.preserveLockedSwatches = true;
            suggestion.effectiveSettings.Clamp();

            BuildTargetHueBands(fit, suggestion.lockedProfiles, suggestion.effectiveSettings, suggestion.targetHueBands);
            ReweightTargetHueBandsFromCoverage(suggestion.targetHueBands, suggestion.softProfiles, suggestion.effectiveSettings.randomVariation, suggestion.diagnostics);

            suggestion.diagnostics.harmonyMode = suggestion.chosenHarmonyMode;
            if (forceRequestedHarmony)
                suggestion.diagnostics.AddNote($"Contextual generation is guided by manual harmony {suggestion.chosenHarmonyMode}.");
            else
                suggestion.diagnostics.AddNote($"Contextual generation chose {suggestion.chosenHarmonyMode} from {suggestion.colourfulLockedCount} locked colour anchor(s).");
            if (!forceRequestedHarmony && suggestion.chosenHarmonyMode != suggestion.requestedHarmonyMode)
                suggestion.diagnostics.AddNote($"Manual harmony {suggestion.requestedHarmonyMode} remains unchanged on the asset; this pass uses {suggestion.chosenHarmonyMode}.");
            if (suggestion.targetHueBands.Count > 0)
                suggestion.diagnostics.AddNote($"Next pass targets {suggestion.targetHueBands.Count} complementary hue band(s), avoiding locked anchors where possible.");

            return suggestion;
        }

        private static PaletteHarmonyFit ScoreForcedHarmonyMode(ColourHarmonyMode mode, PaletteGenerationSettings settings, List<PaletteLockedSwatchProfile> profiles, List<PaletteHarmonyScore> scores)
        {
            PaletteHarmonyFit fit = ScoreHarmonyMode(mode, settings, profiles);
            scores?.Add(new PaletteHarmonyScore
            {
                mode = fit.mode,
                score = fit.score,
                matchedCount = fit.matchedCount,
                explanation = fit.explanation
            });
            return fit;
        }

        private static PaletteGenerationSuggestion CloneSuggestionForExecution(PaletteGenerationSuggestion source)
        {
            if (source == null)
                return null;

            var clone = new PaletteGenerationSuggestion
            {
                autoContextAvailable = source.autoContextAvailable,
                openContext = source.openContext,
                requestedHarmonyMode = source.requestedHarmonyMode,
                chosenHarmonyMode = source.chosenHarmonyMode,
                anchorHue = source.anchorHue,
                lockedCount = source.lockedCount,
                colourfulLockedCount = source.colourfulLockedCount,
                softCoverageCount = source.softCoverageCount,
                iterationSerial = source.iterationSerial,
                executionSeed = source.executionSeed,
                effectiveSettings = source.effectiveSettings != null ? source.effectiveSettings.Clone() : null,
                diagnostics = CloneDiagnostics(source.diagnostics)
            };

            CloneProfiles(source.lockedProfiles, clone.lockedProfiles);
            CloneProfiles(source.softProfiles, clone.softProfiles);

            if (source.harmonyScores != null)
            {
                for (int i = 0; i < source.harmonyScores.Count; i++)
                {
                    PaletteHarmonyScore score = source.harmonyScores[i];
                    if (score == null)
                        continue;

                    clone.harmonyScores.Add(new PaletteHarmonyScore
                    {
                        mode = score.mode,
                        score = score.score,
                        matchedCount = score.matchedCount,
                        explanation = score.explanation
                    });
                }
            }

            if (source.targetHueBands != null)
            {
                for (int i = 0; i < source.targetHueBands.Count; i++)
                {
                    PaletteHueBand band = source.targetHueBands[i];
                    if (band != null)
                        clone.targetHueBands.Add(new PaletteHueBand(band.label, band.centerHue, band.width, band.weight));
                }
            }

            return clone;
        }

        private static PaletteGenerationDiagnostics CloneDiagnostics(PaletteGenerationDiagnostics source)
        {
            var clone = new PaletteGenerationDiagnostics();
            if (source == null)
                return clone;

            clone.requestedCount = source.requestedCount;
            clone.generatedCount = source.generatedCount;
            clone.preservedLockedCount = source.preservedLockedCount;
            clone.usedSeed = source.usedSeed;
            clone.seed = source.seed;
            clone.harmonyMode = source.harmonyMode;
            for (int i = 0; i < source.notes.Count; i++)
                clone.AddNote(source.notes[i]);
            for (int i = 0; i < source.warnings.Count; i++)
                clone.AddWarning(source.warnings[i]);
            return clone;
        }

        private static void CloneProfiles(List<PaletteLockedSwatchProfile> source, List<PaletteLockedSwatchProfile> destination)
        {
            if (source == null || destination == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                PaletteLockedSwatchProfile profile = source[i];
                if (profile == null)
                    continue;

                destination.Add(new PaletteLockedSwatchProfile
                {
                    name = profile.name,
                    role = profile.role,
                    hue = profile.hue,
                    saturation = profile.saturation,
                    value = profile.value,
                    colourful = profile.colourful
                });
            }
        }

        public static PaletteGenerationSettings TightenRangesFromSwatches(IList<PaletteSwatch> swatches, PaletteGenerationSettings fallback = null, float padding = 0.08f)
        {
            var settings = fallback != null ? fallback.Clone() : new PaletteGenerationSettings();
            if (swatches == null || swatches.Count == 0)
                return settings;

            float minS = 1f;
            float maxS = 0f;
            float minV = 1f;
            float maxV = 0f;
            bool any = false;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                Color.RGBToHSV(swatch.color, out _, out float s, out float v);
                minS = Mathf.Min(minS, s);
                maxS = Mathf.Max(maxS, s);
                minV = Mathf.Min(minV, v);
                maxV = Mathf.Max(maxV, v);
                any = true;
            }

            if (!any)
                return settings;

            settings.saturationRange = new Vector2(Mathf.Clamp01(minS - padding), Mathf.Clamp01(maxS + padding));
            settings.valueRange = new Vector2(Mathf.Clamp01(minV - padding), Mathf.Clamp01(maxV + padding));
            return settings;
        }

        private static PaletteGenerationDiagnostics CreateDiagnostics(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches)
        {
            var diagnostics = new PaletteGenerationDiagnostics
            {
                requestedCount = Mathf.Clamp(settings.targetSwatchCount, 1, 64),
                usedSeed = settings.useSeed,
                seed = settings.seed,
                harmonyMode = settings.harmonyMode
            };

            if (settings.preserveLockedSwatches && currentSwatches != null)
            {
                for (int i = 0; i < currentSwatches.Count; i++)
                {
                    if (currentSwatches[i] != null && currentSwatches[i].locked)
                        diagnostics.preservedLockedCount++;
                }
            }

            if (settings.preferReadableAccentPairs)
                diagnostics.AddNote("Readable accent preference enabled.");
            if (settings.enforceTextContrast)
                diagnostics.AddNote("Text contrast repair enabled.");
            if (settings.avoidNearDuplicates)
                diagnostics.AddNote("Near-duplicate avoidance enabled.");

            return diagnostics;
        }

        private static void ApplySuggestionDiagnostics(PaletteGenerationDiagnostics diagnostics, PaletteGenerationSuggestion suggestion)
        {
            if (diagnostics == null || suggestion == null)
                return;

            diagnostics.harmonyMode = suggestion.chosenHarmonyMode;

            if (suggestion.diagnostics != null)
            {
                for (int i = 0; i < suggestion.diagnostics.notes.Count; i++)
                    diagnostics.AddNote(suggestion.diagnostics.notes[i]);
                for (int i = 0; i < suggestion.diagnostics.warnings.Count; i++)
                    diagnostics.AddWarning(suggestion.diagnostics.warnings[i]);
            }

            if (suggestion.targetHueBands != null && suggestion.targetHueBands.Count > 0)
                diagnostics.AddNote($"Next generation hue bands: {suggestion.targetHueBands.Count}.");
        }

        private static void CollectLockedProfiles(IList<PaletteSwatch> swatches, List<PaletteLockedSwatchProfile> profiles)
        {
            if (swatches == null || profiles == null)
                return;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null || !swatch.locked)
                    continue;

                Color.RGBToHSV(swatch.color, out float h, out float s, out float v);
                profiles.Add(new PaletteLockedSwatchProfile
                {
                    name = string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {i + 1}" : swatch.name,
                    role = swatch.role,
                    hue = Mathf.Repeat(h, 1f),
                    saturation = Mathf.Clamp01(s),
                    value = Mathf.Clamp01(v),
                    colourful = s >= 0.14f && v >= 0.08f
                });
            }
        }

        private static void CollectUnlockedCoverageProfiles(IList<PaletteSwatch> swatches, List<PaletteLockedSwatchProfile> profiles)
        {
            if (swatches == null || profiles == null)
                return;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null || swatch.locked)
                    continue;

                Color.RGBToHSV(swatch.color, out float h, out float s, out float v);
                if (s < 0.08f || v < 0.06f)
                    continue;

                profiles.Add(new PaletteLockedSwatchProfile
                {
                    name = string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {i + 1}" : swatch.name,
                    role = swatch.role,
                    hue = Mathf.Repeat(h, 1f),
                    saturation = Mathf.Clamp01(s),
                    value = Mathf.Clamp01(v),
                    colourful = s >= 0.14f && v >= 0.08f
                });
            }
        }

        private static Vector2 ExpandRange(Vector2 source, float min, float max, float amount)
        {
            amount = Mathf.Clamp01(amount);
            float sourceMin = Mathf.Clamp01(Mathf.Min(source.x, source.y));
            float sourceMax = Mathf.Clamp01(Mathf.Max(source.x, source.y));
            return new Vector2(
                Mathf.Lerp(sourceMin, Mathf.Clamp01(min), amount),
                Mathf.Lerp(sourceMax, Mathf.Clamp01(max), amount));
        }

        private static PaletteHarmonyFit ChooseHarmonyFromLocks(PaletteGenerationSettings settings, List<PaletteLockedSwatchProfile> profiles, List<PaletteHarmonyScore> scores)
        {
            ColourHarmonyMode[] modes =
            {
                ColourHarmonyMode.Monochromatic,
                ColourHarmonyMode.Analogous,
                ColourHarmonyMode.Complementary,
                ColourHarmonyMode.SplitComplementary,
                ColourHarmonyMode.Triadic,
                ColourHarmonyMode.Tetradic,
                ColourHarmonyMode.Square,
                ColourHarmonyMode.RandomBalanced,
                ColourHarmonyMode.Contextual
            };

            PaletteHarmonyFit best = new PaletteHarmonyFit
            {
                mode = settings.harmonyMode,
                anchorHue = FirstColourfulHue(profiles),
                score = float.MaxValue,
                offsets = GetHarmonyOffsets(settings.harmonyMode)
            };

            for (int i = 0; i < modes.Length; i++)
            {
                PaletteHarmonyFit fit = ScoreHarmonyMode(modes[i], settings, profiles);
                scores?.Add(new PaletteHarmonyScore
                {
                    mode = fit.mode,
                    score = fit.score,
                    matchedCount = fit.matchedCount,
                    explanation = fit.explanation
                });

                if (fit.score < best.score)
                    best = fit;
            }

            return best;
        }

        private static PaletteHarmonyFit ScoreHarmonyMode(ColourHarmonyMode mode, PaletteGenerationSettings settings, List<PaletteLockedSwatchProfile> profiles)
        {
            float[] offsets = GetHarmonyOffsets(mode);
            if (offsets.Length == 0)
                offsets = new[] { 0f };

            PaletteHarmonyFit best = new PaletteHarmonyFit
            {
                mode = mode,
                anchorHue = FirstColourfulHue(profiles),
                score = float.MaxValue,
                offsets = offsets,
                slotUsage = new int[offsets.Length],
                explanation = "No colourful locked swatches."
            };

            for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                PaletteLockedSwatchProfile profile = profiles[profileIndex];
                if (!profile.colourful)
                    continue;

                for (int offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
                {
                    float candidateBase = Mathf.Repeat(profile.hue - offsets[offsetIndex], 1f);
                    PaletteHarmonyFit fit = EvaluateHarmonyCandidate(mode, settings, profiles, offsets, candidateBase);
                    if (fit.score < best.score)
                        best = fit;
                }
            }

            return best;
        }

        private static PaletteHarmonyFit EvaluateHarmonyCandidate(ColourHarmonyMode mode, PaletteGenerationSettings settings, List<PaletteLockedSwatchProfile> profiles, float[] offsets, float candidateBase)
        {
            int[] usage = new int[offsets.Length];
            float weightedDistance = 0f;
            float totalWeight = 0f;
            float maxDistance = 0f;
            int colourfulCount = 0;
            int matchedCount = 0;
            int outliers = 0;

            for (int i = 0; i < profiles.Count; i++)
            {
                PaletteLockedSwatchProfile profile = profiles[i];
                if (!profile.colourful)
                    continue;

                colourfulCount++;
                int nearest = FindNearestHarmonySlot(candidateBase, offsets, profile.hue, out float distance);
                usage[nearest]++;
                float weight = 0.7f + profile.saturation + Mathf.Abs(profile.value - 0.5f) * 0.25f;
                weightedDistance += distance * weight;
                totalWeight += weight;
                maxDistance = Mathf.Max(maxDistance, distance);

                if (distance <= 0.085f)
                    matchedCount++;
                if (distance >= 0.14f)
                    outliers++;
            }

            float averageDistance = totalWeight > 0f ? weightedDistance / totalWeight : 1f;
            float crowdPenalty = CalculateHarmonyCrowdPenalty(mode, usage, colourfulCount);
            float bias = HarmonySelectionBias(mode, settings, profiles);
            float score = averageDistance * 3.1f + maxDistance * 1.45f + outliers * 0.13f + crowdPenalty + bias;

            return new PaletteHarmonyFit
            {
                mode = mode,
                anchorHue = Mathf.Repeat(candidateBase, 1f),
                score = Mathf.Max(0f, score),
                matchedCount = matchedCount,
                offsets = offsets,
                slotUsage = usage,
                explanation = $"{matchedCount}/{colourfulCount} locked anchor(s) fit; max hue error {maxDistance:0.00}."
            };
        }

        private static float HarmonySelectionBias(ColourHarmonyMode mode, PaletteGenerationSettings settings, List<PaletteLockedSwatchProfile> profiles)
        {
            int colourfulCount = CountColourfulProfiles(profiles);
            if (colourfulCount <= 1)
            {
                switch (mode)
                {
                    case ColourHarmonyMode.Analogous:
                        return -0.12f;
                    case ColourHarmonyMode.Contextual:
                        return -0.08f;
                    case ColourHarmonyMode.Monochromatic:
                        return -0.02f;
                    case ColourHarmonyMode.Complementary:
                        return settings.contrastInfluence > 0.62f ? -0.03f : 0.05f;
                    case ColourHarmonyMode.Tetradic:
                    case ColourHarmonyMode.Square:
                        return 0.07f;
                    case ColourHarmonyMode.RandomBalanced:
                        return 0.09f;
                }
            }

            if (colourfulCount == 2 && TryGetFirstTwoColourfulHues(profiles, out float firstHue, out float secondHue))
            {
                float distance = ColourHarmonyUtility.ShortestHueDistance(firstHue, secondHue);
                switch (mode)
                {
                    case ColourHarmonyMode.Complementary:
                        return Mathf.Abs(distance - 0.5f) < 0.085f ? -0.18f : 0.07f;
                    case ColourHarmonyMode.Analogous:
                        return distance < 0.18f ? -0.12f : 0.04f;
                    case ColourHarmonyMode.SplitComplementary:
                        return distance > 0.28f && distance < 0.47f ? -0.06f : 0f;
                    case ColourHarmonyMode.Triadic:
                        return Mathf.Abs(distance - (1f / 3f)) < 0.085f ? -0.05f : 0.01f;
                }
            }

            if (colourfulCount >= 3)
            {
                switch (mode)
                {
                    case ColourHarmonyMode.Monochromatic:
                    case ColourHarmonyMode.Analogous:
                        return 0.08f;
                    case ColourHarmonyMode.Triadic:
                    case ColourHarmonyMode.Tetradic:
                    case ColourHarmonyMode.Square:
                    case ColourHarmonyMode.Contextual:
                        return -0.04f;
                }
            }

            return 0f;
        }

        private static float CalculateHarmonyCrowdPenalty(ColourHarmonyMode mode, int[] usage, int colourfulCount)
        {
            if (usage == null || usage.Length == 0 || colourfulCount <= 1)
                return 0f;

            int occupied = 0;
            int maxSlot = 0;
            for (int i = 0; i < usage.Length; i++)
            {
                if (usage[i] > 0)
                    occupied++;
                maxSlot = Mathf.Max(maxSlot, usage[i]);
            }

            if (mode == ColourHarmonyMode.Complementary && occupied <= 2)
                return maxSlot == colourfulCount ? 0.05f : 0f;

            int desiredSlots = Mathf.Min(Mathf.Min(colourfulCount, usage.Length), 3);
            float penalty = occupied < desiredSlots ? (desiredSlots - occupied) * 0.055f : 0f;
            if (maxSlot > Mathf.CeilToInt(colourfulCount * 0.75f) && usage.Length > 2)
                penalty += 0.04f;

            return penalty;
        }

        private static void BuildTargetHueBands(PaletteHarmonyFit fit, List<PaletteLockedSwatchProfile> profiles, PaletteGenerationSettings effectiveSettings, List<PaletteHueBand> targetHueBands)
        {
            if (targetHueBands == null)
                return;

            targetHueBands.Clear();
            float[] offsets = fit.offsets != null && fit.offsets.Length > 0 ? fit.offsets : GetHarmonyOffsets(fit.mode);
            float width = Mathf.Lerp(0.055f, 0.14f, Mathf.Clamp01(effectiveSettings.randomVariation));

            for (int i = 0; i < offsets.Length; i++)
            {
                float hue = Mathf.Repeat(fit.anchorHue + offsets[i], 1f);
                float distance = NearestLockedHueDistance(hue, profiles);
                if (distance > 0.055f)
                {
                    targetHueBands.Add(new PaletteHueBand(GetHueBandLabel(fit.mode, i), hue, width, 0.8f + distance * 2f));
                }
            }

            if (targetHueBands.Count > 0)
                return;

            int fallbackCount = Mathf.Min(Mathf.Max(1, offsets.Length), 4);
            for (int i = 0; i < fallbackCount; i++)
            {
                float shift = (i % 2 == 0 ? 1f : -1f) * Mathf.Lerp(0.035f, 0.075f, Mathf.Clamp01(effectiveSettings.randomVariation));
                float hue = Mathf.Repeat(fit.anchorHue + offsets[i] + shift, 1f);
                targetHueBands.Add(new PaletteHueBand(GetHueBandLabel(fit.mode, i), hue, Mathf.Max(0.045f, width * 0.75f), 1f));
            }
        }

        private static void ReweightTargetHueBandsFromCoverage(List<PaletteHueBand> bands, List<PaletteLockedSwatchProfile> softProfiles, float variation, PaletteGenerationDiagnostics diagnostics)
        {
            if (bands == null || bands.Count == 0 || softProfiles == null || softProfiles.Count == 0)
                return;

            int adjusted = 0;
            float variationWeight = Mathf.Lerp(0.32f, 0.82f, Mathf.Clamp01(variation));
            for (int i = 0; i < bands.Count; i++)
            {
                PaletteHueBand band = bands[i];
                if (band == null)
                    continue;

                float nearest = NearestProfileHueDistance(band.centerHue, softProfiles);
                float coverageAvoidance = Mathf.InverseLerp(0.015f, 0.22f, nearest);
                band.weight = Mathf.Max(0.08f, band.weight * Mathf.Lerp(0.44f, 1.48f, coverageAvoidance));

                if (nearest < 0.08f)
                {
                    float direction = i % 2 == 0 ? 1f : -1f;
                    band.centerHue = Mathf.Repeat(band.centerHue + direction * Mathf.Lerp(0.018f, 0.045f, variationWeight), 1f);
                    band.width = Mathf.Clamp(band.width * Mathf.Lerp(0.82f, 0.62f, variationWeight), 0.025f, 1f);
                    adjusted++;
                }
                else if (nearest > 0.18f)
                {
                    band.width = Mathf.Clamp(band.width * Mathf.Lerp(1.08f, 1.26f, variationWeight), 0.025f, 1f);
                }
            }

            bands.Sort((a, b) => b.weight.CompareTo(a.weight));
            diagnostics?.AddNote($"Soft coverage compared {softProfiles.Count} unlocked colour(s); {adjusted} target hue band(s) shifted away from recent colours.");
        }

        private static Vector2 ResolveAutoSaturationRange(PaletteGenerationSettings settings, List<PaletteLockedSwatchProfile> profiles, List<PaletteLockedSwatchProfile> softProfiles)
        {
            float average = AverageProfileSaturation(profiles);
            float min = Mathf.Clamp01(average * 0.55f - 0.06f);
            float max = Mathf.Clamp01(average * 1.18f + 0.18f);
            min = Mathf.Clamp(min, 0.2f, 0.82f);
            max = Mathf.Clamp(max, Mathf.Max(min + 0.12f, 0.44f), 1f);

            if (softProfiles != null && softProfiles.Count > 0)
            {
                float softAverage = AverageProfileSaturation(softProfiles);
                if (softAverage >= min && softAverage <= max)
                {
                    min = Mathf.Clamp01(min - 0.06f);
                    max = Mathf.Clamp01(max + 0.08f);
                }
            }

            return BlendRanges(settings.saturationRange, new Vector2(min, max), 0.72f);
        }

        private static Vector2 ResolveAutoValueRange(PaletteGenerationSettings settings, List<PaletteLockedSwatchProfile> profiles, List<PaletteLockedSwatchProfile> softProfiles)
        {
            float average = AverageProfileValue(profiles);
            Vector2 target;
            if (average < 0.38f)
                target = new Vector2(0.48f, 0.98f);
            else if (average > 0.72f)
                target = new Vector2(0.16f, 0.78f);
            else
                target = new Vector2(0.26f, 0.92f);

            float contrast = Mathf.Clamp01(settings.contrastInfluence);
            target.x = Mathf.Clamp01(target.x - contrast * 0.05f);
            target.y = Mathf.Clamp01(target.y + contrast * 0.04f);

            if (softProfiles != null && softProfiles.Count > 0)
            {
                float softAverage = AverageProfileValue(softProfiles);
                float targetAverage = (target.x + target.y) * 0.5f;
                if (Mathf.Abs(softAverage - targetAverage) < 0.14f)
                {
                    if (softAverage >= 0.5f)
                        target.x = Mathf.Clamp01(target.x - 0.08f);
                    else
                        target.y = Mathf.Clamp01(target.y + 0.08f);
                }
            }

            return BlendRanges(settings.valueRange, target, 0.7f);
        }

        private static Vector2 BlendRanges(Vector2 from, Vector2 to, float t)
        {
            t = Mathf.Clamp01(t);
            float fromMin = Mathf.Clamp01(Mathf.Min(from.x, from.y));
            float fromMax = Mathf.Clamp01(Mathf.Max(from.x, from.y));
            float toMin = Mathf.Clamp01(Mathf.Min(to.x, to.y));
            float toMax = Mathf.Clamp01(Mathf.Max(to.x, to.y));
            return new Vector2(Mathf.Lerp(fromMin, toMin, t), Mathf.Lerp(fromMax, toMax, t));
        }

        private static float ResolveAutoVariation(float manualVariation, int colourfulLockedCount)
        {
            float contextual = Mathf.Lerp(0.46f, 0.16f, Mathf.Clamp01((colourfulLockedCount - 1) / 5f));
            return Mathf.Clamp01(Mathf.Lerp(manualVariation, contextual, 0.68f));
        }

        private static List<PaletteHueBand> CloneUsableHueBands(PaletteGenerationSuggestion suggestion)
        {
            var result = new List<PaletteHueBand>();
            if (suggestion == null || suggestion.targetHueBands == null)
                return result;

            for (int i = 0; i < suggestion.targetHueBands.Count; i++)
            {
                PaletteHueBand band = suggestion.targetHueBands[i];
                if (band == null || band.weight <= 0f || band.width <= 0f)
                    continue;

                result.Add(new PaletteHueBand(
                    band.label,
                    Mathf.Repeat(band.centerHue, 1f),
                    Mathf.Clamp(band.width, 0.01f, 1f),
                    Mathf.Max(0.01f, band.weight)));
            }

            return result;
        }

        private static List<PaletteSwatch> GenerateSwatches(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, PaletteGenerationDiagnostics diagnostics, PaletteGenerationSuggestion suggestion)
        {
            int count = Mathf.Clamp(settings.targetSwatchCount, 1, 64);
            var result = new List<PaletteSwatch>(count);
            System.Random rng = settings.useSeed
                ? new System.Random(settings.seed + ((int)settings.harmonyMode * 1009) + count * 37)
                : new System.Random(unchecked(Environment.TickCount * 31 + Guid.NewGuid().GetHashCode()));

            PaletteGenerationContext context = CreateGenerationContext(settings, currentSwatches, rng, count, suggestion);
            List<float> familyHues = context.familyHues;
            List<PaletteSwatchRole> rolePlan = BuildGenerationRolePlan(settings, currentSwatches, count, diagnostics);

            for (int i = 0; i < count; i++)
            {
                PaletteSwatch existing = currentSwatches != null && i < currentSwatches.Count ? currentSwatches[i] : null;
                if (settings.preserveLockedSwatches && existing != null && existing.locked)
                {
                    result.Add(existing.Clone());
                    continue;
                }

                PaletteSwatchRole role = i < rolePlan.Count ? rolePlan[i] : ResolveGenerationRole(existing, i);
                Color color = GenerateHarmonyRoleColor(role, settings, context, rng, i, count, result);

                if (settings.avoidNearDuplicates && !IsNeutralGenerationRole(role))
                {
                    int guard = 0;
                    while (ContainsSimilarGeneratedColor(result, color) && guard < 10)
                    {
                        color = GenerateHarmonyRoleColor(role, settings, context, rng, i + guard + 1, count, result);
                        guard++;
                    }

                    if (guard >= 10)
                        diagnostics?.AddWarning($"Could not fully separate hue/value for slot {i + 1}.");
                }

                bool roleAssignedFromEmpty = existing == null || !IsFunctionalGenerationRole(existing.role);
                string name = existing != null && !string.IsNullOrWhiteSpace(existing.name) && !(roleAssignedFromEmpty && existing.name.StartsWith("Swatch"))
                    ? existing.name
                    : GetDefaultGeneratedName(role, i + 1);

                result.Add(new PaletteSwatch(name, color, role, false, existing != null ? existing.priority : i)
                {
                    notes = existing != null ? existing.notes : string.Empty,
                    tags = existing != null && existing.tags != null ? new List<string>(existing.tags) : new List<string>()
                });
            }

            if (settings.enforceTextContrast)
                EnforceGeneratedTextContrast(result, diagnostics);

            return result;
        }

        private static List<PaletteSwatchRole> BuildGenerationRolePlan(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, int count, PaletteGenerationDiagnostics diagnostics)
        {
            var roles = new List<PaletteSwatchRole>(count);
            var usedRoles = new HashSet<PaletteSwatchRole>();
            int lockedFunctional = 0;
            int assigned = 0;

            for (int i = 0; i < count; i++)
            {
                PaletteSwatch existing = currentSwatches != null && i < currentSwatches.Count ? currentSwatches[i] : null;
                PaletteSwatchRole planned = PaletteSwatchRole.None;

                if (existing != null && settings.preserveLockedSwatches && existing.locked)
                {
                    planned = existing.role;
                    if (IsFunctionalGenerationRole(planned))
                    {
                        usedRoles.Add(planned);
                        lockedFunctional++;
                    }
                }
                else if (existing != null && IsFunctionalGenerationRole(existing.role))
                {
                    planned = existing.role;
                    usedRoles.Add(planned);
                }

                roles.Add(planned);
            }

            for (int i = 0; i < count; i++)
            {
                if (IsFunctionalGenerationRole(roles[i]))
                    continue;

                PaletteSwatch existing = currentSwatches != null && i < currentSwatches.Count ? currentSwatches[i] : null;
                if (existing != null && settings.preserveLockedSwatches && existing.locked)
                    continue;

                PaletteSwatchRole role = NextGenerationRole(usedRoles, i);
                roles[i] = role;
                if (IsFunctionalGenerationRole(role))
                    usedRoles.Add(role);
                assigned++;
            }

            if (assigned > 0)
                diagnostics?.AddNote($"Assigned palette roles to {assigned} generated/unlocked swatch(es).");
            if (lockedFunctional > 0)
                diagnostics?.AddNote($"Preserved {lockedFunctional} locked role anchor(s) while planning generation.");

            return roles;
        }

        private static PaletteSwatchRole NextGenerationRole(HashSet<PaletteSwatchRole> usedRoles, int index)
        {
            if (DefaultRoleOrder.Length == 0)
                return PaletteSwatchRole.Accent;

            for (int i = 0; i < DefaultRoleOrder.Length; i++)
            {
                PaletteSwatchRole role = DefaultRoleOrder[i];
                if (usedRoles == null || !usedRoles.Contains(role))
                    return role;
            }

            int accentStart = Math.Max(0, Array.IndexOf(DefaultRoleOrder, PaletteSwatchRole.Accent));
            int accentCount = Math.Max(1, DefaultRoleOrder.Length - accentStart);
            return DefaultRoleOrder[accentStart + Math.Abs(index) % accentCount];
        }

        private static PaletteGenerationContext CreateGenerationContext(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, System.Random rng, int count, PaletteGenerationSuggestion suggestion)
        {
            bool hasSuggestion = suggestion != null && suggestion.autoContextAvailable;
            float baseHue = hasSuggestion ? Mathf.Repeat(suggestion.anchorHue, 1f) : ResolveGenerationAnchorHue(settings, currentSwatches, rng);
            List<float> familyHues = BuildHarmonyHueFamily(settings.harmonyMode, baseHue, rng, count);
            List<PaletteHueBand> targetHueBands = CloneUsableHueBands(suggestion);
            float jitter = Mathf.Clamp01(settings.randomVariation);

            float foundationHue = FitHueToRange(baseHue + RandomSigned(rng) * jitter * 0.075f, settings.hueRange, settings.harmonyInfluence);
            float panelHue = familyHues.Count > 1
                ? ColourHarmonyUtility.LerpHue(foundationHue, familyHues[1], Mathf.Lerp(0.18f, 0.58f, settings.harmonyInfluence))
                : foundationHue;
            panelHue = FitHueToRange(panelHue + RandomSigned(rng) * jitter * 0.04f, settings.hueRange, settings.harmonyInfluence);

            float saturationSample = Range(settings.saturationRange, rng);
            float surfaceSaturation = Mathf.Clamp01(Mathf.Lerp(saturationSample * 0.45f, saturationSample, Mathf.Lerp(0.22f, 0.62f, jitter)));
            surfaceSaturation = Mathf.Clamp01(surfaceSaturation + RandomSigned(rng) * jitter * 0.09f);
            float textSaturation = Mathf.Clamp01(surfaceSaturation * Mathf.Lerp(0.22f, 0.62f, jitter) + RandomSigned(rng) * jitter * 0.035f);

            float surfaceValue = Mathf.Clamp01(Range(settings.valueRange, rng) + RandomSigned(rng) * jitter * 0.14f);
            surfaceValue = Mathf.Clamp(surfaceValue, 0.06f, 0.94f);
            bool lightForeground = surfaceValue < 0.52f;
            float surfaceStep = Mathf.Lerp(0.06f, 0.18f, settings.contrastInfluence);
            float panelValue = Mathf.Clamp01(surfaceValue + (lightForeground ? surfaceStep : -surfaceStep));

            float textValue = lightForeground
                ? Mathf.Lerp(0.72f, 0.98f, settings.contrastInfluence)
                : Mathf.Lerp(0.28f, 0.04f, settings.contrastInfluence);
            textValue = Mathf.Clamp01(textValue + RandomSigned(rng) * jitter * 0.045f);

            float mutedValue = lightForeground
                ? Mathf.Lerp(Mathf.Max(surfaceValue + 0.24f, 0.58f), textValue, Mathf.Lerp(0.35f, 0.68f, settings.contrastInfluence))
                : Mathf.Lerp(Mathf.Min(surfaceValue - 0.24f, 0.42f), textValue, Mathf.Lerp(0.35f, 0.68f, settings.contrastInfluence));
            mutedValue = Mathf.Clamp01(mutedValue + RandomSigned(rng) * jitter * 0.04f);

            return new PaletteGenerationContext
            {
                baseHue = baseHue,
                foundationHue = foundationHue,
                panelHue = panelHue,
                surfaceSaturation = surfaceSaturation,
                textSaturation = textSaturation,
                backgroundValue = surfaceValue,
                panelValue = panelValue,
                textValue = textValue,
                mutedTextValue = mutedValue,
                lightForeground = lightForeground,
                randomVariation = jitter,
                familyHues = familyHues,
                targetHueBands = targetHueBands
            };
        }

        private static PaletteSwatchRole ResolveGenerationRole(PaletteSwatch existing, int index)
        {
            if (existing != null && existing.role != PaletteSwatchRole.None && existing.role != PaletteSwatchRole.Custom)
                return existing.role;

            if (DefaultRoleOrder.Length == 0)
                return PaletteSwatchRole.Accent;

            return DefaultRoleOrder[Mathf.Abs(index) % DefaultRoleOrder.Length];
        }

        private static string GetDefaultGeneratedName(PaletteSwatchRole role, int index)
        {
            if (role == PaletteSwatchRole.None || role == PaletteSwatchRole.Custom)
                return $"Swatch {index}";

            return Nicify(role.ToString());
        }

        private static float ResolveGenerationAnchorHue(PaletteGenerationSettings settings, IList<PaletteSwatch> currentSwatches, System.Random rng)
        {
            PaletteSwatch anchor = FindGenerationAnchor(currentSwatches, true) ?? FindGenerationAnchor(currentSwatches, false);
            float rangeHue = RandomHueInRange(settings.hueRange, rng);

            if (anchor == null)
                return rangeHue;

            Color.RGBToHSV(anchor.color, out float anchorHue, out float anchorSaturation, out float anchorValue);
            float anchorStrength = Mathf.Clamp01(anchorSaturation * 1.15f + Mathf.Abs(anchorValue - 0.5f) * 0.15f);
            float rangePull = settings.randomVariation > 0.01f ? Mathf.Lerp(0.18f, 0.55f, settings.randomVariation) : 0.08f;
            float resolved = ColourHarmonyUtility.LerpHue(anchorHue, rangeHue, rangePull * (1f - anchorStrength * 0.45f));

            return FitHueToRange(resolved, settings.hueRange, settings.harmonyInfluence);
        }

        private static PaletteSwatch FindGenerationAnchor(IList<PaletteSwatch> swatches, bool requireColourful)
        {
            if (swatches == null)
                return null;

            PaletteSwatch best = null;
            float bestScore = -1f;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                Color.RGBToHSV(swatch.color, out _, out float saturation, out float value);
                if (requireColourful && saturation < 0.22f)
                    continue;

                float score = saturation * 2.2f + value * 0.25f;
                if (swatch.locked)
                    score += 0.55f;
                if (IsPrimaryHueRole(swatch.role))
                    score += 0.8f;
                if (IsNeutralGenerationRole(swatch.role))
                    score -= 0.65f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = swatch;
                }
            }

            return best;
        }

        private static List<float> BuildHarmonyHueFamily(ColourHarmonyMode mode, float baseHue, System.Random rng, int count)
        {
            var hues = new List<float>();

            switch (mode)
            {
                case ColourHarmonyMode.Monochromatic:
                    hues.Add(baseHue);
                    break;
                case ColourHarmonyMode.Analogous:
                    AddHueOffsets(hues, baseHue, 0f, -1f / 12f, 1f / 12f, -1f / 6f, 1f / 6f);
                    break;
                case ColourHarmonyMode.Complementary:
                    AddHueOffsets(hues, baseHue, 0f, 0.5f, -1f / 24f, 0.5f + 1f / 24f);
                    break;
                case ColourHarmonyMode.SplitComplementary:
                    AddHueOffsets(hues, baseHue, 0f, 5f / 12f, 7f / 12f, -1f / 12f, 1f / 12f);
                    break;
                case ColourHarmonyMode.Triadic:
                    AddHueOffsets(hues, baseHue, 0f, 1f / 3f, 2f / 3f, 1f / 3f + 1f / 24f, 2f / 3f - 1f / 24f);
                    break;
                case ColourHarmonyMode.Tetradic:
                    AddHueOffsets(hues, baseHue, 0f, 1f / 6f, 0.5f, 2f / 3f);
                    break;
                case ColourHarmonyMode.Square:
                    AddHueOffsets(hues, baseHue, 0f, 0.25f, 0.5f, 0.75f);
                    break;
                case ColourHarmonyMode.RandomBalanced:
                    float h = baseHue;
                    for (int i = 0; i < Mathf.Max(4, count); i++)
                    {
                        h = Mathf.Repeat(h + 0.61803398875f + RandomSigned(rng) * 0.055f, 1f);
                        hues.Add(h);
                    }
                    break;
                case ColourHarmonyMode.Contextual:
                default:
                    AddHueOffsets(hues, baseHue, 0f, -1f / 12f, 1f / 12f, 0.5f, 1f / 3f, 2f / 3f);
                    break;
            }

            if (hues.Count == 0)
                hues.Add(baseHue);

            return hues;
        }

        private static void AddHueOffsets(List<float> hues, float baseHue, params float[] offsets)
        {
            for (int i = 0; i < offsets.Length; i++)
                hues.Add(Mathf.Repeat(baseHue + offsets[i], 1f));
        }

        private static Color GenerateHarmonyRoleColor(PaletteSwatchRole role, PaletteGenerationSettings settings, PaletteGenerationContext context, System.Random rng, int index, int count, IList<PaletteSwatch> existing)
        {
            List<float> familyHues = context.familyHues;
            int slot = ResolveHarmonySlot(role, index);
            float harmonyHue = familyHues[Mathf.Abs(slot) % familyHues.Count];
            bool useTargetBand = context.targetHueBands != null && context.targetHueBands.Count > 0 && !IsNeutralGenerationRole(role);
            float freeHue = useTargetBand ? SampleTargetHue(context, role, index, rng) : RandomHueInRange(settings.hueRange, rng);
            float hue = settings.harmonyMode == ColourHarmonyMode.RandomBalanced
                ? harmonyHue
                : ColourHarmonyUtility.LerpHue(freeHue, harmonyHue, Mathf.Clamp01(0.45f + settings.harmonyInfluence * 0.55f));

            if (useTargetBand)
                hue = ColourHarmonyUtility.LerpHue(hue, freeHue, Mathf.Clamp01(0.58f + settings.harmonyInfluence * 0.32f));

            hue = FitHueToRange(hue, settings.hueRange, settings.harmonyInfluence);

            float s = Mathf.Lerp(Range(settings.saturationRange, rng), 0.72f, 0.32f + settings.harmonyInfluence * 0.24f);
            float v = Mathf.Lerp(Range(settings.valueRange, rng), 0.78f, 0.22f + settings.contrastInfluence * 0.18f);
            float jitter = Mathf.Clamp01(settings.randomVariation);

            hue = Mathf.Repeat(hue + RandomSigned(rng) * jitter * GetHueJitterAmount(settings.harmonyMode, role), 1f);
            s = Mathf.Clamp01(s + RandomSigned(rng) * jitter * 0.16f);
            v = Mathf.Clamp01(v + RandomSigned(rng) * jitter * 0.18f);

            switch (role)
            {
                case PaletteSwatchRole.Background:
                    hue = context.foundationHue;
                    s = Mathf.Clamp(context.surfaceSaturation, 0.025f, settings.harmonyMode == ColourHarmonyMode.Monochromatic ? 0.58f : 0.48f);
                    v = context.backgroundValue;
                    break;
                case PaletteSwatchRole.Panel:
                    hue = context.panelHue;
                    s = Mathf.Clamp(context.surfaceSaturation * 1.08f, 0.03f, 0.56f);
                    v = context.panelValue;
                    break;
                case PaletteSwatchRole.Text:
                    hue = context.foundationHue;
                    s = Mathf.Clamp(context.textSaturation, 0f, 0.34f);
                    v = context.textValue;
                    break;
                case PaletteSwatchRole.MutedText:
                    hue = ColourHarmonyUtility.LerpHue(context.foundationHue, context.panelHue, 0.22f);
                    s = Mathf.Clamp(context.textSaturation * 1.18f, 0.01f, 0.38f);
                    v = context.mutedTextValue;
                    break;
                case PaletteSwatchRole.Accent:
                    s = Mathf.Clamp(s * 1.18f, 0.48f, 1f);
                    v = Mathf.Clamp(v * 1.05f, 0.52f, 0.98f);
                    break;
                case PaletteSwatchRole.AccentSecondary:
                    s = Mathf.Clamp(s * 1.12f, 0.42f, 1f);
                    v = Mathf.Clamp(v * 0.98f, 0.45f, 0.94f);
                    break;
                case PaletteSwatchRole.Highlight:
                    s = Mathf.Clamp(s * 1.05f, 0.36f, 0.95f);
                    v = Mathf.Clamp(v * 1.18f, 0.64f, 1f);
                    break;
                case PaletteSwatchRole.Warning:
                    hue = ColourHarmonyUtility.LerpHue(0.105f, hue, settings.harmonyInfluence * 0.35f);
                    s = Mathf.Clamp(s * 1.1f, 0.55f, 1f);
                    v = Mathf.Clamp(v * 1.08f, 0.58f, 1f);
                    break;
                case PaletteSwatchRole.Success:
                    hue = ColourHarmonyUtility.LerpHue(0.34f, hue, settings.harmonyInfluence * 0.38f);
                    s = Mathf.Clamp(s, 0.42f, 0.95f);
                    v = Mathf.Clamp(v, 0.42f, 0.92f);
                    break;
                case PaletteSwatchRole.Error:
                    hue = ColourHarmonyUtility.LerpHue(0f, hue, settings.harmonyInfluence * 0.34f);
                    s = Mathf.Clamp(s * 1.05f, 0.54f, 1f);
                    v = Mathf.Clamp(v, 0.42f, 0.95f);
                    break;
                case PaletteSwatchRole.Outline:
                    hue = context.foundationHue;
                    s = Mathf.Clamp(context.surfaceSaturation * 0.72f, 0.02f, 0.34f);
                    v = Mathf.Clamp01(Mathf.Lerp(context.backgroundValue, context.textValue, context.lightForeground ? 0.32f : 0.22f));
                    break;
                case PaletteSwatchRole.Shadow:
                    hue = context.foundationHue;
                    s = Mathf.Clamp(context.surfaceSaturation * 0.48f, 0.01f, 0.24f);
                    v = context.lightForeground ? Mathf.Clamp01(context.backgroundValue * 0.35f) : Mathf.Clamp01(context.backgroundValue * 0.08f);
                    break;
                case PaletteSwatchRole.None:
                case PaletteSwatchRole.Custom:
                    s = Mathf.Clamp(s, 0.32f, 0.96f);
                    v = Mathf.Clamp(v, 0.28f, 0.96f);
                    break;
            }

            if (settings.harmonyMode == ColourHarmonyMode.Monochromatic && !IsNeutralGenerationRole(role))
            {
                float t = count <= 1 ? 0.5f : Mathf.Repeat(index * 0.37f, 1f);
                s = Mathf.Clamp01(Mathf.Lerp(0.36f, 0.92f, t) + RandomSigned(rng) * jitter * 0.08f);
                v = Mathf.Clamp01(Mathf.Lerp(0.42f, 0.98f, 1f - t * 0.65f) + RandomSigned(rng) * jitter * 0.08f);
            }

            Color color = Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(s), Mathf.Clamp01(v));
            color.a = 1f;
            return color;
        }

        private static int ResolveHarmonySlot(PaletteSwatchRole role, int index)
        {
            switch (role)
            {
                case PaletteSwatchRole.Background:
                case PaletteSwatchRole.Panel:
                case PaletteSwatchRole.Text:
                case PaletteSwatchRole.MutedText:
                case PaletteSwatchRole.Outline:
                case PaletteSwatchRole.Shadow:
                    return 0;
                case PaletteSwatchRole.Accent:
                    return 0;
                case PaletteSwatchRole.AccentSecondary:
                    return 1;
                case PaletteSwatchRole.Highlight:
                    return 2;
                case PaletteSwatchRole.Warning:
                    return 3;
                case PaletteSwatchRole.Success:
                    return 4;
                case PaletteSwatchRole.Error:
                    return 5;
                default:
                    return Mathf.Max(0, index);
            }
        }

        private static float GetHueJitterAmount(ColourHarmonyMode mode, PaletteSwatchRole role)
        {
            if (IsNeutralGenerationRole(role))
                return 0.018f;

            switch (mode)
            {
                case ColourHarmonyMode.Monochromatic:
                    return 0.012f;
                case ColourHarmonyMode.Analogous:
                    return 0.025f;
                case ColourHarmonyMode.Complementary:
                case ColourHarmonyMode.SplitComplementary:
                case ColourHarmonyMode.Triadic:
                case ColourHarmonyMode.Tetradic:
                case ColourHarmonyMode.Square:
                    return 0.018f;
                case ColourHarmonyMode.RandomBalanced:
                    return 0.045f;
                default:
                    return 0.032f;
            }
        }

        private static bool IsPrimaryHueRole(PaletteSwatchRole role)
        {
            return role == PaletteSwatchRole.Accent ||
                   role == PaletteSwatchRole.AccentSecondary ||
                   role == PaletteSwatchRole.Highlight ||
                   role == PaletteSwatchRole.Custom;
        }

        private static bool IsFunctionalGenerationRole(PaletteSwatchRole role)
        {
            return role != PaletteSwatchRole.None && role != PaletteSwatchRole.Custom;
        }

        private static bool IsNeutralGenerationRole(PaletteSwatchRole role)
        {
            return role == PaletteSwatchRole.Background ||
                   role == PaletteSwatchRole.Panel ||
                   role == PaletteSwatchRole.Text ||
                   role == PaletteSwatchRole.MutedText ||
                   role == PaletteSwatchRole.Outline ||
                   role == PaletteSwatchRole.Shadow;
        }

        private static float SampleTargetHue(PaletteGenerationContext context, PaletteSwatchRole role, int index, System.Random rng)
        {
            if (context.targetHueBands == null || context.targetHueBands.Count == 0)
                return RandomHueInRange(new Vector2(0f, 1f), rng);

            int slot = Mathf.Abs(ResolveHarmonySlot(role, index) + index) % context.targetHueBands.Count;
            PaletteHueBand band = context.targetHueBands[slot];
            float width = Mathf.Clamp01(Mathf.Lerp(band.width * 0.38f, band.width, context.randomVariation));
            return Mathf.Repeat(band.centerHue + RandomSigned(rng) * width * 0.5f, 1f);
        }

        private static float[] GetHarmonyOffsets(ColourHarmonyMode mode)
        {
            switch (mode)
            {
                case ColourHarmonyMode.Monochromatic:
                    return new[] { 0f };
                case ColourHarmonyMode.Analogous:
                    return new[] { 0f, -1f / 12f, 1f / 12f, -1f / 6f, 1f / 6f };
                case ColourHarmonyMode.Complementary:
                    return new[] { 0f, 0.5f };
                case ColourHarmonyMode.SplitComplementary:
                    return new[] { 0f, 5f / 12f, 7f / 12f };
                case ColourHarmonyMode.Triadic:
                    return new[] { 0f, 1f / 3f, 2f / 3f };
                case ColourHarmonyMode.Tetradic:
                    return new[] { 0f, 1f / 6f, 0.5f, 2f / 3f };
                case ColourHarmonyMode.Square:
                    return new[] { 0f, 0.25f, 0.5f, 0.75f };
                case ColourHarmonyMode.RandomBalanced:
                    return new[] { 0f, 0.236f, 0.382f, 0.618f, 0.854f };
                case ColourHarmonyMode.Contextual:
                default:
                    return new[] { 0f, -1f / 12f, 1f / 12f, 0.5f, 1f / 3f, 2f / 3f };
            }
        }

        private static int FindNearestHarmonySlot(float baseHue, float[] offsets, float hue, out float distance)
        {
            int nearest = 0;
            distance = float.MaxValue;
            for (int i = 0; i < offsets.Length; i++)
            {
                float slotHue = Mathf.Repeat(baseHue + offsets[i], 1f);
                float slotDistance = ColourHarmonyUtility.ShortestHueDistance(slotHue, hue);
                if (slotDistance < distance)
                {
                    distance = slotDistance;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static float FirstColourfulHue(List<PaletteLockedSwatchProfile> profiles)
        {
            if (profiles == null)
                return 0f;

            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].colourful)
                    return Mathf.Repeat(profiles[i].hue, 1f);
            }

            return profiles.Count > 0 && profiles[0] != null ? Mathf.Repeat(profiles[0].hue, 1f) : 0f;
        }

        private static int CountColourfulProfiles(List<PaletteLockedSwatchProfile> profiles)
        {
            if (profiles == null)
                return 0;

            int count = 0;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null && profiles[i].colourful)
                    count++;
            }

            return count;
        }

        private static bool TryGetFirstTwoColourfulHues(List<PaletteLockedSwatchProfile> profiles, out float firstHue, out float secondHue)
        {
            firstHue = 0f;
            secondHue = 0f;
            bool foundFirst = false;
            if (profiles == null)
                return false;

            for (int i = 0; i < profiles.Count; i++)
            {
                PaletteLockedSwatchProfile profile = profiles[i];
                if (profile == null || !profile.colourful)
                    continue;

                if (!foundFirst)
                {
                    firstHue = profile.hue;
                    foundFirst = true;
                }
                else
                {
                    secondHue = profile.hue;
                    return true;
                }
            }

            return false;
        }

        private static float AverageProfileSaturation(List<PaletteLockedSwatchProfile> profiles)
        {
            float total = 0f;
            int count = 0;
            if (profiles == null)
                return 0.65f;

            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] == null || !profiles[i].colourful)
                    continue;

                total += profiles[i].saturation;
                count++;
            }

            return count > 0 ? total / count : 0.65f;
        }

        private static float AverageProfileValue(List<PaletteLockedSwatchProfile> profiles)
        {
            float total = 0f;
            int count = 0;
            if (profiles == null)
                return 0.55f;

            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] == null || !profiles[i].colourful)
                    continue;

                total += profiles[i].value;
                count++;
            }

            return count > 0 ? total / count : 0.55f;
        }

        private static float NearestLockedHueDistance(float hue, List<PaletteLockedSwatchProfile> profiles)
        {
            float distance = 1f;
            if (profiles == null || profiles.Count == 0)
                return distance;

            for (int i = 0; i < profiles.Count; i++)
            {
                PaletteLockedSwatchProfile profile = profiles[i];
                if (profile == null || !profile.colourful)
                    continue;

                distance = Mathf.Min(distance, ColourHarmonyUtility.ShortestHueDistance(hue, profile.hue));
            }

            return distance;
        }

        private static float NearestProfileHueDistance(float hue, List<PaletteLockedSwatchProfile> profiles)
        {
            float distance = 1f;
            if (profiles == null || profiles.Count == 0)
                return distance;

            for (int i = 0; i < profiles.Count; i++)
            {
                PaletteLockedSwatchProfile profile = profiles[i];
                if (profile == null || !profile.colourful)
                    continue;

                distance = Mathf.Min(distance, ColourHarmonyUtility.ShortestHueDistance(hue, profile.hue));
            }

            return distance;
        }

        private static string GetHueBandLabel(ColourHarmonyMode mode, int index)
        {
            return index == 0 ? $"{mode} anchor" : $"{mode} slot {index + 1}";
        }

        private static float RandomHueInRange(Vector2 range, System.Random rng)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
            if (Mathf.Approximately(min, max))
                return Mathf.Repeat(min, 1f);

            return Mathf.Repeat(Mathf.Lerp(min, max, (float)rng.NextDouble()), 1f);
        }

        private static float FitHueToRange(float hue, Vector2 range, float harmonyInfluence)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
            if (max - min > 0.985f || HueInRange(hue, min, max))
                return Mathf.Repeat(hue, 1f);

            float nearest = Mathf.Abs(Mathf.DeltaAngle(hue * 360f, min * 360f)) < Mathf.Abs(Mathf.DeltaAngle(hue * 360f, max * 360f)) ? min : max;
            float pull = Mathf.Clamp01(0.35f + (1f - harmonyInfluence) * 0.45f);
            return ColourHarmonyUtility.LerpHue(hue, nearest, pull);
        }

        private static bool HueInRange(float hue, float min, float max)
        {
            hue = Mathf.Repeat(hue, 1f);
            return hue >= min && hue <= max;
        }

        private static float Range(Vector2 range, System.Random rng)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        private static float RandomSigned(System.Random rng)
        {
            return ((float)rng.NextDouble() * 2f) - 1f;
        }

        private static bool ContainsSimilarGeneratedColor(IList<PaletteSwatch> swatches, Color color)
        {
            if (swatches == null)
                return false;

            Color.RGBToHSV(color, out float h, out float s, out float v);
            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch other = swatches[i];
                if (other == null || IsNeutralGenerationRole(other.role))
                    continue;

                Color.RGBToHSV(other.color, out float oh, out float os, out float ov);
                float hueDistance = Mathf.Abs(Mathf.DeltaAngle(h * 360f, oh * 360f)) / 360f;
                if (hueDistance < 0.035f && Mathf.Abs(s - os) < 0.12f && Mathf.Abs(v - ov) < 0.14f)
                    return true;
            }

            return false;
        }

        private static void EnforceGeneratedTextContrast(IList<PaletteSwatch> swatches, PaletteGenerationDiagnostics diagnostics)
        {
            PaletteSwatch background = FindGeneratedRole(swatches, PaletteSwatchRole.Background) ?? FindGeneratedRole(swatches, PaletteSwatchRole.Panel);
            PaletteSwatch panel = FindGeneratedRole(swatches, PaletteSwatchRole.Panel) ?? background;
            if (background == null)
                return;

            bool repaired = false;
            repaired |= ImproveGeneratedRoleAgainst(PaletteSwatchRole.Text, background.color, swatches, 4.5f);
            repaired |= ImproveGeneratedRoleAgainst(PaletteSwatchRole.MutedText, background.color, swatches, 3f);
            if (panel != null)
            {
                repaired |= ImproveGeneratedRoleAgainst(PaletteSwatchRole.Text, panel.color, swatches, 4.5f);
                repaired |= ImproveGeneratedRoleAgainst(PaletteSwatchRole.MutedText, panel.color, swatches, 3f);
            }

            if (repaired)
                diagnostics?.AddNote("Adjusted generated text roles to preserve contrast.");
        }

        private static bool ImproveGeneratedRoleAgainst(PaletteSwatchRole role, Color background, IList<PaletteSwatch> swatches, float target)
        {
            PaletteSwatch swatch = FindGeneratedRole(swatches, role);
            if (swatch == null || swatch.locked)
                return false;

            if (ColourContrastUtility.GetContrastRatio(swatch.color, background) < target)
            {
                Color previous = swatch.color;
                swatch.color = ColourContrastUtility.ImproveContrast(swatch.color, background, target);
                return !Approximately(previous, swatch.color);
            }

            return false;
        }

        private static PaletteSwatch FindGeneratedRole(IList<PaletteSwatch> swatches, PaletteSwatchRole role)
        {
            if (swatches == null)
                return null;

            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null && swatches[i].role == role)
                    return swatches[i];
            }

            return null;
        }

        private static string BuildVariantLabel(PaletteGenerationSettings settings, PaletteGenerationDiagnostics diagnostics)
        {
            string seed = diagnostics != null && diagnostics.usedSeed ? $" seed {diagnostics.seed}" : " live";
            return $"{settings.harmonyMode}{seed}";
        }

        private static string Nicify(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Swatch";

            var chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                    chars.Add(' ');
                chars.Add(c);
            }

            return new string(chars.ToArray());
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.0005f &&
                   Mathf.Abs(a.g - b.g) < 0.0005f &&
                   Mathf.Abs(a.b - b.b) < 0.0005f &&
                   Mathf.Abs(a.a - b.a) < 0.0005f;
        }

        private struct PaletteGenerationContext
        {
            public float baseHue;
            public float foundationHue;
            public float panelHue;
            public float surfaceSaturation;
            public float textSaturation;
            public float backgroundValue;
            public float panelValue;
            public float textValue;
            public float mutedTextValue;
            public bool lightForeground;
            public float randomVariation;
            public List<float> familyHues;
            public List<PaletteHueBand> targetHueBands;
        }

        private struct PaletteHarmonyFit
        {
            public ColourHarmonyMode mode;
            public float anchorHue;
            public float score;
            public int matchedCount;
            public float[] offsets;
            public int[] slotUsage;
            public string explanation;
        }
    }
}
