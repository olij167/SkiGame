using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Placement;

namespace PungentFunk.Utilities.Editor.Placement
{
    #if UNITY_EDITOR
    using UnityEditor;

    [InitializeOnLoad]
    public static class PungentAssetPlacementUtilitiesRegistrar
    {
        static PungentAssetPlacementUtilitiesRegistrar()
        {
            PungentUtilityRegistry.Register(new PungentUtilityDescriptor(
                "asset-placement-lab",
                "Asset Placement Lab",
                "Placement",
                "Scatter, grid-snap, validate sockets, and manage generated placement groups using modular project-agnostic placement workflows.",
                "Tools/Utilities/Placement/Asset Placement Lab",
                nameof(AssetPlacementLabWindow),
                new[] { "placement", "scatter", "prefab", "grid", "socket", "generation", "scene", "lab" },
                supportsSceneOverlay: true,
                supportsContextMenu: true,
                supportsSelection: true,
                lab: PungentUtilityLabs.AssetPlacementLab,
                module: "Lab Hub",
                sortOrder: 0,
                isLabHub: true,
                packageStatus: PungentUtilityPackageStatus.InProgress,
                relatedUtilityIds: new[] { "surface-align", "modular-path-builder", "path-authoring-toolkit", "design-validation-audit" }));
        }
    }
    #endif

}