using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SkiGame.UI;
using UnityEngine;

public static class NpcDialogueTokenResolver
{
    public static string Resolve(string text, DialogueContext context, string speakerName, InputActionAsset inputActions)
    {
        return NpcDialogueTextFormatter.Format(text, context, speakerName, inputActions);
    }
}

public static class NpcDialogueTextFormatter
{
    private static readonly Regex InputTokenRegex = new(@"\{input:([^}]+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StyleTokenRegex = new(@"\{style:([^}]+)\}(.*?)\{/style\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ColorTokenRegex = new(@"\{color:([^}]+)\}(.*?)\{/color\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex SimpleWrapperRegex = new(@"\{(warning|accent|muted|success|quest|objective)\}(.*?)\{/\1\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex BoldRegex = new(@"\{b\}(.*?)\{/b\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ItalicRegex = new(@"\{i\}(.*?)\{/i\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex SmallRegex = new(@"\{small\}(.*?)\{/small\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex CapsRegex = new(@"\{caps\}(.*?)\{/caps\}", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ValueTokenRegex = new(@"\{([a-zA-Z][a-zA-Z0-9_.]*)\}", RegexOptions.Compiled);

    public static string Format(string text, DialogueContext context, string speakerName, InputActionAsset inputActions, NpcDialogueBubbleStyleSO style = null)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string resolved = text
            .Replace("{npcName}", FirstNonEmpty(context.npcName, speakerName, "NPC"))
            .Replace("{playerName}", FirstNonEmpty(context.playerName, "You"))
            .Replace("{passName}", FirstNonEmpty(context.passName, context.currentPassName, "your pass"))
            .Replace("{passExpiry}", FirstNonEmpty(context.passExpiry, "soon"))
            .Replace("{passExpirySeconds}", FormatSeconds(context.passExpirySeconds))
            .Replace("{lift}", FirstNonEmpty(context.liftName, context.nearbyLiftName, "this lift"))
            .Replace("{liftName}", FirstNonEmpty(context.liftName, "this lift"))
            .Replace("{stationName}", FirstNonEmpty(context.stationName, "this station"))
            .Replace("{regionName}", FirstNonEmpty(context.regionName, "this area"))
            .Replace("{requiredPass}", FirstNonEmpty(context.requiredPassName, "the right pass"))
            .Replace("{requiredPassName}", FirstNonEmpty(context.requiredPassName, "the right pass"))
            .Replace("{requiredPassTier}", HasLiftAccessContext(context) && context.requiredPassTier >= 0 ? context.requiredPassTier.ToString() : string.Empty)
            .Replace("{requiredPassId}", FirstNonEmpty(context.requiredPassId))
            .Replace("{currentPass}", FirstNonEmpty(context.currentPassName, context.passName, "your pass"))
            .Replace("{currentPassName}", FirstNonEmpty(context.currentPassName, context.passName, "your pass"))
            .Replace("{currentPassTier}", HasLiftAccessContext(context) && context.currentPassTier >= 0 ? context.currentPassTier.ToString() : string.Empty)
            .Replace("{kioskHint}", FirstNonEmpty(context.kioskHint))
            .Replace("{upgradeHint}", FirstNonEmpty(context.upgradeHint))
            .Replace("{raceName}", FirstNonEmpty(context.raceName, "the race"))
            .Replace("{anchorName}", FirstNonEmpty(context.anchorName, "this spot"))
            .Replace("{nearbyRun}", FirstNonEmpty(context.nearbyRunName, "that run"))
            .Replace("{nearbyRunName}", FirstNonEmpty(context.nearbyRunName, "that run"))
            .Replace("{runDifficulty}", FirstNonEmpty(context.nearbyRunDifficulty, "pretty serious"))
            .Replace("{nearbyRunDifficulty}", FirstNonEmpty(context.nearbyRunDifficulty, "pretty serious"))
            .Replace("{nearbyLift}", FirstNonEmpty(context.nearbyLiftName, context.liftName, "that lift"))
            .Replace("{nearbyLiftName}", FirstNonEmpty(context.nearbyLiftName, context.liftName, "that lift"))
            .Replace("{nearbyPoi}", FirstNonEmpty(context.nearbyPoiName, "over there"))
            .Replace("{nearbyPoiName}", FirstNonEmpty(context.nearbyPoiName, "over there"))
            .Replace("{weather}", FirstNonEmpty(context.weather, "weather"))
            .Replace("{timeOfDay}", FirstNonEmpty(context.timeOfDay, "today"))
            .Replace("{kiosk}", FirstNonEmpty(context.kioskName, "the kiosk"))
            .Replace("{kioskName}", FirstNonEmpty(context.kioskName, "the kiosk"))
            .Replace("{speakerName}", FirstNonEmpty(context.speakerName, speakerName, context.npcName, "Skier"))
            .Replace("{listenerName}", FirstNonEmpty(context.listenerName, "them"))
            .Replace("{questTitle}", FirstNonEmpty(context.questTitle, context.questDefinition != null ? context.questDefinition.title : null, "the quest"))
            .Replace("{questStage}", FirstNonEmpty(context.questStage, ResolveQuestStage(context), "current step"))
            .Replace("{objectiveName}", FirstNonEmpty(context.objectiveName, "the objective"));

        resolved = InputTokenRegex.Replace(resolved, match =>
        {
            string payload = match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty;
            string display = InputPromptResolver.ResolveInputPlaceholderPayload(inputActions, payload);
            return ApplyInlineStyle(display, ResolveInlineStyle("input", style));
        });

        resolved = StyleTokenRegex.Replace(resolved, match => ApplyInlineStyle(match.Groups[2].Value, ResolveInlineStyle(match.Groups[1].Value, style)));
        resolved = ColorTokenRegex.Replace(resolved, match => ApplyInlineStyle(match.Groups[2].Value, ResolveInlineStyle(match.Groups[1].Value, style)));
        resolved = SimpleWrapperRegex.Replace(resolved, match => ApplyInlineStyle(match.Groups[2].Value, ResolveInlineStyle(match.Groups[1].Value, style)));
        resolved = BoldRegex.Replace(resolved, match => $"<b>{match.Groups[1].Value}</b>");
        resolved = ItalicRegex.Replace(resolved, match => $"<i>{match.Groups[1].Value}</i>");
        resolved = SmallRegex.Replace(resolved, match => $"<size=85%>{match.Groups[1].Value}</size>");
        resolved = CapsRegex.Replace(resolved, match => match.Groups[1].Value.ToUpperInvariant());
        resolved = ValueTokenRegex.Replace(resolved, match => ResolveValueToken(match.Groups[1].Value, context, match.Value));
        resolved = StripUnknownCustomTags(resolved);
        return resolved;
    }

    private static bool HasLiftAccessContext(DialogueContext context)
    {
        return !string.IsNullOrWhiteSpace(context.liftId) ||
               !string.IsNullOrWhiteSpace(context.liftName) ||
               !string.IsNullOrWhiteSpace(context.requiredPassId) ||
               !string.IsNullOrWhiteSpace(context.requiredPassName) ||
               !string.IsNullOrWhiteSpace(context.currentPassId) ||
               !string.IsNullOrWhiteSpace(context.currentPassName);
    }

    private static string FormatSeconds(float seconds)
    {
        if (float.IsPositiveInfinity(seconds))
            return "infinite";

        return seconds > 0f ? Mathf.CeilToInt(seconds).ToString() : "0";
    }

    private static string ResolveValueToken(string key, DialogueContext context, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
            return fallback;

        string normalized = key.Trim();

        if (context.values != null && context.values.TryGetValue(normalized, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;

        return normalized switch
        {
            "pass" => FirstNonEmpty(context.passName, context.currentPassName, "your pass"),
            "passName" => FirstNonEmpty(context.passName, context.currentPassName, "your pass"),
            "passExpiry" => FirstNonEmpty(context.passExpiry, "soon"),

            "requiredPass" => FirstNonEmpty(context.requiredPassName, "the right pass"),
            "requiredPassName" => FirstNonEmpty(context.requiredPassName, "the right pass"),
            "requiredPassId" => FirstNonEmpty(context.requiredPassId),

            "currentPass" => FirstNonEmpty(context.currentPassName, context.passName, "your pass"),
            "currentPassName" => FirstNonEmpty(context.currentPassName, context.passName, "your pass"),
            "currentPassId" => FirstNonEmpty(context.currentPassId),

            "lift" => FirstNonEmpty(context.liftName, context.nearbyLiftName, "this lift"),
            "liftName" => FirstNonEmpty(context.liftName, context.nearbyLiftName, "this lift"),
            "nearbyLift" => FirstNonEmpty(context.nearbyLiftName, context.liftName, "that lift"),
            "nearbyLiftName" => FirstNonEmpty(context.nearbyLiftName, context.liftName, "that lift"),

            "nearbyRun" => FirstNonEmpty(context.nearbyRunName, "that run"),
            "nearbyRunName" => FirstNonEmpty(context.nearbyRunName, "that run"),
            "nearbyRunDifficulty" => FirstNonEmpty(context.nearbyRunDifficulty, "pretty serious"),
            "runDifficulty" => FirstNonEmpty(context.nearbyRunDifficulty, "pretty serious"),

            "nearbyPoi" => FirstNonEmpty(context.nearbyPoiName, "over there"),
            "nearbyPoiName" => FirstNonEmpty(context.nearbyPoiName, "over there"),

            "kiosk" => FirstNonEmpty(context.kioskName, "the kiosk"),
            "kioskName" => FirstNonEmpty(context.kioskName, "the kiosk"),

            "race" => FirstNonEmpty(context.raceName, "the race"),
            "raceName" => FirstNonEmpty(context.raceName, "the race"),

            "anchor" => FirstNonEmpty(context.anchorName, "this spot"),
            "anchorName" => FirstNonEmpty(context.anchorName, "this spot"),

            _ => ResolveExternalValueToken(normalized, context, fallback)
        };
    }

    private static string ResolveExternalValueToken(string key, DialogueContext context, string fallback)
    {
        var providers = Object.FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < providers.Length; i++)
        {
            if (providers[i] is IDialogueContextProvider provider &&
                provider.TryGetDialogueValue(key, context, out string value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return fallback;
    }

    private static string ResolveQuestStage(DialogueContext context)
    {
        var definition = context.questDefinition;
        var state = context.questState;
        if (definition == null || state == null)
            return string.Empty;

        var stage = definition.GetStage(state.currentStageIndex);
        if (stage == null)
            return string.Empty;

        return FirstNonEmpty(stage.title, stage.id);
    }

    private static string ApplyInlineStyle(string text, NpcDialogueInlineTextStyle? style)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (!style.HasValue)
            return text;

        var value = style.Value;
        var builder = new StringBuilder(text.Length + 32);
        bool hasColor = value.color.a > 0f;
        bool hasSize = value.sizePercent > 0.1f && !Mathf.Approximately(value.sizePercent, 1f);

        if (hasColor)
            builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(value.color)).Append('>');
        if (hasSize)
            builder.Append("<size=").Append(Mathf.RoundToInt(value.sizePercent * 100f)).Append("%>");
        if (value.bold)
            builder.Append("<b>");
        if (value.italic)
            builder.Append("<i>");

        builder.Append(text);

        if (value.italic)
            builder.Append("</i>");
        if (value.bold)
            builder.Append("</b>");
        if (hasSize)
            builder.Append("</size>");
        if (hasColor)
            builder.Append("</color>");

        return builder.ToString();
    }

    private static NpcDialogueInlineTextStyle? ResolveInlineStyle(string styleId, NpcDialogueBubbleStyleSO style)
    {
        if (string.IsNullOrWhiteSpace(styleId))
            return null;

        string normalized = styleId.Trim().ToLowerInvariant();
        if (style != null && style.inlineTextStyles != null)
        {
            for (int i = 0; i < style.inlineTextStyles.Count; i++)
            {
                if (string.Equals(style.inlineTextStyles[i].id?.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
                    return style.inlineTextStyles[i];
            }
        }

        return normalized switch
        {
            "accent" => DefaultStyle(style != null ? style.accentColor : new Color(0.45f, 0.7f, 0.95f, 1f), true),
            "warning" => DefaultStyle(new Color(1f, 0.82f, 0.45f, 1f), true),
            "success" => DefaultStyle(new Color(0.64f, 0.97f, 0.72f, 1f), true),
            "muted" => DefaultStyle(new Color(0.73f, 0.79f, 0.86f, 1f), false),
            "input" => DefaultStyle(style != null ? style.controlsColor : Color.white, true),
            "quest" => DefaultStyle(style != null ? style.titleColor : Color.white, true),
            "objective" => DefaultStyle(style != null ? style.speakerColor : Color.white, true),
            _ => null
        };
    }

    private static NpcDialogueInlineTextStyle DefaultStyle(Color color, bool bold)
    {
        return new NpcDialogueInlineTextStyle
        {
            color = color,
            bold = bold,
            italic = false,
            sizePercent = 1f
        };
    }

    private static string StripUnknownCustomTags(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string result = Regex.Replace(text, @"\{/?style:[^}]+\}", string.Empty, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{/?color:[^}]+\}", string.Empty, RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\{/?(warning|accent|muted|success|quest|objective|b|i|small|caps)\}", string.Empty, RegexOptions.IgnoreCase);
        return result;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }
}
