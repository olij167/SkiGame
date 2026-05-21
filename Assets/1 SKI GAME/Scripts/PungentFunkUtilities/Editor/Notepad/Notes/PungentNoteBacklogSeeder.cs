using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public static class PungentNoteBacklogSeeder
    {
        public static List<PungentBacklogSeedItem> CuratedSeeds()
        {
            List<PungentBacklogSeedItem> seeds = new List<PungentBacklogSeedItem>();

            AddGroup(seeds, "Core / Architecture", "core", PungentNotePriority.Important, new[]
            {
                "Per-lab asmdefs", "Optional bridge packages", "Package manifests", "TMP optional dependency or bridge",
                "Toolbar quick launch/status integration", "SceneView overlay integration", "Keyboard shortcuts / command palette",
                "Selection-aware launch behaviour", "Minimizer Tray tab-strip behaviour", "USS bridge text-style controls",
                "Theme preset font/licence workflow", "Shared scan/cache migration", "Final documentation/export readiness"
            });

            AddGroup(seeds, "Project Audit and Authoring", "audit", PungentNotePriority.Important, new[]
            {
                "Notes & Roadmap contextual surfaces", "Token Validator overhaul", "ScriptableObject generator",
                "ScriptableObject validation/repair", "Batch asset creation from text/CSV/table", "Dependency preview/graph tools",
                "Deeper docs/adapters for Component Tuning Copy"
            });

            AddGroup(seeds, "Asset Placement", "placement", PungentNotePriority.Important, new[]
            {
                "Stamp placement", "Selection array placement", "Socket graph / modular tile placement", "Cluster growth placement",
                "Physics drop / settle placement", "Connector / bridge placement", "Bounds fill / room dressing", "Replace variations",
                "Asset set validator", "Socket library validator", "Terrain-layer/contextual placement rules",
                "Colour Lab prefab/material variation bridge", "Texture Lab density-mask bridge", "Backtracking/solver experiments"
            });

            AddGroup(seeds, "Colour", "colour", PungentNotePriority.NiceToHave, new[]
            {
                "Stronger harmony differentiation", "Richer tone/contrast/temperature generation", "Weighted harmony mixer",
                "Filter influence scalar", "Include selected colour / selected palette toggles", "Scene colour tracker",
                "Reference libraries such as RAL, XKCD, NCS", "Palette report card", "Contrast matrix visualization",
                "Colour blending/tone matrix", "Gradient/tint tools", "Import/export formats such as .aco"
            });

            AddGroup(seeds, "Audio", "audio", PungentNotePriority.NiceToHave, new[]
            {
                "Runtime audio cue catalog/director", "Mixer setup utility", "Standalone AudioSource pool", "Runtime cue hooks",
                "Cue coverage integration with future director"
            });

            AddGroup(seeds, "UI and Feedback", "ui-feedback", PungentNotePriority.NiceToHave, new[]
            {
                "Runtime input prompt icon library", "Input prompt resolver/token/presenter", "Input prompt preview window",
                "World prompt registry/presenter", "Floating text system", "Popup notification system", "Runtime tooltip presenter",
                "Context menu helper", "UI animation trigger/profile system", "Progress/resource VisualElements"
            });

            AddGroup(seeds, "Environment / Settings / Action / Agent / Appearance / Visualization", "systems", PungentNotePriority.NiceToHave, new[]
            {
                "Wind controller and adapters", "Time/calendar core", "Weather presets/transitions", "Surface condition broadcaster",
                "Local environment volumes", "Settings profile/service", "JSON storage/versioning", "Save slot manifest/service",
                "Input binding override storage", "Generic action sequence framework", "Action conditions/effects/targeting",
                "Move resolver / combo / style meter", "Appearance option catalog/validator", "Agent simulation / utility AI framework",
                "Shared matrix/graph/heatmap visualizers", "Radar/spider graph widgets"
            });

            return seeds;
        }

        public static List<PungentBacklogSeedPreviewItem> BuildPreview(IEnumerable<PungentBacklogSeedItem> seeds)
        {
            return (seeds ?? Enumerable.Empty<PungentBacklogSeedItem>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.title))
                .Select(s =>
                {
                    string reason;
                    bool duplicate = IsDuplicate(s, out reason);
                    return new PungentBacklogSeedPreviewItem
                    {
                        seed = s,
                        duplicate = duplicate,
                        skipReason = reason,
                        selected = !duplicate
                    };
                })
                .ToList();
        }

        public static PungentBacklogSeedApplyResult ApplySelected(IEnumerable<PungentBacklogSeedPreviewItem> preview, string importSourceId = null)
        {
            PungentBacklogSeedApplyResult result = new PungentBacklogSeedApplyResult();
            foreach (PungentBacklogSeedPreviewItem item in preview ?? Enumerable.Empty<PungentBacklogSeedPreviewItem>())
            {
                if (item == null || item.seed == null || !item.selected)
                    continue;

                if (IsDuplicate(item.seed, out string reason))
                {
                    item.duplicate = true;
                    item.skipReason = reason;
                    result.skipped++;
                    continue;
                }

                PungentFutureUtilityRecord future = null;
                if (item.seed.createFutureUtilityRecord || item.seed.kind == PungentNoteKind.FutureUtility)
                {
                    future = CreateFutureUtility(item.seed);
                    result.futureUtilityIds.Add(future.id);
                    result.futureUtilitiesCreated++;
                }

                PungentNote note = CreateNote(item.seed, future, importSourceId);
                if (note != null)
                {
                    result.noteIds.Add(note.id);
                    result.notesCreated++;
                }
            }

            if (result.notesCreated > 0 || result.futureUtilitiesCreated > 0)
                PungentNoteStorage.Save();

            return result;
        }

        public static bool IsDuplicate(PungentBacklogSeedItem seed, out string reason)
        {
            reason = string.Empty;
            if (seed == null)
                return false;

            PungentNoteDatabase db = PungentNoteStorage.Database;
            string key = seed.stableKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(key) && db.notes.Any(n => n != null && !n.archived && (StringEquals(n.stableKey, key) || Contains(n.body, "Seed Key: " + key))))
            {
                reason = "Existing note with seed key";
                return true;
            }

            if (db.notes.Any(n => n != null && !n.archived && StringEquals(n.title, seed.title) && HasTag(n, AreaTag(seed.area))))
            {
                reason = "Existing note title and area";
                return true;
            }

            string futureName = string.IsNullOrWhiteSpace(seed.suggestedFutureUtilityName) ? seed.title : seed.suggestedFutureUtilityName;
            if (db.futureUtilities.Any(f => f != null && ((!string.IsNullOrWhiteSpace(key) && StringEquals(f.stableKey, key)) || StringEquals(f.displayName, futureName) || (!string.IsNullOrWhiteSpace(seed.linkedUtilityId) && StringEquals(f.relatedRegistryId, seed.linkedUtilityId)))))
            {
                reason = "Existing future utility";
                return true;
            }

            return false;
        }

        private static PungentNote CreateNote(PungentBacklogSeedItem seed, PungentFutureUtilityRecord future, string importSourceId)
        {
            string now = DateTime.UtcNow.ToString("o");
            PungentNote note = new PungentNote
            {
                id = Guid.NewGuid().ToString("N"),
                title = seed.title,
                kind = seed.kind,
                stableKey = seed.stableKey,
                status = seed.status,
                priority = seed.priority,
                linkedUtilityId = seed.linkedUtilityId ?? string.Empty,
                linkedFutureUtilityId = future != null ? future.id : seed.futureUtilityId ?? string.Empty,
                tags = MergeTags(seed.tags, seed.area, "seeded", "backlog"),
                body = BuildBody(seed),
                createdUtc = now,
                updatedUtc = now
            };
            if (!string.IsNullOrWhiteSpace(importSourceId))
                note.importSourceIds.Add(importSourceId);
            PungentNoteStorage.Database.notes.Add(note);
            return note;
        }

        private static PungentFutureUtilityRecord CreateFutureUtility(PungentBacklogSeedItem seed)
        {
            PungentFutureUtilityRecord record = new PungentFutureUtilityRecord
            {
                id = Guid.NewGuid().ToString("N"),
                stableKey = seed.stableKey,
                displayName = string.IsNullOrWhiteSpace(seed.suggestedFutureUtilityName) ? seed.title : seed.suggestedFutureUtilityName,
                area = seed.area,
                description = seed.description,
                status = seed.status,
                priority = seed.priority,
                relatedRegistryId = seed.linkedUtilityId ?? string.Empty,
                tags = MergeTags(seed.tags, seed.area, "seeded", "future-utility")
            };
            PungentNoteStorage.Database.futureUtilities.Add(record);
            return record;
        }

        private static void AddGroup(List<PungentBacklogSeedItem> seeds, string area, string prefix, PungentNotePriority priority, IEnumerable<string> titles)
        {
            foreach (string title in titles)
            {
                bool largeSystem = title.IndexOf("runtime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   title.IndexOf("framework", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   title.IndexOf("system", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   title.IndexOf("utility", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   title.IndexOf("controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   title.IndexOf("service", StringComparison.OrdinalIgnoreCase) >= 0;

                seeds.Add(new PungentBacklogSeedItem
                {
                    stableKey = prefix + "." + Slug(title),
                    title = title,
                    suggestedFutureUtilityName = title,
                    description = "Curated Phase 2 backlog seed for " + area + ".",
                    area = area,
                    kind = largeSystem ? PungentNoteKind.FutureUtility : PungentNoteKind.FutureFeature,
                    status = PungentNoteStatus.ToDo,
                    priority = priority,
                    createFutureUtilityRecord = largeSystem,
                    tags = new List<string> { prefix }
                });
            }
        }

        private static string BuildBody(PungentBacklogSeedItem seed)
        {
            return (seed.description ?? string.Empty).Trim() +
                   "\n\nArea: " + seed.area +
                   "\nSeed Key: " + seed.stableKey;
        }

        private static List<string> MergeTags(IEnumerable<string> source, params string[] extra)
        {
            return (source ?? Enumerable.Empty<string>())
                .Concat(extra ?? new string[0])
                .Select(t => (t ?? string.Empty).Trim().TrimStart('#'))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string AreaTag(string area) => Slug(area);

        private static string Slug(string value)
        {
            string lower = (value ?? string.Empty).Trim().ToLowerInvariant();
            char[] chars = lower.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            return new string(chars).Trim('-').Replace("--", "-");
        }

        private static bool HasTag(PungentNote note, string tag)
        {
            return note.tags != null && note.tags.Any(t => StringEquals(t, tag));
        }

        private static bool StringEquals(string a, string b) => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        private static bool Contains(string value, string needle) => !string.IsNullOrEmpty(value) && value.IndexOf(needle ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
    }
#endif
}
