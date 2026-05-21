---
name: pungentfunk-unity-utility-doctrine
description: Use when auditing, refactoring, designing, implementing, final-optimising, polishing, or production-hardening PungentFunk Utilities Unity editor/runtime tools; preserves mature tool quality, existing workflows, package modularity, Authoring Data Foundation contracts, Core/lab/project-adapter boundaries, Unity Editor implementation doctrine, shared project scan pipelines, registry metadata, repaint safety, preview/apply flows, optional bridges, package hardening, responsive utility UI rules, and PFU-specific universal design principles for hierarchy, learnability, interaction safety, polish, accessibility, and robustness.
---

# PungentFunk Unity Utility Doctrine Skill

## Purpose

Use this skill when modifying, auditing, refactoring, or designing scripts for the PungentFunk Utilities suite.

This skill converts the PungentFunk Utilities Architecture and Design Principles Bible into practical Codex behaviour. It should guide implementation decisions, review criteria, patch structure, and final summaries for Unity editor/runtime utility work.

The goal is not merely to make code compile. The goal is to preserve the product architecture, Unity Editor implementation doctrine, package boundaries, safety model, visual language, performance standards, and release-readiness expectations of the PungentFunk Utilities ecosystem.

## Quality calibration

Use this doctrine to improve the tool the user already has, not to reduce it to the smallest acceptable implementation.

For mature, near-complete, or user-approved tools:

* preserve existing strengths, visual polish, workflows, shortcuts, menu aliases, and advanced affordances unless they are directly broken,
* optimise, harden, and clarify the current implementation before redesigning it,
* treat regressions in capability, usability, layout density, discoverability, or editor-native feel as bugs,
* avoid replacing a rich workflow with a thinner generic one unless the user explicitly asks for simplification,
* keep enhancements proportionate to the user's requested finishing pass, including meaningful UX polish and performance fixes where they reduce real friction,
* preserve behaviour first, then improve internals, then refine presentation.

When a request says "final optimisation", "polish", "finish", "refine", "make production-ready", or similar, assume the user wants the existing tool elevated, not slimmed down. Make targeted improvements that keep or improve the user's current capabilities.

Control labels should be concise, action-specific, and mode-aware. Prefer labels like "Save and Close", "Close", "Open Full Editor", "Create Linked Note", or "Delete" over ambiguous placeholders such as "Done" when the result could mean save, close, return to preview, or commit. Pair compact labels with clear tooltips, especially for destructive actions, full-tool handoffs, preview/edit toggles, and save/close controls.

## Verifiable implementation standard

Distinguish intent, source changes, and active Unity UI reality.

For implementation and UI claims:

* Current source files outrank old debriefs, historical briefs, previous conversation summaries, and stale audits.
* Active Unity UI reality outranks source intent for visibility, reachability, layout, and usability claims.
* A UI-facing feature is not fully implemented merely because code exists. It also needs an active call path, an access surface, visible/reachable UI, usable controls at representative sizes, and validation of the intended interaction.
* Do not say "restored", "moved", "implemented", "fixed", or "completed" unless the claim was checked against current files and, for UI work, the active UI path.
* Use precise wording when evidence is partial: "added source support", "prepared the panel method", "moved in code; needs Unity visual QA", or "intended to replace".
* State anything unverified explicitly in the final response.

For UI moves, restorations, consolidations, or cleanup passes, use the evidence contract in `references/verifiable-ui-doctrine.md`.

## Instruction surface discipline

Keep this skill focused on doctrine-guided audits, refactors, implementation passes, polishing passes, and production hardening for PungentFunk Utilities.

When the task is really about repository-wide process, path-specific coding rules, documentation architecture, or design-bible maintenance, keep those concerns in their proper instruction surface:

* Repository-wide build, validation, branch, package, and review conventions belong in repo instructions or project documentation.
* Editor-only C# rules belong in path-specific Editor instructions when that instruction surface exists.
* Runtime-only C# rules belong in path-specific Runtime instructions when that instruction surface exists.
* Human-facing doctrine, glossary, examples, diagrams, and per-lab specs belong in design-bible or companion docs.
* This skill should summarize and route to those sources, not duplicate them in full.

Use bundled references only when the request needs them:

* Read `references/canonical-examples.md` when examples would clarify a patch, audit, review, or implementation brief.
* Read `references/documentation-layers.md` when the user asks to improve the skill, design bible, per-lab specs, repo instructions, documentation workflow, changelog, or review process.
* Read `references/verifiable-ui-doctrine.md` when changing, moving, restoring, consolidating, hiding, or summarizing visible Unity editor UI.
* Read `references/resizable-panel-doctrine.md` when changing split panels, draggable borders, toolbar/filter/action rows, panel stretching, overlay trays, popup surfaces, help affordances, documentation links, or generated documentation UI.
* Read `references/workflow-minimal-surface.md` when changing sidebars, toolbars, dropdowns, overlays, popups, tabs, metadata/preset controls, Palette Designer-style workflows, or any UI layout where control frequency and user sequence determine surface choice.
* Read `references/universal-design-principles.md` when changing visible UI, tool layout, control grouping, toolbar/overlay behaviour, graph/canvas/table presentation, accessibility, destructive actions, empty states, scan/apply feedback, or final polish.
* Read `references/shared-scan-pipeline.md` when adding or changing project searches, scans, validation, coverage checks, indexing, token/documentation checks, findings, project facts, background jobs, or scan result UI.
* Read `references/package-modularity-authoring-foundation.md` when adding or moving utilities, providers, editors, scanners, authoring data, notes/roadmap features, browser/editor surfaces, package gates, bridges, package metadata, package-split/release-plan work, or extension ownership.
* Read `references/shared-utility-consistency.md` when changing Developer Mode gates, shared window chrome, minimize/tray behaviour, package badges/cards, utility descriptors, per-package registrars, package presence/simulation, utility cards, browser settings, or global utility affordances.

## When to use this skill

Use this skill for requests involving:

* PungentFunk Utilities
* Unity editor tools
* Utility Core, Control Panel, lab hubs, utility registry, utility descriptors, tray/minimizer, documentation linking, developer mode, theme/appearance tools, design validation, audit tools, token validators, note editors, utility browsers, asset placement labs, colour tools, audio tools, texture tools, scene workflow tools, debug tools, content generators, visualization tools, or package hardening
* Refactoring editor scripts into reusable utility modules
* Auditing scripts against the design bible
* Creating Codex update passes for this suite
* Producing code patches that must fit the PungentFunk Utilities architecture

Do not use this skill for unrelated gameplay systems unless the user explicitly asks to adapt them into PungentFunk Utilities or derive generic utility features from them.

---

# Core doctrine

PungentFunk Utilities is a family of modular Unity editor/runtime utility assets, not a monolithic toolbox.

Every implementation should preserve this model:

* Core provides registry, lab launching, shared theme, preferences, common widgets, package metadata, shared scan/result models, documentation conventions, and implementation doctrine.
* Labs own their domain models and workflows.
* Runtime assemblies must not reference `UnityEditor`.
* Editor assemblies may reference runtime assemblies and Core.
* Optional bridges must be removable and must degrade gracefully when absent.
* Project adapters may reference game-specific classes, but generic packages must not.
* Game-specific assumptions must not leak into generic labs.
* Core must not become a product-feature sink. It owns stable contracts and shared infrastructure; concrete workflows, advanced editors, domain scanners, generators, and package-specific behaviour belong in labs, extensions, bridges, or project adapters.

When uncertain, choose the option that best preserves user value while reducing architectural risk:

* the smallest generic utility that still preserves the requested capability and polish,
* the thinnest project adapter that still supports project-specific needs cleanly,
* the safest preview/apply flow that does not bury common work behind unnecessary ceremony,
* the clearest layout that keeps important mature-tool controls discoverable,
* the strictest dependency boundary that does not remove useful optional integration,
* the most accessible communication pattern that still feels efficient for expert Unity users,
* and the Unity Editor integration point closest to where the user naturally needs the control.

Do not interpret "smaller", "thinner", or "stricter" as permission to remove useful behaviour, flatten a mature workflow, or downgrade a refined tool into a minimal generic one. If preserving quality requires a larger but well-bounded patch, take the larger patch.

---

# Operating rules for Codex

## 1\. Start with architecture classification

Before editing code, classify the task into one or more of these implementation roles:

* EditorWindow / Lab Window
* CustomEditor
* Focused Inspector
* PropertyDrawer
* TreeView / MultiColumnView
* Scene GUI / Handles
* Scene View Overlay
* EditorTool
* Context Menu
* Shortcut Command
* Search Provider
* Runtime Service
* Optional Bridge
* Graph / Canvas Tool
* Hybrid

Choose the role based on authoring context, not based on where the existing code happens to live.

Use this selection guide:

* Use an EditorWindow for workflows with sources, filters, previews, generated results, scan/apply stages, dashboards, tabs, graphs, or persistent workspaces.
* Use a CustomEditor for compact selected-object validation, setup helpers, object-specific toolbars, and Open in Lab handoffs.
* Use a PropertyDrawer for reusable field-level UX.
* Use TreeView, ListView, or MultiColumn views for dense, hierarchical, audit-style, or large result data.
* Use Scene GUI, Handles, Overlays, or EditorTools for direct spatial authoring.
* Use Context Menus for concise selected-object, asset, folder, material, prefab, texture, terrain, palette, or ScriptableObject actions.
* Use Shortcuts only for frequent, safe, easy-to-describe commands.
* Use Graph/Canvas tools only when visible relationships, branching, dependency, pipeline, or spatial organization is the primary authoring problem.
* Use optional bridges for cross-lab integrations.

Do not force every workflow into a long vertical inspector or a single control-panel window.

## 2\. Preserve package boundaries

Respect these dependency rules:

Allowed:

* Lab -> Core
* Editor -> Runtime
* Project Adapter -> Generic Lab
* Bridge -> Lab A + Lab B

Forbidden:

* Core -> Lab
* Runtime -> Editor
* Generic Lab -> game-specific project class
* Cyclic lab dependencies
* Compile-time references to optional labs unless isolated in a bridge asmdef or guarded by version defines / reflection-safe discovery

When a feature links two labs, prefer:

* registry lookup,
* descriptor metadata,
* service interface,
* reflection-safe detection,
* optional asmdef bridge,
* version define,
* define constraint,
* or disabled-but-helpful UI state.

Do not add accidental `using` directives that make one lab hard-dependent on another.

## 3\. Separate drawing from mutation

Editor rendering code must not own expensive scans, asset mutation, scene mutation, graph mutation, or broad project operations.

For editor windows and panels:

* OnGUI / CreateGUI / Bind / Draw methods should render current state.
* Scan, validation, indexing, and discovery should be explicit, cached, refreshable, and routed through shared project scan/session/result systems where the scope is broad or reusable.
* Apply operations should be explicit.
* Batch operations should use preview/dry-run where practical.
* Scene or asset changes must use Undo where possible.
* AssetDatabase calls must be explicit, batched where appropriate, and isolated to editor code.

Never scan the project, rebuild all results, write assets, call heavy reflection, or call `SceneView.RepaintAll()` from routine repaint paths unless there is a documented reason.

Authoring browsers and overlay previews must stay smooth at scale. Provider enumeration, preview hydration, document parsing, graph layout, sheet cell indexing, validation, and target counting should be cached, lazy, or explicitly refreshed; repaint paths should consume prepared state and visible-window slices rather than recomputing whole-provider previews or full-sheet/graph indexes.

Any utility that searches assets, scenes, prefabs, references, scripts, documentation, tokens, registry metadata, optional dependencies, setup state, release readiness, or other broad project facts must first check whether the shared project scan pipeline can provide or cache the data. Prefer extending the shared pipeline through a provider, local scan session, cached result, shared finding model, or project audit index entry over creating a parallel scanner.

## 4\. Default to safe workflows

For destructive, broad, or expensive operations, require a safe flow:

1. Configure
2. Scan / Validate
3. Preview
4. Apply Selected or Apply All
5. Summarize changed / skipped / failed items

Use warnings before modifying:

* shared materials,
* prefab assets,
* imported assets,
* scenes,
* project-wide files,
* generated assets that may overwrite user-authored assets,
* or anything irreversible.

Prefer inline help boxes for recoverable issues. Reserve modal dialogs for destructive, irreversible, or workflow-blocking decisions.

## 5\. Preserve Unity-native behaviour

PungentFunk tools should feel native to the Unity Editor while retaining a recognizable PungentFunk identity.

Use Unity-standard semantics:

* Buttons trigger actions.
* Toggles enable states.
* Dropdowns select one option.
* Radio groups select mutually exclusive modes.
* Sliders adjust ranges.
* Numeric fields set exact values.
* Object fields reference assets or scene objects.
* Search fields filter.
* Tabs separate workflows.
* Toolbars group compact modes/actions.
* Progress bars report long work.
* Help boxes explain state.

Use familiar Unity states:

* selected,
* focused,
* disabled,
* hover,
* active/on,
* warning,
* error,
* success,
* override,
* locked,
* refresh,
* apply,
* search,
* filter,
* drag/drop.

Do not use decorative UI that competes with user content.

## 6\. Apply responsive layout doctrine

Every tool window must remain usable at small sizes, while still using extra space intelligently at large sizes.

Use a cohesive shell with purpose-built interiors:

* Keep shared PungentFunk surface traits consistent: theme tokens, panel framing, splitter behaviour, status/message patterns, headers, toolbar language, icons, empty states, and control semantics.
* Let each utility's internal layout, field grouping, visualizations, and control density follow its task. Do not force unrelated tools into the same panel arrangement when a different arrangement would make the tool clearer or more powerful.
* Prefer the interface that lets the user understand, navigate, and act on the tool's data most effectively, even when that means custom controls, graphs, diagrams, previews, canvases, or richer visualizations.
* Treat cohesion as a shared design language, not a requirement that every window has the same composition.

Panel sizing requirements:

* Panels inside windows should stretch to fill their allocated space.
* Internal panel borders should be resizable where two or more meaningful panels compete for space.
* Split layouts should use a shared resizable panel contract or document why a special local splitter is needed for a canvas, graph, matrix, timeline, or preview workflow.
* Resize handle orientation must match motion: vertical dividers resize left/right width with horizontal mouse movement; horizontal dividers resize top/bottom height with vertical mouse movement.
* Drag direction must match visual ownership unless an inverted/right-anchored or bottom-anchored handle is explicitly named and documented.
* Saved splitter values must be clamped against current available size and sibling minimums before drawing.
* At least one panel in a split must absorb remaining space after fixed regions, handles, padding, headers, toolbars, and footers are allocated.
* Persist splitter sizes through `UtilityWindowPrefs` or UI Toolkit view data where practical.
* Respect useful minimum sizes for each panel so fields remain visible, understandable, and interactable.
* When a panel becomes too small to show its primary task safely, show a compact state, collapsed summary, overflow affordance, or clear resize cue instead of letting controls overlap or disappear silently.

Field and content resizing requirements:

* Fields should resize contextually with the panel, but not at the expense of legibility or accurate input.
* Horizontally arranged controls should wrap, stack, collapse into compact variants, or move into menus before they are clipped.
* Toolbar, filter, chip, and action rows should wrap by semantic groups. Do not split labels from fields, icons from labels, or unrelated controls into one crowded line just because wrapping exists.
* Control groups should have minimum, preferred, and maximum widths. Flexible search/text fields may expand; fixed action buttons should not balloon.
* Wide, short panels may spread related controls horizontally when labels and inputs remain readable.
* Narrow panels should stack field groups vertically, preserve label/input relationships, and keep primary actions reachable.
* Dense lists may use responsive grids or multiple columns when there is enough width, then collapse back to one column when space is constrained.
* Use proportional widths, min/max widths, and measured label/input regions instead of fixed magic widths where content needs to adapt.

Prefer:

* vertical scrolling for whole-window or panel content that extends below the visible area,
* wrapping rows,
* compact cards or rows only where they improve scanning,
* collapsible sections,
* tabs,
* paging,
* stacked columns at narrow widths,
* draggable splitters where panels compete for space,
* persistent splitter sizes via UtilityWindowPrefs or UI Toolkit view data,
* stable panel positions during refresh,
* responsive grids for lists, swatches, cards, result tiles, and previews when extra width improves comprehension.

Avoid:

* long undifferentiated walls of fields,
* excessive nested foldouts,
* large empty gaps,
* controls that jump during refresh,
* important actions buried below large scroll regions,
* duplicate buttons doing the same thing,
* horizontal scrollbars as the first response to a narrow panel,
* layouts that assume a wide monitor.

Scrollbar policy:

* Vertical scrollbars are acceptable for windows or panels when content extends past the bottom.
* Horizontal scrollbars should be sparse and intentional.
* Before adding a horizontal scrollbar, try responsive reflow: wrap rows, stack fields, shorten secondary labels, move secondary actions to a toolbar/menu, use tabs, collapse advanced sections, or switch lists from multi-column to single-column.
* Add horizontal scrolling when the content is inherently horizontal or navigational, such as timelines, matrices, dependency graphs, node canvases, large comparison tables, sprite sheets, waveform/sequence views, or other panels whose design explicitly calls for it.
* If horizontal scrolling becomes necessary because individual fields can no longer remain legible within the panel, make it a deliberate panel-level overflow behaviour and keep primary actions visible outside the horizontal scroll region where possible.

Use visual and structural affordances where they improve the tool:

* Use graphs for relationships, dependencies, branching, pipelines, progression, state machines, or influence networks.
* Use diagrams for spatial, procedural, or conceptual explanations.
* Use previews for assets, generated results, colours, materials, audio, texture operations, prefabs, scenes, layouts, or before/after comparisons.
* Use custom controls when standard Unity fields hide important meaning or make a workflow slower.
* Keep rich visuals interactive when useful: selectable nodes, zoom/pan, filters, legends, tooltips, inline details, and clear reset/focus controls.
* Do not add visuals as decoration. Add them when they help the user inspect, compare, decide, configure, or apply changes.

Help and documentation affordances:

* Keep help visually calm. Prefer one help affordance per section/header cluster instead of a noisy help button beside every field.
* Help affordances must preserve context: utility ID, tab/module, section, selected item where relevant, and topic anchor.
* Help buttons must not crowd polished headers, filters, or action rows; use right-aligned icon slots, contextual help rails, or overflow menus when space is tight.
* Generated documentation surfaces must preserve curated content, label generated/stale/draft/imported sources, and filter inherited Unity/Object noise by default.

## 7\. Use the shared visual system

For IMGUI tools:

* Use `UtilityWindowTheme`.
* Use `UtilityWindowPrefs` for persistent panel/window/view state.
* Use shared helpers for headers, panels, section titles, count/status pills, tinted buttons, warning/info/help boxes, splitter handles, and shared text styles.
* Do not scatter one-off GUIStyle definitions unless strictly local and justified.

For UI Toolkit tools:

* Map styling to the same design tokens.
* Use USS classes rather than inline style sprawl.
* Preserve view data for scroll position, selection, expansion, and splitter sizes.
* Use ListView or collection views for large repeating records.

Repeated UI concepts should become shared widgets/helpers when they appear in more than one lab.

Common reusable controls include:

* search bars,
* enum-with-description rows,
* labelled sliders,
* object rows,
* splitters,
* status pills,
* scan-result rows,
* warning/info boxes,
* mini toolbars,
* preview cards,
* swatch chips,
* count badges,
* lock/refresh/apply controls,
* dependency notices,
* empty-state cards,
* progress panels.

## 8\. Enforce accessibility and messaging standards

For every meaningful UI change:

* Do not rely on colour alone.
* Pair colour with labels, icons, shapes, patterns, or tooltips.
* Maintain readable contrast.
* Preserve visible focus states where feasible.
* Use logical keyboard navigation where the UI technology supports it.
* Avoid tiny click targets for frequent actions.
* Use plain-language labels for common operations.
* Use domain terms only when they help.

Empty states should explain what is missing and offer one clear next action.

Errors should state:

* the problem,
* likely cause,
* next useful step.

Warnings should identify risk before the user applies changes.

Info messages should confirm state without blocking workflow.

## 9\. Registry and descriptor requirements

Every significant utility should be registered through `PungentUtilityRegistry` or the relevant per-lab registrar.

Descriptors should include:

* stable kebab-case ID,
* display name,
* lab,
* module,
* category,
* sort order,
* package status,
* one-sentence description,
* tags,
* menu path,
* capability flags,
* implementation role,
* related utility IDs,
* optional dependency information where relevant.

Package status values should be used consistently:

* Stable
* Experimental
* In Progress
* Deprecated
* Project Adapter

Menu policy:

* Primary editor menus: `Tools/PungentFunk Utilities/...`
* Asset context menus: `Assets/PungentFunk Utilities/...`
* GameObject context menus: `GameObject/PungentFunk Utilities/...`
* CreateAssetMenu root: `PungentFunk Utilities/\[Lab]/\[Asset Type]`

Legacy aliases are allowed only as compatibility routes into generic tools and should be marked internally.

## 10\. Runtime/editor separation

Runtime scripts:

* must compile in player builds,
* must not reference `UnityEditor`,
* should contain runtime-safe data, ScriptableObjects, MonoBehaviours, adapters, or services,
* should use stable IDs/version fields where data crosses assets or package boundaries,
* should not store editor-window state unless it has runtime meaning.

Editor scripts:

* should live in Editor folders or editor asmdefs,
* may use UnityEditor,
* should use SerializedObject/SerializedProperty where preserving Undo, prefab overrides, and multi-object editing matters,
* should use Undo for scene/object changes,
* should use AssetDatabase safely for asset changes,
* should separate UI drawing from scan/apply logic.

Avoid destructive runtime class renames unless a compatibility wrapper or migration path is provided.

## 11\. Prefab, asset, and import safety

Prefab-facing tools must distinguish:

* prefab assets,
* prefab instances,
* variants,
* nested prefabs,
* Prefab Mode isolation,
* in-context prefab editing.

Do not silently apply scene-instance choices to prefab assets.

Generated assets should:

* preserve GUID/meta stability where possible,
* avoid delete/recreate churn when update-in-place is safe,
* use deterministic names where useful,
* avoid overwriting user-authored assets without explicit consent.

Batch asset operations should avoid import/refresh churn and should group AssetDatabase operations where practical.

## 12\. Performance standards

Never implement a tool that depends on expensive work during repaint.

Required standards:

* use shared scan result, severity, issue, scope, session, cache, and summary models when scan data can be reused,
* cache scan results and expose stale/fresh/running/cancelled/failed/not-configured states,
* refresh explicitly or only when relevant settings change,
* avoid project-wide scans on every visual refresh,
* avoid `Thread.Sleep` in editor tools,
* avoid broad `SceneView.RepaintAll()` unless justified,
* preserve row/node identity in large lists, trees, and graphs,
* use virtualization or pooling for large UI Toolkit views,
* stage long operations through editor update queues, delayed calls, or progress panels,
* provide cancellation for expensive scans where practical,
* distinguish local scan sessions from reusable project audit providers,
* make background scans cooperative, idle-aware, cancelable/pausable where practical, and unobtrusive,
* recover from domain reload, code reload, Play Mode transitions, docking/floating, close/reopen, and saved layouts.

Static state is a cache, not source-of-truth user data.

---

# Review process before editing

Before making code changes, perform this internal review:

1. Identify the affected lab/core/project-adapter boundary.
2. Determine whether the change belongs in Core, a lab, a bridge, or a project adapter.
3. Identify package ownership, package tier, required/optional dependencies, provided/consumed capabilities, extension points, and missing-package fallback behaviour.
4. Check whether the change violates the Core Anti-Sink Rule by moving concrete lab workflow into Core.
5. For authoring, notes, roadmap, documents, boards, sheets, help, token, audit, utility, or external-target work, check whether shared Authoring Data Foundation IDs, link targets, metadata, preview/action/validation providers, and missing-provider behaviour apply.
6. For Developer Mode, package presence, and utility chrome work, check the shared root gate, shared window chrome, minimizer/tray, package badge/card, help context, and presence resolver contracts before adding local UI.
7. For registry work, check descriptor metadata saturation and per-package registrar ownership; Core should not register concrete extension utilities as a long-term architecture.
8. Identify whether runtime/editor separation is preserved.
9. Identify the correct Unity Editor integration point.
10. Identify the tool maturity level: prototype, active build-out, mature/near-complete, or legacy compatibility path.
11. Check which existing behaviours, workflows, polish, and user-facing affordances must be preserved.
12. For UI layout work, map the workflow goal, normal sequence, iteration loop, validation/refinement phase, and apply/export/save phase before moving controls.
13. Classify controls by frequency and context: constant, frequent, conditional, rare/setup, advanced, destructive, developer-only; global, selected item, result, preset, metadata, package state, help, developer/debug.
14. Identify the primary visual focal point, secondary support regions, and tertiary chrome/metadata so the eye lands where the workflow begins.
15. Confirm grouping follows workflow meaning, proximity, alignment, and scan order rather than panel symmetry alone.
16. Choose the smallest surface that preserves discoverability and usability: workbench, toolbar, dropdown/action menu, overlay tray, popup, workflow tab, sidebar, modal, or full window.
17. Check whether hidden, tucked, collapsed, or moved controls remain recognizable, labelled, tooltip-backed, and discoverable from the context where users need them.
18. Classify important actions by feedback and recovery needs: idle, hover/focus, pending/running, success, failure, cancel, undo/revert, and retry where relevant.
19. Check whether the layout survives long labels, dense data, empty data, narrow docks, short windows, domain reload, and missing providers.
20. Check whether the existing UI layout matches the workflow.
21. Check whether panels fill allocated space and whether competing panels need resizable, persistent splitters.
22. For split layouts, identify handle ownership, orientation, drag direction, available-size clamp, sibling minimums, persistence key, and flexible region before editing.
23. Check whether fields reflow, wrap, stack, or grid responsively before introducing horizontal overflow.
24. For toolbar/filter/action rows, identify semantic groups, min/preferred/max widths, wrapping order, and the horizontal-scroll exception if one is justified.
25. Check whether vertical scrolling is sufficient, or whether a specific panel genuinely needs horizontal scrolling.
26. Check whether help/documentation affordances preserve utility, section, topic, and selected context without crowding rows.
27. For transient popup-like workflows, decide whether a shared overlay tray is more appropriate than a detached popup window, utility window, or modal dialog.
28. Check whether graphs, diagrams, previews, custom controls, or richer visualizations would make the tool more understandable or powerful.
29. For UI work, identify the active access path, draw/create path, visibility conditions, and state owner before claiming the user can reach the change.
30. For UI cleanup or relocation, create a before/after control inventory and mark every control as preserved, moved, replaced, intentionally removed, or deliberately gated.
31. For scan-like work, classify it as local validation, local scan session, project audit provider, background-safe provider, immediate-only provider, generated index, or apply operation.
32. Check whether an existing shared provider, cached result, project audit index entry, shared scan session, or shared result model can satisfy the need.
33. Check whether scan/apply logic is outside repaint and whether scan UI reads cached state before requesting a rescan.
34. Check whether optional dependencies are guarded.
35. Check whether Undo, preview, and safety messaging are needed.
36. Check whether registry, package metadata, menu paths, package ownership, and access surfaces need updates.
37. For package split/release-plan work, prefer staged architecture hardening over rewrite: metadata first, registrar/menu ownership split, asmdef boundaries, provider ownership split, then bridges after standalone packages compile.
38. Check whether new shared UI, chrome, tray, scan, findings, help, token, authoring, package-presence, or background-work patterns should become reusable helpers instead of one-off code.

Keep this review lightweight and decision-oriented. Use it to prevent regressions and misplaced code, not to reopen settled product decisions or redesign mature tools without need.

---

# Patch strategy

When implementing changes:

* Prefer targeted patches over rewrites, but do not confuse targeted with minimal.
* Preserve public/serialized APIs unless the user explicitly requests migration.
* Keep compatibility wrappers when renaming serialized runtime components.
* Do not remove legacy menu aliases unless confirmed obsolete.
* Preserve existing user workflows, polish, and affordances while routing them into improved generic systems.
* Avoid creating duplicate systems when an existing shared helper can be extended.
* Keep Core boring and stable.
* Keep project-specific logic out of generic packages.
* Isolate optional integrations.
* Add comments only where they clarify architecture, safety, or non-obvious Unity Editor behaviour.

For optimisation and finishing passes:

* prefer additive hardening, polish, layout improvements, caching, validation, clearer messaging, and bug fixes over simplification,
* keep advanced controls available when they are part of the tool's value,
* avoid broad rewrites that replace known-working behaviour with new unproven structure,
* avoid cosmetic churn that changes layout without improving workflow clarity,
* verify that each change either fixes a concrete issue, preserves a strength, or improves production readiness.

---

# Output format for Codex responses

When returning a completed pass, summarize in this order:

## Summary

Briefly state what changed and why.

## Files changed

List each changed file with one concise note.

## Architecture notes

State how the change preserves:

* Core/lab boundaries,
* runtime/editor separation,
* optional integration safety,
* registry/menu consistency,
* and project-adapter isolation.

## UI/UX notes

State how the change improves:

* layout,
* responsiveness,
* visual hierarchy,
* empty states,
* messaging,
* accessibility,
* and Unity-native behaviour.

## Safety/performance notes

State how the change handles:

* scan caching,
* repaint safety,
* Undo,
* preview/apply,
* AssetDatabase batching,
* prefab/scene safety,
* domain reload resilience.

## Evidence and verification

State what was actually verified:

* source files/classes/methods checked for key claims,
* active UI path checked for visible UI claims,
* manual Unity validation performed,
* narrow/wide layout validation performed,
* splitter owner/orientation/drag/clamp/persistence validation performed where split layouts changed,
* narrow/comfortable/wide/short/tall layout validation performed where control rows or stretching panels changed,
* help/documentation context validation performed where help affordances changed,
* overlay tray owner/path/position/focus/dismissal/full-tool handoff validation performed where trays or popup surfaces changed,
* shared scan provider/session/cache/result usage verified where project search or validation changed,
* scan freshness, last-scan age, scope, background state, and explicit refresh behaviour verified where scan UI changed,
* anything not verified.

## Validation steps

Provide concrete Unity validation steps, such as:

* open specific window,
* run scan,
* resize window narrow/wide,
* test empty state,
* test missing optional dependency state,
* test domain reload,
* test Play Mode transition,
* test prefab/scene operation,
* verify console has no compile errors.

## Known limitations

Mention anything intentionally not solved.

Do not present unverified UI claims as completed. If only source changes were made, say so plainly.

---

# Audit mode output

When asked to audit without editing, produce:

## Current state

What exists now.

## Design bible alignment

Where it matches doctrine.

## Gaps

Where it violates or weakly satisfies doctrine.

## Priority fixes

Rank fixes as:

1. Release blocker
2. High-value hardening
3. UX polish
4. Future enhancement

## Recommended implementation pass

Provide a Codex-ready brief for the next pass.

---

# Implementation pass brief format

When asked to create a Codex-ready brief, use this structure:

```text
You are updating the PungentFunk Utilities Unity package.

Primary objective:
\[...]

Relevant doctrine:
- Workflow-first minimal surfaces must expose constant/frequent controls directly and move conditional/rare/setup/metadata/preset controls into discoverable toolbar, dropdown, overlay, popup, or workflow-tab surfaces when appropriate.
- UI layout decisions must be based on user goal, normal sequence, control frequency, context, risk, preview needs, and validation evidence, not visual tidiness alone.
- Substantial UI work must name the decisive design principles in play: hierarchy/grouping, learnability/disclosure, affordance/feedback/forgiveness, functional polish, accessibility, and robustness as applicable.
- Global utility concerns must use shared contracts: Developer Mode root gates, shared window chrome, Minimize to Tray, package badges/cards, help context, package presence, overlay trays, scan freshness, empty states, and common toolbar/filter rows.
- Developer-only controls must not appear or execute unless the relevant Developer Mode root gate is enabled, including menus, shortcuts, trays, overlays, browser cards, stored callbacks, and reflection routes.
- Core owns registry/discovery contracts and fallback states; concrete extension registrations should live in per-package registrars.
- Declare package ownership, tier, required dependencies, optional dependencies, capabilities, extension points, and missing-package behaviour for new utilities/providers/editors/bridges/data types.
- Core owns stable contracts and shared infrastructure; concrete workflows belong in extensions, labs, bridges, or project adapters.
- Authoring, notes, documents, boards, sheets, help, tokens, docs links, audit issues, utilities, and external targets should use shared Authoring Data Foundation IDs, link targets, metadata, preview/action/validation contracts, and safe missing-provider behaviour.
- Preserve Core/lab/project-adapter boundaries.
- Runtime assemblies must not reference UnityEditor.
- Editor rendering must not own scans, asset mutation, or scene mutation.
- Scan/apply flows must be explicit, cached, previewable, and safe.
- Project-wide searches/scans must use or extend the shared scan pipeline unless a documented exception is required.
- Scan-like work must be classified as local validation, local scan session, project audit provider, background-safe provider, immediate-only provider, generated index, or apply operation.
- Scan UI must show cached results, scope, last scan age, stale/fresh/running/failure state, and explicit refresh/cancel/pause/resume controls where supported.
- UI implementation claims must identify source owner, active access path, active draw/create path, and validation status.
- UI cleanup must preserve or account for every existing control through a before/after inventory.
- Split layouts must identify handle ownership, orientation, drag direction, current-size clamping, sibling minimums, flexible region, and persistence behaviour.
- Toolbar/filter/action rows must use semantic grouping and adaptive wrapping before horizontal scrolling.
- Help/documentation affordances must preserve utility/section/topic context and avoid crowding polished headers or control rows.
- Transient contextual popup-style workflows should prefer shared overlay trays over detached popup windows when the task is lightweight, dismissible, and related to the current window, panel, tray, browser row, or selection.
- UI must use a cohesive PungentFunk shell while letting each tool's panel layout fit its specific workflow.
- Visual polish must follow function: improve perceived quality, hierarchy, and confidence without adding decorative noise that competes with content or weakens affordance.
- Panels should fill allocated space and use resizable, persistent internal borders where panels compete for space.
- Fields should reflow, wrap, stack, or use responsive grids before horizontal scrolling is introduced.
- Vertical scrolling is acceptable for content overflow; horizontal scrolling should be deliberate and justified by the panel design or data shape.
- Use graphs, diagrams, previews, custom controls, and richer visualizations where they improve comprehension or workflow power.
- Use UtilityWindowTheme / UtilityWindowPrefs or equivalent UI Toolkit design tokens.
- Optional integrations must degrade gracefully when missing.
- Registry metadata, menu paths, implementation roles, and package status must remain consistent.

Files likely involved:
- \[...]

Required changes:
1. \[...]
2. \[...]
3. \[...]

UI evidence requirements:
- Before control inventory: \[...]
- After control inventory: \[...]
- Workflow goal and normal sequence: \[...]
- Control frequency/context classification: constant / frequent / conditional / rare / advanced / destructive / developer-only.
- Principle application for substantial UI work: name 3-6 decisive principles and how the patch applies them.
- Hierarchy/access path summary: primary focal point, secondary regions, tertiary chrome/metadata, and where moved controls are now reached.
- Surface selection rationale: workbench / toolbar / dropdown / overlay tray / popup / workflow tab / sidebar / modal / full window.
- Moved/hidden/removed controls and rationale: \[...]
- Active access path and draw/create path to verify: \[...]
- Feedback/forgiveness validation, if applicable: idle/pending/success/failure states, cancel/retry/undo path, destructive action separation, and preview/apply behaviour.
- Accessibility/content-extreme validation: keyboard/focus support where available, non-colour status signal, long labels, dense data, empty data, narrow dock, short window, domain reload, and missing-provider states.
- Splitter validation, if applicable: owner, orientation, drag direction, min/max clamp, sibling minimums, flexible region, persistence.
- Adaptive row validation, if applicable: semantic groups, min/preferred/max widths, wrapping order, clipped-label check, horizontal-scroll exception.
- Help/context validation, if applicable: utility ID, section/topic ID, selected context, documentation source precedence.
- Overlay tray validation, if applicable: owner surface, active UI path, placement/clamping, dismissal, focus, pinned/unpinned behaviour, full-tool handoff.

Package and authoring requirements:
- Package owner/tier: \[...]
- Required dependencies and optional dependencies: \[...]
- Provided/consumed capabilities and extension points: \[...]
- Missing package / bridge availability behaviour: \[...]
- Authoring foundation usage, if applicable: IDs, link targets, metadata, preview/action/validation provider, migration/version behaviour.
- Package split/release-plan note, if applicable: metadata/registrar/asmdef/provider/bridge stage and validation configuration.

Shared consistency requirements:
- Developer Mode gates touched: visibility / metadata mutation / source authoring / none.
- Shared chrome/tray/help/package badge usage or justified exception: \[...]
- Descriptor metadata saturation and per-package registrar ownership: \[...]
- Package presence resolver/simulation/fallback behaviour: \[...]

Scan integration requirements:
- Scan classification: local validation / local scan session / project audit provider / background-safe provider / immediate-only provider / generated index / apply operation.
- Shared owner/provider/cache/session/result model used or exception rationale: \[...]
- Scope, stale conditions, background capability, cancellation/pause, and UI summary behaviour: \[...]

Safety requirements:
- \[...]

Performance requirements:
- \[...]

Validation:
- \[...]

Output:
- Provide only changed/new scripts.
- Do not include unchanged files.
- Summarize files changed, architecture notes, UI/UX notes, safety/performance notes, evidence/verification, validation steps, and known limitations.
```

---

# Definition of Done

A PungentFunk utility update is not complete unless it satisfies the relevant items below without regressing existing approved behaviour. Apply the checklist according to the scope and maturity of the tool; do not force unrelated checklist items into a narrow finishing pass.

* Registered in the utility registry or per-lab registrar.
* Uses clean menu paths and optional context menus where appropriate.
* Uses shared utility consistency contracts for Developer Mode, window chrome, Minimize to Tray, package badges/cards, help context, package presence, overlay trays, scan freshness, empty states, and common toolbar/filter rows unless a justified exception is documented.
* Developer-only controls are gated by the global Developer Mode root gate and relevant mutation/source-authoring gates across windows, menus, shortcuts, trays, overlays, browser cards, callbacks, and reflection-accessible actions.
* Utility descriptors satisfy metadata requirements before appearing in user-facing browsers: package ID/tier, implementation role, access path, capabilities, visibility, dependencies, documentation topic, and missing-dependency behaviour.
* Concrete extension utilities, providers, menus, overlays, trays, shortcuts, and bridge hooks are registered by their owning package registrar rather than Core default registration where package split work is in scope.
* Package presence and simulation are separated: simulated states require Developer Mode, are visibly labelled, do not alter manifests/asmdefs, and do not bypass compile-time dependency constraints.
* Declares package ownership, package tier, required dependencies, optional dependencies, capabilities, extension points, fallback behaviour, missing dependency message, documentation topic, and related utilities where relevant.
* Keeps Core as stable contracts/shared infrastructure and avoids moving concrete lab workflows, advanced editors, domain scanners, generators, or package-specific behaviour into Core.
* Optional package gates are visible, calm, explanatory, non-breaking, and do not hide data.
* Has a deliberate implementation role.
* Uses UtilityWindowTheme / UtilityWindowPrefs or equivalent UI Toolkit design tokens.
* Follows Unity-standard component semantics.
* Uses a cohesive PungentFunk visual shell while giving each utility a purpose-built layout.
* Uses universal design principles as a decision aid for UI work: hierarchy and grouping first, learnability and wayfinding second, interaction safety and feedback third, functional polish fourth, accessibility and robustness throughout.
* Identifies one primary focal point per major region, with secondary support and tertiary chrome/metadata visually subordinate unless neutral comparison is required.
* Groups controls by workflow meaning and proximity/alignment, not by accidental code order or forced symmetry.
* Maps the user workflow before layout changes and chooses surfaces by frequency, sequence, risk, context, preview needs, and discoverability.
* Constant and frequent controls remain visible or directly reachable from the active workflow.
* Conditional, rare/setup, metadata, preset, and advanced controls use labelled, tooltip-backed, contextually anchored compact surfaces when they do not need persistent workspace.
* Sidebars are justified by persistent navigation, selection, reference, detail, or comparison needs; otherwise consider toolbar, dropdown, overlay, popup, or workflow tab surfaces.
* Workflow tabs represent coherent phases or modes, not arbitrary overflow.
* Works at small, medium, wide, and short window sizes.
* Panels stretch to fill their allocated space.
* Uses draggable/persistent splitters where panels compete for space, with verified handle ownership, orientation, drag direction, clamp bounds, sibling minimums, and flexible remaining region.
* Reflows, wraps, stacks, collapses, or grids fields before clipping them.
* Toolbar/filter/action rows wrap by semantic group and preserve labels, tooltips, tab/focus order, and primary actions.
* Transient contextual interactions use shared overlay trays where appropriate and reserve detached popup windows or modals for justified persistent, dense, multi-stage, or blocking workflows.
* Overlay trays have clear owners, clamp within usable bounds, preserve focus predictably, dismiss safely, persist/clamp movable state where relevant, and provide Open Full Tool/Browser or Dock as Window handoffs when content outgrows the tray.
* Uses vertical scrolling appropriately for content that extends below the visible panel or window.
* Avoids horizontal scrolling unless the panel's data shape or design explicitly justifies it.
* Has meaningful tooltips/help for important fields without crowding headers or compact control rows.
* Help and documentation affordances preserve utility, section, topic, selected context, and source precedence.
* Has useful empty states.
* Has precise warning/error/info messages.
* Every important action has appropriate affordance and feedback: resting state, hover/focus where supported, pending/running state for delayed work, success/failure state, and retry/cancel/undo where the action warrants recovery.
* Destructive or broad actions are visually separated from routine actions and use the lightest effective forgiveness model: constraint, preview, Undo, safety net, confirmation, or recovery help based on consequence.
* Visual polish increases confidence, scanability, and perceived quality without adding non-load-bearing borders, icons, gradients, shadows, or motion.
* Layouts tolerate credible content extremes such as long names, dense result sets, zero results, missing optional packages, stale scans, and unusually small docked windows.
* Does not rely on colour alone.
* Reuses shared widgets/helpers where patterns repeat.
* Uses dense list/tree/table patterns for dense data.
* Uses graphs, diagrams, previews, custom controls, or other visuals where they materially improve understanding or control.
* For UI work, identifies source owner, active access path, active draw/create path, and visibility conditions for major changed sections.
* For UI refactors, includes before/after control inventory and accounts for moved, hidden, replaced, preserved, and intentionally removed controls.
* Does not hide required controls behind obscure toggles, accidental layout expansion, excessive scroll depth, or developer-only gates unless that is the intended audience.
* Does not claim UI work is restored, moved, fixed, or complete unless active UI reachability and representative layout sizes were verified.
* Package-split work is staged and validated across Core-only, Core-plus-one-extension, bridge-disabled, and full-bundle configurations where relevant; do not rely only on the full bundle.
* Keeps Scene GUI lightweight.
* Uses graph/canvas only when relationships justify it.
* Uses SerializedObject/SerializedProperty where needed.
* Runtime code compiles without UnityEditor.
* Editor code lives in Editor folders or editor asmdefs.
* Avoids compile-time dependencies on game-specific classes.
* Optional integrations degrade gracefully.
* Authoring, notes, roadmap, documents, boards, sheets, help, tokens, docs links, audit issues, utilities, and external targets use shared Authoring Data Foundation IDs/link targets/metadata/provider contracts where applicable.
* Missing authoring providers preserve records, metadata, stable IDs, tags, backlinks, summaries, fallback previews, and safe copy/ping/open actions.
* Legacy Notes and Roadmap migrations preserve browse/manage/integration behaviours and do not destructively convert or delete legacy note bodies without explicit approval.
* AssetDatabase operations are explicit and safe.
* Tolerates domain reload, code reload, window close/reopen, and saved layouts; selected tabs, search text, filters, help topic, developer filters, foldouts, and splitter values restore and clamp safely where relevant.
* Scan-heavy tools use or extend shared scan/session/result/cache/finding models where the result is broad or reusable.
* Project-wide scan providers declare scope, capabilities, stale conditions, background safety, cancellation/pause support where practical, and limitations.
* Scan-consuming UI shows cached results first, last scan age, stale/fresh/running/failed/blocked/not-configured state, scope, summary counts, severity/actionability filters, and an explicit refresh action.
* Rendering paths never perform project-wide scans, broad reflection, documentation generation, expensive indexing, scene opening, asset mutation, or graph mutation.
* Batch/destructive operations provide preview/dry-run and Undo where possible.
* Has been validated in a clean project, with optional labs absent, and with the full bundle installed where relevant.
