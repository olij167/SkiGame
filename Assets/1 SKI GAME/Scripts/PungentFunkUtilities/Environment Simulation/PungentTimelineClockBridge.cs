using UnityEngine;
using UnityEngine.Playables;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Timeline Clock Bridge")]
    public sealed class PungentTimelineClockBridge : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private PungentClockController clock;
        [SerializeField] private bool driveInEditMode;
        [SerializeField] private bool onlyWhileDirectorPlaying = true;
        [SerializeField, Range(0f, 24f)] private float startHour;
        [SerializeField, Range(0f, 24f)] private float endHour = 24f;

        private void OnEnable()
        {
            if (director == null)
                director = GetComponent<PlayableDirector>();
            if (clock == null)
                clock = FindAnyObjectByType<PungentClockController>();
        }

        private void Update()
        {
            if (director == null || clock == null)
                return;
            if (!Application.isPlaying && !driveInEditMode)
                return;
            if (onlyWhileDirectorPlaying && director.state != PlayState.Playing)
                return;

            ApplyDirectorTime();
        }

        public void ApplyDirectorTime()
        {
            if (director == null || clock == null)
                return;

            double duration = director.duration;
            float t = duration > 0.0001d ? Mathf.Clamp01((float)(director.time / duration)) : 0f;
            float hour = Mathf.Lerp(startHour, endHour, t);
            clock.SetHour(hour);
        }
    }
}
