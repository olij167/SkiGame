using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.DataSheets
{
    public enum PungentDataSheetDataType
    {
        Text = 0,
        Number = 10,
        Boolean = 20,
        Date = 30,
        EnumText = 40,
        AuthoringReference = 50
    }

    public enum PungentDataSheetCellValidationStatus
    {
        NotRun = 0,
        Valid = 1,
        Warning = 2,
        Error = 3,
        MissingProvider = 4,
        MissingTarget = 5
    }

    public enum PungentDataSheetPropagationStatus
    {
        Clean = 0,
        Changed = 10,
        Applied = 20,
        Skipped = 30,
        Failed = 40
    }

    public enum PungentDataSheetBindingTargetSource
    {
        CurrentSelection = 0,
        ManualObjects = 10,
        AssetFolder = 20
    }

    public enum PungentDataSheetBindingDirection
    {
        TwoWay = 0,
        PullOnly = 10,
        PushOnly = 20
    }

    public enum PungentDataSheetApplyScope
    {
        SelectedCells = 0,
        SelectedRows = 10,
        ChangedCells = 20,
        FullSheet = 30
    }

    public enum PungentDataSheetConflictPolicy
    {
        Overwrite = 0,
        FillBlanksOnly = 10,
        SkipIfProjectChangedSincePull = 20
    }

    public enum PungentDataSheetApplyPreviewStatus
    {
        Pending = 0,
        Ready = 10,
        Skipped = 20,
        Applied = 30,
        Failed = 40
    }

    [Serializable]
    public sealed class PungentDataSheet
    {
        public const int CurrentMigrationVersion = 2;

        public string id = string.Empty;
        public string title = "Untitled Sheet";
        public string summary = string.Empty;
        public List<string> tags = new List<string>();
        public string status = string.Empty;
        public string priority = string.Empty;
        public string visibility = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
        public bool archived;
        public bool developerOnly;
        public bool locked;
        public string generatedBy = string.Empty;
        public string generatedTemplateId = string.Empty;
        public string generatedUtc = string.Empty;
        public int migrationVersion = CurrentMigrationVersion;
        public List<PungentDataSheetColumn> columns = new List<PungentDataSheetColumn>();
        public List<PungentDataSheetRow> rows = new List<PungentDataSheetRow>();
        public List<PungentDataSheetCell> cells = new List<PungentDataSheetCell>();
        public List<PungentAuthoringTarget> targets = new List<PungentAuthoringTarget>();
        public List<PungentAuthoringReference> references = new List<PungentAuthoringReference>();
        public List<PungentDataSheetBindingProfile> bindingProfiles = new List<PungentDataSheetBindingProfile>();
        public string activeBindingProfileId = string.Empty;
        public string lastImportedPath = string.Empty;
        public string lastImportedUtc = string.Empty;

        public PungentAuthoringReference ToReference()
        {
            return PungentAuthoringReference.Create(PungentAuthoringItemKind.DataSheet, id, "data-sheets", title);
        }

        public PungentAuthoringMetadata ToMetadata(string providerId, string packageCapabilityId)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = id,
                title = title,
                summary = summary,
                kind = PungentAuthoringItemKind.DataSheet,
                status = status,
                priority = priority,
                visibility = visibility,
                tags = PungentAuthoringMetadata.NormalizeTags(tags),
                createdUtc = createdUtc,
                updatedUtc = updatedUtc,
                archived = archived,
                developerOnly = developerOnly,
                sourceProviderId = providerId,
                packageCapabilityId = packageCapabilityId,
                extensionId = packageCapabilityId
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            title = string.IsNullOrWhiteSpace(title) ? "Untitled Sheet" : title.Trim();
            summary = summary == null ? string.Empty : summary.Trim();
            status = status == null ? string.Empty : status.Trim();
            priority = priority == null ? string.Empty : priority.Trim();
            visibility = visibility == null ? string.Empty : visibility.Trim();
            generatedBy = generatedBy == null ? string.Empty : generatedBy.Trim();
            generatedTemplateId = PungentAuthoringId.Normalize(generatedTemplateId);
            generatedUtc = NormalizeDateString(generatedUtc);
            createdUtc = NormalizeDateString(createdUtc);
            updatedUtc = NormalizeDateString(updatedUtc);
            if (string.IsNullOrWhiteSpace(createdUtc))
                createdUtc = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrWhiteSpace(updatedUtc))
                updatedUtc = createdUtc;
            migrationVersion = Math.Max(CurrentMigrationVersion, migrationVersion);
            tags = PungentAuthoringMetadata.NormalizeTags(tags);

            if (columns == null)
                columns = new List<PungentDataSheetColumn>();
            if (rows == null)
                rows = new List<PungentDataSheetRow>();
            if (cells == null)
                cells = new List<PungentDataSheetCell>();
            if (targets == null)
                targets = new List<PungentAuthoringTarget>();
            if (references == null)
                references = new List<PungentAuthoringReference>();
            if (bindingProfiles == null)
                bindingProfiles = new List<PungentDataSheetBindingProfile>();

            activeBindingProfileId = PungentAuthoringId.Normalize(activeBindingProfileId);
            lastImportedPath = lastImportedPath == null ? string.Empty : lastImportedPath.Trim();
            lastImportedUtc = NormalizeDateString(lastImportedUtc);

            for (int i = 0; i < columns.Count; i++)
                columns[i]?.NormalizeInPlace();
            for (int i = 0; i < rows.Count; i++)
                rows[i]?.NormalizeInPlace(i);
            for (int i = 0; i < cells.Count; i++)
                cells[i]?.NormalizeInPlace();
            for (int i = 0; i < bindingProfiles.Count; i++)
                bindingProfiles[i]?.NormalizeInPlace();

            columns.RemoveAll(column => column == null || string.IsNullOrWhiteSpace(column.id));
            rows.RemoveAll(row => row == null || string.IsNullOrWhiteSpace(row.id));
            cells.RemoveAll(cell => cell == null || string.IsNullOrWhiteSpace(cell.rowId) || string.IsNullOrWhiteSpace(cell.columnId));
            bindingProfiles.RemoveAll(profile => profile == null || string.IsNullOrWhiteSpace(profile.id));
            if (string.IsNullOrWhiteSpace(activeBindingProfileId) && bindingProfiles.Count > 0)
                activeBindingProfileId = bindingProfiles[0].id;

            for (int i = 0; i < targets.Count; i++)
                targets[i]?.NormalizeInPlace();
            for (int i = 0; i < references.Count; i++)
                references[i]?.NormalizeInPlace();
        }

        public PungentDataSheetColumn AddColumn(string displayName, PungentDataSheetDataType dataType = PungentDataSheetDataType.Text)
        {
            PungentDataSheetColumn column = PungentDataSheetColumn.Create(displayName, dataType);
            columns.Add(column);
            Touch();
            return column;
        }

        public PungentDataSheetRow AddRow(string displayName = null)
        {
            PungentDataSheetRow row = PungentDataSheetRow.Create(displayName, rows != null ? rows.Count : 0);
            rows.Add(row);
            Touch();
            return row;
        }

        public bool DeleteColumn(string columnId)
        {
            if (columns == null || string.IsNullOrWhiteSpace(columnId))
                return false;

            int removed = columns.RemoveAll(column => column != null && PungentAuthoringId.EqualsId(column.id, columnId));
            if (removed > 0 && cells != null)
                cells.RemoveAll(cell => cell != null && PungentAuthoringId.EqualsId(cell.columnId, columnId));
            if (removed > 0)
                Touch();
            return removed > 0;
        }

        public bool DeleteRow(string rowId)
        {
            if (rows == null || string.IsNullOrWhiteSpace(rowId))
                return false;

            int removed = rows.RemoveAll(row => row != null && PungentAuthoringId.EqualsId(row.id, rowId));
            if (removed > 0 && cells != null)
                cells.RemoveAll(cell => cell != null && PungentAuthoringId.EqualsId(cell.rowId, rowId));
            if (removed > 0)
            {
                ReindexRows();
                Touch();
            }

            return removed > 0;
        }

        public PungentDataSheetCell GetOrCreateCell(string rowId, string columnId)
        {
            if (cells == null)
                cells = new List<PungentDataSheetCell>();

            PungentDataSheetCell cell = FindCell(rowId, columnId);
            if (cell != null)
                return cell;

            cell = new PungentDataSheetCell
            {
                rowId = PungentAuthoringId.Normalize(rowId),
                columnId = PungentAuthoringId.Normalize(columnId)
            };
            cells.Add(cell);
            return cell;
        }

        public PungentDataSheetCell FindCell(string rowId, string columnId)
        {
            if (cells == null)
                return null;

            return cells.FirstOrDefault(existing =>
                existing != null &&
                PungentAuthoringId.EqualsId(existing.rowId, rowId) &&
                PungentAuthoringId.EqualsId(existing.columnId, columnId));
        }

        public PungentDataSheetColumn FindColumn(string columnId)
        {
            return columns == null
                ? null
                : columns.FirstOrDefault(column => column != null && PungentAuthoringId.EqualsId(column.id, columnId));
        }

        public PungentDataSheetRow FindRow(string rowId)
        {
            return rows == null
                ? null
                : rows.FirstOrDefault(row => row != null && PungentAuthoringId.EqualsId(row.id, rowId));
        }

        public PungentDataSheetBindingProfile FindBindingProfile(string profileId)
        {
            return bindingProfiles == null
                ? null
                : bindingProfiles.FirstOrDefault(profile => profile != null && PungentAuthoringId.EqualsId(profile.id, profileId));
        }

        public PungentDataSheetBindingProfile GetOrCreateActiveBindingProfile()
        {
            if (bindingProfiles == null)
                bindingProfiles = new List<PungentDataSheetBindingProfile>();

            PungentDataSheetBindingProfile profile = FindBindingProfile(activeBindingProfileId);
            if (profile != null)
                return profile;

            profile = PungentDataSheetBindingProfile.Create("Default Binding");
            bindingProfiles.Add(profile);
            activeBindingProfileId = profile.id;
            Touch();
            return profile;
        }

        public void Touch()
        {
            updatedUtc = DateTime.UtcNow.ToString("o");
        }

        public void ReindexRows()
        {
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                    rows[i].order = i;
            }
        }

        private static string NormalizeDateString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed)
                ? parsed.ToString("o")
                : value.Trim();
        }
    }

    [Serializable]
    public sealed class PungentDataSheetColumn
    {
        public string id = string.Empty;
        public string displayName = "Column";
        public PungentDataSheetDataType dataType = PungentDataSheetDataType.Text;
        public float width = 140f;
        public bool hidden;
        public bool locked;
        public List<string> enumOptions = new List<string>();
        public PungentAuthoringTarget target;
        public PungentAuthoringReference reference;

        public static PungentDataSheetColumn Create(string displayName, PungentDataSheetDataType dataType = PungentDataSheetDataType.Text)
        {
            PungentDataSheetColumn column = new PungentDataSheetColumn
            {
                id = PungentAuthoringId.NewValue(),
                displayName = string.IsNullOrWhiteSpace(displayName) ? "Column" : displayName.Trim(),
                dataType = dataType
            };
            column.NormalizeInPlace();
            return column;
        }

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = string.IsNullOrWhiteSpace(displayName) ? "Column" : displayName.Trim();
            width = Math.Max(64f, width);
            enumOptions = PungentAuthoringMetadata.NormalizeTags(enumOptions);
            target?.NormalizeInPlace();
            reference?.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentDataSheetRow
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public int order;
        public List<string> tags = new List<string>();
        public PungentAuthoringReference linkedAuthoringRef;
        public bool hidden;
        public bool locked;

        public static PungentDataSheetRow Create(string displayName, int order)
        {
            PungentDataSheetRow row = new PungentDataSheetRow
            {
                id = PungentAuthoringId.NewValue(),
                displayName = string.IsNullOrWhiteSpace(displayName) ? "Row " + (order + 1).ToString(CultureInfo.InvariantCulture) : displayName.Trim(),
                order = order
            };
            row.NormalizeInPlace(order);
            return row;
        }

        public void NormalizeInPlace(int fallbackOrder)
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = displayName == null ? string.Empty : displayName.Trim();
            order = Math.Max(0, order < 0 ? fallbackOrder : order);
            tags = PungentAuthoringMetadata.NormalizeTags(tags);
            linkedAuthoringRef?.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentDataSheetCell
    {
        public string rowId = string.Empty;
        public string columnId = string.Empty;
        public string rawValue = string.Empty;
        public string displayValue = string.Empty;
        public PungentDataSheetCellValidationStatus validationStatus = PungentDataSheetCellValidationStatus.NotRun;
        public PungentDataSheetPropagationStatus propagationStatus = PungentDataSheetPropagationStatus.Clean;
        public string lastPulledValue = string.Empty;
        public string lastPulledUtc = string.Empty;
        public string lastAppliedUtc = string.Empty;
        public string propagationMessage = string.Empty;
        public PungentAuthoringReference linkedAuthoringRef;

        public void NormalizeInPlace()
        {
            rowId = PungentAuthoringId.Normalize(rowId);
            columnId = PungentAuthoringId.Normalize(columnId);
            rawValue = rawValue ?? string.Empty;
            displayValue = displayValue ?? string.Empty;
            lastPulledValue = lastPulledValue ?? string.Empty;
            lastPulledUtc = lastPulledUtc ?? string.Empty;
            lastAppliedUtc = lastAppliedUtc ?? string.Empty;
            propagationMessage = propagationMessage ?? string.Empty;
            linkedAuthoringRef?.NormalizeInPlace();
        }

        public string VisibleValue => string.IsNullOrEmpty(displayValue) ? rawValue : displayValue;
    }

    public static class PungentDataSheetAuthoringReferenceCodec
    {
        private const char Separator = '|';

        public static string Format(PungentAuthoringReference reference)
        {
            if (reference == null)
                return string.Empty;

            reference.NormalizeInPlace();
            return string.Join(Separator.ToString(), new[]
            {
                Escape(reference.providerId),
                reference.itemKind.ToString(),
                Escape(reference.itemId),
                Escape(reference.label)
            });
        }

        public static bool TryParse(string rawValue, out PungentAuthoringReference reference)
        {
            reference = null;
            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            string[] parts = Split(rawValue.Trim());
            if (parts.Length == 1)
            {
                reference = PungentAuthoringReference.Create(PungentAuthoringItemKind.Unknown, parts[0]);
                return reference.HasItemId;
            }

            if (parts.Length < 3)
                return false;

            PungentAuthoringItemKind kind;
            if (!Enum.TryParse(parts[1], true, out kind))
                kind = PungentAuthoringItemKind.Unknown;

            reference = new PungentAuthoringReference
            {
                providerId = Unescape(parts[0]),
                itemKind = kind,
                itemId = Unescape(parts[2]),
                label = parts.Length > 3 ? Unescape(parts[3]) : string.Empty
            };
            reference.NormalizeInPlace();
            return reference.HasItemId;
        }

        public static bool TryParseStructured(string rawValue, out PungentAuthoringReference reference)
        {
            reference = null;
            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            string[] parts = Split(rawValue.Trim());
            if (parts.Length < 3)
                return false;

            string providerId = Unescape(parts[0]);
            string itemId = Unescape(parts[2]);
            if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(itemId))
                return false;

            PungentAuthoringItemKind kind;
            if (!Enum.TryParse(parts[1], true, out kind))
                return false;

            reference = new PungentAuthoringReference
            {
                providerId = providerId,
                itemKind = kind,
                itemId = itemId,
                label = parts.Length > 3 ? Unescape(parts[3]) : string.Empty
            };
            reference.NormalizeInPlace();
            return reference.HasItemId && !string.IsNullOrWhiteSpace(reference.providerId);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace(Separator.ToString(), "\\" + Separator);
        }

        private static string Unescape(string value)
        {
            value = value ?? string.Empty;
            string result = string.Empty;
            bool escaped = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (escaped)
                {
                    result += ch;
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else
                {
                    result += ch;
                }
            }

            if (escaped)
                result += "\\";
            return result;
        }

        private static string[] Split(string value)
        {
            List<string> parts = new List<string>();
            string current = string.Empty;
            bool escaped = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (escaped)
                {
                    current += "\\" + ch;
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == Separator)
                {
                    parts.Add(current);
                    current = string.Empty;
                }
                else
                {
                    current += ch;
                }
            }

            if (escaped)
                current += "\\";
            parts.Add(current);
            return parts.ToArray();
        }
    }
}
