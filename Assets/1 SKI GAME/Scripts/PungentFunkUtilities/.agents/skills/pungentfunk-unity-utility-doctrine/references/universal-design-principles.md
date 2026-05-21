# PFU Universal Design Principles

Use this reference when changing visible UI, tool layout, control grouping, toolbar/overlay behaviour, graph/canvas/table presentation, accessibility, destructive actions, empty states, scan/apply feedback, or final polish.

This is a PFU-specific synthesis of the Universal Design Principles plugin packs. Do not import generic theory wholesale. Use the principles as decision filters that improve PungentFunk utility quality while preserving architecture, shared systems, and mature workflows.

Existing PFU doctrine remains authoritative for package boundaries, shared scan pipelines, Developer Mode gates, overlay trays, resizable panels, and verifiable UI evidence.

## Principle Stack

Apply these layers in order for substantial UI work:

1. Perception and hierarchy: what the user sees first, what groups together, and what recedes.
2. Cognition and learnability: how the user understands the workflow without memorizing hidden paths.
3. Interaction and control: what looks usable, what responds, what is reversible, and what is dangerous.
4. Aesthetics and emotion: how the tool feels polished, trustworthy, native, and calm.
5. Process and robustness: whether the design survives real data, edge cases, accessibility needs, missing packages, and reloads.

Name only the 3-6 decisive principles for the actual change. Do not force every principle into every patch.

## Perception And Hierarchy

Use this first when a window feels busy, flat, visually confusing, or cramped.

PFU application:

* Define one primary focal point per major region: current result, preview, selected asset, graph, scan summary, editor canvas, or apply decision.
* Treat source lists, filters, metadata, package badges, status rows, and help controls as secondary or tertiary unless they are the task.
* Group by workflow meaning using proximity and alignment: source/filter groups, result/action groups, selected-item details, preview/apply groups.
* Improve signal-to-noise by removing duplicate chrome, decorative borders, non-load-bearing icons, repeated help buttons, and polish that competes with data.
* Preserve legibility in dense tables, trees, grids, palettes, graphs, and inspectors. If hierarchy makes neutral comparison biased, keep rows/items visually uniform and put hierarchy in the surrounding chrome.
* Use colour as one signal among shape, text, icon, position, or tone. Never make colour the only state signal.

Useful question: if the user glances for one second, can they tell where to start, what changed, and which action matters most?

## Cognition And Learnability

Use this when the tool has many options, complex state, unfamiliar workflows, filters, setup, or nested surfaces.

PFU application:

* Use progressive disclosure to reduce visible complexity without hiding essential capability.
* Prefer recognition over recall: visible labels, stable icons with tooltips, recent choices, presets, clear selected state, and contextual action names.
* Preserve wayfinding in utility browsers, lab hubs, workflow tabs, overlay trays, and documentation handoffs: current tool, current section, selected item, active filter, scan age, and package state should be obvious.
* Chunk controls into 3-5 meaningful groups where practical: source, filter, preview, result, apply, output, advanced, diagnostics.
* Match Unity mental models: buttons act, toggles hold state, dropdowns select, sliders adjust, object fields reference, progress bars report work, help boxes explain state.
* Defaults should support the common safe workflow. Advanced options remain discoverable, labelled, and close to the context that needs them.

Useful question: does the user need to remember where a control went, or can they recognize it from the current context?

## Interaction And Control

Use this for buttons, splitters, overlays, drag handles, validation, keyboard/focus behaviour, loading states, destructive actions, and scan/apply flows.

PFU application:

* Every interactive element needs an affordance. Splitters need visible handles and correct drag direction. Icon-only controls need tooltips. Disabled controls need a reason when the missing capability matters.
* Every meaningful action needs feedback. Delayed work should show pending/running state, progress or summary where practical, and success/failure/cancel states.
* Destructive, broad, or expensive actions need forgiveness: constraints, preview/dry-run, Undo, safety nets, confirmation, or recovery help based on consequence.
* Separate dangerous actions from routine controls. Do not bury destructive actions in ambiguous overflow menus where risk is unclear.
* Keep users in control: allow cancel/pause for long scan work where supported, explicit refresh for stale results, and clear retry paths for failed providers.
* Match target size and placement to frequency and risk. Frequent safe actions should be easy to reach; rare destructive actions should be deliberate.

Useful question: after each action, does the user know whether it was received, what is happening, what changed, and how to recover?

## Aesthetics And Emotion

Use this for final polish, perceived quality, brand expression, empty states, onboarding, generated previews, and tool surfaces that should feel confident rather than merely functional.

PFU application:

* Form follows function: visual treatment should clarify structure, affordance, state, and confidence.
* Aesthetic polish is valuable when it improves perceived usability, trust, and focus. It is harmful when it adds noise, weakens affordance, or hides data.
* Keep PFU cohesion at the shell level: theme tokens, panel framing, chrome, status language, badges, help affordances, and empty-state tone.
* Let interiors differ by task. Graph tools, palette tools, scan tools, authoring tools, and asset browsers may need different densities, visualizations, and control rhythms.
* Use restraint in work surfaces. Decoration belongs only when it carries meaning, teaches a concept, supports emotional tone in an empty/onboarding state, or improves confidence without interrupting the task.
* Preserve immersion for creative/editorial workflows by keeping the active canvas, preview, palette, or result central and minimizing unrelated chrome.

Useful question: does the polish make the tool feel clearer and more trustworthy, or just more decorated?

## Process And Robustness

Use this as the readiness gate for UI-facing changes.

PFU application:

* Accessibility is structural: non-colour signals, readable contrast, sensible focus/keyboard behaviour where Unity tech supports it, labels/tooltips for compact controls, and predictable Escape/close behaviour for overlays.
* Design with factor of safety for real content: long names, dense result sets, empty results, missing icons/previews, missing providers, stale scans, tiny docked windows, wide monitors, domain reload, and optional packages absent.
* Check the weakest link in the workflow: the one hidden action, stale result, missing status, clipped field, wrong splitter direction, or unguarded dependency can define the user's experience.
* Prefer iteration-safe changes for mature tools. Improve one concrete weakness without destabilizing approved workflows.
* Validate active UI reality before claiming visible work is complete. Source support is not enough.

Useful question: what credible real-world condition would make this UI fail, and did the patch leave a graceful state?

## Required Principle Brief

For substantial UI work, include:

* Decisive principles: 3-6 principles actually used.
* Hierarchy: primary focal point, secondary regions, tertiary chrome/metadata.
* Learnability: what remains visible, what is disclosed, and how users recognize moved controls.
* Interaction: feedback states, recovery path, and destructive-action separation where relevant.
* Polish: what visual treatment improves clarity or confidence.
* Robustness: long content, dense data, empty state, missing provider/package, narrow/short layout, and reload checks.

## Anti-Patterns

Avoid:

* visual hierarchy that makes the wrong thing loud,
* reducing noise by removing useful information or controls,
* progressive disclosure that becomes hiding,
* icon-only controls without tooltips or recognizable conventions,
* hover-only affordances for important controls,
* scan/apply buttons without pending/success/failure feedback,
* destructive actions without preview, separation, Undo, confirmation, or recovery,
* decorative polish that weakens legibility or affordance,
* identical layouts forced across utilities with different jobs,
* accessibility treated as a final pass rather than a design constraint,
* summaries that claim UI work is complete without active access and representative layout validation.
