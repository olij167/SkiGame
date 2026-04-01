using System;

namespace SkiGame.POI
{
    public enum POICategory
    {
        None = 0,
        Resort = 10,
        Shop = 20,
        Kiosk = 30,
        Landmark = 40,
        Service = 50,
        Custom = 100
    }

    public static class POIMetaUtility
    {
        public static string EnsureToken(string meta, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return meta ?? string.Empty;

            if (string.IsNullOrWhiteSpace(meta))
                return token;

            if (meta.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return meta;

            return meta + ";" + token;
        }

        public static string BuildCategoryToken(POICategory category)
        {
            return category == POICategory.None
                ? string.Empty
                : $"category:{category.ToString().ToLowerInvariant()}";
        }
    }
}