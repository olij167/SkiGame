using UnityEngine;

public sealed class BootSceneController : MonoBehaviour
{
    private void Start()
    {
        SceneLoadService.LoadScene(SceneLoadService.MenuSceneName);
    }
}
