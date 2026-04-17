using UnityEngine;

namespace SkiGame.Progression
{
    public sealed class PlayerStatsDebugPanel : MonoBehaviour
    {
        [SerializeField] private bool showOnStart = true;

        private bool _visible;

        private void Awake()
        {
            _visible = showOnStart;
        }

        private void Update()
        {
            // Intentionally empty. New Input System project.
        }

        public void SetVisible(bool visible) => _visible = visible;
        public void ToggleVisible() => _visible = !_visible;

        private void OnGUI()
        {
            if (!_visible) return;

            var mgr = PlayerStatsManager.Instance;
            if (mgr == null)
            {
                GUI.Label(new Rect(10, 10, 400, 20), "PlayerStatsManager not found in scene.");
                return;
            }

            var p = mgr.Profile;
            if (p == null)
            {
                GUI.Label(new Rect(10, 10, 400, 20), "Profile is null (not loaded).");
                return;
            }

            const int w = 420;
            const int h = 320;

            GUILayout.BeginArea(new Rect(10, 10, w, h), GUI.skin.box);
            GUILayout.Label("PLAYER STATS (Phase 1 Debug)");
            GUILayout.Space(6);

            GUILayout.Label($"Path: {mgr.ActivePath}");
            GUILayout.Label($"Version: {p.profileVersion}");
            GUILayout.Label($"Created: {p.createdUtc}");
            GUILayout.Label($"Last Saved: {p.lastSavedUtc}");
            GUILayout.Space(8);

            GUILayout.Label("Lifetime:");
            GUILayout.Label($"  Distance: {p.lifetime.totalDistanceMeters:0} m");
            GUILayout.Label($"  Ascent: {p.lifetime.totalVerticalAscentMeters:0} m | Descent: {p.lifetime.totalVerticalDescentMeters:0} m");
            GUILayout.Label($"  Top Speed: {p.lifetime.topSpeedMps:0.00} m/s | Avg: {p.lifetime.AverageSpeedMps:0.00} m/s");
            GUILayout.Label($"  Airtime: {p.lifetime.totalAirTimeSeconds:0.00} s | Air Dist: {p.lifetime.totalAirDistanceMeters:0} m");
            GUILayout.Label($"  Stacks: {p.lifetime.totalStacks} | Runs: {p.lifetime.totalRunsCompleted} | Lifts: {p.lifetime.totalLiftsUsed}");
            GUILayout.Space(8);

            GUILayout.Label("Session:");
            GUILayout.Label($"  Distance: {p.session.distanceMeters:0} m");
            GUILayout.Label($"  Ascent: {p.session.verticalAscentMeters:0} m | Descent: {p.session.verticalDescentMeters:0} m");
            GUILayout.Label($"  Top Speed: {p.session.topSpeedMps:0.00} m/s | Avg: {p.session.AverageSpeedMps:0.00} m/s");
            GUILayout.Label($"  Airtime: {p.session.airTimeSeconds:0.00} s | Air Dist: {p.session.airDistanceMeters:0} m");
            GUILayout.Label($"  Stacks: {p.session.stacks} | Runs: {p.session.runsCompleted} | Lifts: {p.session.liftsUsed}");
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(28))) mgr.Save();
            if (GUILayout.Button("Load", GUILayout.Height(28))) mgr.Load();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Session", GUILayout.Height(28))) mgr.ResetSession();
            if (GUILayout.Button("Reset All", GUILayout.Height(28))) mgr.ResetAll();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
