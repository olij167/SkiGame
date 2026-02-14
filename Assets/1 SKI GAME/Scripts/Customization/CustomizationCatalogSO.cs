using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkiGame/Customization/Catalog", fileName = "CustomizationCatalog")]
public class CustomizationCatalogSO : ScriptableObject
{
    public List<CustomizationOptionSO> options = new List<CustomizationOptionSO>();

    public IEnumerable<CustomizationOptionSO> GetByType(CustomizationOptionType t)
    {
        for (int i = 0; i < options.Count; i++)
        {
            var o = options[i];
            if (o != null && o.type == t) yield return o;
        }
    }

    public CustomizationOptionSO FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < options.Count; i++)
        {
            var o = options[i];
            if (o != null && o.id == id) return o;
        }
        return null;
    }
}
