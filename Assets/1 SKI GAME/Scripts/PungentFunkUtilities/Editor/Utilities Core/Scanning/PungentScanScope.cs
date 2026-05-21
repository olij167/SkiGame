namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    /// <summary>
    /// Broad scan-scope labels used by scanner-heavy tools. Individual tools may expose richer local options.
    /// </summary>
    public enum PungentScanScope
    {
        Manual = 0,
        Selection = 1,
        ActiveScene = 2,
        OpenScenes = 3,
        ProjectScenes = 4,
        ProjectPrefabs = 5,
        ProjectAssets = 6,
        Custom = 7
    }
#endif
}
