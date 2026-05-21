# Resizable Panel Doctrine

Use this reference when changing split panels, draggable borders, panel stretching, adaptive rows, help affordances, documentation links, generated documentation UI, or shared layout helpers.

## Layout Templates

Choose the layout by workflow, not visual sameness:

| Template | Best for | Doctrine |
|---|---|---|
| Simple vertical stack | Small settings, single-purpose helpers | Header, configuration, action, status/help. Avoid for dense or comparison-heavy workflows. |
| Two-column layout | Source/result, configuration/preview, selection/detail | Use persistent draggable splitters when both sides compete for space. |
| Three-panel layout | Advanced authoring, asset browsers, debug tools, lab hubs | Left navigation/source, centre workspace/results, right details/actions. Collapse or stack at narrow sizes. |
| Grid/card layout | Asset sets, palettes, icons, prefab previews, generated names, tool launchers | Cards wrap and use compact metadata. |
| Table/matrix layout | Coverage tools, validation, dependency maps, compatibility checks | Sorting/filtering/grouping/detail panes. Horizontal scroll is acceptable when column relationships matter. |
| Canvas/workspace | Generation, placement, spatial workflows, graph-like relationships | Central workspace, side configuration, contextual toolbar, mode-specific controls. |
| Wizard/step flow | Setup, migration, export, risky multi-stage operations | Step, inputs, validation, dry-run results, final apply/export. |
| Dashboard/overview | Control panel, lab home, health summaries | Summaries, warnings, search, recent tools, quick actions. Do not turn dashboards into dense control panels. |
| Contextual mini panel | Component, scene selection, terrain, or asset quick actions | Keep compact and include an Open Full Tool handoff for complex work. |

## Shared Resizable Panel Contract

Use a shared contract for split panels and draggable borders. A custom local splitter is acceptable only when a tool has a genuinely special canvas, graph, matrix, timeline, or preview requirement and documents why the shared contract does not apply.

A split panel is valid only when:

* Handle orientation matches motion.
* Drag direction matches the visually owned panel.
* Right-anchored or bottom-anchored inverted handles are explicitly named and documented.
* The handle sits exactly between the regions it resizes.
* Panel edges visually stick to the handle and window edge.
* At least one region fills leftover width or height.
* Bounds are derived from current available size and sibling minimums.
* Saved sizes are clamped before drawing on every layout pass.
* Handle thickness and spacing are included in layout calculations.
* Handles remain discoverable through hit area, tint/cursor affordance, and tooltip where appropriate.
* Dragging both directions has been validated so there is no inverted border regression.

## Canonical Split Calculation

For left/right panels:

1. Reserve outer padding, inner spacing, fixed header/footer regions, and handle width.
2. Compute `minLeft`, `minRight`, and `maxLeft = availableWidth - minRight - handleWidth - spacing`.
3. Clamp saved left width between `minLeft` and `maxLeft` before drawing.
4. Draw the left panel at the clamped width.
5. Draw the handle immediately after the left panel.
6. Draw the right panel with expand-width behaviour so it consumes remaining space.

For right-anchored rails:

* Store `rightWidth` and draw flexible content first, or use an explicitly named inverted handle.
* Do not mix left-owned state with right-owned visual behaviour.

For top/bottom sections:

1. Reserve fixed header/footer/toolbar regions and handle height.
2. Clamp top height against current available height and bottom minimum height.
3. Draw top section, then handle, then bottom section with expand-height behaviour.

## Panel Stretching

Panels should stretch according to their job:

* Navigation/source rails usually have clamped width and full available height.
* Main workspace/results panels expand in both directions and absorb most remaining space.
* Inspector/detail rails may have clamped width but should stretch vertically.
* Configuration sections may have preferred heights.
* Results, previews, logs, diagnostics, documentation, and dense lists should receive leftover height.
* Footer/action bars should remain visible and should not be pushed below an unrelated scroll region.
* Scroll regions should have intentional ownership. Avoid nested scroll views unless the inner scroll is a matrix, list, table, preview strip, or log with independent navigation.

A panel is incomplete if it draws only at preferred size while leaving unused space that should belong to results, previews, diagnostics, documentation, or dense content.

## Overlay Tray And Popup Surface Doctrine

Prefer shared overlay trays for transient popup-style interactions when the task is contextual, lightweight, dismissible, and related to a currently visible utility, browser, panel, tray, toolbar control, scene selection, or row.

Use overlay trays for:

* contextual help, glossary, and documentation previews with an Open Full Help Browser handoff,
* minimized utility controls, recent tools, quick actions, and utility switchers,
* lightweight settings, filters, sort controls, package-state explanations, and missing-dependency notices,
* selected-item summaries, backlinks, related utility lists, authoring previews, and bridge/action menus,
* small pickers, inspectors, confirmation previews, and temporary palettes that support an active panel.

Use a full `EditorWindow` when the workflow needs:

* persistent dockable workspace,
* large result review,
* dense data tables,
* graph/canvas editing,
* rich document editing,
* sheet editing,
* scene authoring,
* multi-stage scan/apply flow,
* long-lived authoring state.

Use a modal dialog only for destructive confirmation, blocking error recovery, required save/discard decisions, or security/safety choices where the user must answer before continuing.

Overlay tray behaviour rules:

* A tray must have a clear owner: utility window, panel, toolbar button, selection, scene overlay, minimizer strip, or browser row.
* A tray should be non-modal by default, easy to dismiss, and should not trap the user unless intentionally blocking.
* A tray must clamp within usable editor/window bounds, handle narrow and short sizes, and avoid covering the primary action that opened it when a better placement exists.
* Movable or resizable trays must persist and safely clamp position, size, collapsed state, and selected topic after domain reload, docking changes, and monitor changes.
* Trays should use shared theme tokens, calm borders, readable elevation/shadow, clear title/action rows, and visible focus states.
* Escape should close dismissible trays. Clicking outside may close lightweight trays unless pinned.
* Trays with more than quick content should include Open Full Tool, Open Full Browser, or Dock as Window handoffs.
* Trays render cached/current state and invoke explicit actions; they must not own project scans, heavy reflection, asset mutation, scene mutation, or broad document generation from repaint.
* Multiple trays should be coordinated by a shared tray manager or rail. Avoid overlapping popup clutter, duplicate tray stacks, and hidden tray state with no visible affordance.

Overlay tray implementation review gate:

* State why the workflow is tray-appropriate rather than a full window, inspector, scene overlay, context menu, or modal dialog.
* Identify the owner surface and active UI path.
* Verify narrow/wide, short/tall, docked/floating, pinned/unpinned, outside-click, Escape, domain reload, and missing-provider states.
* Confirm the tray does not hide essential controls, does not break keyboard focus, and provides a full-tool handoff when content outgrows the tray.

## Adaptive Row Wrapping

Toolbars, filters, chip rows, and compact control rows should adapt before horizontal scrolling.

Rules:

* Keep controls that form one conceptual row together while there is room.
* Keep separate semantic rows separate.
* Wrap whole control groups; do not split labels from fields or icons from labels.
* Give each group minimum, preferred, and maximum widths.
* Let search/text fields expand; do not let fixed action buttons grow excessively.
* Preserve left-to-right reading order and primary action discoverability when wrapping.
* Preserve grouping, labels, tooltips, and tab/focus order after wrapping.
* Use horizontal scroll only for inherently horizontal artifacts such as palette strips, timelines, matrices, comparison tables, node canvases, graph views, and texture/preview filmstrips.

## Validation Grid

Check new or refactored utility windows in these states:

| State | Required result |
|---|---|
| Narrow docked window | Core workflow discoverable, rows wrap before clipping, no essential action disappears. |
| Comfortable docked window | Panels use available space, no large dead zones, splitters feel natural. |
| Wide/floating window | Main workspace expands; side rails do not balloon beyond useful maximums unless designed to. |
| Short height | Primary actions and current state remain reachable; results/previews scroll intentionally. |
| Tall height | Results, docs, previews, or workspaces stretch vertically instead of leaving blank space. |
| Domain reload | Splitter values, tab/module, search/filter text, developer filters, help topic, and foldout state restore and clamp safely. |
| Empty state | Empty panels show action-oriented messages rather than collapsing to unusable height. |
| Populated state | Dense content remains scrollable, grouped, and readable. |

## Common Failures To Prevent

* Inverted resize handle: drag direction changes the opposite panel from the one visually owned.
* Missing resize handle: competing panels cannot be adjusted.
* Unconstrained panel: saved size crushes sibling panels after resize/reload.
* Detached panel edge: unowned gap appears between panel, handle, or window edge.
* Non-stretching results/details: results, docs, or previews stay at preferred height while blank space remains.
* Over-crowded toolbar: buttons, chips, search fields, and help icons clip labels in one line.
* Horizontal scrollbar abuse: ordinary controls require horizontal scrolling.
* Nested scroll trap: user scrolls the wrong region or cannot reach actions reliably.
* Help affordance crowding: repeated help buttons disrupt headers or rows.
* Context-losing help: help opens without utility, section, topic, or selected context.
* Overlay tray misuse: lightweight contextual popup content opens as a detached window/modal and interrupts the current workflow.
* Popup clutter: multiple trays overlap without a visible owner, tray rail, dismissal model, or restore affordance.
* Broken persistence: selected tab, filters, foldouts, help topic, and splitter widths reset or restore impossible values.
* Hidden developer tools: internal controls lack explicit Developer Mode visibility model.

## Help And Documentation UI

Documentation and help tools follow the same panel rules, with extra context-preservation requirements:

* Prefer one help affordance per section/header cluster.
* Do not place noisy help icons beside every label.
* Help affordances must not force headers, filters, or action rows to wrap prematurely.
* Use a compact right-aligned icon slot, contextual side rail, or overflow menu when headers are crowded.
* Preserve utility ID, tab/module, section, selected item, and topic anchor when opening help.
* Generated scripting references should list useful package-authored APIs first.
* Inherited Unity/Object methods and lifecycle noise should be collapsed, filtered, or grouped separately.
* Generated documentation must not overwrite curated documentation by default.
* Documentation surfaces should label source type: curated, generated, imported, stale, missing, or draft.
* If documentation sources disagree, surface precedence rather than silently merging contradictions.

## Recommended Shared Helpers

Build or reuse shared helpers once APIs stabilize:

* `ResizableSplitLayout`: clamps widths/heights using available size, sibling minimums, handle thickness, and anchor ownership.
* `AdaptiveToolbarRow`: groups controls semantically, wraps whole groups, avoids horizontal scrolling for ordinary controls.
* `StretchPanelScope`: draws a themed panel that can intentionally claim leftover space.
* `ContextualHelpButton`: carries utility ID, section ID, topic ID, and selected context without crowding rows.
* `DocumentationSourceBadge`: labels curated/generated/stale/draft sources.
* `OverlayTrayManager` or equivalent: coordinates tray ownership, placement, focus, dismissal, pinning, persistence, and full-tool handoffs.
* `UiStatePersistenceKeys`: centralizes stable keys for splitters, searches, filters, tabs, help topics, developer filters, and foldouts.

These helpers should guarantee correct sizing, persistence, and interaction behaviour without forcing every lab into the same visual composition.
