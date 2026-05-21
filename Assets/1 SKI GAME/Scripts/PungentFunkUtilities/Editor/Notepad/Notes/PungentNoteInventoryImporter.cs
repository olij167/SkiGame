using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    public static class PungentNoteInventoryImporter
    {
        public static List<PungentBacklogSeedItem> ParseCsvFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new List<PungentBacklogSeedItem>();

            return ParseCsv(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
        }

        public static List<PungentBacklogSeedItem> ParseCsv(string csv, string sourceName)
        {
            List<string[]> rows = ParseRows(csv);
            if (rows.Count == 0)
                return new List<PungentBacklogSeedItem>();

            Dictionary<string, int> header = BuildHeader(rows[0]);
            List<PungentBacklogSeedItem> seeds = new List<PungentBacklogSeedItem>();
            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string title = Get(row, header, "desired utility / feature", "desired utility", "feature", "utility", "title", "name");
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                string area = Get(row, header, "area / lab", "area", "lab", "category", "module");
                string status = Get(row, header, "status", "implementation status");
                string usefulness = Get(row, header, "usefulness", "priority", "value");
                string difficulty = Get(row, header, "difficulty", "complexity");
                string fit = Get(row, header, "fit / relevance", "fit", "relevance");
                string evidence = Get(row, header, "current evidence", "evidence");
                string notes = Get(row, header, "notes", "note", "details");
                string key = "inventory." + Slug(sourceName) + "." + Slug(area) + "." + Slug(title);

                PungentNoteStatus mappedStatus = MapStatus(status);
                PungentNotePriority mappedPriority = MapPriority(usefulness);
                bool futureUtility = LooksLikeFutureUtility(title);
                seeds.Add(new PungentBacklogSeedItem
                {
                    stableKey = key,
                    title = title.Trim(),
                    suggestedFutureUtilityName = title.Trim(),
                    description = BuildDescription(fit, evidence, notes, difficulty),
                    area = string.IsNullOrWhiteSpace(area) ? "Imported Inventory" : area.Trim(),
                    kind = futureUtility ? PungentNoteKind.FutureUtility : PungentNoteKind.FutureFeature,
                    status = mappedStatus,
                    priority = mappedPriority,
                    createFutureUtilityRecord = futureUtility,
                    tags = new List<string> { "imported", "inventory", "difficulty-" + Slug(difficulty) }
                });
            }

            return seeds;
        }

        private static string BuildDescription(string fit, string evidence, string notes, string difficulty)
        {
            StringBuilder sb = new StringBuilder();
            Append(sb, "Fit / relevance", fit);
            Append(sb, "Current evidence", evidence);
            Append(sb, "Notes", notes);
            Append(sb, "Difficulty", difficulty);
            return sb.ToString().Trim();
        }

        private static void Append(StringBuilder sb, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(label).Append(": ").Append(value.Trim());
        }

        private static PungentNoteStatus MapStatus(string value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Contains("partial") || v.Contains("progress"))
                return PungentNoteStatus.InProgress;
            if (v.Contains("scaffold"))
                return PungentNoteStatus.NeedsReview;
            if (v.Contains("defer"))
                return PungentNoteStatus.Deferred;
            if (v.Contains("out of scope") || v.Contains("out-of-scope"))
                return PungentNoteStatus.OutOfScope;
            if (v.Contains("fully") || v.Contains("complete") || v.Contains("implemented"))
                return PungentNoteStatus.Complete;
            return PungentNoteStatus.ToDo;
        }

        private static PungentNotePriority MapPriority(string value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (v.Contains("critical") || v.Contains("crucial"))
                return PungentNotePriority.Crucial;
            if (v.Contains("high") || v.Contains("important"))
                return PungentNotePriority.Important;
            if (v.Contains("low"))
                return PungentNotePriority.Low;
            return PungentNotePriority.NiceToHave;
        }

        private static bool LooksLikeFutureUtility(string title)
        {
            string t = (title ?? string.Empty).ToLowerInvariant();
            return t.Contains("utility") || t.Contains("system") || t.Contains("framework") || t.Contains("service") || t.Contains("validator") || t.Contains("generator") || t.Contains("window") || t.Contains("tool");
        }

        private static Dictionary<string, int> BuildHeader(string[] row)
        {
            Dictionary<string, int> header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < row.Length; i++)
            {
                string key = NormalizeHeader(row[i]);
                if (!string.IsNullOrWhiteSpace(key) && !header.ContainsKey(key))
                    header[key] = i;
            }
            return header;
        }

        private static string Get(string[] row, Dictionary<string, int> header, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (header.TryGetValue(NormalizeHeader(names[i]), out int index) && index >= 0 && index < row.Length)
                    return row[index] ?? string.Empty;
            }
            return string.Empty;
        }

        private static string NormalizeHeader(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
        }

        private static List<string[]> ParseRows(string csv)
        {
            List<string[]> rows = new List<string[]>();
            if (string.IsNullOrEmpty(csv))
                return rows;

            List<string> row = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if ((c == '\n' || c == '\r') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                        i++;
                    row.Add(cell.ToString());
                    cell.Length = 0;
                    if (row.Any(v => !string.IsNullOrWhiteSpace(v)))
                        rows.Add(row.ToArray());
                    row.Clear();
                }
                else
                {
                    cell.Append(c);
                }
            }

            row.Add(cell.ToString());
            if (row.Any(v => !string.IsNullOrWhiteSpace(v)))
                rows.Add(row.ToArray());

            return rows;
        }

        private static string Slug(string value)
        {
            string lower = (value ?? string.Empty).Trim().ToLowerInvariant();
            char[] chars = lower.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            return new string(chars).Trim('-').Replace("--", "-");
        }
    }
#endif
}
