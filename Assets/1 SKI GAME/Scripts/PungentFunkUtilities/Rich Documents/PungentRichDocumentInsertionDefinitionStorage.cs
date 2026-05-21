using System;
using System.IO;
using UnityEngine;

namespace PungentFunk.Utilities.RichDocuments
{
    public static class PungentRichDocumentInsertionDefinitionStorage
    {
        public const string RelativeStoragePath = "ProjectSettings/PungentFunkUtilities/RichDocumentInsertionDefinitions.json";

        private static PungentRichDocumentInsertionDefinitionDatabase _database;
        private static string _storagePath = string.Empty;
        private static string _lastError = string.Empty;

        public static PungentRichDocumentInsertionDefinitionDatabase Database
        {
            get
            {
                EnsureLoaded();
                return _database;
            }
        }

        public static string StoragePath
        {
            get
            {
                EnsureLoaded();
                return _storagePath;
            }
        }

        public static string LastError => _lastError;

        public static void EnsureLoaded(string projectRootPath = null)
        {
            if (_database != null)
                return;

            Reload(projectRootPath);
        }

        public static void Reload(string projectRootPath = null)
        {
            _storagePath = ResolveStoragePath(projectRootPath);
            _database = LoadFromPath(_storagePath, out _lastError);
            _database.EnsureDefaults();
        }

        public static bool Save(out string error, string projectRootPath = null)
        {
            EnsureLoaded(projectRootPath);
            if (!string.IsNullOrWhiteSpace(projectRootPath))
                _storagePath = ResolveStoragePath(projectRootPath);

            try
            {
                _database.EnsureDefaults();
                _database.schemaVersion = PungentRichDocumentInsertionDefinitionDatabase.CurrentSchemaVersion;
                _database.lastSavedUtc = DateTime.UtcNow.ToString("o");

                string directory = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_storagePath, JsonUtility.ToJson(_database, true));
                error = string.Empty;
                _lastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Unable to save rich document insertion definitions: " + exception.Message;
                _lastError = error;
                return false;
            }
        }

        private static PungentRichDocumentInsertionDefinitionDatabase LoadFromPath(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new PungentRichDocumentInsertionDefinitionDatabase();

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return new PungentRichDocumentInsertionDefinitionDatabase();

                PungentRichDocumentInsertionDefinitionDatabase database = JsonUtility.FromJson<PungentRichDocumentInsertionDefinitionDatabase>(json);
                return database ?? new PungentRichDocumentInsertionDefinitionDatabase();
            }
            catch (Exception exception)
            {
                error = "Unable to load rich document insertion definitions: " + exception.Message;
                return new PungentRichDocumentInsertionDefinitionDatabase();
            }
        }

        private static string ResolveStoragePath(string projectRootPath)
        {
            string root = projectRootPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                string dataPath = Application.dataPath;
                DirectoryInfo parent = string.IsNullOrWhiteSpace(dataPath) ? null : Directory.GetParent(dataPath);
                root = parent != null ? parent.FullName : Directory.GetCurrentDirectory();
            }

            return Path.Combine(root, RelativeStoragePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
