using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoadService
{
    public readonly struct Request
    {
        public Request(string targetSceneName)
        {
            TargetSceneName = targetSceneName;
        }

        public string TargetSceneName { get; }
    }

    public const string BootSceneName = "Boot";
    public const string LoadingSceneName = "Loading";
    public const string MenuSceneName = "Menu";

    private static Request? _pendingRequest;

    public static bool HasPendingRequest => _pendingRequest.HasValue;

    public static void LoadScene(string targetSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            throw new ArgumentException("Target scene name must be provided.", nameof(targetSceneName));
        }

        _pendingRequest = new Request(targetSceneName);
        SceneManager.LoadScene(LoadingSceneName, LoadSceneMode.Single);
    }

    public static bool TryConsumePendingRequest(out Request request)
    {
        if (_pendingRequest.HasValue)
        {
            request = _pendingRequest.Value;
            _pendingRequest = null;
            return true;
        }

        request = new Request(MenuSceneName);
        return false;
    }
}
