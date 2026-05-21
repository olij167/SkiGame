---
name: unity-editor-design-brief
description: Use when the user gives a design brief, product idea, workflow pain point, or desired Unity Editor extension and wants the best way to implement it inside Unity. This skill translates intent into Unity-native architecture by identifying built-in systems, package integrations, UI patterns, extension points, and the smallest custom layer required.
---

# Unity Editor Design Brief

Turn a brief into a Unity Editor extension design that feels like it belongs in Unity and avoids unnecessary custom work.

## Brief Interpretation

Extract:

- The user's real workflow goal, not just the requested UI.
- The authoring context: inspector, scene view, project window, package manager, profiler, build settings, asset import, graph, overlay, settings, or runtime debug workflow.
- The objects being manipulated: scene objects, assets, prefabs, packages, import settings, build profiles, render settings, animation data, navigation data, UI documents, or generated content.
- The user roles: designer, technical artist, gameplay programmer, tools programmer, QA, build engineer, content author, or solo developer.
- The frequency and risk: daily authoring, occasional setup, destructive migration, debugging, batch processing, one-off generation, or long-running analysis.

## Unity-Native Architecture Search

Before proposing implementation, identify the most relevant Unity-owned precedents:

- Built-in Editor windows and inspectors with comparable workflows.
- Public extension points: `Editor`, `PropertyDrawer`, `EditorWindow`, `SettingsProvider`, `EditorTool`, `Overlay`, `SearchProvider`, `AssetPostprocessor`, `ScriptedImporter`, build callbacks, package samples, shortcuts, and context menus.
- Data/persistence options: `SerializedObject`, ScriptableObject assets, project settings, user settings, `EditorPrefs`, `SessionState`, package manifests, generated assets, and import metadata.
- Existing packages or modules that should be installed, queried, extended, or integrated.

## Output Template

For substantial designs, answer in this order:

1. **Intent:** one paragraph restating the brief as a Unity workflow.
2. **Unity precedents:** built-in interfaces/packages/pipelines to study or reuse.
3. **Recommended extension point:** the Unity surface where the feature should live.
4. **Architecture:** data model, editor surface, lifecycle, package/runtime boundaries, and integration APIs.
5. **UI/UX principles:** Unity Editor Foundations patterns plus relevant general design-principle plugin lenses.
6. **Reuse before custom:** what should be configured, wrapped, extended, or composed.
7. **Custom gap:** the smallest code that still needs to be written.
8. **Risks and validation:** undo, prefab semantics, domain reload, asset refresh, package versioning, performance, tests, and manual verification.

## Decision Bias

- Prefer contextual tools over giant command centers when the workflow starts from a selected object or asset.
- Prefer settings providers for project/tool configuration.
- Prefer inspectors and property drawers for editing serialized object state.
- Prefer overlays or scene tools when direct scene manipulation is central.
- Prefer package integration when a Unity Registry package owns the domain.
- Prefer UI Toolkit for substantial new Editor UI; use IMGUI when local codebase compatibility or property drawer constraints make it the better fit.
