using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Progression Catalog", fileName = "ProgressionCatalog")]
    public sealed class ProgressionCatalogSO : ScriptableObject
    {
        [Header("Legacy / Session Tasks")]
        public List<TaskDefinitionSO> tasks = new();

        [Header("Achievements")]
        public List<AchievementDefinitionSO> achievements = new();

        [Header("Daily Task Ladders (New)")]
        public List<DailyTaskLadderDefinitionSO> dailyTaskLadders = new();
    }
}
