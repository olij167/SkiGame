# Shared Utility Consistency Contracts

Use this reference when changing Developer Mode gates, shared window chrome, minimize/tray behaviour, package badges/cards, utility descriptors, per-package registrars, package presence/simulation, utility cards, browser settings, or global utility affordances.

## Shared Contract Rule

A utility may have a unique workflow, but it must not invent its own behaviour for global concerns.

Use shared contracts for:

* registry identity and access,
* Developer Mode,
* package state and package presence,
* help/context handoff,
* shared window chrome,
* minimize/tray behaviour,
* status badges,
* overlay trays,
* scan freshness,
* empty states,
* toolbar/filter rows,
* package-gated fallback UI.

Bypass a shared helper only when the task has a genuine canvas, graph, matrix, timeline, Scene View, or other special interaction requirement. The exception must state which helper was bypassed, why it was insufficient, what equivalent behaviour remains, and how narrow/wide, focus, persistence, and optional-package states are validated.

## Developer Mode Root Gate

Developer Mode is a global root gate, not a per-window preference. The root switch lives in Utilities Browser -> Browser Settings -> Developer Tools.

Gate levels:

* Visibility gate: shows Developer Mode UI, developer filters, internal utility cards, archived/hidden filters, diagnostics, and explanatory developer badges.
* Metadata mutation gate: allows editing utility metadata, package catalog overrides, documentation links, category membership, hidden/internal/archive flags, developer-only flags, and authoring metadata.
* Source authoring gate: allows source-writing preset tools, generated source stubs, package-facing preset code, or other source-producing tools.

Absolute rule:

* No developer-only control may appear, enable, mutate data, run actions, open source-authoring tools, write metadata, bypass package/status gates, or become callable from menus, context menus, overlays, trays, inspectors, editor windows, shortcuts, browser cards, help panels, stored callbacks, or reflection routes unless the relevant gate is true.

When Developer Mode is disabled:

* developer controls are not drawn,
* developer actions do not execute,
* stale callbacks and direct menu/shortcut paths must not perform gated actions,
* read-only badges are allowed when useful,
* normal workflows must not require Developer Mode.

When Developer Mode is enabled:

* developer surfaces appear in predictable, labelled places,
* each developer control states whether it changes project-local data, package metadata, generated documentation, or source files,
* source-writing actions remain separately gated,
* developer badges pair colour with text labels and tooltips,
* developer filters show visible state and a restore-default path.

## Shared Window Chrome

Eligible utility windows should use shared chrome for utility-level actions and status.

Required chrome elements:

* title and subtitle/status,
* package badge from descriptor metadata and presence resolver,
* help button with utility ID, package ID, module/tab, section, selected item, and topic anchor,
* Minimize to Tray control routed through Core minimizer/tray service,
* Developer badge only when Developer Mode is enabled and the surface contains developer-only controls/data,
* primary action group near the header when persistent access is useful,
* status/freshness row for scan age, selection, package state, or missing dependency state where relevant.

Minimize/tray semantics:

* "Minimize to Tray" is an action, not a local toggle unless a specific panel is being collapsed in place.
* Do not create local parked-window systems, utility-specific minimize toggles, or duplicate tray stacks.
* Minimized state routes through Core minimizer/tray service.
* Restore should return to prior window state as safely as Unity permits.
* Minimized items show utility name, package badge where useful, recent status, restore action, and clear/remove action.
* Developer-only minimized entries must not reveal developer controls unless Developer Mode is enabled.

## Help And Context Handoff

Help affordances preserve:

* utility ID,
* package ID,
* tab/module,
* section,
* selected row/item,
* selected scene/object/asset where relevant,
* topic anchor.

Compact surfaces should open contextual help trays first when staying in place helps. Long help, generated docs, troubleshooting, and reference material should open the Help Browser or full documentation window.

Developer-only help topics and generated docs are hidden or filtered unless Developer Mode is enabled.

## Registry And Per-Package Registrar Doctrine

The registry is the package-level discovery contract. Core owns registration APIs, descriptors, lookup, filtering, sorting, launch policy, recent/favourite/pinned state, and fallback states. Core must not own concrete extension registrations long term.

Descriptor metadata saturation rule:

A user-facing utility registration is incomplete unless its descriptor resolves:

* stable ID,
* display name,
* description,
* lab/category/module,
* primary access path,
* implementation role,
* capability flags,
* visibility model,
* package ID and tier,
* required and optional packages,
* provided and consumed capabilities,
* extension points,
* bridge state,
* documentation topic,
* missing dependency behaviour.

Per-package registrar rule:

* Core exposes registration APIs and discovery services.
* Each package registers only its own utilities, actions, providers, menus, overlays, trays, shortcuts, and bridge hooks.
* Core source files may reference Core, shared contracts, fallback UI, and registrar discovery only.
* If a Core file imports a lab namespace, justify it as a temporary compatibility bridge, move it to a package registrar, or mark it as a release blocker.

Registry validation should detect:

* missing package ID,
* missing implementation role,
* missing documentation topic for product-facing surfaces,
* developer-only utility visible by default,
* extension utility registered by Core default registration,
* package-gated utility without fallback or missing dependency message,
* stale legacy alias pointing to a competing full implementation,
* action path that bypasses Developer Mode or package presence gates.

## Package Presence And Simulation

Package ownership is not package presence.

Separate:

* package map: target ownership and release planning,
* descriptor metadata: package ID, tier, dependencies, capabilities, visibility, fallback behaviour,
* package presence resolver: actual installed packages, asmdefs, providers, symbols, optional bridge availability,
* Developer simulation override: Developer Mode-only testing of installed/missing/disabled states.

Missing package behaviour:

* keep discoverable records visible where useful,
* preserve metadata, owner, stable ID, tags, backlinks, summary, and raw asset/file references,
* show fallback preview when available,
* disable unsupported actions with a clear reason,
* offer safe actions such as Copy ID, Copy path/link, Ping asset/path, Open raw asset/file, Open documentation, or Create linked note when safe,
* show which package or bridge provides the missing capability,
* never remove, corrupt, rewrite, or silently hide data because a concrete package is absent.

Simulation rules:

* simulation controls require Developer Mode,
* simulated states are labelled in package cards, utility cards, and validation logs,
* simulation does not change package manifests or asmdefs,
* simulation does not bypass real compile-time dependency constraints,
* validation includes Core-only, Core-plus-one-extension, full bundle, missing optional bridge, and simulated missing-package states where relevant.

## Common Failure Modes

Treat these as failures:

* local Developer Mode toggles that bypass the root gate,
* developer controls callable from stale menus, shortcuts, trays, or callbacks while the gate is disabled,
* each utility inventing its own window header, help button, package chip, or minimize behaviour,
* a local parked-window/minimize system instead of Core tray service,
* package catalog records treated as proof of installed package state,
* simulated package states not labelled as simulated,
* Core directly importing extension namespaces to register utilities,
* package-gated data disappearing instead of showing safe fallback actions.
