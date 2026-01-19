using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Progression Catalog", fileName = "ProgressionCatalog")]
    public sealed class ProgressionCatalogSO : ScriptableObject
    {
        public List<TaskDefinitionSO> tasks = new();
        public List<AchievementDefinitionSO> achievements = new();
    }
}
