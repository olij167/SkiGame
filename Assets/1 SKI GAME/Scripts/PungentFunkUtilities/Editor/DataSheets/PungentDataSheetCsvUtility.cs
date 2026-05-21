using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.DataSheets;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public static class PungentDataSheetCsvUtility
    {
        public enum ColumnMappingMode
        {
            MatchHeaders = 0,
            ReuseExistingByPosition = 1,
            CreateNewColumns = 2
        }

        public sealed class ExportOptions
        {
            public bool visibleColumnsOnly;
            public bool includeHiddenRows = true;
            public bool useDisplayValues;
            public string dateDisplayFormat = "dd/MM/yyyy";
        }

        public sealed class ImportPreview
        {
            public string path = string.Empty;
            public int totalRows;
            public int dataRows;
            public int csvColumns;
            public int matchedColumns;
            public int createdColumns;
            public ColumnMappingMode columnMappingMode;
            public string error = string.Empty;

            public string Summary =>
                string.IsNullOrWhiteSpace(error)
                    ? "CSV preview: " + dataRows + " data row(s), " + csvColumns + " column(s), " +
                      matchedColumns + " matched / " + createdColumns + " new column(s)."
                    : error;
        }

        private sealed class InferredColumnInfo
        {
            public PungentDataSheetDataType dataType = PungentDataSheetDataType.Text;
            public readonly List<string> enumOptions = new List<string>();
        }

        public static void ExportToCsv(PungentDataSheet sheet, string path)
        {
            ExportToCsv(sheet, path, new ExportOptions());
        }

        public static void ExportToCsv(PungentDataSheet sheet, string path, ExportOptions options)
        {
            if (sheet == null || string.IsNullOrWhiteSpace(path))
                return;

            options = options ?? new ExportOptions();
            sheet.NormalizeInPlace();
            List<PungentDataSheetColumn> columns = new List<PungentDataSheetColumn>();
            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
                if (column != null && (!options.visibleColumnsOnly || !column.hidden))
                    columns.Add(column);

            List<PungentDataSheetRow> rows = new List<PungentDataSheetRow>();
            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
                if (row != null && (options.includeHiddenRows || !row.hidden))
                    rows.Add(row);

            StringBuilder builder = new StringBuilder();

            WriteCsvRow(builder, columns.ConvertAll(column => column != null ? column.displayName : string.Empty));
            for (int r = 0; r < rows.Count; r++)
            {
                PungentDataSheetRow row = rows[r];
                if (row == null)
                    continue;

                List<string> values = new List<string>(columns.Count);
                for (int c = 0; c < columns.Count; c++)
                {
                    PungentDataSheetColumn column = columns[c];
                    PungentDataSheetCell cell = column == null ? null : sheet.FindCell(row.id, column.id);
                    values.Add(cell != null ? (options.useDisplayValues ? FormatDisplayValue(cell, column, options) : cell.rawValue ?? string.Empty) : string.Empty);
                }

                WriteCsvRow(builder, values);
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        public static PungentDataSheet ImportFromCsv(string path, bool firstRowIsHeaders)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            List<List<string>> records = Parse(File.ReadAllText(path));
            if (records.Count == 0)
                return null;

            PungentDataSheet sheet = new PungentDataSheet
            {
                title = Path.GetFileNameWithoutExtension(path),
                summary = "Imported from CSV: " + Path.GetFileName(path),
                createdUtc = DateTime.UtcNow.ToString("o"),
                updatedUtc = DateTime.UtcNow.ToString("o"),
                lastImportedPath = path,
                lastImportedUtc = DateTime.UtcNow.ToString("o")
            };

            int columnCount = 0;
            for (int i = 0; i < records.Count; i++)
                columnCount = Math.Max(columnCount, records[i] != null ? records[i].Count : 0);

            int firstDataRow = firstRowIsHeaders ? 1 : 0;
            List<InferredColumnInfo> inferredTypes = InferColumnTypes(records, firstDataRow, columnCount);
            for (int c = 0; c < columnCount; c++)
            {
                string header = firstRowIsHeaders && records[0] != null && c < records[0].Count
                    ? CleanHeader(records[0][c])
                    : "Column " + (c + 1);
                PungentDataSheetColumn column = PungentDataSheetColumn.Create(string.IsNullOrWhiteSpace(header) ? "Column " + (c + 1) : header, inferredTypes[c].dataType);
                ApplyInferredColumnMetadata(column, inferredTypes[c]);
                sheet.columns.Add(column);
            }

            for (int r = firstDataRow; r < records.Count; r++)
            {
                List<string> record = records[r] ?? new List<string>();
                PungentDataSheetRow row = sheet.AddRow("Row " + (sheet.rows.Count + 1));
                for (int c = 0; c < sheet.columns.Count; c++)
                {
                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, sheet.columns[c].id);
                    ApplyImportedCellValue(cell, c < record.Count ? record[c] ?? string.Empty : string.Empty, sheet.columns[c]);
                }
            }

            sheet.NormalizeInPlace();
            return sheet;
        }

        public static bool ImportIntoExisting(PungentDataSheet sheet, string path, bool firstRowIsHeaders, bool matchExistingColumnsByHeader, out string summary)
        {
            return ImportIntoExisting(sheet, path, firstRowIsHeaders, matchExistingColumnsByHeader ? ColumnMappingMode.MatchHeaders : ColumnMappingMode.CreateNewColumns, out summary);
        }

        public static bool TryPreviewImport(PungentDataSheet sheet, string path, bool firstRowIsHeaders, ColumnMappingMode columnMappingMode, out ImportPreview preview)
        {
            preview = new ImportPreview
            {
                path = path ?? string.Empty,
                columnMappingMode = columnMappingMode
            };

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                preview.error = "Choose an existing CSV file before previewing import.";
                return false;
            }

            List<List<string>> records = Parse(File.ReadAllText(path));
            if (records.Count == 0)
            {
                preview.error = "CSV did not contain any rows.";
                return false;
            }

            preview.totalRows = records.Count;
            preview.dataRows = Math.Max(0, records.Count - (firstRowIsHeaders ? 1 : 0));
            for (int i = 0; i < records.Count; i++)
                preview.csvColumns = Math.Max(preview.csvColumns, records[i] != null ? records[i].Count : 0);

            for (int c = 0; c < preview.csvColumns; c++)
            {
                string header = firstRowIsHeaders && records[0] != null && c < records[0].Count
                    ? CleanHeader(records[0][c])
                    : "Column " + (c + 1);

                bool matched = false;
                if (sheet != null)
                {
                    if (columnMappingMode == ColumnMappingMode.MatchHeaders)
                        matched = FindColumnByName(sheet, header) != null;
                    else if (columnMappingMode == ColumnMappingMode.ReuseExistingByPosition && sheet.columns != null && c < sheet.columns.Count)
                        matched = true;
                }

                if (matched)
                    preview.matchedColumns++;
                else
                    preview.createdColumns++;
            }

            return true;
        }

        public static bool ImportIntoExisting(PungentDataSheet sheet, string path, bool firstRowIsHeaders, ColumnMappingMode columnMappingMode, out string summary)
        {
            summary = string.Empty;
            if (sheet == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                summary = "Sheet or CSV path is missing.";
                return false;
            }

            List<List<string>> records = Parse(File.ReadAllText(path));
            if (records.Count == 0)
            {
                summary = "CSV did not contain any rows.";
                return false;
            }

            sheet.NormalizeInPlace();
            int columnCount = 0;
            for (int i = 0; i < records.Count; i++)
                columnCount = Math.Max(columnCount, records[i] != null ? records[i].Count : 0);

            List<InferredColumnInfo> inferredTypes = InferColumnTypes(records, firstRowIsHeaders ? 1 : 0, columnCount);
            List<PungentDataSheetColumn> mappedColumns = new List<PungentDataSheetColumn>(columnCount);
            int matchedColumns = 0;
            int createdColumns = 0;
            for (int c = 0; c < columnCount; c++)
            {
                string header = firstRowIsHeaders && records[0] != null && c < records[0].Count
                    ? CleanHeader(records[0][c])
                    : "Column " + (c + 1);

                PungentDataSheetColumn column = null;
                if (columnMappingMode == ColumnMappingMode.MatchHeaders)
                    column = FindColumnByName(sheet, header);
                else if (columnMappingMode == ColumnMappingMode.ReuseExistingByPosition && sheet.columns != null && c < sheet.columns.Count)
                    column = sheet.columns[c];

                if (column != null)
                    matchedColumns++;
                if (column == null)
                {
                    column = sheet.AddColumn(string.IsNullOrWhiteSpace(header) ? "Column " + (c + 1) : header, inferredTypes[c].dataType);
                    ApplyInferredColumnMetadata(column, inferredTypes[c]);
                    createdColumns++;
                }

                mappedColumns.Add(column);
            }

            int firstDataRow = firstRowIsHeaders ? 1 : 0;
            int addedRows = 0;
            for (int r = firstDataRow; r < records.Count; r++)
            {
                List<string> record = records[r] ?? new List<string>();
                PungentDataSheetRow row = sheet.AddRow("Imported Row " + (sheet.rows.Count + 1));
                addedRows++;
                for (int c = 0; c < mappedColumns.Count; c++)
                {
                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, mappedColumns[c].id);
                    ApplyImportedCellValue(cell, c < record.Count ? record[c] ?? string.Empty : string.Empty, mappedColumns[c]);
                    cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
                }
            }

            sheet.lastImportedPath = path;
            sheet.lastImportedUtc = DateTime.UtcNow.ToString("o");
            sheet.Touch();
            summary = "Imported " + addedRows + " row(s), mapped " + mappedColumns.Count + " column(s) (" +
                      matchedColumns + " matched / " + createdColumns + " new). Existing cells were not overwritten.";
            return true;
        }

        private static List<InferredColumnInfo> InferColumnTypes(List<List<string>> records, int firstDataRow, int columnCount)
        {
            List<InferredColumnInfo> types = new List<InferredColumnInfo>(columnCount);
            for (int c = 0; c < columnCount; c++)
            {
                string header = firstDataRow > 0 && records != null && records.Count > 0 && records[0] != null && c < records[0].Count
                    ? records[0][c]
                    : string.Empty;
                types.Add(InferColumnType(records, firstDataRow, c, header));
            }
            return types;
        }

        private static InferredColumnInfo InferColumnType(List<List<string>> records, int firstDataRow, int columnIndex, string header)
        {
            InferredColumnInfo info = new InferredColumnInfo();
            int valueCount = 0;
            int booleanCount = 0;
            int numberCount = 0;
            int dateCount = 0;
            int structuredRefCount = 0;
            bool longTextSeen = false;
            bool headerSuggestsDate = HeaderSuggestsDate(header);
            bool headerSuggestsEnum = HeaderSuggestsEnum(header);
            HashSet<string> distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int r = Math.Max(0, firstDataRow); r < (records != null ? records.Count : 0); r++)
            {
                List<string> record = records[r];
                string value = record != null && columnIndex < record.Count ? record[columnIndex] ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                valueCount++;
                if (IsBooleanLiteral(value))
                    booleanCount++;
                if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    numberCount++;
                if ((LooksLikeDate(value) || headerSuggestsDate) && TryParseDate(value, "dd/MM/yyyy", out _))
                    dateCount++;
                if (PungentDataSheetAuthoringReferenceCodec.TryParseStructured(value, out _))
                    structuredRefCount++;
                string clean = value.Trim();
                if (clean.Length > 48 || clean.IndexOf('\n') >= 0 || clean.IndexOf('\r') >= 0)
                    longTextSeen = true;
                AddDistinct(info.enumOptions, clean);
                distinct.Add(clean);
            }

            if (valueCount == 0)
                return info;

            if (booleanCount == valueCount)
                info.dataType = PungentDataSheetDataType.Boolean;
            else if (numberCount == valueCount)
                info.dataType = PungentDataSheetDataType.Number;
            else if (dateCount == valueCount ||
                     (!headerSuggestsEnum && dateCount >= Math.Max(2, valueCount - 1)) ||
                     (headerSuggestsDate && dateCount > 0 && dateCount >= Math.Max(1, valueCount - 2)))
                info.dataType = PungentDataSheetDataType.Date;
            else if (structuredRefCount == valueCount)
                info.dataType = PungentDataSheetDataType.AuthoringReference;
            else if (!longTextSeen && ShouldInferEnum(header, distinct.Count, valueCount, dateCount))
                info.dataType = PungentDataSheetDataType.EnumText;

            if (info.dataType != PungentDataSheetDataType.EnumText)
                info.enumOptions.Clear();
            return info;
        }

        private static void ApplyInferredColumnMetadata(PungentDataSheetColumn column, InferredColumnInfo info)
        {
            if (column == null || info == null)
                return;

            if (info.dataType == PungentDataSheetDataType.EnumText)
                column.enumOptions = PungentAuthoringMetadata.NormalizeTags(info.enumOptions);
        }

        private static void ApplyImportedCellValue(PungentDataSheetCell cell, string value, PungentDataSheetColumn column)
        {
            if (cell == null)
                return;

            string raw = NormalizeImportedRawValue(value, column);
            cell.rawValue = raw;
            cell.displayValue = FormatImportedDisplayValue(raw, column);
            cell.linkedAuthoringRef = column != null &&
                                      column.dataType == PungentDataSheetDataType.AuthoringReference &&
                                      PungentDataSheetAuthoringReferenceCodec.TryParseStructured(raw, out PungentAuthoringReference reference)
                ? reference
                : null;
            cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
        }

        private static string NormalizeImportedRawValue(string value, PungentDataSheetColumn column)
        {
            string raw = value ?? string.Empty;
            if (column == null)
                return raw;

            switch (column.dataType)
            {
                case PungentDataSheetDataType.Boolean:
                    if (IsBooleanLiteral(raw))
                        return IsTruthy(raw) ? "true" : "false";
                    return raw;
                case PungentDataSheetDataType.Number:
                    return raw.Trim();
                case PungentDataSheetDataType.Date:
                    return TryParseDate(raw, "dd/MM/yyyy", out DateTime date)
                        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : raw;
                default:
                    return raw;
            }
        }

        private static string FormatImportedDisplayValue(string raw, PungentDataSheetColumn column)
        {
            raw = raw ?? string.Empty;
            if (column == null)
                return raw;

            switch (column.dataType)
            {
                case PungentDataSheetDataType.Boolean:
                    return string.IsNullOrWhiteSpace(raw) ? string.Empty : IsTruthy(raw) ? "TRUE" : "FALSE";
                case PungentDataSheetDataType.Date:
                    return TryParseDate(raw, "dd/MM/yyyy", out DateTime date)
                        ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                        : raw;
                case PungentDataSheetDataType.AuthoringReference:
                    return PungentDataSheetAuthoringReferenceCodec.TryParseStructured(raw, out PungentAuthoringReference reference) && reference != null
                        ? string.IsNullOrWhiteSpace(reference.label) ? reference.itemId : reference.label
                        : raw;
                default:
                    return raw;
            }
        }

        private static bool HeaderSuggestsDate(string header)
        {
            string clean = NormalizeHeaderName(header).ToLowerInvariant();
            return clean.Contains("date") ||
                   clean.EndsWith("utc", StringComparison.Ordinal) ||
                   clean.Contains("created") ||
                   clean.Contains("updated") ||
                   clean.Contains("timestamp") ||
                   clean.Contains("time");
        }

        private static bool HeaderSuggestsEnum(string header)
        {
            string clean = NormalizeHeaderName(header).ToLowerInvariant();
            return clean.Contains("enum") ||
                   clean.Contains("status") ||
                   clean.Contains("state") ||
                   clean.Contains("type") ||
                   clean.Contains("kind") ||
                   clean.Contains("category") ||
                   clean.Contains("priority") ||
                   clean.Contains("visibility") ||
                   clean.Contains("mode") ||
                   clean.Contains("phase") ||
                   clean.Contains("role") ||
                   clean.Contains("group") ||
                   clean.Contains("class");
        }

        private static bool ShouldInferEnum(string header, int distinctCount, int valueCount, int dateCount)
        {
            if (dateCount >= Math.Max(2, valueCount - 1))
                return false;

            if (HeaderSuggestsEnum(header) && distinctCount <= 32)
                return true;

            if (valueCount < 3 || distinctCount <= 0 || distinctCount == valueCount)
                return false;

            return distinctCount <= Math.Min(12, Math.Max(2, valueCount / 2));
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
                return;

            string clean = value.Trim();
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], clean, StringComparison.OrdinalIgnoreCase))
                    return;

            values.Add(clean);
        }

        private static string FormatDisplayValue(PungentDataSheetCell cell, PungentDataSheetColumn column, ExportOptions options)
        {
            if (cell == null)
                return string.Empty;

            string raw = cell.rawValue ?? string.Empty;
            if (column == null)
                return string.IsNullOrEmpty(cell.displayValue) ? raw : cell.displayValue;

            switch (column.dataType)
            {
                case PungentDataSheetDataType.Boolean:
                    return IsTruthy(raw) ? "TRUE" : string.IsNullOrWhiteSpace(raw) ? string.Empty : "FALSE";
                case PungentDataSheetDataType.Date:
                    DateTime date;
                    if (TryParseDate(raw, options != null ? options.dateDisplayFormat : "dd/MM/yyyy", out date))
                    {
                        string displayFormat = options == null || string.IsNullOrWhiteSpace(options.dateDisplayFormat)
                            ? "dd/MM/yyyy"
                            : options.dateDisplayFormat;
                        return date.ToString(displayFormat, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    return raw;
                case PungentDataSheetDataType.AuthoringReference:
                    PungentAuthoringReference reference = cell.linkedAuthoringRef;
                    if ((reference == null || !reference.HasItemId) && PungentDataSheetAuthoringReferenceCodec.TryParseStructured(raw, out PungentAuthoringReference parsed))
                        reference = parsed;
                    if (reference != null && reference.HasItemId)
                        return string.IsNullOrWhiteSpace(reference.label) ? reference.itemId : reference.label;
                    return raw;
                default:
                    return string.IsNullOrEmpty(cell.displayValue) ? raw : cell.displayValue;
            }
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBooleanLiteral(string value)
        {
            string clean = (value ?? string.Empty).Trim();
            return string.Equals(clean, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "false", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "no", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "0", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "TRUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "FALSE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeDate(string value)
        {
            string clean = (value ?? string.Empty).Trim();
            return clean.IndexOf('/') >= 0 ||
                   clean.IndexOf('-') >= 0 ||
                   clean.IndexOf('.') >= 0;
        }

        private static bool TryParseDate(string value, string format, out DateTime date)
        {
            date = DateTime.MinValue;
            string text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string preferred = string.IsNullOrWhiteSpace(format) ? "dd/MM/yyyy" : format.Trim();
            string[] formats =
            {
                preferred,
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "MM/dd/yyyy",
                "M/d/yyyy",
                "o"
            };
            if (DateTime.TryParseExact(text, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date))
                return true;
            if (DateTime.TryParse(text, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date))
                return true;
            return DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date);
        }

        private static void WriteCsvRow(StringBuilder builder, List<string> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');

                builder.Append(Escape(values[i]));
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!quote)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static PungentDataSheetColumn FindColumnByName(PungentDataSheet sheet, string displayName)
        {
            string normalized = NormalizeHeaderName(displayName);
            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
            {
                if (column != null && string.Equals(NormalizeHeaderName(column.displayName), normalized, StringComparison.OrdinalIgnoreCase))
                    return column;
            }

            return null;
        }

        private static string CleanHeader(string header)
        {
            return (header ?? string.Empty).TrimStart('\uFEFF').Trim();
        }

        private static string NormalizeHeaderName(string header)
        {
            return CleanHeader(header).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static List<List<string>> Parse(string text)
        {
            List<List<string>> rows = new List<List<string>>();
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < (text ?? string.Empty).Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        bool escaped = i + 1 < text.Length && text[i + 1] == '"';
                        if (escaped)
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(ch);
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                }
                else if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(field.ToString());
                    field.Length = 0;
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(ch);
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }
    }
#endif
}
