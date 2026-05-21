# Authoritative Sources

Use this file as the source policy for Unity Editor Engineering work.

## Source Tiers

1. **Installed project evidence**
   - Project code, editor scripts, asmdefs, package manifests, lock files, local package cache, embedded packages, samples, generated assets, and project settings.
   - This is the strongest evidence for what can be used in the current project.

2. **Unity official documentation**
   - Offline docs: `C:/Users/olij1/Downloads/UnityDocumentation.zip`
   - Unity Manual and Scripting API: `https://docs.unity3d.com/`
   - Unity package docs: `https://docs.unity3d.com/Packages/`
   - Package Manager docs: `https://docs.unity3d.com/Manual/Packages-all.html` and `https://docs.unity3d.com/Manual/upm-ui.html`

3. **Unity Editor Foundations**
   - `https://www.foundations.unity.com/`
   - Use for native Editor UX: accessibility, color, typography, iconography, interactions, UI writing, contextual tooling, authoring flows, content organization, overlays, windowing, errors and messaging, and component usage.

4. **Unity reference source and official samples**
   - UnityCsReference: `https://github.com/Unity-Technologies/UnityCsReference`
   - Package samples, official GitHub repositories, Unity Learn, Unite talks, and Unity blog posts.
   - Treat reference source as version-sensitive and reference-only. Prefer documented public APIs for production designs.

5. **Reputable third-party interpretation**
   - Use only after Unity-owned sources. Good third-party material can clarify patterns and pitfalls, but it does not define API contracts or Unity lifecycle behavior.

## System Taxonomy To Check

When designing an editor extension, scan for relevant ownership across:

- Asset database, import pipeline, scripted importers, postprocessors, presets, labels, GUIDs, dependencies, and Addressables.
- Serialization, `SerializedObject`, prefab overrides, undo, dirty state, scenes, prefab stages, and multi-object editing.
- UI Toolkit, IMGUI, inspectors, property drawers, editor windows, overlays, toolbars, contextual menus, search, shortcuts, and scene tools.
- Package Manager, built-in modules, Unity Registry packages, released/pre-release packages, samples, package settings, and package dependencies.
- Build pipeline, build profiles, player settings, platform modules, scripting define symbols, tests, CI, and validation.
- Rendering pipeline, URP/HDRP, Shader Graph, VFX Graph, materials, lighting, volumes, decals, quality settings, and graphics settings.
- Animation, Timeline, Animator, Playables, Cinemachine, Splines, physics, NavMesh, input, XR, audio, localization, analytics/services, profiler, memory/performance tools, version control, and collaboration tooling.

## Required Habit

For substantial work, produce a short gap statement before implementation:

> Existing Unity systems cover X and Y. The missing layer is Z, so the custom tool should only own Z.
