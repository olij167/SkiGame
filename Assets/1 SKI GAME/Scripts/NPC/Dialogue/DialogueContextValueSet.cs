using System;
using System.Collections.Generic;

[Serializable]
public sealed class DialogueContextValue
{
    public string key;
    public string value;
}

[Serializable]
public sealed class DialogueContextValueSet
{
    public List<DialogueContextValue> values = new();

    public bool TryGetValue(string key, out string value)
    {
        value = string.Empty;
        if (values == null || string.IsNullOrWhiteSpace(key))
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            var entry = values[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            if (string.Equals(entry.key.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                value = entry.value ?? string.Empty;
                return true;
            }
        }

        return false;
    }
}

public interface IDialogueContextProvider
{
    bool TryGetDialogueValue(string key, DialogueContext context, out string value);
}
