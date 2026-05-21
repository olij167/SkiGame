namespace PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring
{
#if UNITY_EDITOR && PUNGENTFUNK_INTERNAL_DEVTOOLS
    using System.IO;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEngine;

    public static class UtilityThemePresetDraftStore
    {
        public const string DraftPath = "ProjectSettings/PungentFunkUtilities/ThemePresetDrafts.json";

        public static void SaveLastDraft(UtilityThemePresetSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            string absolutePath = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, DraftPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(new DraftHeader(snapshot), true));
        }

        private sealed class DraftHeader
        {
            public string preset;
            public string displayName;
            public string savedUtc;
            public bool hiddenFromGallery;

            public DraftHeader(UtilityThemePresetSnapshot snapshot)
            {
                preset = snapshot.preset.ToString();
                displayName = snapshot.displayName;
                savedUtc = System.DateTime.UtcNow.ToString("u");
                hiddenFromGallery = snapshot.hiddenFromGallery;
            }
        }
    }
#endif
}
