using System;
using System.IO;
using UnityEngine;

namespace PungentFunk.Utilities.RichDocuments
{
    public static class PungentRichDocumentStorage
    {
        public const string RelativeStoragePath = "ProjectSettings/PungentFunkUtilities/RichDocuments.json";

        private static PungentRichDocumentDatabase _database;
        private static string _storagePath = string.Empty;
        private static string _lastError = string.Empty;

        public static PungentRichDocumentDatabase Database
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
                _database.schemaVersion = PungentRichDocumentDatabase.CurrentSchemaVersion;
                _database.lastSavedUtc = DateTime.UtcNow.ToString("o");

                string directory = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(_storagePath, JsonUtility.ToJson(_database, true));
                error = string.Empty;
                _lastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = "Unable to save rich documents: " + ex.Message;
                _lastError = error;
                return false;
            }
        }

        public static bool TryDeleteDocument(string documentId, out string error, bool saveImmediately = false)
        {
            EnsureLoaded();
            bool removed = _database.Delete(documentId);
            if (!removed)
            {
                error = "Rich document could not be found.";
                return false;
            }

            if (saveImmediately)
                return Save(out error);

            error = string.Empty;
            return true;
        }

        public static PungentRichDocument CreateDocument(string title = null, string templateId = null)
        {
            EnsureLoaded();
            return _database.CreateDocument(title, templateId);
        }

        private static PungentRichDocumentDatabase LoadFromPath(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new PungentRichDocumentDatabase();

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return new PungentRichDocumentDatabase();

                PungentRichDocumentDatabase database = JsonUtility.FromJson<PungentRichDocumentDatabase>(json);
                return database ?? new PungentRichDocumentDatabase();
            }
            catch (Exception ex)
            {
                error = "Unable to load rich documents: " + ex.Message;
                return new PungentRichDocumentDatabase();
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
