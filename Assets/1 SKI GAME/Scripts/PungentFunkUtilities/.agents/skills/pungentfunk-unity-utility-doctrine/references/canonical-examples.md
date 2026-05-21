# PungentFunk Canonical Examples

Use these examples to ground audits, implementation briefs, patch decisions, and final reviews. Prefer the principle behind each example over copying the wording.

## Good Patterns

### Safe Scanner

A scanner caches results, refreshes only when requested or when relevant settings change, previews changes before mutation, applies selected items with Undo where possible, and summarizes changed, skipped, and failed items.

### Shared Project Scan Provider

A project-wide validation registers a provider with stable ID, scope, severity counts, stale-state reporting, cached results, background capability metadata, and action-oriented findings that other windows can consume without reopening the originating tool.

### Hierarchical Scan Results Utility

A scan/results utility makes the scan summary and actionable findings the primary focal point, keeps scope/filter/status controls grouped in a compact secondary toolbar, shows cached freshness and failure state clearly, separates Apply Selected from destructive/batch actions, and provides retry/cancel/Undo or recovery guidance where supported.

### Local Scan Session

A one-object or active-selection validation uses shared scan/session/result models locally without registering a global provider because the results are not reusable outside the current workflow.

### Optional Bridge

A lab-to-lab feature is implemented through registry lookup, descriptor metadata, version defines, reflection-safe discovery, or an optional bridge assembly. The generic lab still compiles and degrades gracefully when the other lab is absent.

### Package Capability Declaration

A new provider declares package ID, tier, owner, required and optional packages, provided and consumed capabilities, extension points, bridge availability, fallback behaviour, missing dependency message, documentation topic, and related utilities.

### Authoring Browser Fallback

A missing Rich Document Editor package leaves legacy notes visible in the Authoring Browser with stable IDs, tags, backlinks, read-only preview, Copy ID/path actions, missing-package explanation, and safe launch/install hints.

### Mature Tool Finishing Pass

A near-complete editor window keeps its current workflows, shortcuts, menu aliases, result views, and advanced controls. The patch fixes concrete bugs, improves layout resilience, adds caching or validation where needed, and polishes messages without replacing known-good behaviour.

### Responsive Panel Layout

Competing panels use draggable persistent splitters. Panels fill their allocated space. Rows wrap or stack as width shrinks. Wide panels use additional columns only when readability improves. Vertical scrolling handles tall overflow; horizontal scrolling appears only for inherently horizontal data or as a deliberate late fallback.

### Correct Splitter Implementation

A split-panel update identifies the handle owner, clamps saved sizes against current available space and sibling minimums before drawing, includes handle thickness in the layout calculation, verifies drag direction in both directions, and persists splitter state with safe reload clamping.

### Adaptive Toolbar Row

A toolbar/filter row keeps semantic groups together, assigns min/preferred/max widths, wraps whole groups at narrow widths, lets search fields expand at wide widths, and avoids horizontal scrolling for ordinary controls.

### Overlay Tray

A help preview, package-missing explanation, selected-item summary, or quick filter panel opens in a shared overlay tray owned by the current window/panel/button. It clamps inside the window, dismisses with Escape, preserves focus predictably, and includes an Open Full Tool/Browser handoff when content grows.

### Workflow-First Minimal Surface

A generation tool keeps the preview/workbench central, exposes regenerate/lock/save/apply as visible toolbar actions, moves rare preset and metadata controls into labelled dropdowns or overlay trays, and places refinement in a workflow tab because it is a later phase.

### Polished Palette Designer Workflow

A Palette Designer-style workflow keeps the palette preview and lock/refine loop central, uses compact toolbar/dropdown/overlay surfaces for presets and metadata, makes accessibility refinement a visible workflow phase when it is part of the next decision, and preserves every moved control through a clear labelled access path.

### Staged Package Split

A package hardening pass adds ownership/capability/fallback metadata and per-package registrar structure first, validates Core-only and Core-plus-one-extension states, and delays bridge packages until standalone extensions compile without each other.

### Shared Utility Chrome

A utility window uses shared chrome for title/status, package badge, contextual help, Minimize to Tray, Developer Mode badge, primary action, and scan freshness rather than drawing local variants of each affordance.

### Developer Mode Root Gate

A metadata editor action is hidden and non-callable from menus, shortcuts, trays, stale callbacks, and browser cards until the global Developer Mode root gate and metadata mutation gate are enabled.

### Package Presence Fallback

A missing extension leaves utility records visible with package badge, missing dependency message, read-only preview, safe copy/open actions, and install/enable hint instead of throwing or silently hiding the data.
### Purpose-Built Visualization

A dependency or asset-flow tool uses a graph or diagram because relationships are the main authoring problem. Nodes are selectable, zoom/pan is clear, legends and tooltips explain state, and the visualization drives useful actions instead of acting as decoration.

### Verifiable UI Relocation

A UI relocation includes before/after control inventory, source owner, active access path, active draw/create path, visibility conditions, and narrow/wide validation notes. The summary says exactly what was verified and what still needs Unity visual QA.

### Unity-Native Safety

Scene or prefab edits use `Undo` and distinguish prefab assets, instances, variants, nested prefabs, and Prefab Mode. Shared assets warn before mutation. Batch asset operations avoid unnecessary import/refresh churn.

## Bad Patterns

### Runtime Editor Leak

A runtime assembly references `UnityEditor`, stores editor-window state as runtime data, or uses editor-only APIs in code that must compile in player builds.

### Repaint Work

`OnGUI`, `CreateGUI`, `Bind`, or routine draw methods perform broad project scans, heavy reflection, asset writes, scene mutation, or repeated `SceneView.RepaintAll()` calls.

### Parallel Project Scanner

A utility creates its own project-wide asset/reference/documentation scanner even though the shared scan pipeline can provide the same facts through a provider, cached result, or shared finding model.

### Window-Open Rescan

Opening a window immediately forces a full rescan instead of displaying cached results, stale state, last scan age, and an explicit refresh action.

### False Simplification

A final optimisation pass removes advanced controls, collapses a rich workflow into a generic inspector, changes menu paths, drops compatibility aliases, or replaces a refined window with a smaller but less useful implementation.

### Dependency Leak

A generic lab directly references game-specific project classes or a different optional lab, making package reuse or partial installation brittle.

### Core Sink

Core absorbs a concrete lab editor, domain scanner, rich document editor, board/sheet workflow, or package-specific generator merely because two windows need to see related metadata.

### Authoring Model Fork

The document editor, board editor, and sheet editor each invent their own permanent IDs, link targets, metadata, action model, or validation result format instead of using the shared Authoring Data Foundation.

### Unsafe Quick Fix

A button silently edits shared materials, prefab assets, imported assets, project-wide files, scenes, or generated assets that may overwrite user-authored work.

### Premature Horizontal Scroll

A narrow panel introduces horizontal scrolling before trying wrapping, stacking, tabs, compact toolbars, single-column list mode, collapsed advanced sections, or other responsive reflow.

### Inverted Or Detached Splitter

A resize handle appears between two panels but drags the wrong panel, uses an arbitrary hard-coded maximum, ignores sibling minimum sizes, or leaves an ambiguous gap between the panel edge and the handle.

### Crowded Help Affordances

Every label gets its own help button, causing headers and filter rows to wrap prematurely and making the tool feel noisier while still losing the relevant help context.

### Detached Popup Sprawl

Lightweight contextual help, picker, filter, or package-state content opens as detached utility windows or modal dialogs, interrupting the current workflow and leaving overlapping popups with no shared owner or restore affordance.

### Minimal Means Hidden

A cleanup pass removes sidebars by burying frequent actions in unlabeled icons, obscure dropdowns, or deep advanced sections, making the surface smaller while the workflow becomes harder.

### Decorative Polish Weakens The Tool

A polishing pass adds borders, shadows, badges, icons, animations, or colour accents that do not carry state, grouping, affordance, or hierarchy. The screen looks busier, data is harder to scan, and clickable elements are less obvious than before.

### Generic Consistency Overrules Workflow

A utility is forced into the same sidebar/detail/action layout as unrelated tools even though its main job is comparison, graph editing, palette refinement, table review, or preview/apply. The shell is cohesive, but the actual workflow becomes less powerful.

### Arbitrary Tabs

A crowded panel is split into tabs that do not map to workflow phases or coherent modes, forcing users to hunt for controls that used to be part of the same loop.

### Premature Package Split

A pass moves concrete code into guessed package folders before metadata, registrar ownership, asmdef boundaries, missing-package states, and standalone validation are ready.

### Local Developer Toggle

A utility adds its own developer checkbox and allows metadata or source-writing actions while the global Developer Mode root gate is disabled.

### Local Chrome Drift

A window draws its own help button, minimize toggle, package badge, and status row with different semantics, tooltips, or persistence from the rest of the suite.

### Simulated Package Confusion

Developer simulation overrides make a package look genuinely installed, alter manifests/asmdefs, or let simulated availability bypass real compile-time constraints.

### Misleading Debrief

A summary says a panel was restored, moved, or fixed when the control only exists in an unused helper method, stale duplicate panel, hidden foldout, or inactive tab path.

## Good Patch Summary

```text
Summary
Hardened the Material Audit Lab without changing its approved workflow: scan results are cached, selected-row apply now records Undo, and the details panel reflows at narrow widths.

Files changed
- MaterialAuditWindow.cs: Split draw logic from scan/apply actions and added persistent splitter state.
- MaterialAuditResultView.cs: Added responsive single-column and two-column result layouts.

Architecture notes
The change stays inside the Material Lab editor assembly, keeps runtime code untouched, and preserves registry/menu metadata.

Validation steps
Open Tools/PungentFunk Utilities/Material Audit, run a scan, resize narrow/wide, apply one selected fix, undo it, enter/exit Play Mode, and confirm no console errors.
```

## Bad Patch Summary

```text
Updated the window and simplified the UI. Some old controls were removed because they seemed redundant. Also moved shared code into Core.
```

This is weak because it does not identify preserved workflows, does not justify removals, does not explain boundary safety, and hints at Core churn without a clear shared need.
