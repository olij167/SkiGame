# Workflow-First Minimal Surface Doctrine

Use this reference when changing sidebars, toolbars, dropdowns, overlays, popups, tabs, metadata/preset controls, Palette Designer-style workflows, or any UI layout where control frequency and user sequence determine surface choice.

## Principle

Minimal UI is a surface-priority system, not a license to hide functionality.

Ask:

* What is the user trying to accomplish?
* In what order?
* How often does each control matter?
* Which controls must remain stable and visible?
* Which controls can be close, discoverable, and compact?
* Which controls are rare, advanced, destructive, setup-oriented, metadata-oriented, preset-oriented, or developer-only?

Frequently used controls should be visible and stable. Conditional controls should be close, labelled, tooltip-backed, and contextually located. Rare or advanced controls should not permanently consume workspace unless the workflow requires continuous reference.

Essential actions must remain visible or one deliberate click away from the context where they are needed.

## Workflow-First Design Pass

Before changing a major UI layout:

1. State the user goal in plain language.
2. Map the normal sequence: first action, iteration loop, validation/refinement phase, apply/export/save phase.
3. Classify each control by frequency: constant, frequent, conditional, rare/setup, advanced, destructive, developer-only.
4. Classify each control by context: global window, selected item, generated result, preset, metadata, package state, help, developer/debug.
5. Choose the smallest surface that preserves discoverability and usability.
6. Validate at narrow, comfortable, and wide sizes.
7. Record before/after access paths for moved controls.

## Surface Priority Model

| Surface | Should host | Should avoid |
|---|---|---|
| Always-visible workbench | Content, preview, list, canvas, palette, results, graph, or table the user constantly manipulates | Rare metadata, preset setup, developer options, long help, low-frequency utility controls |
| Primary toolbar | Frequent actions, mode switches, generate/refresh/apply/save, compact filters, lock/pin, view controls, clear overflow entry points | Large forms, long explanations, destructive actions without preview, unrelated module navigation |
| Toolbar dropdown/action menu | Less frequent grouped commands, preset selection, import/export variants, view modes, sort/group choices, secondary create actions | Critical actions used repeatedly during the core loop |
| Overlay tray | Contextual settings, metadata editing, preset editing, help preview, selected-item detail, quick filters, recent items, compact diagnostics | Persistent multi-hour editing, complex tables, long authoring sessions, full lab workflows |
| Popup/popover | Small temporary decisions: rename, choose, confirm, tweak, pick, duplicate, tag, quick preset | Complex workflows, content needing persistence, anything painful if dismissed |
| Workflow tabs | Sequential or phase-based flows such as Generate -> Refine -> Export, Scan -> Review -> Apply, Browse -> Edit -> Validate | Unrelated dumping grounds for controls that did not fit elsewhere |
| Sidebar/persistent rail | Persistent navigation, source lists, item selection, long-lived detail/reference context, comparative inspection | Rare metadata editing, preset setup, developer tools, controls only needed before/after the main loop |
| Modal dialog | Destructive, irreversible, blocking, permission, save/discard, or security decisions | Recoverable information, routine settings, filter/edit controls |

## Control Frequency Treatment

| Frequency | Treatment |
|---|---|
| Constant | Visible in main workbench or persistent toolbar. User should not hunt. |
| Frequent | Visible in toolbar, row action, compact header, or direct contextual affordance. |
| Conditional | Toolbar dropdown, contextual overlay tray, selected-item popover, or task-specific tab. |
| Rare/setup | Settings, preset editor, overflow menu, or overlay. Do not let it dominate the workbench. |
| Advanced | Collapsed, overlay-hosted, or clearly labelled advanced section. Still discoverable. |
| Developer-only | Hidden and non-functional unless the Developer Mode root gate is enabled. |
| Destructive/batch | Visually separated, preview/dry-run first, never buried where risk is unclear. |

## Sidebar Doctrine

Sidebars must earn their space.

Use a sidebar when the user needs persistent navigation, persistent selection, always-visible reference details, long-running contextual comparison, or a continuous inspector-like relationship with the main workspace.

Do not use a sidebar merely because a utility has many controls. If controls are mostly metadata editing, preset editing, optional filters, rare setup, or developer tools, prefer a toolbar entry, dropdown, overlay tray, popup, or workflow tab.

## Toolbar Doctrine

Toolbars are preferred compact access for workbench actions that are important but not all simultaneously needed.

Good toolbars:

* expose active-loop actions directly,
* group conditional actions in dropdowns,
* keep labels or tooltips clear,
* preserve primary action visibility,
* reduce permanent panel weight.

Bad toolbars:

* become unlabeled dumping grounds,
* hide critical repeated actions,
* merge unrelated semantic groups,
* rely on icons without tooltips or adjacent context.

Toolbar, overlay, and row-action labels must name the result of the action, not just the interaction phase. Use mode-specific labels such as "Save and Close" for dirty editable trays and "Close" for read-only previews. Avoid ambiguous labels like "Done" where users could reasonably read the action as returning to another mode instead of closing, saving, or committing.

## Overlays, Dropdowns, And Popups

Use overlays, dropdowns, and popups for small contextual controls, quick preset changes, metadata editing, filters, optional settings, or short decisions.

These surfaces should:

* anchor near the initiating control,
* close on Escape where appropriate,
* preserve state where useful,
* provide full-tool handoff when content outgrows compact use,
* remain labelled and tooltip-backed.

When content becomes complex, route to an overlay tray, workflow tab, or full tool.

## Workflow Tabs

Use tabs when the workflow has coherent phases or modes: Generate/Refine/Export, Browse/Edit/Validate, Scan/Findings/Apply, Layout/Preview/Publish.

Avoid tabs when the only reason is that a panel became crowded. A tab should clarify sequence, not hide unrelated controls.

## Metadata And Presets

Metadata editing and preset setup are usually conditional. They should not permanently consume primary workspace unless the utility is specifically a metadata or preset editor.

In most workbench tools, metadata and presets belong in toolbar dropdowns, overlay trays, popovers, or settings tabs. Developer-only metadata remains behind the Developer Mode root gate.

## Palette Designer Reference Pattern

Palette Designer is a reference workflow:

1. Generate a palette the user is happy with.
2. Inspect and lock useful colours.
3. Refine the palette for accessibility.

Implications:

* Keep palette preview/workbench central.
* Convert generation, preset, metadata, and action sidebars into compact toolbar/dropdown/overlay surfaces when they are not continuously needed.
* Move accessibility refinement into a workflow tab when it follows generation as the next phase.
* Preserve every moved control through visible toolbar/dropdown/tab/overlay access paths.

## Required Brief Answers

For UI layout changes, answer:

* What is the user trying to accomplish?
* What is the normal sequence?
* Which controls are constant, frequent, conditional, rare, advanced, destructive, or developer-only?
* What remains visible?
* What moves behind compact surfaces?
* Why this layout, in workflow terms?
* What validation proves access, persistence, missing-package behaviour, Developer Mode behaviour, and active interaction?

## Anti-Patterns

Avoid:

* moving controls out of sight and calling the UI minimal,
* using persistent sidebars for rare metadata/preset controls,
* hiding essential actions in obscure dropdowns, unlabeled icons, clipped sections, or deep scroll regions,
* tabs that are arbitrary overflow buckets,
* toolbar menus that hide critical repeated actions,
* Developer Mode used to hide normal complexity,
* compact surfaces without labels, tooltips, or contextual anchors.
