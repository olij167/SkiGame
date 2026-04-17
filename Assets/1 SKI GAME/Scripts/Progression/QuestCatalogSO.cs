using System.Collections.Generic;
using UnityEngine;

namespace SkiGame.Progression
{
    [CreateAssetMenu(menuName = "SkiGame/Progression/Quest Catalog", fileName = "QuestCatalog")]
    public sealed class QuestCatalogSO : ScriptableObject
    {
        public List<QuestDefinitionSO> quests = new List<QuestDefinitionSO>();
    }
}
