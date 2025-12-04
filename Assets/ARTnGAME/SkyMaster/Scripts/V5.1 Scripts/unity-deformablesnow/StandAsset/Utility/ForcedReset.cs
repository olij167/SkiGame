using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Artngame.SKYMASTER.Utils.CrossPlatformInput;
using UnityEngine.UI;
namespace Artngame.SKYMASTER.Utils
{
    [RequireComponent(typeof(Image))]
    public class ForcedReset : MonoBehaviour
    {
        private void Update()
        {
            // if we have forced a reset ...
            if (CrossPlatformInputManager.GetButtonDown("ResetObject"))
            {
                //... reload the scene
                SceneManager.LoadScene(SceneManager.GetSceneAt(0).name);
            }
        }
    }
}