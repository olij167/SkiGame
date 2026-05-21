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
        private enum GuideContrastAdjustment
        {
            Foreground,
            Background,
            Both
        }

        private struct PaletteRoleSuggestion
        {
            public int index;
            public PaletteSwatchRole role;

            public PaletteRoleSuggestion(int index, PaletteSwatchRole role)
            {
                this.index = index;
                this.role = role;
            }
        }

        private struct PaletteContrastCandidate
        {
            public int foregroundIndex;
            public int backgroundIndex;
            public PaletteSwatchRole foregroundRole;
            public PaletteSwatchRole backgroundRole;
            public string label;
            public string source;
            public float contrast;
            public float target;
            public int priority;

            public bool IsValid
            {
                get { return foregroundIndex >= 0 && backgroundIndex >= 0 && foregroundIndex != backgroundIndex; }
            }
        }

        private static readonly PaletteSwatchRole[] GuideRoleOrder =
        {
            PaletteSwatchRole.Background,
            PaletteSwatchRole.Panel,
            PaletteSwatchRole.Text,
            PaletteSwatchRole.MutedText,
            PaletteSwatchRole.Accent,
            PaletteSwatchRole.AccentSecondary,
            PaletteSwatchRole.Highlight,
            PaletteSwatchRole.Warning,
            PaletteSwatchRole.Success,
            PaletteSwatchRole.Error,
            PaletteSwatchRole.Outline,
            PaletteSwatchRole.Shadow
        };

        private void DrawAnalysisPanel()
        {
            DrawRefineAccessibilityOverview();
            DrawRefineSuggestedFixCard();
            DrawRefineRoleCoverage();
            DrawRefineContrastWorkbench();
            if (IsValidSelectedSwatch())
            {
                using (BeginRefineSection("Selected Swatch Context", "selected", PaletteDesignerSectionTone.Detail))
                    DrawRefineSelectedSwatchMiniContext();
            }
            DrawRefineSimulationAndAllChecks();
        }

        private void DrawRefineAccessibilityOverview()
        {
            using (BeginRefineSection("Accessibility Snapshot", RefineOverviewStatus(), PaletteDesignerSectionTone.Summary))
            {
                PaletteContrastCandidate worst = FindWorstVisibleContrastCandidate();
                string worstLabel = worst.IsValid ? $"{worst.contrast:0.00}:1" : "n/a";
                Color worstTint = !worst.IsValid || worst.contrast >= worst.target ? UtilityWindowTheme.Green : worst.contrast >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;
                bool narrow = CurrentContentWidth() < 390f;
                if (narrow)
                {
                    DrawAnalysisSummaryPills();
                    UtilityWindowTheme.CountPill($"Worst {worstLabel}", worstTint, 92f);
                    UtilityWindowTheme.CountPill($"{CountAssignedRoles()} roles", UtilityWindowTheme.Neutral, 84f);
                    if (StudioButton("Refresh", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Secondary, GUILayout.Height(24f)))
                        RefreshGuideAnalysis();
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawAnalysisSummaryPills();
                        UtilityWindowTheme.CountPill($"Worst {worstLabel}", worstTint, 92f);
                        UtilityWindowTheme.CountPill($"{CountAssignedRoles()} roles", UtilityWindowTheme.Neutral, 84f);
                        GUILayout.FlexibleSpace();
                        if (StudioButton("Refresh", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Secondary, GUILayout.Width(82f), GUILayout.Height(24f)))
                            RefreshGuideAnalysis();
                    }
                }

                DrawPaletteRibbon(_activePalette.swatches, 18f);
                DrawInspectorBodyText(RefineOverviewMessage());
            }
        }

        private string RefineOverviewStatus()
        {
            if (_analysisReport == null)
                return "refresh";
            if (BuildRoleSuggestions(false).Count > 0)
                return "roles";
            if (AnalysisProblemCount() > 0)
                return $"{AnalysisProblemCount()} issue(s)";
            return "ready";
        }

        private string RefineOverviewMessage()
        {
            if (_analysisReport == null)
                return "Refresh analysis to evaluate role coverage, contrast, and colour-deficiency resilience.";

            int missingRoles = BuildRoleSuggestions(false).Count;
            if (missingRoles > 0)
                return "Start by assigning functional roles. Contrast checks become more useful once surfaces, text, and accents are identified.";
            if (AnalysisProblemCount() > 0)
                return "The palette needs contrast refinement. Locked swatches remain protected; actions below adjust unlocked colours only.";
            if (_analysisReport.recommendations.Count > 0)
                return "The main checks are usable. Review the suggested refinement or inspect detailed checks before applying.";
            return "No major accessibility issues detected. You can still tune roles, simulate deficiency modes, or inspect all checks.";
        }

        private void DrawRefineFindingLine(string label, string message, Color tint)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(label, tint, 54f);
                EditorGUILayout.LabelField(message, InspectorBodyStyle());
            }
        }

        private void DrawRefineSuggestedFixCard()
        {
            int missingRoles = BuildRoleSuggestions(false).Count;
            int issueCount = AnalysisProblemCount();
            PaletteSwatch background = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Background) ?? PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Panel);
            PaletteSwatch text = PaletteAnalysisUtility.FindRole(_activePalette.swatches, PaletteSwatchRole.Text);
            PaletteContrastCandidate worst = FindWorstActionableContrastCandidate();

            using (BeginRefineSection("Next Best Refinement", RefineOverviewStatus(), PaletteDesignerSectionTone.Primary))
            {
                if (missingRoles > 0)
                {
                    DrawInspectorBodyText($"{missingRoles} unlocked swatch(es) can receive palette roles. This changes role labels only; locked swatches are skipped.");
                    if (StudioButton("Assign Missing Roles", RefineTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(28f)))
                        ApplyRoleSuggestions(false);
                    return;
                }

                if (background == null)
                {
                    DrawInspectorBodyText("Add a neutral surface role so text and accent contrast can be evaluated against a real background.");
                    if (StudioButton("Add Neutral Panel", RefineTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(28f)))
                        AddOrRepairPanelRole();
                    return;
                }

                if (text == null)
                {
                    DrawInspectorBodyText("Add a readable text role for the current surface. Existing locked text roles are left untouched.");
                    if (StudioButton("Add Readable Text", RefineTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(28f)))
                        AddOrRepairTextRole();
                    return;
                }

                if (worst.IsValid)
                {
                    DrawInspectorBodyText($"{worst.label} is the weakest actionable pair at {worst.contrast:0.00}:1. Fixing it balances whichever side is unlocked; locked swatches are skipped.");
                    if (StudioButton("Fix Weakest Pair", RefineTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(28f)))
                        FixGuideContrastCandidate(worst);
                    return;
                }

                if (issueCount > 0)
                {
                    DrawInspectorBodyText("The remaining issues do not map to one safe unlocked pair. Improve text roles as the broadest low-risk repair.");
                    if (StudioButton("Improve Text Roles", RefineTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(28f)))
                        ImproveTextRoles();
                    return;
                }

                DrawInspectorBodyText("The palette is in a good state. Refresh after edits, or use the workbench below for targeted contrast tuning.");
                if (StudioButton("Refresh Analysis", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Height(24f)))
                    RefreshGuideAnalysis();
            }
        }

        private void DrawRefineContrastWorkbench()
        {
            using (BeginRefineSection("Contrast Workbench", $"{_guideTargetContrast:0.0}:1", PaletteDesignerSectionTone.Primary))
            {
                bool narrow = CurrentContentWidth() < 420f;
                PaletteContrastCandidate loaded = GetLoadedGuideContrastCandidate();
                DrawInspectorBodyText("Use a weak pair from the queue, then adjust the unlocked side. Manual Pair stays available for targeted tuning.");

                DrawWorstPairQueue();
                DrawActiveGuideContrastPreview();

                EditorGUI.BeginChangeCheck();
                _guideTargetContrast = EditorGUILayout.Slider("Target Contrast", Mathf.Clamp(_guideTargetContrast, 3f, 7f), 3f, 7f);
                if (EditorGUI.EndChangeCheck())
                    SavePrefs();

                if (!string.IsNullOrWhiteSpace(_guideLastContrastSummary))
                    DrawInspectorBodyText(_guideLastContrastSummary);

                if (narrow)
                {
                    DrawGuideContrastButton("Adjust Foreground", GuideContrastAdjustment.Foreground);
                    DrawGuideContrastButton("Adjust Background", GuideContrastAdjustment.Background);
                    DrawGuideContrastButton(loaded.IsValid ? "Balance Loaded Pair" : "Balance Pair", GuideContrastAdjustment.Both);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawGuideContrastButton("Adjust Foreground", GuideContrastAdjustment.Foreground);
                        DrawGuideContrastButton("Adjust Background", GuideContrastAdjustment.Background);
                        DrawGuideContrastButton(loaded.IsValid ? "Balance Loaded Pair" : "Balance Pair", GuideContrastAdjustment.Both);
                    }
                }

                _showGuideManualPair = EditorGUILayout.Foldout(_showGuideManualPair, "Manual Pair", true);
                if (_showGuideManualPair)
                    DrawManualGuideContrastControls();
            }
        }

        private void DrawWorstPairQueue()
        {
            List<PaletteContrastCandidate> candidates = BuildContrastCandidates(true);
            if (candidates.Count == 0)
            {
                DrawStudioHelpCard("No failing unlocked pairs", "Role pairs are either passing, incomplete, or protected by locked swatches. Manual Pair remains available below.", UtilityWindowTheme.Neutral);
                return;
            }

            EditorGUILayout.LabelField("Weakest Actionable Pairs", UtilityWindowTheme.SectionHeaderStyle);
            int max = Mathf.Min(3, candidates.Count);
            for (int i = 0; i < max; i++)
                DrawContrastCandidateRow(candidates[i], true);
        }

        private void DrawActiveGuideContrastPreview()
        {
            PaletteContrastCandidate loaded = GetLoadedGuideContrastCandidate();
            if (loaded.IsValid)
            {
                DrawContrastCandidatePreview(loaded, "Loaded Pair");
                return;
            }

            DrawGuideRolePairPreview(_guideForegroundRole, _guideBackgroundRole);
        }

        private void DrawManualGuideContrastControls()
        {
            bool narrow = CurrentContentWidth() < 420f;
            EditorGUI.BeginChangeCheck();
            if (narrow)
            {
                _guideForegroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Foreground", _guideForegroundRole);
                _guideBackgroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Background", _guideBackgroundRole);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _guideForegroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Foreground", _guideForegroundRole);
                    _guideBackgroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Background", _guideBackgroundRole);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                ClearLoadedGuideContrastCandidate();
                SavePrefs();
            }
        }

        private PaletteContrastCandidate FindWorstActionableContrastCandidate()
        {
            List<PaletteContrastCandidate> candidates = BuildContrastCandidates(true);
            return candidates.Count > 0 ? candidates[0] : default(PaletteContrastCandidate);
        }

        private PaletteContrastCandidate FindWorstVisibleContrastCandidate()
        {
            List<PaletteContrastCandidate> candidates = BuildContrastCandidates(false);
            return candidates.Count > 0 ? candidates[0] : default(PaletteContrastCandidate);
        }

        private List<PaletteContrastCandidate> BuildContrastCandidates(bool actionableOnly)
        {
            var candidates = new List<PaletteContrastCandidate>();
            if (_activePalette == null || _activePalette.swatches == null)
                return candidates;

            AddRoleContrastCandidate(candidates, PaletteSwatchRole.Text, PaletteSwatchRole.Background, 4.5f, "Text on Background", "Role pair", 0, actionableOnly);
            AddRoleContrastCandidate(candidates, PaletteSwatchRole.Text, PaletteSwatchRole.Panel, 4.5f, "Text on Panel", "Role pair", 0, actionableOnly);
            AddRoleContrastCandidate(candidates, PaletteSwatchRole.MutedText, PaletteSwatchRole.Background, 3f, "Muted Text on Background", "Role pair", 0, actionableOnly);
            AddRoleContrastCandidate(candidates, PaletteSwatchRole.MutedText, PaletteSwatchRole.Panel, 3f, "Muted Text on Panel", "Role pair", 0, actionableOnly);
            AddRoleContrastCandidate(candidates, PaletteSwatchRole.Text, PaletteSwatchRole.Warning, 4.5f, "Text on Warning", "Role pair", 0, actionableOnly);
            AddRoleContrastCandidate(candidates, PaletteSwatchRole.Text, PaletteSwatchRole.Success, 4.5f, "Text on Success", "Role pair", 0, actionableOnly);
            AddRoleContrastCandidate(candidates, PaletteSwatchRole.Text, PaletteSwatchRole.Error, 4.5f, "Text on Error", "Role pair", 0, actionableOnly);

            AddSelectedContrastCandidates(candidates, actionableOnly);

            if (_analysisReport != null)
            {
                for (int i = 0; i < _analysisReport.rolePairs.Count; i++)
                    AddAnalysisContrastCandidate(candidates, _analysisReport.rolePairs[i], "Role check", 2, actionableOnly);

                int max = Mathf.Min(12, _analysisReport.pairs.Count);
                for (int i = 0; i < max; i++)
                    AddAnalysisContrastCandidate(candidates, _analysisReport.pairs[i], "Palette pair", 3, actionableOnly);
            }

            candidates.Sort((a, b) =>
            {
                int priority = a.priority.CompareTo(b.priority);
                if (priority != 0)
                    return priority;

                float deficitA = Mathf.Max(0f, a.target - a.contrast);
                float deficitB = Mathf.Max(0f, b.target - b.contrast);
                int deficit = deficitB.CompareTo(deficitA);
                if (deficit != 0)
                    return deficit;

                return a.contrast.CompareTo(b.contrast);
            });

            return candidates;
        }

        private void AddRoleContrastCandidate(List<PaletteContrastCandidate> candidates, PaletteSwatchRole foregroundRole, PaletteSwatchRole backgroundRole, float target, string label, string source, int priority, bool actionableOnly)
        {
            int foregroundIndex = FindRoleIndex(foregroundRole);
            int backgroundIndex = FindRoleIndex(backgroundRole);
            TryAddContrastCandidate(candidates, foregroundIndex, backgroundIndex, target, label, source, priority, actionableOnly);
        }

        private void AddSelectedContrastCandidates(List<PaletteContrastCandidate> candidates, bool actionableOnly)
        {
            if (!IsValidSelectedSwatch())
                return;

            int selectedIndex = _selectedSwatch;
            PaletteSwatch selected = _activePalette.swatches[selectedIndex];
            AddSelectedFoundationCandidate(candidates, selectedIndex, selected, PaletteSwatchRole.Background, actionableOnly);
            AddSelectedFoundationCandidate(candidates, selectedIndex, selected, PaletteSwatchRole.Panel, actionableOnly);
            AddSelectedFoundationCandidate(candidates, selectedIndex, selected, PaletteSwatchRole.Text, actionableOnly);
            AddSelectedFoundationCandidate(candidates, selectedIndex, selected, PaletteSwatchRole.MutedText, actionableOnly);
        }

        private void AddSelectedFoundationCandidate(List<PaletteContrastCandidate> candidates, int selectedIndex, PaletteSwatch selected, PaletteSwatchRole role, bool actionableOnly)
        {
            int otherIndex = FindRoleIndex(role);
            if (selected == null || otherIndex < 0 || otherIndex == selectedIndex)
                return;

            PaletteSwatch other = _activePalette.swatches[otherIndex];
            int foregroundIndex = selectedIndex;
            int backgroundIndex = otherIndex;
            PaletteSwatchRole foregroundRole = selected.role;
            PaletteSwatchRole backgroundRole = role;

            if (IsSurfaceContrastRole(selected.role) && IsTextContrastRole(role))
            {
                foregroundIndex = otherIndex;
                backgroundIndex = selectedIndex;
                foregroundRole = role;
                backgroundRole = selected.role;
            }

            float target = ContrastTargetForRoles(foregroundRole, backgroundRole, null);
            string label = $"{SafeSwatchName(_activePalette.swatches[foregroundIndex], foregroundIndex)} on {SafeSwatchName(_activePalette.swatches[backgroundIndex], backgroundIndex)}";
            TryAddContrastCandidate(candidates, foregroundIndex, backgroundIndex, target, label, "Selected swatch", 1, actionableOnly);
        }

        private void AddAnalysisContrastCandidate(List<PaletteContrastCandidate> candidates, PaletteContrastPair pair, string source, int priority, bool actionableOnly)
        {
            if (pair == null)
                return;

            int foregroundIndex;
            int backgroundIndex;
            if (!TryResolveAnalysisPair(pair, source == "Role check", out foregroundIndex, out backgroundIndex))
                return;

            PaletteSwatch foreground = _activePalette.swatches[foregroundIndex];
            PaletteSwatch background = _activePalette.swatches[backgroundIndex];
            if (!IsTextContrastRole(foreground.role) && IsTextContrastRole(background.role))
            {
                int swap = foregroundIndex;
                foregroundIndex = backgroundIndex;
                backgroundIndex = swap;
                foreground = _activePalette.swatches[foregroundIndex];
                background = _activePalette.swatches[backgroundIndex];
            }

            float target = ContrastTargetForRoles(foreground.role, background.role, pair.label);
            string label = source == "Role check" ? pair.label : $"{SafeSwatchName(foreground, foregroundIndex)} on {SafeSwatchName(background, backgroundIndex)}";
            TryAddContrastCandidate(candidates, foregroundIndex, backgroundIndex, target, label, source, priority, actionableOnly);
        }

        private bool TryAddContrastCandidate(List<PaletteContrastCandidate> candidates, int foregroundIndex, int backgroundIndex, float target, string label, string source, int priority, bool actionableOnly)
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return false;
            if (foregroundIndex < 0 || backgroundIndex < 0 || foregroundIndex >= _activePalette.swatches.Count || backgroundIndex >= _activePalette.swatches.Count || foregroundIndex == backgroundIndex)
                return false;

            PaletteSwatch foreground = _activePalette.swatches[foregroundIndex];
            PaletteSwatch background = _activePalette.swatches[backgroundIndex];
            if (foreground == null || background == null)
                return false;
            if (actionableOnly && foreground.locked && background.locked)
                return false;

            float contrast = ColourContrastUtility.GetContrastRatio(foreground.color, background.color);
            if (contrast >= target)
                return false;
            if (ContainsContrastCandidate(candidates, foregroundIndex, backgroundIndex))
                return false;

            candidates.Add(new PaletteContrastCandidate
            {
                foregroundIndex = foregroundIndex,
                backgroundIndex = backgroundIndex,
                foregroundRole = foreground.role,
                backgroundRole = background.role,
                label = string.IsNullOrWhiteSpace(label) ? $"{SafeSwatchName(foreground, foregroundIndex)} on {SafeSwatchName(background, backgroundIndex)}" : label,
                source = source,
                contrast = contrast,
                target = target,
                priority = priority
            });
            return true;
        }

        private bool ContainsContrastCandidate(List<PaletteContrastCandidate> candidates, int foregroundIndex, int backgroundIndex)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                PaletteContrastCandidate candidate = candidates[i];
                if ((candidate.foregroundIndex == foregroundIndex && candidate.backgroundIndex == backgroundIndex) ||
                    (candidate.foregroundIndex == backgroundIndex && candidate.backgroundIndex == foregroundIndex))
                    return true;
            }

            return false;
        }

        private bool TryResolveAnalysisPair(PaletteContrastPair pair, bool rolePair, out int foregroundIndex, out int backgroundIndex)
        {
            foregroundIndex = -1;
            backgroundIndex = -1;
            if (pair == null || _activePalette == null || _activePalette.swatches == null)
                return false;

            if (rolePair && IsFunctionalRole(pair.foregroundRole) && IsFunctionalRole(pair.backgroundRole))
            {
                foregroundIndex = FindRoleIndex(pair.foregroundRole);
                backgroundIndex = FindRoleIndex(pair.backgroundRole);
                return foregroundIndex >= 0 && backgroundIndex >= 0 && foregroundIndex != backgroundIndex;
            }

            foregroundIndex = FindUniqueSwatchIndex(pair.label, pair.foregroundRole);
            backgroundIndex = FindUniqueSwatchIndex(pair.otherLabel, pair.backgroundRole);
            return foregroundIndex >= 0 && backgroundIndex >= 0 && foregroundIndex != backgroundIndex;
        }

        private int FindRoleIndex(PaletteSwatchRole role)
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return -1;

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch != null && swatch.role == role)
                    return i;
            }

            return -1;
        }

        private int FindUniqueSwatchIndex(string name, PaletteSwatchRole role)
        {
            if (_activePalette == null || _activePalette.swatches == null || string.IsNullOrWhiteSpace(name))
                return -1;

            int match = -1;
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch == null)
                    continue;
                if (swatch.role != role)
                    continue;
                if (!string.Equals(swatch.name, name, System.StringComparison.Ordinal))
                    continue;

                if (match >= 0)
                    return -1;
                match = i;
            }

            return match;
        }

        private static bool IsTextContrastRole(PaletteSwatchRole role)
        {
            return role == PaletteSwatchRole.Text || role == PaletteSwatchRole.MutedText;
        }

        private static bool IsSurfaceContrastRole(PaletteSwatchRole role)
        {
            return role == PaletteSwatchRole.Background || role == PaletteSwatchRole.Panel || role == PaletteSwatchRole.Warning || role == PaletteSwatchRole.Success || role == PaletteSwatchRole.Error;
        }

        private static float ContrastTargetForRoles(PaletteSwatchRole foregroundRole, PaletteSwatchRole backgroundRole, string label)
        {
            if (foregroundRole == PaletteSwatchRole.MutedText || (!string.IsNullOrEmpty(label) && label.Contains("Muted Text")))
                return 3f;
            if (foregroundRole == PaletteSwatchRole.Text || backgroundRole == PaletteSwatchRole.Text)
                return 4.5f;
            return 3f;
        }

        private string SafeSwatchName(PaletteSwatch swatch, int index)
        {
            if (swatch == null || string.IsNullOrWhiteSpace(swatch.name))
                return $"Swatch {index + 1}";
            return swatch.name;
        }

        private void DrawContrastCandidateRow(PaletteContrastCandidate candidate, bool showUseButton)
        {
            if (!candidate.IsValid)
                return;

            Color tint = candidate.contrast < 3f ? UtilityWindowTheme.Red : UtilityWindowTheme.Amber;
            bool active = IsLoadedGuideContrastCandidate(candidate);
            using (new EditorGUILayout.VerticalScope())
            {
                if (CurrentContentWidth() < 430f)
                {
                    EditorGUILayout.LabelField(candidate.label, InspectorBodyStyle());
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill($"{candidate.contrast:0.00}:1", tint, 66f);
                        UtilityWindowTheme.CountPill(candidate.source, UtilityWindowTheme.Neutral, 92f);
                        GUILayout.FlexibleSpace();
                        if (showUseButton)
                            DrawUseContrastCandidateButton(candidate, active, 76f);
                    }
                    DrawStudioDivider(active ? RefineTint() : UtilityWindowTheme.Neutral, active ? 0.46f : 0.16f);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(candidate.label, InspectorBodyStyle(), GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"{candidate.contrast:0.00}:1", tint, 66f);
                    UtilityWindowTheme.CountPill(candidate.source, UtilityWindowTheme.Neutral, 92f);
                    if (showUseButton)
                        DrawUseContrastCandidateButton(candidate, active, 76f);
                }
                DrawStudioDivider(active ? RefineTint() : UtilityWindowTheme.Neutral, active ? 0.46f : 0.16f);
            }
        }

        private void DrawUseContrastCandidateButton(PaletteContrastCandidate candidate, bool active, float width)
        {
            using (new EditorGUI.DisabledScope(active))
            {
                if (StudioButton(active ? "Loaded" : "Use Pair", active ? UtilityWindowTheme.Neutral : RefineTint(), active ? PaletteDesignerButtonTone.Ghost : PaletteDesignerButtonTone.Secondary, GUILayout.Width(width), GUILayout.Height(22f)))
                    LoadGuideContrastCandidate(candidate);
            }
        }

        private void DrawContrastCandidatePreview(PaletteContrastCandidate candidate, string title)
        {
            PaletteSwatch foreground = GetSwatch(candidate.foregroundIndex);
            PaletteSwatch background = GetSwatch(candidate.backgroundIndex);
            if (foreground == null || background == null)
                return;

            Color tint = candidate.contrast >= candidate.target ? UtilityWindowTheme.Green : candidate.contrast >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;
            DrawStudioDivider(RefineTint(), 0.3f);
            using (new EditorGUILayout.VerticalScope())
            {
                if (CurrentContentWidth() < 430f)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawColourChip(foreground.color, 30f, 20f);
                        DrawColourChip(background.color, 30f, 20f);
                        UtilityWindowTheme.CountPill($"{candidate.contrast:0.00}:1", tint, 70f);
                        GUILayout.FlexibleSpace();
                    }
                    EditorGUILayout.LabelField(title, UtilityWindowTheme.MutedMiniLabelStyle);
                    EditorGUILayout.LabelField(candidate.label, InspectorBodyStyle());
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawColourChip(foreground.color, 30f, 20f);
                        DrawColourChip(background.color, 30f, 20f);
                        EditorGUILayout.LabelField(title, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(78f));
                        EditorGUILayout.LabelField(candidate.label, InspectorBodyStyle());
                        GUILayout.FlexibleSpace();
                        UtilityWindowTheme.CountPill($"{candidate.contrast:0.00}:1", tint, 70f);
                    }
                }

                DrawInspectorBodyText($"Target {candidate.target:0.0}:1. {LockSummary(foreground, background)}");
            }
            DrawStudioDivider(RefineTint(), 0.18f);
        }

        private PaletteContrastCandidate GetLoadedGuideContrastCandidate()
        {
            if (_guideForegroundIndex < 0 || _guideBackgroundIndex < 0)
                return default(PaletteContrastCandidate);
            if (_activePalette == null || _activePalette.swatches == null || _guideForegroundIndex >= _activePalette.swatches.Count || _guideBackgroundIndex >= _activePalette.swatches.Count)
                return default(PaletteContrastCandidate);

            PaletteSwatch foreground = _activePalette.swatches[_guideForegroundIndex];
            PaletteSwatch background = _activePalette.swatches[_guideBackgroundIndex];
            if (foreground == null || background == null || foreground == background)
                return default(PaletteContrastCandidate);

            return new PaletteContrastCandidate
            {
                foregroundIndex = _guideForegroundIndex,
                backgroundIndex = _guideBackgroundIndex,
                foregroundRole = foreground.role,
                backgroundRole = background.role,
                label = $"{SafeSwatchName(foreground, _guideForegroundIndex)} on {SafeSwatchName(background, _guideBackgroundIndex)}",
                source = string.IsNullOrWhiteSpace(_guideActiveContrastSource) ? "Loaded pair" : _guideActiveContrastSource,
                contrast = ColourContrastUtility.GetContrastRatio(foreground.color, background.color),
                target = Mathf.Clamp(_guideTargetContrast, 3f, 7f),
                priority = 0
            };
        }

        private bool IsLoadedGuideContrastCandidate(PaletteContrastCandidate candidate)
        {
            return candidate.IsValid && candidate.foregroundIndex == _guideForegroundIndex && candidate.backgroundIndex == _guideBackgroundIndex;
        }

        private void LoadGuideContrastCandidate(PaletteContrastCandidate candidate)
        {
            if (!candidate.IsValid)
                return;

            _guideForegroundIndex = candidate.foregroundIndex;
            _guideBackgroundIndex = candidate.backgroundIndex;
            _guideForegroundRole = candidate.foregroundRole;
            _guideBackgroundRole = candidate.backgroundRole;
            _guideTargetContrast = Mathf.Clamp(candidate.target, 3f, 7f);
            _guideActiveContrastSource = candidate.source;
            _guideLastContrastSummary = $"Loaded {candidate.label} from {candidate.source}.";
            SavePrefs();
            Repaint();
        }

        private void FixGuideContrastCandidate(PaletteContrastCandidate candidate)
        {
            if (!candidate.IsValid)
                return;

            LoadGuideContrastCandidate(candidate);
            if (CanAdjustGuideContrast(GuideContrastAdjustment.Both))
                AdjustGuideRoleContrast(GuideContrastAdjustment.Both);
            else
                _guideLastContrastSummary = "Loaded weakest pair, but no unlocked side can be adjusted.";
        }

        private void ClearLoadedGuideContrastCandidate()
        {
            _guideForegroundIndex = -1;
            _guideBackgroundIndex = -1;
            _guideActiveContrastSource = null;
            _guideLastContrastSummary = null;
        }

        private PaletteSwatch GetSwatch(int index)
        {
            if (_activePalette == null || _activePalette.swatches == null || index < 0 || index >= _activePalette.swatches.Count)
                return null;
            return _activePalette.swatches[index];
        }

        private static string LockSummary(PaletteSwatch foreground, PaletteSwatch background)
        {
            bool foregroundLocked = foreground != null && foreground.locked;
            bool backgroundLocked = background != null && background.locked;
            if (foregroundLocked && backgroundLocked)
                return "Both swatches are locked; unlock one before adjusting.";
            if (foregroundLocked)
                return "Foreground is locked; background can move.";
            if (backgroundLocked)
                return "Background is locked; foreground can move.";
            return "Both swatches are unlocked.";
        }

        private void DrawRefineSelectedSwatchMiniContext()
        {
            if (!IsValidSelectedSwatch())
            {
                DrawInspectorBodyText("Select a swatch in the workbench to show targeted repair options here.");
                return;
            }

            PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
            PaletteSwatchRole suggestedRole = SuggestRoleForSelectedSwatch();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawColourChip(selected.color, 30f, 18f);
                EditorGUILayout.LabelField($"{selected.name} - {Nicify(selected.role.ToString())}", InspectorBodyStyle());
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(selected.locked || selected.role == suggestedRole))
                {
                    if (StudioButton($"Assign {Nicify(suggestedRole.ToString())}", RefineTint(), PaletteDesignerButtonTone.Ghost, GUILayout.Width(132f), GUILayout.Height(22f)))
                        AssignSelectedRole(suggestedRole);
                }
            }

            DrawSelectedContrastAgainst(selected, PaletteSwatchRole.Background);
            DrawSelectedContrastAgainst(selected, PaletteSwatchRole.Panel);
            DrawSelectedContrastAgainst(selected, PaletteSwatchRole.Text);
            DrawSelectedContrastAgainst(selected, PaletteSwatchRole.MutedText);
        }

        private void DrawRefineRoleCoverage()
        {
            List<PaletteRoleSuggestion> missing = BuildRoleSuggestions(false);
            List<PaletteRoleSuggestion> rebalance = BuildRoleSuggestions(true);
            using (BeginRefineSection("Roles & Coverage", missing.Count > 0 ? $"{missing.Count} missing" : "covered", PaletteDesignerSectionTone.Primary))
            {
                DrawInspectorBodyText(missing.Count > 0
                    ? "Assign functional roles to unassigned unlocked swatches. Locked swatches and meaningful existing roles stay as they are."
                    : "Roles are covered. Rebalance can redistribute unlocked roles around locked anchors when you want a cleaner palette structure.");

                DrawRoleSuggestionPreview(missing.Count > 0 ? missing : rebalance);

                bool narrow = CurrentContentWidth() < 420f;
                if (narrow)
                {
                    using (new EditorGUI.DisabledScope(missing.Count == 0))
                    {
                        if (StudioButton("Assign Missing Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                            ApplyRoleSuggestions(false);
                    }
                    using (new EditorGUI.DisabledScope(rebalance.Count == 0))
                    {
                        if (StudioButton("Rebalance Unlocked Roles", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Height(26f)))
                            ApplyRoleSuggestions(true);
                    }
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(missing.Count == 0))
                    {
                        if (StudioButton("Assign Missing Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                            ApplyRoleSuggestions(false);
                    }
                    using (new EditorGUI.DisabledScope(rebalance.Count == 0))
                    {
                        if (StudioButton("Rebalance Unlocked Roles", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Height(26f)))
                            ApplyRoleSuggestions(true);
                    }
                }
            }
        }

        private void DrawRefineSimulationAndAllChecks()
        {
            string sectionStatus = SimulationPreviewActive()
                ? SimulationPreviewLabel()
                : _analysisReport != null ? $"{AnalysisPairCount()} pair(s)" : "Original";
            using (BeginRefineSection("Simulation & Details", sectionStatus, PaletteDesignerSectionTone.Detail))
            {
                DrawInspectorBodyText(SimulationPreviewActive()
                    ? "Simulation is preview-only. Generated colours, palette assets, history, and apply mappings are unchanged."
                    : "Simulation and detailed diagnostics stay folded until you need to inspect the underlying checks.");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(SimulationPreviewActive() ? SimulationPreviewLabel() : "Simulation", InspectorBodyStyle(), GUILayout.Width(CurrentContentWidth() < 420f ? 122f : 154f));
                    EditorGUI.BeginChangeCheck();
                    _deficiencyPreview = (ColourDeficiencyPreviewMode)EditorGUILayout.EnumPopup(_deficiencyPreview, GUILayout.MaxWidth(CurrentContentWidth() < 420f ? 210f : 260f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        SavePrefs();
                        Repaint();
                    }
                }

                if (SimulationPreviewActive() && StudioButton("View Original", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Secondary, GUILayout.Height(23f)))
                    DisableSimulationPreview();

                DrawPaletteRibbon(_activePalette.swatches, 18f);

                _showGuidePreviewTools = EditorGUILayout.Foldout(_showGuidePreviewTools, "Show simulated swatch grid", true);
                if (_showGuidePreviewTools)
                    DrawDeficiencyPreviewGrid();

                if (_analysisReport == null)
                    return;

                _showContrastDetails = EditorGUILayout.Foldout(_showContrastDetails, "Show detailed checks", true);
                if (!_showContrastDetails)
                {
                    DrawInspectorBodyText("Problems, recommendations, role pairs, and lowest contrast pairs are folded.");
                    return;
                }

                _showProblemDetails = EditorGUILayout.Foldout(_showProblemDetails, "Problems and recommendations", true);
                if (_showProblemDetails)
                {
                    int problemMax = Mathf.Min(6, _analysisReport.problems.Count);
                    for (int i = 0; i < problemMax; i++)
                        DrawCompactMessage(_analysisReport.problems[i], UtilityWindowTheme.Amber);

                    int recommendationMax = Mathf.Min(6, _analysisReport.recommendations.Count);
                    for (int i = 0; i < recommendationMax; i++)
                        DrawCompactMessage(_analysisReport.recommendations[i], RefineTint());
                }

                if (_analysisReport.rolePairs.Count > 0)
                {
                    EditorGUILayout.LabelField("Role Pair Checks", UtilityWindowTheme.SectionHeaderStyle);
                    for (int i = 0; i < _analysisReport.rolePairs.Count; i++)
                        DrawPairRow(_analysisReport.rolePairs[i], true);
                }

                if (_analysisReport.pairs.Count > 0)
                {
                    EditorGUILayout.LabelField("Lowest Contrast Pairs", UtilityWindowTheme.SectionHeaderStyle);
                    int max = Mathf.Min(6, _analysisReport.pairs.Count);
                    for (int i = 0; i < max; i++)
                        DrawPairRow(_analysisReport.pairs[i], true);
                }
            }
        }

        private void DrawRefineAllChecks()
        {
            if (_analysisReport == null)
                return;

            using (BeginInspectorSection("All Checks", UtilityWindowTheme.Neutral, $"{AnalysisPairCount()} pair(s)", PaletteDesignerSectionTone.Detail))
            {
                _showContrastDetails = EditorGUILayout.Foldout(_showContrastDetails, "Show detailed checks", true);
                if (!_showContrastDetails)
                {
                    DrawInspectorBodyText("Detailed problems, recommendations, role pairs, and lowest contrast pairs are folded.");
                    return;
                }

                _showProblemDetails = EditorGUILayout.Foldout(_showProblemDetails, "Problems and recommendations", true);
                if (_showProblemDetails)
                {
                    int problemMax = Mathf.Min(6, _analysisReport.problems.Count);
                    for (int i = 0; i < problemMax; i++)
                        DrawCompactMessage(_analysisReport.problems[i], UtilityWindowTheme.Amber);

                    int recommendationMax = Mathf.Min(6, _analysisReport.recommendations.Count);
                    for (int i = 0; i < recommendationMax; i++)
                        DrawCompactMessage(_analysisReport.recommendations[i], RefineTint());
                }

                if (_analysisReport.rolePairs.Count > 0)
                {
                    EditorGUILayout.LabelField("Role Pair Checks", UtilityWindowTheme.SectionHeaderStyle);
                    for (int i = 0; i < _analysisReport.rolePairs.Count; i++)
                        DrawPairRow(_analysisReport.rolePairs[i], true);
                }

                if (_analysisReport.pairs.Count > 0)
                {
                    EditorGUILayout.LabelField("Lowest Contrast Pairs", UtilityWindowTheme.SectionHeaderStyle);
                    int max = Mathf.Min(6, _analysisReport.pairs.Count);
                    for (int i = 0; i < max; i++)
                        DrawPairRow(_analysisReport.pairs[i], true);
                }
            }
        }

        private void DrawGuideNextBestActionCard()
        {
            int issueCount = AnalysisProblemCount();
            int missingRoles = BuildRoleSuggestions(false).Count;
            string status = missingRoles > 0 ? "roles" : issueCount > 0 ? "contrast" : "ready";
            using (BeginInspectorSection("Recommended Next Step", RefineTint(), status, PaletteDesignerSectionTone.Primary))
            {
                bool narrow = CurrentContentWidth() < 420f;
                if (missingRoles > 0)
                {
                    DrawInspectorBodyText($"{missingRoles} unlocked swatch(es) can be assigned functional roles before contrast repair.");
                    if (StudioButton("Assign Missing Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                        ApplyRoleSuggestions(false);
                    return;
                }

                if (issueCount > 0)
                {
                    DrawInspectorBodyText("Start with readable text and neutral surfaces; locked swatches will be skipped.");
                    if (narrow)
                    {
                        if (StudioButton("Improve Text Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                            ImproveTextRoles();
                        if (StudioButton("Add Neutral Panel", RefineTint(), PaletteDesignerButtonTone.Ghost, GUILayout.Height(26f)))
                            AddOrRepairPanelRole();
                    }
                    else
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (StudioButton("Improve Text Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                                ImproveTextRoles();
                            if (StudioButton("Add Neutral Panel", RefineTint(), PaletteDesignerButtonTone.Ghost, GUILayout.Height(26f)))
                                AddOrRepairPanelRole();
                        }
                    }
                    return;
                }

                DrawInspectorBodyText("Palette structure and contrast checks are in a good state. Use Apply when you are ready to test targets.");
                if (StudioButton("Refresh Analysis", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Height(24f)))
                    RefreshGuideAnalysis();
            }
        }

        private void DrawPaletteHealthSummary()
        {
            using (BeginInspectorSection("Accessibility Snapshot", UtilityWindowTheme.Neutral, null, PaletteDesignerSectionTone.Summary))
            {
                bool narrow = CurrentContentWidth() < 390f;
                if (narrow)
                {
                    DrawAnalysisSummaryPills();
                    UtilityWindowTheme.CountPill($"{CountAssignedRoles()} roles", UtilityWindowTheme.Neutral, 84f);
                    UtilityWindowTheme.CountPill($"{CountLockedSwatches()} locked", CountLockedSwatches() > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 82f);
                    if (StudioButton("Refresh", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Secondary, GUILayout.Height(24f)))
                        RefreshGuideAnalysis();
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawAnalysisSummaryPills();
                        UtilityWindowTheme.CountPill($"{CountAssignedRoles()} roles", UtilityWindowTheme.Neutral, 84f);
                        UtilityWindowTheme.CountPill($"{CountLockedSwatches()} locked", CountLockedSwatches() > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 82f);
                        GUILayout.FlexibleSpace();
                        if (StudioButton("Refresh", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Secondary, GUILayout.Width(82f), GUILayout.Height(24f)))
                            RefreshGuideAnalysis();
                    }
                }

                DrawPaletteRibbon(_activePalette.swatches, 20f);

                if (_analysisReport == null)
                    DrawStudioHelpCard("Analysis needed", "Refresh analysis to evaluate this palette.", RefineTint());
                else if (AnalysisProblemCount() == 0 && _analysisReport.recommendations.Count == 0)
                    DrawStudioHelpCard("Looks healthy", "No major accessibility issues detected. Optional refinements stay available below.", UtilityWindowTheme.Green);
                else
                    DrawTopGuideReason();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Deficiency Preview", InspectorBodyStyle(), GUILayout.Width(narrow ? 108f : 118f));
                    EditorGUI.BeginChangeCheck();
                    _deficiencyPreview = (ColourDeficiencyPreviewMode)EditorGUILayout.EnumPopup(_deficiencyPreview, GUILayout.MaxWidth(narrow ? 220f : 260f));
                    if (EditorGUI.EndChangeCheck())
                        Repaint();
                }
            }
        }

        private void DrawAnalysisSummaryPills()
        {
            int problems = _analysisReport != null && _analysisReport.problems != null ? _analysisReport.problems.Count : 0;
            int pairs = _analysisReport != null && _analysisReport.pairs != null ? _analysisReport.pairs.Count : 0;
            UtilityWindowTheme.CountPill(problems == 0 ? "Accessible" : $"{problems} issues", problems == 0 ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 90f);
            UtilityWindowTheme.CountPill($"{pairs} pairs", UtilityWindowTheme.Neutral, 70f);
        }

        private void DrawPaletteRibbon(List<PaletteSwatch> swatches, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.84f, 0.84f, 0.86f));

            if (swatches == null || swatches.Count == 0)
                return;

            int count = 0;
            for (int i = 0; i < swatches.Count; i++)
            {
                if (swatches[i] != null)
                    count++;
            }
            if (count == 0)
                return;

            float x = rect.x;
            float segmentWidth = rect.width / count;
            for (int i = 0; i < swatches.Count; i++)
            {
                PaletteSwatch swatch = swatches[i];
                if (swatch == null)
                    continue;

                Rect segment = new Rect(x, rect.y, Mathf.Ceil(segmentWidth), rect.height);
                EditorGUI.DrawRect(segment, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                x += segmentWidth;
            }
        }

        private void DrawProblemSummary()
        {
            if (_analysisReport == null)
                return;

            if (_analysisReport.problems.Count == 0 && _analysisReport.recommendations.Count == 0)
            {
                EditorGUILayout.HelpBox("No major palette issues detected.", MessageType.Info);
                return;
            }

            int problemMax = Mathf.Min(2, _analysisReport.problems.Count);
            for (int i = 0; i < problemMax; i++)
                DrawCompactMessage(_analysisReport.problems[i], UtilityWindowTheme.Amber);

            int recommendationMax = Mathf.Min(2, _analysisReport.recommendations.Count);
            for (int i = 0; i < recommendationMax; i++)
                DrawCompactMessage(_analysisReport.recommendations[i], RefineTint());

            int hidden = Mathf.Max(0, _analysisReport.problems.Count - problemMax) + Mathf.Max(0, _analysisReport.recommendations.Count - recommendationMax);
            if (hidden > 0)
                DrawInspectorBodyText($"{hidden} more guidance item(s) in All Checks.");
        }

        private void DrawCompactMessage(string message, Color tint)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.CountPill(CompactMessageLabel(tint), tint, 54f);
                EditorGUILayout.LabelField(message, InspectorBodyStyle());
            }
            DrawStudioDivider(UtilityWindowTheme.Neutral, 0.12f);
        }

        private static string CompactMessageLabel(Color tint)
        {
            if (Mathf.Abs(tint.r - UtilityWindowTheme.Amber.r) < 0.04f && Mathf.Abs(tint.g - UtilityWindowTheme.Amber.g) < 0.04f)
                return "Issue";
            if (Mathf.Abs(tint.r - UtilityWindowTheme.Red.r) < 0.04f && Mathf.Abs(tint.g - UtilityWindowTheme.Red.g) < 0.04f)
                return "Risk";
            return "Tip";
        }

        private void DrawTopGuideReason()
        {
            if (_analysisReport == null)
                return;

            if (_analysisReport.problems.Count > 0)
            {
                DrawCompactMessage(_analysisReport.problems[0], UtilityWindowTheme.Amber);
                int hidden = Mathf.Max(0, _analysisReport.problems.Count - 1) + _analysisReport.recommendations.Count;
                if (hidden > 0)
                    DrawInspectorBodyText($"{hidden} more item(s) in All Checks.");
                return;
            }

            if (_analysisReport.recommendations.Count > 0)
            {
                DrawCompactMessage(_analysisReport.recommendations[0], RefineTint());
                if (_analysisReport.recommendations.Count > 1)
                    DrawInspectorBodyText($"{_analysisReport.recommendations.Count - 1} more recommendation(s) in All Checks.");
            }
        }

        private void DrawGuideActionToolsFoldout()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _showGuideActionTools = EditorGUILayout.Foldout(_showGuideActionTools, "Role & Contrast Actions", true);
                if (!_showGuideActionTools)
                {
                    DrawInspectorBodyText("Open for explicit role assignment, contrast balancing, and selected-swatch repair.");
                    return;
                }
            }

            DrawGuideRoleSuggestionsCard();
            DrawGuideContrastActionsCard();
            if (IsValidSelectedSwatch())
                DrawSelectedSwatchContextPanel();
            else
                DrawStudioHelpCard("No swatch selected", "Select a swatch to show contextual contrast repair actions.", UtilityWindowTheme.Neutral);
        }

        private void DrawGuidePreviewToolsFoldout()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _showGuidePreviewTools = EditorGUILayout.Foldout(_showGuidePreviewTools, "Colour-deficiency Preview", true);
                if (!_showGuidePreviewTools)
                {
                    DrawInspectorBodyText($"Current mode: {_deficiencyPreview}. Open to preview swatches under colour-deficiency simulation.");
                    return;
                }
            }

            DrawDeficiencyPreviewPanel();
        }

        private void DrawGuideContrastActionsCard()
        {
            using (BeginInspectorSection("Contrast Actions", RefineTint(), $"{_guideTargetContrast:0.0}:1", PaletteDesignerSectionTone.Primary))
            {
                bool narrow = CurrentContentWidth() < 420f;
                DrawInspectorBodyText("Choose a role pair, then adjust the unlocked side that should move.");

                EditorGUI.BeginChangeCheck();
                if (narrow)
                {
                    _guideForegroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Foreground", _guideForegroundRole);
                    _guideBackgroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Background", _guideBackgroundRole);
                    _guideTargetContrast = EditorGUILayout.Slider("Target", _guideTargetContrast, 3f, 7f);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _guideForegroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Foreground", _guideForegroundRole);
                        _guideBackgroundRole = (PaletteSwatchRole)EditorGUILayout.EnumPopup("Background", _guideBackgroundRole);
                    }
                    _guideTargetContrast = EditorGUILayout.Slider("Target Contrast", _guideTargetContrast, 3f, 7f);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    _guideTargetContrast = Mathf.Clamp(_guideTargetContrast, 3f, 7f);
                    SavePrefs();
                }

                DrawGuideRolePairPreview(_guideForegroundRole, _guideBackgroundRole);

                if (narrow)
                {
                    DrawGuideContrastButton("Adjust Foreground", GuideContrastAdjustment.Foreground);
                    DrawGuideContrastButton("Adjust Background", GuideContrastAdjustment.Background);
                    DrawGuideContrastButton("Balance Both", GuideContrastAdjustment.Both);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawGuideContrastButton("Adjust Foreground", GuideContrastAdjustment.Foreground);
                        DrawGuideContrastButton("Adjust Background", GuideContrastAdjustment.Background);
                        DrawGuideContrastButton("Balance Both", GuideContrastAdjustment.Both);
                    }
                }
            }
        }

        private void DrawGuideContrastButton(string label, GuideContrastAdjustment adjustment)
        {
            using (new EditorGUI.DisabledScope(!CanAdjustGuideContrast(adjustment)))
            {
                Color tint = adjustment == GuideContrastAdjustment.Both ? RefineTint() : UtilityWindowTheme.Neutral;
                PaletteDesignerButtonTone tone = adjustment == GuideContrastAdjustment.Both ? PaletteDesignerButtonTone.Secondary : PaletteDesignerButtonTone.Ghost;
                if (StudioButton(label, tint, tone, GUILayout.Height(26f)))
                    AdjustGuideRoleContrast(adjustment);
            }
        }

        private void DrawGuideRolePairPreview(PaletteSwatchRole foregroundRole, PaletteSwatchRole backgroundRole)
        {
            PaletteSwatch foreground = PaletteAnalysisUtility.FindRole(_activePalette.swatches, foregroundRole);
            PaletteSwatch background = PaletteAnalysisUtility.FindRole(_activePalette.swatches, backgroundRole);
            if (foreground == null || background == null)
            {
                DrawStudioHelpCard("Role pair incomplete", "Assign the missing role first, or choose a role pair that exists in this palette.", UtilityWindowTheme.Amber);
                return;
            }

            float contrast = ColourContrastUtility.GetContrastRatio(foreground.color, background.color);
            Color tint = contrast >= _guideTargetContrast ? UtilityWindowTheme.Green : contrast >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.04f, 0.018f, 5, 2)))
            {
                DrawColourChip(foreground.color, 30f, 20f);
                DrawColourChip(background.color, 30f, 20f);
                EditorGUILayout.LabelField($"{Nicify(foregroundRole.ToString())} on {Nicify(backgroundRole.ToString())}", InspectorBodyStyle());
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill($"{contrast:0.00}:1", tint, 70f);
            }
        }

        private void DrawGuideRoleSuggestionsCard()
        {
            List<PaletteRoleSuggestion> missing = BuildRoleSuggestions(false);
            List<PaletteRoleSuggestion> rebalance = BuildRoleSuggestions(true);
            using (BeginInspectorSection("Role Suggestions", RefineTint(), missing.Count > 0 ? $"{missing.Count} missing" : "covered", PaletteDesignerSectionTone.Primary))
            {
                if (missing.Count == 0)
                    DrawInspectorBodyText("Every unlocked swatch already has a functional role. Rebalance can still redistribute unlocked roles around locked anchors.");
                else
                    DrawInspectorBodyText("Assign roles to unlocked swatches that are currently None or Custom. Locked swatches are never changed.");

                DrawRoleSuggestionPreview(missing.Count > 0 ? missing : rebalance);

                bool narrow = CurrentContentWidth() < 420f;
                if (narrow)
                {
                    using (new EditorGUI.DisabledScope(missing.Count == 0))
                    {
                        if (StudioButton("Assign Missing Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                            ApplyRoleSuggestions(false);
                    }
                    using (new EditorGUI.DisabledScope(rebalance.Count == 0))
                    {
                        if (StudioButton("Rebalance Unlocked Roles", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Height(26f)))
                            ApplyRoleSuggestions(true);
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(missing.Count == 0))
                        {
                            if (StudioButton("Assign Missing Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                                ApplyRoleSuggestions(false);
                        }
                        using (new EditorGUI.DisabledScope(rebalance.Count == 0))
                        {
                            if (StudioButton("Rebalance Unlocked Roles", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Height(26f)))
                                ApplyRoleSuggestions(true);
                        }
                    }
                }
            }
        }

        private void DrawRoleSuggestionPreview(List<PaletteRoleSuggestion> suggestions)
        {
            if (suggestions == null || suggestions.Count == 0)
            {
                DrawStudioHelpCard("No role changes suggested", "Locked roles and existing functional roles already provide enough structure for analysis and apply workflows.", UtilityWindowTheme.Neutral);
                return;
            }

            int max = Mathf.Min(6, suggestions.Count);
            for (int i = 0; i < max; i++)
            {
                PaletteRoleSuggestion suggestion = suggestions[i];
                PaletteSwatch swatch = suggestion.index >= 0 && _activePalette.swatches != null && suggestion.index < _activePalette.swatches.Count
                    ? _activePalette.swatches[suggestion.index]
                    : null;
                if (swatch == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawColourChip(swatch.color, 26f, 18f);
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {suggestion.index + 1}" : swatch.name, UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill(Nicify(suggestion.role.ToString()), UtilityWindowTheme.Neutral, 116f);
                }
                if (i < max - 1)
                    DrawStudioDivider(UtilityWindowTheme.Neutral, 0.14f);
            }

            if (suggestions.Count > max)
                DrawInspectorBodyText($"{suggestions.Count - max} more role suggestion(s).");
        }

        private void DrawRecommendedRepairsCard()
        {
            using (BeginInspectorSection("Accessibility Repairs", RefineTint(), "unlocked only", PaletteDesignerSectionTone.Primary))
            {
                bool compact = CurrentContentWidth() < 440f;
                if (AnalysisProblemCount() == 0)
                    DrawInspectorBodyText("These actions are still available when you want stronger text, panel, or selected-swatch contrast.");
                else
                    DrawInspectorBodyText("Use these explicit repairs first. Locked swatches are skipped.");

                if (compact)
                {
                    if (StudioButton("Improve Text Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                        ImproveTextRoles();
                    if (StudioButton("Add Neutral Panel", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                        AddOrRepairPanelRole();
                    if (StudioButton("Add Readable Text", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                        AddOrRepairTextRole();
                    using (new EditorGUI.DisabledScope(!IsValidSelectedSwatch()))
                    {
                        if (StudioButton("Repair Selected", RefineTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(26f)))
                            RepairSelectedAgainstBackground();
                    }
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton("Improve Text Roles", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                        ImproveTextRoles();
                    if (StudioButton("Add Neutral Panel", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                        AddOrRepairPanelRole();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (StudioButton("Add Readable Text", RefineTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Height(26f)))
                        AddOrRepairTextRole();
                    using (new EditorGUI.DisabledScope(!IsValidSelectedSwatch()))
                    {
                        if (StudioButton("Repair Selected", RefineTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(26f)))
                            RepairSelectedAgainstBackground();
                    }
                }
            }
        }

        private void DrawSelectedSwatchContextPanel()
        {
            using (BeginInspectorSection("Selected Swatch Context", UtilityWindowTheme.Neutral, IsValidSelectedSwatch() ? $"#{_selectedSwatch + 1}" : "none", PaletteDesignerSectionTone.Detail))
            {
                if (!IsValidSelectedSwatch())
                {
                    DrawStudioHelpCard("No swatch selected", "Select a swatch in the palette to inspect contrast against foundation roles here.", UtilityWindowTheme.Neutral);
                    return;
                }

                PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
                PaletteSwatchRole suggestedRole = SuggestRoleForSelectedSwatch();
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawColourChip(selected.color, 34f, 22f);
                    EditorGUILayout.LabelField($"{selected.name} / {Nicify(selected.role.ToString())}", UtilityWindowTheme.MutedMiniLabelStyle);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(selected.locked || selected.role == suggestedRole))
                    {
                        if (StudioButton($"Assign {Nicify(suggestedRole.ToString())}", RefineTint(), PaletteDesignerButtonTone.Ghost, GUILayout.Width(132f), GUILayout.Height(23f)))
                            AssignSelectedRole(suggestedRole);
                    }
                }
                DrawSelectedContrastAgainst(selected, PaletteSwatchRole.Background);
                DrawSelectedContrastAgainst(selected, PaletteSwatchRole.Panel);
                DrawSelectedContrastAgainst(selected, PaletteSwatchRole.Text);
                DrawSelectedContrastAgainst(selected, PaletteSwatchRole.MutedText);
            }
        }

        private void DrawSelectedContrastAgainst(PaletteSwatch selected, PaletteSwatchRole role)
        {
            PaletteSwatch other = PaletteAnalysisUtility.FindRole(_activePalette.swatches, role);
            if (selected == null || other == null || other == selected)
                return;

            float contrast = ColourContrastUtility.GetContrastRatio(selected.color, other.color);
            Color tint = contrast >= 4.5f ? UtilityWindowTheme.Green : contrast >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect a = GUILayoutUtility.GetRect(28f, 18f, GUILayout.Width(28f), GUILayout.Height(18f));
                EditorGUI.DrawRect(a, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, selected.color));
                Rect b = GUILayoutUtility.GetRect(28f, 18f, GUILayout.Width(28f), GUILayout.Height(18f));
                EditorGUI.DrawRect(b, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, other.color));
                EditorGUILayout.LabelField($"{selected.name} vs {role}", UtilityWindowTheme.MutedMiniLabelStyle);
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill($"{contrast:0.00}:1", tint, 68f);
                using (new EditorGUI.DisabledScope(selected.locked))
                {
                    if (StudioButton("Adjust", tint, PaletteDesignerButtonTone.Ghost, GUILayout.Width(58f), GUILayout.Height(20f)))
                        AdjustSelectedAgainstRole(role, 4.5f);
                }
            }
            DrawStudioDivider(tint, 0.16f);
        }

        private void RefreshGuideAnalysis()
        {
            _analysisDirty = true;
            RefreshAnalysisIfNeeded();
            _lastStatus = "Analysis refreshed.";
        }

        private int CountAssignedRoles()
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return 0;

            int count = 0;
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch != null && IsFunctionalRole(swatch.role))
                    count++;
            }

            return count;
        }

        private bool CanAdjustGuideContrast(GuideContrastAdjustment adjustment)
        {
            if (_activePalette == null || _activePalette.swatches == null)
                return false;

            PaletteSwatch foreground;
            PaletteSwatch background;
            ResolveGuideContrastSwatches(out foreground, out background);
            if (foreground == null || background == null || foreground == background)
                return false;

            switch (adjustment)
            {
                case GuideContrastAdjustment.Foreground:
                    return !foreground.locked;
                case GuideContrastAdjustment.Background:
                    return !background.locked;
                case GuideContrastAdjustment.Both:
                    return !foreground.locked || !background.locked;
                default:
                    return false;
            }
        }

        private void AdjustGuideRoleContrast(GuideContrastAdjustment adjustment)
        {
            if (!CanAdjustGuideContrast(adjustment))
                return;

            PaletteSwatch foreground;
            PaletteSwatch background;
            ResolveGuideContrastSwatches(out foreground, out background);
            if (foreground == null || background == null || foreground == background)
                return;

            float target = Mathf.Clamp(_guideTargetContrast, 3f, 7f);
            int changed = 0;
            int skipped = 0;
            ChangePalette("Adjust Palette Contrast", () =>
            {
                if ((adjustment == GuideContrastAdjustment.Foreground || adjustment == GuideContrastAdjustment.Both) && !foreground.locked)
                {
                    foreground.color = ColourContrastUtility.ImproveContrast(foreground.color, background.color, target);
                    changed++;
                }
                else if (adjustment == GuideContrastAdjustment.Foreground || adjustment == GuideContrastAdjustment.Both)
                {
                    skipped++;
                }

                if ((adjustment == GuideContrastAdjustment.Background || adjustment == GuideContrastAdjustment.Both) && !background.locked)
                {
                    background.color = ColourContrastUtility.ImproveContrast(background.color, foreground.color, target);
                    changed++;
                }
                else if (adjustment == GuideContrastAdjustment.Background || adjustment == GuideContrastAdjustment.Both)
                {
                    skipped++;
                }
            });

            string pair = $"{SafeSwatchName(foreground, _guideForegroundIndex)} on {SafeSwatchName(background, _guideBackgroundIndex)}";
            if (_guideForegroundIndex < 0 || _guideBackgroundIndex < 0)
                pair = $"{Nicify(_guideForegroundRole.ToString())} on {Nicify(_guideBackgroundRole.ToString())}";
            _guideLastContrastSummary = skipped > 0
                ? $"{pair}: adjusted {changed}, skipped {skipped} locked swatch(es)."
                : $"{pair}: adjusted {changed} swatch(es).";
            _lastStatus = _guideLastContrastSummary;
        }

        private void ResolveGuideContrastSwatches(out PaletteSwatch foreground, out PaletteSwatch background)
        {
            foreground = null;
            background = null;
            PaletteContrastCandidate loaded = GetLoadedGuideContrastCandidate();
            if (loaded.IsValid)
            {
                foreground = GetSwatch(loaded.foregroundIndex);
                background = GetSwatch(loaded.backgroundIndex);
                return;
            }

            foreground = PaletteAnalysisUtility.FindRole(_activePalette.swatches, _guideForegroundRole);
            background = PaletteAnalysisUtility.FindRole(_activePalette.swatches, _guideBackgroundRole);
        }

        private List<PaletteRoleSuggestion> BuildRoleSuggestions(bool rebalanceUnlocked)
        {
            var suggestions = new List<PaletteRoleSuggestion>();
            if (_activePalette == null || _activePalette.swatches == null)
                return suggestions;

            var usedRoles = new HashSet<PaletteSwatchRole>();
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch == null)
                    continue;

                if (swatch.locked && IsFunctionalRole(swatch.role))
                    usedRoles.Add(swatch.role);
                else if (!rebalanceUnlocked && IsFunctionalRole(swatch.role))
                    usedRoles.Add(swatch.role);
            }

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch == null || swatch.locked)
                    continue;

                if (!rebalanceUnlocked && IsFunctionalRole(swatch.role))
                    continue;

                PaletteSwatchRole role = NextSuggestedRole(usedRoles, i);
                suggestions.Add(new PaletteRoleSuggestion(i, role));
                if (IsFunctionalRole(role))
                    usedRoles.Add(role);
            }

            return suggestions;
        }

        private void ApplyRoleSuggestions(bool rebalanceUnlocked)
        {
            List<PaletteRoleSuggestion> suggestions = BuildRoleSuggestions(rebalanceUnlocked);
            if (suggestions.Count == 0)
                return;

            ChangePalette(rebalanceUnlocked ? "Rebalance Palette Roles" : "Assign Missing Palette Roles", () =>
            {
                for (int i = 0; i < suggestions.Count; i++)
                {
                    PaletteRoleSuggestion suggestion = suggestions[i];
                    if (suggestion.index < 0 || suggestion.index >= _activePalette.swatches.Count)
                        continue;

                    PaletteSwatch swatch = _activePalette.swatches[suggestion.index];
                    if (swatch == null || swatch.locked)
                        continue;

                    swatch.role = suggestion.role;
                    MaybeRenameForRole(swatch, suggestion.index);
                }
            });

            _lastStatus = rebalanceUnlocked ? "Rebalanced unlocked palette roles." : "Assigned missing palette roles.";
        }

        private PaletteSwatchRole SuggestRoleForSelectedSwatch()
        {
            if (!IsValidSelectedSwatch())
                return PaletteSwatchRole.Accent;

            PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
            if (selected != null && IsFunctionalRole(selected.role))
                return selected.role;

            var usedRoles = new HashSet<PaletteSwatchRole>();
            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                if (i == _selectedSwatch)
                    continue;

                PaletteSwatch swatch = _activePalette.swatches[i];
                if (swatch != null && IsFunctionalRole(swatch.role))
                    usedRoles.Add(swatch.role);
            }

            return NextSuggestedRole(usedRoles, _selectedSwatch);
        }

        private void AssignSelectedRole(PaletteSwatchRole role)
        {
            if (!IsValidSelectedSwatch())
                return;

            PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
            if (selected == null || selected.locked)
                return;

            ChangePalette("Assign Selected Swatch Role", () =>
            {
                selected.role = role;
                MaybeRenameForRole(selected, _selectedSwatch);
            });

            _lastStatus = $"Assigned {Nicify(role.ToString())} role.";
        }

        private void AdjustSelectedAgainstRole(PaletteSwatchRole role, float targetContrast)
        {
            if (!IsValidSelectedSwatch())
                return;

            PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
            PaletteSwatch other = PaletteAnalysisUtility.FindRole(_activePalette.swatches, role);
            if (selected == null || selected.locked || other == null || other == selected)
                return;

            ChangePalette("Adjust Selected Swatch Contrast", () =>
            {
                selected.color = ColourContrastUtility.ImproveContrast(selected.color, other.color, targetContrast);
            });

            _lastStatus = $"Adjusted {selected.name} contrast against {Nicify(role.ToString())}.";
        }

        private static bool IsFunctionalRole(PaletteSwatchRole role)
        {
            return role != PaletteSwatchRole.None && role != PaletteSwatchRole.Custom;
        }

        private PaletteSwatchRole NextSuggestedRole(HashSet<PaletteSwatchRole> usedRoles, int index)
        {
            if (GuideRoleOrder.Length == 0)
                return PaletteSwatchRole.Accent;

            for (int i = 0; i < GuideRoleOrder.Length; i++)
            {
                PaletteSwatchRole role = GuideRoleOrder[i];
                if (usedRoles == null || !usedRoles.Contains(role))
                    return role;
            }

            int accentStart = System.Array.IndexOf(GuideRoleOrder, PaletteSwatchRole.Accent);
            accentStart = Mathf.Max(0, accentStart);
            int accentCount = Mathf.Max(1, GuideRoleOrder.Length - accentStart);
            return GuideRoleOrder[accentStart + Mathf.Abs(index) % accentCount];
        }

        private void MaybeRenameForRole(PaletteSwatch swatch, int index)
        {
            if (swatch == null || !IsFunctionalRole(swatch.role))
                return;

            if (string.IsNullOrWhiteSpace(swatch.name) || swatch.name.StartsWith("Swatch"))
                swatch.name = Nicify(swatch.role.ToString());
        }

        private void DrawDeficiencyPreviewPanel()
        {
            using (BeginStudioCard("Deficiency Preview", UtilityWindowTheme.Neutral, SimulationPreviewActive() ? SimulationPreviewLabel() : "Original", 0.055f, 0.025f))
            {
                if (SimulationPreviewActive())
                {
                    DrawInspectorBodyText("Preview only. Palette data is unchanged.");
                    if (StudioButton("View Original", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Secondary, GUILayout.Height(23f)))
                        DisableSimulationPreview();
                }
                DrawPaletteRibbon(_activePalette.swatches, 18f);
                DrawDeficiencyPreviewGrid();
            }
        }

        private void DrawDeficiencyPreviewGrid()
        {
            if (_activePalette == null || _activePalette.swatches == null || _activePalette.swatches.Count == 0)
                return;

            int max = Mathf.Min(8, _activePalette.swatches.Count);
            int columns = Mathf.Clamp(Mathf.FloorToInt(CurrentContentWidth() / 76f), 1, Mathf.Max(1, max));
            float width = Mathf.Max(58f, Mathf.Min(120f, (CurrentContentWidth() - 12f - (columns - 1) * 4f) / Mathf.Max(1, columns)));
            for (int rowStart = 0; rowStart < max; rowStart += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int offset = 0; offset < columns; offset++)
                    {
                        int i = rowStart + offset;
                        if (i >= max)
                            break;

                        PaletteSwatch swatch = _activePalette.swatches[i];
                        if (swatch == null)
                            continue;

                        using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.04f, 0.018f, 3, 1), GUILayout.Width(width)))
                        {
                            Rect rect = GUILayoutUtility.GetRect(width - 6f, 28f, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
                            EditorGUI.DrawRect(rect, ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color));
                            EditorGUILayout.LabelField(ColourConversionUtility.ToHexRGB(swatch.color), UtilityWindowTheme.MutedMiniLabelStyle);
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawContrastSummary()
        {
            if (_analysisReport == null)
                return;

            _showContrastDetails = EditorGUILayout.Foldout(_showContrastDetails, "All Checks", true);
            if (!_showContrastDetails)
            {
                if (_analysisReport.rolePairs.Count > 0)
                    DrawPairRow(_analysisReport.rolePairs[0]);
                else if (_analysisReport.pairs.Count > 0)
                    DrawPairRow(_analysisReport.pairs[0]);
                return;
            }

            _showProblemDetails = EditorGUILayout.Foldout(_showProblemDetails, "Problems and recommendations", true);
            if (_showProblemDetails)
            {
                int problemMax = Mathf.Min(6, _analysisReport.problems.Count);
                for (int i = 0; i < problemMax; i++)
                    DrawCompactMessage(_analysisReport.problems[i], UtilityWindowTheme.Amber);

                int recommendationMax = Mathf.Min(6, _analysisReport.recommendations.Count);
                for (int i = 0; i < recommendationMax; i++)
                    DrawCompactMessage(_analysisReport.recommendations[i], RefineTint());
            }

            if (_analysisReport.rolePairs.Count > 0)
            {
                EditorGUILayout.LabelField("Role Pair Checks", UtilityWindowTheme.SectionHeaderStyle);
                for (int i = 0; i < _analysisReport.rolePairs.Count; i++)
                    DrawPairRow(_analysisReport.rolePairs[i]);
            }

            if (_analysisReport.pairs.Count > 0)
            {
                EditorGUILayout.LabelField("Lowest Contrast Pairs", UtilityWindowTheme.SectionHeaderStyle);
                int max = Mathf.Min(6, _analysisReport.pairs.Count);
                for (int i = 0; i < max; i++)
                    DrawPairRow(_analysisReport.pairs[i]);
            }
        }

        private void DrawPairRow(PaletteContrastPair pair)
        {
            DrawPairRow(pair, false);
        }

        private void DrawPairRow(PaletteContrastPair pair, bool allowUsePair)
        {
            if (pair == null)
                return;

            Color tint = pair.contrast >= 4.5f ? UtilityWindowTheme.Green : pair.contrast >= 3f ? UtilityWindowTheme.Amber : UtilityWindowTheme.Red;
            PaletteContrastCandidate candidate = default(PaletteContrastCandidate);
            bool canUsePair = allowUsePair && TryBuildAnalysisCandidateForUse(pair, out candidate);
            using (new EditorGUILayout.VerticalScope())
            {
                if (CurrentContentWidth() < 430f)
                {
                    EditorGUILayout.LabelField($"{pair.label} -> {pair.otherLabel}", InspectorBodyStyle());
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        UtilityWindowTheme.CountPill($"{pair.contrast:0.00}:1", tint, 64f);
                        EditorGUILayout.LabelField(pair.WCAG, UtilityWindowTheme.MutedMiniLabelStyle);
                        GUILayout.FlexibleSpace();
                        if (canUsePair)
                            DrawUseContrastCandidateButton(candidate, IsLoadedGuideContrastCandidate(candidate), 76f);
                    }
                    DrawStudioDivider(UtilityWindowTheme.Neutral, 0.14f);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(pair.label, GUILayout.MinWidth(80f));
                    EditorGUILayout.LabelField(pair.otherLabel, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.MinWidth(70f));
                    GUILayout.FlexibleSpace();
                    UtilityWindowTheme.CountPill($"{pair.contrast:0.00}:1", tint, 64f);
                    EditorGUILayout.LabelField(pair.WCAG, UtilityWindowTheme.MutedMiniLabelStyle, GUILayout.Width(62f));
                    if (canUsePair)
                        DrawUseContrastCandidateButton(candidate, IsLoadedGuideContrastCandidate(candidate), 76f);
                }
                DrawStudioDivider(UtilityWindowTheme.Neutral, 0.14f);
            }
        }

        private bool TryBuildAnalysisCandidateForUse(PaletteContrastPair pair, out PaletteContrastCandidate candidate)
        {
            candidate = default(PaletteContrastCandidate);
            if (pair == null || _activePalette == null || _activePalette.swatches == null)
                return false;

            int foregroundIndex;
            int backgroundIndex;
            bool rolePair = !string.IsNullOrEmpty(pair.label) && pair.label.Contains(" on ");
            if (!TryResolveAnalysisPair(pair, rolePair, out foregroundIndex, out backgroundIndex))
                return false;

            PaletteSwatch foreground = GetSwatch(foregroundIndex);
            PaletteSwatch background = GetSwatch(backgroundIndex);
            if (foreground == null || background == null || (foreground.locked && background.locked))
                return false;

            if (!IsTextContrastRole(foreground.role) && IsTextContrastRole(background.role))
            {
                int swap = foregroundIndex;
                foregroundIndex = backgroundIndex;
                backgroundIndex = swap;
                foreground = GetSwatch(foregroundIndex);
                background = GetSwatch(backgroundIndex);
            }

            float target = ContrastTargetForRoles(foreground.role, background.role, pair.label);
            if (pair.contrast >= target)
                return false;

            candidate = new PaletteContrastCandidate
            {
                foregroundIndex = foregroundIndex,
                backgroundIndex = backgroundIndex,
                foregroundRole = foreground.role,
                backgroundRole = background.role,
                label = rolePair ? pair.label : $"{SafeSwatchName(foreground, foregroundIndex)} on {SafeSwatchName(background, backgroundIndex)}",
                source = rolePair ? "Role check" : "Palette pair",
                contrast = ColourContrastUtility.GetContrastRatio(foreground.color, background.color),
                target = target,
                priority = rolePair ? 2 : 3
            };
            return candidate.IsValid;
        }

        private void DrawRepairButtons()
        {
            EditorGUILayout.LabelField("Quick Repairs", UtilityWindowTheme.SectionHeaderStyle);
            bool compact = CurrentContentWidth() < 700f;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Improve Text Roles"))
                    ImproveTextRoles();
                if (GUILayout.Button("Add Neutral Panel"))
                    AddOrRepairPanelRole();

                if (!compact)
                {
                    if (GUILayout.Button("Add Readable Text"))
                        AddOrRepairTextRole();
                    GUI.enabled = IsValidSelectedSwatch();
                    if (GUILayout.Button("Repair Selected"))
                        RepairSelectedAgainstBackground();
                    GUI.enabled = true;
                }
            }

            if (compact)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Readable Text"))
                        AddOrRepairTextRole();
                    GUI.enabled = IsValidSelectedSwatch();
                    if (GUILayout.Button("Repair Selected"))
                        RepairSelectedAgainstBackground();
                    GUI.enabled = true;
                }
            }
        }
    }
#endif
}
