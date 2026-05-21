using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Placement
{
    public static class PungentPlacementScatterUtility
    {
        private const int MaxAttemptsMultiplier = 24;

        public static PungentPlacementResult Generate(PungentPlacementContext context)
        {
            PungentPlacementResult result = new PungentPlacementResult();
            if (context == null)
            {
                result.summary = "Missing context.";
                return result;
            }

            if (context.assetSet == null || !context.assetSet.HasUsableEntries)
            {
                result.summary = "Missing usable asset set.";
                return result;
            }

            int count = Mathf.Max(0, context.requestedCount);
            if (count == 0)
            {
                result.summary = "Requested count is zero.";
                return result;
            }

            System.Random random = new System.Random(context.seed);
            List<Vector3> rawPositions = GenerateRawPositions(context, count, random);
            List<Vector3> acceptedPositions = new List<Vector3>();

            for (int i = 0; i < rawPositions.Count; i++)
            {
                PungentPlacementAssetEntry entry = context.assetSet.Pick(random);
                PungentPlacementCandidate candidate = BuildCandidate(context, entry, rawPositions[i], random, i);
                ValidateCandidate(context, candidate, acceptedPositions, random);
                if (candidate.Accepted)
                    acceptedPositions.Add(candidate.position);
                result.candidates.Add(candidate);
            }

            result.Recount();
            return result;
        }

        private static PungentPlacementCandidate BuildCandidate(PungentPlacementContext context, PungentPlacementAssetEntry entry, Vector3 rawPosition, System.Random random, int index)
        {
            var candidate = new PungentPlacementCandidate
            {
                index = index,
                assetEntry = entry,
                prefab = entry != null ? entry.prefab : null,
                position = rawPosition,
                state = PungentPlacementCandidateState.Pending
            };

            if (entry == null || entry.prefab == null)
            {
                Reject(candidate, PungentPlacementRejectionReason.MissingPrefab, "Missing prefab entry.");
                return candidate;
            }

            Vector3 surfaceNormal = Vector3.up;
            bool hasSurface = TryProjectToSurface(context, candidate.position, out Vector3 projected, out surfaceNormal);
            if (hasSurface)
            {
                candidate.position = projected + surfaceNormal.normalized * GetSurfaceOffset(context);
                candidate.surfaceNormal = surfaceNormal;
            }
            else
            {
                candidate.surfaceNormal = Vector3.up;
            }

            candidate.scale = entry.GetRandomScale(random);
            candidate.rotation = entry.GetRandomRotation(random, candidate.surfaceNormal);
            candidate.position += candidate.rotation * entry.positionOffset;
            candidate.estimatedBounds = PungentPlacementBoundsUtility.EstimatePrefabWorldBounds(entry.prefab, candidate.position, candidate.rotation, candidate.scale, entry.footprint);
            return candidate;
        }

        private static void ValidateCandidate(PungentPlacementContext context, PungentPlacementCandidate candidate, List<Vector3> acceptedPositions, System.Random random)
        {
            if (candidate == null || candidate.state == PungentPlacementCandidateState.Rejected)
                return;

            PungentPlacementRuleSetSO rules = context.ruleSet;
            if (rules != null)
            {
                if (rules.requireSurfaceHit && !TryProjectToSurface(context, candidate.position, out _, out _))
                {
                    Reject(candidate, PungentPlacementRejectionReason.NoSurfaceHit, "No valid surface hit under the candidate.");
                    return;
                }

                if (!rules.HeightAllowed(candidate.position.y))
                {
                    Reject(candidate, PungentPlacementRejectionReason.HeightRejected, "Candidate is outside the allowed height range.");
                    return;
                }

                if (!rules.SlopeAllowed(candidate.surfaceNormal))
                {
                    Reject(candidate, PungentPlacementRejectionReason.SlopeRejected, "Surface slope is outside the allowed range.");
                    return;
                }

                if (rules.useHeatmap && rules.heatmap != null)
                {
                    float value = rules.SampleHeatmap01(candidate.position);
                    if (value < rules.heatmapThreshold)
                    {
                        Reject(candidate, PungentPlacementRejectionReason.HeatmapRejected, $"Heatmap value {value:0.00} is below threshold.");
                        return;
                    }

                    float influence = Mathf.Clamp01(rules.heatmapProbabilityInfluence);
                    if (influence > 0f)
                    {
                        float passChance = Mathf.Lerp(1f, value, influence);
                        if ((float)random.NextDouble() > passChance)
                        {
                            Reject(candidate, PungentPlacementRejectionReason.HeatmapRejected, "Rejected by heatmap probability.");
                            return;
                        }
                    }
                }

                if (rules.enforceMinimumSpacing && rules.minimumSpacing > 0f)
                {
                    float minSqr = rules.minimumSpacing * rules.minimumSpacing;
                    for (int i = 0; i < acceptedPositions.Count; i++)
                    {
                        if ((acceptedPositions[i] - candidate.position).sqrMagnitude < minSqr)
                        {
                            Reject(candidate, PungentPlacementRejectionReason.MinSpacingRejected, "Too close to another accepted candidate.");
                            return;
                        }
                    }
                }

                if (rules.rejectColliderOverlap && OverlapsScene(context, candidate))
                {
                    Reject(candidate, PungentPlacementRejectionReason.OverlapRejected, "Estimated bounds overlap a scene collider.");
                    return;
                }
            }

            candidate.state = PungentPlacementCandidateState.Accepted;
            candidate.rejectionReason = PungentPlacementRejectionReason.None;
            candidate.rejectionMessage = null;
        }

        private static List<Vector3> GenerateRawPositions(PungentPlacementContext context, int count, System.Random random)
        {
            return context.scatterPattern switch
            {
                PungentPlacementScatterPattern.Grid => GenerateGrid(context.areaBounds, count, false, random),
                PungentPlacementScatterPattern.JitteredGrid => GenerateGrid(context.areaBounds, count, true, random),
                PungentPlacementScatterPattern.HexGrid => GenerateHex(context.areaBounds, count),
                PungentPlacementScatterPattern.Spiral => GenerateSpiral(context.areaBounds, count),
                PungentPlacementScatterPattern.Ring => GenerateRing(context.areaBounds, count),
                PungentPlacementScatterPattern.Line => GenerateLine(context.areaBounds, count),
                PungentPlacementScatterPattern.RandomMinSpacing => GenerateRandomMinSpacing(context, count, random),
                _ => GenerateRandom(context.areaBounds, count, random)
            };
        }

        private static List<Vector3> GenerateRandom(Bounds bounds, int count, System.Random random)
        {
            List<Vector3> positions = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
                positions.Add(RandomPoint(bounds, random));
            return positions;
        }

        private static List<Vector3> GenerateRandomMinSpacing(PungentPlacementContext context, int count, System.Random random)
        {
            float spacing = context.ruleSet != null ? Mathf.Max(0.01f, context.ruleSet.minimumSpacing) : 1f;
            float spacingSqr = spacing * spacing;
            List<Vector3> positions = new List<Vector3>(count);
            int maxAttempts = Mathf.Max(count * MaxAttemptsMultiplier, count + 10);

            for (int attempt = 0; attempt < maxAttempts && positions.Count < count; attempt++)
            {
                Vector3 candidate = RandomPoint(context.areaBounds, random);
                bool valid = true;
                for (int i = 0; i < positions.Count; i++)
                {
                    if ((positions[i] - candidate).sqrMagnitude < spacingSqr)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                    positions.Add(candidate);
            }

            return positions;
        }

        private static List<Vector3> GenerateGrid(Bounds bounds, int count, bool jitter, System.Random random)
        {
            List<Vector3> positions = new List<Vector3>(count);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count * Mathf.Max(0.1f, bounds.size.x / Mathf.Max(0.1f, bounds.size.z))));
            int rows = Mathf.CeilToInt(count / Mathf.Max(1f, columns));
            float stepX = bounds.size.x / Mathf.Max(1, columns);
            float stepZ = bounds.size.z / Mathf.Max(1, rows);

            for (int z = 0; z < rows && positions.Count < count; z++)
            {
                for (int x = 0; x < columns && positions.Count < count; x++)
                {
                    float jx = jitter ? ((float)random.NextDouble() - 0.5f) * stepX * 0.65f : 0f;
                    float jz = jitter ? ((float)random.NextDouble() - 0.5f) * stepZ * 0.65f : 0f;
                    positions.Add(new Vector3(bounds.min.x + (x + 0.5f) * stepX + jx, bounds.center.y, bounds.min.z + (z + 0.5f) * stepZ + jz));
                }
            }

            return positions;
        }

        private static List<Vector3> GenerateHex(Bounds bounds, int count)
        {
            List<Vector3> positions = new List<Vector3>(count);
            float cell = Mathf.Sqrt((bounds.size.x * bounds.size.z) / Mathf.Max(1, count));
            float dx = cell;
            float dz = cell * 0.8660254f;
            int row = 0;
            for (float z = bounds.min.z + dz * 0.5f; z <= bounds.max.z && positions.Count < count; z += dz, row++)
            {
                float offset = row % 2 == 0 ? 0f : dx * 0.5f;
                for (float x = bounds.min.x + dx * 0.5f + offset; x <= bounds.max.x && positions.Count < count; x += dx)
                    positions.Add(new Vector3(x, bounds.center.y, z));
            }

            return positions;
        }

        private static List<Vector3> GenerateSpiral(Bounds bounds, int count)
        {
            List<Vector3> positions = new List<Vector3>(count);
            float maxRadius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            float golden = 2.39996323f;
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : i / (float)(count - 1);
                float radius = Mathf.Sqrt(t) * maxRadius;
                float angle = i * golden;
                positions.Add(bounds.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            return positions;
        }

        private static List<Vector3> GenerateRing(Bounds bounds, int count)
        {
            List<Vector3> positions = new List<Vector3>(count);
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.85f;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)Mathf.Max(1, count)) * Mathf.PI * 2f;
                positions.Add(bounds.center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            return positions;
        }

        private static List<Vector3> GenerateLine(Bounds bounds, int count)
        {
            List<Vector3> positions = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                positions.Add(new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, t), bounds.center.y, bounds.center.z));
            }

            return positions;
        }

        private static Vector3 RandomPoint(Bounds bounds, System.Random random)
        {
            return new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble()),
                bounds.center.y,
                Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble()));
        }

        private static bool TryProjectToSurface(PungentPlacementContext context, Vector3 raw, out Vector3 position, out Vector3 normal)
        {
            position = raw;
            normal = Vector3.up;
            PungentPlacementRuleSetSO rules = context.ruleSet;
            if (rules == null)
                return false;

            Vector3 origin = new Vector3(raw.x, context.areaBounds.max.y + rules.raycastStartHeight, raw.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rules.raycastDistance, rules.surfaceMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
                normal = hit.normal;
                return true;
            }

            return false;
        }

        private static float GetSurfaceOffset(PungentPlacementContext context)
        {
            return context.ruleSet != null ? context.ruleSet.surfaceOffset : 0f;
        }

        private static bool OverlapsScene(PungentPlacementContext context, PungentPlacementCandidate candidate)
        {
            PungentPlacementRuleSetSO rules = context.ruleSet;
            if (rules == null)
                return false;

            Bounds b = candidate.estimatedBounds;
            b.Expand(rules.overlapPadding);
            QueryTriggerInteraction query = rules.ignoreTriggerColliders ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide;
            Collider[] hits = Physics.OverlapBox(b.center, b.extents, candidate.rotation, rules.overlapMask, query);
            return hits != null && hits.Length > 0;
        }

        private static void Reject(PungentPlacementCandidate candidate, PungentPlacementRejectionReason reason, string message)
        {
            candidate.state = PungentPlacementCandidateState.Rejected;
            candidate.rejectionReason = reason;
            candidate.rejectionMessage = message;
        }
    }

}