using System;
using UnityEngine;

namespace SkiGame.Runs
{
    public enum SkiRunDifficulty
    {
        Green,
        Blue,
        Red,
        Black
    }

    [CreateAssetMenu(menuName = "SkiGame/Runs/Run Difficulty Profile", fileName = "RunDifficultyProfile")]
    public sealed class RunDifficultyProfileSO : ScriptableObject
    {
        [Serializable]
        public struct Threshold
        {
            public SkiRunDifficulty difficulty;

            [Tooltip("Inclusive minimum slope angle in degrees for this difficulty.")]
            [Range(0f, 89.9f)] public float minAngle;

            [Tooltip("Exclusive maximum slope angle in degrees for this difficulty.")]
            [Range(0f, 89.9f)] public float maxAngle;

            [Tooltip("Primary color to use for line + flags.")]
            public Color color;
        }

        [Header("Thresholds (degrees)")]
        [Tooltip("Ordered thresholds are recommended. First match wins.")]
        public Threshold[] thresholds = new Threshold[]
        {
            new Threshold { difficulty = SkiRunDifficulty.Green, minAngle = 0f,  maxAngle = 15f, color = new Color(0.2f, 0.85f, 0.2f, 1f) },
            new Threshold { difficulty = SkiRunDifficulty.Blue,  minAngle = 15f, maxAngle = 25f, color = new Color(0.2f, 0.4f, 0.95f, 1f) },
            new Threshold { difficulty = SkiRunDifficulty.Red,   minAngle = 25f, maxAngle = 35f, color = new Color(0.95f, 0.2f, 0.2f, 1f) },
            new Threshold { difficulty = SkiRunDifficulty.Black, minAngle = 35f, maxAngle = 90f, color = new Color(0.1f, 0.1f, 0.1f, 1f) },
        };

        [Header("Classification")]
        [Tooltip("If enabled, classification uses max slope instead of average slope (more conservative).")]
        public bool useMaxSlopeForClassification = false;

        public SkiRunDifficulty Classify(float avgSlopeDeg, float maxSlopeDeg, out Color color)
        {
            float v = useMaxSlopeForClassification ? maxSlopeDeg : avgSlopeDeg;

            for (int i = 0; i < thresholds.Length; i++)
            {
                var t = thresholds[i];
                if (v >= t.minAngle && v < t.maxAngle)
                {
                    color = t.color;
                    return t.difficulty;
                }
            }

            // Fallback: last threshold if present, else white.
            if (thresholds != null && thresholds.Length > 0)
            {
                color = thresholds[thresholds.Length - 1].color;
                return thresholds[thresholds.Length - 1].difficulty;
            }

            color = Color.white;
            return SkiRunDifficulty.Green;
        }

        public SkiRunDifficulty ClassifyValue(float slopeDeg, out Color color)
        {
            float v = slopeDeg;

            for (int i = 0; i < thresholds.Length; i++)
            {
                var t = thresholds[i];
                if (v >= t.minAngle && v < t.maxAngle)
                {
                    color = t.color;
                    return t.difficulty;
                }
            }

            if (thresholds != null && thresholds.Length > 0)
            {
                color = thresholds[thresholds.Length - 1].color;
                return thresholds[thresholds.Length - 1].difficulty;
            }

            color = Color.white;
            return SkiRunDifficulty.Green;
        }

        public Color GetColor(SkiRunDifficulty difficulty)
        {
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (thresholds[i].difficulty == difficulty)
                    return thresholds[i].color;
            }
            return Color.white;
        }
    }

}
