using System.Collections.Generic;
using UnityEngine;

public static class TrickPoseEditorHelp
{
    public static GUIContent Button(string key, string fallback)
    {
        return new GUIContent(fallback, GetTooltip(key));
    }

    public static GUIContent Label(string text, string key)
    {
        return new GUIContent(text, GetTooltip(key));
    }

    public static string GetTooltip(string key)
    {
        return key switch
        {
            "Coverage.ShowHelp" => "Toggle short inline explanations for the current tool and tab.",
            "Coverage.ShowAdvancedHelp" => "Reveal deeper guidance for overlap and slot ownership.",
            "Coverage.ResetHelp" => "Restore onboarding banners and help visibility defaults for the trick pose tooling.",
            "Coverage.BuildSlots" => "Generate canonical intended coverage slots from the active coverage plan.",
            "Coverage.AutoMap" => "Classify each slot against the current profile and map the best current authored entry where one cleanly wins.",
            "Coverage.CreatePlaceholders" => "Legacy placeholder creation for older coverage workflows.",
            "Coverage.PlanField" => "The plan defines the intended legal trick-entry space. It does not contain the final authored poses.",
            "Coverage.ProfileField" => "The authored trick pose profile being evaluated, assigned, and expanded.",
            "Coverage.PreviewTargetField" => "Preview target used to build representative preview contexts and drive visual pose inspection.",
            "Coverage.MatrixFilterGaps" => "Legacy filter for older coverage workflows.",
            "Coverage.MatrixFilterIssues" => "Legacy filter for older coverage workflows.",
            "Coverage.MatrixFilterPlaceholders" => "Legacy filter for older coverage workflows.",
            "Coverage.MatrixFilterExcluded" => "Legacy filter for older coverage workflows.",
            "Coverage.MatrixPage" => "Choose the authored orientation/motion slice to inspect.",
            "Coverage.MatrixAxes" => "The matrix is fixed to Pose Family x Pose Shape.",
            "Coverage.AssignCandidate" => "Copy the selected slot conditions into this entry and treat it as the slot's authored owner.",
            "Coverage.ReplaceCandidate" => "Replace a placeholder-owned slot with this real entry while preserving the slot conditions.",
            "Coverage.AnalyzeSamples" => "Run sampled coverage diagnostics. This validates the authored profile but is secondary to slot-based authoring.",
            "Coverage.CancelSamples" => "Stop the current sampled diagnostics run and keep any cached results unchanged.",
            "Coverage.ClearSamples" => "Clear cached sampled diagnostics results.",
            "Preview.Mode.Sequence" => "Preview a timed authored sequence for motion rhythm, transitions, and presentation.",
            "Preview.Mode.Influence" => "Preview derived pose selection from simulated player inputs, lean, and entry rotation. Angular velocity remains a separate modifier/disambiguation input.",
            "Preview.AutoInfluence" => "When enabled, the preview finds the closest authored entry for the current entry rotation/state and nudges bool/lean controls toward it without changing angular velocity values.",
            "Preview.LeanInput" => "Runtime pose shape uses this lean path: forward lean maps to Driving, backward lean maps to LaidOut, and tuck maps to Compact.",
            "Preview.ManualRotation" => "Manual preview entry rotation in degrees. This represents the orientation at trick/pose entry and is separate from angular velocity.",
            "Preview.Mode.Coverage" => "Preview the currently selected coverage slot from the coverage window.",
            "Preview.AssignSelectedEntry" => "Bind the currently selected profile entry to the selected authored slot.",
            "Preview.CreatePlaceholder" => "Legacy placeholder creation for older coverage workflows.",
            "Preview.CopySlotConditions" => "Copy the selected slot identity into the currently selected entry for refinement or tightening.",
            "Profile.PreviewTarget" => "Scene object used for previewing, capturing, and inspecting authored trick poses.",
            "Profile.SceneEditMode" => "Enable scene editing support for manipulating the selected trick pose on the preview target.",
            "Profile.SnapPreview" => "Refresh the preview immediately instead of relying on slower blended transitions.",
            "Profile.MirrorUtilities" => "Tools for copying or reflecting paired left/right limb data while authoring poses.",
            "Profile.CoverageActions" => "Actions that link the currently selected profile entry with the currently selected coverage slot.",
            "Profile.MatchFamily" => "Limit this entry to a specific derived pose family such as Left, Right, Spread, or Neutral.",
            "Profile.MatchShape" => "Limit this entry to a lean-based pose shape: compact, forward lean, backward lean, or neutral.",
            "Profile.MatchOrientation" => "Legacy orientation modifier. Prefer vertical orientation, horizontal orientation, and motion state for normal authoring.",
            "Profile.MatchAirborne" => "Require the entry to match only airborne, only grounded, or either state.",
            "Profile.MatchPoseHeld" => "Require the pose input to be held, released, or ignored.",
            "Profile.UseAdvancedModifierGates" => "Opt in to using spin, flip, and angular velocity as pose-entry disambiguation gates. Leave this off for normal base-pose authoring; rotation modifiers are composed elsewhere.",
            "Profile.MatchSpin" => "Advanced modifier/disambiguation gate. Only active when Use Advanced Modifier Gates is enabled.",
            "Profile.MatchFlip" => "Advanced modifier/disambiguation gate. Only active when Use Advanced Modifier Gates is enabled.",
            "Profile.MatchAngularRange" => "Advanced modifier/disambiguation range. Only active when Use Advanced Modifier Gates is enabled; angular velocity is best used for modifiers, scoring, or rare disambiguation.",
            "Profile.MatchEntryAngle" => "Advanced/debug gate. Prefer enum slot ownership for normal base-pose authoring.",
            "Profile.MatchPreviewEnable" => "Preview how the selected entry's match conditions evaluate and apply on the scene character.",
            "Profile.MatchPreviewContext" => "Choose whether the preview uses the current scene/controller state or a simulated context built from the selected entry's authored conditions.",
            "Plan.CoverageParticipation" => "Choose which axes participate in coverage planning. Disabled axes collapse to a single wildcard value.",
            "Plan.AllowedValues" => "Choose which legal values are intended for the coverage domain. These create the slot combinations.",
            "Plan.AngularBuckets" => "Optional coarse buckets that turn continuous angular ranges into planned slot targets.",
            "Plan.Exclusions" => "Exclude illegal or intentionally unsupported combinations so they do not count as gaps.",
            "Plan.MatrixLayout" => "Legacy matrix-layout controls; the normal matrix is fixed to family x shape.",
            _ => string.Empty
        };
    }

    public static string GetCoverageTabDescription(string tabName)
    {
        return tabName switch
        {
            "Matrix" => "Browse the authored slot atlas: family x shape, sliced by vertical orientation, horizontal orientation, and motion.",
            "ToAuthor" => "Short list of intended slots that do not have an owner yet.",
            "Overlaps" => "Short list of slots currently matched by more than one entry.",
            "Settings" => "Adjust the plan asset or open advanced sampled diagnostics when needed.",
            _ => string.Empty
        };
    }

    public static string GetPreviewModeDescription(TrickPosePreviewMode mode)
    {
        return mode switch
        {
            TrickPosePreviewMode.Sequence => "Use Sequence Preview to inspect playback timing, transitions, and presentation. It is about authored motion flow, not guaranteed runtime rule coverage.",
            TrickPosePreviewMode.Influence => "Use Influence Preview to simulate derived pose matching from player-like inputs, lean, and entry rotation. Shape mirrors runtime, and body orientation is derived from entry rotation rather than rising/diving motion.",
            TrickPosePreviewMode.Coverage => "Use Coverage Preview when working slot-first. It previews the currently selected coverage slot from the Coverage window and lets you bind or refine authored entries against it.",
            _ => string.Empty
        };
    }

    public static string GetCoverageStatusDescription(TrickPoseCoverageSlotValidationStatus status)
    {
        return status switch
        {
            TrickPoseCoverageSlotValidationStatus.Covered => "Covered cleanly: one authored entry wins this intended slot without ambiguity.",
            TrickPoseCoverageSlotValidationStatus.Ambiguous => "Ambiguous: multiple entries tie for top ownership of the slot.",
            TrickPoseCoverageSlotValidationStatus.Suppressed => "Suppressed: multiple entries match, but one stronger entry wins and shadows the others.",
            TrickPoseCoverageSlotValidationStatus.Gap => "Gap: this intended slot has no authored owner yet.",
            TrickPoseCoverageSlotValidationStatus.Excluded => "Excluded: the plan marks this combination as intentionally out of scope.",
            _ => string.Empty
        };
    }

    public static string GetGlossaryText(string key)
    {
        return key switch
        {
            "Coverage Plan" => "Coverage Plan: the intended legal trick-entry space. It decides what should exist, not how poses are authored.",
            "Slot" => "Slot: a canonical intended cell in condition space. Slots are targets for authoring and assignment.",
            "Placeholder" => "Placeholder: a synthetic TrickPoseEntry generated from a slot so coverage can be blocked out before final pose art is authored.",
            "Covered" => "Covered: one entry cleanly owns the slot.",
            "Gap" => "Gap: the slot is intended but currently unowned.",
            "Ambiguous" => "Ambiguous: more than one entry competes equally for the slot.",
            "Suppressed" => "Suppressed: a weaker matching entry is shadowed by a stronger one.",
            "Excluded" => "Excluded: intentionally removed from the intended design space so it does not count as debt.",
            "Representative Context" => "Representative Context: a concrete preview state chosen to stand in for a slot so the existing preview and diagnostics tools can inspect it.",
            "Pose Shape" => "Pose Shape: lean-based authoring state. Compact means tucked, Driving means forward lean, and LaidOut means backward lean / extended.",
            "Sampled Diagnostics" => "Sampled Diagnostics: sampled validation across many states. Useful for secondary checking, but not the primary authoring model.",
            _ => string.Empty
        };
    }

    public static string GetWorkflowGuide()
    {
        return "1. Assign a profile.\n2. Assign a preview target.\n3. Create or select a coverage plan.\n4. Build slots.\n5. Auto-map current entries.\n6. Inspect the matrix.\n7. Fill remaining gaps or placeholders.";
    }

    public static string GetInspectorHelp()
    {
        return "Author and refine final TrickPoseEntry rules here. Prefer Base Pose Entry Conditions and Entry Rotation Conditions for choosing the pose entered. Treat spin/flip/angular velocity as a modifier or advanced disambiguation layer.";
    }

    public static string GetCoverageActionHelp()
    {
        return "Assign maps the selected entry to the selected slot. Adopt copies the slot's conditions into the current entry. Replace Placeholder promotes a real entry into a slot that is currently owned by a placeholder.";
    }

    public static IEnumerable<string> GetGlossaryEntries()
    {
        yield return GetGlossaryText("Coverage Plan");
        yield return GetGlossaryText("Slot");
        yield return GetGlossaryText("Placeholder");
        yield return GetGlossaryText("Covered");
        yield return GetGlossaryText("Gap");
        yield return GetGlossaryText("Ambiguous");
        yield return GetGlossaryText("Suppressed");
        yield return GetGlossaryText("Excluded");
        yield return GetGlossaryText("Representative Context");
        yield return GetGlossaryText("Pose Shape");
        yield return GetGlossaryText("Sampled Diagnostics");
    }
}
