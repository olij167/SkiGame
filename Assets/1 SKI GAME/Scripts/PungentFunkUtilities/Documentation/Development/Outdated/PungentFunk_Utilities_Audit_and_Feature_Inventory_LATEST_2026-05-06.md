# PungentFunk Utilities - Latest Audit and Desired Feature Inventory

Scope: extracted and audited the latest uploaded `PungentFunkUtilities.zip`. The zip contents are treated as the source of truth for implementation status; the architecture/design bible, previous inventory documents, bundled documentation, implementation brief, and available project conversation context are used as target/backlog references.

## Latest audit summary

| Metric | Current archive result |
| --- | --- |
| C# scripts | 97 |
| Editor scripts | 56 |
| Runtime/shared scripts | 41 |
| Total C# lines | 32,754 |
| Files with namespace declarations | 97 |
| Assembly definition files | 2 |
| MenuItem declarations | 79 |
| CreateAssetMenu declarations | 12 |
| EditorWindow classes | 29 |
| Central registry entries | 27 |

## Main deltas since the older bundled documentation

- The package currently contains **97 C# scripts**, not the 94 scripts still reported by the bundled documentation.
- Namespace hardening is complete at file level: **97/97 C# scripts** now declare namespaces.
- Bundle-level assembly separation exists: `PungentFunk.Utilities.Runtime.asmdef` and `PungentFunk.Utilities.Editor.asmdef`.
- CreateAssetMenu roots are now standardised under `PungentFunk Utilities/...`.
- `PungentUtilityPackageStatus.cs`, `PungentUtilityDesignAudit.cs`, and `PungentUtilityDesignAuditWindow.cs` are present and should be considered current Core architecture features.
- The docs bundled inside the zip are stale and should be replaced with the updated files generated from this audit.

## Resolved blockers

- **Global namespace blocker resolved:** every C# file now has a namespace declaration.
- **Initial asmdef blocker resolved:** bundle-level runtime/editor asmdefs exist.
- **CreateAssetMenu root drift resolved:** all 12 asset menu roots now use `PungentFunk Utilities/...`.
- **Package status infrastructure implemented:** statuses are now represented in source and registry metadata.
- **Design/package audit tool implemented:** current source includes a utility that audits many package-readiness criteria from inside Unity.

## Remaining architecture blockers

- **Per-lab and optional bridge asmdefs are still missing.** Current asmdefs support a bundle-style package, not independently releasable labs.
- **Potential TMP/asmdef compile risk:** `FontPreviewWindow.cs` directly uses `TMPro`, but the Editor asmdef currently references only `PungentFunk.Utilities.Runtime`. Add an explicit TMP reference, optionalize this code, or split TMP preview into a bridge.
- **One runtime/shared script still references UnityEditor:** `Modular Paths/ModularPathSpawner.cs`, guarded by editor-only blocks. This is acceptable short-term but should be split for stricter package boundaries.
- **Large monolithic files remain.** Debug, Palette, Audio, Modular Path, Terrain, Reference Scanner, Theme, and several other windows still need panel/service/state extraction.
- **Scan/cache infrastructure is not unified.** Many scanners still own their own result models and AssetDatabase/Resources scan flows.
- **Legacy menu aliases remain.** They may be useful compatibility routes, but should be explicitly kept, deprecated, or removed before release.
- **Optional cross-lab integration is still metadata-level.** Related utility links exist, but optional bridge packages and missing-integration UI cards do not.
- **New requested workflow features are not present yet:** Minimizer Tray tab-strip behaviour, Developer Mode, developer-only theme preset archive controls, USS bridge text-style controls, and user-linkable documentation toolbars.

## Inventory status counts

| Status | Count |
| --- | --- |
| Fully implemented | 44 |
| Partially implemented | 73 |
| Scaffolded | 18 |
| Yet to implement | 84 |

## Inventory rows by lab

| Area / Lab | Rows |
| --- | --- |
| Action / Ability Authoring Lab | 4 |
| Agent Simulation Lab | 4 |
| Appearance Lab | 3 |
| Asset Placement Lab | 31 |
| Asset Preview & Export Lab | 6 |
| Audio Lab | 15 |
| Colour Lab | 37 |
| Combat Move Resolver Lab | 1 |
| Content Generation Lab | 5 |
| Core / Architecture | 45 |
| Debug Lab | 8 |
| Environment Simulation Lab | 11 |
| Project Audit & Authoring Lab | 11 |
| Save & Settings Lab | 6 |
| Scene Workflow Lab | 9 |
| Texture Lab | 7 |
| UI & Feedback Lab | 12 |
| Visualization Lab | 4 |

## Namespace distribution

| Namespace | File count |
| --- | --- |
| PungentFunk.Utilities.Audio | 14 |
| PungentFunk.Utilities.Editor.Core | 11 |
| PungentFunk.Utilities.Colour | 9 |
| PungentFunk.Utilities.Placement | 9 |
| PungentFunk.Utilities.Editor.SceneTools | 8 |
| PungentFunk.Utilities.Editor.ProjectAudit | 7 |
| PungentFunk.Utilities.Editor.Audio | 6 |
| PungentFunk.Utilities.Editor.Placement | 6 |
| PungentFunk.Utilities.Editor.Colour | 4 |
| PungentFunk.Utilities.Editor.PreviewExport | 4 |
| PungentFunk.Utilities.Debugging | 3 |
| PungentFunk.Utilities.Editor.Generation | 3 |
| PungentFunk.Utilities.Editor.Theme | 3 |
| PungentFunk.Utilities.Content | 2 |
| PungentFunk.Utilities.Editor.Content | 2 |
| PungentFunk.Utilities.Generation | 2 |
| PungentFunk.Utilities.SceneTools | 2 |
| PungentFunk.Utilities.Editor.Debugging | 1 |
| PungentFunk.Utilities.Editor.UI | 1 |

## Assembly definition audit

| Path | Name | References | Include platforms |
| --- | --- | --- | --- |
| Editor/PungentFunk.Utilities.Editor.asmdef | PungentFunk.Utilities.Editor | PungentFunk.Utilities.Runtime | Editor |
| PungentFunk.Utilities.Runtime.asmdef | PungentFunk.Utilities.Runtime | — | Any |

## Central registry audit

| Id | Display name | Lab | Module | Status | Window |
| --- | --- | --- | --- | --- | --- |
| theme-customizer | Utility Window Theme | UtilityCore | Theme | Stable | UtilityWindowThemeCustomizer |
| design-validation-audit | Design Validation Audit | UtilityCore | Architecture | InProgress | PungentUtilityDesignAuditWindow |
| utility-tray | Utility Tray | UtilityCore | Workflow | Experimental | PungentUtilityTrayWindow |
| debug-control | Debug Control Center | DebugLab | Debug | Stable | DebugControlWindow |
| asset-placement-lab | Asset Placement Lab | AssetPlacementLab | Lab Hub | InProgress | AssetPlacementLabWindow |
| surface-align | Surface Align Tool | AssetPlacementLab | Manual Assist | Stable | SurfaceAlignToolWindow |
| modular-path-builder | Modular Path Builder | AssetPlacementLab | Modular Assembly | Experimental | ModularPathWindow |
| scene-navigation | Scene Navigation | SceneWorkflowLab | Navigation | Stable | PungentSceneNavigationWindow |
| scene-gizmo-browser | Scene Gizmo Browser | SceneWorkflowLab | Gizmos | Experimental | PungentGizmoBrowserWindow |
| path-authoring-toolkit | Path Authoring Toolkit | SceneWorkflowLab | Path Authoring | Experimental | PungentPathAuthoringToolkitWindow |
| palette-designer | Palette Designer | ColourLab | Palettes | Stable | PaletteDesignerWindow |
| procedural-texture-lab | Procedural Texture Lab | TextureLab | Procedural Textures | Experimental | ProceduralTextureLabWindow |
| texture-array-baker | Texture Array Baker | TextureLab | Texture Arrays | Stable | TextureArrayBakerWindow |
| audio-setup-coverage | Audio Setup Coverage | AudioLab | Coverage | Experimental | AudioCoverageContextWindow |
| audio-catalog-coverage | Audio Catalog Coverage | AudioLab | Coverage | Experimental | AudioCoverageWindow |
| font-preview | Font Preview | AssetPreviewExportLab | Preview | Experimental | FontPreviewWindow |
| prefab-icon-generator | Prefab Icon Generator | AssetPreviewExportLab | Preview | Stable | PrefabIconGeneratorWindow |
| prefab-asset-exporter | Prefab Asset Exporter | AssetPreviewExportLab | Export | Stable | PrefabAssetExporterWindow |
| component-tuning-copy | Component Tuning Copy | ProjectAuditAuthoringLab | Authoring | Stable | ComponentTuningCopyWindow |
| reference-assignment-scanner | Reference Assignment Scanner | ProjectAuditAuthoringLab | Scanners | Experimental | ReferenceAssignmentScannerWindow |
| terrain-usage-scanner | Terrain Usage Scanner | ProjectAuditAuthoringLab | Scanners | Experimental | TerrainUsageScannerWindow |
| bulk-rename | Bulk Rename | ProjectAuditAuthoringLab | Authoring | Stable | BulkRenameWindow |
| tooltip-notes | Tooltip Notes Browser | ProjectAuditAuthoringLab | Documentation | Experimental | PungentTooltipNotesBrowserWindow |
| coverage-matrix | Coverage Matrix | ProjectAuditAuthoringLab | Coverage | Experimental | PungentCoverageMatrixWindow |
| token-validator | Token Validator | ProjectAuditAuthoringLab | Validation | Stable | PungentTokenValidatorWindow |
| input-prompt-icon-library | Input Prompt Icon Library Populator | UIFeedbackLab | Input Prompts | Experimental | InputPromptIconLibraryAutoFill |
| name-generator | Name Generator | ContentGenerationLab | Names | Stable | NameGeneratorWindow |

### Registry status counts

| Status | Count |
| --- | --- |
| Experimental | 13 |
| InProgress | 2 |
| Stable | 12 |

## CreateAssetMenu audit

| Script | fileName | menuName |
| --- | --- | --- |
| Asset Placement Lab/PungentPlacementAssetSetSO.cs | Placement Asset Set | PungentFunk Utilities/Asset Placement/Asset Set |
| Asset Placement Lab/PungentPlacementRuleSetSO.cs | Placement Rule Set | PungentFunk Utilities/Asset Placement/Rule Set |
| Audio Coverage/AudioClipSetSO.cs | AudioClipSet | PungentFunk Utilities/Audio/Clip Set |
| Audio Coverage/AudioCoverageProfileSO.cs | AudioCoverageProfile | PungentFunk Utilities/Audio/Coverage Profile |
| Audio Coverage/AudioInteractionMatrixSO.cs | AudioInteractionMatrix | PungentFunk Utilities/Audio/Interaction Matrix |
| Audio Coverage/AudioInteractionProfileSO.cs | AudioInteractionProfile | PungentFunk Utilities/Audio/Interaction Profile |
| Audio Coverage/AudioMaterialLibrarySO.cs | AudioMaterialLibrary | PungentFunk Utilities/Audio/Material Library |
| Audio Coverage/AudioSurfaceMaterialSO.cs | AudioSurfaceMaterial | PungentFunk Utilities/Audio/Surface Material |
| Audio Coverage/TerrainAudioMaterialProfileSO.cs | TerrainAudioMaterialProfile | PungentFunk Utilities/Audio/Terrain Material Profile |
| Name Generator/MadLibNameList.cs | New Mad-Lib Name List | PungentFunk Utilities/Content Generation/Mad-Lib Name List |
| Name Generator/NameList.cs | New Name List | PungentFunk Utilities/Content Generation/Name List |
| Palette Designer/PungentColourPaletteSO.cs | Pungent Colour Palette | PungentFunk Utilities/Colour/Palette |

## Legacy/non-primary menu aliases still present

| Menu path | Script |
| --- | --- |
| Tools/RenameTool | Editor/BulkRenameWindow.cs |
| Tools/Debug/Debug Control Center | Editor/Debug Control Window/DebugControlWindow.cs |
| Tools/Font Preview | Editor/FontPreviewWindow.cs |
| Tools/Pungent Colour Suite/Open Palette Designer | Editor/Palette Designer/PaletteDesignerWindow.cs |
| Tools/Terrain/Terrain Usage Scanner | Editor/TerrainUsageScannerWindow.cs |

## Duplicate menu path declarations

These are often normal Unity validation-method pairs, but they are worth reviewing for user-facing clutter.

| Menu path | Declaration count |
| --- | --- |
| Assets/PungentFunk Utilities/Create Placement Asset Set From Selected Prefabs | 2 |
| GameObject/PungentFunk Utilities/Add Placement Socket | 2 |
| Tools/Utilities/Input/Populate Selected Input Prompt Library | 2 |
| GameObject/PungentFunk Utilities/Fast Travel SceneView To Selection | 2 |
| GameObject/PungentFunk Utilities/Open Scene Navigation | 2 |
| GameObject/PungentFunk Utilities/Add Scene Gizmo Source | 2 |
| GameObject/PungentFunk Utilities/Open Tooltip Notes | 2 |
| GameObject/PungentFunk Utilities/Open Surface Align Tool | 2 |
| GameObject/PungentFunk Utilities/Open Component Tuning Copy | 2 |
| GameObject/PungentFunk Utilities/Open Reference Assignment Scanner | 2 |
| GameObject/PungentFunk Utilities/Open Modular Path Builder | 2 |

## Scan/repaint/performance hotspot audit

| Pattern | Total occurrences/files | Files |
| --- | --- | --- |
| SceneView.RepaintAll | 1 | Editor/Utilities Core/PungentEditorPerformanceUtility.cs (1) |
| Resources.FindObjectsOfTypeAll | 5 | Editor/Debug Control Window/DebugControlWindow.cs (1); Editor/ReferenceAssignmentScannerWindow.cs (2); Editor/Scene Gizmos/PungentGizmoBrowserWindow.cs (1); Editor/Utilities Core/Window Themes/UtilityWindowTheme.cs (1) |
| AssetDatabase.FindAssets | 23 | Editor/Audio Coverage/AudioCoverageContextWindow.cs (3); Editor/Audio Coverage/AudioCoverageWindow.cs (3); Editor/Audio Coverage/AudioInteractionMatrixEditor.cs (1); Editor/FontPreviewWindow.cs (2); Editor/InputPromptIconLibraryAutoFill.cs (4); Editor/Name Generator/NameGeneratorWindow.cs (2); Editor/PungentCoverageMatrixWindow.cs (1); Editor/PungentTokenValidatorWindow.cs (1); Editor/ReferenceAssignmentScannerWindow.cs (1); Editor/TerrainUsageScannerWindow.cs (2); Editor/Utilities Core/PungentUtilityDesignAudit.cs (3) |
| Thread.Sleep | 1 | Editor/PrefabIconGeneratorWindow.cs (1) |
| InitializeOnLoad | 3 | Editor/Asset Placement Lab/PungentAssetPlacementUtilitiesRegistrar.cs (1); Editor/PungentTooltipNotesBrowserWindow.cs (1); Editor/PungentUtilityTrayWindow.cs (1) |
| Compile-time or direct TMP reference | 2 | Editor/FontPreviewWindow.cs (1); Editor/Palette Designer/PaletteApplyUtility.cs (1) |

## Largest scripts requiring split-first attention

| Script | Lines | Recommended action |
| --- | --- | --- |
| Editor/Debug Control Window/DebugControlWindow.cs | 2873 | Split into panels/services/state models |
| Editor/Palette Designer/PaletteDesignerWindow.cs | 2176 | Split into panels/services/state models |
| Editor/Audio Coverage/AudioCoverageWindow.cs | 1502 | Split into panels/services/state models |
| Modular Paths/ModularPathSpawner.cs | 982 | Split into panels/services/state models |
| Editor/TerrainUsageScannerWindow.cs | 959 | Split into panels/services/state models |
| Editor/Utilities Core/Window Themes/UtilityWindowTheme.cs | 867 | Split into panels/services/state models |
| Editor/ReferenceAssignmentScannerWindow.cs | 854 | Split into panels/services/state models |
| Editor/Utilities Core/Window Themes/UtilityWindowThemeCustomizer.cs | 817 | Split into panels/services/state models |
| Editor/Name Generator/NameGeneratorWindow.cs | 737 | Split into panels/services/state models |
| Editor/Audio Coverage/AudioCoverageContextWindow.cs | 730 | Split into panels/services/state models |
| Editor/FontPreviewWindow.cs | 675 | Split into panels/services/state models |
| Editor/Asset Placement Lab/AssetPlacementLabWindow.cs | 666 | Split into panels/services/state models |
| Editor/Modular Paths/ModularPathWindow.cs | 661 | Split into panels/services/state models |
| Editor/BulkRenameWindow.cs | 658 | Split into panels/services/state models |
| Editor/Utilities Core/PungentUtilityControlPanelWindow.cs | 632 | Split into panels/services/state models |

## Current script index

| Lab | Script | Lines | Namespace | Primary declarations |
| --- | --- | --- | --- | --- |
| Asset Placement Lab | Asset Placement Lab/PungentPlacedAssetMarker.cs | 19 | PungentFunk.Utilities.Placement | PungentPlacedAssetMarker |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementAssetSetSO.cs | 157 | PungentFunk.Utilities.Placement | PungentPlacementAssetSetSO, PungentPlacementAssetEntry |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementBoundsUtility.cs | 118 | PungentFunk.Utilities.Placement | PungentPlacementBoundsUtility |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementGridUtility.cs | 72 | PungentFunk.Utilities.Placement | PungentPlacementGridUtility |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementGroup.cs | 29 | PungentFunk.Utilities.Placement | PungentPlacementGroup |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementRuleSetSO.cs | 85 | PungentFunk.Utilities.Placement | PungentPlacementRuleSetSO |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementScatterUtility.cs | 341 | PungentFunk.Utilities.Placement | PungentPlacementScatterUtility |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementSocket.cs | 46 | PungentFunk.Utilities.Placement | PungentPlacementSocket |
| Asset Placement Lab | Asset Placement Lab/PungentPlacementTypes.cs | 194 | PungentFunk.Utilities.Placement | PungentPlacementScatterPattern, PungentPlacementAreaMode, PungentPlacementSurfaceAlignment, PungentPlacementCandidateState |
| Asset Placement Lab | Editor/Asset Placement Lab/AssetPlacementLabWindow.cs | 666 | PungentFunk.Utilities.Editor.Placement | AssetPlacementLabWindow, LabModule |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentAssetPlacementAccessMenus.cs | 41 | PungentFunk.Utilities.Editor.Placement | PungentAssetPlacementAccessMenus |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentAssetPlacementUtilitiesRegistrar.cs | 35 | PungentFunk.Utilities.Editor.Placement | PungentAssetPlacementUtilitiesRegistrar |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementApplyUtility.cs | 137 | PungentFunk.Utilities.Editor.Placement | PungentPlacementApplyUtility |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementAssetSetBuilder.cs | 90 | PungentFunk.Utilities.Editor.Placement | PungentPlacementAssetSetBuilder |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementSocketValidator.cs | 86 | PungentFunk.Utilities.Editor.Placement | PungentPlacementSocketValidator, ValidationMessage |
| Asset Preview & Export Lab | Editor/FontPreviewWindow.cs | 675 | PungentFunk.Utilities.Editor.PreviewExport | FontPreviewWindow, FontEntry |
| Asset Preview & Export Lab | Editor/Prefab Exporter/PrefabAssetExporter.cs | 297 | PungentFunk.Utilities.Editor.PreviewExport | PrefabAssetExporter, ExportOptions, ExportReport |
| Asset Preview & Export Lab | Editor/Prefab Exporter/PrefabAssetExporterWindow.cs | 114 | PungentFunk.Utilities.Editor.PreviewExport | PrefabAssetExporterWindow |
| Asset Preview & Export Lab | Editor/PrefabIconGeneratorWindow.cs | 403 | PungentFunk.Utilities.Editor.PreviewExport | PrefabIconGeneratorWindow, PrefabIconGeneratorService, IconJob |
| Audio Lab | Audio Coverage/AudioClipSetSO.cs | 152 | PungentFunk.Utilities.Audio | AudioClipEntry, AudioClipSetSO |
| Audio Lab | Audio Coverage/AudioCoverageProfileSO.cs | 513 | PungentFunk.Utilities.Audio | AudioCoverageProfileSO, AudioCueBindingRule, name, AudioComponentExpectationRule |
| Audio Lab | Audio Coverage/AudioInteractionMatrixSO.cs | 91 | PungentFunk.Utilities.Audio | AudioCategoryFallbackRule, AudioInteractionMatrixSO |
| Audio Lab | Audio Coverage/AudioInteractionProfileSO.cs | 70 | PungentFunk.Utilities.Audio | ContactEventAudioRule, InteractionBlendSettings, AudioInteractionProfileSO |
| Audio Lab | Audio Coverage/AudioMaterialLibrarySO.cs | 39 | PungentFunk.Utilities.Audio | AudioMaterialLibrarySO |
| Audio Lab | Audio Coverage/AudioMaterialResolver.cs | 119 | PungentFunk.Utilities.Audio | AudioMaterialResolver |
| Audio Lab | Audio Coverage/AudioMaterialTag.cs | 69 | PungentFunk.Utilities.Audio | AudioMaterialRole, AudioMaterialTag |
| Audio Lab | Audio Coverage/AudioSurfaceMaterialSO.cs | 75 | PungentFunk.Utilities.Audio | AudioMaterialCategory, AudioSurfaceMaterialSO |
| Audio Lab | Audio Coverage/ContactAudioEvent.cs | 23 | PungentFunk.Utilities.Audio | ContactAudioEvent |
| Audio Lab | Audio Coverage/ContactAudioEventType.cs | 17 | PungentFunk.Utilities.Audio | ContactAudioEventType |
| Audio Lab | Audio Coverage/ContactAudioRouter.cs | 210 | PungentFunk.Utilities.Audio | ContactAudioRouter |
| Audio Lab | Audio Coverage/ContactEventClassifier.cs | 111 | PungentFunk.Utilities.Audio | ContactRawInput, ContactEventClassifier |
| Audio Lab | Audio Coverage/ContactLoopInstance.cs | 67 | PungentFunk.Utilities.Audio | ContactLoopInstance |
| Audio Lab | Audio Coverage/TerrainAudioMaterialProfileSO.cs | 139 | PungentFunk.Utilities.Audio | WeightedAudioMaterial, TerrainLayerAudioBinding, TerrainAudioMaterialProfileSO |
| Audio Lab | Editor/Audio Coverage/AudioCoverageContextWindow.cs | 730 | PungentFunk.Utilities.Editor.Audio | AudioCoverageContextWindow, ResultSeverity, ScanResult |
| Audio Lab | Editor/Audio Coverage/AudioCoverageProfileSOEditor.cs | 57 | PungentFunk.Utilities.Editor.Audio | AudioCoverageProfileSOEditor |
| Audio Lab | Editor/Audio Coverage/AudioCoverageWindow.cs | 1502 | PungentFunk.Utilities.Editor.Audio | AudioCoverageWindow, CueStatus, WindowTab, CueAuditRow |
| Audio Lab | Editor/Audio Coverage/AudioInteractionMatrixEditor.cs | 399 | PungentFunk.Utilities.Editor.Audio | AudioInteractionMatrixEditor, MissingPair |
| Audio Lab | Editor/Audio Coverage/AudioMaterialTagEditor.cs | 160 | PungentFunk.Utilities.Editor.Audio | AudioMaterialTagEditor |
| Audio Lab | Editor/Audio Coverage/TerrainAudioProfileEditor.cs | 175 | PungentFunk.Utilities.Editor.Audio | TerrainAudioProfileEditor, ProfileDiagnostics |
| Colour Lab | Editor/Palette Designer/PaletteApplyUtility.cs | 280 | PungentFunk.Utilities.Editor.Colour | PaletteApplyTargetKind, PaletteApplyTarget, PaletteApplyUtility |
| Colour Lab | Editor/Palette Designer/PaletteDesignerWindow.cs | 2176 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PungentColourPaletteSOEditor.cs | 33 | PungentFunk.Utilities.Editor.Colour | PungentColourPaletteSOEditor |
| Colour Lab | Editor/Palette Designer/PungentPaletteStorageUtility.cs | 160 | PungentFunk.Utilities.Editor.Colour | PungentPaletteStorageUtility |
| Colour Lab | Palette Designer/ColourContrastUtility.cs | 78 | PungentFunk.Utilities.Colour | ColourContrastUtility |
| Colour Lab | Palette Designer/ColourConversionUtility.cs | 200 | PungentFunk.Utilities.Colour | ColourConversionUtility, ColourModeValues, ColourValueMode |
| Colour Lab | Palette Designer/ColourDeficiencyPreviewUtility.cs | 71 | PungentFunk.Utilities.Colour | ColourDeficiencyPreviewMode, ColourDeficiencyPreviewUtility |
| Colour Lab | Palette Designer/ColourHarmonyUtility.cs | 101 | PungentFunk.Utilities.Colour | ColourHarmonyUtility |
| Colour Lab | Palette Designer/PaletteAnalysisUtility.cs | 171 | PungentFunk.Utilities.Colour | PaletteAnalysisUtility, PaletteAnalysisReport, PaletteContrastPair |
| Colour Lab | Palette Designer/PaletteGenerationSettings.cs | 81 | PungentFunk.Utilities.Colour | PaletteGenerationSettings, ColourHarmonyMode |
| Colour Lab | Palette Designer/PaletteGeneratorUtility.cs | 306 | PungentFunk.Utilities.Colour | PaletteGeneratorUtility |
| Colour Lab | Palette Designer/PaletteSwatch.cs | 62 | PungentFunk.Utilities.Colour | PaletteSwatch, PaletteSwatchRole |
| Colour Lab | Palette Designer/PungentColourPaletteSO.cs | 83 | PungentFunk.Utilities.Colour | PungentColourPaletteSO |
| Content Generation Lab | Editor/Name Generator/NameGeneratorWindow.cs | 737 | PungentFunk.Utilities.Editor.Content | NameGeneratorWindow, GenerationMode, TokenBinding |
| Content Generation Lab | Editor/Name Generator/NameListEditor.cs | 123 | PungentFunk.Utilities.Editor.Content | NameListEditor, MadLibNameListEditor |
| Content Generation Lab | Name Generator/MadLibNameList.cs | 184 | PungentFunk.Utilities.Content | MadLibNameList |
| Content Generation Lab | Name Generator/NameList.cs | 131 | PungentFunk.Utilities.Content | NameList |
| Debug Lab | Debug/DebugChannels.cs | 69 | PungentFunk.Utilities.Debugging | DebugChannels, DebugSignals |
| Debug Lab | Debug/DebugRouter.cs | 630 | PungentFunk.Utilities.Debugging | DebugRouter, Level, ChannelSnapshot, SourceSnapshot |
| Debug Lab | Debug/DebugSignalRelay.cs | 90 | PungentFunk.Utilities.Debugging | DebugSignalRelay, StringEvent, SignalRule |
| Debug Lab | Editor/Debug Control Window/DebugControlWindow.cs | 2873 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow, GroupMode, ConditionMode, BoolToggleCategory |
| Project Audit & Authoring Lab | Editor/BulkRenameWindow.cs | 658 | PungentFunk.Utilities.Editor.ProjectAudit | BulkRenameWindow, RenameMode, RenamePreview |
| Project Audit & Authoring Lab | Editor/ComponentTuningCopyWindow.cs | 542 | PungentFunk.Utilities.Editor.ProjectAudit | ComponentTuningCopyWindow, ComponentSelection, CopyStats |
| Project Audit & Authoring Lab | Editor/PungentCoverageMatrixWindow.cs | 320 | PungentFunk.Utilities.Editor.ProjectAudit | PungentCoverageMatrixWindow, CoverageStatus, Template, CoverageEntry |
| Project Audit & Authoring Lab | Editor/PungentTokenValidatorWindow.cs | 217 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenValidatorWindow, TokenDefinition |
| Project Audit & Authoring Lab | Editor/PungentTooltipNotesBrowserWindow.cs | 583 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTooltipNoteScope, PungentTooltipNoteVisibility, PungentTooltipNotePriority, PungentTooltipNote |
| Project Audit & Authoring Lab | Editor/ReferenceAssignmentScannerWindow.cs | 854 | PungentFunk.Utilities.Editor.ProjectAudit | ReferenceAssignmentScannerWindow, ScanMode, Candidate, SuggestionRow |
| Project Audit & Authoring Lab | Editor/TerrainUsageScannerWindow.cs | 959 | PungentFunk.Utilities.Editor.ProjectAudit | TerrainUsageScannerWindow, TerrainUseRecord |
| Scene Workflow Lab | Editor/Modular Paths/ModularPathSpawnerEditor.cs | 316 | PungentFunk.Utilities.Editor.SceneTools | ModularPathSpawnerEditor |
| Scene Workflow Lab | Editor/Modular Paths/ModularPathWindow.cs | 661 | PungentFunk.Utilities.Editor.SceneTools | ModularPathWindow, ModularPathBuilderVectorExtensions |
| Scene Workflow Lab | Editor/PungentPathAuthoringToolkitWindow.cs | 431 | PungentFunk.Utilities.Editor.SceneTools | PungentPathAuthoringToolkitWindow, EditMode, PungentPathAuthoringToolkit |
| Scene Workflow Lab | Editor/PungentSceneHelpOverlay.cs | 29 | PungentFunk.Utilities.Editor.SceneTools | PungentSceneHelpOverlay |
| Scene Workflow Lab | Editor/PungentSceneNavigationWindow.cs | 616 | PungentFunk.Utilities.Editor.SceneTools | PungentSceneNavigationWindow, TrackingOperator, TrackingMode, Waypoint |
| Scene Workflow Lab | Editor/Scene Gizmos/PungentGizmoBrowserWindow.cs | 184 | PungentFunk.Utilities.Editor.SceneTools | PungentGizmoBrowserWindow |
| Scene Workflow Lab | Editor/Scene Gizmos/PungentSceneGizmoSourceEditor.cs | 215 | PungentFunk.Utilities.Editor.SceneTools | PungentSceneGizmoSourceEditor |
| Scene Workflow Lab | Editor/TerrainAlignTool.cs | 496 | PungentFunk.Utilities.Editor.SceneTools | TerrainAlignTool, SurfaceAlignToolWindow, does, TerrainAlignToolWindow |
| Scene Workflow Lab | Modular Paths/ModularPathSpawner.cs | 982 | PungentFunk.Utilities.SceneTools | ModularPathSpawner, ForwardAxis, PathMode |
| Scene Workflow Lab | PungentSceneGizmoSource.cs | 497 | PungentFunk.Utilities.SceneTools | PungentSceneGizmoSource, DrawWhen, GizmoShape, PositionMode |
| Texture Lab | Editor/Procedural Texture Lab/ProceduralTextureLabWindow.cs | 433 | PungentFunk.Utilities.Editor.Generation | ProceduralTextureLabWindow |
| Texture Lab | Editor/Texture Array Baker/TextureArrayBakerUtility.cs | 254 | PungentFunk.Utilities.Editor.Generation | TextureArrayFallbackMode, TextureArrayBakeResult, TextureArrayBakerUtility |
| Texture Lab | Editor/Texture Array Baker/TextureArrayBakerWindow.cs | 459 | PungentFunk.Utilities.Editor.Generation | TextureArraySourceMode, TextureArrayBakerWindow |
| Texture Lab | Generation/ProceduralTextureGenerator.cs | 603 | PungentFunk.Utilities.Generation | ProceduralTextureSite, ProceduralTextureGenerator |
| Texture Lab | Generation/ProceduralTextureSettings.cs | 175 | PungentFunk.Utilities.Generation | ProceduralTexturePattern, ProceduralTextureStampShape, ProceduralTextureBlendMode, ProceduralTextureChannel |
| UI & Feedback Lab | Editor/InputPromptIconLibraryAutoFill.cs | 507 | PungentFunk.Utilities.Editor.UI | InputPromptIconLibraryAutoFill, EntryData, PopulateReport, PopulateOptions |
| Utility Core | Editor/PungentUtilityTrayWindow.cs | 354 | PungentFunk.Utilities.Editor.Core | PungentUtilityTrayWindow |
| Utility Core | Editor/Utilities Core/PungentEditorPerformanceUtility.cs | 266 | PungentFunk.Utilities.Editor.Core | PungentEditorPerformanceUtility, WindowRepaintCounter, IncrementalQueue |
| Utility Core | Editor/Utilities Core/PungentUtilityAccessMenus.cs | 142 | PungentFunk.Utilities.Editor.Core | PungentUtilityAccessMenus |
| Utility Core | Editor/Utilities Core/PungentUtilityControlPanelWindow.cs | 632 | PungentFunk.Utilities.Editor.Core | PungentUtilityControlPanelWindow, LabGroupView, ModuleGroupView |
| Utility Core | Editor/Utilities Core/PungentUtilityDescriptor.cs | 265 | PungentFunk.Utilities.Editor.Core | PungentUtilityDescriptor |
| Utility Core | Editor/Utilities Core/PungentUtilityDesignAudit.cs | 500 | PungentFunk.Utilities.Editor.Core | PungentUtilityDesignAudit, Severity, Issue, Report |
| Utility Core | Editor/Utilities Core/PungentUtilityDesignAuditWindow.cs | 270 | PungentFunk.Utilities.Editor.Core | PungentUtilityDesignAuditWindow |
| Utility Core | Editor/Utilities Core/PungentUtilityLabMenus.cs | 47 | PungentFunk.Utilities.Editor.Core | PungentUtilityLabMenus |
| Utility Core | Editor/Utilities Core/PungentUtilityLabs.cs | 146 | PungentFunk.Utilities.Editor.Core | PungentUtilityLabs |
| Utility Core | Editor/Utilities Core/PungentUtilityPackageStatus.cs | 97 | PungentFunk.Utilities.Editor.Core | PungentUtilityPackageStatus |
| Utility Core | Editor/Utilities Core/PungentUtilityRegistry.cs | 359 | PungentFunk.Utilities.Editor.Core | PungentUtilityRegistry |
| Utility Core | Editor/Utilities Core/Window Themes/UtilityWindowPrefs.cs | 39 | PungentFunk.Utilities.Editor.Theme | UtilityWindowPrefs |
| Utility Core | Editor/Utilities Core/Window Themes/UtilityWindowTheme.cs | 867 | PungentFunk.Utilities.Editor.Theme | UtilityWindowTheme, ThemePreset, DeficiencyPreview, GuiBackgroundScope |
| Utility Core | Editor/Utilities Core/Window Themes/UtilityWindowThemeCustomizer.cs | 817 | PungentFunk.Utilities.Editor.Theme | UtilityWindowThemeCustomizer, HarmonyMode, CustomPresetLibrary, CustomPresetRecord |

## Desired utility / feature inventory

| Area / Lab | Desired utility / feature | Status | Usefulness | Difficulty | Fit / relevance | Current evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Architecture | Lab-based package architecture: Core + standalone labs + bundle + project adapters | Partially implemented | Critical | High | Core release architecture | PungentUtilityRegistry, PungentUtilityLabs, namespaces, Runtime/Editor asmdefs, bundled Documentation folder | Bundle-level architecture is now much stronger: 97/97 files are namespaced and Runtime/Editor asmdefs exist. Still missing per-lab asmdefs, optional bridge packages, package manifests, and actual project adapter packages. |
| Core / Architecture | PungentUtilityDescriptor metadata contract | Fully implemented | Critical | Medium | Central to every utility | Editor/Utilities Core/PungentUtilityDescriptor.cs | Descriptor now includes lab/module/status/related utility IDs and cached window type resolution. |
| Core / Architecture | Central utility registry | Fully implemented | Critical | Medium | Central launcher/discovery | Editor/Utilities Core/PungentUtilityRegistry.cs | Central registry currently registers 27 utility windows with lab/module/status metadata and related utility IDs. Asset Placement still also has a per-lab registrar that may duplicate/override the same id. |
| Core / Architecture | Control Panel / lab browser | Partially implemented | Critical | Medium | Primary suite navigation | Editor/Utilities Core/PungentUtilityControlPanelWindow.cs | Control Panel is now significantly expanded with status summary, status filtering, lab descriptions, grouped lab/module/tool cards, recent utility support, and related utility links. Still not a full dashboard with pinned/favourite tools, configurable docs cards, toolbar integration, or package health panels. |
| Core / Architecture | Direct lab menu routing | Fully implemented | High | Low | Navigation | PungentUtilityLabMenus.cs | Tools/Utilities/Labs routes exist for current defined labs. |
| Core / Architecture | Editor toolbar quick-launch/status integration | Yet to implement | Medium | Medium | Access surface / workflow speed | No Unity Toolbar/EditorToolbar integration found | Design bible calls for toolbar quick launches, recent tools, active lab shortcuts, preview/debug toggles, and status indicators for frequent workflows. |
| Core / Architecture | Scene View overlay integration for scene tools | Scaffolded | High | High | Scene workflow access surface | PungentSceneHelpOverlay.cs, SupportsSceneOverlay metadata, scene tool windows; no Unity Overlay class found | Scene-related utilities have overlay-style support and metadata, but proper Unity Scene View Overlay integration is not implemented. |
| Core / Architecture | Inspector-embedded toolbars and Open in Lab actions | Partially implemented | High | Medium | Contextual access / authoring speed | Several custom editors exist: PungentSceneGizmoSourceEditor, AudioMaterialTagEditor, TerrainAudioProfileEditor, AudioInteractionMatrixEditor, PungentColourPaletteSOEditor | Some inspectors expose validation/help/quick actions. There is no package-wide inspector embedding standard with Open in Lab buttons and dependency messages. |
| Core / Architecture | Keyboard shortcuts / command palette quick actions | Yet to implement | Medium | Medium | Power-user workflow | No Shortcut attributes or ShortcutManager usage found | Design bible lists command-style actions such as open control panel/current lab, scan current selection, regenerate selected output, and toggle debug tools. |
| Core / Architecture | Selection-aware and context-aware tool launch startup | Partially implemented | High | Medium | Workflow speed / user context | Context menus and selection-support metadata; several windows import current selection | Some tools seed from current selection or context menus. Not all utility windows detect and adapt to current selection/folder/asset type on open. |
| Core / Architecture | Pinned tools, favourite labs, and resume-last-session workflows | Partially implemented | Medium | Medium | Launcher UX / workflow continuity | Control Panel recent utility support; Utility Tray parking; UtilityWindowPrefs | Recent tools and tray parking exist. Pinned/favourite labs, last active module per lab, recently used assets, and resume-last-session workflows remain future work. |
| Core / Architecture | Utility Tray / parked recent windows | Partially implemented | Medium | Medium | Workflow QoL | Editor/PungentUtilityTrayWindow.cs | Compact tray/parking workflow exists, but it does not yet implement the newer requested Minimizer Tray behaviour where free-floating panels minimize into bottom-left tab headers that restore on selection. |
| Core / Architecture | Minimizer Tray: minimize floating utility panels into bottom-left tab headers | Yet to implement | High | High | Utility workflow / editor chrome | PungentUtilityTrayWindow.cs exists but no minimized tab-strip/window-minimize interception code found | Requested behaviour: enable minimize on free-floating panels, move minimized panels to bottom-left above editor toolbar as tab headers only, restore panel when selected, and support multiple minimized headers that feel like tabs of one editor panel. |
| Core / Architecture | Shared UtilityWindowTheme visual system | Fully implemented | Critical | High | UX consistency | UtilityWindowTheme.cs, UtilityWindowThemeCustomizer.cs | Most windows use it. |
| Core / Architecture | Window appearance/theme customizer | Fully implemented | High | High | UX consistency | UtilityWindowThemeCustomizer.cs, UtilityWindowTheme.cs, UtilityWindowPrefs.cs | Theme customizer includes built-in presets, generated/randomized themes, custom local preset save/update/delete, role colours, locks, and persistence. Developer-mode preset overwrites/archive workflow is separate future work. |
| Core / Architecture | Developer Mode toggle for extra design/package permissions | Yet to implement | High | Medium | Safe shipping / internal tooling boundary | No developer-mode preference, status gate, or permission service found | Requested as a toggle that exposes extra internal design controls without shipping those permissions to normal users by default. |
| Core / Architecture | Theme Recipe developer overwrite selected starter theme | Yet to implement | High | Medium | Theme authoring / developer tooling | UtilityWindowThemeCustomizer.cs has preset/custom-preset workflows but no warning-style developer overwrite section | Requested first Developer Mode feature: warning-style section at bottom of Theme Recipe panel to overwrite selected starter theme with current values. |
| Core / Architecture | Theme preset add/remove/archive management with developer-only archived presets | Partially implemented | High | Medium | Theme authoring / release curation | UtilityWindowThemeCustomizer.cs custom preset save/update/delete; built-in preset list; no archive/developer visibility model | Custom local presets can be saved, applied, and deleted. Built-in preset add/remove/archive and developer-only archived visibility remain unimplemented. |
| Core / Architecture | USS bridge text-style controls and Style Explorer font preset mapping | Yet to implement | High | High | Appearance Lab / editor skinning | No USS bridge or Style Explorer script found in current package | Requested controls for mapping Heading/Subheading/Body/Muted/Link/Field/Path/Code font presets to text elements and keeping inspector/hierarchy font sizes consistent when USS bridge/session GUI styles are enabled. |
| Core / Architecture | Theme presets include curated commercially redistributable fonts | Yet to implement | Medium | Medium | Appearance presets / release polish | No font assets or theme-font binding model found in package; FontPreviewWindow exists | Requested future preset pass should include relevant fonts with commercially safe redistribution licences. Needs a font-license-aware asset pipeline or curated package assets. |
| Core / Architecture | Shared UtilityWindowPrefs persistence | Partially implemented | High | Low | UX consistency | UtilityWindowPrefs.cs + many window pref keys | Shared bool/int/float/string prefs exist and are widely used. More session/state concepts such as pinned tools, favourite labs, doc link profiles, and resume-last-workspace are still incomplete. |
| Core / Architecture | Shared editor performance/incremental queue helper | Partially implemented | High | Medium | Performance foundation | PungentEditorPerformanceUtility.cs | Now includes throttled repaint helpers, type-resolution caching, repaint diagnostics, progress helper, cached filtering, and IncrementalQueue. Scanner-heavy tools still need migration to a shared scan model. |
| Core / Architecture | Package status badges: Stable/Experimental/In Progress/Deprecated/Project Adapter | Fully implemented | High | Low | Release polish | PungentUtilityPackageStatus.cs, PungentUtilityDescriptor.cs, PungentUtilityRegistry.cs, PungentUtilityControlPanelWindow.cs | Shared status constants/descriptions/tints/sort keys exist. Current central registry status counts: {'Stable': 12, 'InProgress': 2, 'Experimental': 13}. |
| Core / Architecture | Lab descriptions/help text in launcher | Fully implemented | High | Low | Discoverability | PungentUtilityLabs.GetDescription, PungentUtilityControlPanelWindow.cs | Standard lab descriptions exist and are surfaced by launcher/tooltips. |
| Core / Architecture | Documentation buttons/cards on each utility tile | Partially implemented | High | Medium | Discoverability/support | PungentUtilityDesignAuditWindow.cs, bundled Documentation folder | Design Audit has fixed Open Design Bible/Open Inventory buttons and docs are bundled, but per-utility docs buttons/cards and user-configurable documentation links are not implemented. |
| Core / Architecture | Category colour coding in control panel | Partially implemented | Medium | Medium | UX navigation | UtilityWindowTheme roles/pills; PungentUtilityControlPanelWindow cards/status tints | Status/lab visual treatment is improved; category-specific colour taxonomy is still not complete. |
| Core / Architecture | Namespace hardening | Fully implemented | Critical | High | Export safety | 97/97 C# files contain namespace declarations | Previous global namespace blocker is resolved for the current archive. Unity compile validation is still recommended after the namespace migration. |
| Core / Architecture | Assembly definitions per runtime/editor/lab/bridge | Partially implemented | Critical | High | Export/package safety | PungentFunk.Utilities.Runtime.asmdef, Editor/PungentFunk.Utilities.Editor.asmdef | Bundle-level Runtime and Editor asmdefs exist and Editor references Runtime. Per-lab asmdefs and optional bridge asmdefs are still future work. The Editor asmdef does not reference TextMeshPro despite FontPreviewWindow using TMPro, so TMP must be declared, optionalized, or bridged. |
| Core / Architecture | Per-lab package manifests and standalone release metadata | Yet to implement | Critical | High | Asset Store / modular distribution | No package.json/manifests or per-lab asmdefs found | Required for independently releasable lab packages and optional bridge packages. Current package is bundle-oriented. |
| Core / Architecture | TextMeshPro optional dependency isolation or asmdef declaration | Partially implemented | High | Medium | Compile safety / optional dependencies | FontPreviewWindow.cs uses TMPro; PaletteApplyUtility uses reflection/string optional handling; Editor asmdef has no TMP reference | PaletteApplyUtility mostly handles TMP optionally. FontPreviewWindow is compile-time TMP-dependent and should be moved to a TMP bridge or the Editor asmdef should declare Unity.TextMeshPro where appropriate. |
| Core / Architecture | Standard Tools/Utilities menu policy | Partially implemented | High | Medium | Professional polish | 79 MenuItem declarations; primary menus mostly under Tools/Utilities plus Assets/GameObject context menus | Primary tool menus are mostly standardised. Legacy aliases remain under Tools/Debug, Tools/Font Preview, Tools/RenameTool, Tools/Terrain, and Tools/Pungent Colour Suite. Several validation overloads share the same menu path, which is normal in Unity but should be reviewed for clarity. |
| Core / Architecture | Standard CreateAssetMenu root: PungentFunk Utilities/<Lab>/<Asset Type> | Fully implemented | High | Low | Professional polish | 12 CreateAssetMenu declarations all use PungentFunk Utilities/... roots | Former PungentFunk/Audio and Pungent Funk/Generation drift has been corrected. Some sublab labels can still be refined, but the root is consistent. |
| Core / Architecture | Optional cross-lab bridge/degraded UI cards | Scaffolded | High | High | Standalone lab safety | PungentUtilityDescriptor.RelatedUtilityIds, PungentUtilityRegistry.RelatedUtilities, Control Panel related utility buttons | Related utility metadata now exists and is surfaced in the launcher. Dedicated bridge packages, missing-integration disabled cards, and soft dependency service lookup are not implemented. |
| Core / Architecture | Shared scan/cache/performance infrastructure | Partially implemented | Critical | High | All scanner windows | PungentEditorPerformanceUtility.cs, PungentUtilityDesignAudit.cs, some window-local caches | Performance helpers and design audit checks exist. Scanner-heavy windows still mostly own local scan logic and shared scan result models have not been adopted package-wide. |
| Core / Architecture | Large-window panel/service/state split | Yet to implement | Critical | High | Maintainability | Largest scripts still monolithic: DebugControlWindow, PaletteDesignerWindow, AudioCoverageWindow, ModularPathSpawner, TerrainUsageScannerWindow, UtilityWindowTheme, ReferenceAssignmentScannerWindow | No substantial split into panels/services/state files has occurred yet. |
| Core / Architecture | Dry-run/preview/apply pattern for destructive workflows | Partially implemented | Critical | Medium | Safety | Placement preview/apply, reference scanner, tuning copy, rename preview | Good in many tools, but not uniformly documented/standardised. |
| Core / Architecture | Undo/SetDirty safety for scene/asset modification | Partially implemented | Critical | Medium | Safety | PlacementApplyUtility, palette changes, editor scanners | Present in many areas; needs package-wide audit. |
| Core / Architecture | Tooltips and contextual enum descriptions for all fields | Partially implemented | High | Medium | Usability | Many GUIContent tooltips exist | Not universal; enum-specific help is inconsistent. |
| Core / Architecture | Responsive small-window layouts, vertical scrolling, draggable shared borders | Partially implemented | High | Medium | UX quality | Theme resize handles; many scroll views | Several windows still have large min sizes or monolithic layouts. |
| Debug Lab | Debug Control Center window | Partially implemented | High | High | Debug utility product | Editor/Debug Control Window/DebugControlWindow.cs | Still feature-rich and registered as Stable, but remains a 2873-line monolith. Split pass still recommended. |
| Debug Lab | Debug Router | Fully implemented | High | High | Debug signal backbone | Debug/DebugRouter.cs | Moved out of Editor folder and namespaced as runtime/shared debug infrastructure. |
| Debug Lab | Debug channel/signal constants | Fully implemented | Medium | Low | Debug standardization | Debug/DebugChannels.cs | Moved out of Editor folder and namespaced under PungentFunk.Utilities.Debugging. |
| Debug Lab | Debug signal relay to UnityEvents | Fully implemented | Medium | Low | Scene integration | Debug/DebugSignalRelay.cs | Moved out of Editor folder and namespaced as runtime/shared debug infrastructure. |
| Debug Lab | Reflected debug fields/static toggles/component state inspection | Partially implemented | High | High | Debug authoring | DebugControlWindow.cs | Implemented inside monolith; not split into DebugFieldController service. |
| Debug Lab | Debug action scheduler | Partially implemented | Medium | High | Debug automation | DebugControlWindow.cs | Scheduled reflected actions appear inside monolith; separate DebugActionScheduler file absent. |
| Debug Lab | Router panel reimplementation without serializedObject/targets OnSceneGUI issues | Partially implemented | High | Medium | Stability | DebugControlWindow.cs | Router tooling is present, but still needs the dedicated clean split requested in the reimplementation brief. |
| Debug Lab | Debug coverage reports via Project Audit optional hook | Yet to implement | Medium | Medium | Cross-lab enhancement | No bridge/report module found | Architecture bible lists this as optional hook. |
| Asset Placement Lab | Placement asset set / weighted prefab catalogue | Fully implemented | Critical | Medium | Placement core | Asset Placement Lab/PungentPlacementAssetSetSO.cs | Namespaced under PungentFunk.Utilities.Placement with standard CreateAssetMenu root. |
| Asset Placement Lab | Placement rule set / reusable surface rules | Partially implemented | Critical | Medium | Placement core | Asset Placement Lab/PungentPlacementRuleSetSO.cs | Core rule data exists and is namespaced; advanced contextual rules/repair modules still need expansion. |
| Asset Placement Lab | Placement data models: context, candidate, result, footprint, marker, group | Fully implemented | Critical | Medium | Placement core | PungentPlacementTypes.cs, PungentPlacedAssetMarker.cs, PungentPlacementGroup.cs | Core result/candidate flow exists. |
| Asset Placement Lab | Area Scatter module | Partially implemented | Critical | High | Primary placement workflow | AssetPlacementLabWindow.cs, PungentPlacementScatterUtility.cs | Preview/apply with several patterns exists; needs polish and lab-level integration with all planned rules. |
| Asset Placement Lab | Scatter patterns: random, min spacing, grid, jittered grid, hex, spiral, ring, line | Fully implemented | High | Medium | Scatter generation | PungentPlacementScatterUtility.cs, PungentPlacementTypes.cs | Candidate generation exists. |
| Asset Placement Lab | Heatmap / density-mask scatter | Partially implemented | High | Medium | Contextual placement | RuleSet heatmap fields + ScatterUtility | Texture heatmap sampling exists; no Texture Lab bridge or mask authoring workflow yet. |
| Asset Placement Lab | Terrain scatter with terrain layers/context rules | Partially implemented | High | High | Level-design automation | RuleSet surface mask/height/slope | No terrain-layer material/category weighting system found. |
| Asset Placement Lab | Grid / footprint placement module | Partially implemented | High | Medium | Manual assist | AssetPlacementLabWindow GridFootprint module, GridUtility | Grid snapping and auto-footprint exist; not a complete grid placement/stamping workflow. |
| Asset Placement Lab | Surface brush placement | Yet to implement | High | High | Manual dressing workflow | No brush module found | Explicitly planned as a priority module. |
| Asset Placement Lab | Stamp placement | Yet to implement | Medium | Medium | Manual assist | No stamp module found | Listed as planned manual assist category. |
| Asset Placement Lab | Selection array placement | Yet to implement | Medium | Medium | Manual assist | No selection array module found | Listed as planned manual assist category. |
| Asset Placement Lab | Placement socket marker | Partially implemented | High | Medium | Modular assembly foundation | PungentPlacementSocket.cs | Socket metadata exists. |
| Asset Placement Lab | Socket validator | Partially implemented | High | Medium | Modular assembly safety | PungentPlacementSocketValidator.cs | Validates selected sockets; not a full socket library validator/solver. |
| Asset Placement Lab | Socket graph / modular tile placement | Scaffolded | High | Very High | Modular assembly | Socket component + validator only | Actual graph placement/solver not implemented. |
| Asset Placement Lab | Path placement / modular path generation | Partially implemented | High | High | Scene dressing/paths | ModularPathSpawner.cs, ModularPathWindow.cs | Functional separate path tool; not fully absorbed into Asset Placement Lab. |
| Asset Placement Lab | Cluster growth placement | Yet to implement | Medium | High | Organic placement | No cluster module found | Planned module from implementation brief. |
| Asset Placement Lab | Physics drop / settle placement | Yet to implement | Medium | High | Natural dressing workflow | No physics placement module found | Planned module. |
| Asset Placement Lab | Connector / bridge placement | Yet to implement | Medium | High | Modular assembly | No connector module found | Planned module. |
| Asset Placement Lab | Placement Group Manager | Partially implemented | High | Medium | Generated-scene maintenance | AssetPlacementLabWindow GroupManager module | Scan/select/adopt/delete markers exists; replace/repair workflows missing. |
| Asset Placement Lab | Bounds fill / room dressing | Yet to implement | High | Very High | Level-design automation | No room-dressing module found | Planned module. |
| Asset Placement Lab | Replace variations | Yet to implement | High | Medium | Scene iteration | No replacement module found | Planned scene-management category. |
| Asset Placement Lab | Re-align existing placed objects | Partially implemented | High | Medium | Scene maintenance | SurfaceAlignToolWindow, GroupManager | Surface Align exists, but not a placement-group re-align operation. |
| Asset Placement Lab | Validate asset set | Scaffolded | High | Medium | Authoring safety | AssetSetBuilder and asset set fields | No dedicated asset set validator beyond selection creation/metadata. |
| Asset Placement Lab | Validate socket library | Partially implemented | Medium | Medium | Authoring safety | SocketValidator | Selection validation exists; library-wide validation absent. |
| Asset Placement Lab | Placement preview utility | Scaffolded | High | Medium | UX/safety | Scene preview drawing inside AssetPlacementLabWindow | No separate PungentPlacementPreviewUtility file, despite brief listing it. |
| Asset Placement Lab | Placement overlap validator | Scaffolded | High | Medium | Placement rule quality | Overlap logic appears inside generation/rule flow | No separate PungentPlacementOverlapValidator file. |
| Asset Placement Lab | Surface sampler utility | Scaffolded | High | Medium | Placement rule quality | Raycast sampling inside scatter utility/rules | No separate PungentPlacementSurfaceSampler file. |
| Asset Placement Lab | Backtracking solver | Yet to implement | Medium | Very High | Advanced placement solving | No file found | Listed in planned shared utilities. |
| Asset Placement Lab | Colour Lab palette-driven prefab/material variation bridge | Yet to implement | Medium | High | Cross-lab enhancement | No bridge package found | Architecture bible recommends optional hook. |
| Asset Placement Lab | Texture Lab density-mask bridge | Yet to implement | Medium | High | Cross-lab enhancement | Heatmap field only | No dedicated bridge or handoff workflow. |
| Scene Workflow Lab | Scene Navigation window | Partially implemented | High | Medium | Scene authoring QoL | PungentSceneNavigationWindow.cs | Waypoints/tracking/focus appear present; prefs/splitters could be more consistent. |
| Scene Workflow Lab | Scene Gizmo Source component | Fully implemented | High | Medium | Scene visualization | PungentSceneGizmoSource.cs | Configurable gizmo rules and labels. |
| Scene Workflow Lab | Scene Gizmo Browser | Partially implemented | High | Medium | Scene visualization management | PungentGizmoBrowserWindow.cs | Cached scanning was added; still uses SceneView.RepaintAll in places. |
| Scene Workflow Lab | Scene Gizmo Source custom inspector | Fully implemented | Medium | Medium | Authoring UX | PungentSceneGizmoSourceEditor.cs | Rule editing/templates/preview. |
| Scene Workflow Lab | Scene Help Overlay | Scaffolded | Medium | Low | Assistance UX | PungentSceneHelpOverlay.cs | Lightweight hook only. |
| Scene Workflow Lab | Surface Align Tool / TerrainAlign compatibility | Partially implemented | High | Medium | Scene placement aid | TerrainAlignTool.cs | Functional and generic; legacy naming remains. |
| Scene Workflow Lab | Path Authoring Toolkit | Partially implemented | High | Medium | Path helper foundation | PungentPathAuthoringToolkitWindow.cs | Handle/sampling concepts present; not fully consolidated with path placement. |
| Scene Workflow Lab | Modular Path Builder window/editor/runtime spawner | Partially implemented | High | High | Path/modular assembly | ModularPathSpawner.cs, ModularPathWindow.cs, ModularPathSpawnerEditor.cs | Useful, but runtime file is large and contains editor-only calls behind #if; should split sampling/rebuild/editor logic. |
| Scene Workflow Lab | Scene Workflow lab hub consolidation | Partially implemented | High | Medium | Navigation | Registry lab menu + individual windows | Tools are grouped by registry, but no cohesive lab hub window beyond launcher filtering. |
| Colour Lab | Palette asset + swatch data model | Fully implemented | Critical | Medium | Colour Lab core | PungentColourPaletteSO.cs, PaletteSwatch.cs | Palette storage and swatch metadata exist. |
| Colour Lab | Palette Designer main window | Partially implemented | Critical | High | Primary Colour Lab UI | Editor/Palette Designer/PaletteDesignerWindow.cs | IMGUI Palette Designer remains feature-rich, but still monolithic. Recent requested UX refinements such as palette-panel-first regeneration, stronger advanced button affordance, and more visually distinct harmony outputs require another pass. |
| Colour Lab | Palette panel-first regeneration workflow with collapsed advanced generation options | Partially implemented | High | Medium | Palette Designer UX | PaletteDesignerWindow.cs includes regenerate/save/generation controls, but workflow is still IMGUI monolith | Requested UX direction: palette panel should be primary, with regenerate individual swatches/whole palette visible from the palette panel and advanced generation initially collapsed with harmony mode exposed in the header. |
| Colour Lab | Palette save/load/storage utility | Fully implemented | High | Medium | Palette workflow | PungentPaletteStorageUtility.cs | Load selected/save/save as/default paths exist. |
| Colour Lab | Palette generation engine | Partially implemented | Critical | High | Palette creation | PaletteGeneratorUtility.cs, PaletteGenerationSettings.cs, PaletteDesignerWindow.cs | Core harmony-aware generation exists. User feedback indicates generated palettes can remain too similar across harmony modes, so generation diversity/contrast/tone logic needs strengthening. |
| Colour Lab | Stronger harmony differentiation and generation diversity | Partially implemented | High | Medium | Palette generation quality | PaletteGeneratorUtility.cs, ColourHarmonyUtility.cs | Core harmony modes exist, but current outputs reportedly remain too similar across harmony selections. Needs contrast/tone/temperature/role-aware generation improvements. |
| Colour Lab | Coolors-style organic palette generation | Partially implemented | High | High | Palette UX | PaletteDesignerWindow generation controls | Seed/harmony/ranges/contrast exist; weighted harmony mixer and richer contextual generation absent. |
| Colour Lab | Harmony rules: monochromatic/complementary/split/analogous/triadic/tetradic/square | Fully implemented | High | Medium | Palette theory | ColourHarmonyUtility.cs, PaletteGenerationSettings.cs | Core harmony calculations exist. |
| Colour Lab | Multiple harmony types with priority/mixing ratios | Yet to implement | High | High | Advanced generation | No weighted mixer model found | Previously desired for advanced generator. |
| Colour Lab | Interactive weighted Harmony Mixer bar | Yet to implement | Medium | High | Advanced generation UI | No mixer UI classes found | Mentioned as finalized design in prior colour work, not in current package. |
| Colour Lab | Harmony Influence scalar | Fully implemented | Medium | Low | Generator tuning | PaletteGenerationSettings.harmonyInfluence, UI slider | Present. |
| Colour Lab | Swatch locks | Fully implemented | High | Medium | Palette iteration | PaletteSwatch.locked + UI controls | Locking and locked-count UI exist. |
| Colour Lab | Right-click/context menu for swatches | Fully implemented | High | Medium | Palette iteration | PaletteDesignerWindow.ShowSwatchContextMenu | Lock/regenerate/duplicate/remove/copy actions present. |
| Colour Lab | Regenerate individual swatches | Fully implemented | High | Medium | Palette iteration | RegenerateSwatch flow | Disabled for locked swatches. |
| Colour Lab | Regenerate whole palette while preserving locked swatches | Fully implemented | High | Medium | Palette iteration | RegeneratePaletteUnlocked | Implemented. |
| Colour Lab | Generated variant/base/extra swatch column model | Yet to implement | Medium | High | Advanced palette generation | No GeneratedPaletteVariant UXML/USS or runtime UI files found | Current package uses IMGUI Palette Designer, not the later runtime/UI Toolkit structure. |
| Colour Lab | Include selected colour / include selected palette toggles during generation | Yet to implement | Medium | Medium | Generator workflow | No include selected/include palette fields found | Mentioned in later colour generator plan. |
| Colour Lab | Palette roles and role-aware generation | Partially implemented | High | Medium | Design-system palette output | PaletteSwatchRole + generation role logic | Roles exist; role UI/ordering is basic compared with full desired system. |
| Colour Lab | Role-based ordering | Partially implemented | Medium | Medium | Palette management | SortSwatches and roles | Sorting exists; full role-first layout not complete. |
| Colour Lab | Colour value modes: RGB/HSV/CMYK/LAB/Grayscale conversions | Fully implemented | High | Medium | Colour editing foundation | ColourConversionUtility.cs | Utility support exists. |
| Colour Lab | Dynamic filter sliders by selected colour mode | Partially implemented | High | Medium | Advanced filtering | PaletteDesignerWindow generation sliders + ColourConversionUtility | Mode conversion exists; full filter-mode panel from runtime design is not present. |
| Colour Lab | Filter influence scalar | Yet to implement | Medium | Low | Generation control | No filterInfluence field found | Harmony influence exists, filter influence does not. |
| Colour Lab | Sorting options UI | Fully implemented | Medium | Low | Palette management | PaletteDesignerWindow sort buttons | Hue/value/saturation/name-like controls appear present. |
| Colour Lab | Contrast analysis / WCAG ratings | Fully implemented | High | Medium | Accessibility | ColourContrastUtility.cs, PaletteAnalysisUtility.cs | Contrast pairs and ratings exist. |
| Colour Lab | Deficiency preview / colour blindness filters | Fully implemented | High | Medium | Accessibility | ColourDeficiencyPreviewUtility.cs + toolbar enum | Preview utility and UI selection exist. |
| Colour Lab | Palette application to materials/renderers/UI/text/TMP-like targets | Partially implemented | High | High | Cross-scene application | PaletteApplyUtility.cs | Applies to selected targets and uses reflection for TMP-like text; still needs optional bridge/dependency documentation. |
| Colour Lab | Scene colour tracker / global scene reference tracking | Yet to implement | Medium | High | Scene design workflow | No scene colour tracker/reference panel files | Mentioned in Colour Inspector planning. |
| Colour Lab | Built-in Unity swatches/reference libraries | Yet to implement | Medium | Medium | Reference workflow | No standard library assets found | Mentioned in colour tool refinement. |
| Colour Lab | Recognized standards: Pungent Library, AS 2700, FS 595, BS 381, RAL, ISCC-NBS, NCS, X11, Crayola, Resene, XKCD | Yet to implement | Medium | High | Reference workflow | No reference library datasets found | XKCD/reference standards not present in archive. |
| Colour Lab | Import/export formats such as .aco | Yet to implement | Low | High | Interchange | No import/export parser found | Mentioned as aspirational feature. |
| Colour Lab | Gradient/tint tools | Yet to implement | Medium | Medium | Colour authoring | No dedicated gradient/tint tools found | Mentioned in advanced colour design list. |
| Colour Lab | Palette report card analysis | Partially implemented | High | Medium | Palette quality | PaletteAnalysisUtility.cs | Contrast/role/similarity diagnostics exist; not a full report-card UI/export. |
| Colour Lab | Palette Refinements: harmony section, monochrome bar, tone matrices | Yet to implement | Medium | High | Advanced refinement UI | No refinement panel files found | Detailed UI structure from prior colour chats absent. |
| Colour Lab | Contrast checker matrix visual section | Scaffolded | Medium | Medium | Accessibility visualization | PaletteAnalysisUtility pair list | No matrix VisualElement/section as designed. |
| Colour Lab | Colour blending A/B tone matrix and temperature bars | Yet to implement | Low | High | Advanced refinement | No blending section found | Prior Colour Inspector plan only. |
| Colour Lab | Runtime UI Toolkit Colour Inspector panels/UXML/USS | Yet to implement | Medium | Very High | Runtime tool product | No UXML/USS/controller files found | Current archive is IMGUI editor package. |
| Texture Lab | Procedural Texture Lab window | Partially implemented | High | High | Texture Lab core | ProceduralTextureLabWindow.cs | Functional preview/generate/export; could still use lab split/docs/bridge polish. |
| Texture Lab | Procedural texture settings/runtime-safe model | Fully implemented | High | Medium | Texture generation core | ProceduralTextureSettings.cs | Serializable settings for patterns/stamps/grid/density/etc. |
| Texture Lab | Procedural texture generator engine | Fully implemented | High | High | Texture generation core | ProceduralTextureGenerator.cs | Pattern/stamp/density/texture creation present. |
| Texture Lab | Texture Array Baker window | Partially implemented | High | High | Asset pipeline | TextureArrayBakerWindow.cs, TextureArrayBakerUtility.cs | Functional bake workflow; still in Asset menu category rather than Texture Lab menu. |
| Texture Lab | Organic/pattern generation expansion | Partially implemented | Medium | High | Texture generation | ProceduralTextureGenerator.cs | Many pattern modes exist; future organic generators can build on this. |
| Texture Lab | Palette-to-texture/material-mask generation | Yet to implement | Medium | High | Colour/Texture bridge | No palette bridge found | Listed as future texture lab enhancement. |
| Texture Lab | Density/stamp texture support for placement masks | Partially implemented | Medium | Medium | Texture/Placement bridge | Procedural settings include density maps; placement rule has heatmap | No direct handoff/bridge UI. |
| Audio Lab | Audio clip set asset | Fully implemented | High | Medium | Audio runtime core | Audio Coverage/AudioClipSetSO.cs | Now uses namespace PungentFunk.Utilities.Audio and standard CreateAssetMenu root PungentFunk Utilities/Audio/Clip Set. |
| Audio Lab | Audio setup coverage profile | Partially implemented | High | High | Coverage validation | Audio Coverage/AudioCoverageProfileSO.cs | Now uses standard CreateAssetMenu root. Generic profile exists, but some default cue examples may still need review. |
| Audio Lab | Audio Setup Coverage scanner | Partially implemented | High | High | Audio authoring QA | AudioCoverageContextWindow.cs | Scene/prefab/profile scans exist; monolithic and scan/cache hardening still needed. |
| Audio Lab | Audio Catalog Coverage scanner | Partially implemented | High | High | Audio authoring QA | AudioCoverageWindow.cs | Cue/catalog auditing exists; monolithic and may need generic catalog service alignment. |
| Audio Lab | Surface audio material definitions/library/tags | Fully implemented | High | Medium | Runtime surface audio | AudioSurfaceMaterialSO.cs, AudioMaterialLibrarySO.cs, AudioMaterialTag.cs | Generic surface materials and tags exist. |
| Audio Lab | Audio material resolver for colliders/raycast hits/terrain | Fully implemented | High | High | Runtime surface audio | AudioMaterialResolver.cs | Collider/tag/terrain resolution helpers. |
| Audio Lab | Interaction matrix/profile system | Fully implemented | High | Medium | Runtime surface audio | AudioInteractionMatrixSO.cs, AudioInteractionProfileSO.cs | Material-pair profile resolution exists. |
| Audio Lab | Terrain audio material profiles | Fully implemented | High | Medium | Terrain audio | TerrainAudioMaterialProfileSO.cs, TerrainAudioProfileEditor.cs | Layer bindings and diagnostics exist. |
| Audio Lab | Contact event classifier | Fully implemented | High | Medium | Runtime contact audio | ContactEventClassifier.cs | Classifies impacts/slide/scrape/etc. |
| Audio Lab | Contact audio router/source pooling/loop instances | Partially implemented | High | High | Runtime audio playback | ContactAudioRouter.cs, ContactLoopInstance.cs | Core routing/loops exist; full director/catalog integration missing. |
| Audio Lab | Mixer routing/default mixer setup utility | Yet to implement | Medium | Medium | Audio project setup | No mixer setup generator found | Mentioned in audio setup planning. |
| Audio Lab | Runtime audio cue catalog/director | Yet to implement | High | High | Audio runtime service | No PungentAudioCueCatalogSO/Director found | Future pass in implementation brief. |
| Audio Lab | Generic AudioSource pool service | Scaffolded | Medium | Medium | Audio runtime service | ContactAudioRouter internal pooling | No standalone PungentAudioSourcePool file. |
| Audio Lab | Runtime audio hooks for cue playback | Yet to implement | Medium | High | Audio integration | No runtime hook classes found | Planned future audio lab expansion. |
| Audio Lab | Audio cue coverage module integrated with future director | Yet to implement | High | High | Coverage + runtime integration | Existing coverage windows only | Needs catalog/director model first. |
| Asset Preview & Export Lab | Prefab Icon Generator | Partially implemented | High | High | Asset presentation/export | PrefabIconGeneratorWindow.cs | Non-blocking preview/export exists; could use bridge polish. |
| Asset Preview & Export Lab | Prefab Asset Exporter | Partially implemented | High | High | Asset packaging | PrefabAssetExporter.cs, PrefabAssetExporterWindow.cs | Copies/reuses prefab dependencies; needs release docs/safety validation. |
| Asset Preview & Export Lab | Font Preview | Partially implemented | Medium | Medium | Asset review | Editor/FontPreviewWindow.cs | Font/TMP preview workflow exists, but FontPreviewWindow has a compile-time TMPro dependency while the Editor asmdef does not reference TextMeshPro. For standalone shipping, add the dependency explicitly or isolate TMP support in an optional bridge. |
| Asset Preview & Export Lab | Font licence classification / redistributable font packaging workflow | Yet to implement | Medium | Medium | Asset packaging / font curation | No font licence metadata or font builder workflow found | A previous font archive task required separating commercially redistributable fonts and preserving licence files. This could become a package-prep utility if kept generic. |
| Asset Preview & Export Lab | Preview/batch render helpers | Scaffolded | Medium | High | Future preview/export | PrefabIconGenerator jobs | No generic batch-render service/lab hub yet. |
| Asset Preview & Export Lab | Colourway icon generation bridge | Yet to implement | Medium | High | Cross-lab enhancement | No Colour/Preview bridge found | Architecture bible optional hook. |
| Project Audit & Authoring Lab | Reference Assignment Scanner | Partially implemented | High | High | Generic project QA | ReferenceAssignmentScannerWindow.cs | Strong tool; monolithic and uses broad scans/resources. |
| Project Audit & Authoring Lab | Terrain Usage Scanner | Partially implemented | High | High | Generic project QA | TerrainUsageScannerWindow.cs | Strong tool; monolithic and scan/cache infra not shared. |
| Project Audit & Authoring Lab | Component Tuning Copy | Partially implemented | High | Medium | Authoring productivity | ComponentTuningCopyWindow.cs | Dry-run/copy modes; could use deeper docs and adapter recipes. |
| Project Audit & Authoring Lab | Coverage Matrix | Partially implemented | Medium | Medium | Authoring QA | PungentCoverageMatrixWindow.cs | Generic coverage model/window exists, but not formal shared visualization module. |
| Project Audit & Authoring Lab | Token Validator | Partially implemented | Medium | Medium | Text/content QA | PungentTokenValidatorWindow.cs | Validates text tokens; could become Content/Authoring bridge. |
| Project Audit & Authoring Lab | Tooltip Notes Browser | Partially implemented | Medium | Medium | Documentation/authoring aid | PungentTooltipNotesBrowserWindow.cs | Useful; uses SceneView repainting and lacks full package docs integration. |
| Project Audit & Authoring Lab | Bulk Rename | Partially implemented | High | Medium | Scene authoring productivity | BulkRenameWindow.cs | Preview/apply exists; legacy Tools/RenameTool alias remains. |
| Project Audit & Authoring Lab | ScriptableObject generator profile/module base/folder routing | Yet to implement | High | High | SO-heavy project automation | No SO lab files found | Future high-value lab. |
| Project Audit & Authoring Lab | ScriptableObject validation rules/scanning/repair | Yet to implement | High | High | SO QA | No SO lab files found | Should extract patterns, not game-specific ability/gacha tools. |
| Project Audit & Authoring Lab | Batch-create assets from text/CSV/table input | Yet to implement | High | High | Authoring automation | No batch SO importer found | Future SO lab feature. |
| Project Audit & Authoring Lab | Dependency previews for generated/validated assets | Yet to implement | Medium | High | Authoring safety | No dependency graph preview found | Could link with Visualization Lab. |
| Content Generation Lab | NameList asset | Fully implemented | Medium | Low | Content generation core | Name Generator/NameList.cs | Now uses namespace PungentFunk.Utilities.Content and standard CreateAssetMenu root PungentFunk Utilities/Content Generation/Name List. |
| Content Generation Lab | MadLibNameList asset | Fully implemented | Medium | Medium | Content generation core | Name Generator/MadLibNameList.cs | Now uses namespace PungentFunk.Utilities.Content and standard CreateAssetMenu root PungentFunk Utilities/Content Generation/Mad-Lib Name List. |
| Content Generation Lab | Name Generator window | Partially implemented | Medium | Medium | Content generation UI | NameGeneratorWindow.cs | Functional but monolithic-ish; future content/token generation not present. |
| Content Generation Lab | Name presets assets | Fully implemented | Low | Low | Convenience content | Name Generator/Name Presets/*.asset | Boy/girl/nonbinary/community/last names present. |
| Content Generation Lab | Generic token/text/content generation beyond names | Yet to implement | Medium | High | Future content tools | No broader generator framework found | Mentioned in future lab structure. |
| UI & Feedback Lab | InputPromptIconLibraryAutoFill editor tool | Partially implemented | High | Medium | Current UI lab seed | InputPromptIconLibraryAutoFill.cs | Editor-only populator exists; relies on serialized names rather than runtime model. |
| UI & Feedback Lab | Runtime PungentInputPromptIconLibrary | Yet to implement | High | Medium | Input prompt runtime core | No runtime input prompt files found | Planned next UI lab start. |
| UI & Feedback Lab | Runtime input prompt resolver/token/presenter | Yet to implement | High | High | Input prompt runtime core | No resolver/token/presenter found | Should remain layout-agnostic and Input System optional/guarded. |
| UI & Feedback Lab | Input prompt preview window/editor | Yet to implement | Medium | Medium | Authoring UX | Only auto-fill window exists | Planned UI lab expansion. |
| UI & Feedback Lab | World Prompt Registry/Presenter | Yet to implement | Medium | High | Reusable interaction UI | No world prompt files found | Must avoid SkiGame quest/interaction dependencies. |
| UI & Feedback Lab | Floating Text System | Yet to implement | Medium | Medium | Feedback runtime | No floating text files found | Planned future module. |
| UI & Feedback Lab | Popup Notification System | Yet to implement | Medium | Medium | Feedback runtime | No popup notification files found | Planned future module. |
| UI & Feedback Lab | Tooltip Presenter | Yet to implement | Medium | Medium | Feedback runtime | Tooltip notes browser is editor-only | Runtime presenter absent. |
| UI & Feedback Lab | Right-click Context Menu runtime/editor helper | Yet to implement | Medium | Medium | UI foundation | No generic context menu helper found | Planned future module. |
| UI & Feedback Lab | UI Animation Trigger/Profile system | Yet to implement | Medium | High | Feedback authoring | No animation profile files found | Planned future module. |
| UI & Feedback Lab | Radar/Spider Graph VisualElement | Yet to implement | Low | Medium | Visualization/UI widget | No VisualElement graph found | Planned future UI/Visualization item. |
| UI & Feedback Lab | Generic progress/resource VisualElements | Yet to implement | Medium | Medium | Runtime UI widgets | No runtime visual elements found | Planned future item. |
| Environment Simulation Lab | PungentWindController core | Yet to implement | High | High | Next new lab candidate | No Environment/Wind files found | Recommended next major non-placement suite. |
| Environment Simulation Lab | Wind source/weather/time interfaces | Yet to implement | High | Medium | Adapter-friendly environment core | No IPungentWindSource/IPungentWeatherSource/IPungentTimeSource found | Planned in implementation brief. |
| Environment Simulation Lab | Cloth/Particle/WindZone wind adapters | Yet to implement | Medium | Medium | Unity integration | No wind adapter files found | Planned initial editor/runtime files. |
| Environment Simulation Lab | Wind Lab window/controller editor | Yet to implement | Medium | Medium | Authoring UX | No wind lab window/editor found | Planned initial editor files. |
| Environment Simulation Lab | Time & Calendar core | Yet to implement | Medium | High | Reusable simulation service | No time/calendar files found | Planned later module. |
| Environment Simulation Lab | Weather preset system/transitions | Yet to implement | High | High | Reusable environment authoring | No weather preset files found | Should use adapters, not old WeatherController directly. |
| Environment Simulation Lab | Surface condition broadcaster | Yet to implement | High | High | Placement/audio/gameplay bridge | No broadcaster files found | Future module. |
| Environment Simulation Lab | Local environment volumes | Yet to implement | Medium | High | Scene simulation | No local volume files found | Future module. |
| Environment Simulation Lab | Environment debugger/preview window | Yet to implement | Medium | Medium | Authoring UX | No environment debugger found | Future module. |
| Environment Simulation Lab | RenderSettings/URP/skybox/particle/cloud adapters | Yet to implement | Medium | Very High | Optional integrations | No environment adapters found | Must be optional/bridge-based. |
| Save & Settings Lab | Generic settings profile | Yet to implement | High | Medium | Runtime infrastructure | No settings profile/service files found | Future lab. |
| Save & Settings Lab | Settings service | Yet to implement | High | High | Runtime infrastructure | No settings service found | Future lab. |
| Save & Settings Lab | JSON storage utility/versioning | Yet to implement | High | Medium | Persistence infrastructure | No JSON storage utility found | Future lab. |
| Save & Settings Lab | Save slot manifest/service | Yet to implement | Medium | High | Persistence infrastructure | No save slot service found | Future lab. |
| Save & Settings Lab | Input binding override storage | Yet to implement | High | Medium | Input/UI bridge | No binding storage found | Future lab; should remain generic. |
| Action / Ability Authoring Lab | Generic action sequence framework | Yet to implement | High | Very High | Future reusable gameplay authoring | No action framework files found | Should be inspired by ability/hazard systems, not directly ported. |
| Action / Ability Authoring Lab | Action conditions/effects/targeting | Yet to implement | High | Very High | Future reusable gameplay authoring | No generic action condition/effect files found | Future lab. |
| Action / Ability Authoring Lab | Generic projectile/status/hazard modules | Yet to implement | Medium | Very High | Future gameplay authoring | No modules found | Only if redesigned generically. |
| Combat Move Resolver Lab | MoveDefinition / MoveResolver / ComboState / StyleMeter / context flags | Yet to implement | Medium | High | Future action/trick/combat abstraction | No move resolver files found | Lower priority. |
| Appearance Lab | Generic appearance option assets/catalog | Yet to implement | Medium | High | Character/customization tooling | No appearance catalog files found | Could link to prefab icon generator and Colour Lab. |
| Appearance Lab | Wearable attachment/material-color options/option generator | Yet to implement | Medium | High | Customization authoring | No appearance generator files found | Keep shop/progression as project adapters. |
| Appearance Lab | Appearance catalog validator | Yet to implement | Medium | Medium | Authoring QA | No validator found | Future authoring tool. |
| Agent Simulation Lab | Utility AI/state/evaluator framework | Yet to implement | Medium | Very High | Generic simulation tools | No agent files found | Must avoid direct NPC/dialogue/quest dependencies. |
| Agent Simulation Lab | Generic needs/traits/emotions/interaction scoring | Yet to implement | Medium | Very High | Generic simulation tools | No agent/social files found | Informed by NPC plans, not ported directly. |
| Agent Simulation Lab | Social group based generic NPC behaviour improvements | Yet to implement | Medium | Very High | Simulation/authoring | No social group files found | Prior SkiGame NPC work should remain project adapter/inspiration. |
| Visualization Lab | Reusable matrix/graph/heatmap/coverage visualizers | Scaffolded | High | High | Shared UI infrastructure | CoverageMatrix, palette analysis, texture preview, placement heatmap concepts | No formal visualization lab/shared widget library yet. |
| Visualization Lab | 3D point cloud previews | Yet to implement | Low | High | Advanced visualization | No point cloud preview found | Future visualization idea. |
| Visualization Lab | Radar graphs | Yet to implement | Low | Medium | UI/visualization | No radar graph files found | Could serve UI Feedback Lab. |
| Core / Architecture | Design Validation Audit window | Fully implemented | Critical | Medium | Architecture QA gate | PungentUtilityDesignAudit.cs, PungentUtilityDesignAuditWindow.cs | The audit window scans scripts, registry entries, menu taxonomy, namespaces, asmdefs, CreateAssetMenu roots, layout signals, performance markers, and bundled docs. Future work is configurable rules, user-linked documentation, and project adapter audit profiles. |
| Core / Architecture | Design Audit user-linkable documentation toolbar | Scaffolded | High | Medium | Project-specific documentation integration | PungentUtilityDesignAuditWindow.cs has fixed Open Design Bible/Open Inventory buttons only | Requested Design Validation Audit update: replace fixed docs buttons with Link Documentation and allow flexible user/project documentation buttons, preferably in a horizontally scrolling toolbar. |
| Core / Architecture | Bundled architecture/inventory documentation inside package | Partially implemented | High | Low | Documentation/release support | Documentation/PungentFunk_Utilities_Architecture_Design_Bible_FULL_DETAIL_CURRENT.pdf, Documentation/PungentFunk_Utilities_Audit_and_Feature_Inventory.md, Documentation/PungentFunk_Utilities_Feature_Inventory.csv | Documentation is bundled, but the docs currently inside the zip are stale: the bundled inventory still reports 94 scripts, 0 namespaces, and 0 asmdefs. Replace them with this updated audit. |
| Core / Architecture | Runtime/editor asmdef separation | Partially implemented | Critical | Medium | Export hardening | PungentFunk.Utilities.Runtime.asmdef and Editor/PungentFunk.Utilities.Editor.asmdef | The first bundle-level split exists. Runtime code still includes a guarded UnityEditor reference in ModularPathSpawner, and per-lab/bridge asmdefs remain future work. |
| Core / Architecture | Registry related-utility links | Fully implemented | High | Low | Cross-lab discoverability | PungentUtilityDescriptor.RelatedUtilityIds, PungentUtilityRegistry.RelatedUtilities, Control Panel related tool buttons | Registry supports and displays related utilities. It does not yet imply optional bridge/package dependency management. |
| Core / Architecture | Cached editor-window type resolution | Fully implemented | High | Medium | Launcher stability/performance | PungentUtilityDescriptor.cs | Descriptor caches resolved window types and preserves OpenDirect compatibility. |
| Asset Placement Lab | Asset Placement Lab hub/window | Partially implemented | Critical | High | Placement lab core | Editor/Asset Placement Lab/AssetPlacementLabWindow.cs | Registered as In Progress and expanded to a 666-line lab hub; still missing planned surface brush/socket graph/cluster/drop/connector/bounds-fill modules. |
| Colour Lab | Colour palette asset | Fully implemented | High | Medium | Package architecture | Palette Designer/PungentColourPaletteSO.cs | Now uses namespace PungentFunk.Utilities.Colour and standard CreateAssetMenu root PungentFunk Utilities/Colour/Palette. |
| Environment Simulation Lab | Environment Simulation Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Save & Settings Lab | Save & Settings Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Action / Ability Authoring Lab | Action / Ability Authoring Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Agent Simulation Lab | Agent Simulation Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Visualization Lab | Visualization Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |