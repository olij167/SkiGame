# PungentFunk Documentation Layers

Use this reference when improving the skill, design bible, per-lab specs, repo instructions, documentation workflow, changelog, or review process.

## Layer Model

Keep durable doctrine, task-specific instruction, and volatile execution notes separate.

| Layer | Purpose | Typical location |
|---|---|---|
| Vision | Short alignment on product goals, pillars, workflows, audience, and package identity | `docs/design-bible/index.md` or equivalent |
| Doctrine | Stable architecture, UX, safety, performance, package boundaries, and design principles | `docs/design-bible/doctrine.md` or equivalent |
| Patterns | Reusable implementation examples such as editor windows, preview/apply, list/detail, graph/canvas, bridge, audit table, responsive panels | `docs/design-bible/patterns/` |
| Lab specs | Tool-specific scope, user problem, entry points, dependencies, data flow, Undo/safety, validation, screenshots, diagrams, localization notes | `docs/design-bible/labs/` |
| Instruction surfaces | Agent-facing repo rules, path-specific coding rules, and specialized skills | `.github/copilot-instructions.md`, `.github/instructions/`, `.agents/skills/` |
| Execution | Issues, task boards, PR descriptions, sprint notes, temporary acceptance criteria | issue tracker / PRs |
| Release history | Curated user-facing and maintainer-facing change narrative | `CHANGELOG.md`, release notes |

## What Belongs In The Skill

Keep:

* when to invoke the skill,
* doctrine workflow for audits and patches,
* boundary and safety checks,
* UX/layout decision rules,
* output expectations,
* links to companion references,
* small canonical examples.

Avoid:

* full design-bible duplication,
* repo-global build/test instructions,
* long per-lab specs,
* volatile ticket details,
* raw changelog history,
* large code samples that should live in scripts or pattern docs.

## What Belongs In Companion Docs

Use design-bible and companion pages for:

* audience routing and reading paths,
* glossary and terminology policy,
* package map and public/internal labeling,
* screenshot and diagram policy,
* localization and accessibility policy,
* per-lab specs,
* good/bad examples with screenshots or diagrams,
* docs ownership, review cadence, and last-reviewed dates.

## Review Checklist For Skill Or Doctrine Changes

Before changing the skill or design bible:

1. Confirm the correct instruction surface.
2. Preserve the strongest existing doctrine.
3. Remove or relocate duplication instead of copying it into more places.
4. Add examples when abstract guidance changes.
5. Keep the skill concise enough to be useful in context.
6. Update related references or companion docs if routing changes.
7. Note whether changelog or owner review is needed.

## Changelog And Versioning Policy

Maintain a curated changelog for meaningful doctrine, skill, package, and documentation changes. Use plain English, describe what changed and why it matters, and avoid dumping raw commit text. Tag doctrine snapshots at release milestones rather than for every copy edit.
