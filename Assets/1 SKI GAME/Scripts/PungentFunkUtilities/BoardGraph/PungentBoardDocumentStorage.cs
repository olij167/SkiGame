using System;
using System.IO;
using UnityEngine;

namespace PungentFunk.Utilities.BoardGraph
{
    public static class PungentBoardDocumentStorage
    {
        public const int CurrentSchemaVersion = PungentBoardDocument.CurrentMigrationVersion;
        public const string StorageFileName = "BoardDocuments.json";

        public static string GetDefaultProjectSettingsPath(string projectRoot)
        {
            string root = string.IsNullOrWhiteSpace(projectRoot)
                ? Directory.GetParent(Application.dataPath).FullName
                : projectRoot;
            return Path.Combine(root, "ProjectSettings", "PungentFunkUtilities", StorageFileName);
        }

        public static PungentBoardDocumentDatabase LoadOrCreate(string absolutePath)
        {
            string error;
            PungentBoardDocumentDatabase database;
            if (TryLoad(absolutePath, out database, out error))
                return database;

            database = new PungentBoardDocumentDatabase();
            database.NormalizeInPlace();
            return database;
        }

        public static bool TryLoad(string absolutePath, out PungentBoardDocumentDatabase database, out string error)
        {
            database = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                database = new PungentBoardDocumentDatabase();
                database.NormalizeInPlace();
                return true;
            }

            try
            {
                string json = File.ReadAllText(absolutePath);
                database = string.IsNullOrWhiteSpace(json)
                    ? new PungentBoardDocumentDatabase()
                    : JsonUtility.FromJson<PungentBoardDocumentDatabase>(json);
                if (database == null)
                    database = new PungentBoardDocumentDatabase();
                database.NormalizeInPlace();
                return true;
            }
            catch (Exception ex)
            {
                database = new PungentBoardDocumentDatabase();
                database.NormalizeInPlace();
                error = ex.Message;
                return false;
            }
        }

        public static bool Save(string absolutePath, PungentBoardDocumentDatabase database, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                error = "Board storage path is empty.";
                return false;
            }

            try
            {
                if (database == null)
                    database = new PungentBoardDocumentDatabase();

                database.schemaVersion = CurrentSchemaVersion;
                database.migrationVersion = Math.Max(database.migrationVersion, CurrentSchemaVersion);
                database.lastSavedUtc = DateTime.UtcNow.ToString("o");
                database.NormalizeInPlace();

                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(database, true);
                string tempPath = absolutePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(absolutePath))
                    File.Copy(absolutePath, absolutePath + ".bak", true);
                File.Copy(tempPath, absolutePath, true);
                File.Delete(tempPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
