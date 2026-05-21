using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public static class PungentRichDocumentInsertionDefinitionRegistry
    {
        public const string DefinitionFieldKey = "definition";
        public const string DefinitionLabelFieldKey = "definitionLabel";
        public const string RepeatablePrefix = "repeat.";

        private static readonly List<PungentRichDocumentInsertionDefinition> BuiltIns = CreateBuiltIns();

        public static IReadOnlyList<PungentRichDocumentInsertionDefinition> AllDefinitions
        {
            get
            {
                EnsureLoaded();
                return BuiltIns
                    .Concat(PungentRichDocumentInsertionDefinitionStorage.Database.definitions ?? new List<PungentRichDocumentInsertionDefinition>())
                    .Where(definition => definition != null && !definition.archived)
                    .GroupBy(definition => definition.id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Last())
                    .OrderBy(definition => definition.category, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(definition => definition.displayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public static IReadOnlyList<PungentRichDocumentInsertionDefinition> CustomDefinitions
        {
            get
            {
                EnsureLoaded();
                return (PungentRichDocumentInsertionDefinitionStorage.Database.definitions ?? new List<PungentRichDocumentInsertionDefinition>())
                    .Where(definition => definition != null && !definition.archived)
                    .OrderBy(definition => definition.category, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(definition => definition.displayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public static void EnsureLoaded()
        {
            PungentRichDocumentInsertionDefinitionStorage.EnsureLoaded();
            PungentRichDocumentInsertionDefinitionStorage.Database.EnsureDefaults();
        }

        public static PungentRichDocumentInsertionDefinition Find(string idOrAlias)
        {
            string alias = (idOrAlias ?? string.Empty).Trim();
            string clean = PungentAuthoringId.Normalize(alias);
            return AllDefinitions.FirstOrDefault(definition =>
                definition != null &&
                (PungentAuthoringId.EqualsId(definition.id, clean) ||
                 string.Equals(definition.syntaxAlias, alias, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(definition.displayName, alias, StringComparison.OrdinalIgnoreCase)));
        }

        public static PungentRichDocumentInsertionDefinition FindForChipLabel(string label)
        {
            PungentRichDocumentInsertionDefinition definition = Find(label);
            if (definition != null && definition.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip)
                return definition;

            return PungentRichDocumentInsertionDefinition.CreateChip(label);
        }

        public static void AddOrUpdate(PungentRichDocumentInsertionDefinition definition)
        {
            if (definition == null)
                return;

            EnsureLoaded();
            definition.builtIn = false;
            definition.NormalizeInPlace();
            PungentRichDocumentInsertionDefinitionStorage.Database.AddOrUpdate(definition);
        }

        public static bool Save(out string error)
        {
            return PungentRichDocumentInsertionDefinitionStorage.Save(out error);
        }

        public static List<PungentRichDocumentSemanticField> CreateDefaultFields(PungentRichDocumentInsertionDefinition definition)
        {
            List<PungentRichDocumentSemanticField> fields = new List<PungentRichDocumentSemanticField>();
            if (definition == null)
                return fields;

            definition.NormalizeInPlace();
            fields.Add(PungentRichDocumentSemanticField.Create(DefinitionFieldKey, definition.id));
            fields.Add(PungentRichDocumentSemanticField.Create(DefinitionLabelFieldKey, definition.displayName));
            fields.Add(PungentRichDocumentSemanticField.Create("category", definition.category));

            foreach (PungentRichDocumentInsertionFieldDefinition field in definition.fields ?? new List<PungentRichDocumentInsertionFieldDefinition>())
            {
                if (field == null || string.IsNullOrWhiteSpace(field.key))
                    continue;

                fields.Add(PungentRichDocumentSemanticField.Create(field.key, field.defaultValue));
            }

            foreach (PungentRichDocumentInsertionRepeatableElementDefinition repeatable in definition.repeatableElements ?? new List<PungentRichDocumentInsertionRepeatableElementDefinition>())
            {
                if (repeatable == null || string.IsNullOrWhiteSpace(repeatable.key))
                    continue;

                foreach (PungentRichDocumentInsertionFieldDefinition field in repeatable.fields ?? new List<PungentRichDocumentInsertionFieldDefinition>())
                {
                    if (field == null || string.IsNullOrWhiteSpace(field.key))
                        continue;

                    fields.Add(PungentRichDocumentSemanticField.Create(RepeatableFieldKey(repeatable.key, 0, field.key), field.defaultValue));
                }
            }

            return fields;
        }

        public static string RepeatableFieldKey(string repeatableKey, int index, string fieldKey)
        {
            return RepeatablePrefix + PungentAuthoringId.Normalize(repeatableKey) + "." + Math.Max(0, index).ToString() + "." + PungentAuthoringId.Normalize(fieldKey);
        }

        public static string GetDefinitionId(IEnumerable<PungentRichDocumentSemanticField> fields)
        {
            return FieldValue(fields, DefinitionFieldKey);
        }

        public static string FieldValue(IEnumerable<PungentRichDocumentSemanticField> fields, string key)
        {
            if (fields == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            PungentRichDocumentSemanticField field = fields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return field == null ? string.Empty : field.value ?? string.Empty;
        }

        public static Color TintForDefinition(PungentRichDocumentInsertionDefinition definition, Color fallback)
        {
            if (definition == null)
                return fallback;

            Color parsed;
            return ColorUtility.TryParseHtmlString("#" + PungentRichDocumentInsertionDefinition.NormalizeHex(definition.tintHex), out parsed)
                ? parsed
                : fallback;
        }

        public static string BuildCustomInsertionRaw(PungentRichDocumentInsertionDefinition definition, string text = null)
        {
            if (definition == null)
                return text ?? string.Empty;

            List<PungentRichDocumentSemanticField> fields = CreateDefaultFields(definition);
            string source = string.IsNullOrWhiteSpace(text) ? definition.defaultText : text;
            return PungentRichDocumentSemanticParser.CreateSemanticTag(PungentRichDocumentSemanticKind.CustomInsertion, source, fields);
        }

        private static List<PungentRichDocumentInsertionDefinition> CreateBuiltIns()
        {
            List<PungentRichDocumentInsertionDefinition> definitions = new List<PungentRichDocumentInsertionDefinition>
            {
                BuiltIn("dialogue-thread", "Dialogue Thread", "Dialogue", PungentRichDocumentInsertionRenderMode.SlimBlock, "6FA8DC", "NPC: Dialogue line.",
                    new[]
                    {
                        PungentRichDocumentInsertionFieldDefinition.Create("speaker", "Speaker", PungentRichDocumentInsertionFieldKind.Text, "NPC"),
                        PungentRichDocumentInsertionFieldDefinition.Create("line", "Line", PungentRichDocumentInsertionFieldKind.LongText, "Dialogue line.")
                    },
                    Repeatable("line", "Line", "Add Line",
                        PungentRichDocumentInsertionFieldDefinition.Create("speaker", "Speaker", PungentRichDocumentInsertionFieldKind.Text, "NPC"),
                        PungentRichDocumentInsertionFieldDefinition.Create("line", "Line", PungentRichDocumentInsertionFieldKind.LongText, "Dialogue line."))),
                BuiltIn("choice-group", "Choice Group", "Dialogue", PungentRichDocumentInsertionRenderMode.SlimBlock, "8E7CC3", "Choice text",
                    new[]
                    {
                        PungentRichDocumentInsertionFieldDefinition.Create("choiceKey", "Choice Key"),
                        PungentRichDocumentInsertionFieldDefinition.Create("choiceText", "Choice Text", PungentRichDocumentInsertionFieldKind.LongText, "Choice text")
                    },
                    Repeatable("choice", "Choice", "Add Choice",
                        PungentRichDocumentInsertionFieldDefinition.Create("choiceKey", "Choice Key"),
                        PungentRichDocumentInsertionFieldDefinition.Create("choiceText", "Choice Text", PungentRichDocumentInsertionFieldKind.LongText, "Choice text"))),
                BuiltIn("quest-objectives", "Quest Objectives", "Quest", PungentRichDocumentInsertionRenderMode.SlimBlock, "76A36D", "Objective text",
                    new[]
                    {
                        PungentRichDocumentInsertionFieldDefinition.Create("questId", "Quest ID"),
                        PungentRichDocumentInsertionFieldDefinition.Create("objectiveText", "Objective", PungentRichDocumentInsertionFieldKind.LongText, "Objective text")
                    },
                    Repeatable("objective", "Objective", "Add Objective",
                        PungentRichDocumentInsertionFieldDefinition.Create("objectiveId", "Objective ID"),
                        PungentRichDocumentInsertionFieldDefinition.Create("objectiveText", "Objective", PungentRichDocumentInsertionFieldKind.LongText, "Objective text"))),
                BuiltIn("tutorial-steps", "Tutorial Steps", "Tutorial", PungentRichDocumentInsertionRenderMode.SlimBlock, "D6A84F", "Tutorial instruction.",
                    new[]
                    {
                        PungentRichDocumentInsertionFieldDefinition.Create("tutorialId", "Tutorial ID"),
                        PungentRichDocumentInsertionFieldDefinition.Create("instruction", "Instruction", PungentRichDocumentInsertionFieldKind.LongText, "Tutorial instruction.")
                    },
                    Repeatable("step", "Step", "Add Step",
                        PungentRichDocumentInsertionFieldDefinition.Create("stepId", "Step ID"),
                        PungentRichDocumentInsertionFieldDefinition.Create("instruction", "Instruction", PungentRichDocumentInsertionFieldKind.LongText, "Tutorial instruction."))),
                BuiltIn("stage-direction-chip", "Stage Direction", "Script", PungentRichDocumentInsertionRenderMode.InlineChip, "8E7CC3", string.Empty, new PungentRichDocumentInsertionFieldDefinition[0], null)
            };

            foreach (PungentRichDocumentInsertionDefinition definition in definitions)
            {
                definition.builtIn = true;
                definition.NormalizeInPlace();
            }

            return definitions;
        }

        private static PungentRichDocumentInsertionDefinition BuiltIn(
            string id,
            string displayName,
            string category,
            PungentRichDocumentInsertionRenderMode renderMode,
            string tintHex,
            string defaultText,
            IEnumerable<PungentRichDocumentInsertionFieldDefinition> fields,
            PungentRichDocumentInsertionRepeatableElementDefinition repeatable)
        {
            PungentRichDocumentInsertionDefinition definition = new PungentRichDocumentInsertionDefinition
            {
                id = id,
                displayName = displayName,
                category = category,
                syntaxAlias = displayName,
                renderMode = renderMode,
                tintHex = tintHex,
                defaultText = defaultText,
                builtIn = true,
                fields = (fields ?? Enumerable.Empty<PungentRichDocumentInsertionFieldDefinition>()).ToList()
            };
            if (repeatable != null)
                definition.repeatableElements.Add(repeatable);
            definition.NormalizeInPlace();
            return definition;
        }

        private static PungentRichDocumentInsertionRepeatableElementDefinition Repeatable(string key, string label, string addLabel, params PungentRichDocumentInsertionFieldDefinition[] fields)
        {
            PungentRichDocumentInsertionRepeatableElementDefinition definition = new PungentRichDocumentInsertionRepeatableElementDefinition
            {
                key = key,
                displayName = label,
                addButtonLabel = addLabel,
                fields = (fields ?? new PungentRichDocumentInsertionFieldDefinition[0]).ToList()
            };
            definition.NormalizeInPlace();
            return definition;
        }
    }
#endif
}
