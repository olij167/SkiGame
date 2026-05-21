using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    [CreateAssetMenu(fileName = "Pungent Colour Palette", menuName = "PungentFunk Utilities/Colour/Palette")]
    public class PungentColourPaletteSO : ScriptableObject
    {
        public string paletteName = "New Palette";
        public List<PaletteSwatch> swatches = new List<PaletteSwatch>();
        [TextArea(2, 6)] public string notes;
        public PaletteGenerationSettings defaultGenerationSettings = new PaletteGenerationSettings();
        public string createdUtc;
        public string modifiedUtc;

        public int Count => swatches != null ? swatches.Count : 0;

        private void OnValidate()
        {
            if (swatches == null)
                swatches = new List<PaletteSwatch>();

            if (defaultGenerationSettings == null)
                defaultGenerationSettings = new PaletteGenerationSettings();

            defaultGenerationSettings.Clamp();

            if (string.IsNullOrWhiteSpace(paletteName))
                paletteName = name;

            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] == null)
                    swatches[i] = new PaletteSwatch($"Swatch {i + 1}", Color.white);
                if (string.IsNullOrWhiteSpace(swatches[i].name))
                    swatches[i].name = $"Swatch {i + 1}";
            }
        }

        public void EnsureMetadata()
        {
            if (string.IsNullOrEmpty(createdUtc))
                createdUtc = DateTime.UtcNow.ToString("u");
            Touch();
        }

        public void Touch()
        {
            modifiedUtc = DateTime.UtcNow.ToString("u");
        }

        public PaletteSwatch GetFirstByRole(PaletteSwatchRole role)
        {
            if (swatches == null)
                return null;

            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch != null && swatch.role == role)
                    return swatch;
            }

            return null;
        }

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

}