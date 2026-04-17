using UnityEngine;

namespace SkiGame.Progression
{
    public static class QuestVisualUtility
    {
        private static readonly Color[] QuestPalette =
        {
            new Color(0.28f, 0.77f, 1.00f, 1f),
            new Color(0.99f, 0.60f, 0.22f, 1f),
            new Color(0.36f, 0.90f, 0.56f, 1f),
            new Color(0.96f, 0.42f, 0.78f, 1f),
            new Color(0.98f, 0.86f, 0.25f, 1f),
        };

        public static Color GetQuestAccent(string questId)
        {
            if (QuestPalette.Length == 0)
                return Color.cyan;

            int hash = 0;
            if (!string.IsNullOrWhiteSpace(questId))
            {
                unchecked
                {
                    string trimmed = questId.Trim();
                    for (int i = 0; i < trimmed.Length; i++)
                        hash = (hash * 31) + trimmed[i];
                }
            }

            if (hash < 0)
                hash = -hash;

            return QuestPalette[hash % QuestPalette.Length];
        }
    }
}
