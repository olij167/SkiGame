using UnityEngine;

namespace Artngame.CommonTools
{
    public class ResolutionChangerSM : MonoBehaviour
    {
        // List of predefined resolutions
        private Resolution[] availableResolutions = new Resolution[]
        {
        new Resolution { width = 1920, height = 1080 },
        new Resolution { width = 1600, height = 900 },
        new Resolution { width = 1280, height = 720 },
        new Resolution { width = 1024, height = 768 },
        new Resolution { width = 800, height = 600 }
        };

        private string[] resolutionLabels;
        private int selectedIndex = 0;

        private void Start()
        {
            // Create labels for the dropdown from the resolution list
            resolutionLabels = new string[availableResolutions.Length];
            for (int i = 0; i < availableResolutions.Length; i++)
            {
                resolutionLabels[i] = $"{availableResolutions[i].width} x {availableResolutions[i].height}";
            }

            // Set default selected index to current resolution if available
            for (int i = 0; i < availableResolutions.Length; i++)
            {
                if (Screen.width == availableResolutions[i].width &&
                    Screen.height == availableResolutions[i].height)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }
        public float yoffsetChnagerButton = 500;
        private void OnGUI()
        {
            GUI.BeginGroup(new Rect(10, 10, 300, 900), "Resolution Changer", GUI.skin.window);

            GUI.Label(new Rect(10, 25, 120, 25), "Select Resolution:");

            selectedIndex = GUI.SelectionGrid(
                new Rect(10, 50, 280, 280),
                selectedIndex,
                resolutionLabels,
                1
            );

            if (GUI.Button(new Rect(10, 280 + yoffsetChnagerButton, 280, 25), "Apply Resolution"))
            {
                ChangeResolution(selectedIndex);
            }

            GUI.EndGroup();
        }

        private void ChangeResolution(int index)
        {
            if (index >= 0 && index < availableResolutions.Length)
            {
                Resolution res = availableResolutions[index];
                Screen.SetResolution(res.width, res.height, FullScreenMode.Windowed);
                Debug.Log($"Resolution changed to: {res.width}x{res.height}");
            }
        }
    }

}