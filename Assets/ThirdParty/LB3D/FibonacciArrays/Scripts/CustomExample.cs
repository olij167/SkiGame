using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomExample : FibonacciBuilder {

	// Use this for initialization
	void Start () {
        DoSomething();
	}
	
	// Update is called once per frame
	void Update () {
        if (Input.GetKeyDown(KeyCode.Return)) {

            SetPoints(144);
            Create();

        }
	}

    public void DoSomething() {
        //set number of points for array
        SetPoints(10);

        //get said points...
        Vector3[] points = GetPointPositions();

        //do something with points
        foreach (Vector3 p in points) {
            print(p);
        }
    }

}
