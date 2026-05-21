using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Audio
{
    /// <summary>
    /// Data-driven coverage profile used by the editor audio coverage tools.
    /// It stores component, event, field, and type references as strings so the editor utilities
    /// can audit project-specific audio wiring without hard compile-time dependencies on gameplay scripts.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCoverageProfile", menuName = "PungentFunk Utilities/Audio/Coverage Profile")]
    public sealed class AudioCoverageProfileSO : ScriptableObject
    {
        [Header("Catalog Schema")]
        [Tooltip("Display name used by the editor coverage tools.")]
        public string profileName = "Default Audio Coverage Profile";

        [Tooltip("Primary audio catalog ScriptableObject type. Leave blank if this project does not use a catalog asset.")]
        public string catalogTypeName = "AudioCatalogSO";

        [Tooltip("Enum or key type used by the audio catalog. Leave blank if this project uses string/object cue IDs.")]
        public string cueEnumTypeName = "AudioCueId";

        [Tooltip("Serialized array/list field on the catalog that contains cue entries.")]
        public string cueListPropertyName = "cues";

        [Tooltip("Cue id/key field inside each cue entry.")]
        public string cueIdPropertyName = "id";

        [Tooltip("Audio clip array/list field inside each cue entry.")]
        public string clipsPropertyName = "clips";

        [Tooltip("Volume field inside each cue entry.")]
        public string volumePropertyName = "volume";

        [Tooltip("Pitch range Vector2 field inside each cue entry.")]
        public string pitchRangePropertyName = "pitchRange";

        [Tooltip("Spatial blend float field inside each cue entry.")]
        public string spatialBlendPropertyName = "spatialBlend";

        [Tooltip("Minimum distance float field inside each cue entry.")]
        public string minDistancePropertyName = "minDistance";

        [Tooltip("Maximum distance float field inside each cue entry.")]
        public string maxDistancePropertyName = "maxDistance";

        [Tooltip("Cooldown float field inside each cue entry.")]
        public string cooldownPropertyName = "cooldownSeconds";

        [Tooltip("Audio mixer group object-reference field inside each cue entry.")]
        public string mixerGroupPropertyName = "mixerGroup";

        [Tooltip("Cue enum/key value that should be ignored as the unset/none value.")]
        public string noneCueName = "None";

        [Header("Coverage Behaviour")]
        [Tooltip("When enabled, non-ignored cue binding rules count as coverage for the cue.")]
        public bool treatBindingRulesAsCoverage = true;

        [Tooltip("When enabled, code references discovered by script scanning count as coverage for the cue.")]
        public bool treatScriptReferencesAsCoverage = true;

        [Tooltip("When enabled, bindings marked as Manual Coverage Only count as coverage without requiring a matching component.")]
        public bool treatManualBindingsAsCoverage = true;

        [Tooltip("When enabled, cues with no configured binding/script coverage are reported as warnings.")]
        public bool warnWhenCueHasNoCoverage = true;

        [Tooltip("Script text must include at least one of these tokens for a cue-name hit to count. Leave empty to count any cue-name reference.")]
        public List<string> scriptSearchTokens = new List<string>
        {
            "AudioCueId.",
            "AudioCue.",
            "PlayCue",
            "TryPlay",
            "PlayUi",
            "PlayWorld",
            "PlayAttached",
            "AudioSource.Play"
        };

        [Header("Runtime Source Diagnostics")]
        [Tooltip("Optional runtime hook/source component types to count in open scenes. Missing type names are reported as optional missing types rather than compile errors.")]
        public List<string> runtimeHookTypeNames = new List<string>
        {
            "UnityEngine.UIElements.UIDocument",
            "UnityEngine.UI.Button",
            "AudioSource",
            "ContactAudioRouter",
            "ContactEventClassifier",
            "AudioMaterialTag"
        };

        [Header("Scene Reference Coverage")]
        [Tooltip("Component type expected on scene colliders or their parents. Default keeps the existing AudioMaterialTag workflow.")]
        public string sceneReferenceComponentTypeName = "AudioMaterialTag";

        [Tooltip("Serialized object-reference field on the scene reference component. Default: surfaceMaterial.")]
        public string sceneReferenceFieldName = "surfaceMaterial";

        [Tooltip("Skip trigger colliders during scene reference coverage scans.")]
        public bool sceneReferenceSkipTriggers = true;

        [Tooltip("Skip terrain colliders during scene reference coverage scans.")]
        public bool sceneReferenceSkipTerrains = true;

        [Header("Asset Coverage Defaults")]
        [Tooltip("Optional terrain audio profile type. Used by Audio Setup Coverage if present.")]
        public string terrainProfileTypeName = "TerrainAudioMaterialProfileSO";

        [Tooltip("Serialized array/list field containing terrain layer bindings.")]
        public string terrainLayerBindingsPropertyName = "layerBindings";

        [Tooltip("Layer field inside each terrain binding element.")]
        public string terrainLayerPropertyName = "layer";

        [Tooltip("Material field inside each terrain binding element.")]
        public string terrainMaterialPropertyName = "material";

        [Tooltip("Optional interaction matrix type. Used by Audio Setup Coverage if present.")]
        public string interactionMatrixTypeName = "AudioInteractionMatrixSO";

        [Tooltip("Serialized exact-profile array/list field on the interaction matrix.")]
        public string exactProfilesPropertyName = "exactProfiles";

        [Tooltip("Serialized category-fallback array/list field on the interaction matrix.")]
        public string categoryFallbacksPropertyName = "categoryFallbacks";

        [Header("Cue Bindings")]
        public List<AudioCueBindingRule> cueBindings = new List<AudioCueBindingRule>();

        [Header("Component Expectations")]
        public List<AudioComponentExpectationRule> componentExpectations = new List<AudioComponentExpectationRule>();

        [Header("Required Asset Fields")]
        public List<AudioRequiredFieldRule> requiredFieldRules = new List<AudioRequiredFieldRule>();

        private void Reset()
        {
            ResetToGenericDefaults(clearExistingRules: true);
        }

        public void ResetToGenericDefaults(bool clearExistingRules)
        {
            profileName = "Default Audio Coverage Profile";
            catalogTypeName = "AudioCatalogSO";
            cueEnumTypeName = "AudioCueId";
            cueListPropertyName = "cues";
            cueIdPropertyName = "id";
            clipsPropertyName = "clips";
            volumePropertyName = "volume";
            pitchRangePropertyName = "pitchRange";
            spatialBlendPropertyName = "spatialBlend";
            minDistancePropertyName = "minDistance";
            maxDistancePropertyName = "maxDistance";
            cooldownPropertyName = "cooldownSeconds";
            mixerGroupPropertyName = "mixerGroup";
            noneCueName = "None";

            scriptSearchTokens = new List<string>
            {
                "AudioCueId.",
                "AudioCue.",
                "PlayCue",
                "TryPlay",
                "PlayUi",
                "PlayWorld",
                "PlayAttached",
                "AudioSource.Play"
            };

            runtimeHookTypeNames = new List<string>
            {
                "UnityEngine.UIElements.UIDocument",
                "UnityEngine.UI.Button",
                "AudioSource",
                "ContactAudioRouter",
                "ContactEventClassifier",
                "AudioMaterialTag"
            };

            sceneReferenceComponentTypeName = "AudioMaterialTag";
            sceneReferenceFieldName = "surfaceMaterial";
            terrainProfileTypeName = "TerrainAudioMaterialProfileSO";
            terrainLayerBindingsPropertyName = "layerBindings";
            terrainLayerPropertyName = "layer";
            terrainMaterialPropertyName = "material";
            interactionMatrixTypeName = "AudioInteractionMatrixSO";
            exactProfilesPropertyName = "exactProfiles";
            categoryFallbacksPropertyName = "categoryFallbacks";

            if (clearExistingRules)
            {
                cueBindings.Clear();
                componentExpectations.Clear();
                requiredFieldRules.Clear();
            }

            AddMissingDefaultBinding("ui_confirm", "Generic confirmation UI", AudioBindingSourceKind.UnityEvent, "", "Confirm event", AudioPlaybackMode.PlayUi);
            AddMissingDefaultBinding("ui_cancel", "Generic cancellation UI", AudioBindingSourceKind.UnityEvent, "", "Cancel event", AudioPlaybackMode.PlayUi);
            AddMissingDefaultBinding("ui_select", "Selectable UI element", AudioBindingSourceKind.UnityEvent, "UnityEngine.UI.Button", "onClick", AudioPlaybackMode.PlayUi);
            AddMissingDefaultBinding("ui_navigation", "Navigation UI element", AudioBindingSourceKind.UnityEvent, "", "Navigate event", AudioPlaybackMode.PlayUi);
            AddMissingDefaultBinding("ui_adjust", "Adjustable UI element", AudioBindingSourceKind.UnityEvent, "", "Value changed event", AudioPlaybackMode.PlayUi);
            AddMissingDefaultBinding("impact_light", "Light contact or impact source", AudioBindingSourceKind.CSharpEventViaAdapter, "", "LightImpact", AudioPlaybackMode.PlayAttached);
            AddMissingDefaultBinding("impact_heavy", "Heavy contact or impact source", AudioBindingSourceKind.CSharpEventViaAdapter, "", "HeavyImpact", AudioPlaybackMode.PlayAttached);
            AddMissingDefaultBinding("interaction_start", "Generic interaction source", AudioBindingSourceKind.CSharpEventViaAdapter, "", "Started", AudioPlaybackMode.PlayWorld);
            AddMissingDefaultBinding("interaction_complete", "Generic interaction source", AudioBindingSourceKind.CSharpEventViaAdapter, "", "Completed", AudioPlaybackMode.PlayWorld);
            AddMissingDefaultBinding("state_enter", "Generic state/event source", AudioBindingSourceKind.CSharpEventViaAdapter, "", "Entered", AudioPlaybackMode.PlayAttached);
            AddMissingDefaultBinding("state_exit", "Generic state/event source", AudioBindingSourceKind.CSharpEventViaAdapter, "", "Exited", AudioPlaybackMode.PlayAttached);
            AddMissingDefaultBinding("loop_ambient", "Ambient looping source", AudioBindingSourceKind.ManualCoverageOnly, "", "Manual loop", AudioPlaybackMode.PlayWorld);
            AddMissingDefaultBinding("fallback_missing", "Fallback cue used when a requested cue is missing", AudioBindingSourceKind.ManualCoverageOnly, "", "Fallback", AudioPlaybackMode.PlayUi);

            AddMissingComponentExpectation("ContactEventClassifier", "ContactAudioRouter", "t:Prefab", AudioBindingValidationScope.ProjectPrefabs);
            AddMissingComponentExpectation("Collider", "AudioMaterialTag", "t:Prefab", AudioBindingValidationScope.SceneAndPrefabs);

            AddMissingRequiredFieldRule("AudioInteractionMatrixSO", new[]
            {
                "defaultSoftProfile",
                "defaultHardProfile",
                "defaultScrapeProfile"
            });
        }

        public int CountBindingsForCue(string cueName, bool includeIgnored = false)
        {
            if (string.IsNullOrWhiteSpace(cueName) || cueBindings == null)
                return 0;

            int count = 0;
            for (int i = 0; i < cueBindings.Count; i++)
            {
                AudioCueBindingRule binding = cueBindings[i];
                if (binding == null)
                    continue;

                if (!includeIgnored && binding.ignoreInCoverage)
                    continue;

                if (string.Equals(binding.cueName, cueName, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public bool IsCueIgnored(string cueName)
        {
            if (string.IsNullOrWhiteSpace(cueName) || cueBindings == null)
                return false;

            for (int i = 0; i < cueBindings.Count; i++)
            {
                AudioCueBindingRule binding = cueBindings[i];
                if (binding == null)
                    continue;

                if (binding.ignoreInCoverage && string.Equals(binding.cueName, cueName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void AddMissingDefaultBinding(string cueName, string displayName, AudioBindingSourceKind sourceKind, string componentTypeName, string memberName, AudioPlaybackMode playbackMode)
        {
            if (CountBindingsForCue(cueName, includeIgnored: true) > 0)
                return;

            cueBindings.Add(new AudioCueBindingRule
            {
                cueName = cueName,
                displayName = displayName,
                sourceKind = sourceKind,
                sourceComponentTypeName = componentTypeName,
                sourceMemberName = memberName,
                triggerKind = sourceKind == AudioBindingSourceKind.UnityEvent ? AudioBindingTriggerKind.UnityEvent : AudioBindingTriggerKind.CSharpEvent,
                playbackMode = playbackMode,
                positionSource = playbackMode == AudioPlaybackMode.PlayUi ? AudioPositionSourceKind.None : AudioPositionSourceKind.SourceTransform,
                validationScope = AudioBindingValidationScope.SceneAndPrefabs,
                required = true,
                allowMultipleSources = true,
                ignoreInCoverage = false
            });
        }

        private void AddMissingComponentExpectation(string sourceComponentTypeName, string requiredComponentTypeName, string prefabSearchFilter, AudioBindingValidationScope scope)
        {
            for (int i = 0; i < componentExpectations.Count; i++)
            {
                AudioComponentExpectationRule rule = componentExpectations[i];
                if (rule != null && rule.sourceComponentTypeName == sourceComponentTypeName && rule.requiredComponentTypeName == requiredComponentTypeName)
                    return;
            }

            componentExpectations.Add(new AudioComponentExpectationRule
            {
                sourceComponentTypeName = sourceComponentTypeName,
                requiredComponentTypeName = requiredComponentTypeName,
                prefabSearchFilter = prefabSearchFilter,
                validationScope = scope,
                required = true
            });
        }

        private void AddMissingRequiredFieldRule(string assetTypeName, string[] requiredFields)
        {
            for (int i = 0; i < requiredFieldRules.Count; i++)
            {
                AudioRequiredFieldRule rule = requiredFieldRules[i];
                if (rule != null && rule.assetTypeName == assetTypeName)
                    return;
            }

            requiredFieldRules.Add(new AudioRequiredFieldRule
            {
                assetTypeName = assetTypeName,
                requiredObjectFieldNames = new List<string>(requiredFields ?? Array.Empty<string>()),
                required = true
            });
        }
    }

    [Serializable]
    public sealed class AudioCueBindingRule
    {
        [Tooltip("Cue enum/value name this binding covers.")]
        public string cueName;

        [Tooltip("Human-readable label for this source, such as 'Button click' or 'Objective completed'.")]
        public string displayName;

        [Tooltip("Optional authoring notes for designers or audio implementers.")]
        [TextArea(1, 4)] public string notes;

        [Tooltip("How this cue is linked to gameplay/UI/audio context.")]
        public AudioBindingSourceKind sourceKind = AudioBindingSourceKind.CSharpEventViaAdapter;

        [Tooltip("Component type that owns the event/state/source. Can be a full type name or short class name.")]
        public string sourceComponentTypeName;

        [Tooltip("Event, UnityEvent, method, field, property, animation event, or authored state name.")]
        public string sourceMemberName;

        [Tooltip("Optional adapter/binder component expected to bridge this source to audio playback.")]
        public string adapterComponentTypeName;

        [Tooltip("Optional companion component expected on the same prefab/object.")]
        public string requiredCompanionComponentTypeName;

        [Tooltip("What type of runtime trigger causes playback.")]
        public AudioBindingTriggerKind triggerKind = AudioBindingTriggerKind.CSharpEvent;

        [Tooltip("How the cue should be played by the runtime audio system.")]
        public AudioPlaybackMode playbackMode = AudioPlaybackMode.PlayAttached;

        [Tooltip("Where positional/attached playback should get its position or target transform.")]
        public AudioPositionSourceKind positionSource = AudioPositionSourceKind.SourceTransform;

        [Tooltip("Optional transform field/property/child name used by the selected position source.")]
        public string transformFieldOrPropertyName;

        [Tooltip("Where the coverage tools should validate this binding.")]
        public AudioBindingValidationScope validationScope = AudioBindingValidationScope.SceneAndPrefabs;

        [Tooltip("Required bindings are reported when missing or invalid.")]
        public bool required = true;

        [Tooltip("Allow more than one runtime source to cover this cue.")]
        public bool allowMultipleSources = true;

        [Tooltip("Ignore this binding in coverage warnings. Useful for deprecated or intentionally unused cues.")]
        public bool ignoreInCoverage = false;

        [Tooltip("Optional extra conditions that describe when this source should play.")]
        public List<AudioBindingCondition> conditions = new List<AudioBindingCondition>();
    }

    [Serializable]
    public sealed class AudioComponentExpectationRule
    {
        [Tooltip("If a scene object or prefab contains this component type...")]
        public string sourceComponentTypeName;

        [Tooltip("...then this component type is expected on the same object or its children. Leave empty to document a source without enforcing a required component.")]
        public string requiredComponentTypeName;

        [Tooltip("AssetDatabase filter used when scanning prefabs. Default: t:Prefab.")]
        public string prefabSearchFilter = "t:Prefab";

        [Tooltip("Where the setup coverage scanner should validate this expectation.")]
        public AudioBindingValidationScope validationScope = AudioBindingValidationScope.ProjectPrefabs;

        [Tooltip("If false, this rule is informational only.")]
        public bool required = true;

        [Tooltip("Optional authoring notes shown in setup coverage results.")]
        [TextArea(1, 3)] public string notes;
    }

    [Serializable]
    public sealed class AudioRequiredFieldRule
    {
        [Tooltip("ScriptableObject/config asset type to validate.")]
        public string assetTypeName;

        [Tooltip("Serialized object-reference or array/list fields that should be assigned/non-empty.")]
        public List<string> requiredObjectFieldNames = new List<string>();

        [Tooltip("If false, this rule is informational only.")]
        public bool required = true;

        [Tooltip("Optional authoring notes shown in setup coverage results.")]
        [TextArea(1, 3)] public string notes;
    }

    [Serializable]
    public sealed class AudioBindingCondition
    {
        public AudioBindingConditionKind conditionKind = AudioBindingConditionKind.NoteOnly;
        public string memberName;
        public string expectedValue;
        public float numericThreshold;
        [TextArea(1, 3)] public string notes;
    }

    public enum AudioBindingSourceKind
    {
        Component,
        UnityEvent,
        CSharpEventViaAdapter,
        AnimationEvent,
        CollisionEvent,
        TriggerEvent,
        ScriptReference,
        ManualCoverageOnly
    }

    public enum AudioBindingTriggerKind
    {
        OnEnable,
        OnDisable,
        UnityEvent,
        CSharpEvent,
        MethodCall,
        CollisionEnter,
        CollisionExit,
        TriggerEnter,
        TriggerExit,
        StateChanged,
        PolledCondition,
        Manual
    }

    public enum AudioPlaybackMode
    {
        PlayUi,
        PlayWorld,
        PlayAttached,
        StartLoop,
        StopLoop,
        OneShotAtPosition,
        OneShotOnSource
    }

    public enum AudioPositionSourceKind
    {
        None,
        SourceTransform,
        ParentTransform,
        ChildTransform,
        ExplicitFieldOrProperty,
        CollisionContactPoint,
        EventPayloadPosition
    }

    public enum AudioBindingValidationScope
    {
        None,
        OpenScenes,
        ProjectPrefabs,
        SelectedObjects,
        CatalogOnly,
        SceneAndPrefabs
    }

    public enum AudioBindingConditionKind
    {
        NoteOnly,
        BoolFieldTrue,
        BoolFieldFalse,
        EnumEquals,
        TagMatches,
        LayerMatches,
        NumericGreaterThan,
        NumericLessThan,
        ObjectReferenceAssigned
    }

}
