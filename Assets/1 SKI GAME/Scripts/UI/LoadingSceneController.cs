using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LoadingSceneController : MonoBehaviour
{
    private const float RevealDelaySeconds = 0.15f;
    private const float MinimumVisibleSeconds = 0.2f;
    private const float ReadyProgressThreshold = 0.9f;
    private const float BackdropAlpha = 0.92f;

    private readonly string[] _loadingFrames = { "Loading", "Loading.", "Loading..", "Loading..." };

    private AsyncOperation _loadOperation;
    private string _targetSceneName;
    private bool _isVisible;
    private float _visibleSinceUnscaledTime;
    private GUIStyle _labelStyle;
    private Texture2D _backdropTexture;

    private void Start()
    {
        if (!SceneLoadService.TryConsumePendingRequest(out SceneLoadService.Request request))
        {
            Debug.LogWarning("[LoadingSceneController] No pending load request was found. Falling back to Menu.");
        }

        _targetSceneName = request.TargetSceneName;
        StartCoroutine(LoadTargetSceneRoutine());
    }

    private IEnumerator LoadTargetSceneRoutine()
    {
        _loadOperation = SceneManager.LoadSceneAsync(_targetSceneName, LoadSceneMode.Single);

        if (_loadOperation == null)
        {
            Debug.LogError($"[LoadingSceneController] Failed to begin loading scene '{_targetSceneName}'.");
            yield break;
        }

        _loadOperation.allowSceneActivation = false;

        float startTime = Time.unscaledTime;

        while (!_loadOperation.isDone)
        {
            bool isReadyToActivate = _loadOperation.progress >= ReadyProgressThreshold;
            bool shouldReveal = !_isVisible && !isReadyToActivate && (Time.unscaledTime - startTime) >= RevealDelaySeconds;

            if (shouldReveal)
            {
                _isVisible = true;
                _visibleSinceUnscaledTime = Time.unscaledTime;
            }

            if (isReadyToActivate)
            {
                if (!_isVisible)
                {
                    _loadOperation.allowSceneActivation = true;
                    yield break;
                }

                float visibleDuration = Time.unscaledTime - _visibleSinceUnscaledTime;
                if (visibleDuration >= MinimumVisibleSeconds)
                {
                    _loadOperation.allowSceneActivation = true;
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void OnGUI()
    {
        if (!_isVisible)
        {
            return;
        }

        EnsureGuiResources();

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, BackdropAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _backdropTexture);
        GUI.color = previousColor;

        int frameIndex = Mathf.FloorToInt(Time.unscaledTime * 6f) % _loadingFrames.Length;
        string label = _loadingFrames[frameIndex];
        Vector2 size = _labelStyle.CalcSize(new GUIContent(label));
        Rect rect = new Rect(
            (Screen.width - size.x) * 0.5f,
            (Screen.height - size.y) * 0.5f,
            size.x,
            size.y);

        GUI.Label(rect, label, _labelStyle);
    }

    private void EnsureGuiResources()
    {
        if (_backdropTexture == null)
        {
            _backdropTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "LoadingBackdrop"
            };
            _backdropTexture.SetPixel(0, 0, Color.white);
            _backdropTexture.Apply(false, true);
        }

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                richText = false
            };
            _labelStyle.normal.textColor = Color.white;
        }
    }

    private void OnDestroy()
    {
        if (_backdropTexture != null)
        {
            Destroy(_backdropTexture);
            _backdropTexture = null;
        }
    }
}
