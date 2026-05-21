using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.DataSheets
{
    [Serializable]
    public sealed class PungentDataSheetDatabase
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public List<PungentDataSheet> sheets = new List<PungentDataSheet>();
        public string lastSavedUtc = string.Empty;

        public void NormalizeInPlace()
        {
            schemaVersion = Math.Max(CurrentSchemaVersion, schemaVersion);
            if (sheets == null)
                sheets = new List<PungentDataSheet>();

            for (int i = 0; i < sheets.Count; i++)
                sheets[i]?.NormalizeInPlace();

            sheets.RemoveAll(sheet => sheet == null || string.IsNullOrWhiteSpace(sheet.id));
            EnsureUniqueSheetIds();
        }

        public PungentDataSheet CreateSheet(string title = "Untitled Sheet")
        {
            PungentDataSheet sheet = new PungentDataSheet
            {
                id = PungentAuthoringId.NewValue(),
                title = string.IsNullOrWhiteSpace(title) ? "Untitled Sheet" : title.Trim(),
                createdUtc = DateTime.UtcNow.ToString("o"),
                updatedUtc = DateTime.UtcNow.ToString("o"),
                migrationVersion = PungentDataSheet.CurrentMigrationVersion
            };
            sheet.NormalizeInPlace();
            sheets.Add(sheet);
            return sheet;
        }

        public bool UpdateSheet(PungentDataSheet sheet)
        {
            if (sheet == null)
                return false;

            sheet.NormalizeInPlace();
            for (int i = 0; i < sheets.Count; i++)
            {
                if (sheets[i] != null && PungentAuthoringId.EqualsId(sheets[i].id, sheet.id))
                {
                    sheets[i] = sheet;
                    return true;
                }
            }

            sheets.Add(sheet);
            return true;
        }

        public bool DeleteSheet(string sheetId)
        {
            if (string.IsNullOrWhiteSpace(sheetId) || sheets == null)
                return false;

            return sheets.RemoveAll(sheet => sheet != null && PungentAuthoringId.EqualsId(sheet.id, sheetId)) > 0;
        }

        public PungentDataSheet FindSheet(string sheetId)
        {
            if (string.IsNullOrWhiteSpace(sheetId) || sheets == null)
                return null;

            return sheets.FirstOrDefault(sheet => sheet != null && PungentAuthoringId.EqualsId(sheet.id, sheetId));
        }

        public PungentDataSheet DuplicateSheet(PungentDataSheet source)
        {
            if (source == null)
                return null;

            PungentDataSheet copy = PungentDataSheetStorage.FromJson(PungentDataSheetStorage.ToJson(source));
            if (copy == null)
                return null;

            copy.id = PungentAuthoringId.NewValue();
            copy.title = string.IsNullOrWhiteSpace(source.title) ? "Sheet Copy" : source.title + " Copy";
            copy.createdUtc = DateTime.UtcNow.ToString("o");
            copy.updatedUtc = copy.createdUtc;
            copy.NormalizeInPlace();
            sheets.Add(copy);
            return copy;
        }

        private void EnsureUniqueSheetIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sheets.Count; i++)
            {
                PungentDataSheet sheet = sheets[i];
                if (sheet == null)
                    continue;

                if (ids.Add(sheet.id))
                    continue;

                sheet.id = PungentAuthoringId.NewValue();
                sheet.Touch();
                ids.Add(sheet.id);
            }
        }
    }
}
