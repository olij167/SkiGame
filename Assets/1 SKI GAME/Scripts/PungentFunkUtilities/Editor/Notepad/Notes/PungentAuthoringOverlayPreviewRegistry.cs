using System.Collections.Generic;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Authoring;
    using PungentFunk.Utilities.Editor.Authoring;

    internal interface IPungentAuthoringOverlayPreviewDrawer
    {
        bool CanDraw(PungentAuthoringReference reference);
        void Draw(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state);
    }

    internal static class PungentAuthoringOverlayPreviewRegistry
    {
        private static readonly List<IPungentAuthoringOverlayPreviewDrawer> Drawers = new List<IPungentAuthoringOverlayPreviewDrawer>();

        public static void Register(IPungentAuthoringOverlayPreviewDrawer drawer)
        {
            if (drawer == null || Drawers.Contains(drawer))
                return;

            Drawers.Add(drawer);
        }

        public static bool TryDraw(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state)
        {
            if (reference == null || state == null)
                return false;

            for (int i = 0; i < Drawers.Count; i++)
            {
                IPungentAuthoringOverlayPreviewDrawer drawer = Drawers[i];
                if (drawer == null || !drawer.CanDraw(reference))
                    continue;

                drawer.Draw(reference, preview, state);
                return true;
            }

            return false;
        }
    }
#endif
}
