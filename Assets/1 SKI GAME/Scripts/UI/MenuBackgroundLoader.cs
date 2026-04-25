using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SkiGame.Progression;

public sealed class MenuBackgroundLoader : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "SkiResort";
    [SerializeField] private string menuSceneName = "Menu";

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument document;
    [SerializeField] private string backgroundElementName = "Background"; // matches your UXML name

    [Header("RenderTexture")]
    [SerializeField] private int rtWidth = 1280;
    [SerializeField] private int rtHeight = 720;
    [SerializeField] private int rtDepth = 16;
    [SerializeField] private RenderTextureFormat rtFormat = RenderTextureFormat.ARGB32;

    [Header("Camera Selection")]
    [SerializeField] private bool randomizeCamera = true;
    [SerializeField] private int fixedCameraIndex = 0;

    [Header("Performance")]
    [SerializeField] private int targetFps = 12;
    [SerializeField] private int startupDelayFrames = 1;
    [SerializeField] private float startupDelaySeconds = 0.1f;
    [SerializeField] private int asyncLoadPriority = 0;
    [SerializeField] private bool usePlaceholderWhileLoading = true;
    [SerializeField] private Texture2D placeholderTexture;

    private RenderTexture _rt;
    private VisualElement _bgElement;
    private Scene _bgScene;
    private MenuBackgroundCameraGroup _group;
    private Camera _activeCam;
    private Coroutine _throttleRoutine;

    public bool IsBackgroundReady => _group != null;

    private void Reset()
    {
        document = GetComponent<UIDocument>();
    }

    private void Awake()
    {
        if (document == null) document = FindObjectOfType<UIDocument>();
    }

    private void Start()
    {
        _bgElement = document != null ? document.rootVisualElement.Q<VisualElement>(backgroundElementName) : null;

        if (_bgElement != null && usePlaceholderWhileLoading && placeholderTexture != null)
        {
            _bgElement.style.backgroundImage = new StyleBackground(placeholderTexture);
            _bgElement.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }

        StartCoroutine(BeginBackgroundLoadRoutine());
    }

    private IEnumerator BeginBackgroundLoadRoutine()
    {
        int delayFrames = Mathf.Max(0, startupDelayFrames);
        for (int i = 0; i < delayFrames; i++)
            yield return null;

        if (startupDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(startupDelaySeconds);

        yield return LoadBackgroundSceneRoutine();
    }

    private IEnumerator LoadBackgroundSceneRoutine()
    {
        RuntimeSceneLoadContext.EnterMenuBackgroundPreview();

        if (!IsSceneLoaded(gameSceneName))
        {
            var previousPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.Low;

            var op = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Additive);
            if (op != null)
                op.priority = asyncLoadPriority;

            while (op != null && !op.isDone)
                yield return null;

            Application.backgroundLoadingPriority = previousPriority;
        }

        _bgScene = SceneManager.GetSceneByName(gameSceneName);

        _group = FindInScene<MenuBackgroundCameraGroup>(_bgScene);
        if (_group == null)
        {
            Debug.LogWarning($"[MenuBackgroundLoader] No MenuBackgroundCameraGroup found in scene '{gameSceneName}'.");
            yield break;
        }

        _group.EnterMenuMode();

        EnsureRT();

        int index = randomizeCamera ? Random.Range(0, Mathf.Max(1, _group.CameraCount)) : fixedCameraIndex;
        _activeCam = _group.ActivateCamera(index);
        if (_activeCam == null)
        {
            Debug.LogWarning("[MenuBackgroundLoader] Failed to activate background camera.");
            yield break;
        }

        _activeCam.targetTexture = _rt;
        _activeCam.enabled = false;

        if (_throttleRoutine != null)
            StopCoroutine(_throttleRoutine);

        _throttleRoutine = StartCoroutine(ThrottleCameraRender(_activeCam, targetFps));

        if (_bgElement != null)
        {
            _bgElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_rt));
            _bgElement.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }
    }

    public IEnumerator EnterGameplayAndUnloadMenu(string menuSceneNameToUnload)
    {
        // Ensure background scene has loaded
        while (!IsBackgroundReady)
            yield return null;

        // Stop menu camera rendering
        if (_throttleRoutine != null)
        {
            StopCoroutine(_throttleRoutine);
            _throttleRoutine = null;
        }

        if (_activeCam != null)
        {
            _activeCam.enabled = false;
            _activeCam.targetTexture = null;
        }

        // Clear UI background so it doesn't hold the RT
        if (_bgElement != null)
            _bgElement.style.backgroundImage = StyleKeyword.None;

        // Switch background scene into gameplay mode (enables roots + disables menu cams)
        _group.ExitMenuMode();

        // Make the gameplay scene the active scene (so Instantiate/Find works as expected)
        if (_bgScene.IsValid() && _bgScene.isLoaded)
            SceneManager.SetActiveScene(_bgScene);

        // Tell gameplay systems they may now fully initialize.
        RuntimeSceneLoadContext.ExitMenuBackgroundPreview();

        // Apply pending load/new now that gameplay roots are enabled
        GameStartBootstrap.ApplyPendingNowIfReady();

        // Rehome CrossFadeAudio from menu scene -> game scene, parent under player camera
        var menuScene = SceneManager.GetSceneByName(menuSceneName);
        var gameScene = SceneManager.GetSceneByName(gameSceneName);

        var gameplayCam = FindGameplayCamera();
        if (gameplayCam != null)
            RehomeWeatherAudio(menuScene, gameScene, gameplayCam.transform);

        // Wait one frame so OnProfileLoaded listeners (e.g. PlayerCustomizationApplier) can run their first deferred apply
        yield return null;

        // Unload main menu scene
        if (!string.IsNullOrEmpty(menuSceneNameToUnload) && SceneManager.GetSceneByName(menuSceneNameToUnload).isLoaded)
            yield return SceneManager.UnloadSceneAsync(menuSceneNameToUnload);

        // Release RT (optional – frees memory; background no longer needed)
        ReleaseRT();
    }

    private IEnumerator ThrottleCameraRender(Camera cam, int fps)
    {
        if (cam == null || fps <= 0)
            yield break;

        float interval = 1f / fps;

        // Let the UI settle first.
        yield return null;

        while (cam != null && cam.targetTexture == _rt)
        {
            cam.Render();
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void EnsureRT()
    {
        if (_rt != null) return;

        _rt = new RenderTexture(rtWidth, rtHeight, rtDepth, rtFormat)
        {
            name = "MenuBackgroundRT",
            antiAliasing = 1
        };
        _rt.Create();
    }

    private void ReleaseRT()
    {
        if (_rt == null) return;
        _rt.Release();
        Destroy(_rt);
        _rt = null;
    }

    private static bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name == sceneName && s.isLoaded) return true;
        }
        return false;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var t = roots[i].GetComponentInChildren<T>(true);
            if (t != null) return t;
        }
        return null;
    }

    private Camera FindGameplayCamera()
    {
        // After gameplay is enabled, Camera.main should resolve to your player camera if tagged.
        var cam = Camera.main;
        if (cam != null) return cam;

        // Fallback: any enabled camera that is not one of the menu background cams.
        var all = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (!all[i].enabled) continue;

            // If your MenuBackgroundCameraGroup exposes the menu cameras, prefer excluding them,
            // but we can’t assume that API exists here. So just take the first enabled camera.
            return all[i];
        }
        return null;
    }

    private void RehomeWeatherAudio(Scene menuScene, Scene gameScene, Transform newParent)
    {
        if (!menuScene.IsValid() || !menuScene.isLoaded) return;
        if (!gameScene.IsValid() || !gameScene.isLoaded) return;
        if (newParent == null) return;

        // Find CrossFadeAudio instances that are currently owned by the menu scene
        // (this includes ones parented under menu cameras).
        var roots = menuScene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            if (roots[r] == null) continue;

            var audios = roots[r].GetComponentsInChildren<TimeWeather.CrossFadeAudio>(true);
            for (int i = 0; i < audios.Length; i++)
            {
                var cfa = audios[i];
                if (cfa == null) continue;

                var go = cfa.gameObject;

                // Move to the Game scene so it won’t be destroyed with the menu scene unload.
                SceneManager.MoveGameObjectToScene(go, gameScene);

                // Parent to the gameplay camera (or whatever you pass in)
                go.transform.SetParent(newParent, false);
            }
        }

        // Make sure WeatherController points at the (now moved) CrossFadeAudio
        // If multiple exist, choose the first under the gameplay camera.
        var wc = FindObjectOfType<TimeWeather.WeatherController>(true);
        if (wc != null)
        {
            var cfaOnCam = newParent.GetComponentInChildren<TimeWeather.CrossFadeAudio>(true);
            if (cfaOnCam != null)
                wc.weatherAudio = cfaOnCam;
        }
    }

    private void OnDestroy()
    {
        if (_throttleRoutine != null)
        {
            StopCoroutine(_throttleRoutine);
            _throttleRoutine = null;
        }

        if (_activeCam != null)
        {
            if (_activeCam.targetTexture == _rt)
                _activeCam.targetTexture = null;

            _activeCam.enabled = false;
        }

        ReleaseRT();
    }
}
