# Verifiable UI Doctrine

Use this reference when changing, moving, restoring, consolidating, hiding, or summarizing visible Unity editor UI.

## Source Truth

Separate four kinds of truth:

* Stated intent: what the request or brief asked for.
* Claimed outcome: what a previous debrief says happened.
* Source reality: what current files actually contain.
* Active UI reality: what the user can see, reach, and use in Unity.

When these disagree, current source reality and active UI reality win. Historical briefs, old summaries, stale audits, and prior claims are context, not proof.

## Evidence Levels

| Evidence | Acceptable for | Not sufficient for |
|---|---|---|
| Current user instruction | Intent, priority, override | Claiming implementation completed |
| Current source file | Source implementation | UI visibility or usability |
| Active Unity UI screenshot/manual test | UI reachability, layout, interaction | Code architecture correctness |
| Snapshot audit | Package state at audit time | Current state after newer changes |
| Debrief/summary | Rationale, claimed goals | Implementation verification |

## No Invisible Implementation

A UI-facing feature is not functionally complete if it only exists in code.

For visible utility work, verify:

1. Current source path exists.
2. Relevant class/method owns the feature or section.
3. Menu, launcher, inspector, context menu, or registry entry opens the expected surface.
4. Window state selects the expected tab, panel, module, foldout, or mode.
5. The active draw/create path calls the section.
6. The section reads current data/state, not a stale duplicate.
7. The control is visible and interactable under normal conditions.
8. The action produces or changes the expected state.
9. Representative narrow and comfortable widths remain usable.
10. No contradictory duplicate stale version is still presented as the active UI.

## Claim Evidence Format

When claiming a UI section was moved, restored, consolidated, fixed, or completed, include evidence when scope warrants it:

* Feature/control: exact feature or section.
* Previous location: where it was before, if known.
* Current source owner: file, class, method, and field/state owner.
* Current active UI path: menu/window/tab/panel/foldout/visibility condition.
* Reachability condition: selection, toggle, install state, developer mode, or optional dependency needed.
* Validation performed: opened window, resized, toggled filters, clicked action, verified output.
* Known caveat: condition under which the claim does not hold.

Use safer wording when proof is partial:

* "Added source support for..."
* "Prepared a panel method for..."
* "Moved in code; needs Unity visual QA..."
* "Intended to replace..."

## No-Hiding Rule

Do not solve a UI problem by hiding functionality users still need.

Hiding, collapsing, filtering, or moving a feature is acceptable only when the new access path is intentional, discoverable, appropriate to the audience, and documented in the implementation summary or brief.

A hidden feature is a regression if:

* the user previously relied on it,
* it remains in intended product scope,
* no replacement path exists,
* it moved to a less discoverable place without a redirect,
* it is reachable only through an obscure toggle,
* it is technically present but clipped, cramped, or buried below impractical scroll depth,
* duplicate active/stale versions make the correct path unclear.

## UI Relocation Contract

For UI moves, consolidations, and cleanup passes, preserve a clear contract:

* Before map: visible controls/sections and where they live.
* After map: new locations and access paths.
* Rationale: why the new location matches the user workflow better.
* Redirect: related-tool link, inline handoff, compatibility alias, or clear label when the old location no longer hosts it.
* Visibility: new location is usable at practical window sizes.
* Persistence: relevant preferences, splitter widths, selected tab, search text, filters, and foldout state remain stable.
* Validation: moved control was tested in the active window after compile/domain reload where possible.

Every control should be one of:

* preserved in place,
* moved with documented path,
* replaced by a better equivalent,
* intentionally removed with user-approved rationale,
* gated behind documented Developer Mode or another appropriate audience gate.

Anything else is a regression.

## UI Refactor Definition Of Done

For passes that change a window, panel, utility browser, debug control center, design validation audit, documentation links window, minimizer, or other visible editor UI, require:

* before control inventory,
* after control inventory,
* list of intentionally removed controls,
* list of moved controls with new access paths,
* list of developer-only or hidden controls with rationale,
* source file/class/method owner for each major section,
* active draw/create path for each major section,
* manual Unity validation steps,
* narrow and comfortable width validation,
* confirmation that no essential action is only reachable through accidental layout expansion.

## Acceptance Questions

A UI refactor is acceptable only when a user can answer these without reading source:

* Where do I start?
* What is selected or filtered?
* What changed after the latest scan/action?
* Where did previous-version controls go?
* Which actions are dangerous?
* Which controls are developer-only or hidden?
* How do I restore hidden or archived utilities?
* How do I open the full tool from a compact/contextual surface?

## Regression Indicators

Treat these as failures:

* Debrief claims a section moved, but active UI still draws the old placement.
* Needed section exists only in a helper method that is not called.
* Panel is technically visible but usable only at impractically wide sizes.
* Cleanup removed the only discoverable access path.
* Filter hides important tools without visible filter indicator or restore path.
* Developer-mode section is required for normal user workflows.
* Visual hierarchy improved while task completion got worse.
