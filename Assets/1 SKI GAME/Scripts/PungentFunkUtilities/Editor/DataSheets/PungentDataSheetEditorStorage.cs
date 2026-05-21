using System;
using System.IO;
using PungentFunk.Utilities.DataSheets;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public static class PungentDataSheetEditorStorage
    {
        private const string RelativeDirectory = "ProjectSettings/PungentFunkUtilities";
        private const string FileName = "DataSheets.json";
        private const double DebouncedSaveDelaySeconds = 1.25d;

        private static PungentDataSheetDatabase _database;
        private static bool _dirty;
        private static bool _saveQueued;
        private static double _nextSaveTime;
        private static string _lastError = string.Empty;

        public static string StorageLocation => RelativeDirectory + "/" + FileName;
        public static string LastError => _lastError;
        public static bool Dirty => _dirty;

        public static PungentDataSheetDatabase Database
        {
            get
            {
                EnsureLoaded();
                return _database;
            }
        }

        public static void EnsureLoaded()
        {
            if (_database != null)
                return;

            string path = AbsolutePath();
            if (!File.Exists(path))
            {
                _database = new PungentDataSheetDatabase();
                _database.NormalizeInPlace();
                return;
            }

            try
            {
                _database = PungentDataSheetStorage.DatabaseFromJson(File.ReadAllText(path));
                _lastError = string.Empty;
            }
            catch (Exception ex)
            {
                _database = new PungentDataSheetDatabase();
                _lastError = "Could not load Data Sheets storage: " + ex.Message;
                Debug.LogWarning(_lastError);
            }
        }

        public static void Reload()
        {
            _database = null;
            _dirty = false;
            _saveQueued = false;
            EnsureLoaded();
        }

        public static void MarkDirty(bool scheduleSave)
        {
            EnsureLoaded();
            _database.NormalizeInPlace();
            _dirty = true;
            if (scheduleSave)
                QueueDebouncedSave();
        }

        public static void SaveNow()
        {
            EnsureLoaded();
            _database.NormalizeInPlace();
            _database.lastSavedUtc = PungentDataSheetStorage.NormalizeStorageTimestamp();

            string path = AbsolutePath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, PungentDataSheetStorage.ToJson(_database, true));
                _dirty = false;
                _saveQueued = false;
                _lastError = string.Empty;
            }
            catch (Exception ex)
            {
                _lastError = "Could not save Data Sheets storage: " + ex.Message;
                Debug.LogError(_lastError);
            }
        }

        public static void QueueDebouncedSave()
        {
            _nextSaveTime = EditorApplication.timeSinceStartup + DebouncedSaveDelaySeconds;
            if (_saveQueued)
                return;

            _saveQueued = true;
            EditorApplication.update -= FlushDebouncedSave;
            EditorApplication.update += FlushDebouncedSave;
        }

        public static PungentDataSheet CreateSheet(string title)
        {
            PungentDataSheet sheet = Database.CreateSheet(title);
            MarkDirty(true);
            return sheet;
        }

        public static bool DeleteSheet(string sheetId)
        {
            bool deleted = Database.DeleteSheet(sheetId);
            if (deleted)
                MarkDirty(true);
            return deleted;
        }

        public static void UpsertSheet(PungentDataSheet sheet, bool scheduleSave)
        {
            if (sheet == null)
                return;

            Database.UpdateSheet(sheet);
            MarkDirty(scheduleSave);
        }

        private static void FlushDebouncedSave()
        {
            if (!_saveQueued)
            {
                EditorApplication.update -= FlushDebouncedSave;
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextSaveTime)
                return;

            EditorApplication.update -= FlushDebouncedSave;
            if (_dirty)
                SaveNow();
            else
                _saveQueued = false;
        }

        private static string AbsolutePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, RelativeDirectory, FileName);
        }
    }
#endif
}
