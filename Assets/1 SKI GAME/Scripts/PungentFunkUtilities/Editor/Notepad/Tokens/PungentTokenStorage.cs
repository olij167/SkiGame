using System.Collections.Generic;
using System.IO;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using UnityEngine;

    public static class PungentTokenStorage
    {
        public static PungentTokenDatabase Database
        {
            get
            {
                EnsureLoaded();
                return PungentTokenDatabase.instance;
            }
        }

        public static void EnsureLoaded()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "PungentFunkUtilities"));
            PungentTokenDatabase db = PungentTokenDatabase.instance;
            if (db.tokens == null)
                db.tokens = new List<PungentTokenDefinition>();
            if (db.bindings == null)
                db.bindings = new List<PungentTokenBinding>();
            if (db.categories == null)
                db.categories = new List<string>();
            db.EnsureScanSettings();
            db.CreateDefaultTokensIfEmpty();
        }

        public static void Save()
        {
            PungentTokenDatabase.instance.SaveDatabase();
        }
    }
#endif
}
