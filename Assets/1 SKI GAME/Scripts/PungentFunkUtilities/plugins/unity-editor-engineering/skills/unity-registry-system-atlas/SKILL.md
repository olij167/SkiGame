---
name: unity-registry-system-atlas
description: Use when researching Unity built-in modules, core systems, Unity Registry packages, released/pre-release packages, package samples, built-in package toggles, package dependencies, or existing Unity pipelines before designing an editor extension. Use to map what Unity already provides and decide whether to integrate, configure, extend, wrap, or build new.
---

# Unity Registry System Atlas

Build a working map of Unity's systems before inventing a new one.

## Research Scope

Consider:

- Built-in modules toggled through Package Manager.
- Core packages bound to the installed Editor version.
- Unity Registry packages, released packages, pre-release packages, samples, and dependencies.
- Built-in interfaces and pipelines: asset import, Addressables, build, render pipeline, UI Toolkit, animation, Timeline, Cinemachine, Splines, NavMesh, physics, input, localization, profiler, test framework, version control, cloud/services integrations, search, shader graph, visual scripting, package management, and project settings.
- Local project packages, embedded packages, package cache, package samples, and package documentation.

## Registry Research Workflow

1. Inspect project package files: `Packages/manifest.json` and `Packages/packages-lock.json`.
2. Check installed packages and local package cache before looking online.
3. Search the offline Unity documentation zip for package names, systems, and extension points.
4. Use official Unity docs and package docs for current behavior and supported APIs.
5. Use UnityCsReference to understand Editor/reference-source structure when public docs are thin.
6. Use reputable third-party sources only to clarify practice, adoption, or pitfalls after official sources are checked.

## Integration Decision Matrix

Classify every relevant Unity system as one of:

- **Use directly:** existing feature covers the need.
- **Configure:** settings, presets, samples, package options, import settings, or project settings cover the need.
- **Extend:** public extension point exists and should be the main architecture.
- **Wrap:** stable public API exists but needs workflow-specific orchestration.
- **Bridge:** multiple Unity systems need a small integration layer.
- **Build:** no suitable Unity-owned feature exists, or custom domain behavior is the point.
- **Avoid:** deprecated, experimental, internal-only, incompatible, or too risky for this project.

## Output Template

For substantial research, produce:

- **System map:** relevant Unity systems/packages and what each owns.
- **Precedents:** built-in tools, package samples, and docs to study.
- **Decision:** use/configure/extend/wrap/bridge/build/avoid.
- **Version notes:** installed package versions, Unity version assumptions, and compatibility risks.
- **Architecture consequence:** how the decision changes the proposed editor extension.
- **Open checks:** anything that requires direct Unity Editor inspection or package installation.

## Reliability Rules

- Treat Unity documentation, installed package manifests, package docs, and UnityCsReference as the strongest sources.
- Treat package registry contents as version-sensitive. Verify against the installed Editor and project manifest when possible.
- Do not recommend adding a package unless it clearly reduces custom code or aligns with Unity's intended ownership of the domain.
- Do not rely on internal APIs for production architecture unless the user explicitly accepts maintenance risk.
