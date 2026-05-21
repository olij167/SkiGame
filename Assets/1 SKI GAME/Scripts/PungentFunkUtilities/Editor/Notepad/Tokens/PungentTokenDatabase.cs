using System;
using System.Collections.Generic;
using System.Linq;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using UnityEditor;

    [FilePath("ProjectSettings/PungentFunkUtilities/Tokens.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentTokenDatabase : ScriptableSingleton<PungentTokenDatabase>
    {
        public List<PungentTokenDefinition> tokens = new List<PungentTokenDefinition>();
        public List<PungentTokenBinding> bindings = new List<PungentTokenBinding>();
        public List<string> categories = new List<string>();
        public PungentTokenScanSettings scanSettings = new PungentTokenScanSettings();
        public string lastSavedUtc = string.Empty;

        public static string StorageLocation => "ProjectSettings/PungentFunkUtilities/Tokens.asset";

        public IReadOnlyList<PungentTokenDefinition> GetTokens()
        {
            PungentTokenStorage.EnsureLoaded();
            return tokens;
        }

        public PungentTokenDefinition FindToken(string key)
        {
            string clean = NormalizeKey(key);
            return tokens.FirstOrDefault(t => t != null && string.Equals(NormalizeKey(t.key), clean, StringComparison.OrdinalIgnoreCase));
        }

        public bool ContainsToken(string key) => FindToken(key) != null;

        public PungentTokenDefinition AddToken(string key = "newToken")
        {
            string clean = UniqueKey(NormalizeKey(key));
            PungentTokenDefinition token = new PungentTokenDefinition
            {
                id = Guid.NewGuid().ToString("N"),
                key = clean,
                displayName = ObjectNames.NicifyVariableName(clean),
                previewValue = clean,
                category = "General",
                createdUtc = DateTime.UtcNow.ToString("o"),
                updatedUtc = DateTime.UtcNow.ToString("o")
            };
            tokens.Add(token);
            SaveDatabase();
            return token;
        }

        public PungentTokenDefinition DuplicateToken(PungentTokenDefinition source)
        {
            if (source == null)
                return null;

            string now = DateTime.UtcNow.ToString("o");
            PungentTokenDefinition token = new PungentTokenDefinition
            {
                id = Guid.NewGuid().ToString("N"),
                key = UniqueKey(source.key + "Copy"),
                displayName = source.displayName + " Copy",
                description = source.description,
                previewValue = source.previewValue,
                category = source.category,
                tags = new List<string>(source.tags ?? new List<string>()),
                examples = new List<string>(source.examples ?? new List<string>()),
                required = source.required,
                deprecated = source.deprecated,
                replacementKey = source.replacementKey,
                archived = source.archived,
                developerOnly = source.developerOnly,
                createdUtc = now,
                updatedUtc = now,
                allowedContext = source.allowedContext,
                documentationNoteId = source.documentationNoteId,
                source = source.source
            };
            tokens.Add(token);
            SaveDatabase();
            return token;
        }

        public void ArchiveToken(PungentTokenDefinition token, bool archived)
        {
            if (token == null)
                return;
            token.archived = archived;
            token.updatedUtc = DateTime.UtcNow.ToString("o");
            SaveDatabase();
        }

        public List<PungentTokenBinding> GetBindingsForToken(string key)
        {
            string clean = NormalizeKey(key);
            return bindings.Where(b => b != null && !b.archived && string.Equals(NormalizeKey(b.tokenKey), clean, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public PungentTokenBinding AddBinding(PungentTokenBinding binding)
        {
            if (binding == null)
                return null;
            binding.tokenKey = NormalizeKey(binding.tokenKey);
            if (bindings.Any(b => b != null && !b.archived && BindingEquals(b, binding)))
                return null;
            bindings.Add(binding);
            SaveDatabase();
            return binding;
        }

        public void RemoveBinding(PungentTokenBinding binding)
        {
            if (binding == null)
                return;
            bindings.Remove(binding);
            SaveDatabase();
        }

        public void CreateDefaultTokensIfEmpty()
        {
            if (tokens.Count > 0)
                return;

            tokens.Add(Default("playerName", "Example user/player name", "Alex"));
            tokens.Add(Default("objectName", "Selected object or target name", "Crate"));
            tokens.Add(Default("locationName", "Location / anchor label", "Workshop"));
            SaveDatabase();
        }

        public void SaveDatabase()
        {
            EnsureScanSettings();
            lastSavedUtc = DateTime.UtcNow.ToString("o");
            Save(true);
        }

        public PungentTokenScanSettings EnsureScanSettings()
        {
            if (scanSettings == null)
                scanSettings = new PungentTokenScanSettings();
            scanSettings.EnsureDefaults();
            return scanSettings;
        }

        public static string NormalizeKey(string key)
        {
            return (key ?? string.Empty).Trim().Trim('{', '}').Trim();
        }

        private string UniqueKey(string key)
        {
            string clean = string.IsNullOrWhiteSpace(key) ? "token" : NormalizeKey(key);
            if (FindToken(clean) == null)
                return clean;
            int i = 2;
            while (FindToken(clean + i) != null)
                i++;
            return clean + i;
        }

        private static PungentTokenDefinition Default(string key, string description, string preview)
        {
            string now = DateTime.UtcNow.ToString("o");
            return new PungentTokenDefinition
            {
                id = Guid.NewGuid().ToString("N"),
                key = key,
                displayName = ObjectNames.NicifyVariableName(key),
                description = description,
                previewValue = preview,
                category = "Default",
                examples = new List<string> { "{" + key + "}" },
                createdUtc = now,
                updatedUtc = now,
                source = "default"
            };
        }

        private static bool BindingEquals(PungentTokenBinding a, PungentTokenBinding b)
        {
            return string.Equals(NormalizeKey(a.tokenKey), NormalizeKey(b.tokenKey), StringComparison.OrdinalIgnoreCase) &&
                   a.targetType == b.targetType &&
                   string.Equals(a.assetGuid, b.assetGuid, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.sceneObjectGlobalId, b.sceneObjectGlobalId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.componentType, b.componentType, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.componentInstanceId, b.componentInstanceId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.propertyPath, b.propertyPath, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.scriptPath, b.scriptPath, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.noteId, b.noteId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.auditIssueCode, b.auditIssueCode, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.utilityId, b.utilityId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.futureUtilityId, b.futureUtilityId, StringComparison.OrdinalIgnoreCase);
        }
    }
#endif
}
