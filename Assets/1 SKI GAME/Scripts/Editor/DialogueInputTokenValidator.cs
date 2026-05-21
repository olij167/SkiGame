using SkiGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static class DialogueInputTokenValidator
{
    private static readonly string[] TestPayloads =
    {
        "Interact",
        "Jump",
        "Lean",
        "Lean:positive",
        "Lean:negative",
        "Lean:up|forward|positive",
        "Lean:down|back|backward|negative"
    };

    [MenuItem("Tools/Ski Game/Input/Validate Dialogue Input Tokens")]
    public static void ValidateDialogueInputTokens()
    {
        var actions = ResolveInputActions();
        if (actions == null)
        {
            Debug.LogWarning("No InputActionAsset found. Select one in the Project window or add an NpcDialogueDirector to the scene.");
            return;
        }

        for (int i = 0; i < TestPayloads.Length; i++)
        {
            string payload = TestPayloads[i];
            string resolved = InputPromptResolver.ResolveInputPlaceholderPayload(actions, payload);
            Debug.Log($"{{input:{payload}}} -> {resolved}", actions);
        }
    }

    private static InputActionAsset ResolveInputActions()
    {
        if (Selection.activeObject is InputActionAsset selectedActions)
            return selectedActions;

        var director = Object.FindAnyObjectByType<NpcDialogueDirector>();
        if (director != null)
            return director.InputActions;

        string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
    }
}
