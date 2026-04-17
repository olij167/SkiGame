using UnityEngine;

namespace SkiGame.Audio
{
    public static class GameAudio
    {
        public static bool PlayUi(GameAudioCueId cueId, float volumeScale = 1f)
        {
            return GameAudioDirector.TryPlayUi(cueId, volumeScale);
        }

        public static bool PlayWorld(GameAudioCueId cueId, Vector3 position, float volumeScale = 1f)
        {
            return GameAudioDirector.TryPlayWorld(cueId, position, volumeScale);
        }

        public static bool PlayWorld(GameAudioCueId cueId, Component source, float volumeScale = 1f)
        {
            return source != null && PlayWorld(cueId, source.transform.position, volumeScale);
        }

        public static bool PlayAttached(GameAudioCueId cueId, Transform target, float volumeScale = 1f)
        {
            return GameAudioDirector.TryPlayAttached(cueId, target, volumeScale);
        }
    }
}
