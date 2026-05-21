namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using PungentFunk.Utilities.Editor.Core;
    using UnityEditor;
    using UnityEngine;

    public static class PungentUtilityHelpAutoIndexer
    {
        public const string ScriptingOwner = "Generated scripting index";
        private static readonly List<Type> ExplicitScriptingTypes = new List<Type>();
        private static readonly string[] DefaultExplicitTypeNames =
        {
            "PungentFunk.Utilities.Debugging.DebugRouter"
        };

        private static readonly HashSet<Type> ExcludedDeclaringTypes = new HashSet<Type>
        {
            typeof(object),
            typeof(UnityEngine.Object),
            typeof(ScriptableObject),
            typeof(MonoBehaviour),
            typeof(EditorWindow),
            typeof(UnityEditor.Editor)
        };

        public static void RegisterScriptingType(Type type)
        {
            if (type != null && !ExplicitScriptingTypes.Contains(type))
                ExplicitScriptingTypes.Add(type);
        }

        public static int RefreshGeneratedIndex(out string status)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Pungent Help", "Scanning package scripting surfaces...", 0.10f);
                List<PungentUtilityHelpTopic> topics = GenerateTopics();
                int stale = MarkStaleGeneratedEntries(topics);
                status = "Generated " + topics.Sum(t => t.scriptingEntries.Count(e => !e.stale)) + " scripting entries across " + topics.Count + " topics. Stale: " + stale + ".";
                PungentUtilityHelpStorage.instance.ReplaceGeneratedTopicsByOwner(ScriptingOwner, topics, status);
                return topics.Sum(t => t.scriptingEntries.Count);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static int RemoveStaleGeneratedEntries(out string status)
        {
            int removed = 0;
            List<PungentUtilityHelpTopic> generated = PungentUtilityHelpStorage.instance.generatedTopics ?? new List<PungentUtilityHelpTopic>();
            foreach (PungentUtilityHelpTopic topic in generated.Where(t => t != null && string.Equals(t.sourceOwner, ScriptingOwner, StringComparison.OrdinalIgnoreCase)))
            {
                if (topic.scriptingEntries == null)
                    continue;
                removed += topic.scriptingEntries.RemoveAll(e => e != null && e.stale);
            }

            generated.RemoveAll(t => t != null &&
                                     string.Equals(t.sourceOwner, ScriptingOwner, StringComparison.OrdinalIgnoreCase) &&
                                     (t.scriptingEntries == null || t.scriptingEntries.Count == 0));
            status = "Removed " + removed + " stale generated scripting entries.";
            PungentUtilityHelpStorage.instance.lastGeneratedStatus = status;
            PungentUtilityHelpStorage.instance.Persist();
            return removed;
        }

        private static List<PungentUtilityHelpTopic> GenerateTopics()
        {
            Dictionary<string, PungentUtilityHelpTopic> topics = new Dictionary<string, PungentUtilityHelpTopic>(StringComparer.OrdinalIgnoreCase);
            List<Type> types = GetCandidateTypes();
            int total = Mathf.Max(1, types.Count);

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                EditorUtility.DisplayProgressBar("Pungent Help", "Indexing " + type.Name, 0.10f + (0.85f * i / total));
                foreach (PungentUtilityHelpScriptingEntry entry in BuildEntries(type))
                {
                    ResolveTopic(type, entry, out string utilityId, out string sectionId, out string topicId);
                    string key = PungentUtilityHelpIds.TopicKey(utilityId, sectionId, topicId);
                    if (!topics.TryGetValue(key, out PungentUtilityHelpTopic topic))
                    {
                        topic = new PungentUtilityHelpTopic
                        {
                            utilityId = utilityId,
                            sectionId = sectionId,
                            topicId = topicId,
                            title = ObjectNames.NicifyVariableName(topicId),
                            summary = "Generated scripting index. Add a manual override to curate descriptions and examples.",
                            developerOnly = true,
                            generated = true,
                            sourceOwner = ScriptingOwner,
                            lastUpdatedUtc = DateTime.UtcNow.ToString("o"),
                            tags = new List<string> { "generated", "scripting", "api" }
                        };
                        topics[key] = topic;
                    }

                    topic.scriptingEntries.Add(entry);
                }
            }

            return topics.Values.OrderBy(t => t.utilityId).ThenBy(t => t.topicId).ToList();
        }

        private static int MarkStaleGeneratedEntries(List<PungentUtilityHelpTopic> freshTopics)
        {
            List<PungentUtilityHelpTopic> existing = PungentUtilityHelpStorage.instance.generatedTopics ?? new List<PungentUtilityHelpTopic>();
            Dictionary<string, PungentUtilityHelpTopic> freshById = freshTopics.ToDictionary(t => t.StableId, StringComparer.OrdinalIgnoreCase);
            HashSet<string> freshEntries = new HashSet<string>(
                freshTopics.SelectMany(t => t.scriptingEntries ?? new List<PungentUtilityHelpScriptingEntry>()).Where(e => e != null).Select(e => e.id),
                StringComparer.OrdinalIgnoreCase);

            int stale = 0;
            foreach (PungentUtilityHelpTopic previousTopic in existing.Where(t => t != null && string.Equals(t.sourceOwner, ScriptingOwner, StringComparison.OrdinalIgnoreCase)))
            {
                if (previousTopic.scriptingEntries == null)
                    continue;

                foreach (PungentUtilityHelpScriptingEntry previousEntry in previousTopic.scriptingEntries)
                {
                    if (previousEntry == null || freshEntries.Contains(previousEntry.id))
                        continue;

                    PungentUtilityHelpTopic target;
                    if (!freshById.TryGetValue(previousTopic.StableId, out target))
                    {
                        target = new PungentUtilityHelpTopic
                        {
                            utilityId = previousTopic.utilityId,
                            sectionId = previousTopic.sectionId,
                            topicId = previousTopic.topicId,
                            title = previousTopic.title,
                            summary = "Generated scripting index contains stale entries that no longer match source members.",
                            developerOnly = true,
                            generated = true,
                            sourceOwner = ScriptingOwner,
                            lastUpdatedUtc = DateTime.UtcNow.ToString("o"),
                            tags = new List<string> { "generated", "scripting", "stale" }
                        };
                        freshTopics.Add(target);
                        freshById[target.StableId] = target;
                    }

                    PungentUtilityHelpScriptingEntry staleEntry = CloneEntry(previousEntry);
                    staleEntry.stale = true;
                    staleEntry.developerOnly = true;
                    staleEntry.hidden = true;
                    staleEntry.description = string.IsNullOrWhiteSpace(staleEntry.description)
                        ? "Stale generated entry. The source member was not found during the latest refresh."
                        : staleEntry.description;
                    target.scriptingEntries.Add(staleEntry);
                    stale++;
                }
            }

            return stale;
        }

        private static PungentUtilityHelpScriptingEntry CloneEntry(PungentUtilityHelpScriptingEntry source)
        {
            return new PungentUtilityHelpScriptingEntry
            {
                id = source.id,
                declaringType = source.declaringType,
                memberName = source.memberName,
                signature = source.signature,
                description = source.description,
                usageNotes = source.usageNotes,
                minimalExample = source.minimalExample,
                whereItAppears = source.whereItAppears,
                developerOnly = source.developerOnly,
                hidden = source.hidden,
                generated = source.generated,
                stale = source.stale,
                sourcePath = source.sourcePath
            };
        }

        private static List<Type> GetCandidateTypes()
        {
            HashSet<Type> types = new HashSet<Type>();

            foreach (string typeName in DefaultExplicitTypeNames)
            {
                Type type = FindType(typeName);
                if (type != null)
                    types.Add(type);
            }

            foreach (Type type in ExplicitScriptingTypes)
                if (type != null)
                    types.Add(type);

            foreach (PungentUtilityDescriptor descriptor in PungentUtilityRegistry.All)
            {
                Type type = descriptor.ResolveWindowType();
                if (type != null)
                    types.Add(type);
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in SafeGetTypes(assembly))
                {
                    if (type == null || string.IsNullOrWhiteSpace(type.Namespace))
                        continue;
                    if (!type.Namespace.StartsWith("PungentFunk.Utilities", StringComparison.Ordinal))
                        continue;
                    if (HasHide(type))
                        continue;
                    if (HasInclude(type) || type.GetCustomAttributes(typeof(PungentHelpTopicAttribute), true).Length > 0)
                        types.Add(type);
                }
            }

            return types.Where(IsPackageOwned).OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
        }

        private static IEnumerable<PungentUtilityHelpScriptingEntry> BuildEntries(Type type)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (!ShouldIncludeMethod(type, method))
                    continue;

                PungentScriptingReferenceAttribute reference = method.GetCustomAttribute<PungentScriptingReferenceAttribute>(true);
                string description = reference == null ? string.Empty : reference.Description;
                yield return new PungentUtilityHelpScriptingEntry
                {
                    id = MakeEntryId(type, method),
                    declaringType = type.FullName ?? type.Name,
                    memberName = method.Name,
                    signature = BuildSignature(method),
                    description = string.IsNullOrWhiteSpace(description) ? "Generated entry awaiting developer review." : description,
                    usageNotes = reference == null ? string.Empty : reference.UsageNotes,
                    minimalExample = reference == null ? string.Empty : reference.MinimalExample,
                    whereItAppears = reference == null ? ResolveUtilityLabel(type) : reference.WhereItAppears,
                    developerOnly = string.IsNullOrWhiteSpace(description),
                    hidden = false,
                    generated = true,
                    sourcePath = ResolveSourcePath(type)
                };
            }
        }

        private static bool ShouldIncludeMethod(Type type, MethodInfo method)
        {
            if (method == null || method.DeclaringType == null)
                return false;
            if (ExcludedDeclaringTypes.Contains(method.DeclaringType))
                return false;
            if (method.IsSpecialName)
                return false;
            if (method.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                return false;
            if (method.GetCustomAttribute<ObsoleteAttribute>() != null && !HasInclude(method))
                return false;
            if (HasHide(method) || HasHide(type))
                return false;

            bool explicitType = ExplicitScriptingTypes.Contains(type) || DefaultExplicitTypeNames.Contains(type.FullName);
            bool annotated = HasInclude(method) || method.GetCustomAttribute<PungentScriptingReferenceAttribute>(true) != null || method.GetCustomAttributes(typeof(PungentHelpTopicAttribute), true).Length > 0;
            bool registeredWindow = typeof(EditorWindow).IsAssignableFrom(type);

            if (registeredWindow)
                return annotated;

            return explicitType || HasInclude(type) || annotated;
        }

        private static void ResolveTopic(Type type, PungentUtilityHelpScriptingEntry entry, out string utilityId, out string sectionId, out string topicId)
        {
            PungentHelpTopicAttribute attribute = type.GetCustomAttributes(typeof(PungentHelpTopicAttribute), true).OfType<PungentHelpTopicAttribute>().FirstOrDefault()
                                                 ?? FindMethodTopicAttribute(type, entry.memberName);
            if (attribute != null)
            {
                utilityId = PungentUtilityHelpIds.Normalize(attribute.UtilityId, GuessUtilityId(type));
                sectionId = PungentUtilityHelpIds.Normalize(attribute.SectionId, "scripting-index");
                topicId = PungentUtilityHelpIds.Normalize(attribute.TopicId, "scripting-index");
                return;
            }

            utilityId = GuessUtilityId(type);
            sectionId = "scripting-index";
            topicId = "scripting-index";
        }

        private static PungentHelpTopicAttribute FindMethodTopicAttribute(Type type, string memberName)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => string.Equals(m.Name, memberName, StringComparison.Ordinal));
            return method == null ? null : method.GetCustomAttributes(typeof(PungentHelpTopicAttribute), true).OfType<PungentHelpTopicAttribute>().FirstOrDefault();
        }

        private static string GuessUtilityId(Type type)
        {
            if (type == null)
                return "help-browser";
            if (type.FullName == "PungentFunk.Utilities.Debugging.DebugRouter")
                return "debug-control";

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.FindByWindowType(type);
            return descriptor == null ? "help-browser" : descriptor.Id;
        }

        private static string ResolveUtilityLabel(Type type)
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.FindByWindowType(type);
            if (descriptor != null)
                return descriptor.DisplayName;
            if (type != null && type.FullName == "PungentFunk.Utilities.Debugging.DebugRouter")
                return "Debug Control Center / DebugRouter";
            return string.Empty;
        }

        private static string MakeEntryId(Type type, MethodInfo method)
        {
            string parameters = string.Join("-", method.GetParameters().Select(p => p.ParameterType.Name));
            return PungentUtilityHelpIds.Normalize((type.FullName ?? type.Name) + "." + method.Name + "(" + parameters + ")");
        }

        private static string BuildSignature(MethodInfo method)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(GetFriendlyTypeName(method.ReturnType));
            builder.Append(" ");
            builder.Append(method.Name);
            builder.Append("(");
            builder.Append(string.Join(", ", method.GetParameters().Select(p => GetFriendlyTypeName(p.ParameterType) + " " + p.Name)));
            builder.Append(")");
            return builder.ToString();
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null)
                return "void";
            if (type == typeof(void))
                return "void";
            if (!type.IsGenericType)
                return type.Name;
            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0)
                name = name.Substring(0, tick);
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName)) + ">";
        }

        private static string ResolveSourcePath(Type type)
        {
            string typeName = type == null ? string.Empty : type.Name;
            if (string.IsNullOrWhiteSpace(typeName))
                return string.Empty;

            string[] guids = AssetDatabase.FindAssets(typeName + " t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                    return path;
            }

            return string.Empty;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static bool IsPackageOwned(Type type)
        {
            return type != null && type.Namespace != null && type.Namespace.StartsWith("PungentFunk.Utilities", StringComparison.Ordinal);
        }

        private static bool HasInclude(MemberInfo member)
        {
            return member != null && member.GetCustomAttribute<PungentHelpIncludeAttribute>(true) != null;
        }

        private static bool HasHide(MemberInfo member)
        {
            return member != null && member.GetCustomAttribute<PungentHelpHideAttribute>(true) != null;
        }
    }
#endif
}
