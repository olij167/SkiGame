using System;
using System.Collections.Generic;

namespace PungentFunk.Utilities.Colour
{
    [Serializable]
    public class PaletteGenerationDiagnostics
    {
        public int requestedCount;
        public int generatedCount;
        public int preservedLockedCount;
        public bool usedSeed;
        public int seed;
        public ColourHarmonyMode harmonyMode;
        public List<string> notes = new List<string>();
        public List<string> warnings = new List<string>();

        public void AddNote(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                notes.Add(message);
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                warnings.Add(message);
        }
    }

    [Serializable]
    public class PaletteGenerationVariant
    {
        public string id;
        public string label;
        public string createdUtc;
        public PaletteGenerationSettings settings;
        public PaletteGenerationDiagnostics diagnostics = new PaletteGenerationDiagnostics();
        public List<PaletteSwatch> swatches = new List<PaletteSwatch>();

        public int Count => swatches != null ? swatches.Count : 0;

        public List<PaletteSwatch> CloneSwatches()
        {
            var result = new List<PaletteSwatch>();
            if (swatches == null)
                return result;

            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null)
                    result.Add(swatches[i].Clone());
            }

            return result;
        }
    }

    [Serializable]
    public class PaletteGenerationResult
    {
        public bool success;
        public string message;
        public PaletteGenerationVariant variant;

        public List<PaletteSwatch> CloneSwatches()
        {
            return variant != null ? variant.CloneSwatches() : new List<PaletteSwatch>();
        }
    }

    public class PaletteGenerationHistory
    {
        private readonly List<PaletteGenerationVariant> _variants = new List<PaletteGenerationVariant>();

        public int activeIndex = -1;
        public int maxEntries = 12;

        public IReadOnlyList<PaletteGenerationVariant> Variants => _variants;
        public int Count => _variants.Count;
        public PaletteGenerationVariant ActiveVariant => activeIndex >= 0 && activeIndex < _variants.Count ? _variants[activeIndex] : null;

        public void Clear()
        {
            _variants.Clear();
            activeIndex = -1;
        }

        public PaletteGenerationVariant Add(PaletteGenerationVariant variant)
        {
            if (variant == null)
                return null;

            _variants.Insert(0, variant);
            while (_variants.Count > Math.Max(1, maxEntries))
                _variants.RemoveAt(_variants.Count - 1);

            activeIndex = 0;
            return variant;
        }

        public bool SetActive(int index)
        {
            if (index < 0 || index >= _variants.Count)
                return false;

            activeIndex = index;
            return true;
        }

        public bool MoveToFront(int index)
        {
            if (index < 0 || index >= _variants.Count)
                return false;

            PaletteGenerationVariant variant = _variants[index];
            _variants.RemoveAt(index);
            _variants.Insert(0, variant);
            activeIndex = 0;
            return true;
        }

        public PaletteGenerationVariant Get(int index)
        {
            return index >= 0 && index < _variants.Count ? _variants[index] : null;
        }
    }

    [Serializable]
    public class PaletteHueBand
    {
        public string label;
        public float centerHue;
        public float width = 0.08f;
        public float weight = 1f;

        public PaletteHueBand()
        {
        }

        public PaletteHueBand(string label, float centerHue, float width, float weight)
        {
            this.label = label;
            this.centerHue = centerHue;
            this.width = width;
            this.weight = weight;
        }
    }

    [Serializable]
    public class PaletteLockedSwatchProfile
    {
        public string name;
        public PaletteSwatchRole role;
        public float hue;
        public float saturation;
        public float value;
        public bool colourful;
    }

    [Serializable]
    public class PaletteHarmonyScore
    {
        public ColourHarmonyMode mode;
        public float score;
        public int matchedCount;
        public string explanation;
    }

    [Serializable]
    public class PaletteGenerationSuggestion
    {
        public bool autoContextAvailable;
        public bool openContext;
        public ColourHarmonyMode requestedHarmonyMode;
        public ColourHarmonyMode chosenHarmonyMode;
        public float anchorHue;
        public int lockedCount;
        public int colourfulLockedCount;
        public int softCoverageCount;
        public int iterationSerial;
        public int executionSeed;
        public PaletteGenerationSettings effectiveSettings;
        public PaletteGenerationDiagnostics diagnostics = new PaletteGenerationDiagnostics();
        public List<PaletteLockedSwatchProfile> lockedProfiles = new List<PaletteLockedSwatchProfile>();
        public List<PaletteLockedSwatchProfile> softProfiles = new List<PaletteLockedSwatchProfile>();
        public List<PaletteHarmonyScore> harmonyScores = new List<PaletteHarmonyScore>();
        public List<PaletteHueBand> targetHueBands = new List<PaletteHueBand>();

        public PaletteGenerationSettings CloneEffectiveSettings()
        {
            return effectiveSettings != null ? effectiveSettings.Clone() : new PaletteGenerationSettings();
        }
    }
}
