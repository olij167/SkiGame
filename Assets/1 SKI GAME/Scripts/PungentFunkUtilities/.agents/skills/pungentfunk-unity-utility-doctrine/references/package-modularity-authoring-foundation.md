# Package Modularity And Authoring Foundation Doctrine

Use this reference when adding or moving utilities, providers, editors, scanners, authoring data, notes/roadmap features, browser/editor surfaces, package gates, bridges, package metadata, or extension ownership.

## Package Modularity

PungentFunk Utilities must support modular optional extension packages. The free/core package should be useful and stable, but it must not absorb every advanced editor or lab workflow. The full bundle coordinates extensions, but standalone extensions must not assume the bundle is installed unless explicitly bundle-only.

Treat package split maps as release-planning context until a formal package manifest is confirmed. Use them to infer ownership themes and validation needs, not to hardcode final package names, folder moves, or dependency edges unless the user explicitly approves that split.

## Distribution Tiers

| Tier | Owns | Must not own |
|---|---|---|
| Free/Core | Stable contracts, registries, shared theme/preferences, minimal browser shells, shared scan and authoring contracts | Concrete lab workflows, advanced editors, paid-domain implementations |
| Extension | Concrete lab UI, workflows, domain models, scanners, generators, import/export, templates, panels | Core-only services or direct dependencies on unrelated optional packages |
| Bridge | Cross-package handoffs and optional integration glue | Primary ownership of either side workflow |
| Full Bundle | Packaging, samples, bridge enablement, bundle landing pages, cross-lab dashboards | Hidden dependency assumptions that make standalone packages fragile |
| Project Adapter | Game-specific presets, wrappers, compatibility aliases | Generic package logic or assumptions |

## Core Anti-Sink Rule

Core exists to reduce duplication, not to absorb product features.

Core may own:

* stable contracts,
* service lookup,
* shared state vocabulary,
* provider registries,
* descriptors,
* fallback states,
* simple shell UI.

Core must not own:

* concrete lab editors,
* domain-specific scanner logic,
* rich document editing,
* board editing,
* sheet editing,
* audit provider implementations,
* scene placement logic,
* colour/audio/debug workflows,
* package-specific generation behaviour.

Decision test:

* If the code interprets domain-specific meaning, generates domain-specific assets, scans domain-specific content, or renders a concrete editor workflow, it belongs in a lab, extension, bridge, or project adapter.
* If it only defines stable identity, metadata, provider contracts, shared UI/service vocabulary, or safe fallback state, it can belong in Core.

## Shared Abstraction Rule

A helper belongs in Core only when at least two labs need it and the API is stable enough to support publicly. Immature helpers should stay local until repeated need proves they are truly shared.

## Package Capability Declarations

Every significant utility, provider, bridge, and authoring item provider should declare:

* `packageId` and `packageDisplayName`,
* `packageTier`: Core, Extension, Bridge, Bundle, ProjectAdapter, or Internal,
* `packageOwner` and `moduleId`,
* `requiredPackageIds` and `optionalPackageIds`,
* `providedCapabilities` and `consumedCapabilities`,
* `extensionPointsProvided` and `extensionPointsConsumed`,
* `bridgeId` and `bridgeAvailabilityState`,
* `fallbackBehavior` and `missingDependencyMessage`,
* `installHint` and `minimumCompatibleVersion`,
* `isDeveloperOnly`, `isExperimental`, and `isBundleOnly`,
* `documentationTopicId` and `relatedUtilityIds`.

Bridge availability states:

* InstalledAndEnabled
* InstalledButDisabled
* MissingRequiredPackage
* VersionMismatch
* MissingOptionalProvider
* DeveloperModeOnly
* UnsupportedInThisContext

Feature gates should be visible, calm, and explanatory. A missing optional package must not compile-break, throw editor exceptions, leave dead buttons, or hide data. Unsupported actions should be disabled with a concise reason and, where appropriate, an install or enablement hint.

## Codex Package Split Rule

Every implementation brief or patch that adds a utility, provider, editor, scanner, data type, bridge, menu entry, or authoring surface must state:

* which package owns it,
* which package dependencies are required,
* which dependencies are optional,
* which capabilities it provides,
* which extension points it consumes,
* how the UI behaves when optional packages are missing.

## Package Split Release Planning

Prefer staged architecture hardening over a rewrite.

Recommended sequence in principle:

1. Freeze or preserve the current monolithic/full-bundle baseline for regression testing.
2. Add package ownership, tier, dependency, capability, bridge, and fallback metadata to descriptors/providers.
3. Split registration ownership so package-specific descriptors, menus, providers, and actions live in per-package registrars; Core keeps the registry service and Core descriptors.
4. Add or refine asmdef/package skeletons gradually so compile boundaries enforce the intended split.
5. Separate shared contracts from concrete providers, especially for authoring, scans, help/docs, and bridgeable features.
6. Preserve existing browsers and workflows while routing advanced editing or domain behaviour into the owning extension.
7. Add optional bridges only after standalone extension configurations compile and validate without each other.

Do not rely on the full bundle as the only tested configuration. Validate Core-only, Core-plus-one-extension, bridge-disabled, missing-provider, and full-bundle states where relevant.

General likely split themes:

* Core/free holds stable contracts, registry/browser shells, theme/prefs, tray/minimizer, help/docs read/open contracts, shared scan contracts, and Authoring Data Foundation contracts.
* Extensions hold concrete workflows, advanced editors, domain models, scanners, generators, import/export, templates, panels, and product-specific authoring surfaces.
* Bridges hold optional cross-package handoffs and provider registration when both sides exist.
* Project adapters stay thin and outside generic packages.

Avoid overfitting implementation to a temporary package map. If a split is uncertain, add metadata and validation hooks first, then move concrete code only when ownership is clear.

## Shared Authoring Data Foundation

The Authoring Data Foundation is a Core-owned model/contracts layer. It lets notes, rich documents, board documents, data sheets, tasks, help topics, token definitions, documentation links, audit issues, utilities, and external targets share IDs, references, metadata, preview actions, open/copy/ping behaviour, and validation results without forcing every editor package to depend on each other.

## Browser Versus Editor Separation

| Surface | Owns | Does not own |
|---|---|---|
| Authoring Browser | Browse, filter, search, metadata, backlinks, context links, read-only previews, provider status, launch actions | Full editing of rich documents, boards, sheets |
| Rich Document Editor | Text authoring, document structure, inline token insertion, templates, document-specific commands | Global authoring registry, board editing, table editing |
| Board / Whiteboard Editor | Spatial cards, nodes, relationships, dependency maps, canvases, graph minimaps | Long-form prose editing, CSV/table workflows |
| Spreadsheet / Data Sheet Editor | Tables, CSV, scanner-generated sheets, token/content/data coverage, structured data editing | Rich prose editing, graph layout |
| Shared Authoring Foundation | IDs, link targets, metadata, preview/action/validation contracts, migration fields, provider registration | Concrete editor UI or domain-specific workflows |

## Core-Owned Authoring Model Scope

Core may own:

* stable authoring ID helpers,
* authoring item type enum or registry: LegacyNote, RichDocument, BoardDocument, DataSheet, Task, HelpTopic, TokenDefinition, DocumentationLink, AuditIssue, Utility, ExternalTarget,
* authoring link target model: asset GUID, file path, web URL, utility ID, future utility ID, help topic ID, token key, note/document ID, board ID, sheet ID, audit issue code, script path, component type, serialized property path, scene object global ID, external path,
* shared metadata: title, summary, tags, status, priority, visibility, created/updated timestamps, source, package owner, extension owner, stable key, archived flag, developer-only flag, migration version,
* shared open/copy/ping/action contract,
* shared preview provider contract,
* shared validation result contract,
* shared migration/version fields,
* provider registration for document, board, sheet, note, token, help, documentation-link, audit, utility, and external-target providers,
* safe missing-provider behaviour.

## Authoring Provider Contracts

Prefer provider interfaces shaped around:

* authoring item provider,
* preview provider,
* action provider,
* validation provider,
* search provider,
* migration provider.

Names can follow the current codebase. The doctrine is provider separation, not exact interface spelling.

## Safe Missing-Provider Behaviour

When a concrete editor package is absent:

* keep the item visible in the browser,
* preserve metadata, source package, stable ID, tags, backlinks, and summary,
* show fallback preview if available,
* disable unsupported editor actions with a clear reason,
* offer Copy ID, Copy path/link, Ping asset/path, Open raw asset/file, or Create linked note where safe,
* show which package provides the missing capability,
* never remove, corrupt, or silently hide records because a provider is absent.

## Legacy Notes And Roadmap Migration

Do not remove the Notes system. Preserve its browser, automation, metadata, context menu, inspector, scene, audit, token, help, documentation-link, and utility integration features.

Deprecate only the old freeform body editing panel inside Notes and Roadmap.

Migration rules:

* keep legacy notes browseable and manageable,
* expose `PungentNote` records through a LegacyNote authoring provider,
* map existing note targets into shared authoring link targets,
* show legacy body as read-only preview/summary in the Authoring Browser,
* route real editing to Rich Document Editor when installed,
* when Rich Document Editor is absent, show read-only preview plus copy/export/missing-package actions,
* preserve stable IDs, backlinks, context menus, inspector summaries, scene badges, help bridges, audit bridges, token links, and documentation-link relationships,
* migrate legacy note bodies by linked copy first,
* do not destructively convert or delete `PungentNote.body`.

## Concurrency Rule

Rich Document Editor, Board/Whiteboard Editor, and Data Sheet Editor may proceed concurrently only after Authoring Data Foundation contracts compile and the Legacy Note provider demonstrates the shared ID/link/metadata/preview/action model.

No track may invent its own permanent ID, link, metadata, action, or validation model unless it first proves the shared foundation cannot represent the need.

## Serialization

Authoring item IDs and link targets must be stable across package installs, domain reloads, asset moves, and optional package availability changes. Missing providers should not delete or rewrite persisted authoring records. Migration/version fields should be explicit and forward-compatible.
