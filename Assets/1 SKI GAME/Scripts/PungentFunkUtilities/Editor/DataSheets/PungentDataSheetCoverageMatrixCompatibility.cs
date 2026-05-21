using System;
using System.Collections.Generic;
using PungentFunk.Utilities.DataSheets;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public static class PungentDataSheetCoverageMatrixCompatibility
    {
        public static PungentDataSheet CreateSheetFromMatrixSnapshot(
            string title,
            IEnumerable<string> rowLabels,
            IEnumerable<string> columnLabels,
            Func<string, string, string> resolveCellValue = null)
        {
            PungentDataSheet sheet = new PungentDataSheet
            {
                title = string.IsNullOrWhiteSpace(title) ? "Coverage Matrix Sheet" : title.Trim(),
                summary = "Created from a Coverage Matrix snapshot. The legacy Coverage Matrix editor remains the source tool until migration is explicitly implemented.",
                createdUtc = DateTime.UtcNow.ToString("o"),
                updatedUtc = DateTime.UtcNow.ToString("o")
            };

            foreach (string columnLabel in columnLabels ?? new List<string>())
                sheet.columns.Add(PungentDataSheetColumn.Create(string.IsNullOrWhiteSpace(columnLabel) ? "Column" : columnLabel.Trim(), PungentDataSheetDataType.Text));

            foreach (string rowLabel in rowLabels ?? new List<string>())
            {
                PungentDataSheetRow row = sheet.AddRow(string.IsNullOrWhiteSpace(rowLabel) ? "Row " + (sheet.rows.Count + 1) : rowLabel.Trim());
                for (int c = 0; c < sheet.columns.Count; c++)
                {
                    PungentDataSheetColumn column = sheet.columns[c];
                    string value = resolveCellValue != null ? resolveCellValue(row.displayName, column.displayName) : string.Empty;
                    if (string.IsNullOrEmpty(value))
                        continue;

                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, column.id);
                    cell.rawValue = value;
                    cell.displayValue = value;
                }
            }

            sheet.NormalizeInPlace();
            return sheet;
        }
    }
#endif
}
