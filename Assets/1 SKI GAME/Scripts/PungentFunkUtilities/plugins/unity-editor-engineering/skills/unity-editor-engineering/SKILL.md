---
name: unity-editor-engineering
description: Use when designing, implementing, reviewing, or debugging Unity Editor extensions, scripts, custom inspectors, property drawers, editor windows, scene tools, overlays, gizmos, asset importers, build pipeline extensions, package tooling, UI Toolkit or IMGUI editor UI, SerializedObject workflows, Undo/prefab handling, editor validation, or authoring workflows that should feel native to the Unity Editor. Also use when interpreting a design brief, analysing built-in Unity interfaces, researching Unity built-in modules or package registries, or deciding whether a new tool should integrate with existing Unity systems instead of rebuilding them. This skill is about developing scripts and extension designs that make considered use of Unity's full existing suite, not about operating the Editor manually.
---

# Unity Editor Engineering

This skill helps Codex act as a Unity Editor engineering specialist: source-aware, conservative with Unity lifecycles, fluent in built-in systems, and attentive to native Editor UX. Use it to produce tools that cooperate with Unity instead of fighting it.

## Specialist Skills

Use these sibling skills when their narrower lens fits:

- `unity-editor-design-brief`: convert product/design briefs into Unity-native editor extension architecture.
- `unity-editor-interface-forensics`: study built-in Unity interfaces and extrapolate the design principles, control choices, and workflow assumptions they represent.
- `unity-registry-system-atlas`: research built-in modules, Unity Registry/released packages, package samples, and comparable Unity pipelines before proposing new work.

## Core Posture

- Treat Unity's built-in systems as the first design surface: `SerializedObject`, `SerializedProperty`, `Undo`, prefab overrides, `EditorGUI`/`EditorGUILayout`, UI Toolkit, `EditorWindow`, `EditorTool`, overlays, gizmos, importers, settings providers, build callbacks, asset database APIs, selection, scene view, play mode, and domain reload behavior.
- Integrate before inventing. Search for a built-in module, Unity package, package sample, existing Editor window, or documented pipeline that already solves part of the problem.
- Treat existing Unity interfaces as case studies. Their layouts, naming, iconography, validation, contextual actions, and empty/error states encode tested product decisions.
- Prefer documented public APIs. Reach for reflection or internal Editor types only after confirming there is no stable public route, and isolate that risk behind a tiny adapter with version notes.
- Preserve user state: selection, focus, expanded foldouts, scroll positions, object dirty state, undo history, prefab override visibility, and play mode boundaries.
- Make editor scripts multi-object safe unless the request explicitly targets a single-object workflow.
- Assume editor code must survive domain reloads, assembly reloads, asset refreshes, scene changes, prefab stages, play mode transitions, and missing or unloaded assets.
- Keep runtime assemblies free of editor-only references. Put editor scripts under an `Editor/` folder or an editor-only asmdef.

## Source Priority

Use these sources in order, loading only the parts needed for the current task:

1. Project code and local package code.
2. Installed project packages: `Packages/manifest.json`, `Packages/packages-lock.json`, `Library/PackageCache` when available, embedded packages, samples, and local package docs.
3. The uploaded offline Unity documentation zip at `C:/Users/olij1/Downloads/UnityDocumentation.zip`.
4. Unity package documentation and package/manual pages:
   - `https://docs.unity3d.com/Manual/Packages-all.html`
   - `https://docs.unity3d.com/Manual/upm-ui.html`
   - `https://docs.unity3d.com/Packages/`
5. Unity Editor Foundations at `https://www.foundations.unity.com/` for Editor UX, components, patterns, writing, accessibility, and visual language.
6. UnityCsReference at `https://github.com/Unity-Technologies/UnityCsReference` for reference-source study of C# Editor/runtime layers. Treat it as reference-only and version-sensitive; validate against the installed Editor and public docs.
7. Unity Learn, official samples, Unite talks, Unity blog posts, and package samples when they illuminate intended workflows.
8. Highly reputable third-party Unity engineering resources only as secondary interpretation, never as the authority for API contracts or lifecycle behavior.
9. Installed design-principle plugins for general UI/UX judgment:
   - `perception-and-hierarchy-principles`
   - `cognition-and-learnability-principles`
   - `interaction-and-control-principles`
   - `aesthetics-and-emotion-principles`
   - `process-and-robustness-principles`

When Unity documentation and general UX guidance disagree, prefer Unity's native Editor conventions for Editor tooling.

For a compact source map and system taxonomy, read `references/authoritative-sources.md` in this plugin.

## System Research Expectation

For non-trivial tool designs, do not start with implementation. First build a small "Unity system map":

- **Native analogue:** which built-in Editor feature, package window, importer, profiler, graph, settings panel, scene tool, overlay, or inspector is closest?
- **Existing integration points:** public APIs, callbacks, providers, asset types, package samples, UI controls, menu commands, shortcuts, context menus, search providers, overlays, and settings providers.
- **Registry/package options:** built-in modules, Unity Registry packages, released/pre-release packages appropriate for the project version, and installed packages already present.
- **Pipeline ownership:** asset pipeline, import pipeline, build pipeline, rendering pipeline, animation pipeline, physics/navigation pipeline, UI pipeline, test pipeline, profiler/debug pipeline, package pipeline, or authoring workflow.
- **Reuse decision:** what can be configured, extended, wrapped, or composed before creating a new abstraction?
- **Gap statement:** the smallest missing layer that still needs custom code.

## Offline Documentation Workflow

Use `scripts/search_unity_docs.py` from this plugin to inspect the local documentation zip without extracting it:

```powershell
python plugins/unity-editor-engineering/scripts/search_unity_docs.py "SerializedObject ApplyModifiedProperties"
python plugins/unity-editor-engineering/scripts/search_unity_docs.py "UI Toolkit custom inspector" --paths Manual ScriptReference
python plugins/unity-editor-engineering/scripts/search_unity_docs.py "EditorTool" --limit 8
```

Search before making claims about API signatures, lifecycle order, version-specific features, obscure Editor APIs, or best practices likely to have changed.

## Design Decision Checklist

Before implementing an editor feature, decide:

- **Extension point:** custom inspector, property drawer, editor window, overlay, scene tool, settings provider, context menu, asset postprocessor, build callback, or package sample.
- **Built-in precedent:** what Unity interface or package already solves a comparable workflow, and which parts should be copied, integrated, or deliberately avoided?
- **Data path:** direct object fields, `SerializedObject`, ScriptableObject settings asset, `EditorPrefs`, `SessionState`, package settings provider, asset labels, or generated artifact.
- **Lifecycle:** edit mode only, play mode aware, execute always, scene/prefab stage aware, asset refresh aware, domain reload safe.
- **Undo and dirtying:** every user-visible mutation should use `Undo.RecordObject`, `Undo.RegisterCompleteObjectUndo`, serialized property APIs, or the appropriate asset/database dirty path.
- **Prefab semantics:** avoid changes that hide prefab override state or mutate prefab assets unintentionally.
- **Selection and scope:** single object, multi-object, active scene, prefab stage, selected assets, project-wide index, or package-level operation.
- **UI framework:** UI Toolkit for durable tool surfaces and complex layouts; IMGUI for lightweight inspectors, property drawers, compatibility, and existing codebases.
- **Validation:** editor tests when logic is non-trivial; manual verification steps when behavior depends on Unity UI, SceneView, or asset pipeline state.

## Native Editor UX Rules

- Match Unity component vocabulary and layout before inventing new metaphors.
- Use Unity Editor Foundations to choose component patterns, not merely colors. Check authoring flows, content organization, contextual tooling, overlays, errors and messaging, windowing, UI writing, accessibility, color, typography, iconography, and interactions.
- Favor progressive disclosure: show common authoring controls first, advanced or destructive controls behind foldouts, menus, or explicit confirmation.
- Use Unity's standard components and patterns: toolbar, foldout, list/tree view, object field, search field, help box, progress bar, toggle, slider, numeric field, contextual menu, and settings provider.
- Keep labels short, specific, and action-oriented. Avoid explanatory walls inside inspectors.
- Provide immediate feedback for slow, destructive, or asynchronous operations.
- Use disabled states, validation messages, and preview summaries to prevent mistakes before they happen.
- Do not surprise users during asset import, selection changes, domain reload, or play mode transitions.

## Implementation Preferences

- Use `SerializedObject.Update()` and `ApplyModifiedProperties()` around inspector edits.
- Use `EditorGUI.BeginChangeCheck()`/`EndChangeCheck()` or UI Toolkit change events to scope mutations.
- Use `Undo` before mutation and mark assets/scenes dirty through the appropriate Unity path.
- Use `AssetDatabase` operations deliberately; batch expensive imports with `StartAssetEditing`/`StopAssetEditing` only when exception-safe.
- Use `EditorApplication.delayCall` sparingly to escape unsafe callbacks; document why.
- Cache expensive reflection, queries, and style resources, but invalidate on reload or asset changes.
- Avoid long blocking editor operations. Use progress reporting and cancellation for project-wide scans.
- Keep generated files deterministic and easy to diff.

## Review Rubric

When reviewing editor code, lead with risks:

- Data loss, missing undo, dirty state, or prefab override breakage.
- Rebuilding an existing Unity system or package capability without a clear gap statement.
- Missing comparison against a built-in Unity analogue or package sample.
- Domain reload, play mode, scene change, or asset refresh hazards.
- Runtime/editor assembly leakage.
- Multi-object, prefab stage, nested prefab, or variant handling gaps.
- UI that conflicts with Unity Editor Foundations or creates unclear control hierarchy.
- Performance risks in `OnGUI`, `CreateInspectorGUI`, `Update`, asset postprocessors, or project-wide scans.
- Missing tests or manual verification for high-risk editor behaviors.

## Output Style

Be concrete. Name the Unity systems involved, cite the local docs or project code path when used, and explain tradeoffs in terms of Unity lifecycle, user workflow, and maintenance risk.
