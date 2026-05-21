# Shared Scan Pipeline Doctrine

Use this reference when adding or changing project searches, scans, validation, coverage checks, indexing, token/documentation checks, findings, project facts, background jobs, or scan result UI.

## Shared Systems First

PungentFunk Utilities should become cohesive through shared package systems, not copy-pasted local implementations.

Before adding a scanner, validator, indexer, findings view, project-fact cache, background job, token parser, documentation checker, or package-health check, first identify whether Core, an existing lab service, an optional bridge, or a shared pattern already owns the concept.

A new local subsystem is an architectural regression when it duplicates shared services for:

* registry metadata,
* theme and layout state,
* scan results and findings,
* help/documentation handoff,
* Developer Mode visibility,
* token parsing,
* background work,
* project-fact caching.

## Ownership Model

Use this split:

* Core owns stable contracts, result models, severity vocabulary, provider registry, scan sessions, project audit index/cache, cooperative scan runner policy, background/idle scheduling policy, scan-state persistence, shared scan UI patterns, service discovery, and package-level coordination.
* Labs own domain-specific scan providers, validators, generators, workflows, and interpretation of domain facts.
* Windows and panels consume scan state. They do not own broad orchestration, indexing, cache invalidation, or cross-lab discovery unless the workflow is genuinely local.
* Optional bridges contribute removable cross-lab or third-party scan providers and degrade gracefully when absent.
* Project adapters may contribute project-specific providers and presets, but generic packages must not compile against project-specific classes.

## Shared Project Scanning Principle

Project-wide scanning is a package-level service, not a per-window implementation detail.

Any utility that inspects assets, scenes, prefabs, references, coverage, setup state, generated documentation, tokens, notes, registry metadata, optional dependencies, or broad project facts must first determine whether the shared project scan pipeline can provide the required data.

Do not create an independent project-wide scanner when the shared pipeline can be extended through a provider, cached result, project audit index entry, shared scan session, or shared result model.

## Provider Versus Local Session

Register a scan provider when the scan:

* produces reusable project facts or findings,
* may be useful to more than one window, panel, dashboard, note, report, checklist, or future lab,
* contributes to package health, release readiness, project audit, coverage, setup validation, dependency checks, token validation, documentation freshness, or registry integrity,
* benefits from cached results, stale-state tracking, background scheduling, provider status, or project audit summary display,
* can run safely through a coordinator-managed immediate or background mode.

Use a local scan session when:

* the scan is scoped to active selection, current inspector object, one asset, one profile, or one small workflow,
* results are useful only inside the current window,
* the operation is closer to preview/apply than reusable project audit,
* background scheduling is unnecessary or unsafe.

Do not register a provider merely because a local button validates a tiny selected object. Local validation may still use shared scan result/session models.

## Shared Scan Result Contract

Shared results should record:

* stable provider/tool ID,
* user-facing display name,
* scan scope and scope label,
* start/completion time and duration,
* total scanned, matched, skipped, changed, warning, and failed counts where relevant,
* severity counts,
* issue list,
* status message,
* cancellation, pause, incomplete, not-configured, failure, blocked, queued, running, fresh, or stale state,
* context to ping, open, inspect, copy, ignore, create a note from, or action a finding.

Issue rows should prefer actionable facts over raw logs. A finding should explain what was found, why it matters, where it is, and the next useful action.

## Background And Immediate Scans

Background scanning must be cooperative, idle-aware, and unobtrusive.

A background-safe provider should:

* declare whether it can run in background mode,
* declare whether it opens scenes, uses `AssetDatabase`, uses modal progress, writes assets, or requires immediate/manual execution,
* work in small time-budgeted steps where practical,
* support cancellation and pause checkpoints where practical,
* avoid scene mutation and asset mutation,
* avoid modal dialogs, focus stealing, broad repaint pressure, and blocking compilation, Play Mode transitions, asset import/update, or active editing.

Immediate scans may be more complete or expensive, but they must be user-triggered, visibly scoped, cancelable where practical, and summarized clearly. Immediate mode is never permission to hide work inside repaint or window-open paths.

## Cache And Stale-State UI

Panels that consume scan information should show cached information immediately when opened, then clearly communicate whether it is fresh, stale, incomplete, running, cancelled, failed, blocked, queued, or not configured.

Mark cached facts stale when relevant project assets, scene hierarchy, configuration, provider settings, package dependencies, generated docs, token definitions, or registry metadata change.

Scan UI must communicate:

* whether results are cached or currently scanning,
* when the last scan completed,
* what scope was scanned,
* whether results may be stale,
* whether a background scan is queued, running, paused, cancelled, failed, blocked, or not configured,
* what action the user can take next.

Do not force a full rescan merely because a window opened. Opening a window should read latest available scan state first.

## Scan UI Doctrine

Any scan-consuming UI should prefer shared scan summary, issue, severity, status, action, and progress presentation patterns.

At minimum, scan UI should expose:

* latest scan summary,
* provider state,
* last scan age,
* stale/fresh indicator,
* scan scope,
* immediate scan action where appropriate,
* background scan state where appropriate,
* cancel, pause, resume, or clear controls where supported,
* filters for severity, provider, scope, freshness, and actionability,
* clear empty state when no scan has run,
* action-oriented issue detail.

Project findings should be scannable at a glance. Summary views should group by provider, severity, actionability, and freshness. Deep technical details should move into an issue detail panel, report, note, or copied fix brief.

## Repaint Safety

`OnGUI`, `CreateGUI`, `Bind`, `Draw`, Scene GUI, overlay draw, and inspector draw paths must not trigger project-wide scans, broad reflection, documentation generation, expensive indexing, scene opening, asset mutation, or graph mutation.

Rendering code may:

* read cached scan state,
* display provider status,
* display progress,
* display summaries and findings,
* request a scan through explicit user action or safe scheduler signal.

Rendering code must not perform the scan itself.

## Scan Integration Review Gate

Before adding a scan-like feature, answer:

1. Is this a local validation, local scan session, project audit provider, background-safe provider, immediate-only provider, generated index, or apply operation?
2. Can an existing provider or cached result satisfy this need?
3. Should this produce reusable project facts?
4. Should other windows consume the result?
5. Can it run in background idle mode safely?
6. Does it declare scope, capabilities, stale conditions, and limitations?
7. Does it use shared result, severity, issue, cache, and summary models?
8. Does the UI show cached results before asking for a rescan?
9. Does the UI show last scan age and stale state?
10. Is all expensive work outside repaint?

A new independent scanner is acceptable only when the shared pipeline is insufficient and the reason is documented. If it later becomes useful across tools, migrate it into the shared pipeline.

## Other Shared Systems

Apply the same shared-systems-first rule to:

* registry metadata and utility discovery,
* theme, layout, splitter, and view state,
* contextual help, documentation handoffs, source badges, annotations, and notes/report workflows,
* Developer Mode and visibility gating,
* findings/issue actions including severity, status, action, ignore, note, copy, open, and ping,
* token parsing, candidate classification, validation, and preview,
* cooperative progress and background task patterns.

Prefer the current shared owners over parallel systems. Class names are implementation references, not permanent doctrine; update pattern docs when ownership changes.
