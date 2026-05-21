using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.RichDocuments;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentTemplateDefinition
    {
        public string id;
        public string displayName;
        public string summary;
        public string kind;
        public string status = "Draft";
        public string priority = "Normal";
        public string visibility = "PrivateProject";
        public string[] tags = Array.Empty<string>();
        public Func<string> bodyFactory;
        public Func<List<PungentRichDocumentBlock>> blockFactory;

        public void ApplyTo(PungentRichDocument document)
        {
            if (document == null)
                return;

            document.templateId = id ?? "general-document";
            document.kind = string.IsNullOrWhiteSpace(kind) ? "General" : kind;
            document.status = string.IsNullOrWhiteSpace(status) ? "Draft" : status;
            document.priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority;
            document.visibility = string.IsNullOrWhiteSpace(visibility) ? "PrivateProject" : visibility;
            document.summary = summary ?? string.Empty;
            document.tags = tags == null ? new List<string>() : tags.ToList();
            document.bodyText = bodyFactory != null ? bodyFactory.Invoke() : string.Empty;
            document.blocks = blockFactory != null ? blockFactory.Invoke() : new List<PungentRichDocumentBlock>();
            document.NormalizeInPlace();
        }
    }

    public static class PungentRichDocumentTemplates
    {
        private static readonly List<PungentRichDocumentTemplateDefinition> Definitions = BuildDefinitions();

        public static IReadOnlyList<PungentRichDocumentTemplateDefinition> All => Definitions;

        public static PungentRichDocumentTemplateDefinition Find(string id)
        {
            return Definitions.FirstOrDefault(template => string.Equals(template.id, id, StringComparison.OrdinalIgnoreCase)) ?? Definitions[0];
        }

        public static PungentRichDocument CreateDocument(string templateId)
        {
            PungentRichDocumentTemplateDefinition template = Find(templateId);
            PungentRichDocument document = PungentRichDocumentStorage.CreateDocument(template.displayName, template.id);
            template.ApplyTo(document);
            document.title = template.displayName;
            document.Touch();
            return document;
        }

        private static List<PungentRichDocumentTemplateDefinition> BuildDefinitions()
        {
            return new List<PungentRichDocumentTemplateDefinition>
            {
                Template(
                    "general-document",
                    "General Document",
                    "A clean writing surface for notes, docs, and planning copy.",
                    "General",
                    new[] { "document", "draft" },
                    () => Lines(
                        "# General Document",
                        "",
                        "## Summary",
                        "",
                        "Write the useful version first. Keep links, assumptions, and follow-up work visible.",
                        "",
                        "## Notes",
                        "",
                        "- Key point",
                        "- Related target",
                        "",
                        "## Follow-up",
                        "",
                        "- [ ] Review with context",
                        "- [ ] Link authoring targets")),

                Template(
                    "design-document",
                    "Design Document",
                    "A focused structure for feature intent, constraints, decisions, and risks.",
                    "Design",
                    new[] { "design", "decision", "feature" },
                    () => Lines(
                        "# Design Document",
                        "",
                        "## Intent",
                        "",
                        "What this should make possible for the player or tool user.",
                        "",
                        "## Current Constraints",
                        "",
                        "- Existing workflow to preserve",
                        "- Package or runtime boundary",
                        "- Optional dependency behaviour",
                        "",
                        "## Proposed Shape",
                        "",
                        "Describe the smallest useful version.",
                        "",
                        "## Decisions",
                        "",
                        "- Decision:",
                        "- Rationale:",
                        "",
                        "## Risks",
                        "",
                        "- [ ] Compile boundary",
                        "- [ ] Data migration",
                        "- [ ] Editor responsiveness")),

                Template(
                    "implementation-brief",
                    "Implementation Brief",
                    "A build-ready brief with scope, files, validation, and fallback behaviour.",
                    "Implementation",
                    new[] { "implementation", "brief", "validation" },
                    () => Lines(
                        "# Implementation Brief",
                        "",
                        "## Objective",
                        "",
                        "Implement the first useful slice without widening package scope.",
                        "",
                        "## Files",
                        "",
                        "- Runtime:",
                        "- Editor:",
                        "- Provider:",
                        "",
                        "## Required Behaviour",
                        "",
                        "- [ ] Preserve existing serialized data",
                        "- [ ] Register provider idempotently",
                        "- [ ] Keep drawing paths light",
                        "",
                        "## Validation",
                        "",
                        "- [ ] Compile cleanly",
                        "- [ ] Open active UI path",
                        "- [ ] Save and reload data")),

                Template(
                    "dialogue-draft",
                    "Dialogue Draft",
                    "Lightweight game-text drafting for speaker lines, choices, commands, and variables.",
                    "Game Text",
                    new[] { "dialogue", "game-text", "tokens" },
                    () => Lines(
                        "# Dialogue Draft",
                        "",
                        "## Scene Beat",
                        "",
                        "NPC: Welcome back, {playerName}.",
                        "Player: What happened here?",
                        "",
                        "## Choices",
                        "",
                        "- [choice] Ask about the mountain",
                        "- [choice] Offer help",
                        "- [choice] Leave",
                        "",
                        "## Commands",
                        "",
                        "<command:open_quest_log>",
                        "<command:set_flag exampleFlag>",
                        "",
                        "## Notes",
                        "",
                        "Keep this as authoring copy only. Exporters/adapters can consume these conventions later."),
                    blocks: () => new List<PungentRichDocumentBlock>
                    {
                        PungentRichDocumentBlock.Heading("Scene Beat", 2),
                        PungentRichDocumentBlock.SpeakerLine("NPC", "Welcome back, {playerName}."),
                        PungentRichDocumentBlock.DialogueChoice("Ask about the mountain"),
                        PungentRichDocumentBlock.CommandPlaceholder("open_quest_log"),
                        PungentRichDocumentBlock.VariablePlaceholder("playerName")
                    }),

                Template(
                    "quest-outline",
                    "Quest Outline",
                    "A practical quest structure with objectives, states, rewards, and text hooks.",
                    "Game Text",
                    new[] { "quest", "objective", "game-text" },
                    () => Lines(
                        "# Quest Outline",
                        "",
                        "## Premise",
                        "",
                        "A short player-facing reason to care.",
                        "",
                        "## Objectives",
                        "",
                        "- [objective] Find the missing item",
                        "- [objective] Return to the quest giver",
                        "- [objective] Choose a resolution",
                        "",
                        "## States",
                        "",
                        "- Offered:",
                        "- Active:",
                        "- Complete:",
                        "- Failed:",
                        "",
                        "## Rewards",
                        "",
                        "- Item:",
                        "- Unlock:",
                        "- Follow-up token: {questRewardName}"),
                    blocks: () => new List<PungentRichDocumentBlock>
                    {
                        PungentRichDocumentBlock.Heading("Objectives", 2),
                        PungentRichDocumentBlock.QuestObjective("Find the missing item"),
                        PungentRichDocumentBlock.VariablePlaceholder("questRewardName")
                    }),

                Template(
                    "tutorial-script",
                    "Tutorial Script",
                    "A step-by-step tutorial copy draft with UI prompts and completion checks.",
                    "Game Text",
                    new[] { "tutorial", "script", "ui" },
                    () => Lines(
                        "# Tutorial Script",
                        "",
                        "## Goal",
                        "",
                        "Teach one action, then get out of the player's way.",
                        "",
                        "## Steps",
                        "",
                        "1. [step] Move toward {targetName}.",
                        "2. [step] Press {inputInteract} to interact.",
                        "3. [step] Confirm the result.",
                        "",
                        "## Prompt Copy",
                        "",
                        "Hold {inputSprint} to build speed.",
                        "",
                        "## Completion",
                        "",
                        "- [ ] Player performed the action",
                        "- [ ] Hint dismissed safely"),
                    blocks: () => new List<PungentRichDocumentBlock>
                    {
                        PungentRichDocumentBlock.Heading("Steps", 2),
                        PungentRichDocumentBlock.TutorialStep("Move toward {targetName}."),
                        PungentRichDocumentBlock.VariablePlaceholder("inputInteract"),
                        PungentRichDocumentBlock.VariablePlaceholder("inputSprint")
                    }),

                Template(
                    "character-world-bible",
                    "Character / World Bible",
                    "A lore and voice reference for characters, places, factions, and terminology.",
                    "World Bible",
                    new[] { "character", "world", "lore" },
                    () => Lines(
                        "# Character / World Bible",
                        "",
                        "## Character Snapshot",
                        "",
                        "- Name:",
                        "- Role:",
                        "- Wants:",
                        "- Avoids:",
                        "",
                        "## Voice",
                        "",
                        "- Sentence rhythm:",
                        "- Favourite words:",
                        "- Words to avoid:",
                        "",
                        "## World Terms",
                        "",
                        "- Term:",
                        "- Meaning:",
                        "- Player-facing phrasing:",
                        "",
                        "## Continuity Checks",
                        "",
                        "- [ ] Name matches token/source",
                        "- [ ] Lore does not contradict current quest copy")),

                Template(
                    "debug-investigation",
                    "Debug Investigation",
                    "A reproducible investigation log for observed behaviour, evidence, and next checks.",
                    "Debug",
                    "In Progress",
                    "Important",
                    new[] { "debug", "investigation", "evidence" },
                    () => Lines(
                        "# Debug Investigation",
                        "",
                        "## Symptom",
                        "",
                        "What went wrong, where, and how often.",
                        "",
                        "## Reproduction",
                        "",
                        "1. Open",
                        "2. Select",
                        "3. Observe",
                        "",
                        "## Evidence",
                        "",
                        "- Console:",
                        "- Scene/object:",
                        "- Related utility:",
                        "",
                        "## Hypotheses",
                        "",
                        "-",
                        "",
                        "## Next Checks",
                        "",
                        "- [ ] Minimal repro",
                        "- [ ] Boundary check",
                        "- [ ] Save/reload check")),

                Template(
                    "release-note-changelog",
                    "Release Note / Changelog",
                    "A concise release-note draft with user-facing changes and validation notes.",
                    "Release",
                    "Draft",
                    "Normal",
                    new[] { "release", "changelog", "notes" },
                    () => Lines(
                        "# Release Note / Changelog",
                        "",
                        "## Added",
                        "",
                        "-",
                        "",
                        "## Changed",
                        "",
                        "-",
                        "",
                        "## Fixed",
                        "",
                        "-",
                        "",
                        "## Validation",
                        "",
                        "- [ ] Compile cleanly",
                        "- [ ] Smoke test active editor path",
                        "- [ ] Confirm no destructive migration")),

                Template(
                    "token-rich-copy-draft",
                    "Token-Rich Copy Draft",
                    "A copywriting draft designed around raw token placeholders and lightweight validation.",
                    "Game Text",
                    "Draft",
                    "Important",
                    new[] { "copy", "tokens", "localization" },
                    () => Lines(
                        "# Token-Rich Copy Draft",
                        "",
                        "## Copy",
                        "",
                        "Hello {playerName}, your next objective is {questObjective}.",
                        "",
                        "## Token Notes",
                        "",
                        "- {playerName}: player display name",
                        "- {questObjective}: current objective label",
                        "- {locationName}: current location label",
                        "",
                        "## Variants",
                        "",
                        "- Short:",
                        "- Warm:",
                        "- Urgent:",
                        "",
                        "## Checks",
                        "",
                        "- [ ] Token braces balanced",
                        "- [ ] No debug-only token left in player copy"),
                    blocks: () => new List<PungentRichDocumentBlock>
                    {
                        PungentRichDocumentBlock.Heading("Copy", 2),
                        PungentRichDocumentBlock.Paragraph("Hello {playerName}, your next objective is {questObjective}."),
                        PungentRichDocumentBlock.VariablePlaceholder("playerName"),
                        PungentRichDocumentBlock.VariablePlaceholder("questObjective"),
                        PungentRichDocumentBlock.VariablePlaceholder("locationName")
                    }),

                Template(
                    "checklist-definition",
                    "Checklist Definition",
                    "A Markdown checklist authoring document that can create or update a project checklist definition.",
                    "Checklist",
                    "Draft",
                    "Normal",
                    new[] { "checklist", "authoring", "validation" },
                    () => Lines(
                        "# Checklist Definition",
                        "",
                        "Checklist ID: example-checklist",
                        "Target Utility ID: qa-checklist-utility",
                        "List Kind: quality-gate",
                        "State Profile: quality-gate.pass-partial-fail",
                        "Tags: example, checklist",
                        "",
                        "## Setup",
                        "",
                        "- [ ] Open the target workflow. | owner:Author | priority:Normal",
                        "- [ ] Confirm the expected data is visible. | optional:false",
                        "",
                        "## Validation",
                        "",
                        "- [ ] Run the main validation path. | linked:example-state-key",
                        "- [ ] Copy results or guidance for unresolved work. | optional:true")),

                Template(
                    "checklist-to-do-list",
                    "To-Do Checklist",
                    "A lightweight to-do list that creates a checklist definition with owner, priority, and due-date metadata.",
                    "Checklist",
                    "Draft",
                    "Normal",
                    new[] { "checklist", "todo", "planning" },
                    () => Lines(
                        "# To-Do Checklist",
                        "",
                        "Checklist ID: todo-checklist",
                        "List Kind: to-do",
                        "State Profile: todo.not-started-in-progress-blocked-done",
                        "Tags: todo, checklist",
                        "",
                        "## Next Work",
                        "",
                        "- [ ] Draft the first task. | owner:Me | priority:High | due:2026-06-01",
                        "- [ ] Review blockers before marking done. | owner:Me | priority:Normal",
                        "- [ ] Share the finished list. | optional:true")),

                Template(
                    "checklist-review",
                    "Review Checklist",
                    "A document, sheet, or graph review list with approved / needs changes / rejected outcomes.",
                    "Checklist",
                    "Draft",
                    "Normal",
                    new[] { "checklist", "review", "approval" },
                    () => Lines(
                        "# Review Checklist",
                        "",
                        "Checklist ID: review-checklist",
                        "List Kind: review",
                        "State Profile: review.needs-changes-approved-rejected",
                        "Tags: review, checklist",
                        "",
                        "## Readability",
                        "",
                        "- [ ] Title and summary match the actual content. | owner:Reviewer | priority:High",
                        "- [ ] Required references are linked. | owner:Reviewer",
                        "",
                        "## Decision",
                        "",
                        "- [ ] Approval notes are clear enough for the author to act on. | owner:Reviewer")),

                Template(
                    "checklist-release-readiness",
                    "Release Readiness Checklist",
                    "A release readiness list for package, utility, or content delivery checks.",
                    "Checklist",
                    "Draft",
                    "Important",
                    new[] { "checklist", "release", "readiness" },
                    () => Lines(
                        "# Release Readiness Checklist",
                        "",
                        "Checklist ID: release-readiness-checklist",
                        "List Kind: release-readiness",
                        "State Profile: release-migration.not-started-in-progress-done-skipped-blocked",
                        "Tags: release, checklist",
                        "",
                        "## Build",
                        "",
                        "- [ ] Compile cleanly in Unity. | owner:Engineering | priority:Critical",
                        "- [ ] Package boundaries remain removable. | owner:Engineering | priority:High",
                        "",
                        "## Documentation",
                        "",
                        "- [ ] User-facing notes describe the reachable workflow. | owner:Docs",
                        "- [ ] Known caveats are recorded. | optional:true")),

                Template(
                    "checklist-migration",
                    "Migration Checklist",
                    "A migration list for staged refactors, package splits, and data compatibility passes.",
                    "Checklist",
                    "Draft",
                    "Important",
                    new[] { "checklist", "migration", "compatibility" },
                    () => Lines(
                        "# Migration Checklist",
                        "",
                        "Checklist ID: migration-checklist",
                        "List Kind: migration",
                        "State Profile: release-migration.not-started-in-progress-done-skipped-blocked",
                        "Tags: migration, checklist",
                        "",
                        "## Compatibility",
                        "",
                        "- [ ] Old serialized data still loads. | owner:Engineering | priority:Critical",
                        "- [ ] New defaults are applied only when fields are missing. | owner:Engineering",
                        "",
                        "## Cleanup",
                        "",
                        "- [ ] Legacy paths remain as aliases until users have moved. | optional:true")),

                Template(
                    "checklist-bug-triage",
                    "Bug Triage Checklist",
                    "A bug or issue triage list using open, investigating, fixed, verified, and won't-fix states.",
                    "Checklist",
                    "Draft",
                    "Important",
                    new[] { "checklist", "bug", "triage" },
                    () => Lines(
                        "# Bug Triage Checklist",
                        "",
                        "Checklist ID: bug-triage-checklist",
                        "List Kind: bug-triage",
                        "State Profile: bug-triage.open-investigating-fixed-verified-wont-fix",
                        "Tags: bug, triage, checklist",
                        "",
                        "## Intake",
                        "",
                        "- [ ] Reproduction steps are captured. | owner:QA | priority:High",
                        "- [ ] Affected package or utility is identified. | owner:QA",
                        "",
                        "## Resolution",
                        "",
                        "- [ ] Fix or decision is verified in the active editor path. | owner:QA")),

                Template(
                    "checklist-rich-document-review",
                    "Rich Document Review Checklist",
                    "A review checklist tuned for rich document authoring and linked checklist creation.",
                    "Checklist",
                    "Draft",
                    "Normal",
                    new[] { "checklist", "rich-document", "review" },
                    () => Lines(
                        "# Rich Document Review Checklist",
                        "",
                        "Checklist ID: rich-document-review-checklist",
                        "Target Utility ID: rich-document-editor",
                        "List Kind: review",
                        "State Profile: review.needs-changes-approved-rejected",
                        "Tags: rich-document, review, checklist",
                        "",
                        "## Content",
                        "",
                        "- [ ] Headings create stable checklist sections. | owner:Reviewer",
                        "- [ ] Inline checklist metadata is readable. | owner:Reviewer",
                        "",
                        "## Linked Workflow",
                        "",
                        "- [ ] Create/update checklist action produces the expected project definition. | owner:Reviewer")),

                Template(
                    "checklist-data-sheet-review",
                    "Data Sheet Review Checklist",
                    "A review checklist for spreadsheet-style data sheet authoring and sync workflows.",
                    "Checklist",
                    "Draft",
                    "Normal",
                    new[] { "checklist", "data-sheet", "review" },
                    () => Lines(
                        "# Data Sheet Review Checklist",
                        "",
                        "Checklist ID: data-sheet-review-checklist",
                        "Target Utility ID: data-sheet-editor",
                        "List Kind: review",
                        "State Profile: review.needs-changes-approved-rejected",
                        "Tags: data-sheet, review, checklist",
                        "",
                        "## Sheet Shape",
                        "",
                        "- [ ] Required checklist columns are present. | owner:Reviewer",
                        "- [ ] Section and item IDs are stable after sync. | owner:Reviewer",
                        "",
                        "## Results",
                        "",
                        "- [ ] State and comment columns can round-trip expected values. | owner:Reviewer")),

                Template(
                    "checklist-package-release-readiness",
                    "Package Release Readiness Checklist",
                    "A package-focused release list that keeps boundaries, docs, and verification visible.",
                    "Checklist",
                    "Draft",
                    "Important",
                    new[] { "checklist", "package", "release" },
                    () => Lines(
                        "# Package Release Readiness Checklist",
                        "",
                        "Checklist ID: package-release-readiness-checklist",
                        "List Kind: release-readiness",
                        "State Profile: release-migration.not-started-in-progress-done-skipped-blocked",
                        "Tags: package, release, checklist",
                        "",
                        "## Boundaries",
                        "",
                        "- [ ] No new Core-to-lab compile dependency was introduced. | owner:Engineering | priority:Critical",
                        "- [ ] Optional providers degrade with a visible message. | owner:Engineering | priority:High",
                        "",
                        "## Ship",
                        "",
                        "- [ ] Help, registry metadata, and menu paths are reachable. | owner:Docs",
                        "- [ ] Verification report is current. | owner:Release"))
            };
        }

        private static PungentRichDocumentTemplateDefinition Template(
            string id,
            string displayName,
            string summary,
            string kind,
            string[] tags,
            Func<string> body,
            Func<List<PungentRichDocumentBlock>> blocks = null)
        {
            return Template(id, displayName, summary, kind, "Draft", "Normal", tags, body, blocks);
        }

        private static PungentRichDocumentTemplateDefinition Template(
            string id,
            string displayName,
            string summary,
            string kind,
            string status,
            string priority,
            string[] tags,
            Func<string> body,
            Func<List<PungentRichDocumentBlock>> blocks = null)
        {
            return new PungentRichDocumentTemplateDefinition
            {
                id = id,
                displayName = displayName,
                summary = summary,
                kind = kind,
                status = status,
                priority = priority,
                tags = tags,
                bodyFactory = body,
                blockFactory = blocks
            };
        }

        private static string Lines(params string[] lines)
        {
            return string.Join(Environment.NewLine, lines ?? Array.Empty<string>());
        }
    }
#endif
}
