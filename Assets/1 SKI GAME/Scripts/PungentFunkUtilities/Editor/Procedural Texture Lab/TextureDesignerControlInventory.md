# Texture Designer Control Inventory

Source-backed inventory for the consolidation pass. Current source and active UI paths are the implementation truth; this document tracks where controls live now and where they should land as the tool collapses into Explore, Compose, Refine, Export, and History / Library.

| Before Location | Current Method/File | Desired Location | Decision | Reason |
| --- | --- | --- | --- | --- |
| Inspector tabs: Compose, Refine, Export, History | `DrawInspectorSelector` / `ProceduralTextureLabInspector.cs` | Explore, Compose, Refine, Export, History / Library | Move | Explore needs to become the idea-generation phase instead of living as a Compose workflow. |
| Compose workflow selector with eight workflows | `DrawComposeWorkflowHeader` / `ProceduralTextureLabToolbarAndWorkbench.cs` | Phase-specific strategy/mode selectors | Move | Random/constraint/preset are Explore strategies; guided/blend/reference are Refine modes; material map prep belongs in Export. |
| Candidate grid | `DrawRandomExploreWorkspace`, `DrawCandidateGrid` / `ProceduralTextureLabToolbarAndWorkbench.cs` | Explore | Preserve | This remains the primary exploration surface. |
| Candidate menu: Edit Parameters, Regenerate, Use as Guide, Blend Lab pick, Add as Layer, Duplicate | `ShowTextureCardMenu` / `ProceduralTextureLabToolbarAndWorkbench.cs` | Explore card semantic actions | Merge | Needs explicit View, Edit Recipe, Add Baked Layer, Use as Guide, Export actions. |
| Candidate adoption | `AdoptCandidate` / `ProceduralTextureLabActions.cs` | View action | Preserve with rename | Viewing a candidate must not replace Manual Compose layers. |
| Candidate as base | `AddCandidateAsBase` / `ProceduralTextureLabActions.cs` | Add as Baked Layer | Rename | The current behavior bakes values into one layer, so the label should say that. |
| Candidate recipe edit | Missing distinct action | Compose | Add | Recipe-backed candidates need to replace or append editable layer stacks. |
| Manual layer stack | `DrawManualLayerStackInspector` / `ProceduralTextureLabCompose.cs` | Compose | Preserve | This is the direct authoring stack. |
| Manual selected-layer editor | `DrawSelectedBaseEditor` / `ProceduralTextureLabCompose.cs` | Compose expert editor / Guided Compose steps | Preserve and route | Expert editing stays; guided editing should be an optional focused mode. |
| Manual persistent Blend Stack diagnostic | `DrawManualComposeAnalysisRail` / `ProceduralTextureLabToolbarAndWorkbench.cs` | Diagnostics overlay | Move | It duplicates the layer stack and consumes persistent space. |
| Layer Contribution rail | `DrawManualComposeAnalysisRail` / `ProceduralTextureLabToolbarAndWorkbench.cs` | Compose diagnostics rail | Preserve | Useful diagnostic surface; keep compact and non-mutating. |
| Output Metrics section | `DrawManualComposeAnalysisRail` / `ProceduralTextureLabToolbarAndWorkbench.cs` | Compact pills plus Metrics overlay | Move | Metrics should help evaluation without dominating the rail. |
| Tile Preview toggle | `DrawManualComposeInspector`, `DrawManualComposeWorkspace`, `DrawTextureOutputControls` | Unified Tileable mode | Merge | Preview-only and generation-time seamless must be distinct. |
| Tileable enum popup | `DrawComposeWorkflowHeader`, `DrawTileabilityGenerationControls`, `DrawRefineInspector` | Unified Tileable mode | Preserve | This is the clearest source of truth; competing toggles should sync through it. |
| Guided Refine workflow | `DrawGuidedRefineWorkspace/Inspector` | Refine: Similar mode | Move | It refines an existing target rather than composing sources. |
| Blend Lab workflow | `DrawBlendLabWorkspace/Inspector` | Refine: Mix Sources mode | Move | A/B mixing is target refinement. |
| Reference Match workflow | `DrawReferenceMatchWorkspace/Inspector` | Refine: Match Reference mode | Move | Reference matching is targeted refinement. |
| Constraint Match workflow | `DrawConstraintMatchWorkspace/Inspector` | Explore: Constrained strategy | Move | It generates and scores candidate grids. |
| Preset Browser workflow | `DrawPresetBrowserWorkspace/Inspector` | Explore/Compose preset overlay | Move | Presets are starts/sources, not a top-level workflow. |
| Material Map Prep workflow | `DrawMaterialMapPrepWorkspace/Inspector` | Export | Move | Map prep belongs with export readiness. |
| History lists | `_baseHistory`, `_combinationHistory`, `DrawHistoryInspector`, `DrawHistoryWorkspace` | History / Library | Extend | Needs durable project/iteration persistence and archive/delete/restore flows. |
| Asset Production bridge status | `DetectAssetProductionBridge`, Material Map Prep UI | Export | Preserve with fallback | Optional bridge must remain calm and non-blocking when absent. |
