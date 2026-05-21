using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;

namespace PungentFunk.Utilities.RichDocuments
{
    public enum PungentRichDocumentBlockType
    {
        Paragraph = 0,
        Heading = 10,
        BulletList = 20,
        Checklist = 30,
        Quote = 40,
        Code = 50,
        Divider = 60,
        TokenReference = 70,
        LinkReference = 80,
        SpeakerLine = 90,
        DialogueChoice = 100,
        QuestObjective = 110,
        TutorialStep = 120,
        CommandPlaceholder = 130,
        VariablePlaceholder = 140
    }

    public enum PungentRichDocumentBlockConvention
    {
        None = 0,
        SpeakerLine = 10,
        DialogueChoice = 20,
        CommandPlaceholder = 30,
        VariableTokenPlaceholder = 40,
        QuestObjective = 50,
        TutorialStep = 60
    }

    public enum PungentRichDocumentSemanticKind
    {
        None = 0,
        Token = 10,
        DialogueLine = 100,
        DialogueChoice = 110,
        QuestObjective = 120,
        TutorialStep = 130,
        GameCopy = 140,
        Command = 150,
        HintCopy = 160,
        ItemCopy = 170,
        CharacterCopy = 180,
        CustomChip = 900,
        CustomInsertion = 910
    }

    public enum PungentRichDocumentInsertionRenderMode
    {
        InlineChip = 0,
        SlimBlock = 10,
        HelpBox = 20
    }

    public enum PungentRichDocumentInsertionLayoutMode
    {
        Vertical = 0,
        HorizontalWrap = 10,
        InlineRow = 20
    }

    public enum PungentRichDocumentInsertionFieldKind
    {
        Text = 0,
        LongText = 10,
        Number = 20,
        Boolean = 30,
        EnumText = 40,
        TargetReference = 50
    }

    [Serializable]
    public sealed class PungentRichDocumentInsertionFieldDefinition
    {
        public string key = string.Empty;
        public string displayName = "Field";
        public PungentRichDocumentInsertionFieldKind fieldKind = PungentRichDocumentInsertionFieldKind.Text;
        public string defaultValue = string.Empty;
        public string placeholder = string.Empty;
        public List<string> enumOptions = new List<string>();
        public bool required;
        public bool multiline;

        public void NormalizeInPlace()
        {
            key = PungentAuthoringId.Normalize(key);
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Field" : displayName.Trim();
            if (!Enum.IsDefined(typeof(PungentRichDocumentInsertionFieldKind), fieldKind))
                fieldKind = PungentRichDocumentInsertionFieldKind.Text;
            defaultValue = defaultValue == null ? string.Empty : defaultValue.Trim();
            placeholder = placeholder == null ? string.Empty : placeholder.Trim();
            enumOptions = PungentAuthoringMetadata.NormalizeTags(enumOptions);
        }

        public static PungentRichDocumentInsertionFieldDefinition Create(string key, string displayName, PungentRichDocumentInsertionFieldKind kind = PungentRichDocumentInsertionFieldKind.Text, string defaultValue = null)
        {
            PungentRichDocumentInsertionFieldDefinition definition = new PungentRichDocumentInsertionFieldDefinition
            {
                key = key ?? string.Empty,
                displayName = displayName ?? string.Empty,
                fieldKind = kind,
                defaultValue = defaultValue ?? string.Empty,
                multiline = kind == PungentRichDocumentInsertionFieldKind.LongText
            };
            definition.NormalizeInPlace();
            return definition;
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentInsertionRepeatableElementDefinition
    {
        public string key = string.Empty;
        public string displayName = "Item";
        public string addButtonLabel = "Add Item";
        public List<PungentRichDocumentInsertionFieldDefinition> fields = new List<PungentRichDocumentInsertionFieldDefinition>();

        public void NormalizeInPlace()
        {
            key = PungentAuthoringId.Normalize(key);
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Item" : displayName.Trim();
            addButtonLabel = string.IsNullOrWhiteSpace(addButtonLabel) ? "Add " + displayName : addButtonLabel.Trim();
            if (fields == null)
                fields = new List<PungentRichDocumentInsertionFieldDefinition>();
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                if (fields[i] == null)
                {
                    fields.RemoveAt(i);
                    continue;
                }

                fields[i].NormalizeInPlace();
                if (string.IsNullOrWhiteSpace(fields[i].key))
                    fields.RemoveAt(i);
            }
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentInsertionDefinition
    {
        public string id = string.Empty;
        public string displayName = "Custom Insertion";
        public string category = "Custom";
        public string syntaxAlias = string.Empty;
        public PungentRichDocumentInsertionRenderMode renderMode = PungentRichDocumentInsertionRenderMode.SlimBlock;
        public PungentRichDocumentInsertionLayoutMode layoutMode = PungentRichDocumentInsertionLayoutMode.Vertical;
        public string tintHex = "6FA8DC";
        public string defaultText = string.Empty;
        public string validationHint = string.Empty;
        public string defaultBindingKind = string.Empty;
        public bool builtIn;
        public bool archived;
        public List<PungentRichDocumentInsertionFieldDefinition> fields = new List<PungentRichDocumentInsertionFieldDefinition>();
        public List<PungentRichDocumentInsertionRepeatableElementDefinition> repeatableElements = new List<PungentRichDocumentInsertionRepeatableElementDefinition>();

        public bool IsChip => renderMode == PungentRichDocumentInsertionRenderMode.InlineChip;
        public bool HasRepeatableElements => repeatableElements != null && repeatableElements.Any(item => item != null);

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();

            displayName = string.IsNullOrWhiteSpace(displayName) ? "Custom Insertion" : displayName.Trim();
            category = string.IsNullOrWhiteSpace(category) ? "Custom" : category.Trim();
            syntaxAlias = string.IsNullOrWhiteSpace(syntaxAlias) ? displayName : syntaxAlias.Trim();
            if (!Enum.IsDefined(typeof(PungentRichDocumentInsertionRenderMode), renderMode))
                renderMode = PungentRichDocumentInsertionRenderMode.SlimBlock;
            if (!Enum.IsDefined(typeof(PungentRichDocumentInsertionLayoutMode), layoutMode))
                layoutMode = PungentRichDocumentInsertionLayoutMode.Vertical;
            tintHex = NormalizeHex(tintHex);
            defaultText = defaultText == null ? string.Empty : defaultText.Trim();
            validationHint = validationHint == null ? string.Empty : validationHint.Trim();
            defaultBindingKind = defaultBindingKind == null ? string.Empty : defaultBindingKind.Trim();

            if (fields == null)
                fields = new List<PungentRichDocumentInsertionFieldDefinition>();
            if (repeatableElements == null)
                repeatableElements = new List<PungentRichDocumentInsertionRepeatableElementDefinition>();

            for (int i = fields.Count - 1; i >= 0; i--)
            {
                if (fields[i] == null)
                {
                    fields.RemoveAt(i);
                    continue;
                }

                fields[i].NormalizeInPlace();
                if (string.IsNullOrWhiteSpace(fields[i].key))
                    fields.RemoveAt(i);
            }

            for (int i = repeatableElements.Count - 1; i >= 0; i--)
            {
                if (repeatableElements[i] == null)
                {
                    repeatableElements.RemoveAt(i);
                    continue;
                }

                repeatableElements[i].NormalizeInPlace();
                if (string.IsNullOrWhiteSpace(repeatableElements[i].key))
                    repeatableElements.RemoveAt(i);
            }
        }

        public static PungentRichDocumentInsertionDefinition CreateChip(string label)
        {
            PungentRichDocumentInsertionDefinition definition = new PungentRichDocumentInsertionDefinition
            {
                id = "chip-" + PungentAuthoringId.Normalize(label),
                displayName = string.IsNullOrWhiteSpace(label) ? "Custom Chip" : label.Trim(),
                category = "Custom",
                syntaxAlias = string.IsNullOrWhiteSpace(label) ? "Custom Chip" : label.Trim(),
                renderMode = PungentRichDocumentInsertionRenderMode.InlineChip,
                tintHex = "8E7CC3"
            };
            definition.NormalizeInPlace();
            return definition;
        }

        public static string NormalizeHex(string value)
        {
            string clean = (value ?? string.Empty).Trim().TrimStart('#');
            if (clean.Length != 6 || clean.Any(ch => !Uri.IsHexDigit(ch)))
                return "6FA8DC";
            return clean.ToUpperInvariant();
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentInsertionDefinitionDatabase
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string lastSavedUtc = string.Empty;
        public List<PungentRichDocumentInsertionDefinition> definitions = new List<PungentRichDocumentInsertionDefinition>();

        public void EnsureDefaults()
        {
            schemaVersion = Math.Max(CurrentSchemaVersion, schemaVersion);
            if (definitions == null)
                definitions = new List<PungentRichDocumentInsertionDefinition>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = definitions.Count - 1; i >= 0; i--)
            {
                if (definitions[i] == null)
                {
                    definitions.RemoveAt(i);
                    continue;
                }

                definitions[i].NormalizeInPlace();
                if (!seen.Add(definitions[i].id))
                    definitions.RemoveAt(i);
            }
        }

        public PungentRichDocumentInsertionDefinition Find(string idOrAlias)
        {
            EnsureDefaults();
            string clean = PungentAuthoringId.Normalize(idOrAlias);
            string alias = (idOrAlias ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clean) && string.IsNullOrWhiteSpace(alias))
                return null;

            return definitions.FirstOrDefault(definition =>
                definition != null &&
                !definition.archived &&
                (PungentAuthoringId.EqualsId(definition.id, clean) ||
                 string.Equals(definition.syntaxAlias, alias, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(definition.displayName, alias, StringComparison.OrdinalIgnoreCase)));
        }

        public void AddOrUpdate(PungentRichDocumentInsertionDefinition definition)
        {
            if (definition == null)
                return;

            EnsureDefaults();
            definition.NormalizeInPlace();
            int index = definitions.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, definition.id));
            if (index >= 0)
                definitions[index] = definition;
            else
                definitions.Add(definition);
        }
    }

    public enum PungentRichDocumentBindingExpressionKind
    {
        Unknown = 0,
        Reference = 10,
        CustomDefinition = 20,
        CustomReference = 30
    }

    [Serializable]
    public sealed class PungentRichDocumentBindingExpression
    {
        public string id = string.Empty;
        public string rawText = string.Empty;
        public PungentRichDocumentBindingExpressionKind kind = PungentRichDocumentBindingExpressionKind.Unknown;
        public string keyword = string.Empty;
        public string targetName = string.Empty;
        public List<string> pathSegments = new List<string>();
        public string customId = string.Empty;
        public string displayText = string.Empty;
        public int sourceIndex = -1;
        public int sourceLength;
        public PungentAuthoringBindingSlot bindingSlot = new PungentAuthoringBindingSlot();
        public PungentAuthoringBindingPath bindingPath = new PungentAuthoringBindingPath();

        public bool IsReference => kind == PungentRichDocumentBindingExpressionKind.Reference;
        public bool IsCustom => kind == PungentRichDocumentBindingExpressionKind.CustomDefinition ||
                                kind == PungentRichDocumentBindingExpressionKind.CustomReference;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.Normalize(id);
            if (string.IsNullOrWhiteSpace(id))
                id = PungentAuthoringId.NewValue();
            rawText = rawText == null ? string.Empty : rawText.Trim();
            keyword = keyword == null ? string.Empty : keyword.Trim();
            targetName = targetName == null ? string.Empty : targetName.Trim();
            customId = PungentAuthoringId.Normalize(customId);
            displayText = displayText == null ? string.Empty : displayText.Trim();
            sourceIndex = Math.Max(-1, sourceIndex);
            sourceLength = Math.Max(0, sourceLength);
            pathSegments = PungentAuthoringMetadata.NormalizeTags(pathSegments);
            bindingSlot = bindingSlot ?? new PungentAuthoringBindingSlot();
            if (bindingSlot.role == PungentAuthoringBindingSlotRole.Unknown)
                bindingSlot.role = kind == PungentRichDocumentBindingExpressionKind.CustomDefinition
                    ? PungentAuthoringBindingSlotRole.RichDocumentInsertionField
                    : PungentAuthoringBindingSlotRole.RichDocumentToken;
            bindingSlot.NormalizeInPlace();
            bindingPath = bindingPath ?? new PungentAuthoringBindingPath();
            bindingPath.NormalizeInPlace();
            if (string.IsNullOrWhiteSpace(bindingSlot.pathId))
                bindingSlot.pathId = bindingPath.id;
            bindingSlot.NormalizeInPlace();
        }
    }

    public static class PungentRichDocumentBindingExpressionParser
    {
        public static List<PungentRichDocumentBindingExpression> ExtractExpressions(string text, bool includeCustomReferences = false)
        {
            List<PungentRichDocumentBindingExpression> expressions = new List<PungentRichDocumentBindingExpression>();
            string source = text ?? string.Empty;
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int open = source.IndexOf('{', searchIndex);
                if (open < 0)
                    break;
                int close = source.IndexOf('}', open + 1);
                if (close < 0)
                    break;

                string raw = source.Substring(open, close - open + 1);
                PungentRichDocumentBindingExpression expression;
                if (TryParseToken(raw, out expression) &&
                    (includeCustomReferences || expression.kind != PungentRichDocumentBindingExpressionKind.CustomReference))
                {
                    expression.sourceIndex = open;
                    expression.sourceLength = raw.Length;
                    expression.NormalizeInPlace();
                    expressions.Add(expression);
                }

                searchIndex = close + 1;
            }

            return expressions;
        }

        public static bool TryParseToken(string tokenText, out PungentRichDocumentBindingExpression expression)
        {
            expression = null;
            string body = NormalizeBody(tokenText);
            if (string.IsNullOrWhiteSpace(body))
                return false;

            if (body.StartsWith("ref ", StringComparison.OrdinalIgnoreCase))
            {
                string payload = body.Substring(4).Trim();
                if (string.IsNullOrWhiteSpace(payload))
                    return false;

                string[] parts = SplitPath(payload);
                expression = new PungentRichDocumentBindingExpression
                {
                    id = StableExpressionId(body),
                    rawText = tokenText ?? string.Empty,
                    kind = PungentRichDocumentBindingExpressionKind.Reference,
                    keyword = "ref",
                    targetName = parts.Length > 0 ? parts[0] : payload
                };
                for (int i = 1; i < parts.Length; i++)
                    expression.pathSegments.Add(parts[i]);
                HydrateSharedBindingModels(expression);
                expression.NormalizeInPlace();
                return true;
            }

            if (body.StartsWith("custom ", StringComparison.OrdinalIgnoreCase))
            {
                string payload = body.Substring(7).Trim();
                if (string.IsNullOrWhiteSpace(payload))
                    return false;

                int colon = payload.IndexOf(':');
                string id = colon < 0 ? payload : payload.Substring(0, colon);
                string display = colon < 0 ? string.Empty : payload.Substring(colon + 1);
                expression = new PungentRichDocumentBindingExpression
                {
                    id = StableExpressionId(body),
                    rawText = tokenText ?? string.Empty,
                    kind = PungentRichDocumentBindingExpressionKind.CustomDefinition,
                    keyword = "custom",
                    customId = id,
                    displayText = display
                };
                HydrateSharedBindingModels(expression);
                expression.NormalizeInPlace();
                return true;
            }

            if (IsSimpleTokenId(body))
            {
                expression = new PungentRichDocumentBindingExpression
                {
                    id = StableExpressionId(body),
                    rawText = tokenText ?? string.Empty,
                    kind = PungentRichDocumentBindingExpressionKind.CustomReference,
                    keyword = "custom-ref",
                    customId = body
                };
                HydrateSharedBindingModels(expression);
                expression.NormalizeInPlace();
                return true;
            }

            return false;
        }

        public static string ResolveCustomText(string tokenId, PungentRichDocumentInsertionDefinitionDatabase database)
        {
            if (database == null || string.IsNullOrWhiteSpace(tokenId))
                return string.Empty;

            PungentRichDocumentInsertionDefinition definition = database.Find(tokenId);
            if (definition == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(definition.defaultText))
                return definition.defaultText;
            return definition.displayName ?? string.Empty;
        }

        public static PungentRichDocumentInsertionDefinition CreateInsertionDefinition(PungentRichDocumentBindingExpression expression)
        {
            if (expression == null || expression.kind != PungentRichDocumentBindingExpressionKind.CustomDefinition)
                return null;

            string label = string.IsNullOrWhiteSpace(expression.displayText) ? expression.customId : expression.displayText;
            PungentRichDocumentInsertionDefinition definition = PungentRichDocumentInsertionDefinition.CreateChip(label);
            definition.id = PungentAuthoringId.Normalize(expression.customId);
            definition.syntaxAlias = expression.customId;
            definition.defaultText = label;
            definition.defaultBindingKind = "inline-token";
            definition.NormalizeInPlace();
            return definition;
        }

        public static bool AddOrUpdateCustomDefinition(PungentRichDocumentInsertionDefinitionDatabase database, PungentRichDocumentBindingExpression expression)
        {
            if (database == null)
                return false;

            PungentRichDocumentInsertionDefinition definition = CreateInsertionDefinition(expression);
            if (definition == null)
                return false;

            database.AddOrUpdate(definition);
            return true;
        }

        private static void HydrateSharedBindingModels(PungentRichDocumentBindingExpression expression)
        {
            if (expression == null)
                return;

            string display = DisplayNameFor(expression);
            expression.bindingPath = new PungentAuthoringBindingPath
            {
                id = StableExpressionId("path:" + expression.rawText),
                displayName = display,
                rootLabel = expression.IsReference ? expression.targetName : expression.customId,
                valueType = expression.IsReference ? PungentAuthoringBindingValueType.Unknown : PungentAuthoringBindingValueType.Text,
                propertyPath = expression.IsReference ? string.Join(".", expression.pathSegments.ToArray()) : expression.customId,
                notes = "Parsed from rich document token expression."
            };

            expression.bindingPath.segments.Add(new PungentAuthoringBindingPathSegment
            {
                kind = PungentAuthoringBindingPathSegmentKind.Token,
                key = expression.IsReference ? expression.targetName : expression.customId,
                displayName = display,
                valueType = expression.bindingPath.valueType,
                order = 0
            });

            for (int i = 0; i < expression.pathSegments.Count; i++)
            {
                expression.bindingPath.segments.Add(new PungentAuthoringBindingPathSegment
                {
                    kind = i == expression.pathSegments.Count - 1
                        ? PungentAuthoringBindingPathSegmentKind.Field
                        : PungentAuthoringBindingPathSegmentKind.Component,
                    key = expression.pathSegments[i],
                    displayName = expression.pathSegments[i],
                    order = i + 1
                });
            }

            expression.bindingSlot = new PungentAuthoringBindingSlot
            {
                id = StableExpressionId("slot:" + expression.rawText),
                role = expression.kind == PungentRichDocumentBindingExpressionKind.CustomDefinition
                    ? PungentAuthoringBindingSlotRole.RichDocumentInsertionField
                    : PungentAuthoringBindingSlotRole.RichDocumentToken,
                interfaceId = "rich-documents",
                elementId = expression.id,
                fieldKey = expression.keyword,
                displayName = display,
                valueType = expression.bindingPath.valueType,
                pathId = expression.bindingPath.id
            };
        }

        private static string DisplayNameFor(PungentRichDocumentBindingExpression expression)
        {
            if (expression == null)
                return string.Empty;
            if (expression.kind == PungentRichDocumentBindingExpressionKind.Reference)
                return expression.targetName + (expression.pathSegments.Count == 0 ? string.Empty : ":" + string.Join(":", expression.pathSegments.ToArray()));
            if (!string.IsNullOrWhiteSpace(expression.displayText))
                return expression.displayText;
            return expression.customId;
        }

        private static string NormalizeBody(string tokenText)
        {
            return (tokenText ?? string.Empty).Trim().Trim('{', '}').Trim();
        }

        private static string[] SplitPath(string payload)
        {
            return (payload ?? string.Empty)
                .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();
        }

        private static bool IsSimpleTokenId(string value)
        {
            string clean = value == null ? string.Empty : value.Trim();
            if (string.IsNullOrWhiteSpace(clean) || clean.IndexOf(' ') >= 0 || clean.IndexOf(':') >= 0)
                return false;

            for (int i = 0; i < clean.Length; i++)
            {
                char ch = clean[i];
                if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-' && ch != '.')
                    return false;
            }

            return true;
        }

        private static string StableExpressionId(string source)
        {
            unchecked
            {
                string value = source ?? string.Empty;
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }

                return "expr-" + hash.ToString("x8");
            }
        }
    }

    public enum PungentRichDocumentAnnotationKind
    {
        Comment = 0,
        Bookmark = 10
    }

    [Serializable]
    public sealed class PungentRichDocumentAnnotation
    {
        public string id = Guid.NewGuid().ToString("N");
        public PungentRichDocumentAnnotationKind kind = PungentRichDocumentAnnotationKind.Comment;
        public string title = string.Empty;
        public string body = string.Empty;
        public int sourceSegmentIndex = -1;
        public int sourceLine;
        public string sourceText = string.Empty;
        public string sourceFingerprint = string.Empty;
        public string createdUtc = DateTime.UtcNow.ToString("o");
        public string updatedUtc = DateTime.UtcNow.ToString("o");
        public bool resolved;
        public bool archived;

        public bool IsStaleForSource(string currentSource)
        {
            if (string.IsNullOrWhiteSpace(sourceFingerprint))
                return false;

            return !string.Equals(sourceFingerprint, PungentRichDocumentValidationSuppression.ComputeFingerprint(currentSource), StringComparison.OrdinalIgnoreCase);
        }

        public void Touch()
        {
            updatedUtc = DateTime.UtcNow.ToString("o");
        }

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : Guid.NewGuid().ToString("N");
            if (!Enum.IsDefined(typeof(PungentRichDocumentAnnotationKind), kind))
                kind = PungentRichDocumentAnnotationKind.Comment;
            title = title == null ? string.Empty : title.Trim();
            body = body == null ? string.Empty : body.Trim();
            sourceSegmentIndex = Math.Max(-1, sourceSegmentIndex);
            sourceLine = Math.Max(0, sourceLine);
            sourceText = sourceText == null ? string.Empty : sourceText.Trim();
            sourceFingerprint = string.IsNullOrWhiteSpace(sourceFingerprint)
                ? PungentRichDocumentValidationSuppression.ComputeFingerprint(sourceText)
                : sourceFingerprint.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? DateTime.UtcNow.ToString("o") : createdUtc.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? createdUtc : updatedUtc.Trim();
        }

        public static PungentRichDocumentAnnotation Create(PungentRichDocumentAnnotationKind kind, string sourceText, int sourceSegmentIndex, int sourceLine, string title = null)
        {
            string now = DateTime.UtcNow.ToString("o");
            PungentRichDocumentAnnotation annotation = new PungentRichDocumentAnnotation
            {
                id = Guid.NewGuid().ToString("N"),
                kind = kind,
                title = string.IsNullOrWhiteSpace(title) ? DefaultTitle(kind) : title.Trim(),
                body = string.Empty,
                sourceText = sourceText ?? string.Empty,
                sourceFingerprint = PungentRichDocumentValidationSuppression.ComputeFingerprint(sourceText),
                sourceSegmentIndex = sourceSegmentIndex,
                sourceLine = Math.Max(0, sourceLine),
                createdUtc = now,
                updatedUtc = now
            };
            annotation.NormalizeInPlace();
            return annotation;
        }

        private static string DefaultTitle(PungentRichDocumentAnnotationKind kind)
        {
            return kind == PungentRichDocumentAnnotationKind.Bookmark ? "Bookmark" : "Comment";
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentSemanticField
    {
        public string key = string.Empty;
        public string value = string.Empty;

        public void NormalizeInPlace()
        {
            key = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
            value = value == null ? string.Empty : value.Trim();
        }

        public static PungentRichDocumentSemanticField Create(string key, string value)
        {
            PungentRichDocumentSemanticField field = new PungentRichDocumentSemanticField
            {
                key = key ?? string.Empty,
                value = value ?? string.Empty
            };
            field.NormalizeInPlace();
            return field;
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentSemanticBinding
    {
        public string id = Guid.NewGuid().ToString("N");
        public PungentRichDocumentSemanticKind kind = PungentRichDocumentSemanticKind.None;
        public string label = string.Empty;
        public string sourceText = string.Empty;
        public string sourceFingerprint = string.Empty;
        public List<PungentRichDocumentSemanticField> fields = new List<PungentRichDocumentSemanticField>();
        public PungentAuthoringTarget target;
        public PungentAuthoringReference reference;
        public string bindingLinkId = string.Empty;
        public string bindingAdapterId = string.Empty;
        public string bindingEndpointId = string.Empty;
        public PungentAuthoringBindingValueType bindingValueType = PungentAuthoringBindingValueType.Unknown;
        public PungentAuthoringBindingApplyMode applyMode = PungentAuthoringBindingApplyMode.ManualApply;
        public string bindingExpression = string.Empty;
        public PungentAuthoringBindingPath bindingPath = new PungentAuthoringBindingPath();
        public PungentAuthoringBindingSlot bindingSlot = new PungentAuthoringBindingSlot();
        public string runtimeAdapterId = string.Empty;
        public bool runtimeBindingEnabled;
        public bool archived;

        public bool HasTarget => target != null && target.HasTarget;

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : Guid.NewGuid().ToString("N");
            if (!Enum.IsDefined(typeof(PungentRichDocumentSemanticKind), kind))
                kind = PungentRichDocumentSemanticKind.None;
            label = label == null ? string.Empty : label.Trim();
            sourceText = sourceText == null ? string.Empty : sourceText.Trim();
            sourceFingerprint = string.IsNullOrWhiteSpace(sourceFingerprint)
                ? PungentRichDocumentValidationSuppression.ComputeFingerprint(sourceText)
                : sourceFingerprint.Trim();

            if (fields == null)
                fields = new List<PungentRichDocumentSemanticField>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                if (fields[i] == null)
                {
                    fields.RemoveAt(i);
                    continue;
                }

                fields[i].NormalizeInPlace();
                if (string.IsNullOrWhiteSpace(fields[i].key) || !seen.Add(fields[i].key))
                    fields.RemoveAt(i);
            }

            target?.NormalizeInPlace();
            reference?.NormalizeInPlace();
            bindingLinkId = PungentAuthoringId.Normalize(bindingLinkId);
            bindingAdapterId = bindingAdapterId == null ? string.Empty : bindingAdapterId.Trim();
            bindingEndpointId = bindingEndpointId == null ? string.Empty : bindingEndpointId.Trim();
            bindingExpression = bindingExpression == null ? string.Empty : bindingExpression.Trim();
            bindingPath = bindingPath ?? new PungentAuthoringBindingPath();
            bindingPath.NormalizeInPlace();
            bindingSlot = bindingSlot ?? new PungentAuthoringBindingSlot();
            if (bindingSlot.role == PungentAuthoringBindingSlotRole.Unknown)
                bindingSlot.role = PungentAuthoringBindingSlotRole.RichDocumentSemanticSpan;
            if (string.IsNullOrWhiteSpace(bindingSlot.elementId))
                bindingSlot.elementId = id;
            if (string.IsNullOrWhiteSpace(bindingSlot.fieldKey))
                bindingSlot.fieldKey = kind.ToString();
            if (bindingSlot.valueType == PungentAuthoringBindingValueType.Unknown)
                bindingSlot.valueType = bindingValueType;
            if (string.IsNullOrWhiteSpace(bindingSlot.pathId))
                bindingSlot.pathId = bindingPath.id;
            bindingSlot.NormalizeInPlace();
            runtimeAdapterId = runtimeAdapterId == null ? string.Empty : runtimeAdapterId.Trim();
        }

        public string GetField(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || fields == null)
                return string.Empty;

            PungentRichDocumentSemanticField field = fields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return field == null ? string.Empty : field.value ?? string.Empty;
        }

        public void SetField(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (fields == null)
                fields = new List<PungentRichDocumentSemanticField>();

            string cleanKey = key.Trim();
            PungentRichDocumentSemanticField field = fields.FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.key, cleanKey, StringComparison.OrdinalIgnoreCase));
            if (field == null)
            {
                fields.Add(PungentRichDocumentSemanticField.Create(cleanKey, value));
                return;
            }

            field.value = value ?? string.Empty;
            field.NormalizeInPlace();
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentBlockItem
    {
        public string text = string.Empty;
        public bool isChecked;
        public string metadata = string.Empty;

        public void NormalizeInPlace()
        {
            text = text == null ? string.Empty : text.Trim();
            metadata = metadata == null ? string.Empty : metadata.Trim();
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentBlock
    {
        public string id = Guid.NewGuid().ToString("N");
        public PungentRichDocumentBlockType type = PungentRichDocumentBlockType.Paragraph;
        public PungentRichDocumentBlockConvention convention = PungentRichDocumentBlockConvention.None;
        public string text = string.Empty;
        public string speaker = string.Empty;
        public int headingLevel = 2;
        public List<PungentRichDocumentBlockItem> items = new List<PungentRichDocumentBlockItem>();
        public string tokenKey = string.Empty;
        public PungentAuthoringReference reference;
        public PungentAuthoringTarget target;
        public string language = string.Empty;
        public bool collapsed;

        public bool HasReadableContent =>
            !string.IsNullOrWhiteSpace(text) ||
            !string.IsNullOrWhiteSpace(speaker) ||
            !string.IsNullOrWhiteSpace(tokenKey) ||
            (items != null && items.Any(item => item != null && !string.IsNullOrWhiteSpace(item.text))) ||
            (reference != null && reference.HasItemId) ||
            (target != null && target.HasTarget);

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : Guid.NewGuid().ToString("N");
            text = text == null ? string.Empty : text.TrimEnd();
            tokenKey = tokenKey == null ? string.Empty : tokenKey.Trim().Trim('{', '}').Trim();
            language = language == null ? string.Empty : language.Trim();
            headingLevel = Math.Max(1, Math.Min(6, headingLevel));

            if (!Enum.IsDefined(typeof(PungentRichDocumentBlockType), type))
                type = PungentRichDocumentBlockType.Paragraph;
            if (!Enum.IsDefined(typeof(PungentRichDocumentBlockConvention), convention))
                convention = PungentRichDocumentBlockConvention.None;
            if (type == PungentRichDocumentBlockType.Paragraph && convention != PungentRichDocumentBlockConvention.None)
                type = TypeForConvention(convention);
            if (convention == PungentRichDocumentBlockConvention.None)
                convention = ConventionForType(type);

            if (items == null)
                items = new List<PungentRichDocumentBlockItem>();
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == null)
                {
                    items.RemoveAt(i);
                    continue;
                }

                items[i].NormalizeInPlace();
            }

            reference?.NormalizeInPlace();
            target?.NormalizeInPlace();
            speaker = speaker == null ? string.Empty : speaker.Trim();
        }

        public static PungentRichDocumentBlock Heading(string text, int level = 2)
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.Heading,
                text = text ?? string.Empty,
                headingLevel = level
            };
        }

        public static PungentRichDocumentBlock Paragraph(string text, PungentRichDocumentBlockConvention convention = PungentRichDocumentBlockConvention.None)
        {
            return new PungentRichDocumentBlock
            {
                type = TypeForConvention(convention),
                text = text ?? string.Empty,
                convention = convention
            };
        }

        public static PungentRichDocumentBlock List(PungentRichDocumentBlockType type, IEnumerable<string> items)
        {
            return new PungentRichDocumentBlock
            {
                type = type == PungentRichDocumentBlockType.Checklist ? PungentRichDocumentBlockType.Checklist : PungentRichDocumentBlockType.BulletList,
                items = (items ?? Enumerable.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => new PungentRichDocumentBlockItem { text = item.Trim() })
                    .ToList()
            };
        }

        public static PungentRichDocumentBlock Checklist(IEnumerable<string> items)
        {
            return List(PungentRichDocumentBlockType.Checklist, items);
        }

        public static PungentRichDocumentBlock Quote(string text)
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.Quote,
                text = text ?? string.Empty
            };
        }

        public static PungentRichDocumentBlock Code(string text, string language = null)
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.Code,
                text = text ?? string.Empty,
                language = language ?? string.Empty
            };
        }

        public static PungentRichDocumentBlock Divider()
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.Divider
            };
        }

        public static PungentRichDocumentBlock Token(string tokenKey, PungentRichDocumentBlockConvention convention = PungentRichDocumentBlockConvention.VariableTokenPlaceholder)
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.TokenReference,
                tokenKey = tokenKey ?? string.Empty,
                convention = convention
            };
        }

        public static PungentRichDocumentBlock Link(string label, PungentAuthoringReference reference = null, PungentAuthoringTarget target = null)
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.LinkReference,
                text = label ?? string.Empty,
                reference = reference,
                target = target
            };
        }

        public static PungentRichDocumentBlock SpeakerLine(string speaker, string text)
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.SpeakerLine,
                convention = PungentRichDocumentBlockConvention.SpeakerLine,
                speaker = speaker ?? string.Empty,
                text = text ?? string.Empty
            };
        }

        public static PungentRichDocumentBlock DialogueChoice(string text)
        {
            return GameText(PungentRichDocumentBlockType.DialogueChoice, PungentRichDocumentBlockConvention.DialogueChoice, text);
        }

        public static PungentRichDocumentBlock QuestObjective(string text)
        {
            return GameText(PungentRichDocumentBlockType.QuestObjective, PungentRichDocumentBlockConvention.QuestObjective, text);
        }

        public static PungentRichDocumentBlock TutorialStep(string text)
        {
            return GameText(PungentRichDocumentBlockType.TutorialStep, PungentRichDocumentBlockConvention.TutorialStep, text);
        }

        public static PungentRichDocumentBlock CommandPlaceholder(string text)
        {
            return GameText(PungentRichDocumentBlockType.CommandPlaceholder, PungentRichDocumentBlockConvention.CommandPlaceholder, text);
        }

        public static PungentRichDocumentBlock VariablePlaceholder(string tokenKey)
        {
            return new PungentRichDocumentBlock
            {
                type = PungentRichDocumentBlockType.VariablePlaceholder,
                convention = PungentRichDocumentBlockConvention.VariableTokenPlaceholder,
                tokenKey = tokenKey ?? string.Empty,
                text = string.IsNullOrWhiteSpace(tokenKey) ? string.Empty : "{" + tokenKey.Trim().Trim('{', '}').Trim() + "}"
            };
        }

        public static PungentRichDocumentBlockType TypeForConvention(PungentRichDocumentBlockConvention convention)
        {
            switch (convention)
            {
                case PungentRichDocumentBlockConvention.SpeakerLine: return PungentRichDocumentBlockType.SpeakerLine;
                case PungentRichDocumentBlockConvention.DialogueChoice: return PungentRichDocumentBlockType.DialogueChoice;
                case PungentRichDocumentBlockConvention.CommandPlaceholder: return PungentRichDocumentBlockType.CommandPlaceholder;
                case PungentRichDocumentBlockConvention.VariableTokenPlaceholder: return PungentRichDocumentBlockType.VariablePlaceholder;
                case PungentRichDocumentBlockConvention.QuestObjective: return PungentRichDocumentBlockType.QuestObjective;
                case PungentRichDocumentBlockConvention.TutorialStep: return PungentRichDocumentBlockType.TutorialStep;
                default: return PungentRichDocumentBlockType.Paragraph;
            }
        }

        public static PungentRichDocumentBlockConvention ConventionForType(PungentRichDocumentBlockType type)
        {
            switch (type)
            {
                case PungentRichDocumentBlockType.SpeakerLine: return PungentRichDocumentBlockConvention.SpeakerLine;
                case PungentRichDocumentBlockType.DialogueChoice: return PungentRichDocumentBlockConvention.DialogueChoice;
                case PungentRichDocumentBlockType.CommandPlaceholder: return PungentRichDocumentBlockConvention.CommandPlaceholder;
                case PungentRichDocumentBlockType.VariablePlaceholder: return PungentRichDocumentBlockConvention.VariableTokenPlaceholder;
                case PungentRichDocumentBlockType.QuestObjective: return PungentRichDocumentBlockConvention.QuestObjective;
                case PungentRichDocumentBlockType.TutorialStep: return PungentRichDocumentBlockConvention.TutorialStep;
                default: return PungentRichDocumentBlockConvention.None;
            }
        }

        private static PungentRichDocumentBlock GameText(PungentRichDocumentBlockType type, PungentRichDocumentBlockConvention convention, string text)
        {
            return new PungentRichDocumentBlock
            {
                type = type,
                convention = convention,
                text = text ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class PungentRichDocumentValidationSuppression
    {
        public string issueCode = string.Empty;
        public string sourceFingerprint = string.Empty;
        public string note = string.Empty;
        public string createdUtc = DateTime.UtcNow.ToString("o");

        public bool Matches(string code, string source)
        {
            return string.Equals(NormalizeCode(issueCode), NormalizeCode(code), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(sourceFingerprint, ComputeFingerprint(source), StringComparison.OrdinalIgnoreCase);
        }

        public void NormalizeInPlace()
        {
            issueCode = NormalizeCode(issueCode);
            sourceFingerprint = sourceFingerprint == null ? string.Empty : sourceFingerprint.Trim();
            note = note == null ? string.Empty : note.Trim();
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? DateTime.UtcNow.ToString("o") : createdUtc.Trim();
        }

        public static PungentRichDocumentValidationSuppression Create(string code, string source, string note = null)
        {
            PungentRichDocumentValidationSuppression suppression = new PungentRichDocumentValidationSuppression
            {
                issueCode = NormalizeCode(code),
                sourceFingerprint = ComputeFingerprint(source),
                note = note ?? string.Empty,
                createdUtc = DateTime.UtcNow.ToString("o")
            };
            suppression.NormalizeInPlace();
            return suppression;
        }

        public static string ComputeFingerprint(string source)
        {
            unchecked
            {
                string text = (source ?? string.Empty).Trim();
                uint hash = 2166136261;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }

        private static string NormalizeCode(string code)
        {
            return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
        }
    }

    [Serializable]
    public sealed class PungentRichDocument
    {
        public const int CurrentVersion = 1;
        public const int CurrentMigrationVersion = 2;

        public string id = Guid.NewGuid().ToString("N");
        public string title = "Untitled Document";
        public string summary = string.Empty;
        public string kind = "General";
        public string templateId = "general-document";
        public string status = "Draft";
        public string priority = "Normal";
        public string visibility = "PrivateProject";
        public List<string> tags = new List<string>();
        public string createdUtc = DateTime.UtcNow.ToString("o");
        public string updatedUtc = DateTime.UtcNow.ToString("o");
        public bool archived;
        public bool developerOnly;
        public bool locked;
        public string generatedBy = string.Empty;
        public string generatedTemplateId = string.Empty;
        public string generatedUtc = string.Empty;
        public string sourceLegacyNoteId = string.Empty;
        public string bodyText = string.Empty;
        public List<PungentRichDocumentBlock> blocks = new List<PungentRichDocumentBlock>();
        public List<PungentAuthoringTarget> targets = new List<PungentAuthoringTarget>();
        public List<PungentAuthoringReference> references = new List<PungentAuthoringReference>();
        public List<PungentRichDocumentValidationSuppression> validationSuppressions = new List<PungentRichDocumentValidationSuppression>();
        public List<PungentRichDocumentSemanticBinding> semanticBindings = new List<PungentRichDocumentSemanticBinding>();
        public List<PungentAuthoringBindingSlot> bindingSlots = new List<PungentAuthoringBindingSlot>();
        public List<PungentRichDocumentAnnotation> annotations = new List<PungentRichDocumentAnnotation>();
        public int version = CurrentVersion;
        public int migrationVersion = CurrentMigrationVersion;

        public bool HasBody =>
            !string.IsNullOrWhiteSpace(bodyText) ||
            (blocks != null && blocks.Any(block => block != null && block.HasReadableContent));

        public PungentAuthoringReference ToReference(string providerId)
        {
            return PungentAuthoringReference.Create(PungentAuthoringItemKind.RichDocument, id, providerId, title);
        }

        public PungentAuthoringMetadata ToMetadata(string providerId)
        {
            PungentAuthoringMetadata metadata = new PungentAuthoringMetadata
            {
                id = id,
                title = string.IsNullOrWhiteSpace(title) ? "Untitled Document" : title,
                summary = string.IsNullOrWhiteSpace(summary) ? BuildPreview(bodyText) : summary,
                kind = PungentAuthoringItemKind.RichDocument,
                customKind = kind,
                status = status,
                priority = priority,
                visibility = visibility,
                tags = PungentAuthoringMetadata.NormalizeTags(tags),
                createdUtc = createdUtc,
                updatedUtc = updatedUtc,
                archived = archived,
                developerOnly = developerOnly,
                sourceProviderId = providerId,
                packageCapabilityId = PungentAuthoringPackageCapabilities.RichDocuments,
                extensionId = PungentAuthoringPackageCapabilities.RichDocuments
            };
            metadata.NormalizeInPlace();
            return metadata;
        }

        public void Touch()
        {
            updatedUtc = DateTime.UtcNow.ToString("o");
        }

        public void NormalizeInPlace()
        {
            id = PungentAuthoringId.IsValidId(id) ? PungentAuthoringId.Normalize(id) : Guid.NewGuid().ToString("N");
            title = string.IsNullOrWhiteSpace(title) ? "Untitled Document" : title.Trim();
            summary = summary == null ? string.Empty : summary.Trim();
            kind = string.IsNullOrWhiteSpace(kind) ? "General" : kind.Trim();
            templateId = string.IsNullOrWhiteSpace(templateId) ? "general-document" : templateId.Trim();
            status = string.IsNullOrWhiteSpace(status) ? "Draft" : status.Trim();
            priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority.Trim();
            visibility = string.IsNullOrWhiteSpace(visibility) ? "PrivateProject" : visibility.Trim();
            generatedBy = generatedBy == null ? string.Empty : generatedBy.Trim();
            generatedTemplateId = PungentAuthoringId.Normalize(generatedTemplateId);
            generatedUtc = string.IsNullOrWhiteSpace(generatedUtc) ? string.Empty : generatedUtc.Trim();
            sourceLegacyNoteId = PungentAuthoringId.Normalize(sourceLegacyNoteId);
            bodyText = bodyText ?? string.Empty;
            createdUtc = string.IsNullOrWhiteSpace(createdUtc) ? DateTime.UtcNow.ToString("o") : createdUtc.Trim();
            updatedUtc = string.IsNullOrWhiteSpace(updatedUtc) ? createdUtc : updatedUtc.Trim();
            version = Math.Max(1, version);
            migrationVersion = Math.Max(CurrentMigrationVersion, migrationVersion);
            tags = PungentAuthoringMetadata.NormalizeTags(tags);

            if (blocks == null)
                blocks = new List<PungentRichDocumentBlock>();
            HashSet<string> seenBlockIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                if (blocks[i] == null)
                {
                    blocks.RemoveAt(i);
                    continue;
                }

                blocks[i].NormalizeInPlace();
                if (!seenBlockIds.Add(blocks[i].id))
                {
                    blocks[i].id = Guid.NewGuid().ToString("N");
                    blocks[i].NormalizeInPlace();
                    seenBlockIds.Add(blocks[i].id);
                }
            }

            NormalizeTargets();
            NormalizeReferences();
            NormalizeValidationSuppressions();
            NormalizeSemanticBindings();
            NormalizeBindingSlots();
            NormalizeAnnotations();
        }

        public bool IsValidationSuppressed(string issueCode, string source)
        {
            if (validationSuppressions == null)
                return false;

            return validationSuppressions.Any(suppression => suppression != null && suppression.Matches(issueCode, source));
        }

        public void SuppressValidation(string issueCode, string source, string note = null)
        {
            if (validationSuppressions == null)
                validationSuppressions = new List<PungentRichDocumentValidationSuppression>();

            if (IsValidationSuppressed(issueCode, source))
                return;

            validationSuppressions.Add(PungentRichDocumentValidationSuppression.Create(issueCode, source, note));
        }

        public static PungentRichDocument Create(string title = null, string templateId = null)
        {
            string now = DateTime.UtcNow.ToString("o");
            PungentRichDocument document = new PungentRichDocument
            {
                id = Guid.NewGuid().ToString("N"),
                title = string.IsNullOrWhiteSpace(title) ? "Untitled Document" : title.Trim(),
                templateId = string.IsNullOrWhiteSpace(templateId) ? "general-document" : templateId.Trim(),
                createdUtc = now,
                updatedUtc = now,
                version = CurrentVersion,
                migrationVersion = CurrentMigrationVersion
            };
            document.NormalizeInPlace();
            return document;
        }

        public static string BuildPreview(string text, int maxLength = 240)
        {
            string clean = string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().Replace("\r", " ").Replace("\n", " ");
            return clean.Length <= maxLength ? clean : clean.Substring(0, Math.Max(0, maxLength - 3)).TrimEnd() + "...";
        }

        private void NormalizeTargets()
        {
            if (targets == null)
                targets = new List<PungentAuthoringTarget>();
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i] == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                targets[i].NormalizeInPlace();
            }
        }

        private void NormalizeReferences()
        {
            if (references == null)
                references = new List<PungentAuthoringReference>();
            for (int i = references.Count - 1; i >= 0; i--)
            {
                if (references[i] == null)
                {
                    references.RemoveAt(i);
                    continue;
                }

                references[i].NormalizeInPlace();
            }
        }

        private void NormalizeValidationSuppressions()
        {
            if (validationSuppressions == null)
                validationSuppressions = new List<PungentRichDocumentValidationSuppression>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = validationSuppressions.Count - 1; i >= 0; i--)
            {
                if (validationSuppressions[i] == null)
                {
                    validationSuppressions.RemoveAt(i);
                    continue;
                }

                validationSuppressions[i].NormalizeInPlace();
                string key = validationSuppressions[i].issueCode + ":" + validationSuppressions[i].sourceFingerprint;
                if (string.IsNullOrWhiteSpace(validationSuppressions[i].issueCode) ||
                    string.IsNullOrWhiteSpace(validationSuppressions[i].sourceFingerprint) ||
                    !seen.Add(key))
                {
                    validationSuppressions.RemoveAt(i);
                }
            }
        }

        private void NormalizeSemanticBindings()
        {
            if (semanticBindings == null)
                semanticBindings = new List<PungentRichDocumentSemanticBinding>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = semanticBindings.Count - 1; i >= 0; i--)
            {
                if (semanticBindings[i] == null)
                {
                    semanticBindings.RemoveAt(i);
                    continue;
                }

                semanticBindings[i].NormalizeInPlace();
                if (!seen.Add(semanticBindings[i].id))
                {
                    semanticBindings[i].id = Guid.NewGuid().ToString("N");
                    semanticBindings[i].NormalizeInPlace();
                    seen.Add(semanticBindings[i].id);
                }
            }
        }

        private void NormalizeBindingSlots()
        {
            if (bindingSlots == null)
                bindingSlots = new List<PungentAuthoringBindingSlot>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = bindingSlots.Count - 1; i >= 0; i--)
            {
                if (bindingSlots[i] == null)
                {
                    bindingSlots.RemoveAt(i);
                    continue;
                }

                if (bindingSlots[i].role == PungentAuthoringBindingSlotRole.Unknown)
                    bindingSlots[i].role = PungentAuthoringBindingSlotRole.RichDocumentToken;
                bindingSlots[i].NormalizeInPlace();
                if (!seen.Add(bindingSlots[i].id))
                    bindingSlots.RemoveAt(i);
            }
        }

        private void NormalizeAnnotations()
        {
            if (annotations == null)
                annotations = new List<PungentRichDocumentAnnotation>();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = annotations.Count - 1; i >= 0; i--)
            {
                if (annotations[i] == null)
                {
                    annotations.RemoveAt(i);
                    continue;
                }

                annotations[i].NormalizeInPlace();
                if (!seen.Add(annotations[i].id))
                {
                    annotations[i].id = Guid.NewGuid().ToString("N");
                    annotations[i].NormalizeInPlace();
                    seen.Add(annotations[i].id);
                }
            }
        }
    }
}
