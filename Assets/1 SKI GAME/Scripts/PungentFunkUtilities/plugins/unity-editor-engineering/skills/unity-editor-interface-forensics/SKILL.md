---
name: unity-editor-interface-forensics
description: Use when analysing existing Unity Editor interfaces, built-in windows, package UIs, inspectors, settings pages, overlays, graph tools, profiler views, importers, or package samples to infer the design principles, architecture, controls, and workflow decisions they represent. Use before designing comparable tools or pipelines.
---

# Unity Editor Interface Forensics

Study Unity's own tools as evidence. The goal is not to copy pixels; it is to uncover the product, architectural, and workflow logic behind proven Editor surfaces.

## Analysis Passes

1. **Workflow anatomy:** entry points, primary user goal, setup path, edit path, preview path, apply path, recovery path, and exit path.
2. **Information architecture:** grouping, hierarchy, foldouts, tabs, list/tree/table choices, detail panes, contextual panels, status areas, and persistent controls.
3. **Control vocabulary:** fields, object pickers, toggles, sliders, search, menus, toolbar buttons, context menus, drag/drop, overlays, handles, shortcuts, and selection coupling.
4. **State model:** empty, loading, dirty, invalid, disabled, mixed values, multi-selection, prefab overrides, play mode, compilation, missing assets, long-running task, and error states.
5. **Feedback and safety:** undo, confirmation, preview, dry run, progress, cancellation, validation, warnings, logs, and help boxes.
6. **Implementation clues:** likely public APIs, serialized properties, asset database usage, package settings, UI Toolkit/IMGUI choice, callbacks, caches, and generated artifacts.
7. **Design principles:** which Unity Editor Foundations patterns and general UX principles the interface demonstrates.

## Evidence Sources

- Offline Unity docs and scripting API.
- Unity Editor Foundations pages for components, patterns, interactions, accessibility, writing, layout, iconography, and contextual tooling.
- UnityCsReference for C# Editor/reference-source behavior when public docs are insufficient.
- Package documentation and package samples for feature-specific workflows.
- The local project and installed package cache when the comparable tool is already present.

## Output Template

When asked to analyse an interface, produce:

- **Observed pattern:** what the interface is optimizing for.
- **Architectural implication:** what Unity system likely owns each responsibility.
- **Reusable principles:** what to carry into the new tool.
- **Do-not-copy notes:** what is domain-specific, legacy, deprecated, or version-bound.
- **Comparable design recommendation:** how the user's tool should adapt the pattern.

## Guardrails

- Do not infer private APIs as required just because Unity's own tool may use them.
- Distinguish a documented public extension point from an observed internal implementation detail.
- If screenshots or direct Editor inspection are unavailable, say the analysis is based on docs/source/reference patterns.
