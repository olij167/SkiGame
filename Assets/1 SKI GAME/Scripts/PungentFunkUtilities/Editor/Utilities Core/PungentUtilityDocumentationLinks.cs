namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    [FilePath("ProjectSettings/PungentFunkUtilities/DocumentationLinks.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityDocumentationLinks : ScriptableSingleton<PungentUtilityDocumentationLinks>
    {
        [Serializable]
        public sealed class DocumentationVersion
        {
            public string versionLabel;
            public string assetGuid;
            public string externalPath;
            public string notes;
            public long archivedUtcTicks;
        }

        [Serializable]
        public sealed class DocumentationLink
        {
            public string id;
            public string displayName;
            public string description;
            public string category;
            public string versionLabel;
            public string assetGuid;
            public string externalPath;
            public string[] utilityIds;
            public long updatedUtcTicks;
            public List<DocumentationVersion> backlog;
            public int order;
        }

        public enum DocumentationTargetKind
        {
            Empty,
            UnityAsset,
            LocalFile,
            WebUrl,
            Missing,
            UnsupportedUrlScheme,
            InvalidTarget
        }

        public sealed class DocumentationTargetStatus
        {
            public DocumentationTargetKind kind;
            public string kindLabel = "Empty Target";
            public string message = string.Empty;
            public string targetValue = string.Empty;
            public string copyValue = string.Empty;
            public string assetPath = string.Empty;
            public string scheme = string.Empty;
            public bool normalizedFromInput;
            public bool hasTarget;
            public bool canOpen;
            public bool canCopy;
            public bool canPingAsset;
        }

        private static readonly HashSet<string> FileNameExtensionsThatShouldNotBecomeUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".asset",
            ".cs",
            ".json",
            ".md",
            ".pdf",
            ".png",
            ".scene",
            ".txt",
            ".unity"
        };

        [SerializeField] private List<DocumentationLink> _links = new List<DocumentationLink>();

        public IReadOnlyList<DocumentationLink> Links
        {
            get
            {
                MigrateInMemory(true);
                return _links;
            }
        }

        public IEnumerable<DocumentationLink> GetAll()
        {
            MigrateInMemory(true);
            return _links;
        }

        public IEnumerable<DocumentationLink> GetGlobalLinks()
        {
            return GetAll().Where(link => link.utilityIds == null || link.utilityIds.Length == 0);
        }

        public IEnumerable<DocumentationLink> GetLinksForUtility(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return Enumerable.Empty<DocumentationLink>();

            return GetAll().Where(link => link.utilityIds != null && link.utilityIds.Any(id => string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase)));
        }

        public bool HasLinksForUtility(string utilityId)
        {
            return GetLinksForUtility(utilityId).Any(HasCurrentTarget);
        }

        public DocumentationLink GetFirstCurrentLinkForUtility(string utilityId)
        {
            return GetLinksForUtility(utilityId).FirstOrDefault(HasCurrentTarget);
        }

        public void Save()
        {
            MigrateInMemory(true);
            for (int i = 0; i < _links.Count; i++)
                NormalizeTargets(_links[i]);
            Reindex();
            _links.Sort((a, b) => a.order.CompareTo(b.order));
            Save(true);
        }

        public void AddAssetLink(UnityEngine.Object asset, string displayName)
        {
            if (asset == null)
                return;

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(path))
                return;

            AddLink(new DocumentationLink
            {
                displayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(path) : displayName.Trim(),
                assetGuid = AssetDatabase.AssetPathToGUID(path),
                versionLabel = "Current",
                order = _links.Count
            });
        }

        public void AddExternalLink(string path, string displayName)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalizedPath = NormalizeExternalPathOrUrl(path);
            AddLink(new DocumentationLink
            {
                displayName = string.IsNullOrWhiteSpace(displayName) ? BuildDisplayNameFromExternalPath(normalizedPath) : displayName.Trim(),
                externalPath = normalizedPath,
                versionLabel = "Current",
                order = _links.Count
            });
        }

        public void AddLink(DocumentationLink link)
        {
            if (link == null)
                return;

            PrepareLink(link, true);
            NormalizeTargets(link);
            link.order = _links.Count;
            _links.Add(link);
            Save();
        }

        public void UpdateLink(DocumentationLink link)
        {
            if (link == null)
                return;

            PrepareLink(link, true);
            NormalizeTargets(link);
            int index = _links.FindIndex(item => string.Equals(item.id, link.id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                _links[index] = link;
            else
                _links.Add(link);
            Save();
        }

        public void RemoveLink(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            _links.RemoveAll(link => string.Equals(link.id, id, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        public void AssignLinkToUtility(string linkId, string utilityId)
        {
            DocumentationLink link = FindById(linkId);
            if (link == null || string.IsNullOrWhiteSpace(utilityId))
                return;

            List<string> ids = (link.utilityIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (!ids.Any(id => string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase)))
                ids.Add(utilityId);
            link.utilityIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Save();
        }

        public void UnassignLinkFromUtility(string linkId, string utilityId)
        {
            DocumentationLink link = FindById(linkId);
            if (link == null || string.IsNullOrWhiteSpace(utilityId))
                return;

            link.utilityIds = (link.utilityIds ?? Array.Empty<string>())
                .Where(id => !string.Equals(id, utilityId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Save();
        }

        public void UpdateCurrentVersion(string linkId, string newAssetGuid, string newExternalPath, string newVersionLabel, string notes)
        {
            DocumentationLink link = FindById(linkId);
            if (link == null || (string.IsNullOrWhiteSpace(newAssetGuid) && string.IsNullOrWhiteSpace(newExternalPath)))
                return;

            PrepareLink(link, true);
            AddCurrentToBacklogIfNeeded(link, notes);
            link.assetGuid = newAssetGuid ?? string.Empty;
            link.externalPath = NormalizeExternalPathOrUrl(newExternalPath);
            link.versionLabel = string.IsNullOrWhiteSpace(newVersionLabel) ? "Current" : newVersionLabel.Trim();
            link.updatedUtcTicks = DateTime.UtcNow.Ticks;
            Save();
        }

        public void ReplaceCurrentTarget(string linkId, string newAssetGuid, string newExternalPath, string newVersionLabel)
        {
            DocumentationLink link = FindById(linkId);
            if (link == null || (string.IsNullOrWhiteSpace(newAssetGuid) && string.IsNullOrWhiteSpace(newExternalPath)))
                return;

            PrepareLink(link, true);
            link.assetGuid = newAssetGuid ?? string.Empty;
            link.externalPath = NormalizeExternalPathOrUrl(newExternalPath);
            link.versionLabel = string.IsNullOrWhiteSpace(newVersionLabel) ? "Current" : newVersionLabel.Trim();
            link.updatedUtcTicks = DateTime.UtcNow.Ticks;
            Save();
        }

        public void RestoreBacklogVersionAsCurrent(string linkId, int backlogIndex)
        {
            DocumentationLink link = FindById(linkId);
            if (link == null || link.backlog == null || backlogIndex < 0 || backlogIndex >= link.backlog.Count)
                return;

            DocumentationVersion version = link.backlog[backlogIndex];
            link.backlog.RemoveAt(backlogIndex);
            AddCurrentToBacklogIfNeeded(link, "Restored from backlog.");
            link.assetGuid = version.assetGuid ?? string.Empty;
            link.externalPath = NormalizeExternalPathOrUrl(version.externalPath);
            link.versionLabel = string.IsNullOrWhiteSpace(version.versionLabel) ? "Current" : version.versionLabel.Trim();
            link.updatedUtcTicks = DateTime.UtcNow.Ticks;
            Save();
        }

        public void RemoveBacklogVersion(string linkId, int backlogIndex)
        {
            DocumentationLink link = FindById(linkId);
            if (link == null || link.backlog == null || backlogIndex < 0 || backlogIndex >= link.backlog.Count)
                return;

            link.backlog.RemoveAt(backlogIndex);
            Save();
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _links.Count)
                return;

            _links.RemoveAt(index);
            Save();
        }

        public void Move(int index, int delta)
        {
            int target = Mathf.Clamp(index + delta, 0, _links.Count - 1);
            if (index == target || index < 0 || index >= _links.Count)
                return;

            DocumentationLink item = _links[index];
            _links.RemoveAt(index);
            _links.Insert(target, item);
            Save();
        }

        public bool OpenLink(DocumentationLink link)
        {
            return Open(link, out _);
        }

        public bool Open(DocumentationLink link, out string error)
        {
            return TryOpenTarget(link, out error);
        }

        public bool OpenBacklogVersion(DocumentationLink link, int backlogIndex)
        {
            if (link == null || link.backlog == null || backlogIndex < 0 || backlogIndex >= link.backlog.Count)
                return false;

            DocumentationVersion version = link.backlog[backlogIndex];
            return TryOpenTarget(version.assetGuid, version.externalPath, out _);
        }

        public DocumentationLink FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            MigrateInMemory(true);
            return _links.FirstOrDefault(link => string.Equals(link.id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasAssetTarget(DocumentationLink link)
        {
            return link != null && HasAssetTarget(link.assetGuid);
        }

        public static bool HasAssetTarget(string assetGuid)
        {
            return GetTargetStatus(assetGuid, string.Empty).kind == DocumentationTargetKind.UnityAsset;
        }

        public static bool HasExternalPathTarget(DocumentationLink link)
        {
            return link != null && HasExternalPathTarget(link.externalPath);
        }

        public static bool HasExternalPathTarget(string externalPath)
        {
            return !string.IsNullOrWhiteSpace(externalPath);
        }

        public static bool HasWebUrlTarget(DocumentationLink link)
        {
            return link != null && HasWebUrlTarget(link.externalPath);
        }

        public static bool HasWebUrlTarget(string externalPath)
        {
            return GetTargetStatus(string.Empty, externalPath).kind == DocumentationTargetKind.WebUrl;
        }

        public static bool TryGetTargetKind(DocumentationLink link, out DocumentationTargetKind kind)
        {
            DocumentationTargetStatus status = GetTargetStatus(link);
            kind = status.kind;
            return status.canOpen;
        }

        public static bool TryGetTargetKind(string assetGuid, string externalPath, out DocumentationTargetKind kind)
        {
            DocumentationTargetStatus status = GetTargetStatus(assetGuid, externalPath);
            kind = status.kind;
            return status.canOpen;
        }

        public static DocumentationTargetKind GetTargetKind(string assetGuid, string externalPath)
        {
            return GetTargetStatus(assetGuid, externalPath).kind;
        }

        public static string GetTargetStatusLabel(string assetGuid, string externalPath)
        {
            return GetTargetStatus(assetGuid, externalPath).kindLabel;
        }

        public static DocumentationTargetStatus GetTargetStatus(DocumentationLink link)
        {
            return link == null ? EmptyStatus("Empty target.") : GetTargetStatus(link.assetGuid, link.externalPath);
        }

        public static DocumentationTargetStatus GetTargetStatus(DocumentationVersion version)
        {
            return version == null ? EmptyStatus("Empty target.") : GetTargetStatus(version.assetGuid, version.externalPath);
        }

        public static DocumentationTargetStatus GetTargetStatus(string assetGuid, string externalPath)
        {
            bool hasAssetGuid = !string.IsNullOrWhiteSpace(assetGuid);
            if (hasAssetGuid)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                UnityEngine.Object asset = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null)
                {
                    string message = string.IsNullOrWhiteSpace(externalPath)
                        ? "Valid Unity asset target."
                        : "Valid Unity asset target. Asset target takes precedence over the external path/web URL.";
                    return TargetStatus(DocumentationTargetKind.UnityAsset, "Asset", message, path, path, path, string.Empty, false, true, true, true, true);
                }

                if (string.IsNullOrWhiteSpace(externalPath))
                    return TargetStatus(DocumentationTargetKind.InvalidTarget, "Invalid Target", "Missing asset target.", string.Empty, string.Empty, string.Empty, string.Empty, false, true, false, false, false);
            }

            if (!string.IsNullOrWhiteSpace(externalPath))
                return GetExternalTargetStatus(externalPath);

            if (hasAssetGuid)
                return TargetStatus(DocumentationTargetKind.InvalidTarget, "Invalid Target", "Missing asset target.", string.Empty, string.Empty, string.Empty, string.Empty, false, true, false, false, false);

            return EmptyStatus("Empty target.");
        }

        public static string GetTargetKindLabel(DocumentationTargetKind kind)
        {
            switch (kind)
            {
                case DocumentationTargetKind.UnityAsset: return "Asset";
                case DocumentationTargetKind.LocalFile: return "Local File";
                case DocumentationTargetKind.WebUrl: return "Web URL";
                case DocumentationTargetKind.UnsupportedUrlScheme: return "Unsupported URL Scheme";
                case DocumentationTargetKind.InvalidTarget: return "Invalid Target";
                case DocumentationTargetKind.Missing: return "Missing Local File";
                case DocumentationTargetKind.Empty: return "Empty Target";
                default: return "Invalid Target";
            }
        }

        public static string GetDisplayName(DocumentationLink link)
        {
            if (link == null)
                return "Documentation";

            if (!string.IsNullOrWhiteSpace(link.displayName))
                return link.displayName.Trim();

            DocumentationTargetStatus status = GetTargetStatus(link);
            if (status.kind == DocumentationTargetKind.WebUrl && Uri.TryCreate(status.targetValue, UriKind.Absolute, out Uri uri) && !string.IsNullOrWhiteSpace(uri.Host))
                return BuildDisplayNameFromUri(uri);
            if (!string.IsNullOrWhiteSpace(status.targetValue))
                return BuildDisplayNameFromExternalPath(status.targetValue);

            return "Documentation";
        }

        public static string GetAssignedUtilitySummary(DocumentationLink link, int maxUtilities = 2)
        {
            if (link == null || link.utilityIds == null || link.utilityIds.Length == 0)
                return "Global";

            string[] names = link.utilityIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => PungentUtilityRegistry.Find(id)?.DisplayName ?? id)
                .Take(Mathf.Max(1, maxUtilities))
                .ToArray();

            if (names.Length == 0)
                return "Global";

            int extra = link.utilityIds.Count(id => !string.IsNullOrWhiteSpace(id)) - names.Length;
            return extra > 0 ? string.Join(", ", names) + " +" + extra : string.Join(", ", names);
        }

        public static bool OpenTarget(string assetGuid, string externalPath)
        {
            return TryOpenTarget(assetGuid, externalPath, out _);
        }

        public static bool TryOpenTarget(DocumentationLink link, out string error)
        {
            if (link == null)
            {
                error = "Documentation link is missing.";
                return false;
            }

            return TryOpenTarget(link.assetGuid, link.externalPath, out error);
        }

        public static bool TryOpenTarget(string assetGuid, string externalPath, out string error)
        {
            DocumentationTargetStatus status = GetTargetStatus(assetGuid, externalPath);
            error = string.Empty;

            switch (status.kind)
            {
                case DocumentationTargetKind.UnityAsset:
                {
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(status.assetPath);
                    if (asset == null)
                        break;

                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    AssetDatabase.OpenAsset(asset);
                    return true;
                }

                case DocumentationTargetKind.LocalFile:
                    EditorUtility.OpenWithDefaultApp(status.targetValue);
                    return true;

                case DocumentationTargetKind.WebUrl:
                    Application.OpenURL(status.targetValue);
                    return true;
            }

            error = string.IsNullOrWhiteSpace(status.message) ? "Documentation link target is missing or unsupported." : status.message;
            return false;
        }

        public static bool TryCopyTarget(DocumentationLink link, out string error)
        {
            if (link == null)
            {
                error = "Documentation link is missing.";
                return false;
            }

            return TryCopyTarget(link.assetGuid, link.externalPath, out error);
        }

        public static bool TryCopyTarget(string assetGuid, string externalPath, out string error)
        {
            DocumentationTargetStatus status = GetTargetStatus(assetGuid, externalPath);
            if (!status.canCopy || string.IsNullOrWhiteSpace(status.copyValue))
            {
                error = string.IsNullOrWhiteSpace(status.message) ? "Documentation link target cannot be copied." : status.message;
                return false;
            }

            EditorGUIUtility.systemCopyBuffer = status.copyValue;
            error = string.Empty;
            return true;
        }

        public static bool TryPingAsset(DocumentationLink link, out string error)
        {
            if (link == null)
            {
                error = "Documentation link is missing.";
                return false;
            }

            return TryPingAsset(link.assetGuid, out error);
        }

        public static bool TryPingAsset(string assetGuid, out string error)
        {
            DocumentationTargetStatus status = GetTargetStatus(assetGuid, string.Empty);
            if (!status.canPingAsset)
            {
                error = string.IsNullOrWhiteSpace(status.message) ? "Ping Asset is only available for valid Unity asset targets." : status.message;
                return false;
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(status.assetPath);
            if (asset == null)
            {
                error = "Missing asset target.";
                return false;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            error = string.Empty;
            return true;
        }

        private static bool HasCurrentTarget(DocumentationLink link)
        {
            return link != null && (!string.IsNullOrWhiteSpace(link.assetGuid) || !string.IsNullOrWhiteSpace(link.externalPath));
        }

        private static void AddCurrentToBacklogIfNeeded(DocumentationLink link, string notes)
        {
            if (!HasCurrentTarget(link))
                return;

            if (link.backlog == null)
                link.backlog = new List<DocumentationVersion>();

            link.backlog.Add(new DocumentationVersion
            {
                versionLabel = string.IsNullOrWhiteSpace(link.versionLabel) ? "Current" : link.versionLabel.Trim(),
                assetGuid = link.assetGuid ?? string.Empty,
                externalPath = NormalizeExternalPathOrUrl(link.externalPath),
                notes = notes ?? string.Empty,
                archivedUtcTicks = DateTime.UtcNow.Ticks
            });
        }

        private static void NormalizeTargets(DocumentationLink link)
        {
            if (link == null)
                return;

            link.externalPath = NormalizeExternalPathOrUrl(link.externalPath);
            if (link.backlog == null)
                return;

            for (int i = 0; i < link.backlog.Count; i++)
            {
                DocumentationVersion version = link.backlog[i];
                if (version != null)
                    version.externalPath = NormalizeExternalPathOrUrl(version.externalPath);
            }
        }

        private static DocumentationTargetStatus GetExternalTargetStatus(string externalPath)
        {
            string target = externalPath == null ? string.Empty : externalPath.Trim();
            if (string.IsNullOrWhiteSpace(target))
                return EmptyStatus("Empty target.");

            if (TryNormalizeWebUrl(target, out string normalizedUrl, out string webStatus))
            {
                Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri webUri);
                string scheme = webUri == null ? string.Empty : webUri.Scheme;
                bool normalized = !string.Equals(target, normalizedUrl, StringComparison.Ordinal);
                string message = normalized
                    ? "Valid web URL. Will save/open as " + normalizedUrl + "."
                    : string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                        ? "Valid HTTP web URL."
                        : "Valid HTTPS web URL.";
                if (!string.IsNullOrWhiteSpace(webStatus) && normalized)
                    message = webStatus;
                return TargetStatus(DocumentationTargetKind.WebUrl, "Web URL", message, normalizedUrl, normalizedUrl, string.Empty, scheme, normalized, true, true, true, false);
            }

            if (TryGetExplicitScheme(target, out string explicitScheme))
            {
                string message = string.Equals(explicitScheme, "http", StringComparison.OrdinalIgnoreCase) || string.Equals(explicitScheme, "https", StringComparison.OrdinalIgnoreCase)
                    ? "Invalid web URL."
                    : "Unsupported URL scheme '" + explicitScheme + "'. Only http:// and https:// documentation URLs can be opened.";
                DocumentationTargetKind kind = string.Equals(explicitScheme, "http", StringComparison.OrdinalIgnoreCase) || string.Equals(explicitScheme, "https", StringComparison.OrdinalIgnoreCase)
                    ? DocumentationTargetKind.InvalidTarget
                    : DocumentationTargetKind.UnsupportedUrlScheme;
                return TargetStatus(kind, GetTargetKindLabel(kind), message, target, string.Empty, string.Empty, explicitScheme, false, true, false, false, false);
            }

            if (File.Exists(target))
                return TargetStatus(DocumentationTargetKind.LocalFile, "Local File", "Valid local file.", target, target, string.Empty, string.Empty, false, true, true, true, false);

            if (IsProbablyBareDomain(target))
                return TargetStatus(DocumentationTargetKind.InvalidTarget, "Invalid Target", "Invalid web URL or missing local file.", target, string.Empty, string.Empty, string.Empty, false, true, false, false, false);

            return TargetStatus(DocumentationTargetKind.Missing, "Missing Local File", "Missing local file. Enter an existing local file path, or a web address like google.com.", target, string.Empty, string.Empty, string.Empty, false, true, false, false, false);
        }

        private static DocumentationTargetStatus EmptyStatus(string message)
        {
            return TargetStatus(DocumentationTargetKind.Empty, "Empty Target", message, string.Empty, string.Empty, string.Empty, string.Empty, false, false, false, false, false);
        }

        private static DocumentationTargetStatus TargetStatus(
            DocumentationTargetKind kind,
            string kindLabel,
            string message,
            string targetValue,
            string copyValue,
            string assetPath,
            string scheme,
            bool normalizedFromInput,
            bool hasTarget,
            bool canOpen,
            bool canCopy,
            bool canPingAsset)
        {
            return new DocumentationTargetStatus
            {
                kind = kind,
                kindLabel = string.IsNullOrWhiteSpace(kindLabel) ? GetTargetKindLabel(kind) : kindLabel,
                message = message ?? string.Empty,
                targetValue = targetValue ?? string.Empty,
                copyValue = copyValue ?? string.Empty,
                assetPath = assetPath ?? string.Empty,
                scheme = scheme ?? string.Empty,
                normalizedFromInput = normalizedFromInput,
                hasTarget = hasTarget,
                canOpen = canOpen,
                canCopy = canCopy,
                canPingAsset = canPingAsset
            };
        }

        public static string NormalizeExternalPathOrUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string trimmed = input.Trim();
            return TryNormalizeWebUrl(trimmed, out string normalizedUrl, out _) ? normalizedUrl : trimmed;
        }

        public static bool TryNormalizeWebUrl(string input, out string normalizedUrl, out string warningOrError)
        {
            normalizedUrl = string.Empty;
            warningOrError = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                warningOrError = "Empty target.";
                return false;
            }

            string trimmed = input.Trim();
            if (TryGetExplicitScheme(trimmed, out string scheme))
            {
                if (!IsSupportedWebScheme(scheme))
                {
                    warningOrError = "Unsupported URL scheme '" + scheme + "'. Only http:// and https:// documentation URLs can be opened.";
                    return false;
                }

                if (!TryCreateSupportedWebUri(trimmed, out _))
                {
                    warningOrError = "Invalid web URL.";
                    return false;
                }

                normalizedUrl = trimmed;
                return true;
            }

            if (LooksLikeLocalPath(trimmed))
            {
                warningOrError = "Local path target.";
                return false;
            }

            if (!IsProbablyBareDomain(trimmed))
            {
                warningOrError = "Not a web URL.";
                return false;
            }

            string candidate = "https://" + trimmed;
            if (!TryCreateSupportedWebUri(candidate, out _))
            {
                warningOrError = "Invalid web URL or missing local file.";
                return false;
            }

            normalizedUrl = candidate;
            warningOrError = "Valid web URL. Will save/open as " + normalizedUrl + ".";
            return true;
        }

        public static bool IsSupportedWebUrl(string input)
        {
            return TryNormalizeWebUrl(input, out _, out _);
        }

        public static bool IsProbablyBareDomain(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string trimmed = input.Trim();
            if (trimmed.IndexOfAny(new[] { '\\', ' ' }) >= 0)
                return false;
            if (trimmed.StartsWith("/", StringComparison.Ordinal) ||
                trimmed.StartsWith("./", StringComparison.Ordinal) ||
                trimmed.StartsWith("../", StringComparison.Ordinal) ||
                trimmed.StartsWith("~", StringComparison.Ordinal))
                return false;
            if (LooksLikeLocalPath(trimmed) || TryGetExplicitScheme(trimmed, out _))
                return false;

            string authority = FirstUrlSegment(trimmed);
            if (string.IsNullOrWhiteSpace(authority))
                return false;

            string host = authority;
            int colon = authority.LastIndexOf(':');
            if (colon >= 0)
            {
                string port = authority.Substring(colon + 1);
                if (string.IsNullOrWhiteSpace(port) || !port.All(char.IsDigit))
                    return false;
                host = authority.Substring(0, colon);
            }

            if (string.IsNullOrWhiteSpace(host) || host.StartsWith(".", StringComparison.Ordinal) || host.EndsWith(".", StringComparison.Ordinal))
                return false;

            string[] labels = host.Split('.');
            if (labels.Length < 2)
                return false;

            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                if (string.IsNullOrWhiteSpace(label) || label.StartsWith("-", StringComparison.Ordinal) || label.EndsWith("-", StringComparison.Ordinal))
                    return false;
                if (!label.All(c => char.IsLetterOrDigit(c) || c == '-'))
                    return false;
            }

            string topLevel = labels[labels.Length - 1];
            if (topLevel.Length < 2 || topLevel.Length > 63 || !topLevel.All(char.IsLetter))
                return false;

            bool hasPathOrQuery = trimmed.IndexOfAny(new[] { '/', '?', '#' }) >= 0;
            if (!hasPathOrQuery)
            {
                try
                {
                    string extension = Path.GetExtension(trimmed);
                    if (!string.IsNullOrWhiteSpace(extension) && FileNameExtensionsThatShouldNotBecomeUrls.Contains(extension))
                        return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryCreateSupportedWebUri(string value, out Uri uri)
        {
            uri = null;
            if (!Uri.IsWellFormedUriString(value, UriKind.Absolute))
                return false;

            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
                return false;

            if (!IsSupportedWebScheme(uri.Scheme))
                return false;

            if (string.IsNullOrWhiteSpace(uri.Host))
                return false;

            return true;
        }

        private static bool IsSupportedWebScheme(string scheme)
        {
            return string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetExplicitScheme(string value, out string scheme)
        {
            scheme = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            int colon = trimmed.IndexOf(':');
            if (colon <= 0)
                return false;

            if (colon == 1 && char.IsLetter(trimmed[0]) &&
                (trimmed.Length == 2 || trimmed[2] == '\\' || trimmed[2] == '/'))
                return false;

            string candidate = trimmed.Substring(0, colon);
            if (!Uri.CheckSchemeName(candidate))
                return false;

            scheme = candidate.ToLowerInvariant();
            return true;
        }

        private static string BuildDisplayNameFromExternalPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Documentation";

            string trimmed = value.Trim();
            if (TryNormalizeWebUrl(trimmed, out string normalizedUrl, out _) &&
                Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri uri))
                return BuildDisplayNameFromUri(uri);

            try
            {
                string fileName = Path.GetFileNameWithoutExtension(trimmed);
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }
            catch (ArgumentException)
            {
            }

            return trimmed.Length <= 80 ? trimmed : trimmed.Substring(0, 77) + "...";
        }

        private static string BuildDisplayNameFromUri(Uri uri)
        {
            if (uri == null)
                return "Documentation";

            string path = uri.AbsolutePath == null ? string.Empty : uri.AbsolutePath.Trim('/');
            if (!string.IsNullOrWhiteSpace(path))
            {
                string fileName = Path.GetFileNameWithoutExtension(path.Split('/').LastOrDefault() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;
            }

            return string.IsNullOrWhiteSpace(uri.Host) ? "Documentation" : uri.Host;
        }

        private static bool LooksLikeLocalPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (trimmed.IndexOf('\\') >= 0)
                return true;
            if (trimmed.StartsWith("/", StringComparison.Ordinal) ||
                trimmed.StartsWith("./", StringComparison.Ordinal) ||
                trimmed.StartsWith("../", StringComparison.Ordinal) ||
                trimmed.StartsWith("~", StringComparison.Ordinal))
                return true;

            try
            {
                if (Path.IsPathRooted(trimmed))
                    return true;
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
                return true;

            string first = FirstUrlSegment(trimmed);
            if (!string.IsNullOrWhiteSpace(first) && first.IndexOf('.') < 0 && trimmed.IndexOf('/') >= 0)
                return true;

            return false;
        }

        private static string FirstUrlSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            int end = value.IndexOfAny(new[] { '/', '?', '#' });
            return end < 0 ? value : value.Substring(0, end);
        }

        private void MigrateInMemory(bool ensureIds)
        {
            if (_links == null)
                _links = new List<DocumentationLink>();

            for (int i = 0; i < _links.Count; i++)
                PrepareLink(_links[i], ensureIds);
        }

        private static void PrepareLink(DocumentationLink link, bool ensureId)
        {
            if (link == null)
                return;

            if (ensureId && string.IsNullOrWhiteSpace(link.id))
                link.id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(link.versionLabel))
                link.versionLabel = "Current";
            if (link.utilityIds == null)
                link.utilityIds = Array.Empty<string>();
            else
                link.utilityIds = link.utilityIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (link.backlog == null)
                link.backlog = new List<DocumentationVersion>();
            if (link.displayName == null)
                link.displayName = string.Empty;
            if (link.description == null)
                link.description = string.Empty;
            if (link.category == null)
                link.category = string.Empty;
        }

        private void Reindex()
        {
            for (int i = 0; i < _links.Count; i++)
                _links[i].order = i;
        }
    }
#endif
}
