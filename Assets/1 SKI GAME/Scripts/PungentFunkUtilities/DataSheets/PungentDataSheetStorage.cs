using System;
using UnityEngine;

namespace PungentFunk.Utilities.DataSheets
{
    public static class PungentDataSheetStorage
    {
        public static string ToJson(PungentDataSheet sheet, bool prettyPrint = false)
        {
            if (sheet == null)
                return string.Empty;

            sheet.NormalizeInPlace();
            return JsonUtility.ToJson(sheet, prettyPrint);
        }

        public static string ToJson(PungentDataSheetDatabase database, bool prettyPrint = false)
        {
            if (database == null)
                database = new PungentDataSheetDatabase();

            database.NormalizeInPlace();
            return JsonUtility.ToJson(database, prettyPrint);
        }

        public static PungentDataSheet FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                PungentDataSheet sheet = JsonUtility.FromJson<PungentDataSheet>(json);
                sheet?.NormalizeInPlace();
                return sheet;
            }
            catch
            {
                return null;
            }
        }

        public static PungentDataSheetDatabase DatabaseFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new PungentDataSheetDatabase();

            try
            {
                PungentDataSheetDatabase database = JsonUtility.FromJson<PungentDataSheetDatabase>(json) ?? new PungentDataSheetDatabase();
                database.NormalizeInPlace();
                return database;
            }
            catch
            {
                return new PungentDataSheetDatabase();
            }
        }

        public static PungentDataSheetDatabase Clone(PungentDataSheetDatabase source)
        {
            return DatabaseFromJson(ToJson(source));
        }

        public static PungentDataSheet Clone(PungentDataSheet source)
        {
            return FromJson(ToJson(source));
        }

        public static string NormalizeStorageTimestamp()
        {
            return DateTime.UtcNow.ToString("o");
        }
    }
}
