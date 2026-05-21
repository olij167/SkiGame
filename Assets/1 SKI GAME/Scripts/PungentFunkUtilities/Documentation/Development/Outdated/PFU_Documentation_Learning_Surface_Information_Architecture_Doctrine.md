# Documentation, Learning Surface, and Information Architecture Doctrine

Help and documentation surfaces are first-class product UI in PungentFunk Utilities. They should preserve the full breadth of the package while making that breadth easier to discover, read, and maintain.

## Product Learning Surface

- Do not reduce feature breadth just to reduce visual clutter.
- Use structured information architecture to reveal breadth gradually.
- Treat the Help Browser like an embedded technical documentation site: landing page, search, left navigation, readable article body, optional right context rail, breadcrumbs, related links, and contextual overlay trays.
- Keep public/user-facing help calm and readable when Developer Mode is disabled.
- Keep developer maintenance controls available, but progressively disclosed.

## Documentation Layout

- Start with a clear landing page that offers a small set of task paths.
- Use readable article structure for topic pages: overview, when to use, quick start, main controls, safety notes, examples or scripting/API, troubleshooting, and related material.
- Use left navigation for the documentation tree and search context.
- Use a right context rail for on-page contents, related topics, related utilities, notes, documentation links, source badges, and generated/developer metadata.
- Use overlay trays for contextual help, glossary/chip explanations, package state, documentation-link details, bug-report scaffolds, and quick actions.

## User Notes, Bookmarks, and Learning Memory

- Help systems should expose user-created notes and bookmarks as first-class learning aids.
- Notes and bookmarks must be discoverable globally, not only from the topic where they were created.
- A topic-level note marker is useful, but not sufficient as the only access path.
- Use overlay trays for lightweight review of notes and bookmarks while reading documentation.
- Use the full Notes/Roadmap or Authoring Browser for heavy editing, bulk actions, migration, and rich authoring.
- Keep local bookmarks and editable notes source-separated, even when shown in a combined list.
- Missing note-provider states must not hide local bookmarks.

## Dashboard And Generation Surfaces

- Dashboard surfaces must separate status, action, review, and raw diagnostics.
- Count chips must be tooltip-rich, actionable, or visually subordinate.
- Developer-mode maintenance UI must be collapsible, scrollable, and resizable where panels compete for space.
- Summary cards should show the few metrics that guide the next action; raw counts belong in details, queues, or exports.
- Review queues should be explicit and navigable, with generated, draft, hidden, ignored, stale, and curated states clearly labelled.

## Package-Wide Extrapolation

Any utility with dense status or generation data must use:

- summary first;
- task-path cards;
- collapsible details;
- persistent filters;
- actionable empty states;
- tooltip-rich metadata;
- clear separation between public/user-facing controls and developer-only maintenance controls.

Each utility window should expose learning affordances without letting help buttons clutter the main workflow. Quick help and popup-style documentation should prefer overlay trays when the task is contextual and lightweight.
