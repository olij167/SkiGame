using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DailyShopService
{
    [Serializable]
    private class DayOffers
    {
        public int dayKey;
        public List<Entry> entries = new();
    }

    [Serializable]
    private class Entry
    {
        public string optionId;
        public int remaining;
        public int initial;
    }

    private const string PrefKey = "SkiGame.Customization.DailyOffers.v1";

    public static int GetUtcDayKey()
    {
        DateTime d = DateTime.UtcNow.Date;
        return (d.Year * 10000) + (d.Month * 100) + d.Day;
    }

    public static void EnsureDailyOffers(
        CustomizationCatalogSO catalog,
        int dayKey,
        int skinPatternCount,
        int eyeIconCount,
        int skisCount,
        int polesCount,
        int hatCount,
        int jacketCount,
        int stockPerOffer,
        Func<string, bool> isOwnedId)
    {
        if (catalog == null) return;

        var state = Load();
        if (state != null && state.dayKey == dayKey && state.entries != null && state.entries.Count > 0)
        {
            BackfillMissingOffers(
                catalog, state,
                skinPatternCount, eyeIconCount, skisCount, polesCount, hatCount, jacketCount,
                stockPerOffer, dayKey,
                isOwnedId
            );

            Save(state);
            return;
        }

        state = new DayOffers
        {
            dayKey = dayKey,
            entries = new List<Entry>(skinPatternCount + eyeIconCount + skisCount + polesCount + hatCount + jacketCount)
        };

        var rng = new System.Random(dayKey);
        var included = new HashSet<string>();

        AddRandomOffers(
            catalog, CustomizationOptionType.SkinPattern, skinPatternCount, stockPerOffer, rng, state.entries,
            predicate: (o) => isOwnedId == null || !isOwnedId(o.id),
            alreadyIncluded: included
        );

        AddRandomOffers(
            catalog, CustomizationOptionType.EyeIcon, eyeIconCount, stockPerOffer, rng, state.entries,
            predicate: (o) => isOwnedId == null || !isOwnedId(o.id),
            alreadyIncluded: included
        );

        BuildGearOffers(catalog, CustomizationOptionType.Skis, skisCount, stockPerOffer, rng, state.entries, included, isOwnedId);
        BuildGearOffers(catalog, CustomizationOptionType.Poles, polesCount, stockPerOffer, rng, state.entries, included, isOwnedId);

        // Wearables: treat like gear for offer logic
        BuildGearOffers(catalog, CustomizationOptionType.Hat, hatCount, stockPerOffer, rng, state.entries, included, isOwnedId);
        BuildGearOffers(catalog, CustomizationOptionType.Jacket, jacketCount, stockPerOffer, rng, state.entries, included, isOwnedId);

        Save(state);
    }

    private static void BackfillMissingOffers(
        CustomizationCatalogSO catalog,
        DayOffers state,
        int skinPatternCount,
        int eyeIconCount,
        int skisCount,
        int polesCount,
        int hatCount,
        int jacketCount,
        int stockPerOffer,
        int dayKey,
        Func<string, bool> isOwnedId)
    {
        if (catalog == null || state == null || state.entries == null) return;

        // Remove persistent items that have become owned (frees slots immediately)
        for (int i = state.entries.Count - 1; i >= 0; i--)
        {
            var e = state.entries[i];
            if (e == null) continue;
            var opt = catalog.FindById(e.optionId);
            if (opt == null) continue;

            if (opt.alwaysInStoreUntilOwned && isOwnedId != null && isOwnedId(opt.id))
                state.entries.RemoveAt(i);
        }

        int Count(CustomizationOptionType t)
        {
            int c = 0;
            for (int i = 0; i < state.entries.Count; i++)
            {
                var e = state.entries[i];
                if (e == null) continue;
                var opt = catalog.FindById(e.optionId);
                if (opt != null && opt.type == t) c++;
            }
            return c;
        }

        var included = new HashSet<string>();
        for (int i = 0; i < state.entries.Count; i++)
            if (state.entries[i] != null && !string.IsNullOrEmpty(state.entries[i].optionId))
                included.Add(state.entries[i].optionId);

        var rng = new System.Random(dayKey + 1337);

        int needSkin = Mathf.Max(0, skinPatternCount - Count(CustomizationOptionType.SkinPattern));
        if (needSkin > 0)
            AddRandomOffers(catalog, CustomizationOptionType.SkinPattern, needSkin, stockPerOffer, rng, state.entries,
                predicate: (o) => isOwnedId == null || !isOwnedId(o.id),
                alreadyIncluded: included);

        int needEyes = Mathf.Max(0, eyeIconCount - Count(CustomizationOptionType.EyeIcon));
        if (needEyes > 0)
            AddRandomOffers(catalog, CustomizationOptionType.EyeIcon, needEyes, stockPerOffer, rng, state.entries,
                predicate: (o) => isOwnedId == null || !isOwnedId(o.id),
                alreadyIncluded: included);

        int needSkis = Mathf.Max(0, skisCount - Count(CustomizationOptionType.Skis));
        if (needSkis > 0)
            BuildGearOffers(catalog, CustomizationOptionType.Skis, needSkis, stockPerOffer, rng, state.entries, included, isOwnedId);

        int needPoles = Mathf.Max(0, polesCount - Count(CustomizationOptionType.Poles));
        if (needPoles > 0)
            BuildGearOffers(catalog, CustomizationOptionType.Poles, needPoles, stockPerOffer, rng, state.entries, included, isOwnedId);

        int needHats = Mathf.Max(0, hatCount - Count(CustomizationOptionType.Hat));
        if (needHats > 0)
            BuildGearOffers(catalog, CustomizationOptionType.Hat, needHats, stockPerOffer, rng, state.entries, included, isOwnedId);

        int needJackets = Mathf.Max(0, jacketCount - Count(CustomizationOptionType.Jacket));
        if (needJackets > 0)
            BuildGearOffers(catalog, CustomizationOptionType.Jacket, needJackets, stockPerOffer, rng, state.entries, included, isOwnedId);
    }

    private static void BuildGearOffers(
        CustomizationCatalogSO catalog,
        CustomizationOptionType type,
        int desiredCount,
        int stockPerOffer,
        System.Random rng,
        List<Entry> target,
        HashSet<string> included,
        Func<string, bool> isOwnedId)
    {
        if (desiredCount <= 0) return;

        // 1) persistent unowned
        AddRandomOffers(
            catalog, type, desiredCount, stockPerOffer, rng, target,
            predicate: (o) =>
            {
                if (!o.alwaysInStoreUntilOwned) return false;
                if (isOwnedId != null && isOwnedId(o.id)) return false;
                return true;
            },
            alreadyIncluded: included
        );

        // 2) fill remaining with rotating unowned
        int have = 0;
        for (int i = 0; i < target.Count; i++)
        {
            var opt = catalog.FindById(target[i].optionId);
            if (opt != null && opt.type == type) have++;
        }

        int remaining = Mathf.Max(0, desiredCount - have);
        if (remaining <= 0) return;

        AddRandomOffers(
            catalog, type, remaining, stockPerOffer, rng, target,
            predicate: (o) =>
            {
                if (o.excludeFromDailyRotation) return false;
                if (o.alwaysInStoreUntilOwned) return false;
                if (isOwnedId != null && isOwnedId(o.id)) return false;
                return true;
            },
            alreadyIncluded: included
        );
    }

    public static bool IsInTodayOffers(string optionId, int todayKey)
    {
        if (string.IsNullOrEmpty(optionId)) return false;
        var state = Load();
        if (state == null || state.dayKey != todayKey || state.entries == null) return false;

        for (int i = 0; i < state.entries.Count; i++)
        {
            var e = state.entries[i];
            if (e != null && e.optionId == optionId) return true;
        }
        return false;
    }

    public static void GetTodayOffers(CustomizationCatalogSO catalog, int todayKey, CustomizationOptionType type, List<CustomizationOptionSO> outList)
    {
        outList.Clear();
        if (catalog == null) return;

        var state = Load();
        if (state == null || state.dayKey != todayKey || state.entries == null) return;

        for (int i = 0; i < state.entries.Count; i++)
        {
            var e = state.entries[i];
            if (e == null) continue;

            var opt = catalog.FindById(e.optionId);
            if (opt != null && opt.type == type)
                outList.Add(opt);
        }
    }

    private static void AddRandomOffers(
    CustomizationCatalogSO catalog,
    CustomizationOptionType type,
    int count,
    int stockPerOffer,
    System.Random rng,
    List<Entry> target,
    Func<CustomizationOptionSO, bool> predicate,
    HashSet<string> alreadyIncluded)
    {
        if (catalog == null || count <= 0) return;

        // IMPORTANT: In this project GetByType returns IEnumerable, not List.
        var poolEnumerable = catalog.GetByType(type);
        if (poolEnumerable == null) return;

        // Build candidate list (materialize + filter)
        var candidates = new List<CustomizationOptionSO>();
        foreach (var o in poolEnumerable)
        {
            if (o == null) continue;
            if (alreadyIncluded != null && alreadyIncluded.Contains(o.id)) continue;
            if (predicate != null && !predicate(o)) continue;
            candidates.Add(o);
        }

        if (candidates.Count == 0) return;

        // Shuffle (Fisher–Yates)
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int take = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < take; i++)
        {
            var o = candidates[i];
            target.Add(new Entry { optionId = o.id, remaining = stockPerOffer, initial = stockPerOffer });
            alreadyIncluded?.Add(o.id);
        }
    }

    private static DayOffers Load()
    {
        string json = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<DayOffers>(json); }
        catch { return null; }
    }

    private static void Save(DayOffers state)
    {
        if (state == null) return;
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }
}
