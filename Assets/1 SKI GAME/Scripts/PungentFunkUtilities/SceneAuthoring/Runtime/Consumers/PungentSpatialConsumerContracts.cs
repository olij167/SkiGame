using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public interface IPungentSpatialConsumer
    {
        string ConsumerId { get; }
        Component SpatialSource { get; }
        bool HasValidSpatialSource { get; }
    }

    public interface IPungentPathConsumer : IPungentSpatialConsumer
    {
        Component PathSource { get; }
    }

    public interface IPungentAreaConsumer : IPungentSpatialConsumer
    {
        Component AreaSource { get; }
    }

    public interface IPungentSpatialQueryProvider
    {
        int CopyPathSources(IList<Component> results);
        int CopyAreaSources(IList<Component> results);
    }

    public interface IPungentSpatialApplier
    {
        bool CanApplySpatialData { get; }
        string ApplyLabel { get; }
        void ApplySpatialData();
    }

    public interface IPungentSpatialPreviewProvider
    {
        bool TryGetPreviewBounds(out Bounds bounds);
        bool TryGetPreviewSummary(out string summary);
    }

    public interface IPungentSpatialValidationProvider
    {
        int GetSpatialValidationIssues(IList<PungentSpatialValidationIssue> results);
    }
}
