using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Lightweight architecture/layout validator for the PungentFunk utility suite.
    /// This intentionally checks durable design-bible signals rather than trying to lint every C# style choice.
    /// </summary>
    public static class PungentUtilityDesignAudit
    {
        public enum Severity
        {
            Info,
            Warning,
            Error
        }

        public sealed class Issue
        {
            public Severity severity;
            public string area;
            public string target;
            public string message;
            public string recommendation;
            public string assetPath;

            public Issue(Severity severity, string area, string target, string message, string recommendation = null, string assetPath = null)
            {
                this.severity = severity;
                this.area = string.IsNullOrWhiteSpace(area) ? "General" : area;
                this.target = string.IsNullOrWhiteSpace(target) ? "Package" : target;
                this.message = message ?? string.Empty;
                this.recommendation = recommendation ?? string.Empty;
                this.assetPath = assetPath ?? string.Empty;
            }
        }

        public sealed class Report
        {
            public DateTime generatedUtc;
            public int scriptCount;
            public int editorWindowCount;
            public int asmdefCount;
            public int namespaceDeclarationCount;
            public int descriptorCount;
            public int createAssetMenuCount;
            public int menuItemCount;
            public readonly List<Issue> issues = new List<Issue>();

            public int ErrorCount => issues.Count(i => i.severity == Severity.Error);
            public int WarningCount => issues.Count(i => i.severity == Severity.Warning);
            public int InfoCount => issues.Count(i => i.severity == Severity.Info);
            public bool HasBlockingIssues => ErrorCount > 0;

            public IEnumerable<Issue> Filter(Severity? severity, string search)
            {
                IEnumerable<Issue> query = issues;
                if (severity.HasValue)
                    query = query.Where(i => i.severity == severity.Value);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string q = search.Trim();
                    query = query.Where(i => Contains(i.area, q) || Contains(i.target, q) || Contains(i.message, q) || Contains(i.recommendation, q) || Contains(i.assetPath, q));
                }

                return query
                    .OrderByDescending(i => i.severity)
                    .ThenBy(i => i.area, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.target, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.message, StringComparer.OrdinalIgnoreCase);
            }

            public string ToMarkdownSummary(int maxIssues = 80)
            {
                var sb = new StringBuilder();
                sb.AppendLine("# PungentFunk Utility Design Audit");
                sb.AppendLine();
                sb.AppendLine($"Generated UTC: {generatedUtc:u}");
                sb.AppendLine($"Scripts: {scriptCount}");
                sb.AppendLine($"Editor windows: {editorWindowCount}");
                sb.AppendLine($"Registered utilities: {descriptorCount}");
                sb.AppendLine($"Assembly definitions: {asmdefCount}");
                sb.AppendLine($"Namespace declarations: {namespaceDeclarationCount}");
                sb.AppendLine($"CreateAssetMenu attributes: {createAssetMenuCount}");
                sb.AppendLine($"MenuItem attributes: {menuItemCount}");
                sb.AppendLine($"Issues: {ErrorCount} errors, {WarningCount} warnings, {InfoCount} info");
                sb.AppendLine();

                foreach (Issue issue in issues
                             .OrderByDescending(i => i.severity)
                             .ThenBy(i => i.area, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(i => i.target, StringComparer.OrdinalIgnoreCase)
                             .Take(Mathf.Max(1, maxIssues)))
                {
                    sb.AppendLine($"- **{issue.severity}** · {issue.area} · {issue.target}: {issue.message}");
                    if (!string.IsNullOrWhiteSpace(issue.recommendation))
                        sb.AppendLine($"  - Recommendation: {issue.recommendation}");
                    if (!string.IsNullOrWhiteSpace(issue.assetPath))
                        sb.AppendLine($"  - Asset: `{issue.assetPath}`");
                }

                if (issues.Count > maxIssues)
                    sb.AppendLine($"- ... {issues.Count - maxIssues} additional issue(s) omitted.");

                return sb.ToString();
            }
        }

        private sealed class ScriptRecord
        {
            public string assetPath;
            public string absolutePath;
            public string text;
            public int lineCount;
            public bool isEditorScript;
            public bool hasNamespace;
            public bool isEditorWindow;
            public string windowClassName;
        }

        private static readonly Regex NamespaceRegex = new Regex(@"(^|\n)\s*namespace\s+[A-Za-z0-9_.]+", RegexOptions.Compiled);
        private static readonly Regex EditorWindowRegex = new Regex(@"class\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*EditorWindow", RegexOptions.Compiled);
        private static readonly Regex CreateAssetMenuRegex = new Regex(@"CreateAssetMenu\s*\((?<args>[\s\S]*?)\)\]", RegexOptions.Compiled);
        private static readonly Regex MenuNameRegex = new Regex("menuName\\s*=\\s*\"(?<path>[^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex MenuItemRegex = new Regex("MenuItem\\s*\\(\\s*\"(?<path>[^\"]+)\"", RegexOptions.Compiled);

        public static Report Run(bool includeInfo = true)
        {
            Report report = new Report { generatedUtc = DateTime.UtcNow };
            List<ScriptRecord> scripts = LoadPackageScripts();

            report.scriptCount = scripts.Count;
            report.editorWindowCount = scripts.Count(s => s.isEditorWindow);
            report.namespaceDeclarationCount = scripts.Count(s => s.hasNamespace);
            report.asmdefCount = CountPackageAsmdefs();
            report.descriptorCount = PungentUtilityRegistry.All.Count;
            report.createAssetMenuCount = scripts.Sum(s => CreateAssetMenuRegex.Matches(s.text).Count);
            report.menuItemCount = scripts.Sum(s => MenuItemRegex.Matches(s.text).Count);

            ValidatePackageBoundaries(report, scripts, includeInfo);
            ValidateRegistry(report, includeInfo);
            ValidateEditorWindows(report, scripts, includeInfo);
            ValidateCreateAssetMenus(report, scripts, includeInfo);
            ValidateMenuItems(report, scripts, includeInfo);

            return report;
        }

        public static string LocateDocumentationAsset(string filenameContains = null)
        {
            string[] guids = AssetDatabase.FindAssets(string.IsNullOrWhiteSpace(filenameContains) ? "PungentFunk_Utilities" : filenameContains);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (!IsInsidePungentFunkUtilities(path))
                    continue;

                string fileName = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(filenameContains) || fileName.IndexOf(filenameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return path;
            }

            return null;
        }

        public static void OpenDocumentation(string preferredFilenameContains = "Architecture_Design_Bible")
        {
            string path = LocateDocumentationAsset(preferredFilenameContains) ?? LocateDocumentationAsset("Audit_and_Feature_Inventory") ?? LocateDocumentationAsset(null);
            if (string.IsNullOrWhiteSpace(path))
            {
                EditorUtility.DisplayDialog(
                    "PungentFunk Documentation",
                    "No bundled documentation asset was found. Expected files are usually stored under PungentFunkUtilities/Documentation.",
                    "OK");
                return;
            }

            UnityEngine.Object doc = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (doc == null)
            {
                EditorUtility.DisplayDialog("PungentFunk Documentation", "Could not load documentation asset at:\n" + path, "OK");
                return;
            }

            Selection.activeObject = doc;
            EditorGUIUtility.PingObject(doc);
            AssetDatabase.OpenAsset(doc);
        }

        private static List<ScriptRecord> LoadPackageScripts()
        {
            var records = new List<ScriptRecord>();
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsInsidePungentFunkUtilities(assetPath) || !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string absolutePath = ToAbsolutePath(assetPath);
                string text = SafeReadAllText(absolutePath);
                if (text == null)
                    continue;

                Match editorWindowMatch = EditorWindowRegex.Match(text);
                records.Add(new ScriptRecord
                {
                    assetPath = assetPath,
                    absolutePath = absolutePath,
                    text = text,
                    lineCount = CountLines(text),
                    isEditorScript = assetPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 || assetPath.StartsWith("Editor/", StringComparison.OrdinalIgnoreCase),
                    hasNamespace = NamespaceRegex.IsMatch(text),
                    isEditorWindow = editorWindowMatch.Success,
                    windowClassName = editorWindowMatch.Success ? editorWindowMatch.Groups[1].Value : string.Empty
                });
            }

            return records.OrderBy(r => r.assetPath, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static int CountPackageAsmdefs()
        {
            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsInsidePungentFunkUtilities(path))
                    count++;
            }

            if (count > 0)
                return count;

            // Fallback for Unity versions that do not classify asmdefs under t:AssemblyDefinitionAsset.
            string dataPath = Application.dataPath;
            if (!Directory.Exists(dataPath))
                return 0;

            try
            {
                foreach (string file in Directory.GetFiles(dataPath, "*.asmdef", SearchOption.AllDirectories))
                {
                    string relative = ToAssetPath(file);
                    if (IsInsidePungentFunkUtilities(relative))
                        count++;
                }
            }
            catch
            {
                // Keep the audit non-blocking. A missing asmdef count is less harmful than a broken validator window.
            }

            return count;
        }

        private static void ValidatePackageBoundaries(Report report, List<ScriptRecord> scripts, bool includeInfo)
        {
            if (report.asmdefCount == 0)
            {
                Add(report, Severity.Error, "Package Boundaries", "Assembly Definitions", "No assembly definition files were found in the package.", "Add Core/runtime/editor asmdefs before release so labs can remain independently shippable and optional integrations can be isolated.");
            }
            else if (includeInfo)
            {
                Add(report, Severity.Info, "Package Boundaries", "Assembly Definitions", report.asmdefCount + " assembly definition file(s) found.", "Review runtime/editor dependency direction before packaging.");
            }

            if (report.namespaceDeclarationCount == 0 && scripts.Count > 0)
            {
                Add(report, Severity.Error, "Package Boundaries", "Namespaces", "No namespace declarations were found in package scripts.", "Add a PungentFunk.Utilities namespace hierarchy before distribution to reduce global-name collisions.");
            }
            else if (report.namespaceDeclarationCount < scripts.Count && includeInfo)
            {
                Add(report, Severity.Warning, "Package Boundaries", "Namespaces", $"Only {report.namespaceDeclarationCount}/{scripts.Count} scripts currently declare namespaces.", "Complete namespace migration by lab once asmdef boundaries are stable.");
            }
        }

        private static void ValidateRegistry(Report report, bool includeInfo)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PungentUtilityDescriptor descriptor in PungentUtilityRegistry.All)
            {
                string target = string.IsNullOrWhiteSpace(descriptor.DisplayName) ? descriptor.Id : descriptor.DisplayName;
                if (string.IsNullOrWhiteSpace(descriptor.Id))
                    Add(report, Severity.Error, "Registry", target, "Descriptor is missing a stable ID.", "Assign a kebab-case ID and keep it stable across releases.");
                else if (!ids.Add(descriptor.Id))
                    Add(report, Severity.Error, "Registry", target, "Duplicate descriptor ID detected: " + descriptor.Id, "Use unique stable IDs. Avoid per-lab registrars replacing central descriptors with weaker metadata.");

                if (string.IsNullOrWhiteSpace(descriptor.Description))
                    Add(report, Severity.Warning, "Registry", target, "Descriptor is missing a description.", "Use a one-sentence promise that explains what the tool does and when to use it.");

                if (string.IsNullOrWhiteSpace(descriptor.Lab) || descriptor.Lab == PungentUtilityLabs.Other)
                    Add(report, Severity.Warning, "Registry", target, "Descriptor is not assigned to a standard lab.", "Assign a durable lab boundary so the control panel, docs, and package split stay coherent.");

                if (string.IsNullOrWhiteSpace(descriptor.Module))
                    Add(report, Severity.Warning, "Registry", target, "Descriptor is missing a module.", "Use module metadata to avoid long undifferentiated lab lists.");

                if (string.IsNullOrWhiteSpace(descriptor.PackageStatus))
                    Add(report, Severity.Warning, "Registry", target, "Descriptor is missing a package status.", "Use Stable, Experimental, In Progress, Deprecated, or Project Adapter.");
                else if (!PungentUtilityPackageStatus.All.Any(s => string.Equals(s, PungentUtilityPackageStatus.Normalize(descriptor.PackageStatus), StringComparison.OrdinalIgnoreCase)))
                    Add(report, Severity.Warning, "Registry", target, "Descriptor uses a non-standard package status: " + descriptor.PackageStatus, "Use standard status labels so launcher badges and docs stay consistent.");

                if (string.IsNullOrWhiteSpace(descriptor.MenuPath))
                    Add(report, Severity.Warning, "Registry", target, "Descriptor is missing a primary menu path.", "Primary editor windows should expose a clear Tools/Utilities/... route while migration aliases remain secondary.");
                else if (!descriptor.MenuPath.StartsWith("Tools/Utilities/", StringComparison.OrdinalIgnoreCase) && !descriptor.MenuPath.StartsWith("Tools/PungentFunk Utilities/", StringComparison.OrdinalIgnoreCase))
                    Add(report, Severity.Warning, "Registry", target, "Primary menu path is outside the suite taxonomy: " + descriptor.MenuPath, "Route public access through Tools/Utilities/... or the release menu root used by the package.");

                if (!descriptor.CanOpen)
                    Add(report, Severity.Error, "Registry", target, "Descriptor cannot resolve an open action or EditorWindow type.", "Check WindowTypeName, renamed classes, and per-lab registration overrides.");

                if (descriptor.RelatedUtilityIds != null)
                {
                    for (int i = 0; i < descriptor.RelatedUtilityIds.Length; i++)
                    {
                        string relatedId = descriptor.RelatedUtilityIds[i];
                        if (!string.IsNullOrWhiteSpace(relatedId) && PungentUtilityRegistry.Find(relatedId) == null)
                            Add(report, Severity.Warning, "Registry", target, "Related utility ID is missing: " + relatedId, "Remove stale related IDs or register the companion utility with complete metadata.");
                    }
                }
            }

            if (includeInfo)
                Add(report, Severity.Info, "Registry", "Registered Utilities", PungentUtilityRegistry.All.Count + " utilities registered with the central registry.", "Use this as the package discovery source of truth.");
        }

        private static void ValidateEditorWindows(Report report, List<ScriptRecord> scripts, bool includeInfo)
        {
            foreach (ScriptRecord script in scripts.Where(s => s.isEditorWindow))
            {
                string target = string.IsNullOrWhiteSpace(script.windowClassName) ? Path.GetFileNameWithoutExtension(script.assetPath) : script.windowClassName;
                if (!script.text.Contains("UtilityWindowTheme"))
                    Add(report, Severity.Error, "Window Layout", target, "EditorWindow does not appear to use UtilityWindowTheme.", "Use the shared theme for headers, panels, status pills, and action styles.", script.assetPath);

                if (!script.text.Contains("BeginScrollView"))
                    Add(report, Severity.Warning, "Window Layout", target, "EditorWindow does not contain a visible scroll-view pattern.", "Windows should remain usable at small sizes. Use vertical scrolling or adaptive panel stacking for ordinary forms and cards.", script.assetPath);

                if (!script.text.Contains("UtilityWindowPrefs") && !script.text.Contains("EditorPrefs"))
                    Add(report, Severity.Warning, "Window Layout", target, "No persisted window preference usage was detected.", "Persist foldouts, active modules, splitter sizes, or recently used context where it improves workflow continuity.", script.assetPath);

                if (!script.text.Contains("minSize"))
                    Add(report, Severity.Warning, "Window Layout", target, "No explicit minSize was detected.", "Set a practical minimum size and ensure the layout degrades gracefully below wide-monitor assumptions.", script.assetPath);

                if (script.lineCount > 1500)
                    Add(report, Severity.Error, "Maintainability", target, $"Large monolithic EditorWindow ({script.lineCount} lines).", "Split into state, services, render panels, scan models, and reusable widgets before adding more feature depth.", script.assetPath);
                else if (script.lineCount > 850)
                    Add(report, Severity.Warning, "Maintainability", target, $"Large EditorWindow ({script.lineCount} lines).", "Consider extracting scan/apply services or panel drawers during the next refactor pass.", script.assetPath);

                if (script.lineCount > 450 && !script.text.Contains("ResizeHandle") && !script.text.Contains("Splitter"))
                    Add(report, Severity.Warning, "Window Layout", target, "Large window has no detected draggable resize handle/splitter usage.", "Use resizable major panels where preview/results and configuration compete for space.", script.assetPath);

                if (includeInfo && script.text.Contains("UtilityWindowTheme") && script.text.Contains("BeginScrollView"))
                    Add(report, Severity.Info, "Window Layout", target, "Uses shared theme and vertical scrolling.", "Continue validating field grouping, core-action visibility, and compact-mode behaviour manually.", script.assetPath);
            }
        }

        private static void ValidateCreateAssetMenus(Report report, List<ScriptRecord> scripts, bool includeInfo)
        {
            foreach (ScriptRecord script in scripts)
            {
                MatchCollection matches = CreateAssetMenuRegex.Matches(script.text);
                foreach (Match match in matches)
                {
                    string path = ExtractMenuName(match.Groups["args"].Value);
                    string target = Path.GetFileNameWithoutExtension(script.assetPath);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        Add(report, Severity.Warning, "CreateAssetMenu", target, "CreateAssetMenu has no menuName.", "Use PungentFunk Utilities/<Lab>/<Asset Type> as the public asset creation root.", script.assetPath);
                        continue;
                    }

                    if (!path.StartsWith("PungentFunk Utilities/", StringComparison.Ordinal))
                    {
                        Add(report, Severity.Error, "CreateAssetMenu", target, "CreateAssetMenu root is inconsistent: " + path, "Use PungentFunk Utilities/<Lab>/<Asset Type> to keep asset menus coherent.", script.assetPath);
                    }
                    else if (includeInfo)
                    {
                        Add(report, Severity.Info, "CreateAssetMenu", target, "CreateAssetMenu root is aligned: " + path, null, script.assetPath);
                    }
                }
            }
        }

        private static void ValidateMenuItems(Report report, List<ScriptRecord> scripts, bool includeInfo)
        {
            foreach (ScriptRecord script in scripts)
            {
                MatchCollection matches = MenuItemRegex.Matches(script.text);
                foreach (Match match in matches)
                {
                    string path = match.Groups["path"].Value;
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    bool isPrimary = path.StartsWith("Tools/Utilities/", StringComparison.OrdinalIgnoreCase) ||
                                     path.StartsWith("Tools/PungentFunk Utilities/", StringComparison.OrdinalIgnoreCase) ||
                                     path.StartsWith("Assets/PungentFunk Utilities/", StringComparison.OrdinalIgnoreCase) ||
                                     path.StartsWith("GameObject/PungentFunk Utilities/", StringComparison.OrdinalIgnoreCase);

                    bool isLegacyToolsAlias = path.StartsWith("Tools/", StringComparison.OrdinalIgnoreCase) &&
                                              !path.StartsWith("Tools/Utilities/", StringComparison.OrdinalIgnoreCase) &&
                                              !path.StartsWith("Tools/PungentFunk Utilities/", StringComparison.OrdinalIgnoreCase);

                    if (isLegacyToolsAlias)
                    {
                        Add(report, Severity.Warning, "Menu Taxonomy", Path.GetFileNameWithoutExtension(script.assetPath), "Legacy or parallel Tools menu alias detected: " + path, "Keep aliases only for migration and prefer the registered Tools/Utilities/... route for public access.", script.assetPath);
                    }
                    else if (!isPrimary && includeInfo)
                    {
                        Add(report, Severity.Info, "Menu Taxonomy", Path.GetFileNameWithoutExtension(script.assetPath), "Non-primary menu route detected: " + path, "Confirm it is a context-aware route rather than duplicate launcher clutter.", script.assetPath);
                    }
                }
            }
        }

        private static string ExtractMenuName(string args)
        {
            Match match = MenuNameRegex.Match(args ?? string.Empty);
            return match.Success ? match.Groups["path"].Value : string.Empty;
        }

        private static void Add(Report report, Severity severity, string area, string target, string message, string recommendation = null, string assetPath = null)
        {
            report.issues.Add(new Issue(severity, area, target, message, recommendation, assetPath));
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsInsidePungentFunkUtilities(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string normalized = assetPath.Replace('\\', '/');
            return normalized.IndexOf("PungentFunkUtilities/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith("PungentFunkUtilities", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
            string normalized = absolutePath.Replace('\\', '/');
            return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length).TrimStart('/')
                : normalized;
        }

        private static string SafeReadAllText(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch
            {
                return null;
            }
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    count++;
            }

            return count;
        }
    }
    #endif

}