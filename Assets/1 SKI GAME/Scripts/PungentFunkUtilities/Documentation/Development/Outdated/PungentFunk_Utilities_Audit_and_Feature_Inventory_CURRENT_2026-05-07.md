# PungentFunk Utilities - Current Audit and Desired Feature Inventory

Scope: extracted and audited the latest uploaded `PungentFunkUtilities.zip`. The zip contents are treated as the source of truth for implementation status. The previously uploaded feature inventory, snapshot audit PDF, architecture/design bible, bundled package documentation, and project conversation backlog were used as target/backlog references.

## Current audit summary
| Metric | Current archive result |
| --- | --- |
| C# scripts | 181 |
| Editor scripts | 139 |
| Runtime/shared scripts | 42 |
| Total C# lines | 48,119 |
| Files with namespace declarations | 181 / 181 |
| Assembly definition files | 2 |
| MenuItem declarations | 70 |
| CreateAssetMenu declarations | 12 |
| EditorWindow classes | 38 |
| Central registry entries | 27 |

## Main deltas since the previous documents
- The previous feature inventory reported 97 C# scripts; the current archive contains **181 C# scripts**.
- The prior snapshot PDF reported 123 C# scripts and 122 namespaced files; the current archive contains **181/181 namespaced C# scripts**.
- The package has expanded heavily in editor-side architecture: Developer Mode, developer theme-preset authoring, minimized utility tabs/popup, documentation links, Notes & Roadmap, token database/bindings, Palette Designer panels, Audio Coverage scanners, surface brush, placement group repair, and additional menu/category systems.
- Runtime/editor separation improved: bundle Runtime/Editor asmdefs remain present and the current text scan found **0 runtime/shared scripts with direct UnityEditor references**.
- The older bundled docs inside `Documentation/Development` are now stale relative to this archive and should be replaced with these generated outputs.

## Resolved or improved blockers
- **Namespace hardening is complete:** all 181 C# scripts declare namespaces.
- **Runtime/editor asmdef separation remains active:** two bundle-level asmdefs are present.
- **CreateAssetMenu root drift remains resolved:** all 12 CreateAssetMenu paths use the `PungentFunk Utilities/...` root.
- **Debug Control monolith is substantially resolved:** Debug Control Center now has dedicated action model/scheduler/component/discovery/reflection/router/state files.
- **Scan infrastructure exists:** reusable `PungentScan*` cache/result/session/severity/summary/GUI models are present.
- **Developer Mode and theme preset source authoring are implemented:** developer-only source writing is gated behind `PUNGENTFUNK_INTERNAL_DEVTOOLS`.
- **Design Audit documentation links are implemented:** project-local doc links can be managed and opened from the audit window.
- **Minimized utilities workflow exists:** code now supports a minimized utilities popup and optional bottom-left tab strip.
- **Palette Designer and Audio Coverage have begun modular split passes.**

## Remaining architecture blockers / risks
- **Per-lab and optional bridge asmdefs are still missing.** Current asmdefs support a bundle-style package, not independently releasable labs.
- **Package manifests / per-lab release metadata are still absent.**
- **TMP integration is guarded but not automatically wired.** `FontPreviewWindow.cs` uses `#if TMP_PRESENT`; add asmdef versionDefines or a bridge so the symbol is managed consistently.
- **Large windows remain.** The worst current files are Theme Customizer, generated preset library, Theme, Palette Designer, Terrain Usage Scanner, Audio Coverage Window, Notes & Roadmap, Modular Path Spawner, Minimized Utility Tray, Control Panel, Design Audit, and Asset Placement Lab.
- **Shared scan infrastructure is not fully adopted.** Audio has begun migration, but scanner windows still own a lot of bespoke scan/cache/result logic.
- **Primary menu root changed to `Tools/Pungent`, but legacy aliases and old roots still exist.** Curate before release.
- **Minimized tab strip and global editor skin bridge need in-Unity visual QA across docked/floating windows, editor layouts, and Unity versions.
- **Generated documentation is now behind the code.** Replace bundled inventory/snapshot docs with this audit set.

## Inventory status counts
| Status | Count |
| --- | --- |
| Fully implemented | 66 |
| Partially implemented | 79 |
| Scaffolded | 19 |
| Yet to implement | 77 |

## Inventory rows by lab
| Area / Lab | Rows |
| --- | --- |
| Action / Ability Authoring Lab | 4 |
| Agent Simulation Lab | 4 |
| Appearance Lab | 3 |
| Asset Placement Lab | 33 |
| Asset Preview & Export Lab | 6 |
| Audio Lab | 16 |
| Colour Lab | 38 |
| Combat Move Resolver Lab | 1 |
| Content Generation Lab | 5 |
| Core / Architecture | 52 |
| Debug Lab | 8 |
| Environment Simulation Lab | 11 |
| Project Audit & Authoring Lab | 21 |
| Save & Settings Lab | 6 |
| Scene Workflow Lab | 10 |
| Texture Lab | 7 |
| UI & Feedback Lab | 12 |
| Visualization Lab | 4 |

## Lab script snapshot
| Lab | Files | Editor | Runtime/shared | Lines |
| --- | --- | --- | --- | --- |
| Core / Architecture | 46 | 46 | 0 | 14,120 |
| Debug Lab | 12 | 9 | 3 | 3,863 |
| Asset Placement Lab | 18 | 9 | 9 | 2,698 |
| Scene Workflow Lab | 12 | 9 | 3 | 4,469 |
| Colour Lab | 21 | 12 | 9 | 3,916 |
| Texture Lab | 5 | 3 | 2 | 1,924 |
| Audio Lab | 23 | 9 | 14 | 4,917 |
| Asset Preview & Export Lab | 4 | 4 | 0 | 1,498 |
| Project Audit & Authoring Lab | 35 | 35 | 0 | 8,986 |
| Content Generation Lab | 4 | 2 | 2 | 1,175 |
| UI & Feedback Lab | 1 | 1 | 0 | 553 |

## Namespace distribution
| Namespace | File count |
| --- | --- |
| PungentFunk.Utilities.Editor.ProjectAudit | 35 |
| PungentFunk.Utilities.Editor.Core | 18 |
| PungentFunk.Utilities.Editor.Theme | 15 |
| PungentFunk.Utilities.Audio | 14 |
| PungentFunk.Utilities.Editor.Colour | 12 |
| PungentFunk.Utilities.Placement | 9 |
| PungentFunk.Utilities.Editor.Placement | 9 |
| PungentFunk.Utilities.Editor.Audio | 9 |
| PungentFunk.Utilities.Editor.Debugging | 9 |
| PungentFunk.Utilities.Editor.SceneTools | 9 |
| PungentFunk.Utilities.Colour | 9 |
| PungentFunk.Utilities.Editor.Scanning | 8 |
| PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring | 4 |
| PungentFunk.Utilities.Editor.PreviewExport | 4 |
| PungentFunk.Utilities.Debugging | 3 |
| PungentFunk.Utilities.Editor.Generation | 3 |
| PungentFunk.Utilities.SceneTools | 3 |
| PungentFunk.Utilities.Editor.Content | 2 |
| PungentFunk.Utilities.Generation | 2 |
| PungentFunk.Utilities.Content | 2 |
| PungentFunk.Utilities.Editor.Developer | 1 |
| PungentFunk.Utilities.Editor.UI | 1 |

## Assembly definition audit
| Path | Name | References | Include platforms | Version defines |
| --- | --- | --- | --- | --- |
| Editor/PungentFunk.Utilities.Editor.asmdef | PungentFunk.Utilities.Editor | PungentFunk.Utilities.Runtime | Editor | 0 |
| PungentFunk.Utilities.Runtime.asmdef | PungentFunk.Utilities.Runtime | - | All | 0 |

## Central registry audit
| Id | Display name | Category | Module | Status | Window |
| --- | --- | --- | --- | --- | --- |
| theme-customizer | Utility Window Theme | Core | Appearance | Stable | UtilityWindowThemeCustomizer |
| design-validation-audit | Design Validation Audit | Core | Internal | InProgress | PungentUtilityDesignAuditWindow |
| utility-tray | Minimized Utilities | Core | Workflow | Experimental | PungentUtilityTrayWindow |
| debug-control | Debug Control Center | Debug | Debugging | Stable | DebugControlWindow |
| asset-placement-lab | Asset Placement Lab | Creation | Asset Placement | InProgress | AssetPlacementLabWindow |
| surface-align | Surface Align Tool | SceneTools | Placement Assist | Stable | SurfaceAlignToolWindow |
| modular-path-builder | Modular Path Builder | SceneTools | Modular Paths | Experimental | ModularPathWindow |
| scene-navigation | Scene Navigation | SceneTools | Navigation | Stable | PungentSceneNavigationWindow |
| scene-gizmo-browser | Scene Gizmo Browser | SceneTools | Gizmos | Experimental | PungentGizmoBrowserWindow |
| path-authoring-toolkit | Path Authoring Toolkit | SceneTools | Path Authoring | Experimental | PungentPathAuthoringToolkitWindow |
| palette-designer | Palette Designer | Creation | Colour | Stable | PaletteDesignerWindow |
| procedural-texture-lab | Procedural Texture Lab | Creation | Procedural Textures | Experimental | ProceduralTextureLabWindow |
| texture-array-baker | Texture Array Baker | Assets | Texture Assets | Stable | TextureArrayBakerWindow |
| audio-setup-coverage | Audio Setup Coverage | Audio | Setup Coverage | Experimental | AudioCoverageContextWindow |
| audio-catalog-coverage | Audio Catalog Coverage | Audio | Catalog Coverage | Experimental | AudioCoverageWindow |
| font-preview | Font Preview | Assets | Font Assets | Experimental | FontPreviewWindow |
| prefab-icon-generator | Prefab Icon Generator | Assets | Prefab Assets | Stable | PrefabIconGeneratorWindow |
| prefab-asset-exporter | Prefab Asset Exporter | Assets | Prefab Assets | Stable | PrefabAssetExporterWindow |
| component-tuning-copy | Component Tuning Copy | Audit | Component Tools | Stable | ComponentTuningCopyWindow |
| reference-assignment-scanner | Reference Assignment Scanner | Audit | Reference Repair | Experimental | ReferenceAssignmentScannerWindow |
| terrain-usage-scanner | Terrain Usage Scanner | Audit | Terrain Assets | Experimental | TerrainUsageScannerWindow |
| bulk-rename | Bulk Rename | Audit | Batch Authoring | Stable | BulkRenameWindow |
| tooltip-notes | Notes & Roadmap | Audit | Notes | Experimental | PungentNotesRoadmapWindow |
| coverage-matrix | Coverage Matrix | Audit | Coverage | Experimental | PungentCoverageMatrixWindow |
| token-validator | Token Validator | Audit | Text Validation | Experimental | PungentTokenValidatorWindow |
| input-prompt-icon-library | Input Prompt Icon Library Populator | UI | Input Prompts | Experimental | InputPromptIconLibraryAutoFill |
| name-generator | Name Generator | Creation | Names | Stable | NameGeneratorWindow |

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

## Menu access audit
| Menu root/category | Count |
| --- | --- |
| Assets/PungentFunk Utilities | 12 |
| GameObject/PungentFunk Utilities | 22 |
| Tools/PungentFunk | 36 |

### Duplicate MenuItem path declarations
These are often command/validation method pairs, but they should remain intentional and clean.
| Menu path | Declaration count | Script(s) |
| --- | --- | --- |
| Assets/PungentFunk Utilities/Create Placement Asset Set From Selected Prefabs | 2 | Editor/Asset Placement Lab/PungentAssetPlacementAccessMenus.cs |
| GameObject/PungentFunk Utilities/Add Placement Socket | 2 | Editor/Asset Placement Lab/PungentAssetPlacementAccessMenus.cs |
| GameObject/PungentFunk Utilities/Add Scene Gizmo Source | 2 | Editor/Utilities Core/PungentUtilityAccessMenus.cs |
| GameObject/PungentFunk Utilities/Fast Travel SceneView To Selection | 2 | Editor/PungentSceneNavigationWindow.cs |
| GameObject/PungentFunk Utilities/Open Component Tuning Copy | 2 | Editor/Utilities Core/PungentUtilityAccessMenus.cs |
| GameObject/PungentFunk Utilities/Open Modular Path Builder | 2 | Editor/Utilities Core/PungentUtilityAccessMenus.cs |
| GameObject/PungentFunk Utilities/Open Reference Assignment Scanner | 2 | Editor/Utilities Core/PungentUtilityAccessMenus.cs |
| GameObject/PungentFunk Utilities/Open Scene Navigation | 2 | Editor/Utilities Core/PungentUtilityAccessMenus.cs |
| GameObject/PungentFunk Utilities/Open Surface Align Tool | 2 | Editor/Utilities Core/PungentUtilityAccessMenus.cs |
| GameObject/PungentFunk Utilities/Open Tooltip Notes | 2 | Editor/Utilities Core/PungentUtilityAccessMenus.cs |
| Tools/PungentFunk/Asset Preview and Export/Save Selected As Prefab With Mesh Assets | 2 | Editor/Utilities Core/PungentUtilityToolMenus.cs |
| Tools/PungentFunk/UI and Feedback/Input Prompts/Populate Selected Input Prompt Library | 2 | Editor/Utilities Core/PungentUtilityToolMenus.cs |

## Scan/repaint/performance hotspot audit
| Pattern | Total occurrences | Files | Examples |
| --- | --- | --- | --- |
| SceneView.RepaintAll | 2 | 2 | Editor/Utilities Core/PungentEditorPerformanceUtility.cs (1); Editor/Utilities Core/PungentUtilityDesignAudit.cs (1) |
| Resources.FindObjectsOfTypeAll | 8 | 6 | Editor/Debug Control Window/DebugControlDiscoveryService.cs (1); Editor/ReferenceAssignmentScannerWindow.cs (2); Editor/Scene Gizmos/PungentGizmoBrowserWindow.cs (1); Editor/Utilities Core/Minimize Utilities/PungentUtilityTrayWindow.cs (2); Editor/Utilities Core/PungentUtilityDesignAudit.cs (1); Editor/Utilities Core/Window Themes/UtilityWindowTheme.cs (1) |
| AssetDatabase.FindAssets | 27 | 14 | Editor/Audio Coverage/AudioCoverageCatalogScanner.cs (1); Editor/Audio Coverage/AudioCoverageContextScanner.cs (2); Editor/Audio Coverage/AudioCoverageWindow.cs (1); Editor/Audio Coverage/AudioInteractionMatrixEditor.cs (1); Editor/FontPreviewWindow.cs (2); Editor/InputPromptIconLibraryAutoFill.cs (4); Editor/Name Generator/NameGeneratorWindow.cs (2); Editor/PungentCoverageMatrixWindow.cs (1); Editor/PungentTokenValidatorWindow.cs (1); Editor/ReferenceAssignmentScannerWindow.cs (1); Editor/TerrainUsageScannerWindow.cs (2); Editor/Utilities Core/PungentUtilityDesignAudit.cs (6); ... |
| Thread.Sleep | 1 | 1 | Editor/PrefabIconGeneratorWindow.cs (1) |
| InitializeOnLoad | 10 | 10 | Editor/Asset Placement Lab/PungentAssetPlacementUtilitiesRegistrar.cs (1); Editor/Modular Paths/ModularPathSpawnerEditorUtility.cs (1); Editor/Project Audit/Notes/PungentNoteContextMenus.cs (1); Editor/Project Audit/Notes/PungentNoteInspectorIntegration.cs (1); Editor/Project Audit/Notes/PungentNoteSceneOverlay.cs (1); Editor/Project Audit/Tokens/PungentTokenContextMenus.cs (1); Editor/PungentTooltipNotesBrowserWindow.cs (1); Editor/Utilities Core/Minimize Utilities/PungentUtilityTrayWindow.cs (1); Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorGuiStyleBridge.cs (1); Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorSkinBridge.cs (1) |
| TMP/TextMeshPro | 12 | 2 | Editor/FontPreviewWindow.cs (10); Editor/Palette Designer/PaletteApplyUtility.cs (2) |

## Largest scripts requiring split-first attention
| Script | Lines | Recommended action |
| --- | --- | --- |
| Editor/Utilities Core/Window Themes/UtilityWindowThemeCustomizer.cs | 2454 | Split/continue modularising |
| Editor/Utilities Core/Window Themes/UtilityThemePresetLibrary.Generated.cs | 1852 | Split/continue modularising |
| Editor/Utilities Core/Window Themes/UtilityWindowTheme.cs | 1139 | Split/continue modularising |
| Editor/Palette Designer/PaletteDesignerWindow.cs | 1110 | Split/continue modularising |
| Editor/TerrainUsageScannerWindow.cs | 1098 | Split/continue modularising |
| Editor/Audio Coverage/AudioCoverageWindow.cs | 1088 | Split/continue modularising |
| Editor/Project Audit/Notes/PungentNotesRoadmapWindow.cs | 1087 | Split/continue modularising |
| Editor/ReferenceAssignmentScannerWindow.cs | 893 | Monitor |
| Modular Paths/ModularPathSpawner.cs | 873 | Monitor |
| Editor/Utilities Core/Minimize Utilities/PungentUtilityTrayWindow.cs | 843 | Monitor |
| Editor/Utilities Core/PungentUtilityControlPanelWindow.cs | 778 | Monitor |
| Editor/Utilities Core/PungentUtilityDesignAudit.cs | 764 | Monitor |
| Editor/Asset Placement Lab/AssetPlacementLabWindow.cs | 756 | Monitor |
| Editor/Name Generator/NameGeneratorWindow.cs | 737 | Monitor |
| Editor/FontPreviewWindow.cs | 693 | Monitor |
| Editor/Debug Control Window/DebugControlActionScheduler.cs | 686 | Monitor |
| Editor/Modular Paths/ModularPathWindow.cs | 669 | Monitor |
| Editor/BulkRenameWindow.cs | 658 | Monitor |
| Debug/DebugRouter.cs | 630 | Monitor |
| Editor/Debug Control Window/DebugControlComponentPanel.cs | 616 | Monitor |
| Editor/PungentSceneNavigationWindow.cs | 615 | Monitor |
| Editor/PungentTooltipNotesBrowserWindow.cs | 613 | Monitor |
| Generation/ProceduralTextureGenerator.cs | 603 | Monitor |
| Editor/ComponentTuningCopyWindow.cs | 577 | Monitor |
| Editor/InputPromptIconLibraryAutoFill.cs | 553 | Monitor |

## New or materially refactored areas since the last feature inventory
- Developer Mode and theme preset source authoring under `Editor/Developer`.
- Minimized Utilities popup/bottom-left tab strip under `Editor/Utilities Core/Minimize Utilities`.
- Documentation-link storage and editor popup for Design Validation Audit.
- Expanded Tool Menu and Menu Path centralization under `PungentUtilityToolMenus` and `PungentUtilityMenuPaths`.
- Project Audit Notes & Roadmap system with persistent note database, context menus, inspector integration, import/backlog seed helpers, audit issue bridge, scene/surface hooks, and future utility records.
- Project Audit Token system with persistent token database, bindings, parser, validator service, context menus, link popup, and GUI helpers.
- Palette Designer panel split across toolbar, swatch grid, generation, selected swatch, analysis, apply, and GUI utility files.
- Audio Coverage service/model split into catalog scanner, context scanner, and scan models.
- Asset Placement additions: surface brush controller, group repair utility, and path integration utility.
- Modular Path editor hooks/utility split from the runtime spawner.
- Experimental global editor skin/text-role mapping additions for USS and GUIStyle styling.

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
| Asset Placement Lab | Asset Placement Lab/PungentPlacementTypes.cs | 196 | PungentFunk.Utilities.Placement | PungentPlacementScatterPattern, PungentPlacementAreaMode, PungentPlacementSurfaceAlignment, PungentPlacementCandidateState, PungentPlacementRejectionReason, PungentPlacementSurfaceCategory |
| Audio Lab | Audio Coverage/AudioClipSetSO.cs | 152 | PungentFunk.Utilities.Audio | AudioClipEntry, AudioClipSetSO |
| Audio Lab | Audio Coverage/AudioCoverageProfileSO.cs | 502 | PungentFunk.Utilities.Audio | AudioCoverageProfileSO, AudioCueBindingRule, AudioComponentExpectationRule, AudioRequiredFieldRule, AudioBindingCondition, AudioBindingSourceKind |
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
| Debug Lab | Debug/DebugChannels.cs | 69 | PungentFunk.Utilities.Debugging | DebugChannels, DebugSignals |
| Debug Lab | Debug/DebugRouter.cs | 630 | PungentFunk.Utilities.Debugging | DebugRouter, Level, ChannelSnapshot, SourceSnapshot, StateSnapshot, SignalSnapshot |
| Debug Lab | Debug/DebugSignalRelay.cs | 90 | PungentFunk.Utilities.Debugging | DebugSignalRelay, StringEvent, SignalRule |
| Asset Placement Lab | Editor/Asset Placement Lab/AssetPlacementLabWindow.cs | 756 | PungentFunk.Utilities.Editor.Placement | AssetPlacementLabWindow, LabModule |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentAssetPlacementAccessMenus.cs | 41 | PungentFunk.Utilities.Editor.Placement | PungentAssetPlacementAccessMenus |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentAssetPlacementUtilitiesRegistrar.cs | 35 | PungentFunk.Utilities.Editor.Placement | PungentAssetPlacementUtilitiesRegistrar |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementApplyUtility.cs | 137 | PungentFunk.Utilities.Editor.Placement | PungentPlacementApplyUtility |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementAssetSetBuilder.cs | 90 | PungentFunk.Utilities.Editor.Placement | PungentPlacementAssetSetBuilder |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementGroupRepairUtility.cs | 166 | PungentFunk.Utilities.Editor.Placement | PungentPlacementGroupRepairUtility |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementPathIntegrationUtility.cs | 95 | PungentFunk.Utilities.Editor.Placement | PungentPlacementPathIntegrationUtility |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementSocketValidator.cs | 86 | PungentFunk.Utilities.Editor.Placement | PungentPlacementSocketValidator, ValidationMessage |
| Asset Placement Lab | Editor/Asset Placement Lab/PungentPlacementSurfaceBrushController.cs | 229 | PungentFunk.Utilities.Editor.Placement | PungentPlacementSurfaceBrushController |
| Audio Lab | Editor/Audio Coverage/AudioCoverageCatalogScanner.cs | 475 | PungentFunk.Utilities.Editor.Audio | AudioCoverageCatalogScanner |
| Audio Lab | Editor/Audio Coverage/AudioCoverageContextScanner.cs | 488 | PungentFunk.Utilities.Editor.Audio | AudioCoverageContextScanner |
| Audio Lab | Editor/Audio Coverage/AudioCoverageContextWindow.cs | 304 | PungentFunk.Utilities.Editor.Audio | AudioCoverageContextWindow |
| Audio Lab | Editor/Audio Coverage/AudioCoverageProfileSOEditor.cs | 58 | PungentFunk.Utilities.Editor.Audio | AudioCoverageProfileSOEditor |
| Audio Lab | Editor/Audio Coverage/AudioCoverageScanModels.cs | 83 | PungentFunk.Utilities.Editor.Audio | AudioCoverageCueStatus, AudioCoverageResultSeverity, AudioCoverageCueAuditRow, AudioCoverageCatalogEntryInfo, AudioCoverageScriptReferenceHit, AudioCoverageContextResult |
| Audio Lab | Editor/Audio Coverage/AudioCoverageWindow.cs | 1088 | PungentFunk.Utilities.Editor.Audio | AudioCoverageWindow, WindowTab |
| Audio Lab | Editor/Audio Coverage/AudioInteractionMatrixEditor.cs | 400 | PungentFunk.Utilities.Editor.Audio | AudioInteractionMatrixEditor, MissingPair |
| Audio Lab | Editor/Audio Coverage/AudioMaterialTagEditor.cs | 161 | PungentFunk.Utilities.Editor.Audio | AudioMaterialTagEditor |
| Audio Lab | Editor/Audio Coverage/TerrainAudioProfileEditor.cs | 176 | PungentFunk.Utilities.Editor.Audio | TerrainAudioProfileEditor, ProfileDiagnostics |
| Project Audit & Authoring Lab | Editor/BulkRenameWindow.cs | 658 | PungentFunk.Utilities.Editor.ProjectAudit | BulkRenameWindow, RenameMode, RenamePreview |
| Project Audit & Authoring Lab | Editor/ComponentTuningCopyWindow.cs | 577 | PungentFunk.Utilities.Editor.ProjectAudit | ComponentTuningCopyWindow, ComponentSelection, CopyStats |
| Debug Lab | Editor/Debug Control Window/DebugControlActionModel.cs | 74 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow, ScheduledCall, ScheduledCallSaveData, ScheduledCallSave |
| Debug Lab | Editor/Debug Control Window/DebugControlActionScheduler.cs | 686 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow |
| Debug Lab | Editor/Debug Control Window/DebugControlComponentPanel.cs | 616 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow |
| Debug Lab | Editor/Debug Control Window/DebugControlDiscoveryService.cs | 122 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow |
| Debug Lab | Editor/Debug Control Window/DebugControlFieldModel.cs | 39 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow, DebugComponentInfo, StaticDebugFieldInfo |
| Debug Lab | Editor/Debug Control Window/DebugControlReflectionUtility.cs | 496 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow |
| Debug Lab | Editor/Debug Control Window/DebugControlRouterPanel.cs | 414 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow |
| Debug Lab | Editor/Debug Control Window/DebugControlWindow.cs | 424 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow, GuiBackgroundScope |
| Debug Lab | Editor/Debug Control Window/DebugControlWindowState.cs | 203 | PungentFunk.Utilities.Editor.Debugging | DebugControlWindow, GroupMode, ConditionMode, BoolToggleCategory, ToggleStats |
| Core / Architecture | Editor/Developer/PungentDeveloperMode.cs | 43 | PungentFunk.Utilities.Editor.Developer | PungentDeveloperMode |
| Core / Architecture | Editor/Developer/Theme Preset Authoring/UtilityThemePresetDraftStore.cs | 40 | PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring | UtilityThemePresetDraftStore, DraftHeader |
| Core / Architecture | Editor/Developer/Theme Preset Authoring/UtilityThemePresetSnapshot.cs | 89 | PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring | UtilityThemePresetSnapshot |
| Core / Architecture | Editor/Developer/Theme Preset Authoring/UtilityThemePresetSourceGenerator.cs | 133 | PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring | UtilityThemePresetSourceGenerator |
| Core / Architecture | Editor/Developer/Theme Preset Authoring/UtilityThemePresetSourceWriter.cs | 140 | PungentFunk.Utilities.Editor.Developer.ThemePresetAuthoring | UtilityThemePresetSourceWriter |
| Core / Architecture | Editor/Developer/Theme Preset Authoring/UtilityWindowThemeCustomizer.PresetAuthoring.cs | 133 | PungentFunk.Utilities.Editor.Theme | UtilityWindowThemeCustomizer |
| Asset Preview & Export Lab | Editor/FontPreviewWindow.cs | 693 | PungentFunk.Utilities.Editor.PreviewExport | FontPreviewWindow, FontEntry |
| UI & Feedback Lab | Editor/InputPromptIconLibraryAutoFill.cs | 553 | PungentFunk.Utilities.Editor.UI | InputPromptIconLibraryAutoFill, EntryData, PopulateReport, PopulateOptions |
| Scene Workflow Lab | Editor/Modular Paths/ModularPathSpawnerEditor.cs | 316 | PungentFunk.Utilities.Editor.SceneTools | ModularPathSpawnerEditor |
| Scene Workflow Lab | Editor/Modular Paths/ModularPathSpawnerEditorUtility.cs | 125 | PungentFunk.Utilities.Editor.SceneTools | ModularPathSpawnerEditorUtility |
| Scene Workflow Lab | Editor/Modular Paths/ModularPathWindow.cs | 669 | PungentFunk.Utilities.Editor.SceneTools | ModularPathWindow, ModularPathBuilderVectorExtensions |
| Content Generation Lab | Editor/Name Generator/NameGeneratorWindow.cs | 737 | PungentFunk.Utilities.Editor.Content | NameGeneratorWindow, GenerationMode, TokenBinding |
| Content Generation Lab | Editor/Name Generator/NameListEditor.cs | 123 | PungentFunk.Utilities.Editor.Content | NameListEditor, MadLibNameListEditor |
| Colour Lab | Editor/Palette Designer/PaletteApplyUtility.cs | 280 | PungentFunk.Utilities.Editor.Colour | PaletteApplyTargetKind, PaletteApplyTarget, PaletteApplyUtility |
| Colour Lab | Editor/Palette Designer/PaletteDesignerAnalysisPanel.cs | 200 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerApplyPanel.cs | 121 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerGUIUtility.cs | 59 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerGenerationPanel.cs | 235 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerPanels.cs | 9 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerSelectedSwatchPanel.cs | 88 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerSwatchGridPanel.cs | 319 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerToolbar.cs | 149 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PaletteDesignerWindow.cs | 1110 | PungentFunk.Utilities.Editor.Colour | PaletteDesignerWindow |
| Colour Lab | Editor/Palette Designer/PungentColourPaletteSOEditor.cs | 33 | PungentFunk.Utilities.Editor.Colour | PungentColourPaletteSOEditor |
| Colour Lab | Editor/Palette Designer/PungentPaletteStorageUtility.cs | 160 | PungentFunk.Utilities.Editor.Colour | PungentPaletteStorageUtility |
| Asset Preview & Export Lab | Editor/Prefab Exporter/PrefabAssetExporter.cs | 289 | PungentFunk.Utilities.Editor.PreviewExport | PrefabAssetExporter, ExportOptions, ExportReport |
| Asset Preview & Export Lab | Editor/Prefab Exporter/PrefabAssetExporterWindow.cs | 114 | PungentFunk.Utilities.Editor.PreviewExport | PrefabAssetExporterWindow |
| Asset Preview & Export Lab | Editor/PrefabIconGeneratorWindow.cs | 402 | PungentFunk.Utilities.Editor.PreviewExport | PrefabIconGeneratorWindow, PrefabIconGeneratorService, IconJob |
| Texture Lab | Editor/Procedural Texture Lab/ProceduralTextureLabWindow.cs | 433 | PungentFunk.Utilities.Editor.Generation | ProceduralTextureLabWindow |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteAuditIssueBridge.cs | 160 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteAuditIssueBridge |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteBacklogSeedModels.cs | 42 | PungentFunk.Utilities.Editor.ProjectAudit | PungentBacklogSeedItem, PungentBacklogSeedPreviewItem, PungentBacklogSeedApplyResult |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteBacklogSeeder.cs | 262 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteBacklogSeeder |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteBulkActions.cs | 84 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteBulkActions |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteContextMenus.cs | 109 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteContextMenus |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteContextResolver.cs | 209 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteContextResolver |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteDatabase.cs | 129 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteDatabase |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteDisplaySettings.cs | 42 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteDisplaySettingsService |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteFilters.cs | 204 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNotesSavedView, PungentNotesSortMode, PungentNotesGroupMode, PungentNoteFilterState, PungentNoteFilters |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteGUI.cs | 138 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteGUI |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteImportSources.cs | 98 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteImportSources |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteInspectorIntegration.cs | 78 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteInspectorIntegration |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteInventoryImporter.cs | 206 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteInventoryImporter |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteModels.cs | 176 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteKind, PungentNoteStatus, PungentNotePriority, PungentNoteVisibility, PungentNoteTargetType, PungentNoteSurfaceMode |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteSceneOverlay.cs | 96 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteSceneOverlay |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteStorage.cs | 263 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteStorage |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNoteSurfaceGUI.cs | 39 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNoteSurfaceGUI |
| Project Audit & Authoring Lab | Editor/Project Audit/Notes/PungentNotesRoadmapWindow.cs | 1087 | PungentFunk.Utilities.Editor.ProjectAudit | PungentNotesRoadmapWindow |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenContextMenus.cs | 44 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenContextMenus |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenContextResolver.cs | 85 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenContextResolver |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenDatabase.cs | 188 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenDatabase |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenFilters.cs | 64 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenQuickFilter, PungentTokenFilters |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenGUI.cs | 50 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenGUI |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenLinkPopup.cs | 92 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenLinkPopup |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenModels.cs | 97 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenTargetType, PungentTokenUsageStatus, PungentTokenDefinition, PungentTokenBinding, PungentTokenUsage |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenParser.cs | 70 | PungentFunk.Utilities.Editor.ProjectAudit | PungentParsedToken, PungentTokenParser |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenStorage.cs | 40 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenStorage |
| Project Audit & Authoring Lab | Editor/Project Audit/Tokens/PungentTokenValidatorService.cs | 153 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenValidatorService |
| Project Audit & Authoring Lab | Editor/PungentCoverageMatrixWindow.cs | 412 | PungentFunk.Utilities.Editor.ProjectAudit | PungentCoverageMatrixWindow, CoverageStatus, Template, CoverageEntry, MarkStats |
| Scene Workflow Lab | Editor/PungentPathAuthoringToolkitWindow.cs | 430 | PungentFunk.Utilities.Editor.SceneTools | PungentPathAuthoringToolkitWindow, EditMode, PungentPathAuthoringToolkit |
| Scene Workflow Lab | Editor/PungentSceneHelpOverlay.cs | 29 | PungentFunk.Utilities.Editor.SceneTools | PungentSceneHelpOverlay |
| Scene Workflow Lab | Editor/PungentSceneNavigationWindow.cs | 615 | PungentFunk.Utilities.Editor.SceneTools | PungentSceneNavigationWindow, TrackingOperator, TrackingMode, Waypoint, WaypointStore, TrackingRule |
| Project Audit & Authoring Lab | Editor/PungentTokenValidatorWindow.cs | 430 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTokenValidatorWindow |
| Project Audit & Authoring Lab | Editor/PungentTooltipNotesBrowserWindow.cs | 613 | PungentFunk.Utilities.Editor.ProjectAudit | PungentTooltipNoteScope, PungentTooltipNoteVisibility, PungentTooltipNotePriority, PungentTooltipNote, PungentTooltipNoteDatabase, PungentTooltipNoteMenuHooks |
| Project Audit & Authoring Lab | Editor/ReferenceAssignmentScannerWindow.cs | 893 | PungentFunk.Utilities.Editor.ProjectAudit | ReferenceAssignmentScannerWindow, ScanMode, Candidate, SuggestionRow |
| Scene Workflow Lab | Editor/Scene Gizmos/PungentGizmoBrowserWindow.cs | 183 | PungentFunk.Utilities.Editor.SceneTools | PungentGizmoBrowserWindow |
| Scene Workflow Lab | Editor/Scene Gizmos/PungentSceneGizmoSourceEditor.cs | 215 | PungentFunk.Utilities.Editor.SceneTools | PungentSceneGizmoSourceEditor |
| Scene Workflow Lab | Editor/TerrainAlignTool.cs | 496 | PungentFunk.Utilities.Editor.SceneTools | TerrainAlignTool, SurfaceAlignToolWindow, TerrainAlignToolWindow |
| Project Audit & Authoring Lab | Editor/TerrainUsageScannerWindow.cs | 1098 | PungentFunk.Utilities.Editor.ProjectAudit | TerrainUsageScannerWindow, TerrainUseRecord |
| Texture Lab | Editor/Texture Array Baker/TextureArrayBakerUtility.cs | 254 | PungentFunk.Utilities.Editor.Generation | TextureArrayFallbackMode, TextureArrayBakeResult, TextureArrayBakerUtility |
| Texture Lab | Editor/Texture Array Baker/TextureArrayBakerWindow.cs | 459 | PungentFunk.Utilities.Editor.Generation | TextureArraySourceMode, TextureArrayBakerWindow |
| Core / Architecture | Editor/Utilities Core/DocumentationLinkEditorPopup.cs | 94 | PungentFunk.Utilities.Editor.Core | DocumentationLinkEditorPopup |
| Core / Architecture | Editor/Utilities Core/Minimize Utilities/PungentMinimizedUtilitiesButton.cs | 54 | PungentFunk.Utilities.Editor.Core | PungentMinimizedUtilitiesButton |
| Core / Architecture | Editor/Utilities Core/Minimize Utilities/PungentMinimizedUtilitiesPopupContent.cs | 161 | PungentFunk.Utilities.Editor.Core | PungentMinimizedUtilitiesPopupContent |
| Core / Architecture | Editor/Utilities Core/Minimize Utilities/PungentUtilityTrayWindow.cs | 843 | PungentFunk.Utilities.Editor.Core | PungentMinimizedUtilityEntry, PungentMinimizedUtilityEntrySet, PungentUtilityMinimizer, PungentUtilityTrayWindow |
| Core / Architecture | Editor/Utilities Core/PungentEditorPerformanceUtility.cs | 266 | PungentFunk.Utilities.Editor.Core | PungentEditorPerformanceUtility, WindowRepaintCounter, IncrementalQueue |
| Core / Architecture | Editor/Utilities Core/PungentUtilityAccessMenus.cs | 136 | PungentFunk.Utilities.Editor.Core | PungentUtilityAccessMenus |
| Core / Architecture | Editor/Utilities Core/PungentUtilityCategories.cs | 144 | PungentFunk.Utilities.Editor.Core | PungentUtilityCategories |
| Core / Architecture | Editor/Utilities Core/PungentUtilityControlPanelWindow.cs | 778 | PungentFunk.Utilities.Editor.Core | PungentUtilityControlPanelWindow, BrowserViewMode, CategoryGroupView, TypeGroupView |
| Core / Architecture | Editor/Utilities Core/PungentUtilityDescriptor.cs | 288 | PungentFunk.Utilities.Editor.Core | PungentUtilityDescriptor |
| Core / Architecture | Editor/Utilities Core/PungentUtilityDesignAudit.cs | 764 | PungentFunk.Utilities.Editor.Core | PungentUtilityDesignAudit, Severity, Issue, Report, ScriptRecord |
| Core / Architecture | Editor/Utilities Core/PungentUtilityDesignAuditWindow.cs | 435 | PungentFunk.Utilities.Editor.Core | PungentUtilityDesignAuditWindow |
| Core / Architecture | Editor/Utilities Core/PungentUtilityDocumentationLinks.cs | 124 | PungentFunk.Utilities.Editor.Core | PungentUtilityDocumentationLinks, DocumentationLink |
| Core / Architecture | Editor/Utilities Core/PungentUtilityLabs.cs | 40 | PungentFunk.Utilities.Editor.Core | PungentUtilityLabs |
| Core / Architecture | Editor/Utilities Core/PungentUtilityMenuPaths.cs | 55 | PungentFunk.Utilities.Editor.Core | PungentUtilityMenuPaths |
| Core / Architecture | Editor/Utilities Core/PungentUtilityPackageStatus.cs | 97 | PungentFunk.Utilities.Editor.Core | PungentUtilityPackageStatus |
| Core / Architecture | Editor/Utilities Core/PungentUtilityRegistry.cs | 383 | PungentFunk.Utilities.Editor.Core | PungentUtilityRegistry |
| Core / Architecture | Editor/Utilities Core/PungentUtilityReleaseReadiness.cs | 249 | PungentFunk.Utilities.Editor.Core | PungentUtilityReleaseReadiness, Priority, UpdatePass, Assessment |
| Core / Architecture | Editor/Utilities Core/PungentUtilityToolMenus.cs | 128 | PungentFunk.Utilities.Editor.Core | PungentUtilityToolMenus |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanCache.cs | 52 | PungentFunk.Utilities.Editor.Scanning | PungentScanCache |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanGUI.cs | 125 | PungentFunk.Utilities.Editor.Scanning | PungentScanGUI |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanIssue.cs | 49 | PungentFunk.Utilities.Editor.Scanning | PungentScanIssue |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanResult.cs | 99 | PungentFunk.Utilities.Editor.Scanning | PungentScanResult |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanScope.cs | 20 | PungentFunk.Utilities.Editor.Scanning | PungentScanScope |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanSession.cs | 87 | PungentFunk.Utilities.Editor.Scanning | PungentScanSession |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanSeverity.cs | 17 | PungentFunk.Utilities.Editor.Scanning | PungentScanSeverity |
| Core / Architecture | Editor/Utilities Core/Scanning/PungentScanSummary.cs | 34 | PungentFunk.Utilities.Editor.Scanning | PungentScanSummary |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorGuiStyleBridge.cs | 355 | PungentFunk.Utilities.Editor.Theme | PungentEditorGuiStyleBridge, StyleRoleMapping, StyleSnapshot, StateSnapshot |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorSkinBridge.cs | 161 | PungentFunk.Utilities.Editor.Theme | PungentEditorSkinBridge |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorSkinDiagnostics.cs | 47 | PungentFunk.Utilities.Editor.Theme | PungentEditorSkinDiagnostics |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorSkinProfile.cs | 65 | PungentFunk.Utilities.Editor.Theme | PungentEditorSkinCompatibility, PungentEditorSkinProperty, PungentEditorSkinProfile, PungentEditorStyleOverride |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorSkinSelectorMap.cs | 232 | PungentFunk.Utilities.Editor.Theme | PungentEditorSkinSelectorMap |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorSkinUssGenerator.cs | 480 | PungentFunk.Utilities.Editor.Theme | PungentEditorSkinUssGenerator |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorStyleExplorerWindow.cs | 255 | PungentFunk.Utilities.Editor.Theme | PungentEditorStyleExplorerWindow |
| Core / Architecture | Editor/Utilities Core/Window Themes/Experimental Global Skin/PungentEditorTextRoleMap.cs | 374 | PungentFunk.Utilities.Editor.Theme | PungentEditorTextTarget, PungentEditorTextRoleMap |
| Core / Architecture | Editor/Utilities Core/Window Themes/UtilityThemePresetDefinition.cs | 154 | PungentFunk.Utilities.Editor.Theme | UtilityThemePresetCategory, UtilityThemePresetTone, UtilityThemePresetColourFamily, UtilityThemePresetDefinition |
| Core / Architecture | Editor/Utilities Core/Window Themes/UtilityThemePresetLibrary.Generated.cs | 1852 | PungentFunk.Utilities.Editor.Theme | UtilityThemePresetLibrary |
| Core / Architecture | Editor/Utilities Core/Window Themes/UtilityThemePresetLibrary.cs | 413 | PungentFunk.Utilities.Editor.Theme | UtilityThemePresetLibrary |
| Core / Architecture | Editor/Utilities Core/Window Themes/UtilityWindowPrefs.cs | 39 | PungentFunk.Utilities.Editor.Theme | UtilityWindowPrefs |
| Core / Architecture | Editor/Utilities Core/Window Themes/UtilityWindowTheme.cs | 1139 | PungentFunk.Utilities.Editor.Theme | UtilityWindowTheme, ThemePreset, DeficiencyPreview, ThemeScope, TextRole, GuiBackgroundScope |
| Core / Architecture | Editor/Utilities Core/Window Themes/UtilityWindowThemeCustomizer.cs | 2454 | PungentFunk.Utilities.Editor.Theme | UtilityWindowThemeCustomizer, ThemeHarmonyMode, PresetCategoryFilter, PresetToneFilter, PresetFamilyFilter, PaletteThemeBuildMode |
| Texture Lab | Generation/ProceduralTextureGenerator.cs | 603 | PungentFunk.Utilities.Generation | ProceduralTextureSite, ProceduralTextureGenerator |
| Texture Lab | Generation/ProceduralTextureSettings.cs | 175 | PungentFunk.Utilities.Generation | ProceduralTexturePattern, ProceduralTextureStampShape, ProceduralTextureBlendMode, ProceduralTextureChannel, ProceduralTextureColorMode, ProceduralTextureGenerationSettings |
| Scene Workflow Lab | Modular Paths/ModularPathSpawner.cs | 873 | PungentFunk.Utilities.SceneTools | ModularPathSpawner, ForwardAxis, PathMode |
| Scene Workflow Lab | Modular Paths/ModularPathSpawnerEditorHooks.cs | 21 | PungentFunk.Utilities.SceneTools | ModularPathSpawnerEditorHooks |
| Content Generation Lab | Name Generator/MadLibNameList.cs | 184 | PungentFunk.Utilities.Content | MadLibNameList |
| Content Generation Lab | Name Generator/NameList.cs | 131 | PungentFunk.Utilities.Content | NameList |
| Colour Lab | Palette Designer/ColourContrastUtility.cs | 78 | PungentFunk.Utilities.Colour | ColourContrastUtility |
| Colour Lab | Palette Designer/ColourConversionUtility.cs | 200 | PungentFunk.Utilities.Colour | ColourConversionUtility, ColourModeValues, ColourValueMode |
| Colour Lab | Palette Designer/ColourDeficiencyPreviewUtility.cs | 71 | PungentFunk.Utilities.Colour | ColourDeficiencyPreviewMode, ColourDeficiencyPreviewUtility |
| Colour Lab | Palette Designer/ColourHarmonyUtility.cs | 101 | PungentFunk.Utilities.Colour | ColourHarmonyUtility |
| Colour Lab | Palette Designer/PaletteAnalysisUtility.cs | 171 | PungentFunk.Utilities.Colour | PaletteAnalysisUtility, PaletteAnalysisReport, PaletteContrastPair |
| Colour Lab | Palette Designer/PaletteGenerationSettings.cs | 81 | PungentFunk.Utilities.Colour | PaletteGenerationSettings, ColourHarmonyMode |
| Colour Lab | Palette Designer/PaletteGeneratorUtility.cs | 306 | PungentFunk.Utilities.Colour | PaletteGeneratorUtility |
| Colour Lab | Palette Designer/PaletteSwatch.cs | 62 | PungentFunk.Utilities.Colour | PaletteSwatch, PaletteSwatchRole |
| Colour Lab | Palette Designer/PungentColourPaletteSO.cs | 83 | PungentFunk.Utilities.Colour | PungentColourPaletteSO |
| Scene Workflow Lab | PungentSceneGizmoSource.cs | 497 | PungentFunk.Utilities.SceneTools | PungentSceneGizmoSource, DrawWhen, GizmoShape, PositionMode, SizeMode, ConditionMode |

## Desired utility / feature inventory
| Area / Lab | Desired utility / feature | Status | Usefulness | Difficulty | Fit / relevance | Current evidence | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Core / Architecture | Assembly definitions per runtime/editor/lab/bridge | Partially implemented | Critical | High | Export/package safety | PungentFunk.Utilities.Runtime.asmdef; Editor/PungentFunk.Utilities.Editor.asmdef | Bundle-level Runtime/Editor asmdefs exist. Per-lab asmdefs, optional bridge asmdefs, package manifests, and version-defined optional dependencies are still missing. |
| Core / Architecture | Bundled architecture/inventory documentation inside package | Partially implemented | High | Low | Documentation/release support | Documentation/Development contains active and Outdated docs; current zip includes older feature inventory files | Documentation is included, but current source has grown to 181 scripts; replace older bundled snapshot/inventory documents with this updated audit set. |
| Core / Architecture | Cached editor-window type resolution | Fully implemented | High | Medium | Launcher stability/performance | PungentUtilityDescriptor.cs | Descriptor caches resolved window types and preserves OpenDirect compatibility. |
| Core / Architecture | Category colour coding in control panel | Fully implemented | Medium | Medium | UX navigation | PungentUtilityCategories.cs; UtilityWindowTheme category tints; Control Panel category filters | Category taxonomy and tint lookup are centralized. |
| Core / Architecture | Central utility registry | Fully implemented | Critical | Medium | Central launcher/discovery | Editor/Utilities Core/PungentUtilityRegistry.cs | Central registry currently registers 27 utility windows with lab/module/status metadata and related utility IDs. Asset Placement still also has a per-lab registrar that may duplicate/override the same id. |
| Core / Architecture | Centralized menu path constants and direct tool menu owner | Fully implemented | High | Low | Menu hygiene / release polish | PungentUtilityMenuPaths.cs; PungentUtilityToolMenus.cs | Direct menu entries are now owned centrally under Tools/Pungent rather than scattered window-by-window. Legacy aliases still need final curation. |
| Core / Architecture | Control Panel / lab browser | Partially implemented | Critical | Medium | Primary suite navigation | PungentUtilityControlPanelWindow.cs, PungentUtilityCategories.cs, PungentUtilityRegistry.cs, PungentUtilityToolMenus.cs | Control Panel now has stronger category/lab organization, status metadata, related utility links, and recent/minimized utility flow. It still lacks full pinned/favourite lab workflows and per-utility documentation cards. |
| Core / Architecture | Core category taxonomy and tint service | Fully implemented | Medium | Low | Launcher organization / visual taxonomy | PungentUtilityCategories.cs | Broad product categories, descriptions, sort order, compatibility normalization, and tint lookup are centralized. |
| Core / Architecture | Design Audit user-linkable documentation toolbar | Fully implemented | High | Medium | Project-specific documentation integration | PungentUtilityDocumentationLinks.cs; DocumentationLinkEditorPopup.cs; DrawDocumentationLinksToolbar in PungentUtilityDesignAuditWindow.cs | Fixed Open Design Bible/Open Inventory buttons were replaced by project-local documentation links. Toolbar is horizontal but not yet a dedicated scrollable control. |
| Core / Architecture | Design Validation Audit window | Fully implemented | Critical | Medium | Architecture QA gate | PungentUtilityDesignAudit.cs, PungentUtilityDesignAuditWindow.cs | The audit window scans scripts, registry entries, menu taxonomy, namespaces, asmdefs, CreateAssetMenu roots, layout signals, performance markers, and bundled docs. Future work is configurable rules, user-linked documentation, and project adapter audit profiles. |
| Core / Architecture | Developer Mode toggle for extra design/package permissions | Fully implemented | High | Medium | Safe shipping / internal tooling boundary | Editor/Developer/PungentDeveloperMode.cs; PungentUtilityDesignAuditWindow.cs developer toggle | Developer Mode is gated by PUNGENTFUNK_INTERNAL_DEVTOOLS and includes a release-blocker warning for public package export. |
| Core / Architecture | Developer-only theme preset source generation | Fully implemented | High | High | Internal theme authoring | UtilityThemePresetSourceGenerator.cs; UtilityThemePresetSourceWriter.cs; UtilityThemePresetLibrary.Generated.cs | Generated preset source can be created/updated/removed from developer-only tooling behind PUNGENTFUNK_INTERNAL_DEVTOOLS. |
| Core / Architecture | Direct lab menu routing | Fully implemented | High | Low | Navigation | PungentUtilityToolMenus.cs, PungentUtilityMenuPaths.cs, PungentUtilityLabs.cs | Direct tool/lab access is centralized under Tools/Pungent. Legacy aliases remain where compatibility is preserved. |
| Core / Architecture | Documentation buttons/cards on each utility tile | Partially implemented | High | Medium | Discoverability/support | PungentUtilityDocumentationLinks.cs; DocumentationLinkEditorPopup.cs; PungentUtilityDesignAuditWindow.cs | User-linkable documentation exists for the Design Validation Audit toolbar. Per-utility documentation buttons/cards in the Control Panel are still incomplete. |
| Core / Architecture | Documentation link storage and editor popup | Fully implemented | High | Medium | Project documentation integration | PungentUtilityDocumentationLinks.cs; DocumentationLinkEditorPopup.cs | Project-local documentation links can be added as asset links or external paths and opened from the audit toolbar. |
| Core / Architecture | Dry-run/preview/apply pattern for destructive workflows | Partially implemented | Critical | Medium | Safety | Placement preview/apply, reference scanner, tuning copy, rename preview | Good in many tools, but not uniformly documented/standardised. |
| Core / Architecture | Editor GUIStyle session bridge | Partially implemented | High | High | Appearance/editor customization | PungentEditorGuiStyleBridge.cs; PungentEditorStyleExplorerWindow.cs | Session GUIStyle overrides and restoration exist. Requires careful safeguards because global editor styling can affect unrelated Unity panels. |
| Core / Architecture | Editor toolbar quick-launch/status integration | Yet to implement | Medium | Medium | Access surface / workflow speed | No Unity Toolbar/EditorToolbar integration found | Design bible calls for toolbar quick launches, recent tools, active lab shortcuts, preview/debug toggles, and status indicators for frequent workflows. |
| Core / Architecture | Experimental global editor skin bridge | Partially implemented | High | Very High | Appearance/editor customization | PungentEditorSkinBridge.cs; PungentEditorSkinProfile.cs; PungentEditorSkinSelectorMap.cs; PungentEditorSkinUssGenerator.cs | USS generation and enable/disable flow exist. It remains experimental and needs visual compatibility testing across Unity versions/panels. |
| Core / Architecture | Inspector-embedded toolbars and Open in Lab actions | Partially implemented | High | Medium | Contextual access / authoring speed | PungentNoteInspectorIntegration.cs, Audio/Profile custom editors, PungentSceneGizmoSourceEditor.cs, PungentColourPaletteSOEditor.cs | Inspector notes and several custom editors provide context actions. A standardized package-wide Open in Lab toolbar pattern is still not complete. |
| Core / Architecture | Keyboard shortcuts / command palette quick actions | Yet to implement | Medium | Medium | Power-user workflow | No Shortcut attributes or ShortcutManager usage found | Design bible lists command-style actions such as open control panel/current lab, scan current selection, regenerate selected output, and toggle debug tools. |
| Core / Architecture | Lab descriptions/help text in launcher | Fully implemented | High | Low | Discoverability | PungentUtilityLabs.GetDescription, PungentUtilityControlPanelWindow.cs | Standard lab descriptions exist and are surfaced by launcher/tooltips. |
| Core / Architecture | Lab-based package architecture: Core + standalone labs + bundle + project adapters | Partially implemented | Critical | High | Core release architecture | 181 namespaced C# scripts; Runtime/Editor asmdefs; central registry; category/menu constants; bundled development docs | Bundle-level architecture is now mature. Still missing per-lab package manifests, per-lab/bridge asmdefs, optional bridge packages, and project-adapter package structure. |
| Core / Architecture | Large-window panel/service/state split | Partially implemented | Critical | High | Maintainability | Debug split files, PaletteDesigner* panels, AudioCoverage* scanners/models, ModularPathSpawnerEditorUtility.cs, ModularPathSpawnerEditorHooks.cs | Debug Control, Palette Designer, Audio Coverage, and Modular Path systems have been split/refactored. Several large windows remain above 1,000 lines, including Theme Customizer, generated preset library, Terrain Usage Scanner, and Audio Coverage Window. |
| Core / Architecture | Minimizer Tray: minimize floating utility panels into bottom-left tab headers | Partially implemented | High | High | Utility workflow / editor chrome | PungentUtilityMinimizer.BottomStripEnabled; ComputeTrayRect; PungentUtilityTrayWindow tab drawing | Minimize/restore, popup list, and optional bottom-left tab strip are implemented in code. Needs in-Unity validation for docking edge cases, strip positioning, clipping, and whether every registered utility header exposes the minimize action consistently. |
| Core / Architecture | Namespace hardening | Fully implemented | Critical | High | Export safety | 181/181 C# scripts declare namespaces | The previous namespace blocker is fully resolved in this archive. |
| Core / Architecture | Optional cross-lab bridge/degraded UI cards | Scaffolded | High | High | Standalone lab safety | RelatedUtilityIds in registry/control panel | Related utility links exist, but optional bridge packages, missing-integration cards, and soft service lookup are not yet implemented. |
| Core / Architecture | Package status badges: Stable/Experimental/In Progress/Deprecated/Project Adapter | Fully implemented | High | Low | Release polish | PungentUtilityPackageStatus.cs, PungentUtilityDescriptor.cs, PungentUtilityRegistry.cs, PungentUtilityControlPanelWindow.cs | Shared status constants/descriptions/tints/sort keys exist. Current central registry status counts: {'Stable': 12, 'InProgress': 2, 'Experimental': 13}. |
| Core / Architecture | Per-lab package manifests and standalone release metadata | Yet to implement | Critical | High | Asset Store / modular distribution | No package.json/manifests or per-lab asmdefs found | Required for independently releasable lab packages and optional bridge packages. Current package is bundle-oriented. |
| Core / Architecture | Pinned tools, favourite labs, and resume-last-session workflows | Partially implemented | Medium | Medium | Launcher UX / workflow continuity | PungentUtilityControlPanelWindow.cs, PungentUtilityMinimizer, UtilityWindowPrefs | Recent/minimized utility flows exist. Pinned tools, favourite labs, last active module per lab, and recently used asset resume are still not a unified feature. |
| Core / Architecture | PungentUtilityDescriptor metadata contract | Fully implemented | Critical | Medium | Central to every utility | Editor/Utilities Core/PungentUtilityDescriptor.cs | Descriptor now includes lab/module/status/related utility IDs and cached window type resolution. |
| Core / Architecture | Registry related-utility links | Fully implemented | High | Low | Cross-lab discoverability | PungentUtilityDescriptor.RelatedUtilityIds, PungentUtilityRegistry.RelatedUtilities, Control Panel related tool buttons | Registry supports and displays related utilities. It does not yet imply optional bridge/package dependency management. |
| Core / Architecture | Release-readiness interpretation and next-pass brief generator | Fully implemented | High | Medium | Internal QA / update planning | PungentUtilityReleaseReadiness.cs; PungentUtilityDesignAuditWindow.cs | Design audit reports can be converted into prioritized gates and paste-ready next-pass brief text. |
| Core / Architecture | Responsive small-window layouts, vertical scrolling, draggable shared borders | Partially implemented | High | Medium | UX quality | Theme resize handles; many scroll views | Several windows still have large min sizes or monolithic layouts. |
| Core / Architecture | Runtime/editor asmdef separation | Partially implemented | Critical | Medium | Export hardening | PungentFunk.Utilities.Runtime.asmdef; Editor/PungentFunk.Utilities.Editor.asmdef; 0 UnityEditor refs in runtime/shared scripts detected by text scan | The runtime/editor split is cleaner than the prior snapshot. Per-lab/bridge splits remain future work. |
| Core / Architecture | Scene View overlay integration for scene tools | Scaffolded | High | High | Scene workflow access surface | PungentSceneHelpOverlay.cs, PungentNoteSceneOverlay.cs, scene GUI handlers, SupportsSceneOverlay metadata | Scene-related hooks and overlay-like GUI exist, but no formal Unity Overlay API classes were found. |
| Core / Architecture | Selection-aware and context-aware tool launch startup | Partially implemented | High | Medium | Workflow speed / user context | Context menus and selection-support metadata; several windows import current selection | Some tools seed from current selection or context menus. Not all utility windows detect and adapt to current selection/folder/asset type on open. |
| Core / Architecture | Shared UtilityWindowPrefs persistence | Fully implemented | High | Low | UX consistency | UtilityWindowPrefs.cs plus widespread preference usage | Shared EditorPrefs helper is stable and broadly used. Additional domain-specific session state still lives in individual tools where appropriate. |
| Core / Architecture | Shared UtilityWindowTheme visual system | Fully implemented | Critical | High | UX consistency | UtilityWindowTheme.cs, UtilityWindowThemeCustomizer.cs | Most windows use it. |
| Core / Architecture | Shared editor performance/incremental queue helper | Partially implemented | High | Medium | Performance foundation | PungentEditorPerformanceUtility.cs, PungentScan* infrastructure | Performance helper has expanded significantly. Adoption is not yet universal across all scanner and preview windows. |
| Core / Architecture | Shared scan/cache/performance infrastructure | Partially implemented | Critical | High | All scanner windows | Editor/Utilities Core/Scanning/PungentScanCache.cs, PungentScanResult.cs, PungentScanSession.cs, PungentScanGUI.cs; audio scanners use PungentScanResult | Reusable scan infrastructure is implemented. Full migration across all scanner windows is still in progress. |
| Core / Architecture | Standard CreateAssetMenu root: PungentFunk Utilities/<Lab>/<Asset Type> | Fully implemented | High | Low | Professional polish | 12 CreateAssetMenu declarations all use PungentFunk Utilities/... roots | Former PungentFunk/Audio and Pungent Funk/Generation drift has been corrected. Some sublab labels can still be refined, but the root is consistent. |
| Core / Architecture | Standard Tools/Utilities menu policy | Partially implemented | High | Medium | Professional polish | PungentUtilityMenuPaths.Root = Tools/Pungent; PungentUtilityToolMenus.cs; legacy menu aliases remain | Primary public menu root has shifted to Tools/Pungent and is centralized. Legacy Tools/Utilities and old compatibility aliases remain in some files and should be curated before release. |
| Core / Architecture | TextMeshPro optional dependency isolation or asmdef declaration | Partially implemented | High | Medium | Compile safety / optional dependencies | FontPreviewWindow.cs uses #if TMP_PRESENT; PaletteApplyUtility uses reflection-style TMP handling; Editor asmdef has no versionDefines | Compile-time TMP usage is guarded, reducing hard dependency risk. Add asmdef versionDefines or optional bridge packaging so TMP features become available automatically when TextMeshPro is installed. |
| Core / Architecture | Theme Recipe developer overwrite selected starter theme | Fully implemented | High | Medium | Theme authoring / developer tooling | UtilityWindowThemeCustomizer.PresetAuthoring.cs; UtilityThemePresetSourceWriter.cs | Developer-only warning panel can overwrite selected generated preset source with current theme values. |
| Core / Architecture | Theme preset add/remove/archive management with developer-only archived presets | Fully implemented | High | Medium | Theme authoring / release curation | UtilityWindowThemeCustomizer.PresetAuthoring.cs; UtilityThemePresetSourceWriter.cs; UtilityThemePresetLibrary.Generated.cs | Developer tools can add generated presets, remove generated entries, and archive/unarchive presets using hiddenFromGallery metadata. |
| Core / Architecture | Theme presets include curated commercially redistributable fonts | Scaffolded | Medium | Medium | Appearance presets / release polish | UtilityThemePresetDefinition.FontHints; FontPreviewWindow.cs | Preset font-hint metadata exists, but no redistributable font asset pack/licence workflow is present in this archive. |
| Core / Architecture | Tooltips and contextual enum descriptions for all fields | Partially implemented | High | Medium | Usability | Many GUIContent tooltips exist | Not universal; enum-specific help is inconsistent. |
| Core / Architecture | USS bridge text-style controls and Style Explorer font preset mapping | Partially implemented | High | High | Appearance Lab / editor skinning | PungentEditorStyleExplorerWindow.cs; PungentEditorTextRoleMap.cs; PungentEditorGuiStyleBridge.cs; PungentEditorSkinUssGenerator.cs | Style Explorer now exposes text-role mapping with USS and GUIStyle toggles per editor text target. It remains experimental and needs visual QA against hierarchy/inspector/font-size edge cases. |
| Core / Architecture | Undo/SetDirty safety for scene/asset modification | Partially implemented | Critical | Medium | Safety | PlacementApplyUtility, palette changes, editor scanners | Present in many areas; needs package-wide audit. |
| Core / Architecture | Utility Tray / parked recent windows | Fully implemented | Medium | Medium | Workflow QoL | Editor/Utilities Core/Minimize Utilities/PungentUtilityTrayWindow.cs; PungentMinimizedUtilitiesButton.cs; PungentMinimizedUtilitiesPopupContent.cs | The old tray has been replaced/expanded into a minimized utilities popup and restore system with persisted entries. |
| Core / Architecture | Window appearance/theme customizer | Fully implemented | High | High | UX consistency | UtilityWindowThemeCustomizer.cs, UtilityWindowTheme.cs, UtilityWindowPrefs.cs | Theme customizer includes built-in presets, generated/randomized themes, custom local preset save/update/delete, role colours, locks, and persistence. Developer-mode preset overwrites/archive workflow is separate future work. |
| Debug Lab | Debug Control Center window | Fully implemented | High | High | Debug utility product | DebugControlWindow.cs shell plus DebugControlActionScheduler, ComponentPanel, DiscoveryService, RouterPanel, ReflectionUtility, WindowState | Debug Control Center has been split into maintainable support files. Monitor file sizes but the prior single-file blocker is resolved for this tool. |
| Debug Lab | Debug Router | Fully implemented | High | High | Debug signal backbone | Debug/DebugRouter.cs | Moved out of Editor folder and namespaced as runtime/shared debug infrastructure. |
| Debug Lab | Debug action scheduler | Fully implemented | Medium | High | Debug automation | DebugControlActionModel.cs, DebugControlActionScheduler.cs | Dedicated scheduler model and executor/panel now exist. |
| Debug Lab | Debug channel/signal constants | Fully implemented | Medium | Low | Debug standardization | Debug/DebugChannels.cs | Moved out of Editor folder and namespaced under PungentFunk.Utilities.Debugging. |
| Debug Lab | Debug coverage reports via Project Audit optional hook | Yet to implement | Medium | Medium | Cross-lab enhancement | No bridge/report module found | Architecture bible lists this as optional hook. |
| Debug Lab | Debug signal relay to UnityEvents | Fully implemented | Medium | Low | Scene integration | Debug/DebugSignalRelay.cs | Moved out of Editor folder and namespaced as runtime/shared debug infrastructure. |
| Debug Lab | Reflected debug fields/static toggles/component state inspection | Fully implemented | High | High | Debug authoring | DebugControlComponentPanel.cs, DebugControlDiscoveryService.cs, DebugControlReflectionUtility.cs | Feature is extracted from the monolith into service/panel files. |
| Debug Lab | Router panel reimplementation without serializedObject/targets OnSceneGUI issues | Fully implemented | High | Medium | Stability | DebugControlRouterPanel.cs | Router UI is now a dedicated panel file and no longer bundled only inside DebugControlWindow. |
| Asset Placement Lab | Area Scatter module | Partially implemented | Critical | High | Primary placement workflow | AssetPlacementLabWindow.cs, PungentPlacementScatterUtility.cs | Preview/apply with several patterns exists; needs polish and lab-level integration with all planned rules. |
| Asset Placement Lab | Asset Placement Lab hub/window | Partially implemented | Critical | High | Placement lab core | Editor/Asset Placement Lab/AssetPlacementLabWindow.cs | Registered as In Progress and expanded to a 666-line lab hub; still missing planned surface brush/socket graph/cluster/drop/connector/bounds-fill modules. |
| Asset Placement Lab | Backtracking solver | Yet to implement | Medium | Very High | Advanced placement solving | No file found | Listed in planned shared utilities. |
| Asset Placement Lab | Bounds fill / room dressing | Yet to implement | High | Very High | Level-design automation | No room-dressing module found | Planned module. |
| Asset Placement Lab | Cluster growth placement | Yet to implement | Medium | High | Organic placement | No cluster module found | Planned module from implementation brief. |
| Asset Placement Lab | Colour Lab palette-driven prefab/material variation bridge | Yet to implement | Medium | High | Cross-lab enhancement | No bridge package found | Architecture bible recommends optional hook. |
| Asset Placement Lab | Connector / bridge placement | Yet to implement | Medium | High | Modular assembly | No connector module found | Planned module. |
| Asset Placement Lab | Grid / footprint placement module | Partially implemented | High | Medium | Manual assist | AssetPlacementLabWindow GridFootprint module, GridUtility | Grid snapping and auto-footprint exist; not a complete grid placement/stamping workflow. |
| Asset Placement Lab | Heatmap / density-mask scatter | Partially implemented | High | Medium | Contextual placement | RuleSet heatmap fields + ScatterUtility | Texture heatmap sampling exists; no Texture Lab bridge or mask authoring workflow yet. |
| Asset Placement Lab | Modular path to placement-lab handoff | Partially implemented | High | Medium | Cross-tool scene workflow | PungentPlacementPathIntegrationUtility.cs; AssetPlacementLabWindow.OpenWithPath | Path sources can open/focus the placement lab and produce bounds/path previews. Needs full bridge UX polish. |
| Asset Placement Lab | Path placement / modular path generation | Partially implemented | High | High | Scene dressing/paths | PungentPlacementPathIntegrationUtility.cs; ModularPathSpawner.cs; ModularPathWindow.cs; AssetPlacementLabWindow ModularPath area mode | Asset Placement Lab can integrate with ModularPathSpawner for path bounds/preview. Still not a full standalone path placement solver inside Placement Lab. |
| Asset Placement Lab | Physics drop / settle placement | Yet to implement | Medium | High | Natural dressing workflow | No physics placement module found | Planned module. |
| Asset Placement Lab | Placement Group Manager | Partially implemented | High | Medium | Generated-scene maintenance | AssetPlacementLabWindow Group Manager; PungentPlacementGroupRepairUtility.cs | Group scan/adopt/delete/repair/revalidate/realign/source-prefab checks are implemented. Replace variations and deeper repair remain future work. |
| Asset Placement Lab | Placement asset set / weighted prefab catalogue | Fully implemented | Critical | Medium | Placement core | Asset Placement Lab/PungentPlacementAssetSetSO.cs | Namespaced under PungentFunk.Utilities.Placement with standard CreateAssetMenu root. |
| Asset Placement Lab | Placement data models: context, candidate, result, footprint, marker, group | Fully implemented | Critical | Medium | Placement core | PungentPlacementTypes.cs, PungentPlacedAssetMarker.cs, PungentPlacementGroup.cs | Core result/candidate flow exists. |
| Asset Placement Lab | Placement group repair utility | Partially implemented | High | Medium | Scene maintenance / placement lifecycle | PungentPlacementGroupRepairUtility.cs | Adds revalidate, orphan marker removal, child selection, missing source prefab detection, source prefab replacement, and surface realignment helpers. |
| Asset Placement Lab | Placement overlap validator | Scaffolded | High | Medium | Placement rule quality | Overlap logic appears inside generation/rule flow | No separate PungentPlacementOverlapValidator file. |
| Asset Placement Lab | Placement preview utility | Scaffolded | High | Medium | UX/safety | Preview drawing and path preview remain inside AssetPlacementLabWindow/PungentPlacementPathIntegrationUtility | Preview logic exists but no standalone PungentPlacementPreviewUtility file was found. |
| Asset Placement Lab | Placement rule set / reusable surface rules | Partially implemented | Critical | Medium | Placement core | Asset Placement Lab/PungentPlacementRuleSetSO.cs | Core rule data exists and is namespaced; advanced contextual rules/repair modules still need expansion. |
| Asset Placement Lab | Placement socket marker | Partially implemented | High | Medium | Modular assembly foundation | PungentPlacementSocket.cs | Socket metadata exists. |
| Asset Placement Lab | Re-align existing placed objects | Partially implemented | High | Medium | Scene maintenance | PungentPlacementGroupRepairUtility.RealignSelectedMarkersToSurface; SurfaceAlignToolWindow | Selected marker realignment exists. A full batch re-align workflow for all generated groups remains incomplete. |
| Asset Placement Lab | Replace variations | Yet to implement | High | Medium | Scene iteration | No replacement module found | Planned scene-management category. |
| Asset Placement Lab | Scatter patterns: random, min spacing, grid, jittered grid, hex, spiral, ring, line | Fully implemented | High | Medium | Scatter generation | PungentPlacementScatterUtility.cs, PungentPlacementTypes.cs | Candidate generation exists. |
| Asset Placement Lab | Selection array placement | Yet to implement | Medium | Medium | Manual assist | No selection array module found | Listed as planned manual assist category. |
| Asset Placement Lab | Socket graph / modular tile placement | Scaffolded | High | Very High | Modular assembly | Socket component + validator only | Actual graph placement/solver not implemented. |
| Asset Placement Lab | Socket validator | Partially implemented | High | Medium | Modular assembly safety | PungentPlacementSocketValidator.cs | Validates selected sockets; not a full socket library validator/solver. |
| Asset Placement Lab | Stamp placement | Yet to implement | Medium | Medium | Manual assist | No stamp module found | Listed as planned manual assist category. |
| Asset Placement Lab | Surface brush placement | Partially implemented | High | High | Manual dressing workflow | PungentPlacementSurfaceBrushController.cs; AssetPlacementLabWindow SurfaceBrush module | Surface Brush module and scene GUI controller exist. Needs interactive Unity validation and broader brush/stamp feature depth. |
| Asset Placement Lab | Surface sampler utility | Scaffolded | High | Medium | Placement rule quality | Brush/path/scatter raycast sampling logic in placement files | Sampling exists inline across placement modules, but no shared PungentPlacementSurfaceSampler utility exists yet. |
| Asset Placement Lab | Terrain scatter with terrain layers/context rules | Partially implemented | High | High | Level-design automation | RuleSet surface mask/height/slope | No terrain-layer material/category weighting system found. |
| Asset Placement Lab | Texture Lab density-mask bridge | Yet to implement | Medium | High | Cross-lab enhancement | Heatmap field only | No dedicated bridge or handoff workflow. |
| Asset Placement Lab | Validate asset set | Scaffolded | High | Medium | Authoring safety | AssetSetBuilder and asset set fields | No dedicated asset set validator beyond selection creation/metadata. |
| Asset Placement Lab | Validate socket library | Partially implemented | Medium | Medium | Authoring safety | SocketValidator | Selection validation exists; library-wide validation absent. |
| Scene Workflow Lab | Modular Path Builder window/editor/runtime spawner | Partially implemented | High | High | Path/modular assembly | ModularPathSpawner.cs, ModularPathSpawnerEditorHooks.cs, ModularPathSpawnerEditorUtility.cs, ModularPathWindow.cs | Editor hooks/utility were split from runtime. ModularPathSpawner is still 873 lines and remains a split target. |
| Scene Workflow Lab | Modular Path editor/runtime split helpers | Partially implemented | High | Medium | Runtime/editor separation | ModularPathSpawnerEditorHooks.cs; ModularPathSpawnerEditorUtility.cs | Editor hooks and utility logic have moved out of the runtime spawner. The spawner remains large but runtime UnityEditor references are no longer detected by text scan. |
| Scene Workflow Lab | Path Authoring Toolkit | Partially implemented | High | Medium | Path helper foundation | PungentPathAuthoringToolkitWindow.cs | Handle/sampling concepts present; not fully consolidated with path placement. |
| Scene Workflow Lab | Scene Gizmo Browser | Partially implemented | High | Medium | Scene visualization management | PungentGizmoBrowserWindow.cs | Cached scanning was added; still uses SceneView.RepaintAll in places. |
| Scene Workflow Lab | Scene Gizmo Source component | Fully implemented | High | Medium | Scene visualization | PungentSceneGizmoSource.cs | Configurable gizmo rules and labels. |
| Scene Workflow Lab | Scene Gizmo Source custom inspector | Fully implemented | Medium | Medium | Authoring UX | PungentSceneGizmoSourceEditor.cs | Rule editing/templates/preview. |
| Scene Workflow Lab | Scene Help Overlay | Scaffolded | Medium | Low | Assistance UX | PungentSceneHelpOverlay.cs | Lightweight hook only. |
| Scene Workflow Lab | Scene Navigation window | Partially implemented | High | Medium | Scene authoring QoL | PungentSceneNavigationWindow.cs | Waypoints/tracking/focus appear present; prefs/splitters could be more consistent. |
| Scene Workflow Lab | Scene Workflow lab hub consolidation | Partially implemented | High | Medium | Navigation | Registry lab menu + individual windows | Tools are grouped by registry, but no cohesive lab hub window beyond launcher filtering. |
| Scene Workflow Lab | Surface Align Tool / TerrainAlign compatibility | Partially implemented | High | Medium | Scene placement aid | TerrainAlignTool.cs | Functional and generic; legacy naming remains. |
| Colour Lab | Built-in Unity swatches/reference libraries | Yet to implement | Medium | Medium | Reference workflow | No standard library assets found | Mentioned in colour tool refinement. |
| Colour Lab | Colour blending A/B tone matrix and temperature bars | Yet to implement | Low | High | Advanced refinement | No blending section found | Prior Colour Inspector plan only. |
| Colour Lab | Colour palette asset | Fully implemented | High | Medium | Package architecture | Palette Designer/PungentColourPaletteSO.cs | Now uses namespace PungentFunk.Utilities.Colour and standard CreateAssetMenu root PungentFunk Utilities/Colour/Palette. |
| Colour Lab | Colour value modes: RGB/HSV/CMYK/LAB/Grayscale conversions | Fully implemented | High | Medium | Colour editing foundation | ColourConversionUtility.cs | Utility support exists. |
| Colour Lab | Contrast analysis / WCAG ratings | Fully implemented | High | Medium | Accessibility | ColourContrastUtility.cs, PaletteAnalysisUtility.cs | Contrast pairs and ratings exist. |
| Colour Lab | Contrast checker matrix visual section | Scaffolded | Medium | Medium | Accessibility visualization | PaletteAnalysisUtility pair list | No matrix VisualElement/section as designed. |
| Colour Lab | Coolors-style organic palette generation | Partially implemented | High | High | Palette UX | PaletteDesignerWindow generation controls | Seed/harmony/ranges/contrast exist; weighted harmony mixer and richer contextual generation absent. |
| Colour Lab | Deficiency preview / colour blindness filters | Fully implemented | High | Medium | Accessibility | ColourDeficiencyPreviewUtility.cs + toolbar enum | Preview utility and UI selection exist. |
| Colour Lab | Dynamic filter sliders by selected colour mode | Partially implemented | High | Medium | Advanced filtering | PaletteDesignerWindow generation sliders + ColourConversionUtility | Mode conversion exists; full filter-mode panel from runtime design is not present. |
| Colour Lab | Filter influence scalar | Yet to implement | Medium | Low | Generation control | No filterInfluence field found | Harmony influence exists, filter influence does not. |
| Colour Lab | Generated variant/base/extra swatch column model | Yet to implement | Medium | High | Advanced palette generation | No GeneratedPaletteVariant UXML/USS or runtime UI files found | Current package uses IMGUI Palette Designer, not the later runtime/UI Toolkit structure. |
| Colour Lab | Gradient/tint tools | Yet to implement | Medium | Medium | Colour authoring | No dedicated gradient/tint tools found | Mentioned in advanced colour design list. |
| Colour Lab | Harmony Influence scalar | Fully implemented | Medium | Low | Generator tuning | PaletteGenerationSettings.harmonyInfluence, UI slider | Present. |
| Colour Lab | Harmony rules: monochromatic/complementary/split/analogous/triadic/tetradic/square | Fully implemented | High | Medium | Palette theory | ColourHarmonyUtility.cs, PaletteGenerationSettings.cs | Core harmony calculations exist. |
| Colour Lab | Import/export formats such as .aco | Yet to implement | Low | High | Interchange | No import/export parser found | Mentioned as aspirational feature. |
| Colour Lab | Include selected colour / include selected palette toggles during generation | Yet to implement | Medium | Medium | Generator workflow | No include selected/include palette fields found | Mentioned in later colour generator plan. |
| Colour Lab | Interactive weighted Harmony Mixer bar | Yet to implement | Medium | High | Advanced generation UI | No mixer UI classes found | Mentioned as finalized design in prior colour work, not in current package. |
| Colour Lab | Multiple harmony types with priority/mixing ratios | Yet to implement | High | High | Advanced generation | No weighted mixer model found | Previously desired for advanced generator. |
| Colour Lab | Palette Designer main window | Partially implemented | Critical | High | Primary Colour Lab UI | PaletteDesignerWindow.cs plus PaletteDesignerToolbar, SwatchGridPanel, GenerationPanel, SelectedSwatchPanel, AnalysisPanel, ApplyPanel, GUIUtility | Palette Designer has been modularized into panel files and the main window is much smaller than before. It still needs UX/generation-quality passes and remains IMGUI/editor-only. |
| Colour Lab | Palette Designer panel/service split | Partially implemented | High | Medium | Maintainability / UX refinement | PaletteDesignerAnalysisPanel.cs; PaletteDesignerApplyPanel.cs; PaletteDesignerGenerationPanel.cs; PaletteDesignerSelectedSwatchPanel.cs; PaletteDesignerSwatchGridPanel.cs; PaletteDesignerToolbar.cs | Major panel extraction has occurred. Main window remains >1,000 lines and generation/UI polish remains pending. |
| Colour Lab | Palette Refinements: harmony section, monochrome bar, tone matrices | Yet to implement | Medium | High | Advanced refinement UI | No refinement panel files found | Detailed UI structure from prior colour chats absent. |
| Colour Lab | Palette application to materials/renderers/UI/text/TMP-like targets | Partially implemented | High | High | Cross-scene application | PaletteApplyUtility.cs | Applies to selected targets and uses reflection for TMP-like text; still needs optional bridge/dependency documentation. |
| Colour Lab | Palette asset + swatch data model | Fully implemented | Critical | Medium | Colour Lab core | PungentColourPaletteSO.cs, PaletteSwatch.cs | Palette storage and swatch metadata exist. |
| Colour Lab | Palette generation engine | Partially implemented | Critical | High | Palette creation | PaletteGeneratorUtility.cs, PaletteGenerationSettings.cs, PaletteDesignerGenerationPanel.cs | Core engine remains, but generation diversity and harmony contrast still need subjective QA. |
| Colour Lab | Palette panel-first regeneration workflow with collapsed advanced generation options | Partially implemented | High | Medium | Palette Designer UX | PaletteDesignerSwatchGridPanel.cs; PaletteDesignerGenerationPanel.cs; PaletteDesignerToolbar.cs | Palette UI is split into swatch grid/generation/toolbar panels. Needs user testing against the requested palette-panel-first workflow and advanced affordance polish. |
| Colour Lab | Palette report card analysis | Partially implemented | High | Medium | Palette quality | PaletteAnalysisUtility.cs | Contrast/role/similarity diagnostics exist; not a full report-card UI/export. |
| Colour Lab | Palette roles and role-aware generation | Partially implemented | High | Medium | Design-system palette output | PaletteSwatchRole + generation role logic | Roles exist; role UI/ordering is basic compared with full desired system. |
| Colour Lab | Palette save/load/storage utility | Fully implemented | High | Medium | Palette workflow | PungentPaletteStorageUtility.cs | Load selected/save/save as/default paths exist. |
| Colour Lab | Recognized standards: Pungent Library, AS 2700, FS 595, BS 381, RAL, ISCC-NBS, NCS, X11, Crayola, Resene, XKCD | Yet to implement | Medium | High | Reference workflow | No reference library datasets found | XKCD/reference standards not present in archive. |
| Colour Lab | Regenerate individual swatches | Fully implemented | High | Medium | Palette iteration | RegenerateSwatch flow | Disabled for locked swatches. |
| Colour Lab | Regenerate whole palette while preserving locked swatches | Fully implemented | High | Medium | Palette iteration | RegeneratePaletteUnlocked | Implemented. |
| Colour Lab | Right-click/context menu for swatches | Fully implemented | High | Medium | Palette iteration | PaletteDesignerWindow.ShowSwatchContextMenu | Lock/regenerate/duplicate/remove/copy actions present. |
| Colour Lab | Role-based ordering | Partially implemented | Medium | Medium | Palette management | SortSwatches and roles | Sorting exists; full role-first layout not complete. |
| Colour Lab | Runtime UI Toolkit Colour Inspector panels/UXML/USS | Yet to implement | Medium | Very High | Runtime tool product | No UXML/USS/controller files found | Current archive is IMGUI editor package. |
| Colour Lab | Scene colour tracker / global scene reference tracking | Yet to implement | Medium | High | Scene design workflow | No scene colour tracker/reference panel files | Mentioned in Colour Inspector planning. |
| Colour Lab | Sorting options UI | Fully implemented | Medium | Low | Palette management | PaletteDesignerWindow sort buttons | Hue/value/saturation/name-like controls appear present. |
| Colour Lab | Stronger harmony differentiation and generation diversity | Partially implemented | High | Medium | Palette generation quality | PaletteGeneratorUtility.cs, ColourHarmonyUtility.cs | Core harmony modes exist, but current outputs reportedly remain too similar across harmony selections. Needs contrast/tone/temperature/role-aware generation improvements. |
| Colour Lab | Swatch locks | Fully implemented | High | Medium | Palette iteration | PaletteSwatch.locked + UI controls | Locking and locked-count UI exist. |
| Texture Lab | Density/stamp texture support for placement masks | Partially implemented | Medium | Medium | Texture/Placement bridge | Procedural settings include density maps; placement rule has heatmap | No direct handoff/bridge UI. |
| Texture Lab | Organic/pattern generation expansion | Partially implemented | Medium | High | Texture generation | ProceduralTextureGenerator.cs | Many pattern modes exist; future organic generators can build on this. |
| Texture Lab | Palette-to-texture/material-mask generation | Yet to implement | Medium | High | Colour/Texture bridge | No palette bridge found | Listed as future texture lab enhancement. |
| Texture Lab | Procedural Texture Lab window | Partially implemented | High | High | Texture Lab core | ProceduralTextureLabWindow.cs | Functional preview/generate/export; could still use lab split/docs/bridge polish. |
| Texture Lab | Procedural texture generator engine | Fully implemented | High | High | Texture generation core | ProceduralTextureGenerator.cs | Pattern/stamp/density/texture creation present. |
| Texture Lab | Procedural texture settings/runtime-safe model | Fully implemented | High | Medium | Texture generation core | ProceduralTextureSettings.cs | Serializable settings for patterns/stamps/grid/density/etc. |
| Texture Lab | Texture Array Baker window | Partially implemented | High | High | Asset pipeline | TextureArrayBakerWindow.cs, TextureArrayBakerUtility.cs | Functional bake workflow; still in Asset menu category rather than Texture Lab menu. |
| Audio Lab | Audio Catalog Coverage scanner | Partially implemented | High | High | Audio authoring QA | AudioCoverageWindow.cs plus AudioCoverageCatalogScanner.cs, AudioCoverageScanModels.cs | Catalog scan logic has been extracted. Runtime cue director/catalog remains future work. |
| Audio Lab | Audio Setup Coverage scanner | Partially implemented | High | High | Audio authoring QA | AudioCoverageContextWindow.cs plus AudioCoverageContextScanner.cs, AudioCoverageScanModels.cs | Scanner logic has been extracted into a service/model split and can publish PungentScanResult entries. Window is still sizeable but less monolithic. |
| Audio Lab | Audio clip set asset | Fully implemented | High | Medium | Audio runtime core | Audio Coverage/AudioClipSetSO.cs | Now uses namespace PungentFunk.Utilities.Audio and standard CreateAssetMenu root PungentFunk Utilities/Audio/Clip Set. |
| Audio Lab | Audio coverage scanner service/model split | Partially implemented | High | Medium | Maintainability / scan infrastructure adoption | AudioCoverageCatalogScanner.cs; AudioCoverageContextScanner.cs; AudioCoverageScanModels.cs | Audio scan logic is now extracted from windows and can populate shared PungentScanResult models. |
| Audio Lab | Audio cue coverage module integrated with future director | Yet to implement | High | High | Coverage + runtime integration | Existing coverage windows only | Needs catalog/director model first. |
| Audio Lab | Audio material resolver for colliders/raycast hits/terrain | Fully implemented | High | High | Runtime surface audio | AudioMaterialResolver.cs | Collider/tag/terrain resolution helpers. |
| Audio Lab | Audio setup coverage profile | Partially implemented | High | High | Coverage validation | Audio Coverage/AudioCoverageProfileSO.cs | Now uses standard CreateAssetMenu root. Generic profile exists, but some default cue examples may still need review. |
| Audio Lab | Contact audio router/source pooling/loop instances | Partially implemented | High | High | Runtime audio playback | ContactAudioRouter.cs, ContactLoopInstance.cs | Core routing/loops exist; full director/catalog integration missing. |
| Audio Lab | Contact event classifier | Fully implemented | High | Medium | Runtime contact audio | ContactEventClassifier.cs | Classifies impacts/slide/scrape/etc. |
| Audio Lab | Generic AudioSource pool service | Scaffolded | Medium | Medium | Audio runtime service | ContactAudioRouter internal pooling | No standalone PungentAudioSourcePool file. |
| Audio Lab | Interaction matrix/profile system | Fully implemented | High | Medium | Runtime surface audio | AudioInteractionMatrixSO.cs, AudioInteractionProfileSO.cs | Material-pair profile resolution exists. |
| Audio Lab | Mixer routing/default mixer setup utility | Yet to implement | Medium | Medium | Audio project setup | No mixer setup generator found | Mentioned in audio setup planning. |
| Audio Lab | Runtime audio cue catalog/director | Yet to implement | High | High | Audio runtime service | No PungentAudioCueCatalogSO/Director found | Future pass in implementation brief. |
| Audio Lab | Runtime audio hooks for cue playback | Yet to implement | Medium | High | Audio integration | No runtime hook classes found | Planned future audio lab expansion. |
| Audio Lab | Surface audio material definitions/library/tags | Fully implemented | High | Medium | Runtime surface audio | AudioSurfaceMaterialSO.cs, AudioMaterialLibrarySO.cs, AudioMaterialTag.cs | Generic surface materials and tags exist. |
| Audio Lab | Terrain audio material profiles | Fully implemented | High | Medium | Terrain audio | TerrainAudioMaterialProfileSO.cs, TerrainAudioProfileEditor.cs | Layer bindings and diagnostics exist. |
| Asset Preview & Export Lab | Colourway icon generation bridge | Yet to implement | Medium | High | Cross-lab enhancement | No Colour/Preview bridge found | Architecture bible optional hook. |
| Asset Preview & Export Lab | Font Preview | Partially implemented | Medium | Medium | Asset review | Editor/FontPreviewWindow.cs | Font/TMP preview workflow exists, but FontPreviewWindow has a compile-time TMPro dependency while the Editor asmdef does not reference TextMeshPro. For standalone shipping, add the dependency explicitly or isolate TMP support in an optional bridge. |
| Asset Preview & Export Lab | Font licence classification / redistributable font packaging workflow | Yet to implement | Medium | Medium | Asset packaging / font curation | No font licence metadata or font builder workflow found | A previous font archive task required separating commercially redistributable fonts and preserving licence files. This could become a package-prep utility if kept generic. |
| Asset Preview & Export Lab | Prefab Asset Exporter | Partially implemented | High | High | Asset packaging | PrefabAssetExporter.cs, PrefabAssetExporterWindow.cs | Copies/reuses prefab dependencies; needs release docs/safety validation. |
| Asset Preview & Export Lab | Prefab Icon Generator | Partially implemented | High | High | Asset presentation/export | PrefabIconGeneratorWindow.cs | Non-blocking preview/export exists; could use bridge polish. |
| Asset Preview & Export Lab | Preview/batch render helpers | Scaffolded | Medium | High | Future preview/export | PrefabIconGenerator jobs | No generic batch-render service/lab hub yet. |
| Project Audit & Authoring Lab | Batch-create assets from text/CSV/table input | Yet to implement | High | High | Authoring automation | No batch SO importer found | Future SO lab feature. |
| Project Audit & Authoring Lab | Bulk Rename | Partially implemented | High | Medium | Scene authoring productivity | BulkRenameWindow.cs | Preview/apply exists; legacy Tools/RenameTool alias remains. |
| Project Audit & Authoring Lab | Component Tuning Copy | Partially implemented | High | Medium | Authoring productivity | ComponentTuningCopyWindow.cs | Dry-run/copy modes; could use deeper docs and adapter recipes. |
| Project Audit & Authoring Lab | Coverage Matrix | Partially implemented | Medium | Medium | Authoring QA | PungentCoverageMatrixWindow.cs and shared scan models | Coverage matrix remains useful; still not a formal Visualization Lab widget. |
| Project Audit & Authoring Lab | Dependency previews for generated/validated assets | Yet to implement | Medium | High | Authoring safety | No dependency graph preview found | Could link with Visualization Lab. |
| Project Audit & Authoring Lab | Design-audit issue to note/backlog bridge | Fully implemented | High | Medium | Audit follow-up workflow | PungentNoteAuditIssueBridge.cs | Design audit issues can be converted into actionable notes/follow-ups. |
| Project Audit & Authoring Lab | Feature inventory/import to notes backlog | Partially implemented | High | High | Backlog migration / planning | PungentNoteInventoryImporter.cs; PungentNoteImportSources.cs; PungentNoteBacklogSeeder.cs | Curated seeds and import source tracking exist. Needs validation on current inventory CSV and duplicate handling workflow. |
| Project Audit & Authoring Lab | Future utility record model | Partially implemented | High | Medium | Roadmap tracking | PungentNoteModels.cs; PungentNoteBacklogSeedModels.cs | Future utility records exist and can be linked to notes, utilities, status, priority, tags, and implementation status. Not yet integrated with registry descriptors as first-class future utilities. |
| Project Audit & Authoring Lab | Notes & Roadmap workspace | Partially implemented | Critical | High | Planning, audit follow-up, feature backlog, documentation | PungentNotesRoadmapWindow.cs plus Notes subsystem files | Large new workspace tracks notes, future utilities, status, priority, tags, linked utilities, audit issues, imports, and contextual targets. Needs user testing and documentation replacement. |
| Project Audit & Authoring Lab | Notes context menus and inspector integration | Partially implemented | High | Medium | Context-aware documentation | PungentNoteContextMenus.cs; PungentNoteInspectorIntegration.cs; PungentNoteContextResolver.cs | Asset/GameObject/property note creation and viewing are implemented. Needs broad inspector compatibility validation. |
| Project Audit & Authoring Lab | Notes scene overlay / surface display | Scaffolded | Medium | High | Scene documentation/annotations | PungentNoteSceneOverlay.cs; PungentNoteSurfaceGUI.cs | Overlay/surface GUI hooks exist but need formal overlay integration and usability testing. |
| Project Audit & Authoring Lab | Persistent notes database and storage | Fully implemented | High | Medium | Project metadata persistence | PungentNoteDatabase.cs; PungentNoteStorage.cs | Notes and future utility records persist under ProjectSettings/PungentFunkUtilities/NotesAndRoadmap.asset via ScriptableSingleton. |
| Project Audit & Authoring Lab | Persistent token database and bindings | Fully implemented | High | Medium | Text/content metadata validation | PungentTokenDatabase.cs; PungentTokenModels.cs; PungentTokenStorage.cs | Tokens, bindings, archive/deprecate state, examples, allowed contexts, note links, audit issue links, and future utility links are persisted. |
| Project Audit & Authoring Lab | Reference Assignment Scanner | Partially implemented | High | High | Generic project QA | ReferenceAssignmentScannerWindow.cs | Strong tool; monolithic and uses broad scans/resources. |
| Project Audit & Authoring Lab | ScriptableObject generator profile/module base/folder routing | Yet to implement | High | High | SO-heavy project automation | No SO lab files found | Future high-value lab. |
| Project Audit & Authoring Lab | ScriptableObject validation rules/scanning/repair | Yet to implement | High | High | SO QA | No SO lab files found | Should extract patterns, not game-specific ability/gacha tools. |
| Project Audit & Authoring Lab | Terrain Usage Scanner | Partially implemented | High | High | Generic project QA | TerrainUsageScannerWindow.cs | Strong tool; monolithic and scan/cache infra not shared. |
| Project Audit & Authoring Lab | Token Validator | Fully implemented | Medium | Medium | Text/content QA | PungentTokenValidatorWindow.cs plus PungentTokenDatabase, Parser, Storage, ValidatorService, GUI, LinkPopup, ContextMenus | Token validation has expanded into a persistent token database, bindings, parser, notes validation, and context-linking workflow. |
| Project Audit & Authoring Lab | Token link popup and context menus | Fully implemented | Medium | Medium | Context-aware token authoring | PungentTokenContextMenus.cs; PungentTokenLinkPopup.cs; PungentTokenContextResolver.cs | Assets and GameObjects can be linked to tokens from context menus; popup exists for binding creation. |
| Project Audit & Authoring Lab | Token parser, validator service, and usage scanning | Fully implemented | High | Medium | Text/content QA | PungentTokenParser.cs; PungentTokenValidatorService.cs | Brace-token parsing, preview resolution, text validation, notes validation, definition validation, and binding validation are implemented. |
| Project Audit & Authoring Lab | Tooltip Notes Browser | Partially implemented | Medium | Medium | Documentation/authoring aid | PungentNotesRoadmapWindow.cs and Notes subsystem files; PungentTooltipNotesBrowserWindow.cs retained | The old tooltip notes concept has expanded into a Notes & Roadmap workspace with storage, filtering, context menus, imports, audit links, and future utility records. Legacy Tooltip Notes Browser remains for compatibility. |
| Content Generation Lab | Generic token/text/content generation beyond names | Yet to implement | Medium | High | Future content tools | No broader generator framework found | Mentioned in future lab structure. |
| Content Generation Lab | MadLibNameList asset | Fully implemented | Medium | Medium | Content generation core | Name Generator/MadLibNameList.cs | Now uses namespace PungentFunk.Utilities.Content and standard CreateAssetMenu root PungentFunk Utilities/Content Generation/Mad-Lib Name List. |
| Content Generation Lab | Name Generator window | Partially implemented | Medium | Medium | Content generation UI | NameGeneratorWindow.cs | Functional but monolithic-ish; future content/token generation not present. |
| Content Generation Lab | Name presets assets | Fully implemented | Low | Low | Convenience content | Name Generator/Name Presets/*.asset | Boy/girl/nonbinary/community/last names present. |
| Content Generation Lab | NameList asset | Fully implemented | Medium | Low | Content generation core | Name Generator/NameList.cs | Now uses namespace PungentFunk.Utilities.Content and standard CreateAssetMenu root PungentFunk Utilities/Content Generation/Name List. |
| UI & Feedback Lab | Floating Text System | Yet to implement | Medium | Medium | Feedback runtime | No floating text files found | Planned future module. |
| UI & Feedback Lab | Generic progress/resource VisualElements | Yet to implement | Medium | Medium | Runtime UI widgets | No runtime visual elements found | Planned future item. |
| UI & Feedback Lab | Input prompt preview window/editor | Yet to implement | Medium | Medium | Authoring UX | Only auto-fill window exists | Planned UI lab expansion. |
| UI & Feedback Lab | InputPromptIconLibraryAutoFill editor tool | Partially implemented | High | Medium | Current UI lab seed | InputPromptIconLibraryAutoFill.cs | Editor-only populator exists; relies on serialized names rather than runtime model. |
| UI & Feedback Lab | Popup Notification System | Yet to implement | Medium | Medium | Feedback runtime | No popup notification files found | Planned future module. |
| UI & Feedback Lab | Radar/Spider Graph VisualElement | Yet to implement | Low | Medium | Visualization/UI widget | No VisualElement graph found | Planned future UI/Visualization item. |
| UI & Feedback Lab | Right-click Context Menu runtime/editor helper | Yet to implement | Medium | Medium | UI foundation | No generic context menu helper found | Planned future module. |
| UI & Feedback Lab | Runtime PungentInputPromptIconLibrary | Yet to implement | High | Medium | Input prompt runtime core | No runtime input prompt files found | Planned next UI lab start. |
| UI & Feedback Lab | Runtime input prompt resolver/token/presenter | Yet to implement | High | High | Input prompt runtime core | No resolver/token/presenter found | Should remain layout-agnostic and Input System optional/guarded. |
| UI & Feedback Lab | Tooltip Presenter | Yet to implement | Medium | Medium | Feedback runtime | Tooltip notes browser is editor-only | Runtime presenter absent. |
| UI & Feedback Lab | UI Animation Trigger/Profile system | Yet to implement | Medium | High | Feedback authoring | No animation profile files found | Planned future module. |
| UI & Feedback Lab | World Prompt Registry/Presenter | Yet to implement | Medium | High | Reusable interaction UI | No world prompt files found | Must avoid SkiGame quest/interaction dependencies. |
| Environment Simulation Lab | Cloth/Particle/WindZone wind adapters | Yet to implement | Medium | Medium | Unity integration | No wind adapter files found | Planned initial editor/runtime files. |
| Environment Simulation Lab | Environment Simulation Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Environment Simulation Lab | Environment debugger/preview window | Yet to implement | Medium | Medium | Authoring UX | No environment debugger found | Future module. |
| Environment Simulation Lab | Local environment volumes | Yet to implement | Medium | High | Scene simulation | No local volume files found | Future module. |
| Environment Simulation Lab | PungentWindController core | Yet to implement | High | High | Next new lab candidate | No Environment/Wind files found | Recommended next major non-placement suite. |
| Environment Simulation Lab | RenderSettings/URP/skybox/particle/cloud adapters | Yet to implement | Medium | Very High | Optional integrations | No environment adapters found | Must be optional/bridge-based. |
| Environment Simulation Lab | Surface condition broadcaster | Yet to implement | High | High | Placement/audio/gameplay bridge | No broadcaster files found | Future module. |
| Environment Simulation Lab | Time & Calendar core | Yet to implement | Medium | High | Reusable simulation service | No time/calendar files found | Planned later module. |
| Environment Simulation Lab | Weather preset system/transitions | Yet to implement | High | High | Reusable environment authoring | No weather preset files found | Should use adapters, not old WeatherController directly. |
| Environment Simulation Lab | Wind Lab window/controller editor | Yet to implement | Medium | Medium | Authoring UX | No wind lab window/editor found | Planned initial editor files. |
| Environment Simulation Lab | Wind source/weather/time interfaces | Yet to implement | High | Medium | Adapter-friendly environment core | No IPungentWindSource/IPungentWeatherSource/IPungentTimeSource found | Planned in implementation brief. |
| Save & Settings Lab | Generic settings profile | Yet to implement | High | Medium | Runtime infrastructure | No settings profile/service files found | Future lab. |
| Save & Settings Lab | Input binding override storage | Yet to implement | High | Medium | Input/UI bridge | No binding storage found | Future lab; should remain generic. |
| Save & Settings Lab | JSON storage utility/versioning | Yet to implement | High | Medium | Persistence infrastructure | No JSON storage utility found | Future lab. |
| Save & Settings Lab | Save & Settings Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Save & Settings Lab | Save slot manifest/service | Yet to implement | Medium | High | Persistence infrastructure | No save slot service found | Future lab. |
| Save & Settings Lab | Settings service | Yet to implement | High | High | Runtime infrastructure | No settings service found | Future lab. |
| Action / Ability Authoring Lab | Action / Ability Authoring Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Action / Ability Authoring Lab | Action conditions/effects/targeting | Yet to implement | High | Very High | Future reusable gameplay authoring | No generic action condition/effect files found | Future lab. |
| Action / Ability Authoring Lab | Generic action sequence framework | Yet to implement | High | Very High | Future reusable gameplay authoring | No action framework files found | Should be inspired by ability/hazard systems, not directly ported. |
| Action / Ability Authoring Lab | Generic projectile/status/hazard modules | Yet to implement | Medium | Very High | Future gameplay authoring | No modules found | Only if redesigned generically. |
| Combat Move Resolver Lab | MoveDefinition / MoveResolver / ComboState / StyleMeter / context flags | Yet to implement | Medium | High | Future action/trick/combat abstraction | No move resolver files found | Lower priority. |
| Appearance Lab | Appearance catalog validator | Yet to implement | Medium | Medium | Authoring QA | No validator found | Future authoring tool. |
| Appearance Lab | Generic appearance option assets/catalog | Yet to implement | Medium | High | Character/customization tooling | No appearance catalog files found | Could link to prefab icon generator and Colour Lab. |
| Appearance Lab | Wearable attachment/material-color options/option generator | Yet to implement | Medium | High | Customization authoring | No appearance generator files found | Keep shop/progression as project adapters. |
| Agent Simulation Lab | Agent Simulation Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |
| Agent Simulation Lab | Generic needs/traits/emotions/interaction scoring | Yet to implement | Medium | Very High | Generic simulation tools | No agent/social files found | Informed by NPC plans, not ported directly. |
| Agent Simulation Lab | Social group based generic NPC behaviour improvements | Yet to implement | Medium | Very High | Simulation/authoring | No social group files found | Prior SkiGame NPC work should remain project adapter/inspiration. |
| Agent Simulation Lab | Utility AI/state/evaluator framework | Yet to implement | Medium | Very High | Generic simulation tools | No agent files found | Must avoid direct NPC/dialogue/quest dependencies. |
| Visualization Lab | 3D point cloud previews | Yet to implement | Low | High | Advanced visualization | No point cloud preview found | Future visualization idea. |
| Visualization Lab | Radar graphs | Yet to implement | Low | Medium | UI/visualization | No radar graph files found | Could serve UI Feedback Lab. |
| Visualization Lab | Reusable matrix/graph/heatmap/coverage visualizers | Scaffolded | High | High | Shared UI infrastructure | CoverageMatrix, palette analysis, texture preview, placement heatmap concepts | No formal visualization lab/shared widget library yet. |
| Visualization Lab | Visualization Lab shell | Scaffolded | High | High | Future lab taxonomy | PungentUtilityLabs planned constants only | Lab names/descriptions/sort keys exist in PungentUtilityLabs, but no runtime/editor implementation files for this lab yet. |