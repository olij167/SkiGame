using System;
using PungentFunk.Utilities.SceneTools;
using UnityEngine;

/// <summary>
/// Legacy compatibility component for existing scenes/prefabs.
///
/// New content should use ModularPathSpawner directly. Existing FencePath components inherit the
/// generic implementation so old scene references remain valid while the toolchain moves away
/// from fence-specific terminology.
/// </summary>
[Obsolete("FencePath is a legacy compatibility wrapper. Use ModularPathSpawner for new paths.")]
[AddComponentMenu("Utilities/Legacy/Fence Path (Compatibility Wrapper)")]
public class FencePath : ModularPathSpawner
{
}
