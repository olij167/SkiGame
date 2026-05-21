using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProductExample : MonoBehaviour
{

    public GameObject sphereObject;
    public FibonacciBuilder fibonacciBuilder;
    public FibonacciArray fibonacciArray = null;
    public Slider minMaxDots;
    public Slider rot;
    public Toggle latticeTypeSphere;
        
    private float dotsLastValue = 0;
    private float rotationLastValue = 0;
  
    // Update is called once per frame
    void Update()
    {
        UpdateLatticeType();
        UpdateDots();
        UpdateRotation();       
        
    }

    public void UpdateLatticeType() {
             

        if (latticeTypeSphere.isOn)
        {            
            fibonacciBuilder.latticeType = FibonacciBuilder.LatticeType.Sphere;
            sphereObject.SetActive(true);
        }
        else {
            fibonacciBuilder.latticeType = FibonacciBuilder.LatticeType.Disk;
            sphereObject.SetActive(false);
        }
         
        
    }

    public void UpdateDots() {
        if (minMaxDots.value == dotsLastValue) return;
       
        dotsLastValue = minMaxDots.value;
        fibonacciBuilder.points = (int) minMaxDots.value;

        fibonacciBuilder.GenerateLattice(create: true);
     }

    public void UpdateRotation() {

        if (!fibonacciArray) {
            fibonacciArray = GetComponentInChildren<FibonacciArray>();
        }

        if (rot.value == rotationLastValue) return;
        Transform t = fibonacciArray.gameObject.transform;
        t.eulerAngles = new Vector3(t.eulerAngles.x, t.eulerAngles.y, rot.value);
    }

}
