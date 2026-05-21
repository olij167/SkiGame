using System;
using System.Collections.Generic;
using System.Globalization;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Authoring;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public static class PungentDataSheetValidation
    {
        public static PungentAuthoringValidationResult ValidateSheet(PungentDataSheet sheet)
        {
            PungentAuthoringValidationResult result = new PungentAuthoringValidationResult
            {
                providerId = PungentDataSheetProvider.Id,
                itemId = sheet != null ? sheet.id ?? string.Empty : string.Empty
            };

            if (sheet == null)
            {
                result.AddIssue(PungentAuthoringValidationIssue.Create(
                    PungentAuthoringValidationSeverity.Error,
                    "Data sheet is missing.",
                    PungentDataSheetProvider.Id,
                    string.Empty,
                    null,
                    "Create sheet",
                    string.Empty,
                    "MISSING_SHEET"));
                return result;
            }

            ValidateIds(sheet, result);
            ValidateCells(sheet, result);
            ValidateAuthoringReferences(sheet, result);
            ValidateBindingProfiles(sheet, result);
            result.RefreshStatus();
            return result;
        }

        private static void ValidateIds(PungentDataSheet sheet, PungentAuthoringValidationResult result)
        {
            HashSet<string> rowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
            {
                if (row == null || string.IsNullOrWhiteSpace(row.id))
                {
                    Add(result, PungentAuthoringValidationSeverity.Error, "A row is missing its stable ID.", "MISSING_ROW_ID", "Rows");
                    continue;
                }

                if (!rowIds.Add(PungentAuthoringId.Normalize(row.id)))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Duplicate row ID '" + row.id + "'.", "DUPLICATE_ROW_ID", row.id);
            }

            HashSet<string> columnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
            {
                if (column == null || string.IsNullOrWhiteSpace(column.id))
                {
                    Add(result, PungentAuthoringValidationSeverity.Error, "A column is missing its stable ID.", "MISSING_COLUMN_ID", "Columns");
                    continue;
                }

                if (!columnIds.Add(PungentAuthoringId.Normalize(column.id)))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Duplicate column ID '" + column.id + "'.", "DUPLICATE_COLUMN_ID", column.id);
            }
        }

        private static void ValidateCells(PungentDataSheet sheet, PungentAuthoringValidationResult result)
        {
            HashSet<string> rowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> columnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
                if (row != null && !string.IsNullOrWhiteSpace(row.id))
                    rowIds.Add(PungentAuthoringId.Normalize(row.id));
            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
                if (column != null && !string.IsNullOrWhiteSpace(column.id))
                    columnIds.Add(PungentAuthoringId.Normalize(column.id));

            foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
            {
                if (cell == null)
                    continue;

                cell.validationStatus = PungentDataSheetCellValidationStatus.Valid;
                string source = (cell.rowId ?? string.Empty) + " / " + (cell.columnId ?? string.Empty);
                bool rowMissing = !rowIds.Contains(PungentAuthoringId.Normalize(cell.rowId));
                bool columnMissing = !columnIds.Contains(PungentAuthoringId.Normalize(cell.columnId));
                if (rowMissing)
                {
                    cell.validationStatus = PungentDataSheetCellValidationStatus.Error;
                    Add(result, PungentAuthoringValidationSeverity.Error, "Cell references missing row ID '" + cell.rowId + "'.", "MISSING_CELL_ROW", source);
                }

                if (columnMissing)
                {
                    cell.validationStatus = PungentDataSheetCellValidationStatus.Error;
                    Add(result, PungentAuthoringValidationSeverity.Error, "Cell references missing column ID '" + cell.columnId + "'.", "MISSING_CELL_COLUMN", source);
                }

                PungentDataSheetColumn column = sheet.FindColumn(cell.columnId);
                if (column != null && !ValidateValueForType(cell.rawValue, column, out string message, out PungentAuthoringValidationSeverity severity))
                {
                    cell.validationStatus = severity == PungentAuthoringValidationSeverity.Error
                        ? PungentDataSheetCellValidationStatus.Error
                        : PungentDataSheetCellValidationStatus.Warning;
                    Add(result, severity, message, "INVALID_" + column.dataType.ToString().ToUpperInvariant(), source);
                }

                if (column != null && column.dataType == PungentDataSheetDataType.AuthoringReference)
                {
                    PungentAuthoringReference reference = cell.linkedAuthoringRef;
                    if ((reference == null || !reference.HasItemId) && !string.IsNullOrWhiteSpace(cell.rawValue))
                    {
                        if (!PungentDataSheetAuthoringReferenceCodec.TryParseStructured(cell.rawValue, out reference))
                        {
                            cell.validationStatus = PungentDataSheetCellValidationStatus.Error;
                            Add(result, PungentAuthoringValidationSeverity.Error, "Invalid authoring reference value '" + cell.rawValue + "'. Use provider|kind|id|label.", "INVALID_AUTHORING_REFERENCE", source);
                        }
                    }

                    if (reference != null && reference.HasItemId)
                    {
                        int before = result.issues.Count;
                        AddReferenceIssues(result, reference, "Authoring reference cell " + source);
                        if (result.issues.Count > before)
                            cell.validationStatus = HasMissingProviderIssue(result, before)
                                ? PungentDataSheetCellValidationStatus.MissingProvider
                                : PungentDataSheetCellValidationStatus.MissingTarget;
                    }
                }
            }
        }

        private static void ValidateAuthoringReferences(PungentDataSheet sheet, PungentAuthoringValidationResult result)
        {
            foreach (PungentAuthoringReference reference in sheet.references ?? new List<PungentAuthoringReference>())
                AddReferenceIssues(result, reference, "Sheet reference");

            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
                if (row != null && row.linkedAuthoringRef != null && row.linkedAuthoringRef.HasItemId)
                    AddReferenceIssues(result, row.linkedAuthoringRef, "Row '" + DisplayRow(row) + "'");

            foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
            {
                if (cell == null || cell.linkedAuthoringRef == null || !cell.linkedAuthoringRef.HasItemId)
                    continue;

                PungentDataSheetColumn column = sheet.FindColumn(cell.columnId);
                if (column != null && column.dataType == PungentDataSheetDataType.AuthoringReference)
                    continue;

                int before = result.issues.Count;
                AddReferenceIssues(result, cell.linkedAuthoringRef, "Cell " + cell.rowId + " / " + cell.columnId);
                if (result.issues.Count > before)
                {
                    cell.validationStatus = HasMissingProviderIssue(result, before)
                        ? PungentDataSheetCellValidationStatus.MissingProvider
                        : PungentDataSheetCellValidationStatus.MissingTarget;
                }
            }
        }

        private static void ValidateBindingProfiles(PungentDataSheet sheet, PungentAuthoringValidationResult result)
        {
            HashSet<string> profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> columnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> rowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
                if (column != null && !string.IsNullOrWhiteSpace(column.id))
                    columnIds.Add(PungentAuthoringId.Normalize(column.id));
            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
                if (row != null && !string.IsNullOrWhiteSpace(row.id))
                    rowIds.Add(PungentAuthoringId.Normalize(row.id));

            foreach (PungentDataSheetBindingProfile profile in sheet.bindingProfiles ?? new List<PungentDataSheetBindingProfile>())
            {
                if (profile == null)
                    continue;

                if (string.IsNullOrWhiteSpace(profile.id))
                {
                    Add(result, PungentAuthoringValidationSeverity.Error, "A binding profile is missing its stable ID.", "MISSING_BINDING_PROFILE_ID", "Bindings");
                    continue;
                }

                if (!profileIds.Add(PungentAuthoringId.Normalize(profile.id)))
                    Add(result, PungentAuthoringValidationSeverity.Error, "Duplicate binding profile ID '" + profile.id + "'.", "DUPLICATE_BINDING_PROFILE_ID", profile.id);

                if (!string.IsNullOrWhiteSpace(profile.rowIdentityColumnId) && !columnIds.Contains(PungentAuthoringId.Normalize(profile.rowIdentityColumnId)))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Binding profile row identity column is missing.", "MISSING_BINDING_IDENTITY_COLUMN", profile.displayName);

                if (!string.IsNullOrWhiteSpace(profile.objectNameColumnId) && !columnIds.Contains(PungentAuthoringId.Normalize(profile.objectNameColumnId)))
                    Add(result, PungentAuthoringValidationSeverity.Warning, "Binding profile object name column is missing.", "MISSING_BINDING_NAME_COLUMN", profile.displayName);

                foreach (PungentDataSheetColumnBinding binding in profile.columnBindings ?? new List<PungentDataSheetColumnBinding>())
                {
                    if (binding == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(binding.columnId) || !columnIds.Contains(PungentAuthoringId.Normalize(binding.columnId)))
                        Add(result, PungentAuthoringValidationSeverity.Warning, "A binding references a missing column.", "MISSING_BOUND_COLUMN", profile.displayName);
                    if ((binding.pullEnabled || binding.pushEnabled) && string.IsNullOrWhiteSpace(binding.propertyPath))
                        Add(result, PungentAuthoringValidationSeverity.Warning, "A binding-enabled column has no SerializedProperty path.", "MISSING_BOUND_PROPERTY_PATH", profile.displayName);
                }

                foreach (PungentDataSheetTargetBinding targetBinding in profile.targetBindings ?? new List<PungentDataSheetTargetBinding>())
                {
                    if (targetBinding == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(targetBinding.rowId) || !rowIds.Contains(PungentAuthoringId.Normalize(targetBinding.rowId)))
                        Add(result, PungentAuthoringValidationSeverity.Warning, "A binding target references a missing row.", "MISSING_BOUND_TARGET_ROW", targetBinding.objectName);
                    if (string.IsNullOrWhiteSpace(targetBinding.targetGlobalId))
                        Add(result, PungentAuthoringValidationSeverity.Warning, "A binding target is missing its Unity global object ID.", "MISSING_BOUND_TARGET_ID", targetBinding.objectName);
                    else if (PungentDataSheetBindingUtility.ResolveGlobalObject(targetBinding.targetGlobalId) == null)
                        Add(result, PungentAuthoringValidationSeverity.Warning, "A binding target could not be resolved. It remains visible and will be skipped on apply.", "STALE_BOUND_TARGET", targetBinding.objectName);
                }
            }
        }

        private static void AddReferenceIssues(PungentAuthoringValidationResult aggregate, PungentAuthoringReference reference, string label)
        {
            PungentAuthoringValidationResult referenceResult = PungentAuthoringValidationAdapter.ValidateReference(reference);
            foreach (PungentAuthoringValidationIssue issue in referenceResult.issues)
            {
                aggregate.AddIssue(PungentAuthoringValidationIssue.Create(
                    issue.severity,
                    label + ": " + issue.message,
                    PungentDataSheetProvider.Id,
                    aggregate.itemId,
                    issue.target,
                    issue.suggestedActionLabel,
                    issue.sourcePathKeyOrId,
                    issue.issueCode));
            }
        }

        private static bool ValidateValueForType(string rawValue, PungentDataSheetColumn column, out string message, out PungentAuthoringValidationSeverity severity)
        {
            message = string.Empty;
            severity = PungentAuthoringValidationSeverity.Error;
            if (string.IsNullOrWhiteSpace(rawValue))
                return true;

            switch (column.dataType)
            {
                case PungentDataSheetDataType.Number:
                    double number;
                    if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    {
                        message = "Invalid number value '" + rawValue + "'.";
                        return false;
                    }
                    break;
                case PungentDataSheetDataType.Boolean:
                    if (!IsBooleanValue(rawValue))
                    {
                        message = "Invalid boolean value '" + rawValue + "'. Use true/false, yes/no, or 1/0.";
                        return false;
                    }
                    break;
                case PungentDataSheetDataType.Date:
                    DateTime date;
                    if (!TryParseSheetDate(rawValue, out date))
                    {
                        message = "Invalid date value '" + rawValue + "'. Use dd/MM/yyyy, yyyy-MM-dd, or another standard date value.";
                        return false;
                    }
                    break;
                case PungentDataSheetDataType.EnumText:
                    if (column.enumOptions != null && column.enumOptions.Count > 0 &&
                        !column.enumOptions.Exists(option => string.Equals(option, rawValue, StringComparison.OrdinalIgnoreCase)))
                    {
                        severity = PungentAuthoringValidationSeverity.Warning;
                        message = "Value '" + rawValue + "' is not in enum options for column '" + column.displayName + "'.";
                        return false;
                    }
                    break;
            }

            return true;
        }

        private static bool IsBooleanValue(string value)
        {
            string clean = (value ?? string.Empty).Trim();
            return string.Equals(clean, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "false", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "no", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "0", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseSheetDate(string value, out DateTime date)
        {
            date = DateTime.MinValue;
            string text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string[] formats =
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "MM/dd/yyyy",
                "M/d/yyyy",
                "o"
            };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
                return true;
            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date))
                return true;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
        }

        private static bool HasMissingProviderIssue(PungentAuthoringValidationResult result, int startIndex)
        {
            for (int i = startIndex; i < result.issues.Count; i++)
            {
                if (result.issues[i] != null && string.Equals(result.issues[i].issueCode, "MISSING_PROVIDER", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void Add(PungentAuthoringValidationResult result, PungentAuthoringValidationSeverity severity, string message, string code, string source)
        {
            result.AddIssue(PungentAuthoringValidationIssue.Create(
                severity,
                message,
                PungentDataSheetProvider.Id,
                result.itemId,
                null,
                string.Empty,
                source,
                code));
        }

        private static string DisplayRow(PungentDataSheetRow row)
        {
            if (row == null)
                return "Row";

            return string.IsNullOrWhiteSpace(row.displayName) ? row.id : row.displayName;
        }
    }
#endif
}
