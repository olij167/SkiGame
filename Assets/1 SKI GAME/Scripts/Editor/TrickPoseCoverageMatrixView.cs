using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[Serializable]
public sealed class TrickPoseCoverageMatrixState
{
    public TrickPoseVerticalOrientationRequirement verticalOrientation = TrickPoseVerticalOrientationRequirement.Upright;
    public TrickPoseHorizontalOrientationRequirement horizontalOrientation = TrickPoseHorizontalOrientationRequirement.Upright;
    public TrickPoseTravelFacingRequirement travelFacing = TrickPoseTravelFacingRequirement.Forward;
    public TrickPoseMotionStateRequirement motionState = TrickPoseMotionStateRequirement.Any;
}

[Serializable]
public sealed class TrickPoseCoverageAtlasState
{
    public TrickPoseVerticalOrientationRequirement verticalFilter = TrickPoseVerticalOrientationRequirement.Any;
    public TrickPoseHorizontalOrientationRequirement horizontalFilter = TrickPoseHorizontalOrientationRequirement.Any;
    public TrickPoseTravelFacingRequirement travelFacingFilter = TrickPoseTravelFacingRequirement.Any;
    public TrickPoseMotionStateRequirement motionFilter = TrickPoseMotionStateRequirement.Any;
    public string searchText = string.Empty;
    public bool hideCleanMatrices;
    public bool dimNonMatchingFocus = true;
    public int selectedEntryIndex = -1;
    public bool highlightSelectedEntry = true;
}

public static class TrickPoseCoverageMatrixView
{
    private const float HeaderWidth = 140f;
    private const float CellWidth = 132f;
    private const float CellHeight = 52f;

    public static int DrawMatrix(List<TrickPoseCoverageSlot> slots, TrickPoseCoveragePlanSO plan, TrickPoseCoverageMatrixState state, int selectedIndex)
    {
        TrickPoseCoverageAtlasState atlasState = new TrickPoseCoverageAtlasState
        {
            verticalFilter = state != null ? state.verticalOrientation : TrickPoseVerticalOrientationRequirement.Any,
            horizontalFilter = state != null ? state.horizontalOrientation : TrickPoseHorizontalOrientationRequirement.Any,
            travelFacingFilter = state != null ? state.travelFacing : TrickPoseTravelFacingRequirement.Any,
            motionFilter = state != null ? state.motionState : TrickPoseMotionStateRequirement.Any
        };
        return DrawAtlas(slots, plan, atlasState, selectedIndex);
    }

    public static int DrawAtlas(List<TrickPoseCoverageSlot> slots, TrickPoseCoveragePlanSO plan, TrickPoseCoverageAtlasState state, int selectedIndex)
    {
        if (plan == null || slots == null || slots.Count == 0)
        {
            EditorGUILayout.HelpBox("Build slots to see the pose coverage atlas.", MessageType.Info);
            return selectedIndex;
        }

        List<SkiController.AerialPoseFamily> families = CollectFamilies(slots);
        List<SkiController.AerialPoseShape> shapes = CollectShapes(slots);
        if (families.Count == 0 || shapes.Count == 0)
        {
            EditorGUILayout.HelpBox("No family/shape slots are available for the current atlas.", MessageType.Info);
            return selectedIndex;
        }

        List<AtlasSection> sections = BuildSections(slots, state, families, shapes);
        if (sections.Count == 0)
        {
            EditorGUILayout.HelpBox("No atlas sections match the current focus.", MessageType.Info);
            return selectedIndex;
        }

        for (int i = 0; i < sections.Count; i++)
            selectedIndex = DrawSection(slots, sections[i], state, selectedIndex);

        return selectedIndex;
    }

    public static bool MatchesFocus(TrickPoseCoverageSlot slot, TrickPoseCoverageAtlasState state)
    {
        if (slot == null)
            return false;

        if (state != null)
        {
            if (state.verticalFilter != TrickPoseVerticalOrientationRequirement.Any &&
                slot.verticalOrientation != state.verticalFilter)
                return false;
            if (state.horizontalFilter != TrickPoseHorizontalOrientationRequirement.Any &&
                slot.horizontalOrientation != state.horizontalFilter)
                return false;
            if (state.travelFacingFilter != TrickPoseTravelFacingRequirement.Any &&
                slot.travelFacing != state.travelFacingFilter)
                return false;
            if (state.motionFilter != TrickPoseMotionStateRequirement.Any &&
                slot.motionState != state.motionFilter)
                return false;
        }

        return MatchesSearch(slot, state != null ? state.searchText : string.Empty);
    }

    public static bool MatchesSearch(TrickPoseCoverageSlot slot, string searchText)
    {
        if (slot == null || string.IsNullOrWhiteSpace(searchText))
            return true;

        string needle = searchText.Trim();
        if (Contains(slot.shortLabel, needle) ||
            Contains(slot.slotId, needle) ||
            Contains(slot.summary, needle) ||
            Contains(slot.validationStatus.ToString(), needle) ||
            Contains(slot.exclusionReason, needle) ||
            Contains(slot.assignedEntry != null ? slot.assignedEntry.GetSummary() : string.Empty, needle) ||
            Contains(slot.representativeContext != null ? slot.representativeContext.poseName : string.Empty, needle))
        {
            return true;
        }

        for (int i = 0; i < slot.candidateEntryLabels.Count; i++)
        {
            if (Contains(slot.candidateEntryLabels[i], needle))
                return true;
        }

        return false;
    }

    private static int DrawSection(List<TrickPoseCoverageSlot> slots, AtlasSection section, TrickPoseCoverageAtlasState state, int selectedIndex)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                $"{section.vertical} | {section.horizontal} | Travel {section.travelFacing} | Motion {section.motion}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"Covered {section.coveredCount}  Gap {section.gapCount}  Ambiguous {section.ambiguousCount}  Suppressed {section.suppressedCount}",
                EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(HeaderWidth);
                for (int c = 0; c < section.shapes.Count; c++)
                    GUILayout.Label(DescribeVerticalInput(section.shapes[c]), EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));
            }

            for (int r = 0; r < section.families.Count; r++)
            {
                SkiController.AerialPoseFamily family = section.families[r];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(DescribeHorizontalInput(family), EditorStyles.boldLabel, GUILayout.Width(HeaderWidth));
                    for (int c = 0; c < section.shapes.Count; c++)
                    {
                        selectedIndex = DrawCell(
                            slots,
                            family,
                            section.shapes[c],
                            section.vertical,
                            section.horizontal,
                            section.travelFacing,
                            section.motion,
                            state,
                            selectedIndex);
                    }
                }
            }
        }

        return selectedIndex;
    }

    private static int DrawCell(
        List<TrickPoseCoverageSlot> slots,
        SkiController.AerialPoseFamily family,
        SkiController.AerialPoseShape shape,
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseTravelFacingRequirement travelFacing,
        TrickPoseMotionStateRequirement motion,
        TrickPoseCoverageAtlasState state,
        int selectedIndex)
    {
        int slotIndex = FindSlotIndex(slots, family, shape, vertical, horizontal, travelFacing, motion);
        if (slotIndex < 0)
        {
            GUILayout.Box("-", EditorStyles.helpBox, GUILayout.Width(CellWidth), GUILayout.Height(CellHeight));
            return selectedIndex;
        }

        TrickPoseCoverageSlot slot = slots[slotIndex];
        bool matchesFocus = MatchesFocus(slot, state);
        bool isSelected = slotIndex == selectedIndex;
        SelectedEntryInvolvement selectedEntryInvolvement = GetSelectedEntryInvolvement(slot, state);
        bool highlightsSelectedEntry = selectedEntryInvolvement != SelectedEntryInvolvement.None;
        using (new EditorGUI.DisabledScope(false))
        {
            Rect cellRect = GUILayoutUtility.GetRect(CellWidth, CellHeight, GUILayout.Width(CellWidth), GUILayout.Height(CellHeight));
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;

            GUI.backgroundColor = CellColor(slot, matchesFocus, state != null && state.dimNonMatchingFocus, isSelected);
            if (!matchesFocus && state != null && state.dimNonMatchingFocus)
                GUI.contentColor = new Color(previousContent.r, previousContent.g, previousContent.b, 0.7f);

            string buttonText = $"{BuildStatusGlyph(slot)} {BuildSelectedEntryGlyph(selectedEntryInvolvement)}{BuildPrimaryLabel(slot)}\n{BuildSecondaryLabel(slot)}";
            string tooltip =
                $"{slot.slotId}\n" +
                $"{slot.summary}\n" +
                $"Status: {slot.validationStatus}\n" +
                $"Owner: {BuildOwnerSummary(slot)}\n" +
                $"{BuildInvolvedSummary(slot)}\n" +
                $"{BuildSelectedEntryTooltip(selectedEntryInvolvement)}";

            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                fontSize = 10,
                fixedHeight = CellHeight
            };

            if (GUI.Button(cellRect, new GUIContent(buttonText, tooltip), style))
                selectedIndex = slotIndex;

            DrawCellBorder(cellRect, isSelected, selectedEntryInvolvement, highlightsSelectedEntry);

            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
        }

        return selectedIndex;
    }

    private static List<AtlasSection> BuildSections(
        List<TrickPoseCoverageSlot> slots,
        TrickPoseCoverageAtlasState state,
        List<SkiController.AerialPoseFamily> families,
        List<SkiController.AerialPoseShape> shapes)
    {
        List<AtlasSection> sections = new List<AtlasSection>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null)
                continue;
            if (state != null)
            {
                if (state.verticalFilter != TrickPoseVerticalOrientationRequirement.Any && slot.verticalOrientation != state.verticalFilter)
                    continue;
                if (state.horizontalFilter != TrickPoseHorizontalOrientationRequirement.Any && slot.horizontalOrientation != state.horizontalFilter)
                    continue;
                if (state.travelFacingFilter != TrickPoseTravelFacingRequirement.Any && slot.travelFacing != state.travelFacingFilter)
                    continue;
                if (state.motionFilter == TrickPoseMotionStateRequirement.Any)
                {
                    if (slot.motionState != TrickPoseMotionStateRequirement.Any)
                        continue;
                }
                else if (slot.motionState != state.motionFilter)
                    continue;
            }

            string key = $"{slot.verticalOrientation}|{slot.horizontalOrientation}|{slot.travelFacing}|{slot.motionState}";
            if (!seen.Add(key))
                continue;

            AtlasSection section = CreateSection(slots, slot.verticalOrientation, slot.horizontalOrientation, slot.travelFacing, slot.motionState, families, shapes);
            if (section.totalSlots == 0)
                continue;
            if (state != null && state.hideCleanMatrices && section.gapCount == 0 && section.ambiguousCount == 0 && section.suppressedCount == 0)
                continue;

            sections.Add(section);
        }

        sections.Sort((a, b) =>
        {
            int verticalCompare = a.vertical.CompareTo(b.vertical);
            if (verticalCompare != 0)
                return verticalCompare;
            int horizontalCompare = a.horizontal.CompareTo(b.horizontal);
            if (horizontalCompare != 0)
                return horizontalCompare;
            int travelCompare = a.travelFacing.CompareTo(b.travelFacing);
            if (travelCompare != 0)
                return travelCompare;
            return a.motion.CompareTo(b.motion);
        });

        return sections;
    }

    private static AtlasSection CreateSection(
        List<TrickPoseCoverageSlot> slots,
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseTravelFacingRequirement travelFacing,
        TrickPoseMotionStateRequirement motion,
        List<SkiController.AerialPoseFamily> families,
        List<SkiController.AerialPoseShape> shapes)
    {
        AtlasSection section = new AtlasSection
        {
            vertical = vertical,
            horizontal = horizontal,
            travelFacing = travelFacing,
            motion = motion,
            families = families,
            shapes = shapes
        };

        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null ||
                slot.verticalOrientation != vertical ||
                slot.horizontalOrientation != horizontal ||
                slot.travelFacing != travelFacing ||
                slot.motionState != motion)
            {
                continue;
            }

            section.totalSlots++;
            switch (slot.validationStatus)
            {
                case TrickPoseCoverageSlotValidationStatus.Covered:
                    section.coveredCount++;
                    break;
                case TrickPoseCoverageSlotValidationStatus.Gap:
                    section.gapCount++;
                    break;
                case TrickPoseCoverageSlotValidationStatus.Ambiguous:
                    section.ambiguousCount++;
                    break;
                case TrickPoseCoverageSlotValidationStatus.Suppressed:
                    section.suppressedCount++;
                    break;
            }
        }

        return section;
    }

    private static string BuildStatusGlyph(TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return "-";
        return slot.validationStatus switch
        {
            TrickPoseCoverageSlotValidationStatus.Covered => slot.assignmentState == TrickPoseCoverageAssignmentState.AssignedToPlaceholder ? "P" : "C",
            TrickPoseCoverageSlotValidationStatus.Gap => "G",
            TrickPoseCoverageSlotValidationStatus.Ambiguous => "A",
            TrickPoseCoverageSlotValidationStatus.Suppressed => "S",
            TrickPoseCoverageSlotValidationStatus.Excluded => "X",
            _ => "?"
        };
    }

    private static string BuildPrimaryLabel(TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return "(none)";
        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Excluded)
            return "Excluded";
        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Gap)
            return "Gap";
        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Ambiguous)
            return "Ambiguous";
        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Suppressed)
            return "Suppressed";
        if (slot.assignmentState == TrickPoseCoverageAssignmentState.AssignedToPlaceholder)
            return "Placeholder";
        return "Covered";
    }

    private static string BuildSecondaryLabel(TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return string.Empty;
        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Excluded)
            return Truncate(slot.exclusionReason, 24);
        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Ambiguous)
            return Truncate(BuildInvolvedSummary(slot), 24);
        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Suppressed)
            return Truncate(BuildInvolvedSummary(slot), 24);
        if (slot.assignedEntry != null)
            return Truncate(slot.assignedEntry.GetSummary(), 24);
        if (slot.candidateEntryLabels != null && slot.candidateEntryLabels.Count > 0)
            return Truncate(slot.candidateEntryLabels[0], 24);
        return Truncate(slot.shortLabel, 24);
    }

    private static string BuildOwnerSummary(TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return "(none)";
        if (slot.assignedEntry != null)
            return slot.assignedEntry.GetSummary();
        if (slot.candidateEntryLabels != null && slot.candidateEntryLabels.Count > 0)
            return string.Join(", ", slot.candidateEntryLabels);
        return "(none)";
    }

    private static string BuildInvolvedSummary(TrickPoseCoverageSlot slot)
    {
        if (slot == null)
            return string.Empty;

        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Ambiguous)
        {
            List<string> labels = slot.ambiguousEntryIndices != null && slot.ambiguousEntryIndices.Count > 0
                ? BuildLabels(slot, slot.ambiguousEntryIndices)
                : slot.candidateEntryLabels;
            return $"A: {string.Join(" / ", labels)}";
        }

        if (slot.validationStatus == TrickPoseCoverageSlotValidationStatus.Suppressed)
        {
            List<string> suppressed = slot.suppressedEntryIndices != null && slot.suppressedEntryIndices.Count > 0
                ? BuildLabels(slot, slot.suppressedEntryIndices)
                : BuildFallbackSuppressedLabels(slot);
            string winner = slot.assignedEntry != null ? slot.assignedEntry.GetSummary() : "(none)";
            return $"S: {winner} suppresses {string.Join(" / ", suppressed)}";
        }

        return string.Empty;
    }

    private static void DrawCellBorder(Rect rect, bool isSelectedSlot, SelectedEntryInvolvement involvement, bool drawSelectedEntry)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        if (drawSelectedEntry)
        {
            Color selectedEntryColor = involvement switch
            {
                SelectedEntryInvolvement.Owner => new Color(0.30f, 0.80f, 1f, 1f),
                SelectedEntryInvolvement.Candidate => new Color(0.55f, 0.85f, 1f, 1f),
                SelectedEntryInvolvement.Ambiguous => new Color(1f, 0.9f, 0.2f, 1f),
                SelectedEntryInvolvement.Suppressed => new Color(1f, 0.45f, 0.25f, 1f),
                _ => new Color(0.85f, 0.85f, 0.85f, 1f)
            };

            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = selectedEntryColor;
            Handles.DrawAAPolyLine(3f, new Vector3(rect.x + 1f, rect.y + 1f), new Vector3(rect.xMax - 1f, rect.y + 1f), new Vector3(rect.xMax - 1f, rect.yMax - 1f), new Vector3(rect.x + 1f, rect.yMax - 1f), new Vector3(rect.x + 1f, rect.y + 1f));
            Handles.color = previous;
            Handles.EndGUI();
        }

        if (isSelectedSlot)
        {
            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 1f);
            Rect inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            Handles.DrawAAPolyLine(2f, new Vector3(inner.x, inner.y), new Vector3(inner.xMax, inner.y), new Vector3(inner.xMax, inner.yMax), new Vector3(inner.x, inner.yMax), new Vector3(inner.x, inner.y));
            Handles.color = previous;
            Handles.EndGUI();
        }
    }

    private static string BuildSelectedEntryGlyph(SelectedEntryInvolvement involvement)
    {
        return involvement switch
        {
            SelectedEntryInvolvement.Owner => "SEL ",
            SelectedEntryInvolvement.Ambiguous => "\u2605 ",
            SelectedEntryInvolvement.Suppressed => "S! ",
            _ => string.Empty
        };
    }

    private static string BuildSelectedEntryTooltip(SelectedEntryInvolvement involvement)
    {
        return involvement switch
        {
            SelectedEntryInvolvement.Owner => "Selected pose: Owner",
            SelectedEntryInvolvement.Candidate => "Selected pose: Candidate",
            SelectedEntryInvolvement.Ambiguous => "Selected pose: Ambiguous",
            SelectedEntryInvolvement.Suppressed => "Selected pose: Suppressed",
            _ => string.Empty
        };
    }

    private static SelectedEntryInvolvement GetSelectedEntryInvolvement(TrickPoseCoverageSlot slot, TrickPoseCoverageAtlasState state)
    {
        if (slot == null || state == null || !state.highlightSelectedEntry || state.selectedEntryIndex < 0)
            return SelectedEntryInvolvement.None;

        int selectedEntryIndex = state.selectedEntryIndex;
        if (slot.assignedEntryIndex == selectedEntryIndex)
            return SelectedEntryInvolvement.Owner;
        if (slot.ambiguousEntryIndices != null && slot.ambiguousEntryIndices.Contains(selectedEntryIndex))
            return SelectedEntryInvolvement.Ambiguous;
        if (slot.suppressedEntryIndices != null && slot.suppressedEntryIndices.Contains(selectedEntryIndex))
            return SelectedEntryInvolvement.Suppressed;
        if (slot.candidateEntryIndices != null && slot.candidateEntryIndices.Contains(selectedEntryIndex))
            return SelectedEntryInvolvement.Candidate;
        return SelectedEntryInvolvement.None;
    }

    private static List<string> BuildLabels(TrickPoseCoverageSlot slot, List<int> indices)
    {
        List<string> labels = new List<string>();
        if (slot == null || indices == null)
            return labels;

        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            if (slot.candidateEntryIndices == null || slot.candidateEntryLabels == null)
                continue;

            int candidateIndex = slot.candidateEntryIndices.IndexOf(index);
            if (candidateIndex >= 0 && candidateIndex < slot.candidateEntryLabels.Count)
                labels.Add(slot.candidateEntryLabels[candidateIndex]);
        }

        return labels;
    }

    private static List<string> BuildFallbackSuppressedLabels(TrickPoseCoverageSlot slot)
    {
        List<string> labels = new List<string>();
        if (slot == null || slot.candidateEntryIndices == null || slot.candidateEntryLabels == null)
            return labels;

        for (int i = 0; i < slot.candidateEntryIndices.Count && i < slot.candidateEntryLabels.Count; i++)
        {
            if (slot.candidateEntryIndices[i] != slot.assignedEntryIndex)
                labels.Add(slot.candidateEntryLabels[i]);
        }

        return labels;
    }

    private static int FindSlotIndex(
        List<TrickPoseCoverageSlot> slots,
        SkiController.AerialPoseFamily family,
        SkiController.AerialPoseShape shape,
        TrickPoseVerticalOrientationRequirement vertical,
        TrickPoseHorizontalOrientationRequirement horizontal,
        TrickPoseTravelFacingRequirement travelFacing,
        TrickPoseMotionStateRequirement motion)
    {
        int bestIndex = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null ||
                slot.poseFamily != family ||
                slot.poseShape != shape ||
                slot.verticalOrientation != vertical ||
                slot.horizontalOrientation != horizontal ||
                slot.travelFacing != travelFacing ||
                slot.motionState != motion)
            {
                continue;
            }

            if (bestIndex < 0 || Severity(slot.validationStatus) > Severity(slots[bestIndex].validationStatus))
                bestIndex = i;
        }

        return bestIndex;
    }

    private static List<SkiController.AerialPoseFamily> CollectFamilies(List<TrickPoseCoverageSlot> slots)
    {
        List<SkiController.AerialPoseFamily> values = new List<SkiController.AerialPoseFamily>();
        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null)
                continue;

            SkiController.AerialPoseFamily value = slot.poseFamily;
            if (value != SkiController.AerialPoseFamily.None && !values.Contains(value))
                values.Add(value);
        }

        values.Sort();
        return values;
    }

    private static List<SkiController.AerialPoseShape> CollectShapes(List<TrickPoseCoverageSlot> slots)
    {
        List<SkiController.AerialPoseShape> values = new List<SkiController.AerialPoseShape>();
        for (int i = 0; i < slots.Count; i++)
        {
            TrickPoseCoverageSlot slot = slots[i];
            if (slot == null)
                continue;

            SkiController.AerialPoseShape value = slot.poseShape;
            if (value != SkiController.AerialPoseShape.None && !values.Contains(value))
                values.Add(value);
        }

        values.Sort();
        return values;
    }

    private static int Severity(TrickPoseCoverageSlotValidationStatus status)
    {
        return status switch
        {
            TrickPoseCoverageSlotValidationStatus.Gap => 4,
            TrickPoseCoverageSlotValidationStatus.Ambiguous => 3,
            TrickPoseCoverageSlotValidationStatus.Suppressed => 2,
            TrickPoseCoverageSlotValidationStatus.Excluded => 1,
            _ => 0
        };
    }

    private static Color CellColor(TrickPoseCoverageSlot slot, bool matchesFocus, bool dimNonMatching, bool isSelected)
    {
        Color color = slot.validationStatus switch
        {
            TrickPoseCoverageSlotValidationStatus.Excluded => new Color(0.36f, 0.36f, 0.36f),
            TrickPoseCoverageSlotValidationStatus.Gap => new Color(0.80f, 0.34f, 0.34f),
            TrickPoseCoverageSlotValidationStatus.Ambiguous => new Color(0.90f, 0.63f, 0.22f),
            TrickPoseCoverageSlotValidationStatus.Suppressed => new Color(0.79f, 0.58f, 0.24f),
            _ => slot.assignmentState == TrickPoseCoverageAssignmentState.AssignedToPlaceholder
                ? new Color(0.50f, 0.72f, 0.93f)
                : new Color(0.42f, 0.72f, 0.46f)
        };

        if (dimNonMatching && !matchesFocus)
            color = Color.Lerp(color, new Color(0.2f, 0.2f, 0.2f), 0.45f);
        if (isSelected)
            color = Color.Lerp(color, Color.white, 0.18f);

        return color;
    }

    private static bool Contains(string haystack, string needle)
    {
        return !string.IsNullOrWhiteSpace(haystack) &&
               haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;
        return value.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
    }

    private sealed class AtlasSection
    {
        public TrickPoseVerticalOrientationRequirement vertical;
        public TrickPoseHorizontalOrientationRequirement horizontal;
        public TrickPoseTravelFacingRequirement travelFacing;
        public TrickPoseMotionStateRequirement motion;
        public List<SkiController.AerialPoseFamily> families;
        public List<SkiController.AerialPoseShape> shapes;
        public int totalSlots;
        public int coveredCount;
        public int gapCount;
        public int ambiguousCount;
        public int suppressedCount;
    }

    private enum SelectedEntryInvolvement
    {
        None = 0,
        Owner = 1,
        Candidate = 2,
        Ambiguous = 3,
        Suppressed = 4
    }

    private static string DescribeHorizontalInput(SkiController.AerialPoseFamily family)
    {
        return family switch
        {
            SkiController.AerialPoseFamily.Left => "Left",
            SkiController.AerialPoseFamily.Right => "Right",
            SkiController.AerialPoseFamily.Spread => "Both / Spread",
            SkiController.AerialPoseFamily.Neutral => "Neutral",
            _ => family.ToString()
        };
    }

    private static string DescribeVerticalInput(SkiController.AerialPoseShape shape)
    {
        return shape switch
        {
            SkiController.AerialPoseShape.Driving => "Forward / Driving",
            SkiController.AerialPoseShape.LaidOut => "Backward / LaidOut",
            SkiController.AerialPoseShape.Neutral => "Neutral",
            _ => shape.ToString()
        };
    }
}
