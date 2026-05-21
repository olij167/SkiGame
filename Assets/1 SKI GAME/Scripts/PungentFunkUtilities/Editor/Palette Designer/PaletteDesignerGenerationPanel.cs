using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Colour;

namespace PungentFunk.Utilities.Editor.Colour
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public partial class PaletteDesignerWindow
    {
        private void DrawAdvancedGenerationPanel()
        {
            RefreshAutoContextIfNeeded();
            PaletteGenerationSuggestion suggestion = _autoContextSuggestion;
            PaletteGenerationSettings effective = suggestion != null && suggestion.effectiveSettings != null
                ? suggestion.effectiveSettings
                : BuildHybridBaseSettingsForSuggestion();

            using (BeginInspectorSection("Harmony", ControlsTint(), null, PaletteDesignerSectionTone.Primary))
            {
                DrawInspectorBodyText("Contextual generation is always active. Switch individual fields to Manual when you want to guide the next pass more directly.");

                DrawHybridGenerationControls(effective);

                if (suggestion != null)
                    DrawContextualHarmonyCard(suggestion);

                bool compact = CurrentContentWidth() < 760f;
                DrawGenerationSettingsFoldout(compact);
                DrawAdvancedGenerationActions(compact);
            }
        }

        private void DrawHybridGenerationControls(PaletteGenerationSettings effective)
        {
            if (effective == null)
                effective = BuildHybridBaseSettingsForSuggestion();

            DrawHybridHarmonyOverride(effective);
            DrawHybridCountOverride(effective);
            DrawHybridFloatOverride("Harmony", "How strongly generated swatches follow the selected harmony structure.", effective.harmonyInfluence, ref _overrideHarmonyInfluence, ref _manualHarmonyInfluenceEdited, ref _manualHarmonyInfluence);
            DrawHybridFloatOverride("Variation", "Widens or narrows the target hue bands and increases exploratory change.", effective.randomVariation, ref _overrideVariation, ref _manualVariationEdited, ref _manualVariation);
            DrawHybridFloatOverride("Contrast", "Biases generated foreground/surface separation and text readability.", effective.contrastInfluence, ref _overrideContrast, ref _manualContrastEdited, ref _manualContrast);
            DrawHybridRangeOverride("Hue", "Limits the hue region available to generated unlocked swatches.", effective.hueRange, ref _overrideHueRange, ref _manualHueRangeEdited, ref _manualHueRange);
            DrawHybridRangeOverride("Saturation", "Limits colour intensity for generated unlocked swatches.", effective.saturationRange, ref _overrideSaturationRange, ref _manualSaturationRangeEdited, ref _manualSaturationRange);
            DrawHybridRangeOverride("Value", "Limits light/dark values for generated unlocked swatches.", effective.valueRange, ref _overrideValueRange, ref _manualValueRangeEdited, ref _manualValueRange);
        }

        private void DrawHybridHarmonyOverride(PaletteGenerationSettings effective)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawOverrideToggle("Harmony", effective.harmonyMode.ToString(), ref _overrideHarmonyMode) && !_manualHarmonyModeEdited)
                    _manualHarmonyMode = effective.harmonyMode;

                using (new EditorGUI.DisabledScope(!_overrideHarmonyMode))
                {
                    EditorGUI.BeginChangeCheck();
                    ColourHarmonyMode next = (ColourHarmonyMode)EditorGUILayout.EnumPopup(_overrideHarmonyMode ? _manualHarmonyMode : effective.harmonyMode);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _manualHarmonyMode = next;
                        _manualHarmonyModeEdited = next != effective.harmonyMode;
                        MarkHybridControlsChanged("Updated harmony override.");
                    }
                }
            }
        }

        private void DrawHybridCountOverride(PaletteGenerationSettings effective)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawOverrideToggle("Count", $"{effective.targetSwatchCount}", ref _overrideTargetCount) && !_manualTargetCountEdited)
                    _manualTargetCount = effective.targetSwatchCount;

                using (new EditorGUI.DisabledScope(!_overrideTargetCount))
                {
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.IntSlider(_overrideTargetCount ? _manualTargetCount : effective.targetSwatchCount, 1, 32);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _manualTargetCount = next;
                        _manualTargetCountEdited = next != effective.targetSwatchCount;
                        MarkHybridControlsChanged("Updated count override.");
                    }
                }
            }
        }

        private void DrawHybridFloatOverride(string label, string tooltip, float autoValue, ref bool overrideEnabled, ref bool edited, ref float manualValue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawOverrideToggle(label, $"{autoValue:0.00}", ref overrideEnabled) && !edited)
                    manualValue = autoValue;

                using (new EditorGUI.DisabledScope(!overrideEnabled))
                {
                    EditorGUI.BeginChangeCheck();
                    float next = EditorGUILayout.Slider(new GUIContent(string.Empty, tooltip), overrideEnabled ? manualValue : autoValue, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        manualValue = next;
                        edited = !Mathf.Approximately(next, autoValue);
                        MarkHybridControlsChanged($"Updated {label.ToLowerInvariant()} override.");
                    }
                }
            }
        }

        private void DrawHybridRangeOverride(string label, string tooltip, Vector2 autoValue, ref bool overrideEnabled, ref bool edited, ref Vector2 manualValue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (DrawOverrideToggle(label, $"{autoValue.x:0.00}-{autoValue.y:0.00}", ref overrideEnabled) && !edited)
                    manualValue = autoValue;

                using (new EditorGUI.DisabledScope(!overrideEnabled))
                {
                    EditorGUI.BeginChangeCheck();
                    Vector2 next = DrawCompactRange(GUIContent.none, overrideEnabled ? manualValue : autoValue, 0f, 1f, tooltip);
                    if (EditorGUI.EndChangeCheck())
                    {
                        manualValue = next;
                        edited = !Approximately(next, autoValue);
                        MarkHybridControlsChanged($"Updated {label.ToLowerInvariant()} override.");
                    }
                }
            }
        }

        private bool DrawOverrideToggle(string label, string autoSummary, ref bool overrideEnabled)
        {
            float labelWidth = CurrentContentWidth() < 380f ? 106f : 132f;
            Rect labelRect = GUILayoutUtility.GetRect(labelWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(labelWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            Rect textRect = new Rect(labelRect.x, labelRect.y, Mathf.Max(48f, labelRect.width - 20f), labelRect.height);
            Rect toggleRect = new Rect(labelRect.xMax - 17f, labelRect.y + 1f, 16f, labelRect.height - 2f);
            string tooltip = overrideEnabled
                ? "Manual override is active. Disable to return this field to contextual generation."
                : $"Using contextual value: {autoSummary}. Enable to override this field.";
            GUI.Label(textRect, new GUIContent(label, tooltip), EditorStyles.label);

            bool wasEnabled = overrideEnabled;
            bool next = EditorGUI.Toggle(toggleRect, new GUIContent(string.Empty, tooltip), overrideEnabled);
            if (next != overrideEnabled)
            {
                overrideEnabled = next;
                MarkHybridControlsChanged(overrideEnabled ? $"{label} override enabled." : $"{label} returned to contextual.");
            }

            return overrideEnabled && !wasEnabled;
        }

        private Vector2 DrawCompactRange(GUIContent label, Vector2 value, float min, float max, string tooltip)
        {
            if (label != GUIContent.none)
                EditorGUILayout.LabelField(label, GUILayout.Width(78f));

            value.x = EditorGUILayout.FloatField(value.x, GUILayout.Width(46f));
            Rect slider = GUILayoutUtility.GetRect(40f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            GUIContent tooltipContent = new GUIContent(string.Empty, tooltip);
            EditorGUI.MinMaxSlider(slider, tooltipContent, ref value.x, ref value.y, min, max);
            value.y = EditorGUILayout.FloatField(value.y, GUILayout.Width(46f));

            value.x = Mathf.Clamp(value.x, min, max);
            value.y = Mathf.Clamp(value.y, min, max);
            if (value.y < value.x)
            {
                float temp = value.x;
                value.x = value.y;
                value.y = temp;
            }
            return value;
        }

        private bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
        }

        private void MarkHybridControlsChanged(string status)
        {
            MarkAutoContextDirty();
            _lastStatus = status;
            Repaint();
        }

        private void DrawGenerationSettingsFoldout(bool compact)
        {
            _showGenerationRuleSettings = EditorGUILayout.Foldout(_showGenerationRuleSettings, "Settings", true);
            if (!_showGenerationRuleSettings)
                return;

            EditorGUI.BeginChangeCheck();
            DrawGenerationRuleToggles();

            using (new EditorGUILayout.HorizontalScope())
            {
                _workingSettings.useSeed = EditorGUILayout.ToggleLeft("Seed", _workingSettings.useSeed, GUILayout.Width(56f));
                using (new EditorGUI.DisabledScope(!_workingSettings.useSeed))
                    _workingSettings.seed = EditorGUILayout.IntField(_workingSettings.seed, GUILayout.Width(92f));
                GUILayout.FlexibleSpace();
            }

            if (EditorGUI.EndChangeCheck())
            {
                _workingSettings.Clamp();
                MarkAutoContextDirty();
                _lastStatus = "Updated generation settings.";
            }
        }

        private void DrawGenerationRuleToggles()
        {
            float width = CurrentContentWidth();
            int columns = width > 500f ? 3 : width > 360f ? 2 : 1;
            float itemWidth = columns == 1 ? width - 18f : Mathf.Floor((width - 28f) / columns);
            string[] labels =
            {
                "Use locked anchors",
                "Enforce text contrast",
                "Avoid duplicates",
                "Readable accents"
            };

            bool[] values =
            {
                _workingSettings.preserveLockedSwatches,
                _workingSettings.enforceTextContrast,
                _workingSettings.avoidNearDuplicates,
                _workingSettings.preferReadableAccentPairs
            };

            for (int i = 0; i < labels.Length; i += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && i + column < labels.Length; column++)
                    {
                        int index = i + column;
                        values[index] = EditorGUILayout.ToggleLeft(labels[index], values[index], GUILayout.Width(itemWidth));
                    }
                    GUILayout.FlexibleSpace();
                }
            }

            _workingSettings.preserveLockedSwatches = values[0];
            _workingSettings.enforceTextContrast = values[1];
            _workingSettings.avoidNearDuplicates = values[2];
            _workingSettings.preferReadableAccentPairs = values[3];
        }

        private void DrawAdvancedGenerationActions(bool compact)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioButton("Tighten Ranges From Palette", ControlsTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(24f)))
                {
                    _workingSettings = PaletteGeneratorUtility.TightenRangesFromSwatches(_activePalette.swatches, _workingSettings);
                    _workingSettings.Clamp();
                    MarkAutoContextDirty();
                    _lastStatus = "Updated generation ranges from palette.";
                }

                if (!compact)
                    DrawRangePresetButtons();
            }

            if (compact)
                DrawRangePresetButtons();
        }

        private void DrawRangePresetButtons()
        {
            if (CurrentContentWidth() < 390f)
            {
                if (StudioButton("Reset Ranges", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Height(23f)))
                {
                    ResetGenerationRanges();
                    _lastStatus = "Reset generation ranges.";
                }

                if (StudioButton("Load Defaults", UtilityWindowTheme.Blue, PaletteDesignerButtonTone.Ghost, GUILayout.Height(23f)))
                {
                    _workingSettings = _activePalette.defaultGenerationSettings != null ? _activePalette.defaultGenerationSettings.Clone() : new PaletteGenerationSettings();
                    _workingSettings.Clamp();
                    MarkAutoContextDirty();
                    _lastStatus = "Loaded generation defaults.";
                }

                if (StudioButton("Save Defaults", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Secondary, GUILayout.Height(23f)))
                    SaveWorkingSettingsAsDefaults(true);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioButton("Reset Ranges", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Width(110f), GUILayout.Height(23f)))
                {
                    ResetGenerationRanges();
                    _lastStatus = "Reset generation ranges.";
                }

                if (StudioButton("Load Defaults", UtilityWindowTheme.Blue, PaletteDesignerButtonTone.Ghost, GUILayout.Width(108f), GUILayout.Height(23f)))
                {
                    _workingSettings = _activePalette.defaultGenerationSettings != null ? _activePalette.defaultGenerationSettings.Clone() : new PaletteGenerationSettings();
                    _workingSettings.Clamp();
                    MarkAutoContextDirty();
                    _lastStatus = "Loaded generation defaults.";
                }

                if (StudioButton("Save Defaults", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Secondary, GUILayout.Width(112f), GUILayout.Height(23f)))
                    SaveWorkingSettingsAsDefaults(true);
                GUILayout.FlexibleSpace();
            }
        }

        private void ResetGenerationRanges()
        {
            _workingSettings.hueRange = new Vector2(0f, 1f);
            _workingSettings.saturationRange = new Vector2(0.35f, 0.95f);
            _workingSettings.valueRange = new Vector2(0.18f, 0.95f);
            _workingSettings.Clamp();
            MarkAutoContextDirty();
        }

    }
#endif
}
