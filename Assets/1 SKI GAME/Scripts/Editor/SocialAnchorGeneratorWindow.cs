#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SkiGame.POI;
using UnityEditor;
using UnityEngine;

public sealed class SocialAnchorGeneratorWindow : EditorWindow
{
    private bool dryRun = true;
    private bool generateMissingOnly = true;
    private bool updateExisting;
    private bool parentUnderSourceObject = true;
    private bool parentUnderSocialAnchorsRoot;
    private bool includeLifts = true;
    private bool includeKiosks = true;
    private bool includeRaces = true;
    private bool includeMedicResort = true;
    private bool includeGenericPois = true;

    [MenuItem("Tools/Ski Game/NPC Social/Generate Social Anchors From POIs")]
    public static void Open()
    {
        GetWindow<SocialAnchorGeneratorWindow>("Social Anchors");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Generate Social Anchors From POIs", EditorStyles.boldLabel);
        dryRun = EditorGUILayout.Toggle("Dry Run", dryRun);
        generateMissingOnly = EditorGUILayout.Toggle("Generate Missing Only", generateMissingOnly);
        updateExisting = EditorGUILayout.Toggle("Update Existing", updateExisting);
        parentUnderSourceObject = EditorGUILayout.Toggle("Parent Under Source Object", parentUnderSourceObject);
        parentUnderSocialAnchorsRoot = EditorGUILayout.Toggle("Parent Under SocialAnchors Root", parentUnderSocialAnchorsRoot);

        EditorGUILayout.Space();
        includeLifts = EditorGUILayout.Toggle("Include Lifts", includeLifts);
        includeKiosks = EditorGUILayout.Toggle("Include Kiosks", includeKiosks);
        includeRaces = EditorGUILayout.Toggle("Include Races", includeRaces);
        includeMedicResort = EditorGUILayout.Toggle("Include Medic/Resort", includeMedicResort);
        includeGenericPois = EditorGUILayout.Toggle("Include Generic POIs", includeGenericPois);

        EditorGUILayout.Space();
        if (GUILayout.Button(dryRun ? "Dry Run Scan" : "Generate / Update Anchors"))
            Generate();
    }

    private void Generate()
    {
        PointOfInterestRegistry registry = FindObjectOfType<PointOfInterestRegistry>();
        if (registry == null)
        {
            Debug.LogWarning("[SocialAnchorGenerator] No PointOfInterestRegistry found in scene.");
            return;
        }

        registry.Refresh();
        IReadOnlyList<POIInfo> pois = registry.Current;
        Transform root = parentUnderSocialAnchorsRoot ? GetOrCreateRoot() : null;
        int created = 0;
        int updated = 0;
        int skipped = 0;

        for (int i = 0; i < pois.Count; i++)
        {
            POIInfo poi = pois[i];
            if (!poi.IsValid || !ShouldInclude(poi))
            {
                skipped++;
                continue;
            }

            UnityEngine.Object source = poi.source;
            Transform sourceTransform = ResolveSourceTransform(source);
            NpcSocialAnchor existing = FindExistingAnchor(poi, sourceTransform);
            if (existing != null && generateMissingOnly && !updateExisting)
            {
                skipped++;
                continue;
            }

            NpcSocialAnchorType anchorType = ResolveAnchorType(poi);
            string[] tags = BuildTags(poi);
            string anchorName = $"SocialAnchor_{SanitizeName(poi.displayName)}";

            if (dryRun)
            {
                Debug.Log($"[SocialAnchorGenerator] {(existing != null ? "Would update" : "Would create")} {anchorName} type={anchorType} poi={poi.displayName}", source);
                continue;
            }

            if (existing == null)
            {
                Transform parent = parentUnderSourceObject && sourceTransform != null ? sourceTransform : root;
                if (parent == null)
                    parent = root != null ? root : registry.transform;

                GameObject go = new GameObject(anchorName);
                Undo.RegisterCreatedObjectUndo(go, "Create Social Anchor");
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.position = poi.position;
                existing = go.AddComponent<NpcSocialAnchor>();
                created++;
            }
            else
            {
                Undo.RecordObject(existing, "Update Social Anchor");
                updated++;
            }

            existing.SetGeneratedIdentity(BuildAnchorId(poi), poi.displayName, anchorType, tags);
            existing.RefreshContext();
            EditorUtility.SetDirty(existing);
        }

        Debug.Log($"[SocialAnchorGenerator] Complete. created={created} updated={updated} skipped={skipped} dryRun={dryRun}");
    }

    private bool ShouldInclude(POIInfo poi)
    {
        if (poi.type == POIType.SkiLift)
            return includeLifts;

        if (poi.category == POICategory.Kiosk)
            return includeKiosks;

        if (poi.category == POICategory.Race)
            return includeRaces;

        if (poi.category == POICategory.Medical || poi.category == POICategory.Resort)
            return includeMedicResort;

        return includeGenericPois;
    }

    private static NpcSocialAnchor FindExistingAnchor(POIInfo poi, Transform source)
    {
        if (source != null)
        {
            var anchors = source.GetComponentsInChildren<NpcSocialAnchor>(true);
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null && string.Equals(anchors[i].AnchorId, BuildAnchorId(poi), StringComparison.OrdinalIgnoreCase))
                    return anchors[i];
            }
        }

        NpcSocialAnchor[] all = FindObjectsOfType<NpcSocialAnchor>(true);
        string expectedId = BuildAnchorId(poi);
        string expectedName = $"SocialAnchor_{SanitizeName(poi.displayName)}";
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;

            if (string.Equals(all[i].AnchorId, expectedId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(all[i].name, expectedName, StringComparison.OrdinalIgnoreCase))
                return all[i];
        }

        return null;
    }

    private static Transform ResolveSourceTransform(UnityEngine.Object source)
    {
        return source switch
        {
            GameObject go => go.transform,
            Component component => component.transform,
            _ => null
        };
    }

    private static Transform GetOrCreateRoot()
    {
        GameObject existing = GameObject.Find("SocialAnchors");
        if (existing != null)
            return existing.transform;

        GameObject root = new GameObject("SocialAnchors");
        Undo.RegisterCreatedObjectUndo(root, "Create SocialAnchors Root");
        return root.transform;
    }

    private static NpcSocialAnchorType ResolveAnchorType(POIInfo poi)
    {
        if (poi.type == POIType.SkiLift)
            return poi.displayName != null && poi.displayName.IndexOf("bottom", StringComparison.OrdinalIgnoreCase) >= 0
                ? NpcSocialAnchorType.LiftQueue
                : NpcSocialAnchorType.LiftStation;

        return poi.category switch
        {
            POICategory.Kiosk => NpcSocialAnchorType.Kiosk,
            POICategory.Race => NpcSocialAnchorType.RaceStart,
            POICategory.Medical => NpcSocialAnchorType.MedicTent,
            POICategory.Resort => NpcSocialAnchorType.ResortEntry,
            POICategory.Shop => NpcSocialAnchorType.Lodge,
            _ => poi.type == POIType.SkiRun ? NpcSocialAnchorType.RunOverlook : NpcSocialAnchorType.Generic
        };
    }

    private static string[] BuildTags(POIInfo poi)
    {
        var tags = new List<string>();
        if (poi.type != POIType.Unknown)
            tags.Add(poi.type.ToString());
        if (poi.category != POICategory.None)
            tags.Add(poi.category.ToString());
        if (!string.IsNullOrWhiteSpace(poi.meta))
            tags.Add(poi.meta);

        return tags.ToArray();
    }

    private static string BuildAnchorId(POIInfo poi)
    {
        return $"poi:{poi.id}";
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "POI";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(" ", "_").Trim();
    }
}
#endif
