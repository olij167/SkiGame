#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MoreMountains.Feedbacks;

[CustomEditor(typeof(SkiControllerFeelBridge))]
public sealed class SkiControllerFeelBridgeEditor : Editor
{
    private bool _createMissingOnly = true;
    private bool _renameToMatch = true;

    private bool _generateTemplates = true;
    private bool _overwriteFeedbackLists = false;
    private bool _autoShakerSetup = true;

    private bool _includeAudioFeedbacks = false;
    private bool _includeParticleFeedbacks = false;

    private bool _addPrefix = false;
    private string _prefix = "FB_";
    private Transform _parentOverride;

    private static readonly BindingFlags FieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("FEEL Child Generator", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            _parentOverride = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Parent Override", "If null, children will be created under this GameObject."),
                _parentOverride,
                typeof(Transform),
                true
            );

            _createMissingOnly = EditorGUILayout.ToggleLeft(
                new GUIContent("Create Missing Only", "If enabled, will not replace existing references/children."),
                _createMissingOnly
            );

            _renameToMatch = EditorGUILayout.ToggleLeft(
                new GUIContent("Rename Children To Match", "If enabled, children names will be set to match generated name."),
                _renameToMatch
            );

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Template Generation", EditorStyles.boldLabel);

            _generateTemplates = EditorGUILayout.ToggleLeft(
                new GUIContent("Generate Feedback Templates", "If enabled, populates each MMF_Player with a default feedback stack."),
                _generateTemplates
            );

            _includeAudioFeedbacks = EditorGUILayout.ToggleLeft(
    new GUIContent("Include FEEL Audio Feedbacks", "Off by default (you already have SkiAudioController). Enable only for rare one-shots like crash/land if desired."),
    _includeAudioFeedbacks
);

            _includeParticleFeedbacks = EditorGUILayout.ToggleLeft(
                new GUIContent("Include FEEL Particle Feedbacks", "Off by default (you already have SkiSnowParticles). Enable only for non-snow FX like sparks/flash, etc."),
                _includeParticleFeedbacks
            );

            using (new EditorGUI.DisabledScope(!_generateTemplates))
            {
                _overwriteFeedbackLists = EditorGUILayout.ToggleLeft(
                    new GUIContent("Overwrite Existing Feedback Lists", "If enabled, clears and rebuilds feedback lists even if they already contain feedbacks."),
                    _overwriteFeedbackLists
                );

                _autoShakerSetup = EditorGUILayout.ToggleLeft(
                    new GUIContent("Run Automatic Shaker Setup", "Calls AutomaticShakerSetup on each generated MMF_Player."),
                    _autoShakerSetup
                );
            }

            EditorGUILayout.Space(6);

            _addPrefix = EditorGUILayout.ToggleLeft(
                new GUIContent("Add Prefix", "If enabled, generated names are prefixed (e.g. FB_SpeedLoop)."),
                _addPrefix
            );

            using (new EditorGUI.DisabledScope(!_addPrefix))
            {
                _prefix = EditorGUILayout.TextField("Prefix", _prefix);
                if (string.IsNullOrEmpty(_prefix)) _prefix = "FB_";
            }

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Generate / Assign MMF Players (+ Templates)", GUILayout.Height(28)))
            {
                GenerateAssignAndTemplate();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void GenerateAssignAndTemplate()
    {
        var bridge = (SkiControllerFeelBridge)target;
        if (bridge == null) return;

        Transform root = ResolveRoot(bridge);
        if (root == null) return;

        Undo.RegisterCompleteObjectUndo(bridge, "Generate FEEL Feedback Children");
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Generate FEEL Feedback Children");

        var mmfFields = GetMMFPlayerFields(bridge);
        if (mmfFields.Count == 0)
        {
            Debug.LogWarning("[FEEL Bridge Editor] No MMF_Player fields found on SkiControllerFeelBridge.");
            return;
        }

        int createdPlayers = 0;
        int assignedPlayers = 0;
        int templatedPlayers = 0;

        foreach (var field in mmfFields)
        {
            string childName = MakeChildName(field.Name);

            var current = field.GetValue(bridge) as MMF_Player;

            if (_createMissingOnly && current != null)
            {
                if (_renameToMatch && current.gameObject.name != childName)
                {
                    Undo.RecordObject(current.gameObject, "Rename FEEL Feedback Child");
                    current.gameObject.name = childName;
                    EditorUtility.SetDirty(current.gameObject);
                }

                if (_generateTemplates)
                {
                    if (ApplyTemplateIfNeeded(current, field.Name))
                        templatedPlayers++;
                }

                continue;
            }

            MMF_Player resolved = null;

            // Try find existing by name under root
            Transform existing = root.Find(childName);
            if (existing != null)
            {
                resolved = existing.GetComponent<MMF_Player>();
                if (resolved == null)
                {
                    resolved = Undo.AddComponent<MMF_Player>(existing.gameObject);
                    createdPlayers++;
                }
            }
            else
            {
                var go = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(go, "Create FEEL Feedback Child");
                go.transform.SetParent(root, false);

                resolved = Undo.AddComponent<MMF_Player>(go);
                createdPlayers++;
            }

            if (resolved != null)
            {
                field.SetValue(bridge, resolved);
                assignedPlayers++;

                if (_renameToMatch && resolved.gameObject.name != childName)
                {
                    Undo.RecordObject(resolved.gameObject, "Rename FEEL Feedback Child");
                    resolved.gameObject.name = childName;
                }

                EditorUtility.SetDirty(resolved);

                if (_generateTemplates)
                {
                    if (ApplyTemplateIfNeeded(resolved, field.Name))
                        templatedPlayers++;
                }
            }
        }

        EditorUtility.SetDirty(bridge);
        serializedObject.Update();

        Debug.Log($"[FEEL Bridge Editor] Done. CreatedPlayers:{createdPlayers}, AssignedPlayers:{assignedPlayers}, Templated:{templatedPlayers}. Root:{root.name}", root);
    }

    private Transform ResolveRoot(SkiControllerFeelBridge bridge)
    {
        return _parentOverride != null ? _parentOverride : bridge.transform;
    }

    private string MakeChildName(string fieldName)
    {
        // fbSpeedLoop -> SpeedLoop
        string cleaned = fieldName;
        if (cleaned.StartsWith("fb", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(2);

        if (cleaned.Length > 0)
            cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned.Substring(1);

        if (_addPrefix)
            cleaned = _prefix + cleaned;

        return cleaned;
    }

    private static List<FieldInfo> GetMMFPlayerFields(SkiControllerFeelBridge bridge)
    {
        var list = new List<FieldInfo>();
        var type = bridge.GetType();
        foreach (var f in type.GetFields(FieldFlags))
        {
            if (f.FieldType == typeof(MMF_Player))
                list.Add(f);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return list;
    }

    // -----------------------------
    // Template authoring
    // -----------------------------

    private bool ApplyTemplateIfNeeded(MMF_Player player, string bridgeFieldName)
    {
        if (player == null) return false;

        bool hasAny = player.FeedbacksList != null && player.FeedbacksList.Count > 0;
        if (hasAny && !_overwriteFeedbackLists)
            return false;

        Undo.RegisterCompleteObjectUndo(player, "Apply FEEL Feedback Template");

        if (_overwriteFeedbackLists)
        {
            // Clear existing feedback list
            player.FeedbacksList?.Clear();
            EditorUtility.SetDirty(player);
        }

        // Build template based on bridge field
        var template = GetTemplateForField(bridgeFieldName);
        if (template == null || template.Count == 0)
            return false;

        // Add feedbacks in order
        foreach (var typeName in template)
        {
            var t = ResolveFeedbackType(typeName);
            if (t == null)
            {
                // Missing modules (PostProcessing, NiceVibrations, etc.) shouldn’t break generation
                continue;
            }

            // AddFeedback(Type) is the supported API. :contentReference[oaicite:3]{index=3}
            var newFb = player.AddFeedback(t, add: true);
            ConfigureDefaultFeedbackSettings(newFb, t);
        }

        // For loop-style players, ensure looping via Looper feedbacks (if present in template list)
        // FEEL loops are done via Looper/LooperStart. :contentReference[oaicite:4]{index=4}
        ConfigureLoopersIfAny(player);

        if (_autoShakerSetup)
        {
            // Auto-wires shakers where possible. :contentReference[oaicite:5]{index=5}
            player.AutomaticShakerSetup();
        }

        EditorUtility.SetDirty(player);
        return true;
    }

    /// <summary>
    /// Returns a list of FEEL feedback type names to add, in order.
    /// These are full type names. Missing types are skipped gracefully.
    /// </summary>
    private List<string> GetTemplateForField(string bridgeFieldName)
    {
        // IMPORTANT: These are “safe defaults”. You will still assign clips, particle prefabs,
        // post volume references, etc. in the inspector afterwards.
        //
        // For loops we include LooperStart at top and Looper at bottom.

        switch (bridgeFieldName)
        {
            case "fbSpeedLoop":
                {
                    var list = new List<string>
    {
        "MoreMountains.Feedbacks.MMF_LooperStart",
        "MoreMountains.Feedbacks.MMF_CameraShake",
        "MoreMountains.Feedbacks.MMF_CameraFieldOfView",
        "MoreMountains.Feedbacks.MMF_ChromaticAberration",
        "MoreMountains.Feedbacks.MMF_Vignette",
        "MoreMountains.Feedbacks.MMF_Looper"
    };

                    if (_includeAudioFeedbacks)
                        list.Insert(1, "MoreMountains.Feedbacks.MMF_Sound");

                    return list;
                }

            case "fbVeryFastMilestone":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_CameraShake",
                    "MoreMountains.Feedbacks.MMF_CameraFieldOfView",
                    "MoreMountains.Feedbacks.MMF_ChromaticAberration"
                };

            case "fbJumpTakeoff":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_CameraShake"
                };

            case "fbAirLoop":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_LooperStart",
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_CameraShake",
                    "MoreMountains.Feedbacks.MMF_Looper"
                };

            case "fbLand":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_CameraShake"
                };

            case "fbStackCrash":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_CameraShake",
                    "MoreMountains.Feedbacks.MMF_FreezeFrame",
                    "MoreMountains.Feedbacks.MMF_Flash" // requires MMFlash UI element / shaker setup
                };

            case "fbGrindLoop":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_LooperStart",
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_ParticlesPlay",
                    "MoreMountains.Feedbacks.MMF_Looper"
                };

            case "fbPolePlant":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_ParticlesInstantiation"
                };

            case "fbPoleDragLoop":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_LooperStart",
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_ParticlesPlay",
                    "MoreMountains.Feedbacks.MMF_Looper"
                };

            case "fbPoleRelease":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_Sound"
                };

            case "fbSkatePush":
                return new List<string>
                {
                    "MoreMountains.Feedbacks.MMF_Sound",
                    "MoreMountains.Feedbacks.MMF_ParticlesInstantiation"
                };

            default:
                // Unknown/new field: generate nothing
                return null;
        }
    }

    private static Type ResolveFeedbackType(string fullName)
    {
        // Fast path
        var t = Type.GetType(fullName);
        if (t != null) return t;

        // Scan loaded assemblies
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    private static void ConfigureDefaultFeedbackSettings(MMF_Feedback fb, Type fbType)
    {
        if (fb == null) return;

        // Label if present (nice in inspector)
        TrySet(fb, "Label", fbType.Name.Replace("MMF_", string.Empty));

        // A few safe, conservative defaults (won’t break if fields don’t exist)
        // Camera shake defaults inspired by FEEL recipes (Duration ~0.3, Amplitude ~2, Frequency ~40). :contentReference[oaicite:6]{index=6}
        if (fbType.Name == "MMF_CameraShake")
        {
            TrySet(fb, "Duration", 0.18f);
            TrySet(fb, "Amplitude", 0.6f);
            TrySet(fb, "Frequency", 18f);

        }

        if (fbType.Name == "MMF_CameraFieldOfView")
        {
            TrySet(fb, "Duration", 0.18f);
            TrySet(fb, "FOVChange", 2.5f);

        }

        // Freeze frame is a classic short pause; FEEL notes it requires a TimeManager. :contentReference[oaicite:7]{index=7}
        if (fbType.Name == "MMF_FreezeFrame")
        {
            TrySet(fb, "FreezeFrameDuration", 0.04f);
        }

        if (fbType.Name == "MMF_Vignette")
        {
            TrySet(fb, "Duration", 0.20f);
            TrySet(fb, "Intensity", 0.08f);

        }

        if (fbType.Name == "MMF_ChromaticAberration")
        {
            TrySet(fb, "Duration", 0.20f);
            TrySet(fb, "Intensity", 0.15f);
        }

        if (fbType.Name == "MMF_Sound")
        {
            // Leave clip empty; user will assign.
            // But we can default volume and spatial blend if those fields exist.
            TrySet(fb, "Volume", 0.8f);
            TrySet(fb, "SpatialBlend", 1f);
        }
    }

    private static void ConfigureLoopersIfAny(MMF_Player player)
    {
        if (player == null || player.FeedbacksList == null) return;

        // Find looper feedbacks and set them to “infinite” if possible.
        foreach (var fb in player.FeedbacksList)
        {
            if (fb == null) continue;
            var n = fb.GetType().Name;

            if (n == "MMF_Looper")
            {
                // Different FEEL versions use slightly different field names.
                // We try a handful safely.
                if (!TrySet(fb, "InfiniteLoop", true))
                {
                    // If there’s a numeric loop count, set to -1 or a large number
                    if (!TrySet(fb, "NumberOfLoops", -1))
                        TrySet(fb, "NumberOfLoops", 999999);
                }
            }
        }
    }

    private static bool TrySet(object obj, string memberName, object value)
    {
        if (obj == null) return false;
        var t = obj.GetType();

        // Property
        var p = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite)
        {
            try
            {
                var coerced = Coerce(value, p.PropertyType);
                p.SetValue(obj, coerced);
                return true;
            }
            catch { return false; }
        }

        // Field
        var f = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null)
        {
            try
            {
                var coerced = Coerce(value, f.FieldType);
                f.SetValue(obj, coerced);
                return true;
            }
            catch { return false; }
        }

        return false;
    }

    private static object Coerce(object value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        try
        {
            if (targetType.IsEnum && value is int i)
                return Enum.ToObject(targetType, i);

            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return value;
        }
    }
}
#endif
