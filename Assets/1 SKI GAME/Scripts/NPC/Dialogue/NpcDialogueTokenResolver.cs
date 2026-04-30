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

    public static string Format(string text, DialogueContext context, string speakerName, InputActionAsset inputActions, NpcDialogueBubbleStyleSO style = null)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string resolved = text
            .Replace("{npcName}", FirstNonEmpty(context.npcName, speakerName, "NPC"))
            .Replace("{playerName}", FirstNonEmpty(context.playerName, "You"))
            .Replace("{passName}", FirstNonEmpty(context.passName, "pass"))
            .Replace("{passExpiry}", FirstNonEmpty(context.passExpiry, "soon"))
            .Replace("{raceName}", FirstNonEmpty(context.raceName, "the race"));

        resolved = InputTokenRegex.Replace(resolved, match =>
        {
            string actionName = match.Groups.Count > 1 ? match.Groups[1].Value : string.Empty;
            string display = InputPromptResolver.GetBindingDisplay(inputActions, actionName);
            return ApplyInlineStyle(display, ResolveInlineStyle("input", style));
        });

        resolved = StyleTokenRegex.Replace(resolved, match => ApplyInlineStyle(match.Groups[2].Value, ResolveInlineStyle(match.Groups[1].Value, style)));
        resolved = ColorTokenRegex.Replace(resolved, match => ApplyInlineStyle(match.Groups[2].Value, ResolveInlineStyle(match.Groups[1].Value, style)));
        resolved = SimpleWrapperRegex.Replace(resolved, match => ApplyInlineStyle(match.Groups[2].Value, ResolveInlineStyle(match.Groups[1].Value, style)));
        resolved = BoldRegex.Replace(resolved, match => $"<b>{match.Groups[1].Value}</b>");
        resolved = ItalicRegex.Replace(resolved, match => $"<i>{match.Groups[1].Value}</i>");
        resolved = SmallRegex.Replace(resolved, match => $"<size=85%>{match.Groups[1].Value}</size>");
        resolved = CapsRegex.Replace(resolved, match => match.Groups[1].Value.ToUpperInvariant());
        resolved = StripUnknownCustomTags(resolved);
        return resolved;
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
            "input" => DefaultStyle(style != null ? style.chipTextColor : Color.white, true),
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
