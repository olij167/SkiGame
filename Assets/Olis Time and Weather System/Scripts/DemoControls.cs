using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoControls : MonoBehaviour
{
    public GameObject demoUI;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            demoUI.SetActive(!demoUI.activeSelf);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();

        }
    }

}
