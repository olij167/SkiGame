using UnityEngine;
using UnityEngine.Playables;

namespace PungentFunk.Utilities.EnvironmentSimulation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PungentFunk/Environment Simulation/Adapters/Pungent Delta Time Playable Director Adapter")]
    public sealed class PungentDeltaTimePlayableDirectorAdapter : MonoBehaviour
    {
        [SerializeField] private PungentDeltaTimeController deltaTime;
        [SerializeField] private PlayableDirector director;
        [SerializeField] private PungentTimeChannel channel = PungentTimeChannel.Gameplay;
        [SerializeField] private string customChannel;
        [SerializeField] private bool setRootPlayableSpeed = true;
        [SerializeField] private bool pauseDirectorWhenScaleIsZero = true;
        [SerializeField] private bool resumeDirectorWhenScaleRestored = true;

        private bool _pausedByAdapter;

        private void OnEnable()
        {
            if (director == null)
                director = GetComponent<PlayableDirector>();
            if (deltaTime == null)
                deltaTime = PungentDeltaTimeController.Instance != null ? PungentDeltaTimeController.Instance : FindAnyObjectByType<PungentDeltaTimeController>();
        }

        private void LateUpdate()
        {
            Apply();
        }

        public void Apply()
        {
            if (deltaTime == null || director == null)
                return;

            float scale = deltaTime.GetScale(channel, customChannel);
            if (pauseDirectorWhenScaleIsZero && scale <= 0.0001f && director.state == PlayState.Playing)
            {
                director.Pause();
                _pausedByAdapter = true;
            }
            else if (resumeDirectorWhenScaleRestored && _pausedByAdapter && scale > 0.0001f && director.state != PlayState.Playing)
            {
                director.Play();
                _pausedByAdapter = false;
            }

            if (setRootPlayableSpeed)
                SetPlayableGraphSpeed(scale);
        }

        private void SetPlayableGraphSpeed(float scale)
        {
            PlayableGraph graph = director.playableGraph;
            if (!graph.IsValid())
                return;

            int rootCount = graph.GetRootPlayableCount();
            for (int i = 0; i < rootCount; i++)
            {
                Playable playable = graph.GetRootPlayable(i);
                if (playable.IsValid())
                    playable.SetSpeed(Mathf.Max(0f, scale));
            }
        }
    }
}
