using UnityEngine;

public sealed class MenuBackgroundCameraGroup : MonoBehaviour
{
    [Header("Background Cameras (only one enabled at a time)")]
    [SerializeField] private Camera[] cameras;

    [Header("Optional: Disable these roots while in menu")]
    [SerializeField] private GameObject[] disableWhileInMenu;

    private int _activeIndex = -1;

    public int CameraCount => cameras != null ? cameras.Length : 0;

    private void Awake()
    {
        // Default to menu mode; loader will pick a camera.
        EnterMenuMode();
    }

    public void EnterMenuMode()
    {
        SetAllCamerasEnabled(false);
        SetGameplayEnabled(false);
    }

    public void ExitMenuMode()
    {
        // Stop menu cameras entirely; gameplay uses its own main camera
        SetAllCamerasEnabled(false);
        SetGameplayEnabled(true);
    }

    public Camera ActivateCamera(int index)
    {
        if (cameras == null || cameras.Length == 0) return null;

        index = Mathf.Clamp(index, 0, cameras.Length - 1);

        if (_activeIndex == index && cameras[index] != null && cameras[index].enabled)
            return cameras[index];

        SetAllCamerasEnabled(false);
        _activeIndex = index;

        var cam = cameras[_activeIndex];
        if (cam != null)
        {
            cam.enabled = true;
            cam.gameObject.SetActive(true);
        }

        return cam;
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (disableWhileInMenu == null) return;
        for (int i = 0; i < disableWhileInMenu.Length; i++)
        {
            if (disableWhileInMenu[i] != null)
                disableWhileInMenu[i].SetActive(enabled);
        }
    }

    private void SetAllCamerasEnabled(bool enabled)
    {
        if (cameras == null) return;
        for (int i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i];
            if (cam == null) continue;

            cam.enabled = enabled;
            cam.gameObject.SetActive(true);
        }
    }
}
