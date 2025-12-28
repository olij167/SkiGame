using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeCamera : MonoBehaviour
{
    public List<Camera> cameraList;
    public Camera currentCam;
    public int count;

    private SetParentTransform setParent;
    public Billboard moonBillboard;

    private void Start()
    {
        setParent = FindFirstObjectByType<SetParentTransform>();
        foreach (Camera cam in cameraList)
            if (cam != currentCam) cam.gameObject.SetActive(false);
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            currentCam.gameObject.SetActive(false);

            if (count + 1 < cameraList.Count) count++;
            else count = 0;

            currentCam = cameraList[count];
            setParent.parentTransform = currentCam.transform;

            setParent.SetParent();

            currentCam.gameObject.SetActive(true);

            moonBillboard.camTransform = currentCam.transform;
        }
    }
}
