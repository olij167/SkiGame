using UnityEngine;
using SkiGame.Activities;

[DisallowMultipleComponent]
public sealed class ActivityGameplayHUD : MonoBehaviour
{
    [SerializeField] private bool showHud = true;
    [SerializeField] private Vector2 hudOffset = new Vector2(20f, 20f);
    [SerializeField] private float calloutDuration = 1.75f;
    [SerializeField] private float calloutSublineDuration = 2.4f;

    private string _toastMessage = "";
    private float _toastUntil = 0f;
    private string _calloutHeadline = "";
    private string _calloutSubline = "";
    private Color _calloutColor = Color.white;
    private float _calloutUntil = 0f;
    private bool _calloutFailure;

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
        if (kind == MountainActivityKind.Race)
            return;

        if (kind == MountainActivityKind.Rescue && RescueService.Instance != null)
        {
            RescueService rescue = RescueService.Instance;
            _toastMessage = $"{displayName} completed";

            if (rescue.LastMissionRewardGranted > 0)
                _toastMessage += $"  +${rescue.LastMissionRewardGranted}";

            if (rescue.LastMissionRankIncreased)
                _toastMessage += $"  Rank {Mathf.Max(1, rescue.LastMissionNewRank)}";
        }
        else
        {
            _toastMessage = $"{displayName} completed";
        }

        _toastUntil = Time.unscaledTime + 4f;
    }

    private void HandleFailed(MountainActivityKind kind, MonoBehaviour source, string displayName, string reason)
    {
        if (kind == MountainActivityKind.Race)
            return;

        _toastMessage = string.IsNullOrWhiteSpace(reason)
            ? $"{displayName} failed"
            : $"{displayName} failed: {reason}";
        _toastUntil = Time.unscaledTime + 4f;
    }

    private void ShowCallout(string headline, string subline, Color color, bool failure)
    {
        _calloutHeadline = string.IsNullOrWhiteSpace(headline) ? (failure ? "FAILED" : "COMPLETE") : headline.Trim().ToUpperInvariant();
        _calloutSubline = string.IsNullOrWhiteSpace(subline) ? string.Empty : subline.Trim();
        _calloutColor = color;
        _calloutFailure = failure;
        _calloutUntil = Time.unscaledTime + Mathf.Max(0.5f, failure ? calloutSublineDuration : calloutDuration);
    }

    private static string GetSuccessHeadline(MountainActivityKind kind, MonoBehaviour source)
    {
        if (kind == MountainActivityKind.Race && source is RaceCourseLine race)
        {
            if (race.LastChampionshipCompletedForFirstTime)
                return "Champion";

            if (race.LastResolvedPlacement == 1)
                return "Victory";

            if (race.LastPersonalBestImproved)
                return "Personal Best";

            return "Finished";
        }

        if (kind == MountainActivityKind.Rescue && RescueService.Instance != null && RescueService.Instance.LastMissionRankIncreased)
            return "Promoted";

        return "Complete";
    }

    private static string GetFailureHeadline(MountainActivityKind kind, string reason)
    {
        if (kind == MountainActivityKind.Race)
        {
            if (!string.IsNullOrWhiteSpace(reason) && reason.IndexOf("stack", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Wipeout";

            if (!string.IsNullOrWhiteSpace(reason) && reason.IndexOf("course", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Off Line";
        }

        if (kind == MountainActivityKind.Rescue)
            return "Botched";

        return "Failed";
    }

    private static Color GetSuccessColor(MountainActivityKind kind)
    {
        return kind == MountainActivityKind.Rescue
            ? new Color(0.58f, 0.92f, 1f, 1f)
            : new Color(0.49f, 1f, 0.58f, 1f);
    }

    private static Color GetFailureColor(MountainActivityKind kind)
    {
        return kind == MountainActivityKind.Rescue
            ? new Color(1f, 0.66f, 0.52f, 1f)
            : new Color(1f, 0.36f, 0.36f, 1f);
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
            if (mgr.ActiveKind == MountainActivityKind.Rescue && RescueService.Instance != null && RescueService.Instance.HasActiveMission)
            {
                var rescue = RescueService.Instance;
                GUILayout.BeginArea(new Rect(x, y, 320f, 170f), GUI.skin.window);
                GUILayout.Label("Rescue Mission");
                GUILayout.Label(rescue.IsExactLocationMission ? "Mode: Exact location" : "Mode: Search area");
                GUILayout.Label($"Remaining casualties: {rescue.RemainingCasualties}");
                GUILayout.Label($"Time left: {Mathf.Max(0f, rescue.TimeRemainingSeconds):0}s");

                if (rescue.IsSearchAreaMission)
                    GUILayout.Label($"Search radius: {rescue.SearchAreaRadius:0}m");

                GUILayout.EndArea();
            }
        }

        if (!string.IsNullOrWhiteSpace(_toastMessage) && Time.unscaledTime < _toastUntil)
        {
            GUILayout.BeginArea(new Rect(x, Screen.height - 90f, 460f, 50f), GUI.skin.window);
            GUILayout.Label(_toastMessage);
            GUILayout.EndArea();
        }

        DrawCenterCallout();
    }

    private void DrawCenterCallout()
    {
        if (string.IsNullOrWhiteSpace(_calloutHeadline) || Time.unscaledTime >= _calloutUntil)
            return;

        float remaining01 = Mathf.Clamp01((_calloutUntil - Time.unscaledTime) / Mathf.Max(0.01f, _calloutFailure ? calloutSublineDuration : calloutDuration));
        float intro01 = 1f - Mathf.Clamp01((Time.unscaledTime - (_calloutUntil - (_calloutFailure ? calloutSublineDuration : calloutDuration))) / 0.18f);
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * (_calloutFailure ? 18f : 14f)) * (_calloutFailure ? 0.04f : 0.03f);
        float scale = Mathf.Lerp(1f, 1.24f, intro01) * pulse;
        float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - intro01)) * Mathf.SmoothStep(0f, 1f, remaining01);

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.34f;

        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

        var headlineStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = _calloutFailure ? 54 : 58,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(_calloutColor.r, _calloutColor.g, _calloutColor.b, alpha) }
        };

        var shadowStyle = new GUIStyle(headlineStyle);
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.45f);

        var sublineStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 1f, 1f, alpha * 0.92f) }
        };

        float invScale = 1f / scale;
        Rect headlineRect = new Rect((centerX - 360f) * invScale, (centerY - 50f) * invScale, 720f * invScale, 88f * invScale);
        Rect shadowRect = headlineRect;
        shadowRect.position += new Vector2(4f * invScale, 4f * invScale);
        Rect sublineRect = new Rect((centerX - 320f) * invScale, (centerY + 22f) * invScale, 640f * invScale, 40f * invScale);

        GUI.Label(shadowRect, _calloutHeadline, shadowStyle);
        GUI.Label(headlineRect, _calloutHeadline, headlineStyle);
        GUI.Label(sublineRect, _calloutSubline, sublineStyle);

        GUI.matrix = Matrix4x4.identity;
    }
}
