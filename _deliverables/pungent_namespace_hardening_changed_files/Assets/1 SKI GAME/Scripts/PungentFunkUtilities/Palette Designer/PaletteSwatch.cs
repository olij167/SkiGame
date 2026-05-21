using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Colour
{
    [Serializable]
    public class PaletteSwatch
    {
        public string name = "Swatch";
        public Color color = Color.white;
        public PaletteSwatchRole role = PaletteSwatchRole.None;
        public bool locked;
        public int priority;
        [TextArea(1, 3)] public string notes;
        public List<string> tags = new List<string>();

        public PaletteSwatch() { }

        public PaletteSwatch(string name, Color color, PaletteSwatchRole role = PaletteSwatchRole.None, bool locked = false, int priority = 0)
        {
            this.name = string.IsNullOrWhiteSpace(name) ? "Swatch" : name;
            this.color = color;
            this.role = role;
            this.locked = locked;
            this.priority = priority;
        }

        public PaletteSwatch Clone()
        {
            return new PaletteSwatch
            {
                name = name,
                color = color,
                role = role,
                locked = locked,
                priority = priority,
                notes = notes,
                tags = tags != null ? new List<string>(tags) : new List<string>()
            };
        }
    }

    public enum PaletteSwatchRole
    {
        None,
        Background,
        Panel,
        Text,
        MutedText,
        Accent,
        AccentSecondary,
        Highlight,
        Warning,
        Success,
        Error,
        Outline,
        Shadow,
        Custom
    }

}