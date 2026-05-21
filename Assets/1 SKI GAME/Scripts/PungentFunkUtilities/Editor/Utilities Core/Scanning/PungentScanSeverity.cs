namespace PungentFunk.Utilities.Editor.Scanning
{
#if UNITY_EDITOR
    /// <summary>
    /// Shared severity contract for project-audit, coverage, validation, and setup scanners.
    /// </summary>
    public enum PungentScanSeverity
    {
        None = 0,
        Info = 1,
        Success = 2,
        Warning = 3,
        Error = 4
    }
#endif
}
