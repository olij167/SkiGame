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
        private void DrawApplyPanel()
        {
            EnsureApplyMappingsForTargets();

            using (BeginInspectorSection("Apply Palette", ApplyTint(), ApplyReadinessStatus(), PaletteDesignerSectionTone.Summary))
            {
                DrawInspectorBodyText("Map palette roles or specific swatches to scanned targets, omit anything that should stay untouched, then apply the included rows explicitly.");
                DrawApplyReadinessCard();
            }

            DrawApplyTargetToggles();

            using (BeginInspectorSection("Material Colour", UtilityWindowTheme.Neutral, _materialColorProperty, PaletteDesignerSectionTone.Detail))
            {
                _materialColorProperty = EditorGUILayout.TextField("Material Property", _materialColorProperty);
                _applyRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Auto Fallback Role", _applyRole);
                DrawInspectorBodyText("Auto mapping uses target names and component types first, then falls back to this role, the selected swatch, or the first palette colour.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (StudioButton("Scan Selection", ApplyTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                    ScanApplyTargets();

                using (new EditorGUI.DisabledScope(!CanApplyMappedTargets()))
                {
                    if (StudioButton("Apply Included", ApplyTint(), PaletteDesignerButtonTone.Primary, GUILayout.Width(132f), GUILayout.Height(26f)))
                        ApplyMappedPaletteToTargets();
                }
            }

            DrawApplyTargetMappings();
            DrawApplyReport();
        }

        private string ApplyReadinessStatus()
        {
            if (_applyTargets.Count == 0)
                return "not scanned";

            int included = CountIncludedApplyMappings();
            int resolved = CountResolvedApplyMappings();
            return $"{resolved}/{included} ready";
        }

        private void DrawApplyReadinessCard()
        {
            int included = CountIncludedApplyMappings();
            int resolved = CountResolvedApplyMappings();
            bool narrow = CurrentContentWidth() < 430f;
            if (narrow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_applyTargets.Count > 0 ? $"{_applyTargets.Count} scanned" : "No targets", _applyTargets.Count > 0 ? ApplyTint() : UtilityWindowTheme.Neutral, 98f);
                    UtilityWindowTheme.CountPill($"{included} included", included > 0 ? ApplyTint() : UtilityWindowTheme.Neutral, 94f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"{resolved} mapped", resolved == included && included > 0 ? ApplyTint() : UtilityWindowTheme.Amber, 94f);
                    UtilityWindowTheme.CountPill(_modifySharedMaterials ? "Shared materials" : "Instance safe", _modifySharedMaterials ? UtilityWindowTheme.Amber : ApplyTint(), 120f);
                    GUILayout.FlexibleSpace();
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill(_applyTargets.Count > 0 ? $"{_applyTargets.Count} scanned" : "No targets", _applyTargets.Count > 0 ? ApplyTint() : UtilityWindowTheme.Neutral, 98f);
                    UtilityWindowTheme.CountPill($"{included} included", included > 0 ? ApplyTint() : UtilityWindowTheme.Neutral, 94f);
                    UtilityWindowTheme.CountPill($"{resolved} mapped", resolved == included && included > 0 ? ApplyTint() : UtilityWindowTheme.Amber, 94f);
                    UtilityWindowTheme.CountPill(_modifySharedMaterials ? "Shared materials" : "Instance safe", _modifySharedMaterials ? UtilityWindowTheme.Amber : ApplyTint(), 120f);
                    GUILayout.FlexibleSpace();
                }
            }

            DrawInspectorBodyText(_lastApplyScanSummary);
        }

        private void DrawApplyTargetToggles()
        {
            bool compact = CurrentContentWidth() < 520f;
            using (BeginInspectorSection("Scan Scope", UtilityWindowTheme.Neutral, _modifySharedMaterials ? "shared materials" : "safe default", PaletteDesignerSectionTone.Detail))
            {
                EditorGUI.BeginChangeCheck();
                if (compact)
                {
                    _applyRenderers = EditorGUILayout.ToggleLeft("Renderer materials", _applyRenderers);
                    _applySelectedMaterials = EditorGUILayout.ToggleLeft("Selected material assets", _applySelectedMaterials);
                    _applySpriteRenderers = EditorGUILayout.ToggleLeft("Sprite renderers", _applySpriteRenderers);
                    _applyUiGraphics = EditorGUILayout.ToggleLeft("UI Graphics", _applyUiGraphics);
                    _applyTmpText = EditorGUILayout.ToggleLeft("TMP Text", _applyTmpText);
                    _modifySharedMaterials = EditorGUILayout.ToggleLeft("Shared materials", _modifySharedMaterials);
                    HandleApplyScopeChanged();
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _applyRenderers = EditorGUILayout.ToggleLeft("Renderers", _applyRenderers, GUILayout.Width(86f));
                    _applySelectedMaterials = EditorGUILayout.ToggleLeft("Materials", _applySelectedMaterials, GUILayout.Width(86f));
                    _applySpriteRenderers = EditorGUILayout.ToggleLeft("Sprites", _applySpriteRenderers, GUILayout.Width(72f));
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _applyUiGraphics = EditorGUILayout.ToggleLeft("UI Graphics", _applyUiGraphics, GUILayout.Width(96f));
                    _applyTmpText = EditorGUILayout.ToggleLeft("TMP Text", _applyTmpText, GUILayout.Width(82f));
                    _modifySharedMaterials = EditorGUILayout.ToggleLeft("Shared materials", _modifySharedMaterials, GUILayout.Width(124f));
                    GUILayout.FlexibleSpace();
                }

                HandleApplyScopeChanged();
            }
        }

        private void HandleApplyScopeChanged()
        {
            if (!EditorGUI.EndChangeCheck())
                return;

            if (_applyTargets.Count > 0)
            {
                _applyTargets.Clear();
                _applyMappings.Clear();
                _lastApplyReport = null;
                _lastApplyScanSummary = "Scan needed after scope changed.";
                _lastStatus = "Apply scope changed. Scan selection again.";
            }
        }

        private PaletteSwatch ResolveApplySwatch()
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return null;

            PaletteSwatch applySwatch = PaletteAnalysisUtility.FindRole(_activePalette.swatches, _applyRole);
            if (applySwatch == null && IsValidSelectedSwatch())
                applySwatch = _activePalette.swatches[_selectedSwatch];
            if (applySwatch == null)
                applySwatch = FirstAvailableApplySwatch();

            return applySwatch;
        }

        private bool CanApplyCurrentSwatch()
        {
            return CanApplyMappedTargets();
        }

        private void ScanApplyTargets()
        {
            var previous = CaptureApplyMappingState();
            _applyTargets.Clear();
            _applyTargets.AddRange(PaletteApplyUtility.ScanSelection(_applyRenderers, _applySelectedMaterials, _applySpriteRenderers, _applyUiGraphics, _applyTmpText));
            _applyMappings.Clear();
            for (int i = 0; i < _applyTargets.Count; i++)
            {
                PaletteApplyTarget target = _applyTargets[i];
                string key = GetApplyTargetKey(target);
                PaletteApplyTargetMappingState mapping;
                if (!string.IsNullOrEmpty(key) && previous.TryGetValue(key, out PaletteApplyTargetMappingState saved))
                    mapping = CopyApplyMappingState(saved);
                else
                    mapping = CreateDefaultApplyMapping(target);

                mapping.key = key;
                _applyMappings.Add(mapping);
            }

            _lastApplyReport = null;
            _showApply = true;
            FocusWorkflowInspector(PaletteOverlayKind.Apply);
            _lastApplyScanSummary = _applyTargets.Count > 0
                ? $"Last scan found {_applyTargets.Count} target(s). Scope: {CurrentApplyScopeSummary()}."
                : $"Last scan found no targets. Scope: {CurrentApplyScopeSummary()}.";
            _lastStatus = _applyTargets.Count > 0 ? $"Scanned {_applyTargets.Count} apply targets." : "No apply targets found in the current selection.";
        }

        private Dictionary<string, PaletteApplyTargetMappingState> CaptureApplyMappingState()
        {
            var result = new Dictionary<string, PaletteApplyTargetMappingState>();
            for (int i = 0; i < _applyMappings.Count; i++)
            {
                PaletteApplyTargetMappingState mapping = _applyMappings[i];
                if (mapping == null || string.IsNullOrEmpty(mapping.key))
                    continue;
                result[mapping.key] = CopyApplyMappingState(mapping);
            }
            return result;
        }

        private PaletteApplyTargetMappingState CopyApplyMappingState(PaletteApplyTargetMappingState source)
        {
            return new PaletteApplyTargetMappingState
            {
                key = source.key,
                included = source.included,
                mode = source.mode,
                role = source.role,
                swatchIndex = source.swatchIndex
            };
        }

        private PaletteApplyTargetMappingState CreateDefaultApplyMapping(PaletteApplyTarget target)
        {
            return new PaletteApplyTargetMappingState
            {
                key = GetApplyTargetKey(target),
                included = true,
                mode = PaletteApplyMappingMode.Auto,
                role = SuggestApplyRoleForTarget(target),
                swatchIndex = IsValidSelectedSwatch() ? _selectedSwatch : 0
            };
        }

        private void EnsureApplyMappingsForTargets()
        {
            if (_applyMappings.Count == _applyTargets.Count)
                return;

            var previous = CaptureApplyMappingState();
            _applyMappings.Clear();
            for (int i = 0; i < _applyTargets.Count; i++)
            {
                PaletteApplyTarget target = _applyTargets[i];
                string key = GetApplyTargetKey(target);
                PaletteApplyTargetMappingState mapping;
                if (!string.IsNullOrEmpty(key) && previous.TryGetValue(key, out PaletteApplyTargetMappingState saved))
                    mapping = CopyApplyMappingState(saved);
                else
                    mapping = CreateDefaultApplyMapping(target);

                mapping.key = key;
                _applyMappings.Add(mapping);
            }
        }

        private string CurrentApplyScopeSummary()
        {
            var parts = new List<string>();
            if (_applyRenderers)
                parts.Add("renderers");
            if (_applySelectedMaterials)
                parts.Add("materials");
            if (_applySpriteRenderers)
                parts.Add("sprites");
            if (_applyUiGraphics)
                parts.Add("UI");
            if (_applyTmpText)
                parts.Add("TMP");

            string scope = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "nothing selected for scanning";
            return _modifySharedMaterials ? scope + "; shared material writes enabled" : scope + "; renderer writes use instances";
        }

        private void DrawApplyTargetMappings()
        {
            if (_applyTargets.Count == 0)
            {
                DrawStudioHelpCard("Scan before applying", "Select scene objects or material assets, then scan to build a target mapping list.", ApplyTint());
                return;
            }

            using (BeginInspectorSection("Target Mapping", ApplyTint(), $"{_applyTargets.Count} target(s)", PaletteDesignerSectionTone.Primary))
            {
                DrawInspectorBodyText("Each included row resolves a palette colour independently. Use Omit for targets that should stay untouched.");
                for (int i = 0; i < _applyTargets.Count; i++)
                    DrawApplyTargetMappingRow(i);
            }
        }

        private void DrawApplyTargetMappingRow(int index)
        {
            if (index < 0 || index >= _applyTargets.Count || index >= _applyMappings.Count)
                return;

            PaletteApplyTarget target = _applyTargets[index];
            PaletteApplyTargetMappingState mapping = _applyMappings[index];
            PaletteSwatch swatch = ResolveApplyMappingSwatch(mapping, target, out PaletteSwatchRole resolvedRole, out string status);
            bool resolved = swatch != null;
            Color tint = !mapping.included ? UtilityWindowTheme.Neutral : resolved ? ApplyTint() : UtilityWindowTheme.Amber;

            using (BeginStudioCard(null, tint, null, 0.045f, 0.02f))
            {
                bool narrow = CurrentContentWidth() < 520f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    mapping.included = EditorGUILayout.ToggleLeft(mapping.included ? "Include" : "Omit", mapping.included, GUILayout.Width(78f));
                    EditorGUILayout.ObjectField(target != null ? target.targetObject : null, typeof(Object), true);
                }

                DrawInspectorBodyText(target != null ? target.description : "Missing target.");

                if (narrow)
                {
                    DrawApplyMappingControls(mapping);
                    DrawResolvedApplyColour(swatch, resolvedRole, status);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawApplyMappingControls(mapping);
                        GUILayout.FlexibleSpace();
                        DrawResolvedApplyColour(swatch, resolvedRole, status);
                    }
                }
            }
        }

        private void DrawApplyMappingControls(PaletteApplyTargetMappingState mapping)
        {
            bool narrow = CurrentContentWidth() < 520f;
            float stackedWidth = Mathf.Max(160f, CurrentContentWidth() - 22f);
            EditorGUI.BeginChangeCheck();
            if (narrow)
            {
                mapping.mode = (PaletteApplyMappingMode)EditorGUILayout.EnumPopup(mapping.mode, GUILayout.Width(stackedWidth));
                if (mapping.mode == PaletteApplyMappingMode.Role)
                    mapping.role = (PaletteSwatchRole)EditorGUILayout.EnumPopup(mapping.role, GUILayout.Width(stackedWidth));
                else if (mapping.mode == PaletteApplyMappingMode.Swatch)
                    mapping.swatchIndex = EditorGUILayout.Popup(Mathf.Clamp(mapping.swatchIndex, 0, Mathf.Max(0, ApplySwatchOptionCount() - 1)), ApplySwatchOptions(), GUILayout.Width(stackedWidth));
            }
            else
            {
                mapping.mode = (PaletteApplyMappingMode)EditorGUILayout.EnumPopup(mapping.mode, GUILayout.Width(84f));
                if (mapping.mode == PaletteApplyMappingMode.Role)
                    mapping.role = (PaletteSwatchRole)EditorGUILayout.EnumPopup(mapping.role, GUILayout.Width(126f));
                else if (mapping.mode == PaletteApplyMappingMode.Swatch)
                    mapping.swatchIndex = EditorGUILayout.Popup(Mathf.Clamp(mapping.swatchIndex, 0, Mathf.Max(0, ApplySwatchOptionCount() - 1)), ApplySwatchOptions(), GUILayout.Width(138f));
            }
            if (EditorGUI.EndChangeCheck())
            {
                _lastApplyReport = null;
                Repaint();
            }
        }

        private void DrawResolvedApplyColour(PaletteSwatch swatch, PaletteSwatchRole resolvedRole, string status)
        {
            if (swatch == null)
            {
                UtilityWindowTheme.CountPill("No colour", UtilityWindowTheme.Amber, 82f);
                return;
            }

            DrawColourChip(swatch.color, 30f, 18f);
            string label = string.IsNullOrWhiteSpace(status) ? Nicify(resolvedRole.ToString()) : status;
            UtilityWindowTheme.CountPill(label, ApplyTint(), Mathf.Min(130f, Mathf.Max(82f, CurrentContentWidth() * 0.28f)));
        }

        private bool CanApplyMappedTargets()
        {
            if (_applyTargets.Count == 0)
                return false;

            return CountResolvedApplyMappings() > 0;
        }

        private int CountIncludedApplyMappings()
        {
            EnsureApplyMappingsForTargets();
            int count = 0;
            for (int i = 0; i < _applyMappings.Count; i++)
            {
                if (_applyMappings[i] != null && _applyMappings[i].included)
                    count++;
            }
            return count;
        }

        private int CountResolvedApplyMappings()
        {
            EnsureApplyMappingsForTargets();
            int count = 0;
            for (int i = 0; i < _applyTargets.Count && i < _applyMappings.Count; i++)
            {
                PaletteApplyTargetMappingState mapping = _applyMappings[i];
                if (mapping == null || !mapping.included)
                    continue;
                if (ResolveApplyMappingSwatch(mapping, _applyTargets[i], out PaletteSwatchRole _, out string _) != null)
                    count++;
            }
            return count;
        }

        private void ApplyCurrentSwatchToTargets()
        {
            ApplyMappedPaletteToTargets();
        }

        private void ApplyMappedPaletteToTargets()
        {
            if (_applyTargets.Count == 0)
                return;

            List<PaletteApplyResolvedTarget> resolvedTargets = BuildResolvedApplyTargets();
            int included = 0;
            for (int i = 0; i < resolvedTargets.Count; i++)
            {
                if (resolvedTargets[i] != null && resolvedTargets[i].included)
                    included++;
            }

            if (included == 0)
            {
                _lastStatus = "No included apply targets.";
                return;
            }

            string message = _modifySharedMaterials
                ? $"This will apply mapped palette colours to {included} included target(s), modifying shared material references where renderer targets use shared materials. This can affect other objects using those materials."
                : $"This will apply mapped palette colours to {included} included target(s). Renderer materials use instance materials unless shared-material modification is enabled.";

            if (!EditorUtility.DisplayDialog("Apply Palette Mapping", message, "Apply Included", "Cancel"))
                return;

            _lastApplyReport = PaletteApplyUtility.ApplyMappedColorsDetailed(resolvedTargets, _modifySharedMaterials, _materialColorProperty);
            _lastStatus = $"Applied palette mapping. {_lastApplyReport.Summary}";
        }

        private List<PaletteApplyResolvedTarget> BuildResolvedApplyTargets()
        {
            EnsureApplyMappingsForTargets();
            var result = new List<PaletteApplyResolvedTarget>();
            for (int i = 0; i < _applyTargets.Count; i++)
            {
                PaletteApplyTarget target = _applyTargets[i];
                PaletteApplyTargetMappingState mapping = i < _applyMappings.Count ? _applyMappings[i] : null;
                PaletteSwatch swatch = ResolveApplyMappingSwatch(mapping, target, out PaletteSwatchRole role, out string status);
                result.Add(new PaletteApplyResolvedTarget
                {
                    target = target,
                    included = mapping != null && mapping.included,
                    hasColor = swatch != null,
                    color = swatch != null ? swatch.color : Color.clear,
                    colorLabel = swatch != null ? $"{status} / {ColourConversionUtility.ToHexRGB(swatch.color)}" : string.Empty,
                    skipReason = swatch == null ? $"No swatch resolved for {Nicify(role.ToString())}." : null
                });
            }
            return result;
        }

        private PaletteSwatch ResolveApplyMappingSwatch(PaletteApplyTargetMappingState mapping, PaletteApplyTarget target, out PaletteSwatchRole resolvedRole, out string status)
        {
            resolvedRole = _applyRole;
            status = Nicify(_applyRole.ToString());
            if (_activePalette == null || _activePalette.swatches == null || mapping == null)
                return null;

            switch (mapping.mode)
            {
                case PaletteApplyMappingMode.Role:
                    resolvedRole = mapping.role;
                    status = Nicify(mapping.role.ToString());
                    return PaletteAnalysisUtility.FindRole(_activePalette.swatches, mapping.role);
                case PaletteApplyMappingMode.Swatch:
                    if (mapping.swatchIndex >= 0 && mapping.swatchIndex < _activePalette.swatches.Count)
                    {
                        PaletteSwatch swatch = _activePalette.swatches[mapping.swatchIndex];
                        if (swatch != null)
                        {
                            resolvedRole = swatch.role;
                            status = string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {mapping.swatchIndex + 1}" : swatch.name;
                            return swatch;
                        }
                    }
                    status = "Missing swatch";
                    return null;
                default:
                    resolvedRole = SuggestApplyRoleForTarget(target);
                    PaletteSwatch autoSwatch = PaletteAnalysisUtility.FindRole(_activePalette.swatches, resolvedRole);
                    if (autoSwatch != null)
                    {
                        status = Nicify(resolvedRole.ToString());
                        return autoSwatch;
                    }

                    PaletteSwatch fallback = ResolveApplySwatch();
                    if (fallback != null)
                    {
                        resolvedRole = fallback.role;
                        status = "Fallback";
                    }
                    return fallback;
            }
        }

        private PaletteSwatchRole SuggestApplyRoleForTarget(PaletteApplyTarget target)
        {
            if (target == null)
                return _applyRole;

            string text = ApplyTargetSearchText(target);
            if (ContainsAny(text, "muted", "secondary text", "subtle", "placeholder"))
                return PaletteSwatchRole.MutedText;
            if (target.kind == PaletteApplyTargetKind.TmpText || ContainsAny(text, " text ", "label", "caption", "title", "body", "tmp"))
                return PaletteSwatchRole.Text;
            if (ContainsAny(text, "background", "backdrop", "canvas", "page"))
                return PaletteSwatchRole.Background;
            if (ContainsAny(text, "panel", "surface", "card", "container", "window", "modal"))
                return PaletteSwatchRole.Panel;
            if (ContainsAny(text, "warning", "caution"))
                return PaletteSwatchRole.Warning;
            if (ContainsAny(text, "success", "valid", "positive"))
                return PaletteSwatchRole.Success;
            if (ContainsAny(text, "error", "danger", "invalid", "negative"))
                return PaletteSwatchRole.Error;
            if (ContainsAny(text, "outline", "border", "stroke"))
                return PaletteSwatchRole.Outline;
            if (ContainsAny(text, "highlight", "selected", "focus"))
                return PaletteSwatchRole.Highlight;
            return PaletteSwatchRole.Accent;
        }

        private string ApplyTargetSearchText(PaletteApplyTarget target)
        {
            string text = target.description ?? string.Empty;
            if (target.gameObject != null)
                text += " " + target.gameObject.name;
            if (target.targetObject != null)
                text += " " + target.targetObject.name;
            if (target.component != null)
                text += " " + target.component.GetType().Name;
            return " " + text.ToLowerInvariant() + " ";
        }

        private static bool ContainsAny(string source, params string[] values)
        {
            if (string.IsNullOrEmpty(source) || values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]) && source.Contains(values[i]))
                    return true;
            }
            return false;
        }

        private PaletteSwatch FirstAvailableApplySwatch()
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return null;

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                if (_activePalette.swatches[i] != null)
                    return _activePalette.swatches[i];
            }
            return null;
        }

        private string GetApplyTargetKey(PaletteApplyTarget target)
        {
            if (target == null || target.targetObject == null)
                return string.Empty;

            string objectId = GlobalObjectId.GetGlobalObjectIdSlow(target.targetObject).ToString();
            if (string.IsNullOrWhiteSpace(objectId))
                objectId = target.targetObject.name;
            return $"{target.kind}:{objectId}:{target.serializedColorProperty}:{target.description}";
        }

        private int ApplySwatchOptionCount()
        {
            return _activePalette != null && _activePalette.swatches != null ? Mathf.Max(1, _activePalette.swatches.Count) : 1;
        }

        private string[] ApplySwatchOptions()
        {
            if (_activePalette == null || _activePalette.swatches == null || _activePalette.swatches.Count == 0)
                return new[] { "No swatches" };

            var roleCounts = new Dictionary<PaletteSwatchRole, int>();
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch == null)
                    continue;

                if (!roleCounts.ContainsKey(swatch.role))
                    roleCounts[swatch.role] = 0;
                roleCounts[swatch.role]++;
            }

            var options = new string[_activePalette.swatches.Count];
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch == null)
                {
                    options[i] = $"Swatch {i + 1}";
                    continue;
                }

                string name = SanitiseApplyPopupLabel(string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {i + 1}" : swatch.name);
                string role = ApplyRolePopupGroupLabel(swatch.role);
                string hex = ColourConversionUtility.ToHexRGB(swatch.color);
                bool duplicateRole = roleCounts.TryGetValue(swatch.role, out int roleCount) && roleCount > 1;
                string item = $"{i + 1}. {name} - {role} - {hex}";
                options[i] = duplicateRole ? $"{role}/{i + 1}. {name} - {hex}" : item;
            }
            return options;
        }

        private string ApplyRolePopupGroupLabel(PaletteSwatchRole role)
        {
            switch (role)
            {
                case PaletteSwatchRole.None:
                    return "Unassigned";
                case PaletteSwatchRole.Custom:
                    return "Custom";
                default:
                    return SanitiseApplyPopupLabel(Nicify(role.ToString()));
            }
        }

        private static string SanitiseApplyPopupLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Swatch" : value.Replace("/", "-");
        }

        private void DrawApplyReport()
        {
            if (_lastApplyReport == null)
                return;

            using (BeginInspectorSection("Last Apply Summary", _lastApplyReport.failed > 0 ? UtilityWindowTheme.Amber : ApplyTint(), _lastApplyReport.failed > 0 ? "review" : "complete", PaletteDesignerSectionTone.Detail))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"Included {_lastApplyReport.included}", ApplyTint(), 92f);
                    UtilityWindowTheme.CountPill($"Omitted {_lastApplyReport.omitted}", UtilityWindowTheme.Neutral, 92f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"Changed {_lastApplyReport.changed}", ApplyTint(), 92f);
                    UtilityWindowTheme.CountPill($"Skipped {_lastApplyReport.skipped}", UtilityWindowTheme.Neutral, 92f);
                    GUILayout.FlexibleSpace();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    UtilityWindowTheme.CountPill($"Failed {_lastApplyReport.failed}", _lastApplyReport.failed > 0 ? UtilityWindowTheme.Red : UtilityWindowTheme.Neutral, 82f);
                    GUILayout.FlexibleSpace();
                }

                for (int i = 0; i < _lastApplyReport.messages.Count; i++)
                    DrawInspectorBodyText(_lastApplyReport.messages[i]);
            }
        }
    }
#endif
}
