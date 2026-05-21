using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    [CreateAssetMenu(menuName = "PungentFunk Utilities/Asset Placement/Asset Set", fileName = "Placement Asset Set")]
    public sealed class PungentPlacementAssetSetSO : ScriptableObject
    {
        public string displayName = "Placement Asset Set";
        [TextArea(2, 5)] public string notes;
        public List<PungentPlacementAssetEntry> entries = new List<PungentPlacementAssetEntry>();

        public bool HasUsableEntries
        {
            get
            {
                if (entries == null)
                    return false;

                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && entries[i].prefab != null && entries[i].weight > 0f)
                        return true;
                }

                return false;
            }
        }

        public PungentPlacementAssetEntry Pick(System.Random random)
        {
            if (entries == null || entries.Count == 0)
                return null;

            if (random == null)
                random = new System.Random(0);
            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                PungentPlacementAssetEntry entry = entries[i];
                if (entry != null && entry.prefab != null)
                    total += Mathf.Max(0f, entry.weight);
            }

            if (total <= 0f)
                return null;

            float pick = (float)(random.NextDouble() * total);
            float running = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                PungentPlacementAssetEntry entry = entries[i];
                if (entry == null || entry.prefab == null || entry.weight <= 0f)
                    continue;

                running += entry.weight;
                if (pick <= running)
                    return entry;
            }

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] != null && entries[i].prefab != null)
                    return entries[i];
            }

            return null;
        }

        public void AddPrefab(GameObject prefab)
        {
            if (prefab == null)
                return;

            if (entries == null)
                entries = new List<PungentPlacementAssetEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].prefab == prefab)
                    return;
            }

            entries.Add(new PungentPlacementAssetEntry
            {
                prefab = prefab,
                displayNameOverride = prefab.name,
                weight = 1f,
                scaleRange = Vector2.one,
                randomYaw = true,
                footprint = new PungentPlacementFootprint()
            });
        }
    }

    [Serializable]
    public sealed class PungentPlacementAssetEntry
    {
        public GameObject prefab;
        public string displayNameOverride;
        [Min(0f)] public float weight = 1f;
        public Vector2 scaleRange = Vector2.one;
        public bool uniformScale = true;
        public Vector3 nonUniformScaleMin = Vector3.one;
        public Vector3 nonUniformScaleMax = Vector3.one;
        public bool randomYaw = true;
        public Vector2 yawRange = new Vector2(0f, 360f);
        public Vector3 fixedEulerOffset = Vector3.zero;
        public Vector3 positionOffset = Vector3.zero;
        public PungentPlacementSurfaceAlignment surfaceAlignment = PungentPlacementSurfaceAlignment.PreserveYawAlignUpToSurfaceNormal;
        public PungentPlacementFootprint footprint = new PungentPlacementFootprint();
        public PungentPlacementSurfaceCategory categories = PungentPlacementSurfaceCategory.Outdoor | PungentPlacementSurfaceCategory.Terrain | PungentPlacementSurfaceCategory.Floor;
        public PungentPlacementTagList placementTags = new PungentPlacementTagList();

        public string DisplayName => !string.IsNullOrWhiteSpace(displayNameOverride) ? displayNameOverride : prefab != null ? prefab.name : "Missing Prefab";

        public Vector3 GetRandomScale(System.Random random)
        {
            if (random == null)
                random = new System.Random(0);
            if (uniformScale)
            {
                float min = Mathf.Min(scaleRange.x, scaleRange.y);
                float max = Mathf.Max(scaleRange.x, scaleRange.y);
                float value = Mathf.Lerp(min, max, (float)random.NextDouble());
                return new Vector3(value, value, value);
            }

            return new Vector3(
                Mathf.Lerp(nonUniformScaleMin.x, nonUniformScaleMax.x, (float)random.NextDouble()),
                Mathf.Lerp(nonUniformScaleMin.y, nonUniformScaleMax.y, (float)random.NextDouble()),
                Mathf.Lerp(nonUniformScaleMin.z, nonUniformScaleMax.z, (float)random.NextDouble()));
        }

        public Quaternion GetRandomRotation(System.Random random, Vector3 surfaceNormal)
        {
            if (random == null)
                random = new System.Random(0);
            float yaw = randomYaw ? Mathf.Lerp(yawRange.x, yawRange.y, (float)random.NextDouble()) : 0f;
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            Quaternion offset = Quaternion.Euler(fixedEulerOffset);

            Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f ? surfaceNormal.normalized : Vector3.up;
            if (surfaceAlignment == PungentPlacementSurfaceAlignment.AlignUpToSurfaceNormal)
                return Quaternion.FromToRotation(Vector3.up, normal) * yawRotation * offset;

            if (surfaceAlignment == PungentPlacementSurfaceAlignment.PreserveYawAlignUpToSurfaceNormal)
            {
                Quaternion align = Quaternion.FromToRotation(Vector3.up, normal);
                return align * yawRotation * offset;
            }

            return yawRotation * offset;
        }
    }

}