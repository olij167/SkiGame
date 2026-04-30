using System.Collections.Generic;

namespace SkiGame.UI
{
    public static class WorldInteractionPromptRegistry
    {
        private static readonly HashSet<IWorldInteractionPromptSource> Sources = new();

        public static void Register(IWorldInteractionPromptSource source)
        {
            if (source != null)
                Sources.Add(source);
        }

        public static void Unregister(IWorldInteractionPromptSource source)
        {
            if (source != null)
                Sources.Remove(source);
        }

        public static void GetSources(List<IWorldInteractionPromptSource> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            foreach (var source in Sources)
            {
                if (source != null)
                    buffer.Add(source);
            }
        }

        public static bool HasRegisteredSources => Sources.Count > 0;
    }
}
