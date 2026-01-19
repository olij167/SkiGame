using UnityEngine;

namespace SkiGame.Progression
{
    [DisallowMultipleComponent]
    public sealed class GrindStatsRecorder : MonoBehaviour
    {
        [SerializeField] private SkiController skiController;
        [SerializeField] private Rigidbody rb;

        private Vector3 _lastPos;
        private bool _hadInit;

        private void Reset()
        {
            skiController = GetComponent<SkiController>();
            rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            _hadInit = false;
        }

        private void FixedUpdate()
        {
            if (skiController == null || rb == null)
                return;

            var mgr = PlayerStatsManager.Instance;
            var profile = mgr != null ? mgr.Profile : null;
            if (profile == null)
                return;

            Vector3 pos = rb.position;

            if (!_hadInit)
            {
                _hadInit = true;
                _lastPos = pos;
                return;
            }

            if (!skiController.IsGrinding)
            {
                _lastPos = pos;
                return;
            }

            float dt = Time.fixedDeltaTime;
            float dist = Vector3.Distance(pos, _lastPos);

            // Session
            profile.session.grindTimeSeconds += dt;
            profile.session.grindDistanceMeters += dist;

            // Lifetime
            profile.lifetime.totalGrindTimeSeconds += dt;
            profile.lifetime.totalGrindDistanceMeters += dist;

            _lastPos = pos;
        }
    }
}
