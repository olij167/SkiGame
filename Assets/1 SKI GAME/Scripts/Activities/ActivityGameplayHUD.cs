using UnityEngine;
using SkiGame.Activities;

[DisallowMultipleComponent]
public sealed class ActivityGameplayHUD : MonoBehaviour
{
    [SerializeField] private bool showHud = true;
    [SerializeField] private Vector2 hudOffset = new Vector2(20f, 20f);

    private string _toastMessage = "";
    private float _toastUntil = 0f;

    private MountainActivityManager _boundActivityManager;

    private void OnEnable()
    {
        TryBindActivityManager();
    }

    private void OnDisable()
    {
        UnbindActivityManager();
    }

    private void Update()
    {
        TryBindActivityManager();
    }

    private void TryBindActivityManager()
    {
        var mgr = MountainActivityManager.Instance;
        if (mgr == null || _boundActivityManager == mgr)
            return;

        UnbindActivityManager();

        _boundActivityManager = mgr;
        _boundActivityManager.OnActivityCompleted += HandleCompleted;
        _boundActivityManager.OnActivityFailed += HandleFailed;
    }

    private void UnbindActivityManager()
    {
        if (_boundActivityManager == null)
            return;

        _boundActivityManager.OnActivityCompleted -= HandleCompleted;
        _boundActivityManager.OnActivityFailed -= HandleFailed;
        _boundActivityManager = null;
    }

    private void HandleCompleted(MountainActivityKind kind, MonoBehaviour source, string displayName)
    {
        _toastMessage = $"{displayName} completed";
        _toastUntil = Time.unscaledTime + 4f;
    }

    private void HandleFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
    {
        _toastMessage = string.IsNullOrWhiteSpace(reason)
            ? $"{displayName} failed"
            : $"{displayName} failed: {reason}";
        _toastUntil = Time.unscaledTime + 4f;
    }

    private void OnGUI()
    {
        if (!showHud)
            return;

        float x = hudOffset.x;
        float y = hudOffset.y;

        var mgr = MountainActivityManager.Instance;
        if (mgr != null && mgr.HasActiveActivity)
        {
            if (mgr.ActiveKind == MountainActivityKind.Race && mgr.ActiveSource is RaceCourseLine race)
            {
                GUILayout.BeginArea(new Rect(x, y, 320f, 150f), GUI.skin.window);
                GUILayout.Label(race.RaceName);
                GUILayout.Label($"League: {race.GetLeagueDisplayName(race.ActiveLeagueNumber)}");

                if (race.IsCountdownActive)
                    GUILayout.Label($"Countdown: {Mathf.CeilToInt(race.CountdownRemainingSeconds)}");
                else if (race.IsRaceInProgress)
                    GUILayout.Label($"Time: {race.AttemptTimeSeconds:0.00}s");

                GUILayout.Label($"Checkpoint: {Mathf.Min(race.CurrentCheckpointIndex + 1, Mathf.Max(1, race.CheckpointCount))} / {Mathf.Max(1, race.CheckpointCount)}");

                if (race.LastResolvedPlacement > 0 && race.LastResolvedEntrantCount > 0)
                    GUILayout.Label($"Last Result: {race.LastResolvedPlacement} / {race.LastResolvedEntrantCount}");
                else if (RaceActivityService.Instance != null)
                    GUILayout.Label($"NPC Finished: {RaceActivityService.Instance.FinishedNpcCount} / {RaceActivityService.Instance.ActiveNpcCount}");

                GUILayout.EndArea();
            }
            else if (mgr.ActiveKind == MountainActivityKind.Rescue && RescueService.Instance != null && RescueService.Instance.HasActiveMission)
            {
                var rescue = RescueService.Instance;
                GUILayout.BeginArea(new Rect(x, y, 320f, 150f), GUI.skin.window);
                GUILayout.Label("Rescue Mission");
                GUILayout.Label($"Remaining casualties: {rescue.RemainingCasualties}");
                GUILayout.Label($"Time left: {Mathf.Max(0f, rescue.TimeRemainingSeconds):0}s");
                GUILayout.EndArea();
            }
        }

        if (!string.IsNullOrWhiteSpace(_toastMessage) && Time.unscaledTime < _toastUntil)
        {
            GUILayout.BeginArea(new Rect(x, Screen.height - 90f, 460f, 50f), GUI.skin.window);
            GUILayout.Label(_toastMessage);
            GUILayout.EndArea();
        }
    }
}