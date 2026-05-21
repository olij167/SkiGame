using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.BoardGraph;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public enum PungentBoardRefreshMode
    {
        ManualOnly = 0
    }

    public enum PungentBoardPropagationDirection
    {
        OneWayImport = 0,
        TwoWaySelectedFields = 10
    }

    [Flags]
    public enum PungentBoardProjectionSourceScope
    {
        None = 0,
        AuthoringProviders = 1 << 0,
        CurrentSelection = 1 << 1,
        SelectedGameObjectsAndComponents = 1 << 2,
        ExplicitAssetOrFolderRoots = 1 << 3
    }

    public enum PungentBoardFieldMappingTarget
    {
        Title = 0,
        Summary = 10,
        Body = 20,
        StatusProperty = 30,
        Tag = 40,
        NodeProperty = 50,
        NodeTypeKey = 60,
        StyleKey = 70
    }

    public enum PungentBoardGroupBy
    {
        None = 0,
        Provider = 10,
        SourceLabel = 20,
        GameObject = 30,
        ComponentType = 40,
        Folder = 50,
        Tag = 60
    }

    [Serializable]
    public sealed class PungentBoardFieldMappingRule
    {
        public bool enabled = true;
        public string sourcePath = string.Empty;
        public PungentBoardFieldMappingTarget target = PungentBoardFieldMappingTarget.NodeProperty;
        public string targetKey = string.Empty;
        public string fallbackValue = string.Empty;

        public void NormalizeInPlace()
        {
            sourcePath = sourcePath == null ? string.Empty : sourcePath.Trim();
            targetKey = targetKey == null ? string.Empty : targetKey.Trim();
            fallbackValue = fallbackValue == null ? string.Empty : fallbackValue.Trim();
        }
    }

    [Serializable]
    public sealed class PungentBoardRelationMappingRule
    {
        public bool enabled = true;
        public bool includeObjectReferenceFields = true;
        public string sourcePropertyPathContains = string.Empty;
        public string edgeTypeKey = string.Empty;
        public string labelPrefix = string.Empty;
        public bool onlyWhenTargetIncluded = true;

        public void NormalizeInPlace()
        {
            sourcePropertyPathContains = sourcePropertyPathContains == null ? string.Empty : sourcePropertyPathContains.Trim();
            edgeTypeKey = edgeTypeKey == null ? string.Empty : edgeTypeKey.Trim();
            labelPrefix = labelPrefix == null ? string.Empty : labelPrefix.Trim();
        }
    }

    [Serializable]
    public sealed class PungentBoardGroupMappingRule
    {
        public bool enabled;
        public PungentBoardGroupBy groupBy = PungentBoardGroupBy.None;
        public string titlePrefix = string.Empty;
        public string styleKey = "group";

        public void NormalizeInPlace()
        {
            titlePrefix = titlePrefix == null ? string.Empty : titlePrefix.Trim();
            styleKey = string.IsNullOrWhiteSpace(styleKey) ? "group" : styleKey.Trim();
        }
    }

    [Serializable]
    public sealed class PungentBoardIntegrationProfile
    {
        public string id = string.Empty;
        public string displayName = "Default Authoring Import";
        public string description = "Import registered authoring provider items as linked board nodes.";
        public string sourceLabel = "Authoring";
        public PungentBoardProjectionSourceScope sourceScopes = PungentBoardProjectionSourceScope.AuthoringProviders;
        public PungentBoardRefreshMode refreshMode = PungentBoardRefreshMode.ManualOnly;
        public PungentBoardPropagationDirection propagationDirection = PungentBoardPropagationDirection.OneWayImport;
        public List<PungentAuthoringItemKind> includedKinds = new List<PungentAuthoringItemKind>();
        public List<string> includedProviderIds = new List<string>();
        public List<string> includedSourceTypeNames = new List<string>();
        public List<string> excludedSourceTypeNames = new List<string>();
        public List<string> explicitRootPaths = new List<string>();
        public List<PungentBoardFieldMappingRule> fieldMappings = new List<PungentBoardFieldMappingRule>();
        public List<PungentBoardRelationMappingRule> relationMappings = new List<PungentBoardRelationMappingRule>();
        public List<PungentBoardGroupMappingRule> groupMappings = new List<PungentBoardGroupMappingRule>();
        public bool updateExistingNodes = true;
        public bool createMissingNodes = true;
        public bool createProviderGroups = true;
        public bool updateExistingEdges = true;
        public bool createMissingEdges = true;
        public bool updateExistingGroups = true;
        public bool createMissingGroups = true;
        public bool markStaleMissingSources;
        public int maxItemsPerRefresh = 50;
        public float nodeSpacingX = 280f;
        public float nodeSpacingY = 150f;

        public void NormalizeInPlace()
        {
            id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Default Authoring Import" : displayName.Trim();
            description = description == null ? string.Empty : description.Trim();
            sourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "Authoring" : sourceLabel.Trim();
            includedKinds = includedKinds ?? new List<PungentAuthoringItemKind>();
            includedProviderIds = (includedProviderIds ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            includedSourceTypeNames = NormalizeStrings(includedSourceTypeNames);
            excludedSourceTypeNames = NormalizeStrings(excludedSourceTypeNames);
            explicitRootPaths = NormalizeStrings(explicitRootPaths);
            fieldMappings = fieldMappings ?? new List<PungentBoardFieldMappingRule>();
            relationMappings = relationMappings ?? new List<PungentBoardRelationMappingRule>();
            groupMappings = groupMappings ?? new List<PungentBoardGroupMappingRule>();
            for (int i = fieldMappings.Count - 1; i >= 0; i--)
            {
                if (fieldMappings[i] == null)
                {
                    fieldMappings.RemoveAt(i);
                    continue;
                }

                fieldMappings[i].NormalizeInPlace();
            }

            for (int i = relationMappings.Count - 1; i >= 0; i--)
            {
                if (relationMappings[i] == null)
                {
                    relationMappings.RemoveAt(i);
                    continue;
                }

                relationMappings[i].NormalizeInPlace();
            }

            for (int i = groupMappings.Count - 1; i >= 0; i--)
            {
                if (groupMappings[i] == null)
                {
                    groupMappings.RemoveAt(i);
                    continue;
                }

                groupMappings[i].NormalizeInPlace();
            }

            if (sourceScopes == PungentBoardProjectionSourceScope.None)
                sourceScopes = PungentBoardProjectionSourceScope.AuthoringProviders;
            maxItemsPerRefresh = Mathf.Clamp(maxItemsPerRefresh, 1, 600);
            nodeSpacingX = Mathf.Clamp(float.IsNaN(nodeSpacingX) || float.IsInfinity(nodeSpacingX) ? 280f : nodeSpacingX, 160f, 720f);
            nodeSpacingY = Mathf.Clamp(float.IsNaN(nodeSpacingY) || float.IsInfinity(nodeSpacingY) ? 150f : nodeSpacingY, 90f, 520f);
        }

        public bool Includes(IPungentAuthoringProvider provider, PungentAuthoringMetadata metadata)
        {
            if (provider == null || metadata == null)
                return false;

            if (includedProviderIds.Count > 0 && !includedProviderIds.Any(id => string.Equals(id, provider.ProviderId, StringComparison.OrdinalIgnoreCase)))
                return false;

            return includedKinds.Count == 0 || includedKinds.Contains(metadata.kind);
        }

        public bool HasScope(PungentBoardProjectionSourceScope scope)
        {
            return (sourceScopes & scope) != 0;
        }

        public bool IncludesSourceType(Type type)
        {
            if (type == null)
                return false;

            string typeName = type.Name;
            string fullName = type.FullName ?? type.Name;
            if (excludedSourceTypeNames.Any(value => MatchesTypeToken(value, typeName, fullName)))
                return false;

            return includedSourceTypeNames.Count == 0 || includedSourceTypeNames.Any(value => MatchesTypeToken(value, typeName, fullName));
        }

        private static bool MatchesTypeToken(string token, string typeName, string fullName)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            return string.Equals(token, typeName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, fullName, StringComparison.OrdinalIgnoreCase) ||
                   fullName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<string> NormalizeStrings(IEnumerable<string> source)
        {
            return (source ?? new string[0])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static PungentBoardIntegrationProfile CreateDefault()
        {
            PungentBoardIntegrationProfile profile = new PungentBoardIntegrationProfile
            {
                id = "default-authoring-import",
                displayName = "Default Authoring Import",
                description = "Explicitly import notes, utilities, tokens, documentation links, audit issues, help topics, and rich documents when their providers are installed.",
                sourceLabel = "Authoring Providers",
                sourceScopes = PungentBoardProjectionSourceScope.AuthoringProviders,
                maxItemsPerRefresh = 50,
                includedKinds = new List<PungentAuthoringItemKind>
                {
                    PungentAuthoringItemKind.LegacyNote,
                    PungentAuthoringItemKind.Task,
                    PungentAuthoringItemKind.FutureUtility,
                    PungentAuthoringItemKind.Utility,
                    PungentAuthoringItemKind.TokenDefinition,
                    PungentAuthoringItemKind.DocumentationLink,
                    PungentAuthoringItemKind.HelpTopic,
                    PungentAuthoringItemKind.AuditIssue,
                    PungentAuthoringItemKind.RichDocument,
                    PungentAuthoringItemKind.DataSheet,
                    PungentAuthoringItemKind.ExternalReference
                }
            };
            profile.NormalizeInPlace();
            return profile;
        }

        public static PungentBoardIntegrationProfile CreateComponentSelectionDefault()
        {
            PungentBoardIntegrationProfile profile = new PungentBoardIntegrationProfile
            {
                id = "component-selection-import",
                displayName = "Component Selection Import",
                description = "Preview selected GameObjects, Components, and explicit asset roots as board nodes, with SerializedObject reference fields as edges.",
                sourceLabel = "Unity Selection",
                sourceScopes = PungentBoardProjectionSourceScope.CurrentSelection | PungentBoardProjectionSourceScope.SelectedGameObjectsAndComponents,
                maxItemsPerRefresh = 40,
                includedKinds = new List<PungentAuthoringItemKind>(),
                createProviderGroups = false,
                createMissingGroups = true,
                updateExistingGroups = true,
                createMissingEdges = true,
                updateExistingEdges = true,
                groupMappings = new List<PungentBoardGroupMappingRule>
                {
                    new PungentBoardGroupMappingRule
                    {
                        enabled = true,
                        groupBy = PungentBoardGroupBy.GameObject,
                        titlePrefix = "GameObject",
                        styleKey = "group"
                    }
                },
                relationMappings = new List<PungentBoardRelationMappingRule>
                {
                    new PungentBoardRelationMappingRule
                    {
                        enabled = true,
                        includeObjectReferenceFields = true,
                        labelPrefix = "ref",
                        onlyWhenTargetIncluded = true
                    }
                },
                fieldMappings = new List<PungentBoardFieldMappingRule>
                {
                    new PungentBoardFieldMappingRule
                    {
                        enabled = true,
                        sourcePath = "component.type",
                        target = PungentBoardFieldMappingTarget.Tag,
                        targetKey = "type"
                    }
                }
            };
            profile.NormalizeInPlace();
            return profile;
        }
    }

    [Serializable]
    public sealed class PungentBoardIntegrationProfileDatabase
    {
        public int schemaVersion = 1;
        public string lastSavedUtc = string.Empty;
        public List<PungentBoardIntegrationProfile> profiles = new List<PungentBoardIntegrationProfile>();

        public void NormalizeInPlace()
        {
            profiles = profiles ?? new List<PungentBoardIntegrationProfile>();
            for (int i = profiles.Count - 1; i >= 0; i--)
            {
                if (profiles[i] == null)
                {
                    profiles.RemoveAt(i);
                    continue;
                }

                profiles[i].NormalizeInPlace();
            }

            if (profiles.Count == 0)
                profiles.Add(PungentBoardIntegrationProfile.CreateDefault());
        }

        public PungentBoardIntegrationProfile Find(string id)
        {
            NormalizeInPlace();
            return profiles.FirstOrDefault(profile => profile != null && string.Equals(profile.id, id, StringComparison.OrdinalIgnoreCase)) ?? profiles.FirstOrDefault();
        }
    }

    public static class PungentBoardIntegrationProfileStorage
    {
        private const string StorageFileName = "BoardIntegrationProfiles.json";

        private static PungentBoardIntegrationProfileDatabase _database;
        private static string _loadError = string.Empty;

        public static string StoragePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "PungentFunkUtilities", StorageFileName);
        public static string LoadError => _loadError;

        public static PungentBoardIntegrationProfileDatabase Database
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

            Reload();
        }

        public static void Reload()
        {
            _loadError = string.Empty;
            if (!File.Exists(StoragePath))
            {
                _database = new PungentBoardIntegrationProfileDatabase();
                _database.NormalizeInPlace();
                return;
            }

            try
            {
                string json = File.ReadAllText(StoragePath);
                _database = string.IsNullOrWhiteSpace(json)
                    ? new PungentBoardIntegrationProfileDatabase()
                    : JsonUtility.FromJson<PungentBoardIntegrationProfileDatabase>(json);
                if (_database == null)
                    _database = new PungentBoardIntegrationProfileDatabase();
                _database.NormalizeInPlace();
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
                _database = new PungentBoardIntegrationProfileDatabase();
                _database.NormalizeInPlace();
                Debug.LogWarning("PungentFunk Board integration profiles could not be loaded. A safe default profile is being used. " + _loadError);
            }
        }

        public static PungentBoardIntegrationProfile Find(string id)
        {
            return Database.Find(id);
        }

        public static bool Save(out string error)
        {
            error = string.Empty;
            try
            {
                PungentBoardIntegrationProfileDatabase database = Database;
                database.lastSavedUtc = DateTime.UtcNow.ToString("o");
                database.NormalizeInPlace();

                string directory = Path.GetDirectoryName(StoragePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(database, true);
                string tempPath = StoragePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(StoragePath))
                    File.Copy(StoragePath, StoragePath + ".bak", true);
                File.Copy(tempPath, StoragePath, true);
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
#endif
}
