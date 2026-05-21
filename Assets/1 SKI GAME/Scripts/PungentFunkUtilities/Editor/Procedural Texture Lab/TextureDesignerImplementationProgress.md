# Texture Designer Implementation Progress

This file tracks the focused passes that move the Procedural Texture Lab toward the final Texture Designer workflow.

## Completed

- Workflow shell: Compose, Refine, Export, History.
- Compose workflow routing: Manual Compose, Random Explore, Guided Refine, plus reachable placeholders for later workflows.
- Manual Compose MVP: visible layer stack, add/duplicate/delete, add selected texture as layer, combined preview.
- Random Explore MVP: flexible texture grid, guide shelf, guide promotion, guide strength/similarity controls.
- Performance pass: deferred generation queue, preview/full separation, lightweight session persistence.
- Output quality pass: percentile tonal stretching and preview contrast gate.
- Texture Influence Quilt MVP: cached texture-derived trait strips for Random Explore guides.
- Guided Refine MVP: active target canvas, Similarity Ring, trait preserve chips, and Generate Similar behavior.
- Seamless/tileable generation MVP: wrapped stamp rasterization, toroidal spacing, seam repair, seam score, and reachable tileability controls.
- Seam polish and Constraint Match MVP: cached seam heatmap overlays, persistent seam overlay mode, candidate constraint metrics, score matrix, Sort by Score, and Relax controls.
- Richer Constraint Match controls: editable seam/coverage/contrast/scale targets, generation envelope controls, sort modes, pass/review badges, and clearer score feedback.
- Blend Lab MVP: A/B source picking from grid/guides, trait blend controls, generated blend candidates, blend output preview, and A/B Blend Braid diagram.
- Reference Match MVP: reference texture assignment, extraction toggles, cached decomposition previews, Reference Decomposition diagram, and generated matches from selected extracted traits.
- Preset Browser MVP: built-in editable recipe templates, searchable/filterable preset grid, recipe stack preview, and Use/Edit/Duplicate/Guide/Add Layer/Export actions.
- Material Map Prep MVP: map intent defaults, cached map strip previews, export-prep controls, selected-target count, normal/alpha settings, and optional Asset Production bridge fallback.
- History Lineage MVP: snapshot workspace with generation metadata, restore/compare/use-as-guide/add-layer/export actions, and lightweight lineage graph.
- Final MVP validation pass: compile checks, runtime/editor separation check, cache-disposal audit for new preview caches, and workflow reachability pass.
- Manual Compose refinement: auto-preview debounce, output-first workspace, analysis rail, inspector layer cards, selected-layer foldouts, and cached contribution metrics.
- Consolidation stability pass: source-backed control inventory, Explore tab routing, candidate View/Edit Recipe/Add Baked/Guide/Export actions, opacity-correct layer blending, unified tileability controls, metrics popup, lightweight internal library/autosave scaffold, and Guided Compose scaffold.

## Current Pass

- Texture Designer consolidation pass complete at source level. Awaiting live Unity visual QA for the new Explore/Compose routing and candidate recipe editing paths.

## Remaining Focused Passes

1. Full five-phase routing polish: move Guided Refine, Blend Lab, Reference Match, Constraint Match, Presets, and Material Map Prep out of the legacy Compose workflow selector into their final phase modes/overlays.
2. History / Library polish: restore/edit/archive/delete project records and show durable iteration cards from the internal library.
3. Explore polish: tighten guide shelf, candidate card ergonomics, generation balance controls, constraint overlays, preset overlay, and influence quilt readability.
4. Refine polish: consolidate Polish / Similar / Mix Sources / Match Reference around one target texture.
5. Export polish: absorb Material Map Prep fully, verify map previews, normal/alpha behavior, import settings, and optional bridge handoff.
6. Unity visual QA and production validation: docked sizes, domain reload/session restore, cache disposal, and longer live Editor sessions.

## Validation Commands

- `dotnet build PungentFunk.Utilities.Runtime.csproj --no-restore -v:minimal`
- `MSBuild.exe PungentFunk.Utilities.Editor.csproj /t:Build /p:RestorePackages=false /v:minimal`
- `rg -n "UnityEditor" "Assets/1 SKI GAME/Scripts/PungentFunkUtilities/Generation"`

## Latest Validation Results

- Runtime build: passed with existing project/reference warnings.
- Editor build: `dotnet build PungentFunk.Utilities.Editor.csproj --no-restore -v:minimal` currently fails before diagnostics with the existing generated-project issue (`Build FAILED. 0 Warning(s) 0 Error(s)`). Runtime build remains the reliable automated check for this pass.
- Runtime/editor separation: `rg -n "UnityEditor" "Generation"` returned no matches.
